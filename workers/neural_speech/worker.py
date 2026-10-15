"""One offline neural speech operation; no speech files or transcript logging."""
from __future__ import annotations

# Standard library only before complete command admission and parent's job assignment.
import base64
import contextlib
import ctypes
import importlib.metadata
import io
import json
import logging
import os
from pathlib import Path
import sys
import uuid
import wave

MAX_REQUEST_BYTES = 16384
MAX_SMALL_EVENT_BYTES = 65536
MAX_PROBE_EVENT_BYTES = 6 * 1024 * 1024
MAX_TEXT_CHARACTERS = 2000
SAMPLE_RATE = 24000
MAX_SECONDS = 30
MAX_SAMPLES = SAMPLE_RATE * MAX_SECONDS
MAX_SYNTHESIZED_SAMPLES = SAMPLE_RATE * 59 // 2  # 29.5s leaves time for the terminal event.
MAX_WAVE_BYTES = 44 + MAX_SAMPLES * 2
VOICES = (
    {"id": "kokoro:af_heart", "name": "Heart — Neural (US)", "culture": "en-US"},
    {"id": "kokoro:af_bella", "name": "Bella — Neural (US)", "culture": "en-US"},
    {"id": "kokoro:am_michael", "name": "Michael — Neural (US)", "culture": "en-US"},
    {"id": "kokoro:bm_george", "name": "George — Neural (UK)", "culture": "en-GB"},
)
VOICE_IDS = frozenset(voice["id"] for voice in VOICES)
RUNTIME_VERSIONS = {"kokoro-onnx": "0.6.1", "onnxruntime": "1.23.2", "numpy": "2.3.3",
                    "phonemizer": "3.4.0", "espeakng-loader": "0.2.4"}


def unique(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("Duplicate field")
        result[key] = value
    return result


def parse_request(raw):
    if not isinstance(raw, bytes) or not raw.endswith(b"\n") or len(raw) > MAX_REQUEST_BYTES:
        raise ValueError("Invalid frame")
    value = json.loads(raw.decode("utf-8"), object_pairs_hook=unique,
                       parse_constant=lambda _: (_ for _ in ()).throw(ValueError("Nonfinite JSON")))
    if not isinstance(value, dict) or set(value) != {"protocol", "id", "kind", "modelDir", "request"}:
        raise ValueError("Invalid command")
    if type(value["protocol"]) is not int or value["protocol"] != 1:
        raise ValueError("Invalid protocol")
    if not isinstance(value["id"], str) or str(uuid.UUID(value["id"])) != value["id"] or uuid.UUID(value["id"]).int == 0:
        raise ValueError("Invalid identity")
    if value["kind"] not in ("voices", "speak", "probe") or not isinstance(value["modelDir"], str):
        raise ValueError("Invalid operation")
    request = value["request"]
    if value["kind"] == "voices":
        if request is not None:
            raise ValueError("Unexpected speech")
    else:
        if not isinstance(request, dict) or set(request) != {"text", "voiceId", "rate"}:
            raise ValueError("Invalid speech")
        text = request["text"]
        if not isinstance(text, str) or not text.strip() or len(text) > MAX_TEXT_CHARACTERS:
            raise ValueError("Invalid text")
        if any(ord(char) == 0 or 0xD800 <= ord(char) <= 0xDFFF for char in text):
            raise ValueError("Invalid Unicode")
        if len(text.encode("utf-16-le")) // 2 > MAX_TEXT_CHARACTERS:
            raise ValueError("Invalid text size")
        if not isinstance(request["voiceId"], str) or request["voiceId"] not in VOICE_IDS or type(request["rate"]) is not int or not -5 <= request["rate"] <= 5:
            raise ValueError("Invalid voice")
    return value


def configure_offline():
    for name in list(os.environ):
        if name.startswith(("HF_", "HUGGING", "KOKORO_", "ORT_", "PHONEMIZER_", "ESPEAK_")) or name in ("PYTHONPATH", "PYTHONHOME", "ONNX_PROVIDER"):
            os.environ.pop(name, None)
    os.environ.update({"HF_HUB_OFFLINE": "1", "HF_HUB_DISABLE_TELEMETRY": "1", "DO_NOT_TRACK": "1",
                       "CUDA_VISIBLE_DEVICES": "-1", "OMP_NUM_THREADS": "2", "MKL_NUM_THREADS": "2",
                       "OPENBLAS_NUM_THREADS": "2", "NUMEXPR_NUM_THREADS": "2"})
    logging.disable(logging.CRITICAL)


def block_network(event, args):
    if event in ("socket.connect", "socket.getaddrinfo", "socket.gethostbyname", "socket.gethostbyaddr",
                 "socket.sendto", "subprocess.Popen", "os.system"):
        raise PermissionError("Neural speech worker network and child processes are disabled")


def runtime_preflight():
    from assets import SpeechError
    for name, expected in RUNTIME_VERSIONS.items():
        try:
            actual = importlib.metadata.version(name)
        except importlib.metadata.PackageNotFoundError:
            raise SpeechError("runtime_missing") from None
        if actual != expected:
            raise SpeechError("runtime_missing")


def memory_preflight():
    from assets import SpeechError
    if os.name != "nt":
        raise SpeechError("unsupported_platform")

    class MemoryStatus(ctypes.Structure):
        _fields_ = [("length", ctypes.c_uint32), ("load", ctypes.c_uint32)] + [
            (name, ctypes.c_uint64) for name in ("total_physical", "available_physical", "total_commit", "available_commit",
                                                "total_virtual", "available_virtual", "extended_virtual")]

    class PerformanceInfo(ctypes.Structure):
        _fields_ = [("size", ctypes.c_uint32)] + [
            (name, ctypes.c_size_t) for name in ("commit_total", "commit_limit", "commit_peak", "physical_total",
                                               "physical_available", "system_cache", "kernel_total", "kernel_paged",
                                               "kernel_nonpaged", "page_size")] + [
            (name, ctypes.c_uint32) for name in ("handles", "processes", "threads")]

    status = MemoryStatus()
    status.length = ctypes.sizeof(status)
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    get_memory = kernel.GlobalMemoryStatusEx
    get_memory.argtypes = [ctypes.POINTER(MemoryStatus)]
    get_memory.restype = ctypes.c_int
    info = PerformanceInfo()
    info.size = ctypes.sizeof(info)
    psapi = ctypes.WinDLL("psapi", use_last_error=True)
    get_info = psapi.GetPerformanceInfo
    get_info.argtypes = [ctypes.POINTER(PerformanceInfo), ctypes.c_uint32]
    get_info.restype = ctypes.c_int
    if not get_memory(ctypes.byref(status)) or not get_info(ctypes.byref(info), info.size):
        raise SpeechError("memory_limit")
    system_available = (info.commit_limit - info.commit_total) * info.page_size
    if (info.page_size <= 0 or info.commit_total > info.commit_limit or status.available_physical < 512 * 1024 * 1024
            or system_available < 1536 * 1024 * 1024 or status.available_commit < 1024 * 1024 * 1024):
        raise SpeechError("memory_limit")


def synthesize(request, model_dir):
    from assets import SpeechError
    memory_preflight()
    import numpy as np
    import onnxruntime as rt
    import espeakng_loader
    from kokoro_onnx import Kokoro
    from kokoro_onnx.config import EspeakConfig
    from kokoro_onnx.chunker import pause_after, split_phonemes
    rt.disable_telemetry_events()
    options = rt.SessionOptions()
    options.intra_op_num_threads = 2
    options.inter_op_num_threads = 1
    options.execution_mode = rt.ExecutionMode.ORT_SEQUENTIAL
    options.log_severity_level = 4
    options.enable_cpu_mem_arena = False
    session = rt.InferenceSession(str(model_dir / "kokoro-v1.0.onnx"), sess_options=options,
                                  providers=["CPUExecutionProvider"])
    if session.get_providers() != ["CPUExecutionProvider"]:
        raise SpeechError("runtime_missing")
    # Admit the packaged phonemizer DLL explicitly. If it is unavailable, do not
    # trigger Kokoro's system-wide library search/fallback.
    library = espeakng_loader.get_library_path()
    ctypes.CDLL(library)
    phonemizer = EspeakConfig(lib_path=library, data_path=espeakng_loader.get_data_path())
    engine = Kokoro.from_session(session, str(model_dir / "voices-v1.0.bin"), espeak_config=phonemizer)
    voice = request["voiceId"].split(":", 1)[1]
    language = "en-gb" if voice == "bm_george" else "en-us"
    speed = 1.0 + request["rate"] * 0.05
    phonemes = " ".join(engine.tokenizer.phonemize(request["text"], language).split())
    batches = split_phonemes(phonemes, 160)
    if not batches:
        raise SpeechError("invalid_request")
    parts = []
    remaining = MAX_SYNTHESIZED_SAMPLES
    for index, batch in enumerate(batches):
        audio, rate = engine.create(batch, voice=voice, speed=speed, lang=language, is_phonemes=True)
        audio = np.asarray(audio)
        if rate != SAMPLE_RATE or audio.ndim != 1 or audio.size == 0 or not np.isfinite(audio).all():
            raise SpeechError("audio_limit")
        # Only one bounded phoneme batch is materialized at a time. Keep at most
        # 30 seconds for this explicitly capped read-aloud operation.
        length = min(remaining, len(audio))
        parts.append(np.clip(audio[:length], -1, 1).astype(np.float32, copy=False))
        remaining -= length
        if remaining <= 0:
            break
        if index + 1 < len(batches):
            pause = min(remaining, int(pause_after(batch, .25, .1) * SAMPLE_RATE))
            if pause:
                parts.append(np.zeros(pause, dtype=np.float32))
                remaining -= pause
    return samples_to_wave(np.concatenate(parts))


def samples_to_wave(samples):
    """Validate bounded floating point mono audio and encode ordinary PCM16 WAV."""
    from assets import SpeechError
    import numpy as np
    samples = np.asarray(samples)
    if (samples.ndim != 1 or not samples.size or samples.size > MAX_SAMPLES
            or samples.dtype.kind not in "fi" or not np.isfinite(samples).all() or not np.any(samples)):
        raise SpeechError("audio_limit")
    integers = (np.clip(samples, -1, 1) * 32767).round().astype("<i2")
    if not np.any(integers):
        raise SpeechError("audio_limit")
    pcm = integers.tobytes()
    with io.BytesIO() as stream:
        with wave.open(stream, "wb") as writer:
            writer.setnchannels(1)
            writer.setsampwidth(2)
            writer.setframerate(SAMPLE_RATE)
            writer.writeframes(pcm)
        result = stream.getvalue()
    if not 44 < len(result) <= MAX_WAVE_BYTES:
        raise SpeechError("audio_limit")
    return result


def emit(operation_id, kind, **payload):
    event = {"protocol": 1, "id": operation_id, "kind": kind, **payload}
    raw = (json.dumps(event, ensure_ascii=False, allow_nan=False, separators=(",", ":")) + "\n").encode("utf-8")
    limit = MAX_PROBE_EVENT_BYTES if kind == "probe" else MAX_SMALL_EVENT_BYTES
    if len(raw) > limit:
        raise ValueError("Event limit")
    sys.stdout.buffer.write(raw)
    sys.stdout.buffer.flush()


def main():
    command = None
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    from assets import SpeechError, verified_assets
    try:
        if not sys.stdin.isatty() and not sys.stdout.isatty():
            raw = sys.stdin.buffer.readline(MAX_REQUEST_BYTES + 1)
            command = parse_request(raw)
            # One complete request plus EOF, never an interactive command stream.
            if sys.stdin.buffer.read(1):
                raise ValueError("Unexpected command tail")
        else:
            return 2
        configure_offline()
        sys.addaudithook(block_network)
        runtime_preflight()
        with verified_assets(command["modelDir"]) as model_dir:
            if command["kind"] == "voices":
                emit(command["id"], "voices", voices=VOICES)
                return 0
            # Native/library warnings must never reveal text through stderr.
            class DiscardOutput(io.TextIOBase):
                def write(self, text):
                    return len(text)

            with contextlib.redirect_stdout(DiscardOutput()), contextlib.redirect_stderr(DiscardOutput()):
                audio = synthesize(command["request"], model_dir)
            if command["kind"] == "probe":
                emit(command["id"], "probe", waveBase64=base64.b64encode(audio).decode("ascii"))
            else:
                import winsound
                emit(command["id"], "speaking")
                winsound.PlaySound(audio, winsound.SND_MEMORY | winsound.SND_NODEFAULT | winsound.SND_SYNC)
                emit(command["id"], "completed")
        return 0
    except Exception as error:
        if command is not None:
            code = error.code if isinstance(error, SpeechError) else "invalid_request" if isinstance(error, (ValueError, UnicodeError)) else "inference_failed"
            emit(command["id"], "failed", failure=code)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
