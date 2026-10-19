"""Production worker subprocesses: project artifact isolation, no model simulation."""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import uuid

WORKER = Path(__file__).resolve().parents[2] / "workers" / "ml_lab" / "worker.py"


class ResearchProjectWorkerTests(unittest.TestCase):
    def test_train_list_describe_and_export_stay_in_selected_project(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "explicit-selection.csv"
            source.write_text("x,target\n" + "".join(f"{i},{2*i+3}\n" for i in range(80)), encoding="utf-8")
            before = hashlib.sha256(source.read_bytes()).digest()
            project_a, project_b = root / "projects" / uuid.uuid4().hex, root / "projects" / uuid.uuid4().hex

            def invoke(project, action, parameters):
                request = {"protocol": 1, "id": str(uuid.uuid4()), "action": action,
                           "projectDir": str(project), "parameters": parameters,
                           "limits": {"maxRows": 100, "memoryMiB": 1024, "threads": 2}}
                environment = os.environ.copy()
                environment.update(OPENBLAS_NUM_THREADS="2", OMP_NUM_THREADS="2", MKL_NUM_THREADS="2", MPLBACKEND="Agg")
                process = subprocess.run([sys.executable, "-I", str(WORKER)], input=json.dumps(request) + "\n",
                                         text=True, capture_output=True, timeout=45, env=environment)
                response = json.loads(process.stdout)
                self.assertEqual(request["id"], response["id"])
                # Main's protocol reports declared errors in JSON and exits zero;
                # PR #1's transport fixes remain separate from this increment.
                self.assertEqual(0, process.returncode)
                return response

            trained = invoke(project_a, "train", {"dataset": {"path": str(source), "rowLimit": 80},
                "task": "regression", "algorithm": "linear", "features": ["x"], "target": "target",
                "scaling": "none", "seed": 42, "testFraction": 0.2, "split": "random", "name": "Project A baseline"})
            self.assertTrue(trained["ok"], trained)
            experiment = trained["result"]["id"]
            listed_a = invoke(project_a, "list", {})
            listed_b = invoke(project_b, "list", {})
            self.assertEqual([experiment], [item["id"] for item in listed_a["result"]["experiments"]])
            self.assertEqual([], listed_b["result"]["experiments"])
            denied = invoke(project_b, "describe", {"experimentId": experiment})
            self.assertFalse(denied["ok"], "An identifier from another project grants no retrieval authority.")
            description = invoke(project_a, "describe", {"experimentId": experiment})
            self.assertTrue(description["ok"], description)
            exported = invoke(project_a, "export", {"experimentId": experiment})
            self.assertTrue(exported["ok"], exported)
            export_path = Path(exported["result"]["path"]).resolve()
            self.assertTrue(export_path.is_relative_to(project_a.resolve()))
            self.assertFalse(export_path.is_relative_to(project_b.resolve()))
            self.assertEqual(before, hashlib.sha256(source.read_bytes()).digest())
            self.assertFalse(any("experiments" in path.parts for path in project_b.rglob("*")))


if __name__ == "__main__":
    unittest.main()
