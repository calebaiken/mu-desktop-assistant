"""Black-box one-request worker ingress, local capabilities and journal privacy."""
from __future__ import annotations

import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import uuid

ROOT = Path(__file__).resolve().parents[2]
WORKER = ROOT / "workers" / "ml_lab" / "worker.py"


class IndependentProtocolTests(unittest.TestCase):
    def setUp(self):
        self.scratch = ROOT / "artifacts" / "qa" / "ml-independent-scratch"
        self.scratch.mkdir(parents=True, exist_ok=True)
        self.directory = tempfile.TemporaryDirectory(prefix="protocol-", dir=self.scratch)
        self.root = Path(self.directory.name)

    def tearDown(self):
        self.assertEqual(self.root.resolve().parent, self.scratch.resolve())
        self.directory.cleanup()

    def request(self, **overrides):
        return {"protocol": 1, "id": str(uuid.uuid4()), "action": "capabilities", "projectDir": str(self.root / "project"),
                "parameters": {}, "limits": {"maxRows": 100, "memoryMiB": 1024, "threads": 2}, **overrides}

    def invoke(self, request=None, raw=None, no_site=False):
        environment = {name: value for name, value in os.environ.items()
                       if name.upper() in {"SYSTEMROOT", "WINDIR", "TEMP", "TMP", "USERPROFILE", "LOCALAPPDATA", "APPDATA"}}
        environment["PATH"] = str(Path(os.environ["SystemRoot"]) / "System32")
        environment["MPLBACKEND"] = "Agg"
        command = [sys.executable, "-I", "-B"] + (["-S"] if no_site else []) + [str(WORKER)]
        if raw is None:
            raw = (json.dumps(request) + "\n").encode("utf-8")
        process = subprocess.run(command, input=raw, capture_output=True, timeout=35, env=environment, cwd=self.root)
        self.assertEqual(process.returncode, 0, process.stderr.decode("utf-8", errors="replace"))
        self.assertLessEqual(len(process.stdout), 2 * 1024 * 1024 + 1)
        self.assertEqual(process.stderr, b"")
        lines = process.stdout.splitlines()
        self.assertEqual(len(lines), 1)
        response = json.loads(lines[0])
        self.assertEqual(response["protocol"], 1)
        return response

    def test_capabilities_use_real_versions_and_exact_operation_identity(self):
        request = self.request()
        result = self.invoke(request)
        self.assertTrue(result["ok"], result)
        self.assertEqual(result["id"], request["id"])
        self.assertEqual(result["result"]["device"], "CPU")
        self.assertGreaterEqual(len(result["result"]["algorithms"]), 15)
        for package in ["numpy", "scikit-learn", "h5py", "skops"]:
            self.assertIsInstance(result["result"]["versions"][package], str)

    def test_unknown_duplicate_and_nested_fields_are_rejected(self):
        requests = [self.request(code="PRIVATE_PROTOCOL_SENTINEL"),
                    self.request(parameters={"execute": "PRIVATE_PROTOCOL_SENTINEL"}),
                    self.request(limits={"memoryMiB": 1024, "download": True}),
                    self.request(action="inspect", parameters={"path": str(self.root / "a.csv"), "allowPickle": True}),
                    self.request(action="predict", parameters={"modelId": str(uuid.uuid4()), "dataset": {"path": str(self.root / "a.csv"), "upload": True}})]
        for request in requests:
            with self.subTest(request=request):
                response = self.invoke(request)
                self.assertFalse(response["ok"])
                self.assertNotIn("PRIVATE_PROTOCOL_SENTINEL", str(response))
        raw = json.dumps(self.request()).replace('"parameters": {}', '"parameters": {}, "parameters": {}').encode() + b"\n"
        self.assertFalse(self.invoke(raw=raw)["ok"])

    def test_framing_size_unicode_nonfinite_and_depth_rejection(self):
        good = json.dumps(self.request()).encode()
        cases = [good, good + b"\n" + good + b"\n", b"x" * (128 * 1024 + 1) + b"\n", b"\xff\n",
                 good.replace(b'"memoryMiB": 1024', b'"memoryMiB": NaN') + b"\n"]
        deep = self.request(parameters={"x": [[[[[[[[[[[[[[[[[[1]]]]]]]]]]]]]]]]]]})
        cases.append(json.dumps(deep).encode() + b"\n")
        for index, raw in enumerate(cases):
            with self.subTest(case=index):
                self.assertFalse(self.invoke(raw=raw)["ok"])

    def test_missing_scientific_dependencies_are_reported_without_traceback(self):
        response = self.invoke(self.request(), no_site=True)
        self.assertFalse(response["ok"])
        self.assertEqual(response["error"]["code"], "missing_dependency")
        self.assertNotIn("Traceback", str(response))

    def test_inspection_journal_never_contains_raw_data_path_or_cell_value(self):
        path = self.root / "PRIVATE_FILE_SENTINEL.csv"
        path.write_text("value,label\n1,PRIVATE_CELL_SENTINEL\n2,safe\n", encoding="utf-8")
        response = self.invoke(self.request(action="inspect", parameters={"path": str(path), "rowLimit": 10}))
        self.assertTrue(response["ok"], response)
        journal = (self.root / "project" / "operations.jsonl").read_text(encoding="utf-8")
        self.assertNotIn("PRIVATE_FILE_SENTINEL", journal)
        self.assertNotIn("PRIVATE_CELL_SENTINEL", journal)
        rows = [json.loads(line) for line in journal.splitlines()]
        self.assertEqual([row["state"] for row in rows], ["running", "completed"])

    def test_protocol_version_requires_integer_one(self):
        for value in [True, 1.0, "1", 0, 2, None]:
            with self.subTest(protocol=value):
                self.assertFalse(self.invoke(self.request(protocol=value))["ok"])


if __name__ == "__main__":
    unittest.main(verbosity=2)
