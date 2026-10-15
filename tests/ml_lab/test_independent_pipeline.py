"""Train-only transform and closed model registry checks independent of UI/worker."""
from __future__ import annotations

from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "workers" / "ml_lab"))

import numpy as np
import pandas as pd
from common import LabError
from models import CATALOG, make_model, make_pipeline, prepare_frame, preprocessing_details, schema_for

RESOURCE = {"memory_mb": 1024, "max_rows": 10000, "preview_rows": 1000, "workers": 2}


class IndependentPipelineTests(unittest.TestCase):
    def test_scaler_imputer_encoder_do_not_learn_held_out_rows(self):
        training = pd.DataFrame({"numeric": [1.0, np.nan, 5.0, 3.0], "category": ["a", "a", np.nan, "b"]})
        held_out = pd.DataFrame({"numeric": [99999.0, np.nan], "category": ["held-out-only", np.nan]})
        model = make_model("regression", "ridge", 42, {}, len(training), RESOURCE)
        pipeline, schema = make_pipeline(training, "standard", model, RESOURCE)
        pipeline.fit(prepare_frame(training, schema), np.array([1., 2., 3., 4.]))
        numeric = pipeline.named_steps["preprocessing"].named_transformers_["numeric"]
        categorical = pipeline.named_steps["preprocessing"].named_transformers_["categorical"]
        # Training median=3; imputed values [1,3,5,3], population mean=3 and variance=2.
        np.testing.assert_array_equal(numeric.named_steps["imputer"].statistics_, [3.0])
        np.testing.assert_array_equal(numeric.named_steps["scaler"].mean_, [3.0])
        np.testing.assert_allclose(numeric.named_steps["scaler"].scale_, [np.sqrt(2.0)])
        np.testing.assert_array_equal(categorical.named_steps["imputer"].statistics_, ["a"])
        self.assertEqual(categorical.named_steps["encoder"].categories_[0].tolist(), ["a", "b"])
        before = preprocessing_details(pipeline, "standard")
        result = pipeline.predict(prepare_frame(held_out, schema))
        self.assertEqual(result.shape, (2,))
        self.assertTrue(np.isfinite(result).all())
        self.assertEqual(before, preprocessing_details(pipeline, "standard"))
        self.assertNotIn("held-out-only", str(before))

    def test_none_scaling_is_honored_and_all_missing_numeric_retained(self):
        frame = pd.DataFrame({"x": [1., 2., 3.], "empty": [np.nan] * 3})
        model = make_model("regression", "linear", 42, {}, 3, RESOURCE)
        pipeline, schema = make_pipeline(frame, "none", model, RESOURCE)
        pipeline.fit(prepare_frame(frame, schema), [2., 4., 6.])
        transform = pipeline.named_steps["preprocessing"]
        self.assertNotIn("scaler", transform.named_transformers_["numeric"].named_steps)
        self.assertEqual(transform.transform(prepare_frame(frame, schema)).shape, (3, 2))
        np.testing.assert_allclose(pipeline.predict(prepare_frame(frame, schema)), [2., 4., 6.], atol=1e-12)

    def test_high_cardinality_expansion_rejected_before_fit(self):
        frame = pd.DataFrame({"label": [f"category-{index}" for index in range(513)]})
        model = make_model("regression", "ridge", 42, {}, len(frame), RESOURCE)
        with self.assertRaises(LabError) as caught:
            make_pipeline(frame, "standard", model, RESOURCE)
        self.assertEqual(caught.exception.code, "resource_limit")

    def test_model_registry_rejects_execution_and_unbounded_thread_parameters(self):
        for parameters in [{"n_jobs": -1}, {"random_state": 99}, {"callback": "execute"}, {"n_estimators": 501}, {"max_depth": 1000}]:
            with self.subTest(parameters=parameters), self.assertRaises(LabError):
                make_model("regression", "random_forest", 42, parameters, 100, RESOURCE)

    def test_task_algorithm_mismatch_and_quadratic_dataset_guard(self):
        for task, algorithm in [("classification", "linear"), ("regression", "logistic"), ("regression", "__import__"), ("causality", "ridge")]:
            with self.subTest(task=task, algorithm=algorithm), self.assertRaises(LabError):
                make_model(task, algorithm, 42, {}, 100, RESOURCE)
        for task, algorithm in [("classification", "svc"), ("regression", "svr"), ("clustering", "dbscan")]:
            with self.subTest(algorithm=algorithm), self.assertRaises(LabError) as caught:
                make_model(task, algorithm, 42, {}, 10001, RESOURCE)
            self.assertEqual(caught.exception.code, "resource_limit")

    def test_each_advertised_model_fits_small_deterministic_data(self):
        rng = np.random.default_rng(614)
        frame = pd.DataFrame(rng.normal(size=(36, 3)), columns=["a", "b", "c"])
        targets = {"regression": frame["a"].to_numpy() * 2 - frame["b"].to_numpy(),
                   "classification": (frame["a"].to_numpy() > 0).astype(int)}
        count = 0
        for entry in CATALOG:
            for task in entry["tasks"]:
                with self.subTest(algorithm=entry["id"], task=task):
                    model = make_model(task, entry["id"], 42, {}, len(frame), RESOURCE)
                    pipeline, schema = make_pipeline(frame, "standard", model, RESOURCE)
                    pipeline.fit(prepare_frame(frame, schema), targets.get(task))
                    self.assertEqual(preprocessing_details(pipeline, "standard")["outputFeatures"], ["numeric__a", "numeric__b", "numeric__c"])
                    if task in targets:
                        predictions = pipeline.predict(prepare_frame(frame.iloc[:3], schema))
                        self.assertEqual(len(predictions), 3)
                    count += 1
        self.assertGreaterEqual(count, 20)

    def test_nonfinite_values_are_explicitly_prepared_without_mutating_source(self):
        original = pd.DataFrame({"x": [np.inf, -np.inf, 1.0], "flag": [True, False, True]})
        before = original.copy(deep=True)
        prepared = prepare_frame(original, schema_for(original))
        self.assertTrue(prepared["x"].iloc[:2].isna().all())
        self.assertEqual(prepared["flag"].tolist(), ["True", "False", "True"])
        pd.testing.assert_frame_equal(original, before)


if __name__ == "__main__":
    unittest.main(verbosity=2)
