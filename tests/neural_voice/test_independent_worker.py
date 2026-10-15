"""Reviewer-owned neural-worker boundaries; no inference, assets or network fetch."""
import contextlib
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "workers/neural_speech"
sys.path.insert(0, str(SOURCE))


def load(name, source):
    specification = importlib.util.spec_from_file_location(name, source)
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


worker = load("reviewed_neural_worker", SOURCE / "worker.py")
import assets


class IndependentNeuralWorkerTests(unittest.TestCase):
    def request(self, kind="probe"):
        return {"protocol": 1, "id": "00000000-0000-4000-8000-000000000001", "kind": kind,
                "modelDir": str(ROOT / "artifacts/neural-voice-model"),
                "request": None if kind == "voices" else {"text": "Public voice fixture", "voiceId": "kokoro:af_heart", "rate": 0}}

    def parse(self, value):
        return worker.parse_request(json.dumps(value).encode() + b"\n")

    def test_valid_closed_requests_import_only_standard_library(self):
        for kind in ("voices", "probe", "speak"):
            request = self.request(kind)
            self.assertEqual(self.parse(request), request)
        code = """import importlib.util, sys
s = importlib.util.spec_from_file_location('reviewed_worker', sys.argv[1])
w = importlib.util.module_from_spec(s); s.loader.exec_module(w)
assert all(name not in sys.modules for name in ('numpy', 'onnxruntime', 'kokoro_onnx', 'torch'))
print('standard_library_only')
"""
        result = subprocess.run([sys.executable, "-I", "-c", code, str(SOURCE / "worker.py")],
                                capture_output=True, timeout=10, check=False)
        self.assertEqual(result.returncode, 0, result.stderr.decode())
        self.assertEqual(result.stdout.strip(), b"standard_library_only")

    def test_identity_fields_types_and_kinds_fail_closed(self):
        cases = [{"protocol": True}, {"protocol": 1.0}, {"protocol": 2},
                 {"id": "00000000-0000-0000-0000-000000000000"}, {"id": "{00000000-0000-4000-8000-000000000001}"},
                 {"id": None}, {"kind": "shell"}, {"kind": True}, {"modelDir": {}}, {"extra": "run"}]
        for change in cases:
            with self.subTest(change=change), self.assertRaises((ValueError, TypeError)):
                self.parse(self.request() | change)
        for key in self.request():
            request = self.request(); del request[key]
            with self.subTest(missing=key), self.assertRaises(ValueError):
                self.parse(request)

    def test_exact_utf16_unit_boundary_and_valid_astral_pairs(self):
        request = self.request()
        for text in ("x" * 2000, "\U0001f30c" * 1000, "x" * 1998 + "\U0001f30c"):
            self.assertEqual(self.parse(request | {"request": request["request"] | {"text": text}})["request"]["text"], text)
        for text in ("x" * 2001, "\U0001f30c" * 1001, "x" * 1999 + "\U0001f30c", "\ud800", "\udc00", "bad\x00", " \t", ""):
            with self.subTest(units=len(text.encode("utf-16-le", errors="surrogatepass")) // 2), self.assertRaises(ValueError):
                self.parse(request | {"request": request["request"] | {"text": text}})

    def test_only_four_pinned_voices_and_integral_rates(self):
        request = self.request()
        self.assertEqual(worker.VOICE_IDS, {"kokoro:af_heart", "kokoro:af_bella", "kokoro:am_michael", "kokoro:bm_george"})
        for voice in worker.VOICE_IDS:
            for rate in (-5, 0, 5):
                self.parse(request | {"request": request["request"] | {"voiceId": voice, "rate": rate}})
        for change in [{"rate": True}, {"rate": 0.0}, {"rate": -6}, {"rate": 6}, {"voiceId": "Microsoft David"},
                       {"voiceId": "kokoro:unknown"}, {"voiceId": ["kokoro:af_heart"]}, {"extra": "execute"}]:
            with self.subTest(change=change), self.assertRaises(ValueError):
                self.parse(request | {"request": request["request"] | change})
        with self.assertRaises(ValueError):
            self.parse(self.request("voices") | {"request": request["request"]})

    def test_json_framing_duplicates_invalid_utf8_and_nonfinite_refused(self):
        valid = json.dumps(self.request()).encode()
        cases = [valid, valid + b"\n{}\n", valid.replace(b'"protocol": 1', b'"protocol": 1, "protocol": 1') + b"\n",
                 valid.replace(b'"rate": 0', b'"rate": NaN') + b"\n", b"\xff\n", b"x" * 16385 + b"\n",
                 b"[" * 1500 + b"0" + b"]" * 1500 + b"\n"]
        for raw in cases:
            with self.subTest(length=len(raw)), self.assertRaises((ValueError, RecursionError, UnicodeError)):
                worker.parse_request(raw)

    def test_runtime_pin_admission_does_not_import_runtime(self):
        with patch.object(worker.importlib.metadata, "version", side_effect=lambda name: worker.RUNTIME_VERSIONS[name]):
            worker.runtime_preflight()
        for affected in worker.RUNTIME_VERSIONS:
            with self.subTest(package=affected), patch.object(worker.importlib.metadata, "version",
                    side_effect=lambda name: "wrong" if name == affected else worker.RUNTIME_VERSIONS[name]), self.assertRaises(assets.SpeechError) as caught:
                worker.runtime_preflight()
            self.assertEqual(caught.exception.code, "runtime_missing")
        self.assertNotIn("onnxruntime", sys.modules)

    def test_fresh_process_denies_dns_connect_and_child_process(self):
        code = """import importlib.util, socket, subprocess, sys
s = importlib.util.spec_from_file_location('reviewed_worker', sys.argv[1])
w = importlib.util.module_from_spec(s); s.loader.exec_module(w)
sys.addaudithook(w.block_network)
for operation in (lambda: socket.getaddrinfo('example.invalid', 80), lambda: socket.socket().connect(('127.0.0.1', 9)), lambda: subprocess.Popen([sys.executable, '-c', 'pass'])):
    try: operation()
    except PermissionError: continue
    raise SystemExit(2)
print('three_denials')
"""
        result = subprocess.run([sys.executable, "-I", "-c", code, str(SOURCE / "worker.py")],
                                capture_output=True, timeout=10, check=False)
        self.assertEqual(result.returncode, 0, result.stderr.decode())
        self.assertEqual(result.stdout.strip(), b"three_denials")

    def test_offline_configuration_removes_remote_and_startup_hooks(self):
        with patch.dict(os.environ, {"HF_TOKEN": "not-a-token", "HF_ENDPOINT": "https://example.invalid", "KOKORO_REMOTE": "1",
                                     "ORT_REMOTE": "1", "PYTHONPATH": "hook", "PYTHONHOME": "hook"}, clear=True):
            worker.configure_offline()
            for forbidden in ("HF_TOKEN", "HF_ENDPOINT", "KOKORO_REMOTE", "ORT_REMOTE", "PYTHONPATH", "PYTHONHOME"):
                self.assertNotIn(forbidden, os.environ)
            self.assertEqual(os.environ["HF_HUB_OFFLINE"], "1")
            self.assertEqual(os.environ["CUDA_VISIBLE_DEVICES"], "-1")
            self.assertEqual(os.environ["OMP_NUM_THREADS"], "2")

    def test_exact_windows_memory_thresholds_fail_closed(self):
        class Function:
            def __init__(self, call): self.call = call
            def __call__(self, *arguments): return self.call(*arguments)
        class Queries:
            def __init__(self, physical, process, system, memory_ok=1, performance_ok=1, page=4096):
                self.physical, self.process, self.system = physical, process, system
                self.memory_ok, self.performance_ok, self.page = memory_ok, performance_ok, page
                self.GlobalMemoryStatusEx = Function(self.memory)
                self.GetPerformanceInfo = Function(self.performance)
            def memory(self, pointer):
                pointer._obj.available_physical = self.physical * 1024**2
                pointer._obj.available_commit = self.process * 1024**2
                return self.memory_ok
            def performance(self, pointer, _):
                pointer._obj.page_size = self.page
                pointer._obj.commit_total = 5000 * 256
                pointer._obj.commit_limit = (5000 + self.system) * 256
                return self.performance_ok
        def check(query, expected):
            with patch.object(worker.ctypes, "WinDLL", return_value=query):
                if expected:
                    worker.memory_preflight()
                else:
                    with self.assertRaises(assets.SpeechError) as caught:
                        worker.memory_preflight()
                    self.assertEqual(caught.exception.code, "memory_limit")
        check(Queries(512, 1024, 1536), True)
        for query in (Queries(511, 1024, 1536), Queries(512, 1023, 1536), Queries(512, 1024, 1535),
                      Queries(512, 1024, 1536, memory_ok=0), Queries(512, 1024, 1536, performance_ok=0),
                      Queries(512, 1024, 1536, page=0), Queries(512, 1024, -1)):
            check(query, False)

    def test_local_path_refuses_remote_relative_device_and_links(self):
        for path in ("relative", "\\\\server\\share\\weights", "//server/share/weights", "\\\\?\\C:\\weights", "C:\\file:stream", "C:\\bad\x00"):
            with self.subTest(path=path), self.assertRaises(assets.SpeechError):
                assets.local_path(path)
        with patch.object(Path, "is_symlink", return_value=True), self.assertRaises(assets.SpeechError):
            assets.local_path(str(ROOT / "artifacts/neural-voice-model"))

    def test_exact_asset_layout_hash_and_real_windows_read_lease(self):
        scratch = ROOT / "artifacts/qa/neural-voice-review"
        scratch.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix="assets-", dir=scratch) as temporary:
            folder = Path(temporary); models = folder / "models"; models.mkdir()
            expected = {}
            for name, raw in {"kokoro-v1.0.onnx": b"public-model-fixture", "voices-v1.0.bin": b"public-voice-fixture"}.items():
                (models / name).write_bytes(raw)
                expected[name] = (len(raw), hashlib.sha256(raw).hexdigest())
            manifest = {"schemaVersion": 1, "repository": "thewh1teagle/kokoro-onnx", "revision": "model-files-v1.1",
                        "files": {name: {"size": values[0], "sha256": values[1]} for name, values in expected.items()}}
            (folder / "model-manifest.json").write_text(json.dumps(manifest), encoding="utf-8")
            with patch.object(assets, "EXPECTED", expected), patch.object(assets, "__file__", str(folder / "assets.py")):
                with assets.verified_assets(str(models)):
                    for name in expected:
                        with self.assertRaises(OSError):
                            (models / name).write_bytes(b"tamper")
                        with self.assertRaises(OSError):
                            (models / name).rename(models / (name + ".renamed"))
                selected = models / "kokoro-v1.0.onnx"
                selected.write_bytes(b"public-model-fixture")
                (models / "unexpected.txt").write_text("extra")
                with self.assertRaises(assets.SpeechError) as caught:
                    with assets.verified_assets(str(models)): pass
                self.assertEqual(caught.exception.code, "asset_mismatch")
                (models / "unexpected.txt").unlink()
                selected.write_bytes(b"tampered-model-value")
                with self.assertRaises(assets.SpeechError) as caught:
                    with assets.verified_assets(str(models)): pass
                self.assertEqual(caught.exception.code, "asset_mismatch")


if __name__ == "__main__":
    unittest.main()
