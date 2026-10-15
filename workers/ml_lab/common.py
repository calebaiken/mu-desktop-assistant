"""Shared bounded JSON, local paths and artifact persistence for Mu ML Lab."""
from __future__ import annotations

import hashlib
import json
import math
import os
from pathlib import Path
import tempfile
from datetime import datetime, timezone

SCHEMA_VERSION = 1
MAX_COLUMNS = 128
MAX_TEXT = 512


class LabError(Exception):
    def __init__(self, code: str, message: str):
        self.code = code
        super().__init__(message)


def fail(message: str, code: str = "invalid_input"):
    raise LabError(code, message)


def now() -> str:
    return datetime.now(timezone.utc).isoformat()


def clean(value):
    """JSON values only; scientific NaN/Infinity is represented by null."""
    if isinstance(value, dict):
        return {str(k): clean(v) for k, v in value.items()}
    if isinstance(value, (list, tuple)):
        return [clean(v) for v in value]
    if hasattr(value, "item"):
        return clean(value.item())
    if isinstance(value, float) and not math.isfinite(value):
        return None
    if value is None or isinstance(value, (str, bool, int, float)):
        return value
    return str(value)


def json_bytes(value) -> bytes:
    return json.dumps(clean(value), ensure_ascii=False, allow_nan=False, indent=2).encode("utf-8")


def local_path(value: str, *, exists: bool = True) -> Path:
    if not isinstance(value, str) or not value or len(value) > 1024 or any(ord(c) < 32 for c in value):
        fail("Choose an absolute local file path.")
    path = Path(value)
    if not path.is_absolute() or value.startswith(("\\\\", "//")) or (os.name == "nt" and ":" in value[2:]):
        fail("Only absolute local paths are supported.")
    # Refuse symbolic links/junctions rather than following a source to another location.
    for part in (path, *path.parents):
        if part.is_symlink() or (hasattr(part, "is_junction") and part.is_junction()):
            fail("Paths containing links or junctions are not supported.", "unsafe_path")
    if exists and not path.is_file():
        fail("The source file is missing or is not a regular file.", "missing_source")
    return path


def project_path(value: str) -> Path:
    path = local_path(value, exists=False)
    if path.exists() and not path.is_dir():
        fail("The project location must be a directory.")
    path.mkdir(parents=True, exist_ok=True)
    return path


def atomic_write(path: Path, contents: bytes):
    if len(contents) > 32 * 1024 * 1024:
        fail("The metadata artifact exceeds the 32 MiB limit.", "resource_limit")
    local_path(str(path), exists=False)
    path.parent.mkdir(parents=True, exist_ok=True)
    name = None
    try:
        with tempfile.NamedTemporaryFile(dir=path.parent, prefix=".mu-", suffix=".tmp", delete=False) as stream:
            name = stream.name
            stream.write(contents)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(name, path)
        name = None
    finally:
        if name is not None:
            Path(name).unlink(missing_ok=True)


def write_json(path: Path, value):
    atomic_write(path, json_bytes(value))


def read_json(path: Path, limit=32 * 1024 * 1024):
    local_path(str(path))
    if path.stat().st_size > limit:
        fail("The metadata file exceeds its size limit.", "resource_limit")
    with path.open(encoding="utf-8") as stream:
        return json.load(stream)


def hash_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def bounded_int(value, default: int, low: int, high: int, name: str) -> int:
    if value is None:
        return default
    if type(value) is not int or not low <= value <= high:
        fail(f"{name} must be an integer from {low} to {high}.")
    return value


def limits(request: dict) -> dict:
    source = request.get("limits") or {}
    if not isinstance(source, dict):
        fail("Compute limits must be an object.")
    return {
        "memory_mb": bounded_int(source.get("memoryMiB"), 1024, 256, 4096, "memoryMiB"),
        "max_rows": bounded_int(source.get("maxRows"), 10000, 10, 100000, "maxRows"),
        "preview_rows": 1000,
        "workers": bounded_int(source.get("threads"), 2, 1, 8, "threads"),
    }


def identifier(value: str) -> str:
    import uuid
    try:
        return str(uuid.UUID(value))
    except (ValueError, TypeError, AttributeError):
        fail("Choose a valid saved experiment identifier.")
