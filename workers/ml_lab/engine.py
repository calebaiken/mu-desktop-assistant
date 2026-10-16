"""Local experiment execution. Model output never becomes executable code."""
from __future__ import annotations

import importlib.metadata
import importlib.util
import itertools
import json
import os
from pathlib import Path
import platform
import time
import uuid
import warnings as warning_module
import zipfile

import numpy as np
import pandas as pd
import psutil
from sklearn import metrics as scores
from sklearn.model_selection import (train_test_split, GroupShuffleSplit, KFold, StratifiedKFold,
                                     GroupKFold, TimeSeriesSplit, cross_validate)
from sklearn.inspection import permutation_importance
from threadpoolctl import threadpool_limits
import skops.io as skops

from common import (SCHEMA_VERSION, LabError, atomic_write, bounded_int, clean, fail, hash_file,
                    identifier, json_bytes, local_path, now, read_json, write_json)
from data import FORMATS, browse, inspect_frame, read_dataset, source_identity
from models import CATALOG, make_model, make_pipeline, prepare_frame, preprocessing_details, schema_for

PACKAGES = ["numpy", "pandas", "scipy", "scikit-learn", "matplotlib", "h5py", "skops", "psutil"]
MODEL_MAX_BYTES = 64 * 1024 * 1024
# Exact pinned sklearn 1.7.2 primitive loss/link state needed by GradientBoosting.
# Reviewed installed link.py/loss.py; these are data/math objects, not user callbacks.
# There is deliberately no namespace wildcard or dynamic trust of discovered types.
SAFE_SERIALIZATION_TYPES = [
    "numpy.dtype", "sklearn._loss.link.IdentityLink", "sklearn._loss.link.Interval",
    "sklearn._loss.link.LogitLink", "sklearn._loss.link.MultinomialLogit",
    "sklearn._loss.loss.HalfSquaredError", "sklearn._loss.loss.HalfBinomialLoss",
    "sklearn._loss.loss.HalfMultinomialLoss", "_loss.CyHalfMultinomialLoss",
]


def versions():
    return {name: importlib.metadata.version(name) for name in PACKAGES} | {"python": platform.python_version()}


def capabilities(resource):
    return {"available": True, "versions": versions(), "algorithms": CATALOG,
            "formats": sorted(FORMATS), "device": "CPU",
            "optional": {name: importlib.util.find_spec(module) is not None for name, module in
                         [("UMAP", "umap"), ("HDBSCAN", "hdbscan"), ("SHAP", "shap"), ("PyTorch", "torch"),
                          ("XGBoost", "xgboost"), ("LightGBM", "lightgbm"), ("CatBoost", "catboost"),
                          ("Parquet", "pyarrow"), ("Excel", "openpyxl")]},
            "limits": resource, "hardware": hardware(),
            "warnings": ["Optional packages are detected only; advanced model adapters are not enabled in this foundation.",
                         "Pause/resume and GPU training are not supported. Cancel terminates the owned computation process."]}


def hardware():
    memory = psutil.virtual_memory()
    return {"device": "CPU", "logicalCores": psutil.cpu_count(), "physicalCores": psutil.cpu_count(logical=False),
            "availableRamBytes": int(memory.available), "processRamBytes": psutil.Process().memory_info().rss,
            "platform": platform.system(), "architecture": platform.machine()}


def inspect(parameters, project, resource):
    frame, metadata, warnings, sampled = read_dataset(parameters, resource, preview=True)
    features, preview, findings = inspect_frame(frame)
    metadata["importedAt"] = now()
    dataset_dir = project / "datasets"
    dataset_dir.mkdir(exist_ok=True)
    write_json(dataset_dir / (metadata["id"] + ".json"), metadata)
    # Preview plots are regenerated only on explicit inspection; bounded to 1000 rows.
    plot_dir = project / "figures" / metadata["id"]
    plot_dir.mkdir(parents=True, exist_ok=True)
    plots = inspection_plots(frame, plot_dir)
    return {"dataset": metadata, "features": features, "preview": preview, "warnings": warnings + findings,
            "plots": plots, "sampled": sampled, "inspectionScope": "selected leading rows", "mode": "inspection"}


def _plot_modules():
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    return plt


def _save_plot(plt, figure, path, name):
    local_path(str(path), exists=False)
    try:
        figure.tight_layout()
        figure.savefig(path, dpi=120, format="png")
    finally:
        plt.close(figure)
    return {"name": name, "path": str(path)}


def inspection_plots(frame, directory):
    plt = _plot_modules()
    numeric = frame.select_dtypes(include=np.number).replace([np.inf, -np.inf], np.nan)
    plots = []
    columns = list(numeric.columns[:6])
    if columns:
        figure, axes = plt.subplots(len(columns), 1, figsize=(7, max(3, 2 * len(columns))), squeeze=False)
        for axis, column in zip(axes[:, 0], columns):
            axis.hist(numeric[column].dropna(), bins=30, color="#317461", edgecolor="white")
            axis.set_title(str(column), fontsize=10)
            axis.set_ylabel("Count")
        plots.append(_save_plot(plt, figure, directory / "distributions.png", "Selected-row distributions"))
    if 2 <= len(numeric.columns) <= 20:
        correlation = numeric.corr(numeric_only=True)
        figure, axis = plt.subplots(figsize=(7, 6))
        image = axis.imshow(correlation, vmin=-1, vmax=1, cmap="coolwarm")
        axis.set_xticks(range(len(correlation)), correlation.columns, rotation=75, ha="right", fontsize=8)
        axis.set_yticks(range(len(correlation)), correlation.columns, fontsize=8)
        axis.set_title("Pearson correlations in selected rows; association is not causation")
        figure.colorbar(image, ax=axis)
        plots.append(_save_plot(plt, figure, directory / "correlation.png", "Correlation matrix"))
    return plots


def _validate_train(parameters, frame):
    task = parameters.get("task")
    if task not in ("regression", "classification", "clustering", "reduction", "anomaly"):
        fail("Choose a supported learning task.")
    features = parameters.get("features")
    if not isinstance(features, list) or not features or len(features) != len(set(features)) or any(not isinstance(x, str) or x not in frame.columns for x in features):
        fail("Select unique existing feature columns.")
    target = parameters.get("target")
    if task in ("regression", "classification") and (target not in frame.columns or target in features):
        fail("Select an existing target column that is not also an input feature.")
    if task not in ("regression", "classification") and target is not None:
        fail("Unsupervised tasks do not train on a target. Clear the target selection.")
    name = parameters.get("name") or "ML experiment"
    if not isinstance(name, str) or not name.strip() or len(name) > 120 or any(ord(x) < 32 for x in name):
        fail("Experiment names must contain 1 to 120 characters without control characters.")
    seed = bounded_int(parameters.get("seed"), 42, 0, 2147483647, "seed")
    folds = bounded_int(parameters.get("folds"), 0, 0, 10, "folds")
    if folds == 1:
        fail("Cross-validation requires 2 to 10 folds, or 0 to disable it.")
    fraction = parameters.get("testFraction", .2)
    if type(fraction) not in (int, float) or not .1 <= fraction <= .5:
        fail("Test fraction must be between 0.1 and 0.5.")
    return task, features, target, name, seed, folds, float(fraction)


def _split(parameters, frame, target, task, seed, fraction):
    split = parameters.get("split", "stratified" if task == "classification" else "random")
    if split not in ("random", "stratified", "group", "time"):
        fail("Split must be random, stratified, group or time.")
    indices = np.arange(len(frame))
    group_column = parameters.get("groupColumn")
    group_values = None
    if split in ("group", "time"):
        if group_column not in frame or frame[group_column].isna().any():
            fail("Choose a nonmissing grouping/time column for this split.")
        if group_column in parameters["features"]:
            fail("Remove the grouping/time split column from predictive features explicitly before training.")
        group_values = frame[group_column].to_numpy()
    if split == "stratified":
        if task != "classification":
            fail("Stratified splitting is supported only for classification.")
        if pd.Series(target).value_counts().min() < 2:
            fail("Each class needs at least two rows for stratified splitting.")
        train, test = train_test_split(indices, test_size=fraction, random_state=seed, stratify=target)
    elif split == "group":
        train, test = next(GroupShuffleSplit(n_splits=1, test_size=fraction, random_state=seed).split(frame, target, group_values))
    elif split == "time":
        # Stable sorting makes equal timestamps deterministic; boundary ties must not
        # place the same time value into both partitions.
        order = np.argsort(group_values, kind="stable")
        cut = len(order) - max(1, int(np.ceil(len(order) * fraction)))
        while cut > 0 and group_values[order[cut - 1]] == group_values[order[cut]]:
            cut -= 1
        if cut < 2:
            fail("The time split leaves too few training rows. Select a wider time range.")
        train, test = order[:cut], order[cut:]
    else:
        train, test = train_test_split(indices, test_size=fraction, random_state=seed)
    if min(len(train), len(test)) < 2:
        fail("Both training and test partitions need at least two rows.")
    if task == "classification" and (len(np.unique(target[train])) < 2 or set(np.unique(target[test])) - set(np.unique(target[train]))):
        fail("Every test class must occur in training, with at least two training classes. Use a stratified split or revise the group/time selection.")
    return train, test, split, group_values


def _regression_metrics(actual, predicted):
    residual = np.asarray(actual) - np.asarray(predicted)
    return {"mae": scores.mean_absolute_error(actual, predicted), "mse": scores.mean_squared_error(actual, predicted),
            "rmse": float(np.sqrt(scores.mean_squared_error(actual, predicted))), "r2": scores.r2_score(actual, predicted),
            "explainedVariance": scores.explained_variance_score(actual, predicted),
            "residualMean": float(residual.mean()), "residualStd": float(residual.std(ddof=1))}


def _classification_metrics(actual, predicted, pipeline, features):
    labels = pipeline.named_steps["model"].classes_
    output = {"accuracy": scores.accuracy_score(actual, predicted), "balancedAccuracy": scores.balanced_accuracy_score(actual, predicted),
              "precisionMacro": scores.precision_score(actual, predicted, average="macro", zero_division=0),
              "recallMacro": scores.recall_score(actual, predicted, average="macro", zero_division=0),
              "f1Macro": scores.f1_score(actual, predicted, average="macro", zero_division=0),
              "f1Weighted": scores.f1_score(actual, predicted, average="weighted", zero_division=0),
              "labels": labels.tolist(), "confusionMatrix": scores.confusion_matrix(actual, predicted, labels=labels).tolist()}
    if hasattr(pipeline, "predict_proba"):
        probability = pipeline.predict_proba(features)
        if set(np.unique(actual)) <= set(labels):
            output["logLoss"] = scores.log_loss(actual, probability, labels=labels)
        if len(labels) == 2 and len(np.unique(actual)) == 2:
            binary = np.asarray(actual) == labels[1]
            output["rocAuc"] = scores.roc_auc_score(binary, probability[:, 1])
            output["averagePrecision"] = scores.average_precision_score(binary, probability[:, 1])
            output["positiveClass"] = clean(labels[1])
    return output


def _cv(pipeline, features, target, train, split, groups, folds, task, seed):
    if not folds:
        return None
    if folds > len(train) // 2:
        fail("Each cross-validation fold needs at least two training-partition rows.")
    x, y = features.iloc[train], target[train]
    if split == "group":
        selected_groups = groups[train]
        if len(np.unique(selected_groups)) < folds:
            fail("Cross-validation folds exceed the number of training groups.")
        cv = GroupKFold(n_splits=folds)
    elif split == "time":
        selected_groups = None
        if len(np.unique(groups[train])) != len(train):
            fail("Time-aware cross-validation currently requires unique timestamps in the training partition. Disable folds or aggregate repeated timestamps explicitly.")
        cv = TimeSeriesSplit(n_splits=folds)
    elif task == "classification":
        if pd.Series(y).value_counts().min() < folds:
            fail("Each class in the training partition needs at least one row per stratified fold.")
        selected_groups = None
        cv = StratifiedKFold(n_splits=folds, shuffle=True, random_state=seed)
    else:
        selected_groups = None
        cv = KFold(n_splits=folds, shuffle=True, random_state=seed)
    scoring = {"rmse": "neg_root_mean_squared_error", "r2": "r2"} if task == "regression" else {"accuracy": "accuracy", "f1Macro": "f1_macro"}
    result = cross_validate(pipeline, x, y, cv=cv, groups=selected_groups, scoring=scoring, n_jobs=1, error_score="raise")
    result_metrics = {}
    for key in scoring:
        values = result["test_" + key] * (-1 if key == "rmse" else 1)
        result_metrics[key] = {"mean": float(values.mean()), "std": float(values.std(ddof=1)), "foldValues": values.tolist()}
    return {"folds": folds, "scope": "training partition only; test partition untouched", "metrics": result_metrics}


def _save_model(pipeline, directory):
    path = directory / "model.skops"
    temporary = directory / "model.pending.skops"
    skops.dump(pipeline, temporary)
    if temporary.stat().st_size > MODEL_MAX_BYTES:
        temporary.unlink()
        fail("The model artifact exceeds the 64 MiB foundation limit. Use fewer trees or a smaller model.", "resource_limit")
    unknown = skops.get_untrusted_types(file=temporary)
    if set(unknown) - set(SAFE_SERIALIZATION_TYPES):
        temporary.unlink()
        fail("This model requires serialization types outside the reviewed safe set. Choose another algorithm.", "unsupported_model")
    os.replace(temporary, path)
    return path, hash_file(path)


def train(parameters, project: Path, resource):
    started = time.perf_counter()
    frame, dataset, warnings, sampled = read_dataset(parameters.get("dataset"), resource)
    task, feature_names, target_name, name, seed, folds, fraction = _validate_train(parameters, frame)
    experiment_id = str(uuid.uuid4())
    directory = project / "experiments" / experiment_id
    local_path(str(directory), exists=False)
    directory.mkdir(parents=True)
    initial = {"schemaVersion": SCHEMA_VERSION, "id": experiment_id, "name": name, "createdAt": now(),
               "status": "running", "dataset": dataset, "task": task, "algorithm": parameters.get("algorithm"),
               "features": feature_names, "target": target_name, "parameters": parameters, "seed": seed,
               "warnings": warnings, "software": versions(), "hardware": hardware(), "compute": resource, "version": 1,
               "metadataPath": str(directory / "experiment.json")}
    write_json(directory / "experiment.json", initial)
    try:
        if len(frame) < 10:
            fail("Training requires at least 10 selected rows.")
        excluded = []
        if task in ("regression", "classification"):
            missing_target = frame[target_name].isna()
            if task == "regression":
                if not pd.api.types.is_numeric_dtype(frame[target_name]):
                    fail("Regression needs a numeric target.")
                missing_target |= ~np.isfinite(frame[target_name].astype(float))
            excluded = frame.index[missing_target].tolist()
            frame = frame.loc[~missing_target].copy()
            if excluded:
                warnings.append(f"{len(excluded)} rows with missing/nonfinite targets were excluded; row indices are recorded.")
            if task == "classification":
                unique = frame[target_name].nunique()
                if unique < 2 or unique > 50:
                    fail("Classification requires 2 to 50 target classes in the selected rows.")
                # Homogeneous strings avoid ambiguous numeric versus text label serialization.
                target = frame[target_name].map(str).to_numpy()
            else:
                target = frame[target_name].to_numpy(dtype=float)
        else:
            if folds:
                fail("Cross-validation is currently available only for supervised tasks.")
            target = None
        original_indices = frame.index.to_numpy()
        frame.reset_index(drop=True, inplace=True)
        features = frame[feature_names].copy()
        if len(features) < 10:
            fail("Fewer than 10 usable rows remain after target validation.")
        estimate = int(features.memory_usage(deep=True).sum()) * 12 + 256 * 1024 * 1024
        if estimate > resource["memory_mb"] * 1024 * 1024 or estimate > psutil.virtual_memory().available * .8:
            fail("Estimated working RAM exceeds the configured or available memory. Select fewer rows/features.", "resource_limit")
        split = "none"
        group_values = None
        if target is not None:
            train_rows, test_rows, split, group_values = _split(parameters, frame, target, task, seed, fraction)
        else:
            train_rows, test_rows = np.arange(len(frame)), np.array([], dtype=int)
        model = make_model(task, parameters.get("algorithm"), seed, parameters.get("hyperparameters") or {}, len(train_rows), resource)
        pipeline, schema = make_pipeline(features.iloc[train_rows], parameters.get("scaling", "standard"), model, resource)
        features = prepare_frame(features, schema)
        with threadpool_limits(limits=resource["workers"]), warning_module.catch_warnings(record=True) as captured:
            warning_module.simplefilter("always")
            cv = _cv(pipeline, features, target, train_rows, split, group_values, folds, task, seed) if target is not None else None
            fit_start = time.perf_counter()
            pipeline.fit(features.iloc[train_rows], target[train_rows] if target is not None else None)
            fit_duration = time.perf_counter() - fit_start
            prediction_start = time.perf_counter()
            if task == "regression":
                predicted = pipeline.predict(features.iloc[test_rows])
                evaluation = _regression_metrics(target[test_rows], predicted)
            elif task == "classification":
                predicted = pipeline.predict(features.iloc[test_rows])
                evaluation = _classification_metrics(target[test_rows], predicted, pipeline, features.iloc[test_rows])
            else:
                transformed = pipeline.named_steps["preprocessing"].transform(features)
                if task == "reduction":
                    predicted = model.transform(transformed)
                    evaluation = {"explainedVarianceRatio": model.explained_variance_ratio_.tolist(),
                                  "explainedVarianceTotal": float(model.explained_variance_ratio_.sum()),
                                  "components": model.components_.tolist()}
                elif task == "clustering":
                    predicted = model.labels_ if hasattr(model, "labels_") else model.predict(transformed)
                    labels, counts = np.unique(predicted, return_counts=True)
                    evaluation = {"clusterCounts": {str(label): int(count) for label, count in zip(labels, counts)},
                                  "noiseRows": int(np.sum(predicted == -1))}
                    nonnoise = predicted != -1
                    distinct = len(np.unique(predicted[nonnoise]))
                    if 2 <= distinct < int(nonnoise.sum()):
                        sample = np.flatnonzero(nonnoise)
                        if len(sample) > 2000:
                            sample = np.random.default_rng(seed).choice(sample, 2000, replace=False)
                        if 2 <= len(np.unique(predicted[sample])) < len(sample):
                            evaluation["silhouette"] = scores.silhouette_score(transformed[sample], predicted[sample])
                            evaluation["daviesBouldin"] = scores.davies_bouldin_score(transformed[sample], predicted[sample])
                            evaluation["calinskiHarabasz"] = scores.calinski_harabasz_score(transformed[sample], predicted[sample])
                            evaluation["metricSampleRows"] = len(sample)
                    warnings.append("Clusters describe the selected feature representation. They do not establish physical populations.")
                else:
                    if parameters["algorithm"] == "local_outlier_factor":
                        anomaly_score = -model.negative_outlier_factor_
                        predicted = np.where(model.negative_outlier_factor_ < model.offset_, -1, 1)
                    else:
                        anomaly_score = -model.score_samples(transformed)
                        predicted = model.predict(transformed)
                    evaluation = {"anomalyRows": int(np.sum(predicted == -1)), "inlierRows": int(np.sum(predicted == 1)),
                                  "scoreDirection": "higher is more unusual", "rankedRowIndices": (original_indices[np.argsort(-anomaly_score)[:100]] + dataset["start"]).tolist(),
                                  "rankedScores": np.sort(anomaly_score)[::-1][:100].tolist()}
                    warnings.append("Anomaly labels depend on feature representation and contamination assumptions. No rows were removed.")
            inference_duration = time.perf_counter() - prediction_start
            warnings += sorted({f"{type(w.message).__name__}: model reported a numerical/convergence warning; review scaling, sample size and hyperparameters." for w in captured})
        if task in ("regression", "classification"):
            warnings.append("Predictive performance and feature importance do not establish causality or physical laws.")
            if split in ("random", "stratified"):
                warnings.append("Random row splitting assumes independent rows. Repeated objects or sequential events may require group/time splitting.")
        details = preprocessing_details(pipeline, parameters.get("scaling", "standard"))
        interpretability = {}
        if hasattr(model, "coef_"):
            interpretability = {"method": "linear_coefficients", "features": details["outputFeatures"], "values": clean(model.coef_.tolist())}
        elif hasattr(model, "feature_importances_"):
            interpretability = {"method": "impurity_importance", "features": details["outputFeatures"], "values": model.feature_importances_.tolist(),
                               "warning": "Impurity importance can favor high-cardinality features and is not causal evidence."}
        plots = training_plots(task, target[test_rows] if target is not None else None, predicted, evaluation, directory, interpretability)
        model_path, model_hash = _save_model(pipeline, directory)
        assignment = pd.DataFrame({"sourceRow": (original_indices[test_rows] if target is not None else original_indices) + dataset["start"]})
        if np.asarray(predicted).ndim == 1:
            assignment["prediction"] = predicted
        else:
            for index in range(np.asarray(predicted).shape[1]):
                assignment[f"component_{index + 1}"] = predicted[:, index]
        if task == "anomaly":
            assignment["anomalyScore"] = anomaly_score
        assignment.to_csv(directory / "predictions.csv", index=False)
        result = initial | {"status": "completed", "completedAt": now(), "preprocessing": details,
                            "hyperparameters": clean(model.get_params(deep=False)), "featureSchema": schema,
                            "excludedRows": [int(x) + dataset["start"] for x in excluded],
                            "split": {"strategy": split, "testFraction": fraction if target is not None else None, "seed": seed,
                                      "shuffle": split in ("random", "stratified"), "groupColumn": parameters.get("groupColumn"),
                                      "trainRows": (original_indices[train_rows] + dataset["start"]).tolist(),
                                      "testRows": (original_indices[test_rows] + dataset["start"]).tolist()},
                            "metrics": clean(evaluation), "validation": clean(cv), "interpretability": interpretability,
                            "fitDurationSeconds": fit_duration, "inferenceDurationSeconds": inference_duration,
                            "durationSeconds": time.perf_counter() - started, "memoryEstimateBytes": estimate,
                            "modelId": experiment_id, "modelPath": str(model_path), "modelSha256": model_hash,
                            "modelBytes": model_path.stat().st_size, "modelVersion": 1,
                            "predictionSupported": task != "clustering" or parameters["algorithm"] != "dbscan",
                            "plots": plots, "warnings": list(dict.fromkeys(warnings)), "outputPath": str(directory / "predictions.csv"),
                            "scriptPath": str(directory / "reproduce.py")}
        # Recheck source identity after model execution, not only after parsing.
        if source_identity(Path(dataset["path"])) != {k: dataset[k] for k in ("fileSize", "modifiedAt", "fingerprint", "fingerprintKind")}:
            fail("The dataset changed during training. This result cannot be registered as reproducible.", "source_changed")
        atomic_write(directory / "reproduce.py", reproduction_script(result).encode("utf-8"))
        write_json(directory / "experiment.json", result)
        return result
    except Exception as error:
        failed = initial | {"status": "failed", "completedAt": now(), "durationSeconds": time.perf_counter() - started,
                            "errorCode": error.code if isinstance(error, LabError) else "computation_failed",
                            "error": str(error) if isinstance(error, LabError) else "The computation failed. Check the feature types, split and algorithm configuration."}
        write_json(directory / "experiment.json", failed)
        raise


def training_plots(task, actual, predicted, evaluation, directory, interpretation):
    plt = _plot_modules()
    plots = []
    sample = np.arange(min(2000, len(predicted)))
    if task == "regression":
        figure, axes = plt.subplots(1, 2, figsize=(10, 4))
        axes[0].scatter(actual[sample], predicted[sample], s=12, alpha=.6, color="#317461")
        bounds = [min(np.min(actual[sample]), np.min(predicted[sample])), max(np.max(actual[sample]), np.max(predicted[sample]))]
        axes[0].plot(bounds, bounds, color="#b56551")
        axes[0].set(xlabel="Observed", ylabel="Predicted", title="Held-out predictions")
        axes[1].scatter(predicted[sample], actual[sample] - predicted[sample], s=12, alpha=.6, color="#317461")
        axes[1].axhline(0, color="#b56551")
        axes[1].set(xlabel="Predicted", ylabel="Observed − predicted", title="Held-out residuals")
        plots.append(_save_plot(plt, figure, directory / "evaluation.png", "Predictions and residuals"))
    elif task == "classification":
        figure, axis = plt.subplots(figsize=(6, 5))
        scores.ConfusionMatrixDisplay(np.asarray(evaluation["confusionMatrix"]), display_labels=evaluation["labels"]).plot(ax=axis, cmap="Greens", colorbar=False)
        axis.set_title("Held-out confusion matrix")
        plots.append(_save_plot(plt, figure, directory / "evaluation.png", "Confusion matrix"))
    elif task == "reduction":
        figure, axes = plt.subplots(1, 2, figsize=(10, 4))
        projection = np.asarray(predicted)
        axes[0].scatter(projection[sample, 0], projection[sample, 1] if projection.shape[1] > 1 else np.zeros(len(sample)), s=12, alpha=.6)
        axes[0].set(xlabel="PC1", ylabel="PC2" if projection.shape[1] > 1 else "Zero", title="PCA selected-row projection")
        axes[1].bar(np.arange(1, len(evaluation["explainedVarianceRatio"]) + 1), evaluation["explainedVarianceRatio"])
        axes[1].set(xlabel="Component", ylabel="Explained variance ratio")
        plots.append(_save_plot(plt, figure, directory / "evaluation.png", "PCA projection and variance"))
    else:
        figure, axis = plt.subplots(figsize=(7, 4))
        if task == "clustering":
            counts = evaluation["clusterCounts"]
            axis.bar(list(counts), list(counts.values()), color="#317461")
            axis.set(xlabel="Cluster (−1 is noise)", ylabel="Rows", title="Selected-row cluster sizes")
        else:
            axis.hist(evaluation["rankedScores"], bins=20, color="#317461")
            axis.set(xlabel="Anomaly score (higher is more unusual)", ylabel="Rows", title="Top 100 anomaly scores")
        plots.append(_save_plot(plt, figure, directory / "evaluation.png", "Cluster counts" if task == "clustering" else "Ranked anomaly distribution"))
    values = interpretation.get("values")
    if values is not None and np.asarray(values).ndim == 1:
        array = np.asarray(values)
        order = np.argsort(np.abs(array))[-20:]
        figure, axis = plt.subplots(figsize=(8, max(4, .3 * len(order))))
        axis.barh(np.asarray(interpretation["features"])[order], array[order], color="#317461")
        axis.set_title(interpretation["method"] + "; not causal effects")
        plots.append(_save_plot(plt, figure, directory / "importance.png", "Feature interpretation"))
    return plots


def _experiment(project, experiment_id):
    experiment_id = identifier(experiment_id)
    directory = project / "experiments" / experiment_id
    metadata = read_json(directory / "experiment.json")
    if metadata.get("schemaVersion") != SCHEMA_VERSION or metadata.get("id") != experiment_id:
        fail("The saved experiment metadata is incompatible or invalid.", "invalid_artifact")
    return directory, metadata


def list_project(project):
    experiments, models, datasets, warnings = [], [], [], []
    experiment_root = project / "experiments"
    if experiment_root.exists():
        local_path(str(experiment_root), exists=False)
        paths = list(itertools.islice(experiment_root.iterdir(), 201))
        if len(paths) > 200:
            warnings.append("History displays at most 200 experiment directories. Additional files were preserved.")
        for path in paths[:200]:
            try:
                _, value = _experiment(project, path.name)
                if value.get("status") == "running":
                    value = value | {"status": "interrupted", "warning": "Worker did not finish; no model from this job is admitted."}
                # Full row-index arrays remain in per-experiment files; catalog stays bounded.
                brief = {k: v for k, v in value.items() if k not in ("split", "preprocessing", "interpretability", "parameters")}
                brief["split"] = {k: v for k, v in value.get("split", {}).items() if k not in ("trainRows", "testRows")}
                experiments.append(brief)
                if value.get("status") == "completed":
                    models.append({"id": value["id"], "name": value["name"], "algorithm": value["algorithm"], "task": value["task"],
                                   "features": value["features"], "target": value["target"], "createdAt": value["createdAt"],
                                   "metrics": value["metrics"], "version": value["modelVersion"], "modelBytes": value["modelBytes"]})
            except (LabError, ValueError, OSError, KeyError):
                warnings.append("One saved experiment could not be read; its files were preserved.")
    dataset_root = project / "datasets"
    if dataset_root.exists():
        local_path(str(dataset_root), exists=False)
        paths = list(itertools.islice(dataset_root.glob("*.json"), 201))
        if len(paths) > 200:
            warnings.append("History displays at most 200 dataset references. Additional files were preserved.")
        for path in paths[:200]:
            try:
                datasets.append(read_json(path, 65536))
            except (LabError, ValueError, OSError):
                warnings.append("One dataset reference could not be read; its files were preserved.")
    return {"experiments": sorted(experiments, key=lambda x: x["createdAt"], reverse=True), "models": models, "datasets": datasets,
            "warnings": warnings, "projectDir": str(project)}


def load_model(project, model_id):
    directory, metadata = _experiment(project, model_id)
    if metadata.get("status") != "completed":
        fail("This experiment did not complete and has no admitted model.", "invalid_artifact")
    current_versions = versions()
    if any(metadata.get("software", {}).get(k) != current_versions[k] for k in ("scikit-learn", "numpy", "scipy", "skops")):
        fail("Saved model library versions differ from this runtime. Reproduce/retrain the experiment instead of loading it.", "incompatible_model")
    path = local_path(str(directory / "model.skops"))
    if path.stat().st_size > MODEL_MAX_BYTES or hash_file(path) != metadata.get("modelSha256"):
        fail("Saved model size/hash validation failed. The artifact was preserved and not loaded.", "invalid_artifact")
    with zipfile.ZipFile(path) as archive:
        entries = archive.infolist()
        if len(entries) > 20000 or sum(x.file_size for x in entries) > 256 * 1024 * 1024:
            fail("Saved model expansion exceeds its admission limit.", "resource_limit")
    if set(skops.get_untrusted_types(file=path)) - set(SAFE_SERIALIZATION_TYPES):
        fail("The saved model contains untrusted serialization types and will not be loaded.", "untrusted_model")
    return skops.load(path, trusted=SAFE_SERIALIZATION_TYPES), metadata


def predict(parameters, project, resource):
    pipeline, metadata = load_model(project, parameters.get("modelId"))
    if not metadata.get("predictionSupported"):
        fail("This clustering algorithm has no out-of-sample prediction method. Its saved assignments remain available.", "unsupported_prediction")
    frame, dataset, warnings, _ = read_dataset(parameters.get("dataset"), resource)
    expected = metadata["featureSchema"]
    missing = [x["name"] for x in expected if x["name"] not in frame]
    if missing:
        fail("Incoming data are missing required feature columns: " + ", ".join(missing[:12]), "schema_mismatch")
    actual = {x["name"]: x["type"] for x in schema_for(frame)}
    if any(actual[x["name"]] != x["type"] for x in expected):
        fail("Incoming feature types differ from the saved numeric/boolean/categorical schema.", "schema_mismatch")
    selected_names = [x["name"] for x in expected]
    if [x for x in frame.columns if x in selected_names] != selected_names:
        fail("Incoming feature order differs from the saved schema. Reorder input columns explicitly before predicting.", "schema_mismatch")
    features = prepare_frame(frame[selected_names], expected)
    categorical = pipeline.named_steps["preprocessing"].named_transformers_.get("categorical")
    if categorical is not None:
        category_names = [x["name"] for x in expected if x["type"] != "numeric"]
        for column, categories in zip(category_names, categorical.named_steps["encoder"].categories_):
            unknown = int((features[column].notna() & ~features[column].isin(categories)).sum())
            if unknown:
                warnings.append(f"{column}: {unknown} rows contain categories not seen during training; the saved encoder represents them with zero indicators.")
    with threadpool_limits(limits=resource["workers"]):
        predicted = pipeline.transform(features) if metadata["task"] == "reduction" else pipeline.predict(features)
        probability = pipeline.predict_proba(features) if metadata["task"] == "classification" and hasattr(pipeline, "predict_proba") else None
    output = pd.DataFrame({"sourceRow": np.arange(len(frame)) + dataset["start"]})
    if np.asarray(predicted).ndim == 1:
        output["prediction"] = predicted
    else:
        for index in range(predicted.shape[1]):
            output[f"component_{index + 1}"] = predicted[:, index]
    if probability is not None:
        for index, label in enumerate(pipeline.named_steps["model"].classes_):
            output[f"probability_{label}"] = probability[:, index]
    prediction_dir = project / "predictions"
    local_path(str(prediction_dir), exists=False)
    prediction_dir.mkdir(exist_ok=True)
    output_path = prediction_dir / (str(uuid.uuid4()) + ".csv")
    output.to_csv(output_path, index=False)
    write_json(output_path.with_suffix(".json"), {"modelId": metadata["id"], "modelVersion": metadata["modelVersion"],
                                                 "dataset": dataset, "createdAt": now(), "rows": len(frame), "warnings": warnings})
    return {"modelId": metadata["id"], "version": metadata["modelVersion"], "rows": len(frame),
            "predictions": clean(predicted[:100].tolist()), "probabilities": clean(probability[:100].tolist()) if probability is not None else None,
            "preview": clean(output.head(30).to_dict(orient="records")), "outputPath": str(output_path), "warnings": warnings,
            "previewLimit": 100, "featureOrder": selected_names}


def export(parameters, project):
    directory, metadata = _experiment(project, parameters.get("experimentId"))
    if metadata.get("status") != "completed":
        fail("Only completed experiments can be exported.")
    path = directory / "reproduce.py"
    atomic_write(path, reproduction_script(metadata).encode("utf-8"))
    return {"path": str(path), "experimentId": metadata["id"]}


def describe(parameters, project):
    _, metadata = _experiment(project, parameters.get("experimentId"))
    if metadata.get("status") == "running":
        metadata = metadata | {"status": "interrupted", "warning": "Worker did not finish. Artifacts remain preserved but no model is admitted."}
    return metadata


def compare(parameters, project):
    values = parameters.get("experimentIds")
    if not isinstance(values, list) or not 2 <= len(values) <= 5 or len(set(values)) != len(values):
        fail("Choose two to five distinct completed experiments for comparison.")
    experiments = [_experiment(project, value)[1] for value in values]
    if any(item.get("status") != "completed" for item in experiments):
        fail("Only completed experiments can be compared.", "incompatible_comparison")

    def identity(item):
        dataset = item["dataset"]
        split = item["split"]
        return {"dataset": {key: dataset.get(key) for key in ("fingerprint", "fingerprintKind", "start", "rowLimit", "selectedRows", "key")},
                "features": item["features"], "schema": item["featureSchema"], "target": item["target"], "task": item["task"],
                "split": split, "excludedRows": item["excludedRows"], "folds": item["parameters"].get("folds", 0)}

    baseline = identity(experiments[0])
    if any(identity(item) != baseline for item in experiments[1:]):
        fail("Experiments must use the same source fingerprint, selected rows, feature schema, target, task, exact holdout rows and cross-validation strategy. Duplicate an existing configuration to create a comparable run.", "incompatible_comparison")
    rows = []
    for item in experiments:
        rows.append({"id": item["id"], "name": item["name"], "algorithm": item["algorithm"],
                     "scaling": item["preprocessing"]["scaling"], "hyperparameters": item["hyperparameters"],
                     "validation": item.get("validation"), "metrics": item["metrics"],
                     "fitDurationSeconds": item["fitDurationSeconds"], "inferenceDurationSeconds": item["inferenceDurationSeconds"],
                     "modelBytes": item["modelBytes"], "featureCount": len(item["features"]),
                     "transformedFeatureCount": len(item["preprocessing"]["outputFeatures"]), "modelVersion": item["modelVersion"]})
    warnings = ["No universal best model is selected. Consider predictive error, simplicity, runtime and the scientific assumptions separately.",
                "Repeatedly choosing models using held-out test results can overfit the test set. Use training-only cross-validation for selection, then reserve a fresh final test set."]
    if experiments[0]["dataset"]["fingerprintKind"] != "sha256-full":
        warnings.append("Source identity uses sampled fingerprints and modification metadata rather than a full-file content hash.")
    return {"models": rows, "task": experiments[0]["task"], "datasetId": experiments[0]["dataset"]["id"],
            "split": {key: value for key, value in experiments[0]["split"].items() if key not in ("trainRows", "testRows")},
            "warnings": warnings}


def reproduction_script(experiment):
    """Standalone, inspectable source; data are loaded again, never embedded."""
    # Every interpolated user value is encoded as a Python string literal via repr
    # or as JSON data. No model/user formula is executed.
    config = {k: experiment[k] for k in ("dataset", "task", "algorithm", "features", "target", "featureSchema", "split", "hyperparameters", "software", "compute")}
    config["scaling"] = experiment["preprocessing"]["scaling"]
    config["parameters"] = experiment["parameters"]
    header = '# Mu ML Lab reproducible experiment. All computation stays local.\n# Install the exact versions in CONFIG["software"]. Output is a fresh fit, not a loaded pickle.\n'
    return header + "CONFIG_JSON = " + repr(json.dumps(clean(config), ensure_ascii=False)) + '\n' + _EXPORT_BODY


_EXPORT_BODY = r'''
import json
import importlib.metadata
import hashlib
import csv
import io
import itertools
from pathlib import Path
import numpy as np
import pandas as pd
from sklearn.compose import ColumnTransformer
from sklearn.pipeline import Pipeline
from sklearn.impute import SimpleImputer
from sklearn.preprocessing import StandardScaler, RobustScaler, MinMaxScaler, OneHotEncoder
from sklearn import linear_model, tree, ensemble, svm, neighbors, cluster, mixture, decomposition, metrics
from sklearn.model_selection import KFold, StratifiedKFold, GroupKFold, TimeSeriesSplit, cross_validate
from threadpoolctl import threadpool_limits

CONFIG = json.loads(CONFIG_JSON)
for package in ("numpy", "pandas", "scipy", "scikit-learn"):
    if importlib.metadata.version(package) != CONFIG["software"][package]:
        raise RuntimeError("Version mismatch: " + package)
source = CONFIG["dataset"]
path = Path(source["path"])
stat = path.stat()
digest = hashlib.sha256()
with path.open("rb") as stream:
    if source["fingerprintKind"] == "sha256-full":
        for block in iter(lambda: stream.read(1048576), b""):
            digest.update(block)
    else:
        for offset in sorted({0, max(0, stat.st_size // 2 - 524288), max(0, stat.st_size - 1048576)}):
            stream.seek(offset)
            digest.update(str(offset).encode("ascii"))
            digest.update(stream.read(1048576))
        digest.update(str(stat.st_size).encode("ascii"))
        digest.update(str(stat.st_mtime_ns).encode("ascii"))
if digest.hexdigest() != source["fingerprint"]:
    raise RuntimeError("Dataset fingerprint changed; do not treat this as the same experiment.")
start, count, key = source["start"], source["rowLimit"], source["key"]
suffix = path.suffix.lower()
if suffix in (".csv", ".tsv"):
    delimiter = "\t" if suffix == ".tsv" else ","
    with path.open(encoding="utf-8-sig", newline="") as stream:
        reader = csv.reader(stream, delimiter=delimiter, strict=True)
        header = next(reader)
        rows = itertools.islice((row for row in reader if row), start, start + count)
        selected = io.StringIO(newline="")
        writer = csv.writer(selected, delimiter=delimiter)
        writer.writerow(header)
        writer.writerows(rows)
    selected.seek(0)
    frame = pd.read_csv(selected, sep=delimiter, low_memory=False)
elif suffix == ".json":
    with path.open(encoding="utf-8-sig") as stream:
        frame = pd.DataFrame(json.load(stream)[start:start + count])
elif suffix == ".jsonl":
    import itertools
    with path.open(encoding="utf-8-sig") as stream:
        rows = (json.loads(line) for line in stream if line.strip())
        frame = pd.DataFrame(itertools.islice(rows, start, start + count))
elif suffix in (".npy", ".npz", ".h5", ".hdf5"):
    def _has_h5_dataset(handle, candidate):
        if not candidate:
            return False
        import h5py
        current = handle
        parts = [p for p in str(candidate).strip("/").split("/") if p]
        if not parts:
            return False
        for part in parts:
            if part in (".", "..") or not isinstance(current, h5py.Group):
                return False
            link = current.get(part, getlink=True)
            if not isinstance(link, h5py.HardLink):
                return False
            current = current[part]
        return isinstance(current, h5py.Dataset)

    def _key_exists(existing_keys, candidate):
        if existing_keys is None or not candidate:
            return False
        if isinstance(existing_keys, (set, list, tuple, dict)):
            return candidate in existing_keys
        return _has_h5_dataset(existing_keys, candidate)

    def _parse_key(raw_key, existing_keys=None):
        if not raw_key:
            return None, None, False
        text = str(raw_key).strip()
        if _key_exists(existing_keys, text):
            return text, None, False
        escaped = text.replace("@@", "\x00")
        wf = False
        ch = None
        if escaped.endswith("@waveform"):
            wf = True
            escaped = escaped[:-len("@waveform")]
        candidate_after_wf = escaped.replace("\x00", "@")
        if wf and _key_exists(existing_keys, candidate_after_wf):
            return candidate_after_wf, None, True
        if "@channel:" in escaped:
            base, _, ch_str = escaped.rpartition("@channel:")
            if ch_str.isdigit():
                ch = int(ch_str)
                escaped = base
        base_key = escaped.replace("\x00", "@")
        return (base_key if base_key else None), ch, wf

    def _extract_wf(matrix, prefix="wf"):
        data = np.asarray(matrix, dtype=np.float64)
        n_rows, n_samples = data.shape
        mean = np.mean(data, axis=1)
        std = np.std(data, axis=1, ddof=0)
        minimum = np.min(data, axis=1)
        maximum = np.max(data, axis=1)
        ptp = maximum - minimum
        sq = np.square(data)
        energy = np.sum(sq, axis=1)
        rms = np.sqrt(energy / n_samples)
        abs_data = np.abs(data)
        abs_mean = np.mean(abs_data, axis=1)
        median = np.median(data, axis=1)
        q25, q75 = np.percentile(data, [25.0, 75.0], axis=1)
        iqr = q75 - q25
        if n_samples >= 2:
            signs = np.sign(data)
            filled_signs = np.take_along_axis(
                signs,
                np.maximum.accumulate(np.where(signs != 0.0, np.arange(n_samples), 0), axis=1),
                axis=1,
            )
            zero_crossings = np.sum(filled_signs[:, 1:] * filled_signs[:, :-1] < 0.0, axis=1).astype(np.float64)
        else:
            zero_crossings = np.zeros(n_rows, dtype=np.float64)
        peak_abs = np.max(abs_data, axis=1)
        safe_rms = np.where(rms > 0.0, rms, 1.0)
        crest_factor = np.where(rms > 0.0, peak_abs / safe_rms, 0.0)
        peak_index = np.argmax(abs_data, axis=1).astype(np.float64)
        centered = data - mean[:, None]
        spectrum = np.abs(np.fft.rfft(centered, axis=1))
        bins = np.arange(spectrum.shape[1], dtype=np.float64)
        spec_sum = np.sum(spectrum, axis=1)
        safe_spec_sum = np.where(spec_sum > 0.0, spec_sum, 1.0)
        spectral_centroid = np.where(spec_sum > 0.0, np.sum(spectrum * bins[None, :], axis=1) / safe_spec_sum, 0.0)
        if spectrum.shape[1] > 1:
            ac_mag = spectrum[:, 1:]
            ac_max = np.max(ac_mag, axis=1)
            dominant_bin = np.where(ac_max > 0.0, (np.argmax(ac_mag, axis=1) + 1).astype(np.float64), 0.0)
        else:
            dominant_bin = np.zeros(n_rows, dtype=np.float64)
        return pd.DataFrame({
            f"{prefix}_mean": mean, f"{prefix}_std": std, f"{prefix}_min": minimum,
            f"{prefix}_max": maximum, f"{prefix}_ptp": ptp, f"{prefix}_rms": rms,
            f"{prefix}_energy": energy, f"{prefix}_abs_mean": abs_mean,
            f"{prefix}_median": median, f"{prefix}_iqr": iqr,
            f"{prefix}_zero_crossings": zero_crossings, f"{prefix}_crest_factor": crest_factor,
            f"{prefix}_peak_index": peak_index, f"{prefix}_spectral_centroid": spectral_centroid,
            f"{prefix}_dominant_bin": dominant_bin,
        })

    if suffix == ".npy":
        base_key, ch_idx, wf_mode = _parse_key(key)
        raw_arr = np.load(path, mmap_mode="r", allow_pickle=False)[start:start + count]
    elif suffix == ".npz":
        with np.load(path, allow_pickle=False) as archive:
            base_key, ch_idx, wf_mode = _parse_key(key, set(archive.files))
            resolved_key = base_key or (next(iter(archive.files)) if len(archive.files) == 1 else None)
            raw_arr = archive[resolved_key][start:start + count]
    else:
        import h5py
        with h5py.File(path, "r") as handle:
            base_key, ch_idx, wf_mode = _parse_key(key, handle)
            current = handle
            for part in (base_key or "").strip("/").split("/"):
                if part in ("", ".", "..") or not isinstance(current, h5py.Group):
                    raise RuntimeError("Invalid HDF5 dataset key.")
                link = current.get(part, getlink=True)
                if not isinstance(link, h5py.HardLink):
                    raise RuntimeError("HDF5 external and symbolic links are not followed.")
                current = current[part]
            if not isinstance(current, h5py.Dataset) or current.is_virtual or current.external:
                raise RuntimeError("Invalid or virtual/external HDF5 dataset.")
            if len(current.shape) == 3 and ch_idx is not None:
                raw_arr = current[start:start + count, ch_idx, :]
            else:
                raw_arr = current[start:start + count]
    raw_arr = np.asarray(raw_arr)
    if ch_idx is not None or wf_mode:
        if raw_arr.ndim == 3:
            if ch_idx is not None:
                sliced_2d = raw_arr[:, ch_idx, :]
                frame = _extract_wf(sliced_2d, f"ch{ch_idx}") if wf_mode else pd.DataFrame(sliced_2d, columns=[f"ch{ch_idx}_sample_{i}" for i in range(sliced_2d.shape[1])])
            else:
                frame = pd.concat([_extract_wf(raw_arr[:, c, :], f"ch{c}") for c in range(raw_arr.shape[1])], axis=1)
        elif ch_idx is not None and raw_arr.ndim == 2:
            frame = _extract_wf(raw_arr, f"ch{ch_idx}") if wf_mode else pd.DataFrame(raw_arr, columns=[f"ch{ch_idx}_sample_{i}" for i in range(raw_arr.shape[1])])
        else:
            if raw_arr.ndim == 1:
                raw_arr = raw_arr.reshape(-1, 1)
            frame = _extract_wf(raw_arr, "wf")
    else:
        if raw_arr.ndim == 1:
            raw_arr = raw_arr.reshape(-1, 1)
        frame = pd.DataFrame(raw_arr, columns=[f"feature_{i}" for i in range(raw_arr.shape[1])])
elif suffix == ".parquet":
    import pyarrow.parquet as pq
    parts, scanned = [], 0
    for batch in pq.ParquetFile(path).iter_batches(batch_size=min(count, 1000)):
        end = scanned + batch.num_rows
        if end > start:
            parts.append(batch.slice(max(0, start-scanned), max(0, min(end,start+count)-max(start,scanned))).to_pandas())
        scanned = end
        if scanned >= start+count:
            break
    frame = pd.concat(parts, ignore_index=True)
else:
    frame = pd.read_excel(path, sheet_name=key or 0, skiprows=range(1, start+1), nrows=count)
frame.columns = [str(c) for c in frame.columns]
x = frame[CONFIG["features"]].copy()
numeric = [c["name"] for c in CONFIG["featureSchema"] if c["type"] == "numeric"]
categorical = [c["name"] for c in CONFIG["featureSchema"] if c["type"] != "numeric"]
for column in numeric:
    x[column] = x[column].astype(float).replace([np.inf, -np.inf], np.nan)
for column in categorical:
    x[column] = x[column].map(lambda value: str(value) if pd.notna(value) else np.nan).astype(object)
numeric_steps = [("imputer", SimpleImputer(strategy="median", keep_empty_features=True))]
if CONFIG["scaling"] != "none":
    numeric_steps.append(("scaler", {"standard": StandardScaler, "robust": RobustScaler, "minmax": MinMaxScaler}[CONFIG["scaling"]]()))
transformers = []
if numeric:
    transformers.append(("numeric", Pipeline(numeric_steps), numeric))
if categorical:
    transformers.append(("categorical", Pipeline([("imputer", SimpleImputer(strategy="most_frequent", keep_empty_features=True)),
        ("encoder", OneHotEncoder(handle_unknown="ignore", sparse_output=False, max_categories=128))]), categorical))
regression = CONFIG["task"] == "regression"
registry = {"linear": linear_model.LinearRegression, "ridge": linear_model.Ridge, "logistic": linear_model.LogisticRegression,
    "decision_tree": tree.DecisionTreeRegressor if regression else tree.DecisionTreeClassifier,
    "random_forest": ensemble.RandomForestRegressor if regression else ensemble.RandomForestClassifier,
    "extra_trees": ensemble.ExtraTreesRegressor if regression else ensemble.ExtraTreesClassifier,
    "gradient_boosting": ensemble.GradientBoostingRegressor if regression else ensemble.GradientBoostingClassifier,
    "svr": svm.SVR, "svc": svm.SVC, "knn": neighbors.KNeighborsRegressor if regression else neighbors.KNeighborsClassifier,
    "kmeans": cluster.KMeans, "gaussian_mixture": mixture.GaussianMixture, "dbscan": cluster.DBSCAN,
    "pca": decomposition.PCA, "isolation_forest": ensemble.IsolationForest, "local_outlier_factor": neighbors.LocalOutlierFactor}
pipeline = Pipeline([("preprocessing", ColumnTransformer(transformers, remainder="drop", verbose_feature_names_out=True)),
    ("model", registry[CONFIG["algorithm"]](**CONFIG["hyperparameters"]))])
train = np.array(CONFIG["split"]["trainRows"]) - start
test = np.array(CONFIG["split"]["testRows"], dtype=int) - start
y = None if CONFIG["target"] is None else frame[CONFIG["target"]].to_numpy()
if CONFIG["task"] == "classification":
    y = frame[CONFIG["target"]].map(str).to_numpy()
folds = CONFIG["parameters"].get("folds", 0)
with threadpool_limits(limits=CONFIG["compute"]["workers"]):
    if folds and y is not None:
        strategy = CONFIG["split"]["strategy"]
        groups = None
        if strategy == "group":
            cv = GroupKFold(n_splits=folds)
            groups = frame[CONFIG["split"]["groupColumn"]].iloc[train].to_numpy()
        elif strategy == "time":
            cv = TimeSeriesSplit(n_splits=folds)
        elif CONFIG["task"] == "classification":
            cv = StratifiedKFold(n_splits=folds, shuffle=True, random_state=CONFIG["split"]["seed"])
        else:
            cv = KFold(n_splits=folds, shuffle=True, random_state=CONFIG["split"]["seed"])
        scoring = {"rmse":"neg_root_mean_squared_error", "r2":"r2"} if regression else {"accuracy":"accuracy", "f1Macro":"f1_macro"}
        validation = cross_validate(pipeline, x.iloc[train], y[train], cv=cv, groups=groups, scoring=scoring, n_jobs=1, error_score="raise")
        print(json.dumps({"validation": {key: (validation["test_"+key] * (-1 if key == "rmse" else 1)).tolist() for key in scoring}}))
    pipeline.fit(x.iloc[train], y[train] if y is not None else None)
if y is not None:
    predicted = pipeline.predict(x.iloc[test])
    if regression:
        print(json.dumps({"mae": metrics.mean_absolute_error(y[test], predicted),
            "rmse": float(np.sqrt(metrics.mean_squared_error(y[test], predicted))), "r2": metrics.r2_score(y[test], predicted)}))
    else:
        print(json.dumps({"accuracy": metrics.accuracy_score(y[test], predicted),
            "f1Macro": metrics.f1_score(y[test], predicted, average="macro", zero_division=0)}))
elif CONFIG["task"] == "reduction":
    predicted = pipeline.transform(x)
    print(json.dumps({"explainedVarianceRatio": pipeline.named_steps["model"].explained_variance_ratio_.tolist()}))
else:
    model = pipeline.named_steps["model"]
    if hasattr(model, "labels_"):
        predicted = model.labels_
    elif CONFIG["algorithm"] == "local_outlier_factor":
        predicted = np.where(model.negative_outlier_factor_ < model.offset_, -1, 1)
    else:
        predicted = pipeline.predict(x)
    print(json.dumps({"labels": {str(k): int(v) for k,v in zip(*np.unique(predicted, return_counts=True))}}))
pd.DataFrame(np.asarray(predicted)).to_csv("reproduced_predictions.csv", index=False)
'''
