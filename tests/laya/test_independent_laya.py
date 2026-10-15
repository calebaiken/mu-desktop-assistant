"""Reviewer-owned bounded protocol and model admission tests; no model downloads."""
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
WORKER = ROOT / "workers/laya_advisor/worker.py"


def load(name, source):
    spec = importlib.util.spec_from_file_location(name, source)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


worker = load("reviewed_laya_worker", WORKER)
assets = load("reviewed_laya_assets", WORKER.with_name("assets.py"))


class IndependentLayaTests(unittest.TestCase):
    def request(self):
        return {"protocol": 1, "id": "00000000-0000-4000-8000-000000000001", "prompt": "Predict detector amplitude", "modelDir": str(ROOT / "artifacts/laya-model")}

    def answer(self):
        return {"type": "choice", "choice": "regression", "answer_confidence": .75,
                "probabilities": {name: .75 if name == "regression" else .05 for name in worker.LABELS}}

    def test_valid_request_and_no_scientific_import_before_admission(self):
        raw = json.dumps(self.request()).encode() + b"\n"
        self.assertEqual(worker.parse_request(raw), self.request())
        self.assertNotIn("torch", sys.modules)
        self.assertNotIn("laya", sys.modules)

    def test_request_closed_fields_identity_and_types(self):
        changes = [{"protocol": True}, {"protocol": 1.0}, {"protocol": 2}, {"id": "{00000000-0000-4000-8000-000000000001}"},
                   {"id": None}, {"prompt": ""}, {"prompt": " \t"}, {"prompt": "a" * 2001}, {"prompt": "bad\x00"},
                   {"prompt": "\ud800"}, {"modelDir": {}}, {"command": "execute"}]
        for change in changes:
            with self.subTest(change=change), self.assertRaises((ValueError, TypeError)):
                worker.parse_request(json.dumps(self.request() | change).encode() + b"\n")

    def test_duplicate_nonfinite_framing_and_depth_fail_closed(self):
        valid = json.dumps(self.request()).encode()
        for raw in [valid, valid + b"\n{}\n", valid.replace(b'"protocol": 1', b'"protocol": 1, "protocol": 1') + b"\n",
                    b'{"value":NaN}\n', b'\xff\n', b'x' * 16384 + b'\n', b'[' * 1500 + b'0' + b']' * 1500 + b'\n']:
            with self.subTest(length=len(raw)), self.assertRaises((ValueError, RecursionError, UnicodeError)):
                worker.parse_request(raw)

    def test_normalization_is_closed_and_explicitly_uncalibrated(self):
        result = worker.normalize_answer(self.answer(), truncated=True, revision=assets.MODEL_REVISION, elapsed=.25)
        self.assertEqual(set(result), {"category", "confidence", "probabilities", "truncated", "modelRevision", "elapsedSeconds", "warnings"})
        self.assertEqual(result["category"], "regression")
        self.assertAlmostEqual(sum(result["probabilities"].values()), 1)
        self.assertTrue(any("not calibrated" in warning for warning in result["warnings"]))
        self.assertTrue(any("truncated" in warning for warning in result["warnings"]))
        self.assertTrue(any("cannot authorize" in warning for warning in result["warnings"]))

    def test_inconsistent_and_nonfinite_distributions_rejected(self):
        answers = []
        for value in [True, float("nan"), float("inf"), -.1, 1.1, "0.75"]:
            answer = self.answer(); answer["probabilities"]["regression"] = value; answers.append(answer)
        for key, value in [("answer_confidence", .8), ("answer_confidence", True), ("choice", "shell"), ("choice", "other"), ("type", "code")]:
            answers.append(self.answer() | {key: value})
        extra = self.answer(); extra["probabilities"]["execute"] = 0; answers.append(extra)
        missing = self.answer(); del missing["probabilities"]["other"]; answers.append(missing)
        for answer in answers:
            with self.subTest(answer=answer), self.assertRaises(ValueError):
                worker.normalize_answer(answer, truncated=False, revision=assets.MODEL_REVISION, elapsed=1)

    def test_inference_provenance_refuses_nonfinite_time_and_wrong_revision(self):
        for changes in [{"truncated": 1}, {"revision": "unknown"}, {"elapsed": float("inf")}, {"elapsed": -1}, {"elapsed": True}]:
            arguments = {"truncated": False, "revision": assets.MODEL_REVISION, "elapsed": .2} | changes
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                worker.normalize_answer(self.answer(), **arguments)

    def test_windows_memory_admission_checks_physical_and_commit_before_import(self):
        class Query:
            def __init__(self, physical, process_commit, system_commit, success=1, performance_success=1, page_size=4096):
                self.physical, self.process_commit, self.system_commit = physical, process_commit, system_commit
                self.success, self.performance_success, self.page_size = success, performance_success, page_size
            def __call__(self, pointer):
                pointer._obj.available_physical = self.physical * 1024**2
                pointer._obj.available_commit = self.process_commit * 1024**2
                return self.success
            def performance(self, pointer, _):
                pointer._obj.page_size = self.page_size
                pointer._obj.commit_total = 5000 * 256
                pointer._obj.commit_limit = (5000 + self.system_commit) * 256
                return self.performance_success
        class Callable:
            def __init__(self, function): self.function = function
            def __call__(self, *args): return self.function(*args)
        class Kernel:
            def __init__(self, query): self.GlobalMemoryStatusEx = query; self.GetPerformanceInfo = Callable(query.performance)
        with patch.dict(sys.modules, {"assets": assets}):
            cases = [(Query(1535, 5000, 5000), "memory_limit"), (Query(5000, 2559, 5000), "memory_limit"),
                     (Query(5000, 5000, 3071), "memory_limit"), (Query(5000, 5000, 5000, success=0), "memory_status"),
                     (Query(5000, 5000, 5000, performance_success=0), "memory_status"),
                     (Query(5000, 5000, 5000, page_size=0), "memory_status"), (Query(5000, 5000, -1), "memory_status")]
            for query, expected in cases:
                with patch.object(worker.ctypes, "WinDLL", return_value=Kernel(query)):
                    with self.assertRaises(assets.AdvisorError) as error: worker.memory_preflight()
                    self.assertEqual(error.exception.code, expected)
            with patch.object(worker.ctypes, "WinDLL", return_value=Kernel(Query(1536, 2560, 3072))): worker.memory_preflight()
        self.assertNotIn("torch", sys.modules)

    def test_environment_scrub_and_offline_cpu_defaults(self):
        with patch.dict(os.environ, {"HF_TOKEN": "sentinel", "LAYA_KEY": "secret", "TORCH_HOME": "remote", "PYTHONPATH": "untrusted"}, clear=True):
            worker.configure_offline_environment()
            for key in ["HF_TOKEN", "LAYA_KEY", "TORCH_HOME", "PYTHONPATH"]: self.assertNotIn(key, os.environ)
            for key in ["HF_HUB_OFFLINE", "TRANSFORMERS_OFFLINE", "HF_HUB_DISABLE_IMPLICIT_TOKEN", "DO_NOT_TRACK"]: self.assertEqual(os.environ[key], "1")
            self.assertEqual(os.environ["CUDA_VISIBLE_DEVICES"], "-1")
            self.assertEqual(os.environ["OMP_NUM_THREADS"], "2")

    def test_network_audit_denies_dns_and_connections_in_fresh_process(self):
        script = f"""import importlib.util,sys,socket
s=importlib.util.spec_from_file_location('w',{str(WORKER)!r});w=importlib.util.module_from_spec(s);s.loader.exec_module(w)
sys.addaudithook(w.block_network)
for attempt in [lambda:socket.getaddrinfo('localhost',80), lambda:socket.socket().connect(('127.0.0.1',9))]:
 try: attempt()
 except PermissionError: pass
 else: raise AssertionError('network operation admitted')
print('denied')
"""
        result = subprocess.run([sys.executable, "-I", "-S", "-c", script], capture_output=True, timeout=10)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(result.stdout.strip(), b"denied")

    def test_blackbox_bad_frame_and_missing_dependency_return_one_fixed_error(self):
        payloads = [b"{}\n", json.dumps(self.request()).encode() + b"\n\n", json.dumps(self.request()).encode() + b"\n"]
        for payload in payloads:
            process = subprocess.run([sys.executable, "-I", "-S", str(WORKER)], input=payload, capture_output=True, timeout=10)
            self.assertEqual(process.returncode, 0)
            self.assertEqual(process.stderr, b"")
            self.assertEqual(len(process.stdout.splitlines()), 1)
            reply = json.loads(process.stdout)
            self.assertFalse(reply["ok"])
            self.assertIn(reply["error"]["code"], ["invalid_request", "missing_dependency"])
            self.assertNotIn(self.request()["prompt"], reply["error"]["message"])

    def fixture(self, directory):
        manifest = {"schemaVersion": 1, "revision": assets.MODEL_REVISION, "files": {}}
        for name in assets.EXPECTED_FILES:
            path = Path(directory) / name; path.parent.mkdir(parents=True, exist_ok=True)
            contents = ("synthetic test bytes:" + name).encode(); path.write_bytes(contents)
            manifest["files"][name] = {"size": len(contents), "sha256": hashlib.sha256(contents).hexdigest()}
        return manifest

    def test_model_hashes_closed_layout_and_windows_read_leases(self):
        with tempfile.TemporaryDirectory() as directory:
            manifest = self.fixture(directory)
            target = Path(directory) / "model.safetensors"
            with patch.object(assets, "read_manifest", return_value=manifest):
                with assets.verified_assets(directory) as (model_dir, _):
                    self.assertEqual(model_dir, Path(directory))
                    self.assertTrue(target.read_bytes())
                    if os.name == "nt":
                        with self.assertRaises(OSError): target.write_bytes(b"changed")
                        with self.assertRaises(OSError): target.rename(target.with_suffix(".moved"))
                target.write_bytes(b"x" * manifest["files"]["model.safetensors"]["size"])
                with self.assertRaises(assets.AdvisorError) as failure:
                    with assets.verified_assets(directory): pass
                self.assertEqual(failure.exception.code, "model_integrity")

    def test_extra_or_missing_model_files_and_unsafe_paths_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            manifest = self.fixture(directory)
            with patch.object(assets, "read_manifest", return_value=manifest):
                extra = Path(directory) / "evil.py"; extra.write_text("raise Exception()")
                with self.assertRaises(assets.AdvisorError):
                    with assets.verified_assets(directory): pass
                extra.unlink(); (Path(directory) / "model.safetensors").unlink()
                with self.assertRaises(assets.AdvisorError):
                    with assets.verified_assets(directory): pass
        for path in ["relative", "//server/share", "\\\\server\\share", "bad\0path"]:
            with self.subTest(path=path), self.assertRaises(assets.AdvisorError): assets.local_directory(path)


if __name__ == "__main__": unittest.main(verbosity=2)
