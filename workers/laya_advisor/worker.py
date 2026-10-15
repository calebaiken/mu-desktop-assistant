"""One local advisory choice. It cannot authorize or execute an ML action."""
from __future__ import annotations

# Standard library only until complete stdin admission. The desktop attaches its
# owned Windows job and memory ceiling before sending this request.
import contextlib
import ctypes
import importlib.metadata
import json
import math
import os
from pathlib import Path
import sys
import time
import uuid

MAX_REQUEST_BYTES = 16384
MAX_RESPONSE_BYTES = 65536
MAX_PROMPT_CHARACTERS = 2000
MAX_TOKENS = 512
HEAD_TOKENS = 192
LABELS = ("regression", "classification", "clustering", "reduction", "anomaly", "other")
MODEL_REVISION = "55cf4c4ebb4ebe31b2550e8bdf3bd21b99753851"
QUESTION = {
    "type": "choice",
    "instructions": "Which machine-learning task best matches the user's primary requested outcome? Choose other if the request is unrelated or too ambiguous.",
    "criteria": {
        "regression": "predict a continuous numeric target, amount, amplitude, or measured value",
        "classification": "predict a known discrete category or class label using labeled examples",
        "clustering": "discover groups or populations in unlabeled data",
        "reduction": "project features into fewer dimensions for visualization, such as PCA",
        "anomaly": "rank or detect unusual records, outliers, or rare detector events",
        "other": "not an ML modeling task, unclear goal, or unrelated conversation",
    },
}
QUESTION_ID = "ml_task"
RUNTIME_VERSIONS = {"laya": "0.3.21", "torch": "2.6.0+cpu", "transformers": "4.51.3",
                    "safetensors": "0.6.2", "huggingface-hub": "0.35.3", "tokenizers": "0.21.4", "numpy": "2.3.3"}


def _unique(pairs):
    result = {}
    for name, value in pairs:
        if name in result:
            raise ValueError("Duplicate request field")
        result[name] = value
    return result


def parse_request(raw):
    if not isinstance(raw, bytes) or len(raw) > MAX_REQUEST_BYTES or not raw.endswith(b"\n"):
        raise ValueError("Invalid request framing")
    request = json.loads(raw.decode("utf-8"), object_pairs_hook=_unique,
                         parse_constant=lambda _: (_ for _ in ()).throw(ValueError("Nonfinite JSON")))
    if not isinstance(request, dict) or set(request) != {"protocol", "id", "prompt", "modelDir"}:
        raise ValueError("Invalid request fields")
    if type(request["protocol"]) is not int or request["protocol"] != 1:
        raise ValueError("Unsupported protocol")
    if not isinstance(request["id"], str) or str(uuid.UUID(request["id"])) != request["id"]:
        raise ValueError("Invalid request identity")
    prompt = request["prompt"]
    if not isinstance(prompt, str) or not prompt.strip() or len(prompt) > MAX_PROMPT_CHARACTERS:
        raise ValueError("Invalid prompt size")
    if any((ord(c) < 32 and c not in "\n\r\t") or 0xD800 <= ord(c) <= 0xDFFF for c in prompt):
        raise ValueError("Invalid prompt text")
    if not isinstance(request["modelDir"], str):
        raise ValueError("Invalid model path")
    return request


def normalize_answer(answer, *, truncated, revision, elapsed):
    if type(truncated) is not bool or revision != MODEL_REVISION or type(elapsed) not in (int, float) or not math.isfinite(elapsed) or elapsed < 0:
        raise ValueError("Invalid inference provenance")
    if not isinstance(answer, dict) or answer.get("type") != "choice" or answer.get("choice") not in LABELS:
        raise ValueError("Invalid LAYA category")
    probabilities = answer.get("probabilities")
    if not isinstance(probabilities, dict) or set(probabilities) != set(LABELS):
        raise ValueError("Invalid LAYA distribution")
    if any(type(value) not in (int, float) or not math.isfinite(value) or not 0 <= value <= 1 for value in probabilities.values()):
        raise ValueError("Invalid LAYA probability")
    total = sum(probabilities.values())
    # LAYA exposes four-decimal probabilities; permit only their rounding error.
    if abs(total - 1) > .001 or total <= 0:
        raise ValueError("Invalid LAYA probability mass")
    confidence = answer.get("answer_confidence")
    if type(confidence) not in (int, float) or not math.isfinite(confidence) or not 0 <= confidence <= 1:
        raise ValueError("Invalid LAYA confidence")
    category = answer["choice"]
    if probabilities[category] < max(probabilities.values()) - .0001 or abs(confidence - max(probabilities.values())) > .001:
        raise ValueError("Inconsistent LAYA answer")
    warnings = ["Advisory task category only. LAYA cannot authorize, execute or validate a scientific result.",
                "These model scores are not calibrated for Mu's scientific ML tasks; confidence is not measured accuracy.",
                "This pinned checkpoint is intended for English requests.",
                "Inference uses the checkpoint's float16 weights and CPU float16 autocast to limit memory; scores may differ from float32 execution."]
    if truncated:
        warnings.append("The prompt exceeded the checkpoint's token budget. The category is based on a truncated prefix; shorten the request before using this advice.")
    return {"category": category, "confidence": float(confidence),
            "probabilities": {label: float(probabilities[label]) for label in LABELS},
            "truncated": bool(truncated), "modelRevision": revision, "elapsedSeconds": elapsed, "warnings": warnings}


def configure_offline_environment():
    for key in list(os.environ):
        if key.startswith(("HF_", "HUGGING", "LAYA_", "TORCH_")) or key in ("TRANSFORMERS_CACHE", "PYTHONPATH", "PYTHONHOME", "TOKENIZERS_PARALLELISM"):
            os.environ.pop(key, None)
    os.environ.update({"HF_HUB_OFFLINE": "1", "TRANSFORMERS_OFFLINE": "1", "HF_HUB_DISABLE_TELEMETRY": "1",
                       "HF_HUB_DISABLE_IMPLICIT_TOKEN": "1", "HF_HUB_DISABLE_XET": "1", "DO_NOT_TRACK": "1",
                       "TOKENIZERS_PARALLELISM": "false", "CUDA_VISIBLE_DEVICES": "-1",
                       "OMP_NUM_THREADS": "2", "MKL_NUM_THREADS": "2", "OPENBLAS_NUM_THREADS": "2"})


def block_network(event, args):
    # A local-only dependency load must never silently fall back to Hub lookup.
    # Audit denial covers DNS and connect even if a library ignores offline flags.
    if event in ("socket.connect", "socket.getaddrinfo", "socket.gethostbyname", "socket.gethostbyaddr", "socket.sendto"):
        raise PermissionError("LAYA worker network access is disabled")


def memory_preflight():
    """Require headroom before importing the optional native runtime."""
    from assets import AdvisorError
    if os.name != "nt":
        raise AdvisorError("unsupported_platform", "This reviewed LAYA worker currently supports Windows only.")

    class MemoryStatus(ctypes.Structure):
        _fields_ = [("length", ctypes.c_uint32), ("load", ctypes.c_uint32)] + [
            (name, ctypes.c_uint64) for name in ("total_physical", "available_physical", "total_commit", "available_commit",
                                                "total_virtual", "available_virtual", "extended_virtual")]
    status = MemoryStatus()
    status.length = ctypes.sizeof(status)
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    query = kernel.GlobalMemoryStatusEx
    query.argtypes = [ctypes.POINTER(MemoryStatus)]
    query.restype = ctypes.c_int
    if not query(ctypes.byref(status)):
        raise AdvisorError("memory_status", "Windows memory availability could not be checked. Retry when other compute jobs have finished.")

    class PerformanceInfo(ctypes.Structure):
        _fields_ = [("size", ctypes.c_uint32)] + [(name, ctypes.c_size_t) for name in
            ("commit_total", "commit_limit", "commit_peak", "physical_total", "physical_available", "system_cache",
             "kernel_total", "kernel_paged", "kernel_nonpaged", "page_size")] + [
            (name, ctypes.c_uint32) for name in ("handle_count", "process_count", "thread_count")]
    performance = PerformanceInfo()
    performance.size = ctypes.sizeof(performance)
    psapi = ctypes.WinDLL("psapi", use_last_error=True)
    query_performance = psapi.GetPerformanceInfo
    query_performance.argtypes = [ctypes.POINTER(PerformanceInfo), ctypes.c_uint32]
    query_performance.restype = ctypes.c_int
    if not query_performance(ctypes.byref(performance), performance.size) or performance.page_size == 0 or performance.commit_total > performance.commit_limit:
        raise AdvisorError("memory_status", "Windows commit availability could not be checked. Retry when other compute jobs have finished.")
    # MEMORYSTATUSEX's available commit can be limited by this process's job.
    # Requiring the entire 3 GiB job ceiling there would reject every request
    # after startup. GetPerformanceInfo reports the independent system budget.
    available_system_commit = (performance.commit_limit - performance.commit_total) * performance.page_size
    if (status.available_physical < 1536 * 1024**2 or available_system_commit < 3072 * 1024**2
            or status.available_commit < 2560 * 1024**2):
        raise AdvisorError("memory_limit", "Optional LAYA needs at least 1.5 GiB available RAM and 3 GiB Windows commit headroom. Close other compute jobs and retry; Mu can continue without this advisor.")


def run_advice(request):
    from assets import AdvisorError, MODEL_REVISION, verified_assets
    started = time.perf_counter()
    configure_offline_environment()
    sys.addaudithook(block_network)
    for package, expected in RUNTIME_VERSIONS.items():
        try:
            actual = importlib.metadata.version(package)
        except importlib.metadata.PackageNotFoundError:
            raise AdvisorError("missing_dependency", "The optional LAYA CPU runtime is incomplete. Run scripts/setup-laya.ps1.") from None
        if actual != expected:
            raise AdvisorError("runtime_version", "The optional LAYA runtime differs from the reviewed versions. Run scripts/setup-laya.ps1.")
    with verified_assets(request["modelDir"]) as (model_dir, manifest):
        memory_preflight()
        import torch
        import laya
        from laya.common import build_sequence, encode_text
        torch.set_num_threads(2)
        torch.set_num_interop_threads(1)
        if torch.version.cuda is not None:
            raise AdvisorError("runtime_version", "Use the reviewed optional CPU-only LAYA runtime.")
        # All model configuration/tokenizer bytes are pinned before these library
        # calls. No arbitrary remote-code, checkpoint class or hook is accepted.
        # The pinned safetensors contain F16 weights (apart from a tiny temperature
        # buffer). Avoid allocating a second full F32 parameter copy during load.
        # This is a public PyTorch dtype policy scoped to this isolated worker;
        # no library monkeypatch, alternate checkpoint or lossy quantizer is used.
        previous_dtype = torch.get_default_dtype()
        try:
            torch.set_default_dtype(torch.float16)
            agent = laya.load(str(model_dir), device="cpu", compile=False, fast=False,
                              expected_sha256={key: value["sha256"] for key, value in manifest["files"].items()})
        finally:
            torch.set_default_dtype(previous_dtype)
        if any(parameter.dtype != torch.float16 for parameter in agent.model.parameters()):
            raise AdvisorError("precision_policy", "The local runtime could not honor LAYA's reviewed half-precision memory policy.")
        internal_question = {"t": "choice", "ins": QUESTION["instructions"], "crit": QUESTION["criteria"]}
        empty_sequence, markers, option_stats = build_sequence(agent.tok, "", internal_question, MAX_TOKENS, HEAD_TOKENS, state_ids=[], return_stats=True)
        token_ids = encode_text(agent.tok, request["prompt"].replace(agent.tok.mask_token, " "), add_special_tokens=False)["input_ids"]
        truncated = len(token_ids) > MAX_TOKENS - len(empty_sequence)
        if len(markers) != len(LABELS) or option_stats["options_distinct"] != len(LABELS):
            raise AdvisorError("question_budget", "The LAYA category rubric exceeded its reviewed token budget.")
        with torch.inference_mode(), torch.autocast(device_type="cpu", dtype=torch.float16):
            result = agent.predict(request["prompt"], {QUESTION_ID: QUESTION}, max_len=MAX_TOKENS, head_max_len=HEAD_TOKENS)
        return normalize_answer(result["answers"][QUESTION_ID], truncated=truncated, revision=MODEL_REVISION,
                                elapsed=time.perf_counter() - started)


def main():
    request_id = None
    try:
        raw = sys.stdin.buffer.readline(MAX_REQUEST_BYTES + 1)
        if sys.stdin.buffer.read(1):
            raise ValueError("Only one request is permitted")
        request = parse_request(raw)
        request_id = request["id"]
        sys.path.insert(0, str(Path(__file__).resolve().parent))
        with open(os.devnull, "w", encoding="utf-8") as sink, contextlib.redirect_stdout(sink), contextlib.redirect_stderr(sink):
            result = run_advice(request)
        response = {"protocol": 1, "id": request_id, "ok": True, "result": result}
    except Exception as problem:
        if type(problem).__name__ == "AdvisorError" and hasattr(problem, "code"):
            code, message = problem.code, str(problem)
        elif isinstance(problem, MemoryError):
            code, message = "memory_limit", "LAYA could not fit in its memory budget. Close other compute jobs or keep using Mu without this optional advisor."
        elif isinstance(problem, (ValueError, TypeError, KeyError, RecursionError, UnicodeError)):
            code, message = "invalid_request", "LAYA received invalid input or an invalid typed model response. Use a shorter plain-language ML task description."
        elif isinstance(problem, ImportError):
            code, message = "missing_dependency", "The optional LAYA runtime is incomplete. Run scripts/setup-laya.ps1."
        elif isinstance(problem, OSError):
            code, message = "local_file", "LAYA local assets could not be read. Check the optional installation; no online fallback was attempted."
        else:
            code, message = "inference_failed", "The optional local LAYA advisor could not finish. Mu chat and explicit ML controls remain available."
        response = {"protocol": 1, "id": request_id, "ok": False, "error": {"code": code, "message": message}}
    encoded = json.dumps(response, ensure_ascii=False, allow_nan=False, separators=(",", ":")).encode("utf-8")
    if len(encoded) > MAX_RESPONSE_BYTES:
        encoded = json.dumps({"protocol": 1, "id": request_id, "ok": False, "error": {"code": "output_limit", "message": "LAYA returned an oversized result."}}, separators=(",", ":")).encode("utf-8")
    sys.stdout.buffer.write(encoded + b"\n")
    sys.stdout.buffer.flush()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
