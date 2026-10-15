"""One admitted request per private worker process; stdout is a single JSON reply."""
from __future__ import annotations

# Only standard-library imports before stdin admission. The desktop attaches its
# Windows memory/cleanup job before it writes this request.
import contextlib
import io
import json
import os
from pathlib import Path
import sys
import uuid

MAX_REQUEST = 128 * 1024
MAX_RESPONSE = 2 * 1024 * 1024


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("Duplicate JSON field.")
        result[key] = value
    return result


def check_depth(value, depth=0):
    if depth > 16:
        raise ValueError("JSON is too deeply nested.")
    if isinstance(value, dict):
        for key, item in value.items():
            if len(key) > 1024:
                raise ValueError("JSON field name is too long.")
            check_depth(item, depth + 1)
    elif isinstance(value, list):
        if len(value) > 512:
            raise ValueError("JSON array is too long.")
        for item in value:
            check_depth(item, depth + 1)


def closed(value, allowed):
    if not isinstance(value, dict) or set(value) - set(allowed):
        raise ValueError("Unknown or invalid JSON fields.")


def validate_parameters(action, parameters):
    allowed = {
        "capabilities": set(), "list": set(),
        "inspect": {"path", "key", "start", "rowLimit"}, "browse": {"path", "key", "start", "rowLimit"},
        "train": {"dataset", "task", "algorithm", "features", "target", "scaling", "testFraction", "seed", "hyperparameters", "split", "groupColumn", "folds", "name"},
        "predict": {"modelId", "dataset"}, "export": {"experimentId"}, "describe": {"experimentId"},
        "compare": {"experimentIds"},
    }
    closed(parameters, allowed[action])
    if "dataset" in parameters:
        closed(parameters["dataset"], {"path", "key", "start", "rowLimit"})


def journal(project, action, request_id, state, result=None, code=None):
    """Best-effort bounded content-free journal; it cannot replace the result."""
    try:
        from common import atomic_write, local_path, now
        path = project / "operations.jsonl"
        local_path(str(path), exists=False)
        entry = {"schemaVersion": 1, "operation": action, "operationId": request_id, "state": state, "timestamp": now(), "code": code}
        if isinstance(result, dict):
            entry["experimentId"] = result.get("id") if action == "train" else result.get("experimentId")
            dataset = result.get("dataset")
            entry["datasetId"] = dataset.get("id") if isinstance(dataset, dict) else None
        existing = path.read_bytes() if path.exists() and path.stat().st_size <= 256 * 1024 else b""
        record = json.dumps(entry, separators=(",", ":")).encode("utf-8") + b"\n"
        if len(existing) + len(record) > 256 * 1024:
            existing = b""
        atomic_write(path, existing + record)
    except Exception:
        pass


def main():
    request_id = None
    response = None
    project = None
    action = None
    try:
        raw = sys.stdin.buffer.readline(MAX_REQUEST + 1)
        if len(raw) > MAX_REQUEST or not raw.endswith(b"\n"):
            raise ValueError("Request size or framing is invalid.")
        if sys.stdin.buffer.read(1):
            raise ValueError("Only one request is allowed per worker process.")
        request = json.loads(raw.decode("utf-8"), object_pairs_hook=unique_object, parse_constant=lambda _: (_ for _ in ()).throw(ValueError("Nonfinite JSON")))
        check_depth(request)
        closed(request, {"protocol", "id", "action", "projectDir", "parameters", "limits"})
        if not isinstance(request, dict) or type(request.get("protocol")) is not int or request.get("protocol") != 1:
            raise ValueError("Unsupported protocol.")
        request_id = str(uuid.UUID(request["id"]))
        if request_id != request["id"]:
            raise ValueError("Request identifier is invalid.")
        action = request.get("action")
        if action not in ("capabilities", "inspect", "browse", "train", "list", "predict", "export", "describe", "compare"):
            raise ValueError("Unsupported action.")
        parameters = request.get("parameters")
        if not isinstance(parameters, dict):
            raise ValueError("Parameters must be an object.")
        validate_parameters(action, parameters)
        closed(request.get("limits", {}), {"maxRows", "memoryMiB", "threads"})
        # Environment is also restricted by the desktop. Explicitly disable
        # third-party HDF5 plugins and bound BLAS/OpenMP before scientific imports.
        for name in ("OMP_NUM_THREADS", "OPENBLAS_NUM_THREADS", "MKL_NUM_THREADS", "NUMEXPR_NUM_THREADS"):
            os.environ[name] = "1"
        os.environ["HDF5_PLUGIN_PRELOAD"] = "::"
        os.environ["MPLBACKEND"] = "Agg"
        sys.path.insert(0, str(Path(__file__).resolve().parent))
        from common import LabError, clean, limits, project_path
        resource = limits(request)
        project = project_path(request.get("projectDir"))
        journal(project, action, request_id, "running")
        # Never let warnings or library prints carry dataset values onto the wire.
        # Native stdout is not used by supported model algorithms. No subprocesses
        # or networking are invoked by this worker.
        with open(os.devnull, "w", encoding="utf-8") as sink, contextlib.redirect_stdout(sink), contextlib.redirect_stderr(sink):
            import engine
            handlers = {
                "capabilities": lambda: engine.capabilities(resource),
                "inspect": lambda: engine.inspect(parameters, project, resource),
                "browse": lambda: engine.browse(parameters, resource),
                "train": lambda: engine.train(parameters, project, resource),
                "list": lambda: engine.list_project(project),
                "predict": lambda: engine.predict(parameters, project, resource),
                "export": lambda: engine.export(parameters, project),
                "describe": lambda: engine.describe(parameters, project),
                "compare": lambda: engine.compare(parameters, project),
            }
            result = handlers[action]()
        response = {"protocol": 1, "id": request_id, "ok": True, "result": clean(result)}
        journal(project, action, request_id, "completed", result)
    except ImportError:
        response = error(request_id, "missing_dependency", "The local ML environment is incomplete. Run setup-ml-lab.ps1 to install the pinned optional ML runtime.")
    except MemoryError:
        response = error(request_id, "resource_limit", "The memory limit was reached. Select fewer rows/features or use a smaller model.")
    except Exception as problem:
        if type(problem).__name__ == "LabError" and hasattr(problem, "code"):
            response = error(request_id, problem.code, str(problem))
        elif isinstance(problem, (ValueError, KeyError, TypeError)):
            response = error(request_id, "invalid_input", "The data or configuration is invalid. Check the format, selected columns, split and hyperparameters.")
        elif isinstance(problem, (OSError, EOFError)):
            response = error(request_id, "file_error", "A local file could not be read or saved. Check that it exists, is not corrupted and that disk space is available.")
        else:
            response = error(request_id, "computation_failed", "The ML operation failed. Try a smaller selection and review the data types and algorithm configuration.")
    if project is not None and not response.get("ok"):
        journal(project, action, request_id, "failed", code=response["error"]["code"])
    encoded = json.dumps(response, ensure_ascii=False, allow_nan=False, separators=(",", ":")).encode("utf-8")
    if len(encoded) > MAX_RESPONSE:
        encoded = json.dumps(error(request_id, "response_limit", "The result exceeds the response limit. Its project artifacts were retained; select fewer rows or features."), separators=(",", ":")).encode("utf-8")
    sys.stdout.buffer.write(encoded + b"\n")
    sys.stdout.buffer.flush()
    return 0


def error(request_id, code, message):
    return {"protocol": 1, "id": request_id, "ok": False, "error": {"code": code, "message": message}}


if __name__ == "__main__":
    raise SystemExit(main())
