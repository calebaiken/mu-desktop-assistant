"""Independent numerical, leakage, persistence and reproducibility workflow checks."""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "workers" / "ml_lab"))

import h5py
import numpy as np
import pandas as pd
import skops.io as skops
from common import LabError
import engine
from models import CATALOG

RESOURCE = {"memory_mb": 1024, "max_rows": 10000, "preview_rows": 1000, "workers": 2}


class UnexpectedSerializable:
    def __init__(self):
        self.value = "unreviewed type"


class IndependentEngineTests(unittest.TestCase):
    def setUp(self):
        self.scratch = Path(__file__).resolve().parents[2] / "artifacts" / "qa" / "ml-independent-scratch"
        self.scratch.mkdir(parents=True, exist_ok=True)
        self.directory = tempfile.TemporaryDirectory(prefix="engine-", dir=self.scratch)
        self.root = Path(self.directory.name)
        self.project = self.root / "project"
        self.project.mkdir()
        self.frame = pd.DataFrame({"x": np.arange(80, dtype=float), "z": np.arange(80) % 5,
                                   "y": 2 * np.arange(80, dtype=float) + 3, "category": ["a", "b"] * 40,
                                   "time": np.arange(80), "group": np.arange(80) // 4,
                                   "label": ["A", "B"] * 40})
        self.path = self.root / "events.csv"
        self.frame.to_csv(self.path, index=False)

    def tearDown(self):
        self.assertEqual(self.root.resolve().parent, self.scratch.resolve())
        self.directory.cleanup()

    def parameters(self, **changes):
        return {"dataset": {"path": str(self.path), "rowLimit": 100}, "task": "regression", "algorithm": "linear",
                "features": ["x", "z"], "target": "y", "scaling": "standard", "testFraction": .2,
                "seed": 42, "split": "random", "groupColumn": None, "folds": 0, "name": "Independent baseline", "hyperparameters": {}, **changes}

    def train(self, **changes):
        return engine.train(self.parameters(**changes), self.project, RESOURCE)

    def test_regression_metrics_match_hand_calculated_oracle(self):
        result = engine._regression_metrics(np.array([1., 2., 4.]), np.array([2., 2., 1.]))
        expected = {"mae": 4 / 3, "mse": 10 / 3, "rmse": np.sqrt(10 / 3), "r2": -8 / 7,
                    "explainedVariance": -6 / 7, "residualMean": 2 / 3, "residualStd": np.sqrt(13 / 3)}
        for name, value in expected.items():
            self.assertAlmostEqual(result[name], value, places=12, msg=name)

    def test_classification_confusion_and_macro_metrics_match_counts(self):
        fake = SimpleNamespace(named_steps={"model": SimpleNamespace(classes_=np.array(["A", "B"]))})
        result = engine._classification_metrics(np.array(["A", "A", "B", "B"]), np.array(["A", "B", "A", "B"]), fake, None)
        self.assertEqual(result["confusionMatrix"], [[1, 1], [1, 1]])
        for name in ["accuracy", "balancedAccuracy", "precisionMacro", "recallMacro", "f1Macro", "f1Weighted"]:
            self.assertEqual(result[name], .5)

    def test_engine_time_split_keeps_held_out_values_out_of_all_preprocessing(self):
        self.frame.loc[5, "x"] = np.nan
        self.frame.loc[64:, "x"] = 99999.
        self.frame.loc[64:, "category"] = "test-only-category"
        self.frame.to_csv(self.path, index=False)
        result = self.train(features=["x", "category"], split="time", groupColumn="time", algorithm="ridge")
        model, _ = engine.load_model(self.project, result["id"])
        numeric = model.named_steps["preprocessing"].named_transformers_["numeric"]
        categorical = model.named_steps["preprocessing"].named_transformers_["categorical"]
        self.assertEqual(result["split"]["trainRows"], list(range(64)))
        self.assertEqual(result["split"]["testRows"], list(range(64, 80)))
        expected_median = float(np.median(np.delete(np.arange(64, dtype=float), 5)))
        expected_imputed = np.arange(64, dtype=float)
        expected_imputed[5] = expected_median
        self.assertEqual(numeric.named_steps["imputer"].statistics_[0], expected_median)
        self.assertAlmostEqual(numeric.named_steps["scaler"].mean_[0], expected_imputed.mean())
        self.assertEqual(categorical.named_steps["encoder"].categories_[0].tolist(), ["a", "b"])
        self.assertNotIn("test-only-category", str(result["preprocessing"]))

    def test_cross_validation_receives_only_outer_training_partition(self):
        original = engine.cross_validate
        admitted = []
        def observe(estimator, x, y, **options):
            admitted.append(x.index.tolist())
            return original(estimator, x, y, **options)
        with patch("engine.cross_validate", side_effect=observe):
            result = self.train(folds=3)
        self.assertEqual(admitted, [result["split"]["trainRows"]])
        self.assertTrue(set(admitted[0]).isdisjoint(result["split"]["testRows"]))
        self.assertEqual(result["validation"]["folds"], 3)
        self.assertEqual(len(result["validation"]["metrics"]["rmse"]["foldValues"]), 3)
        self.assertLess(result["metrics"]["rmse"], 1e-10)

    def test_group_split_has_no_identity_overlap(self):
        result = self.train(split="group", groupColumn="group", folds=3)
        train = self.frame.iloc[result["split"]["trainRows"]]["group"]
        test = self.frame.iloc[result["split"]["testRows"]]["group"]
        self.assertTrue(set(train).isdisjoint(test))
        self.assertEqual(result["validation"]["folds"], 3)

    def test_nonzero_selection_source_rows_and_missing_target_exclusion_agree(self):
        self.frame.loc[17, "y"] = np.nan
        self.frame.to_csv(self.path, index=False)
        result = self.train(dataset={"path": str(self.path), "start": 10, "rowLimit": 60})
        self.assertEqual(result["excludedRows"], [17])
        partition = set(result["split"]["trainRows"]) | set(result["split"]["testRows"])
        self.assertEqual(partition, set(range(10, 70)) - {17})
        rows = pd.read_csv(result["outputPath"])
        self.assertEqual(rows["sourceRow"].tolist(), result["split"]["testRows"])

    def test_persistence_predict_and_strict_schema_mismatch(self):
        result = self.train()
        listed = engine.list_project(self.project)
        self.assertEqual(listed["models"][0]["id"], result["modelId"])
        self.assertEqual(listed["experiments"][0]["status"], "completed")
        new = self.root / "new.csv"
        pd.DataFrame({"x": [100., 101., 102.], "z": [0, 1, 2]}).to_csv(new, index=False)
        predicted = engine.predict({"modelId": result["id"], "dataset": {"path": str(new)}}, self.project, RESOURCE)
        np.testing.assert_allclose(predicted["predictions"], [203., 205., 207.], atol=1e-10)
        self.assertEqual(predicted["version"], 1)
        self.assertTrue(Path(predicted["outputPath"]).is_file())
        for frame in [pd.DataFrame({"x": [1]}), pd.DataFrame({"z": [1], "x": [1]}), pd.DataFrame({"x": ["bad"], "z": [1]})]:
            frame.to_csv(new, index=False)
            with self.subTest(schema=frame.dtypes.to_dict()), self.assertRaises(LabError) as caught:
                engine.predict({"modelId": result["id"], "dataset": {"path": str(new)}}, self.project, RESOURCE)
            self.assertEqual(caught.exception.code, "schema_mismatch")

    def test_model_tampering_and_unknown_skops_types_remain_untrusted(self):
        result = self.train()
        model_path = Path(result["modelPath"])
        metadata_path = model_path.parent / "experiment.json"
        original = model_path.read_bytes()
        model_path.write_bytes(original + b"TAMPER")
        with self.assertRaises(LabError) as caught:
            engine.load_model(self.project, result["id"])
        self.assertEqual(caught.exception.code, "invalid_artifact")
        skops.dump(UnexpectedSerializable(), model_path)
        metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
        metadata["modelSha256"] = hashlib.sha256(model_path.read_bytes()).hexdigest()
        metadata_path.write_text(json.dumps(metadata), encoding="utf-8")
        with self.assertRaises(LabError) as caught:
            engine.load_model(self.project, result["id"])
        self.assertEqual(caught.exception.code, "untrusted_model")

    def test_pca_clustering_noise_and_anomaly_assignments_preserve_all_rows(self):
        for task, algorithm, parameters in [("reduction", "pca", {}), ("clustering", "dbscan", {"eps": .0001}),
                                             ("anomaly", "local_outlier_factor", {"contamination": .1})]:
            with self.subTest(algorithm=algorithm):
                result = self.train(task=task, algorithm=algorithm, target=None,
                                    dataset={"path": str(self.path), "start": 10, "rowLimit": 60}, hyperparameters=parameters)
                assignments = pd.read_csv(result["outputPath"])
                self.assertEqual(assignments["sourceRow"].tolist(), list(range(10, 70)))
                self.assertEqual(len(assignments), 60)
                self.assertTrue(result["plots"])
                if task == "clustering":
                    self.assertEqual(result["metrics"]["noiseRows"], 60)
                    self.assertTrue((assignments["prediction"] == -1).all())
                    self.assertIn("do not establish physical populations", " ".join(result["warnings"]))
                if task == "anomaly":
                    self.assertEqual(result["metrics"]["anomalyRows"] + result["metrics"]["inlierRows"], 60)
                    self.assertTrue(set(result["metrics"]["rankedRowIndices"]).issubset(set(range(10, 70))))

    def test_export_runs_standalone_and_matches_metrics(self):
        result = self.train(dataset={"path": str(self.path), "start": 10, "rowLimit": 60})
        exported = engine.export({"experimentId": result["id"]}, self.project)
        destination = self.root / "standalone"
        destination.mkdir()
        environment = os.environ.copy()
        for name in ["OMP_NUM_THREADS", "OPENBLAS_NUM_THREADS", "MKL_NUM_THREADS", "NUMEXPR_NUM_THREADS"]:
            environment[name] = "2"
        run = subprocess.run([sys.executable, "-I", "-B", exported["path"]], cwd=destination,
                             capture_output=True, text=True, timeout=30, env=environment)
        self.assertEqual(run.returncode, 0, run.stderr)
        metrics = json.loads(run.stdout.strip())
        for key in ["mae", "rmse", "r2"]:
            self.assertAlmostEqual(metrics[key], result["metrics"][key], places=10)
        self.assertTrue((destination / "reproduced_predictions.csv").is_file())

    def test_failed_job_is_visible_and_does_not_register_model(self):
        with self.assertRaises(LabError):
            self.train(algorithm="random_forest", hyperparameters={"n_jobs": -1})
        listing = engine.list_project(self.project)
        self.assertEqual(len(listing["experiments"]), 1)
        self.assertEqual(listing["experiments"][0]["status"], "failed")
        self.assertEqual(listing["models"], [])

    def test_source_change_during_fit_prevents_completed_registration(self):
        original = engine.training_plots
        def mutate(*args, **kwargs):
            result = original(*args, **kwargs)
            self.path.write_text(self.path.read_text(encoding="utf-8") + "81,0,81,a,81,20,A\n", encoding="utf-8")
            return result
        with patch("engine.training_plots", side_effect=mutate), self.assertRaises(LabError) as caught:
            self.train()
        self.assertEqual(caught.exception.code, "source_changed")
        self.assertEqual(engine.list_project(self.project)["models"], [])

    def test_every_advertised_algorithm_persists_and_reloads(self):
        for entry in CATALOG:
            for task in entry["tasks"]:
                with self.subTest(algorithm=entry["id"], task=task):
                    result = self.train(task=task, algorithm=entry["id"], target="label" if task == "classification" else "y" if task == "regression" else None,
                                        split="stratified" if task == "classification" else "random")
                    model, metadata = engine.load_model(self.project, result["id"])
                    self.assertEqual(metadata["algorithm"], entry["id"])
                    self.assertEqual(metadata["status"], "completed")
                    self.assertIsNotNone(model.named_steps["model"])

    def test_unseen_test_class_and_same_timestamp_cv_are_rejected(self):
        self.frame["label"] = ["A"] * 40 + ["B"] * 38 + ["test-only-C"] * 2
        self.frame.to_csv(self.path, index=False)
        with self.assertRaises(LabError):
            self.train(task="classification", algorithm="logistic", target="label", split="time", groupColumn="time")
        self.frame["time"] = np.arange(80) // 2
        self.frame.to_csv(self.path, index=False)
        with self.assertRaises(LabError):
            self.train(split="time", groupColumn="time", folds=3)

    def test_multiclass_gradient_boosting_persistence_retains_predictions(self):
        self.frame["label"] = np.asarray(["alpha", "beta", "gamma"])[np.arange(80) % 3]
        self.frame.to_csv(self.path, index=False)
        result = self.train(task="classification", algorithm="gradient_boosting", target="label", split="stratified")
        model, metadata = engine.load_model(self.project, result["id"])
        test = self.frame.iloc[result["split"]["testRows"]][["x", "z"]]
        stored = pd.read_csv(result["outputPath"])["prediction"].to_numpy()
        np.testing.assert_array_equal(model.predict(test), stored)
        self.assertEqual(len(metadata["metrics"]["confusionMatrix"]), 3)
        self.assertEqual(sum(sum(row) for row in metadata["metrics"]["confusionMatrix"]), len(stored))

    def test_historical_description_and_comparison_enforce_exact_cohort(self):
        baseline = self.train(folds=3)
        ridge = self.train(algorithm="ridge", folds=3, scaling="none")
        described = engine.describe({"experimentId": baseline["id"]}, self.project)
        self.assertEqual(described["split"], baseline["split"])
        self.assertEqual(described["preprocessing"], baseline["preprocessing"])
        self.assertEqual(described["software"], baseline["software"])
        result = engine.compare({"experimentIds": [baseline["id"], ridge["id"]]}, self.project)
        self.assertEqual([model["algorithm"] for model in result["models"]], ["linear", "ridge"])
        self.assertEqual(result["models"][0]["metrics"], baseline["metrics"])
        self.assertIn("No universal best", " ".join(result["warnings"]))
        metadata_path = Path(ridge["modelPath"]).parent / "experiment.json"
        for mutate in [
            lambda record: record["dataset"].update(start=1),
            lambda record: record["dataset"].update(fingerprint="different-source"),
            lambda record: record["split"]["testRows"].reverse(),
            lambda record: record["parameters"].update(folds=5),
            lambda record: record.update(target="other"),
            lambda record: record.update(features=["z", "x"]),
            lambda record: record.update(status="interrupted"),
        ]:
            altered = json.loads(json.dumps(ridge))
            mutate(altered)
            metadata_path.write_text(json.dumps(altered), encoding="utf-8")
            with self.assertRaises(LabError) as caught:
                engine.compare({"experimentIds": [baseline["id"], ridge["id"]]}, self.project)
            self.assertEqual(caught.exception.code, "incompatible_comparison")
        for ids in [[baseline["id"]], [baseline["id"], baseline["id"]]]:
            with self.assertRaises(LabError):
                engine.compare({"experimentIds": ids}, self.project)

    def test_standalone_export_uses_logical_csv_rows_with_multiline_fields(self):
        self.frame["category"] = ["before\nafter" if i % 2 else "a" for i in range(80)]
        self.frame.to_csv(self.path, index=False)
        result = self.train(features=["x", "category"], dataset={"path": str(self.path), "start": 10, "rowLimit": 60})
        exported = engine.export({"experimentId": result["id"]}, self.project)
        destination = self.root / "multiline-export"
        destination.mkdir()
        run = subprocess.run([sys.executable, "-I", "-B", exported["path"]], cwd=destination,
                             capture_output=True, text=True, timeout=30)
        self.assertEqual(run.returncode, 0, run.stderr)
        output = pd.read_csv(destination / "reproduced_predictions.csv")
        original = pd.read_csv(result["outputPath"])
        self.assertEqual(output.shape[1], 1)
        np.testing.assert_allclose(output.iloc[:, 0], original["prediction"], atol=1e-9)

    def test_incompatible_versions_and_interrupted_records_cannot_admit_model(self):
        result = self.train()
        metadata_path = Path(result["modelPath"]).parent / "experiment.json"
        altered = json.loads(json.dumps(result))
        altered["software"]["scikit-learn"] = "0.0.0"
        metadata_path.write_text(json.dumps(altered), encoding="utf-8")
        with patch("engine.skops.load", side_effect=AssertionError("Unmatched versions must not deserialize")):
            with self.assertRaises(LabError) as caught:
                engine.load_model(self.project, result["id"])
        self.assertEqual(caught.exception.code, "incompatible_model")
        altered["software"] = result["software"]
        altered["status"] = "running"
        metadata_path.write_text(json.dumps(altered), encoding="utf-8")
        listing = engine.list_project(self.project)
        self.assertEqual(listing["experiments"][0]["status"], "interrupted")
        self.assertEqual(listing["models"], [])
        self.assertEqual(engine.describe({"experimentId": result["id"]}, self.project)["status"], "interrupted")
        with self.assertRaises(LabError) as caught:
            engine.load_model(self.project, result["id"])
        self.assertEqual(caught.exception.code, "invalid_artifact")
        self.assertTrue(Path(result["modelPath"]).exists())

    def test_model_registry_refuses_traversal_and_missing_source_is_actionable(self):
        for identifier in ["../outside", "..\\outside", str(self.root), "not-a-uuid"]:
            with self.subTest(identifier=identifier), self.assertRaises(LabError):
                engine.load_model(self.project, identifier)
        self.path.unlink()
        with self.assertRaises(LabError) as caught:
            self.train()
        self.assertEqual(caught.exception.code, "missing_source")



    def test_waveform_hdf5_and_npz_training_prediction_and_standalone_export_parity(self):
        rng = np.random.default_rng(12345)
        t = np.linspace(0.0, 1.0, 64, endpoint=False)
        waveforms = np.zeros((48, 2, 64), dtype=np.float64)
        for i in range(48):
            amp = 1.0 + 0.15 * i
            waveforms[i, 0, :] = amp * np.sin(2.0 * np.pi * 3.0 * t) + 0.01 * rng.standard_normal(64)
            waveforms[i, 1, :] = 0.5 * amp * np.cos(2.0 * np.pi * 6.0 * t) + 0.01 * rng.standard_normal(64)

        h5_path = self.root / "wf_train.h5"
        with h5py.File(h5_path, "w") as handle:
            handle.create_dataset("detector/pulses", data=waveforms, chunks=(12, 1, 64))

        npz_path = self.root / "wf_train.npz"
        np.savez(npz_path, pulses=waveforms)

        npy_path = self.root / "wf_train.npy"
        np.save(npy_path, waveforms[:, 0, :])

        for ds_path, ds_key, target_col, feat_cols, exp_name in (
            (h5_path, "/detector/pulses@channel:0@waveform", "ch0_energy", ["ch0_mean", "ch0_std", "ch0_ptp", "ch0_abs_mean", "ch0_dominant_bin"], "H5WaveformRidge"),
            (npz_path, "pulses@channel:0@waveform", "ch0_energy", ["ch0_mean", "ch0_std", "ch0_ptp", "ch0_abs_mean", "ch0_dominant_bin"], "NpzWaveformRidge"),
            (npy_path, "@waveform", "wf_energy", ["wf_mean", "wf_std", "wf_ptp", "wf_abs_mean", "wf_dominant_bin"], "NpyWaveformRidge"),
        ):
            trained = self.train(
                name=exp_name,
                task="regression",
                algorithm="ridge",
                dataset={"path": str(ds_path), "key": ds_key, "start": 0, "rowLimit": 40},
                target=target_col,
                features=feat_cols,
                scaling="standard",
                testFraction=0.2,
                seed=17,
                split="random",
                folds=3,
                hyperparameters={"alpha": 1.0},
            )
            pred = engine.predict(
                {
                    "modelId": trained["id"],
                    "dataset": {"path": str(ds_path), "key": ds_key, "start": 40, "rowLimit": 8},
                },
                self.project,
                RESOURCE,
            )
            self.assertEqual(pred["rows"], 8)
            self.assertEqual(len(pred["predictions"]), 8)

            exported = engine.export({"experimentId": trained["id"]}, self.project)
            destination = self.root / f"export_{exp_name}"
            destination.mkdir()
            proc = subprocess.run(
                [sys.executable, "-I", "-B", exported["path"]],
                cwd=destination,
                capture_output=True,
                text=True,
                timeout=30,
            )
            self.assertEqual(proc.returncode, 0, proc.stderr)
            output = pd.read_csv(destination / "reproduced_predictions.csv")
            original = pd.read_csv(trained["outputPath"])
            self.assertEqual(output.shape[1], 1)
            np.testing.assert_allclose(output.iloc[:, 0], original["prediction"], atol=1e-9)

    def test_zero_crossing_production_and_export_parity(self):
        patterns = [
            [1.0, 0.0, -1.0, 0.0, 1.0, -1.0, 0.0, 2.0],
            [0.0, 0.0, 1.0, 0.0, 0.0, -1.0, 0.0, 0.0],
            [1.0, 0.0, 0.0, 2.0, -1.0, 0.0, -2.0, 0.0],
            [0.0, -2.0, 0.0, 2.0, 0.0, -2.0, 0.0, 0.0],
        ]
        rows = []
        for i in range(36):
            scale = 1.0 + 0.1 * (i + 1)
            base = np.array(patterns[i % len(patterns)], dtype=np.float64) * scale
            rows.append(base)
        matrix = np.vstack(rows)
        npy_path = self.root / "zc_parity.npy"
        np.save(npy_path, matrix)

        trained = self.train(
            name="ZeroCrossingExportParity",
            task="regression",
            algorithm="ridge",
            dataset={"path": str(npy_path), "key": "@waveform", "start": 0, "rowLimit": 32},
            target="wf_energy",
            features=["wf_zero_crossings", "wf_rms", "wf_ptp"],
            scaling="standard",
            testFraction=0.25,
            seed=23,
            split="random",
            folds=3,
            hyperparameters={"alpha": 0.5},
        )
        exported = engine.export({"experimentId": trained["id"]}, self.project)
        destination = self.root / "export_zc_parity"
        destination.mkdir()
        proc = subprocess.run(
            [sys.executable, "-I", "-B", exported["path"]],
            cwd=destination,
            capture_output=True,
            text=True,
            timeout=30,
        )
        self.assertEqual(proc.returncode, 0, proc.stderr)
        output = pd.read_csv(destination / "reproduced_predictions.csv")
        original = pd.read_csv(trained["outputPath"])
        np.testing.assert_allclose(output.iloc[:, 0], original["prediction"], atol=1e-9)


if __name__ == "__main__":
    unittest.main(verbosity=2)
