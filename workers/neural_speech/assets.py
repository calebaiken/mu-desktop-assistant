"""Closed local Kokoro assets, with read-only leases during synthesis."""
from __future__ import annotations

import contextlib
import ctypes
import hashlib
import json
import os
from pathlib import Path

EXPECTED = {
    "kokoro-v1.0.onnx": (325505369, "beb0d1848dee9a49da392cc3df26958d46cfa35d321edf434f52949153f0df3a"),
    "voices-v1.0.bin": (28214398, "bca610b8308e8d99f32e6fe4197e7ec01679264efed0cac9140fe9c29f1fbf7d"),
}


class SpeechError(Exception):
    def __init__(self, code):
        super().__init__(code)
        self.code = code


def local_path(value):
    if not isinstance(value, str) or not value or len(value) > 2048 or any(ord(c) < 32 for c in value):
        raise SpeechError("invalid_request")
    path = Path(value)
    if not path.is_absolute() or value.startswith(("\\\\", "//")) or (os.name == "nt" and ":" in value[2:]):
        raise SpeechError("invalid_request")
    for part in (path, *path.parents):
        if part.is_symlink() or (hasattr(part, "is_junction") and part.is_junction()):
            raise SpeechError("invalid_request")
    return path


def open_leased(path):
    if os.name != "nt":
        return path.open("rb")
    import msvcrt
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    create = kernel.CreateFileW
    create.argtypes = [ctypes.c_wchar_p, ctypes.c_uint32, ctypes.c_uint32, ctypes.c_void_p,
                       ctypes.c_uint32, ctypes.c_uint32, ctypes.c_void_p]
    create.restype = ctypes.c_void_p
    # FILE_SHARE_READ only. Prevent rename, write and delete while native code reads.
    handle = create(str(path), 0x80000000, 1, None, 3, 0x08000000, None)
    if handle == ctypes.c_void_p(-1).value:
        raise OSError("Asset lease unavailable")
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
def verified_assets(value):
    path = local_path(value)
    if not path.is_dir():
        raise SpeechError("missing_assets")
    manifest_path = Path(__file__).with_name("model-manifest.json")
    if manifest_path.stat().st_size > 8192:
        raise SpeechError("asset_mismatch")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if (manifest.get("schemaVersion") != 1 or manifest.get("repository") != "thewh1teagle/kokoro-onnx"
            or manifest.get("revision") != "model-files-v1.1" or set(manifest.get("files", {})) != set(EXPECTED)):
        raise SpeechError("asset_mismatch")
    for name, (size, digest) in EXPECTED.items():
        if manifest["files"][name] != {"size": size, "sha256": digest}:
            raise SpeechError("asset_mismatch")
    entries = []
    for entry in path.iterdir():
        entries.append(entry)
        if len(entries) > len(EXPECTED):
            raise SpeechError("asset_mismatch")
    if len(entries) != len(EXPECTED) or {entry.name for entry in entries} != set(EXPECTED):
        raise SpeechError("asset_mismatch")
    with contextlib.ExitStack() as leases:
        for name, (size, expected_hash) in EXPECTED.items():
            source = local_path(str(path / name))
            if not source.is_file():
                raise SpeechError("asset_mismatch")
            stream = leases.enter_context(open_leased(source))
            if os.fstat(stream.fileno()).st_size != size:
                raise SpeechError("asset_mismatch")
            digest = hashlib.sha256()
            for block in iter(lambda: stream.read(1024 * 1024), b""):
                digest.update(block)
            if digest.hexdigest() != expected_hash:
                raise SpeechError("asset_mismatch")
        yield path
