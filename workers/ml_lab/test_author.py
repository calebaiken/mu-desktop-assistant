"""Author integration fixtures. Run with the pinned optional ML interpreter."""
from __future__ import annotations

import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import uuid

import numpy as np
import pandas as pd

import engine
from common import LabError


class EngineSmokeTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.directory = tempfile.TemporaryDirectory(prefix="mu-ml-author-")
        cls.project = Path(cls.directory.name)
        cls.resource = {"memory_mb": 1024, "max_rows": 10000, "preview_rows": 1000, "workers": 2}
        rng = np.random.default_rng(431)
        cls.frame = pd.DataFrame({"a": rng.normal(size=80), "b": rng.normal(size=80), "category": ["A", "B"] * 40})
        cls.frame["target"] = 2 * cls.frame.a - cls.frame.b
        cls.frame["class"] = np.where(cls.frame.a >= 0, "yes", "no")
        cls.source = cls.project / "fixture.csv"
        cls.frame.to_csv(cls.source, index=False)

    @classmethod
    def tearDownClass(cls):
        cls.directory.cleanup()

    def configuration(self, task, algorithm):
        return {"dataset": {"path": str(self.source)}, "task": task, "algorithm": algorithm,
                "features": ["a", "b"], "target": "target" if task == "regression" else "class" if task == "classification" else None,
                "scaling": "standard", "testFraction": .25, "seed": 42,
                "split": "stratified" if task == "classification" else "random", "folds": 0, "name": "Author fixture"}

    def test_all_advertised_algorithm_task_pairs_train_and_reload(self):
        for entry in engine.CATALOG:
            for task in entry["tasks"]:
                with self.subTest(task=task, algorithm=entry["id"]):
                    result = engine.train(self.configuration(task, entry["id"]), self.project, self.resource)
                    self.assertEqual("completed", result["status"])
                    self.assertTrue(all(Path(plot["path"]).is_file() for plot in result["plots"]))
                    model, _ = engine.load_model(self.project, result["id"])
                    self.assertIsNotNone(model)
                    if result["predictionSupported"]:
                        prediction = engine.predict({"modelId": result["id"], "dataset": {"path": str(self.source)}}, self.project, self.resource)
                        self.assertEqual(80, prediction["rows"])

    def test_isolated_worker_exact_protocol_and_unknown_rejection(self):
        request = {"protocol": 1, "id": str(uuid.uuid4()), "action": "capabilities", "projectDir": str(self.project),
                   "parameters": {}, "limits": {"maxRows": 10000, "memoryMiB": 1024, "threads": 2}}
        completed = subprocess.run([sys.executable, "-I", "-u", str(Path(__file__).with_name("worker.py"))],
                                   input=json.dumps(request) + "\n", capture_output=True, text=True, timeout=30)
        self.assertEqual(0, completed.returncode)
        self.assertEqual("", completed.stderr)
        self.assertEqual(1, len(completed.stdout.splitlines()))
        response = json.loads(completed.stdout)
        self.assertTrue(response["ok"])
        self.assertEqual(request["id"], response["id"])
        request["parameters"] = {"execute": "print('must not execute')"}
        completed = subprocess.run([sys.executable, "-I", "-u", str(Path(__file__).with_name("worker.py"))],
                                   input=json.dumps(request) + "\n", capture_output=True, text=True, timeout=30)
        self.assertFalse(json.loads(completed.stdout)["ok"])
        self.assertNotIn("must not execute", completed.stdout)

    def test_malformed_trailing_duplicate_and_deep_requests(self):
        base = {"protocol": 1, "id": str(uuid.uuid4()), "action": "capabilities", "projectDir": str(self.project), "parameters": {}, "limits": {}}
        texts = [json.dumps(base) + "\n{}\n", '{"protocol":1,"protocol":1}\n', json.dumps(base | {"parameters": {"x": [[[[[[[[[[[[[[[[[[[[1]]]]]]]]]]]]]]]]]]]]}}) + "\n"]
        for text in texts:
            with self.subTest(text=text[:30]):
                result = subprocess.run([sys.executable, "-I", "-u", str(Path(__file__).with_name("worker.py"))], input=text,
                                        capture_output=True, text=True, timeout=15)
                self.assertFalse(json.loads(result.stdout)["ok"])

    def test_native_group_time_split_and_no_scaling(self):
        source = self.project / "sequential.csv"
        frame = self.frame.copy()
        frame["time"] = np.arange(80)
        frame["group"] = np.repeat(np.arange(20), 4)
        frame.to_csv(source, index=False)
        for strategy, column in [("group", "group"), ("time", "time")]:
            with self.subTest(strategy=strategy):
                config = self.configuration("regression", "ridge")
                config.update(dataset={"path": str(source)}, split=strategy, groupColumn=column, folds=3, scaling="none")
                result = engine.train(config, self.project, self.resource)
                self.assertEqual("none", result["preprocessing"]["scaling"])
                self.assertEqual(3, result["validation"]["folds"])
                train = frame.iloc[result["split"]["trainRows"]][column]
                test = frame.iloc[result["split"]["testRows"]][column]
                self.assertFalse(set(train) & set(test))

    def test_multiclass_gradient_boosting_safe_loss_state_roundtrip(self):
        source = self.project / "multiclass.csv"
        frame = self.frame.copy()
        frame["class"] = np.arange(len(frame)) % 3
        frame.to_csv(source, index=False)
        config = self.configuration("classification", "gradient_boosting")
        config["dataset"] = {"path": str(source)}
        result = engine.train(config, self.project, self.resource)
        self.assertEqual(3, len(result["metrics"]["labels"]))
        prediction = engine.predict({"modelId": result["id"], "dataset": {"path": str(source)}}, self.project, self.resource)
        self.assertEqual(80, prediction["rows"])
        self.assertEqual(3, len(prediction["probabilities"][0]))

    def test_describe_and_exact_split_comparison(self):
        first = engine.train(self.configuration("regression", "ridge"), self.project, self.resource)
        second = engine.train(self.configuration("regression", "linear"), self.project, self.resource)
        described = engine.describe({"experimentId": first["id"]}, self.project)
        self.assertEqual(first["split"], described["split"])
        self.assertEqual(first["preprocessing"], described["preprocessing"])
        compared = engine.compare({"experimentIds": [first["id"], second["id"]]}, self.project)
        self.assertEqual(2, len(compared["models"]))
        self.assertGreaterEqual(len(compared["warnings"]), 2)
        config = self.configuration("regression", "ridge")
        config["seed"] = 44
        other = engine.train(config, self.project, self.resource)
        with self.assertRaises(LabError) as mismatch:
            engine.compare({"experimentIds": [first["id"], other["id"]]}, self.project)
        self.assertEqual("incompatible_comparison", mismatch.exception.code)


if __name__ == "__main__":
    unittest.main(verbosity=2)
