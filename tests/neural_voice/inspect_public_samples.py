"""Independent stdlib WAV oracle for explicitly saved public neural-voice samples.

No production parser is imported and no private utterance/audio is logged.
"""
import array
import hashlib
import json
from pathlib import Path
import struct
import sys

MAXIMUM_BYTES = 4 * 1024 * 1024
EXPECTED_RATE = 24_000
MAXIMUM_SECONDS = 30


def inspect_wave(raw):
    if not 44 <= len(raw) <= MAXIMUM_BYTES:
        raise ValueError("byte_limit")
    if raw[:4] != b"RIFF" or raw[8:12] != b"WAVE":
        raise ValueError("container")
    if struct.unpack_from("<I", raw, 4)[0] != len(raw) - 8:
        raise ValueError("riff_length")
    cursor, form, pcm, chunks = 12, None, None, []
    while cursor < len(raw):
        if len(chunks) >= 32 or len(raw) - cursor < 8:
            raise ValueError("chunk_header")
        identifier, length = struct.unpack_from("<4sI", raw, cursor)
        if any(byte < 32 or byte > 126 for byte in identifier):
            raise ValueError("chunk_id")
        end = cursor + 8 + length
        padded_end = end + (length & 1)
        if end > len(raw) or padded_end > len(raw):
            raise ValueError("chunk_length")
        body = raw[cursor + 8:end]
        chunks.append({"id": identifier.decode("ascii"), "offset": cursor, "length": length})
        if identifier == b"fmt ":
            if form is not None or pcm is not None or length < 16:
                raise ValueError("format_order")
            form = struct.unpack_from("<HHIIHH", body)
            if form != (1, 1, EXPECTED_RATE, EXPECTED_RATE * 2, 2, 16):
                raise ValueError("pcm_format")
        elif identifier == b"data":
            if form is None or pcm is not None or not length or length % 2:
                raise ValueError("data_order_alignment")
            pcm = body
        cursor = padded_end
    if cursor != len(raw) or form is None or pcm is None:
        raise ValueError("incomplete")
    samples = array.array("h")
    samples.frombytes(pcm)
    if sys.byteorder != "little":
        samples.byteswap()
    frame_count = len(samples)
    if frame_count > EXPECTED_RATE * MAXIMUM_SECONDS:
        raise ValueError("duration_limit")
    if not any(samples):
        raise ValueError("silent_samples")
    minimum, maximum = min(samples), max(samples)
    return {
        "totalBytes": len(raw), "dataBytes": len(pcm), "sampleRate": EXPECTED_RATE,
        "channels": 1, "bitsPerSample": 16, "frames": frame_count,
        "durationSeconds": frame_count / EXPECTED_RATE,
        "minimumSample": minimum, "maximumSample": maximum,
        "endpointSamples": sum(value in (-32768, 32767) for value in samples),
        "nonzeroSamples": sum(value != 0 for value in samples),
        "sha256": hashlib.sha256(raw).hexdigest(), "chunks": chunks,
    }


def main(paths):
    if not paths:
        raise SystemExit("Pass explicitly saved public sample WAV paths.")
    results = []
    for supplied in paths:
        path = Path(supplied).resolve(strict=True)
        if path.suffix.lower() != ".wav" or not path.is_file():
            raise ValueError("public_wav_required")
        if path.stat().st_size > MAXIMUM_BYTES:
            raise ValueError("byte_limit")
        with path.open("rb") as stream:
            raw = stream.read(MAXIMUM_BYTES + 1)
        results.append({"sample": path.name, **inspect_wave(raw)})
    print(json.dumps({"oracle": "independent-stdlib-pcm16-24000", "samples": results}, indent=2))


if __name__ == "__main__":
    main(sys.argv[1:])
