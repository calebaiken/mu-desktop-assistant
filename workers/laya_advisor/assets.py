"""Admission and read-only leases for one reviewed local LAYA checkpoint."""
from __future__ import annotations

import contextlib
import ctypes
import hashlib
import json
import os
from pathlib import Path

MODEL_REVISION = "55cf4c4ebb4ebe31b2550e8bdf3bd21b99753851"
EXPECTED_FILES = {"model.safetensors", "rl_agent_config.json", "encoder/config.json", "tokenizer/tokenizer.json", "tokenizer/tokenizer_config.json"}


class AdvisorError(Exception):
    def __init__(self, code, message):
        super().__init__(message)
        self.code = code


def local_directory(value):
    if not isinstance(value, str) or not value or len(value) > 2048 or any(ord(c) < 32 for c in value):
        raise AdvisorError("invalid_request", "Choose an absolute local LAYA model directory.")
    path = Path(value)
    if not path.is_absolute() or value.startswith(("\\\\", "//")) or (os.name == "nt" and ":" in value[2:]):
        raise AdvisorError("invalid_request", "LAYA model paths must be ordinary local paths.")
    for part in (path, *path.parents):
        if part.is_symlink() or (hasattr(part, "is_junction") and part.is_junction()):
            raise AdvisorError("unsafe_path", "Linked LAYA model paths are not supported.")
    return path


def read_manifest():
    path = Path(__file__).with_name("model-manifest.json")
    if path.stat().st_size > 16384:
        raise AdvisorError("manifest", "The bundled LAYA integrity manifest is invalid.")
    manifest = json.loads(path.read_text(encoding="utf-8"))
    if manifest.get("schemaVersion") != 1 or manifest.get("revision") != MODEL_REVISION or set(manifest.get("files", {})) != EXPECTED_FILES:
        raise AdvisorError("manifest", "The bundled LAYA integrity manifest is incompatible.")
    return manifest


def _open_leased(path):
    if os.name != "nt":
        return path.open("rb")
    # FILE_SHARE_READ only: the library may read these files, but they cannot be
    # replaced, renamed or written while the admitted inference owns its leases.
    import msvcrt
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    create = kernel.CreateFileW
    create.argtypes = [ctypes.c_wchar_p, ctypes.c_uint32, ctypes.c_uint32, ctypes.c_void_p,
                       ctypes.c_uint32, ctypes.c_uint32, ctypes.c_void_p]
    create.restype = ctypes.c_void_p
    handle = create(str(path), 0x80000000, 1, None, 3, 0x08000000, None)
    if handle == ctypes.c_void_p(-1).value:
        raise OSError("Model lease unavailable")
    try:
        descriptor = msvcrt.open_osfhandle(handle, os.O_RDONLY | os.O_BINARY)
    except Exception:
        close = kernel.CloseHandle
        close.argtypes = [ctypes.c_void_p]
        close.restype = ctypes.c_int
        close(handle)
        raise
    return os.fdopen(descriptor, "rb")


@contextlib.contextmanager
def verified_assets(directory):
    path = local_directory(str(directory))
    manifest = read_manifest()
    if not path.is_dir():
        raise AdvisorError("missing_model", "Optional LAYA assets are missing. Run scripts/setup-laya.ps1, then retry.")
    with contextlib.ExitStack() as leases:
        actual = set()
        # The reviewed layout is deliberately shallow and closed. Never follow
        # untrusted nested folders to discover an arbitrary checkpoint.
        for entry in path.iterdir():
            local_directory(str(entry))
            if entry.is_dir():
                if entry.name not in ("encoder", "tokenizer"):
                    raise AdvisorError("model_integrity", "Unexpected files are present in the local LAYA checkpoint.")
                for child in entry.iterdir():
                    local_directory(str(child))
                    if not child.is_file():
                        raise AdvisorError("model_integrity", "LAYA assets must be regular files in the reviewed layout.")
                    actual.add(entry.name + "/" + child.name)
                    if len(actual) > len(EXPECTED_FILES):
                        raise AdvisorError("model_integrity", "Unexpected files are present in the local LAYA checkpoint.")
            else:
                actual.add(entry.name)
            if len(actual) > len(EXPECTED_FILES):
                raise AdvisorError("model_integrity", "Unexpected files are present in the local LAYA checkpoint.")
        if actual != EXPECTED_FILES:
            raise AdvisorError("model_integrity", "The local LAYA checkpoint has missing or unexpected files. Run setup-laya.ps1 to inspect the installation.")
        for name, metadata in manifest["files"].items():
            source = path / name
            local_directory(str(source))
            stream = leases.enter_context(_open_leased(source))
            if os.fstat(stream.fileno()).st_size != metadata["size"]:
                raise AdvisorError("model_integrity", "A LAYA asset does not match its reviewed size. It was preserved and not loaded.")
            digest = hashlib.sha256()
            for block in iter(lambda: stream.read(1024 * 1024), b""):
                digest.update(block)
            if digest.hexdigest() != metadata["sha256"]:
                raise AdvisorError("model_integrity", "A LAYA asset failed SHA256 verification. It was preserved and not loaded.")
        yield path, manifest
