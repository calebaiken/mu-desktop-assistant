"""Closed sklearn model registry and train-only preprocessing construction."""
from __future__ import annotations

import numpy as np
import pandas as pd
from sklearn.compose import ColumnTransformer
from sklearn.pipeline import Pipeline
from sklearn.impute import SimpleImputer
from sklearn.preprocessing import StandardScaler, RobustScaler, MinMaxScaler, OneHotEncoder
from sklearn.linear_model import LinearRegression, Ridge, LogisticRegression
from sklearn.tree import DecisionTreeRegressor, DecisionTreeClassifier
from sklearn.ensemble import (RandomForestRegressor, RandomForestClassifier, ExtraTreesRegressor,
                              ExtraTreesClassifier, GradientBoostingRegressor, GradientBoostingClassifier,
                              IsolationForest)
from sklearn.svm import SVR, SVC
from sklearn.neighbors import KNeighborsRegressor, KNeighborsClassifier, LocalOutlierFactor
from sklearn.cluster import KMeans, DBSCAN
from sklearn.mixture import GaussianMixture
from sklearn.decomposition import PCA

from common import clean, fail

CATALOG = [
    {"id": "linear", "name": "Linear regression", "tasks": ["regression"], "description": "Interpretable additive linear baseline; inspect residuals and collinearity."},
    {"id": "ridge", "name": "Ridge regression", "tasks": ["regression"], "description": "Regularized linear baseline useful with correlated features."},
    {"id": "logistic", "name": "Logistic regression", "tasks": ["classification"], "description": "Regularized linear class baseline; scaling is usually useful."},
    {"id": "decision_tree", "name": "Decision tree", "tasks": ["regression", "classification"], "description": "Inspect a nonlinear baseline; depth limits reduce overfitting."},
    {"id": "random_forest", "name": "Random forest", "tasks": ["regression", "classification"], "description": "Nonlinear ensemble baseline with impurity importance; importance is not causality."},
    {"id": "extra_trees", "name": "Extra trees", "tasks": ["regression", "classification"], "description": "Randomized tree ensemble for a nonlinear comparison."},
    {"id": "gradient_boosting", "name": "Gradient boosting", "tasks": ["regression", "classification"], "description": "Sequential tree ensemble; compare validation performance and runtime."},
    {"id": "svr", "name": "Support vector regression", "tasks": ["regression"], "description": "Kernel regression for small datasets; scaling and sample size matter."},
    {"id": "svc", "name": "Support vector classifier", "tasks": ["classification"], "description": "Kernel classifier for small datasets; scaling and sample size matter."},
    {"id": "knn", "name": "K-nearest neighbors", "tasks": ["regression", "classification"], "description": "Distance-based baseline; feature scale and dimensionality matter."},
    {"id": "kmeans", "name": "k-Means", "tasks": ["clustering"], "description": "Compact distance-based clusters; chosen k does not establish physical populations."},
    {"id": "gaussian_mixture", "name": "Gaussian mixture", "tasks": ["clustering"], "description": "Probabilistic population approximation with covariance assumptions."},
    {"id": "dbscan", "name": "DBSCAN", "tasks": ["clustering"], "description": "Density clusters with preserved noise labels; sensitive to scale and eps."},
    {"id": "pca", "name": "Principal component analysis", "tasks": ["reduction"], "description": "Linear variance projection; component loadings depend on preprocessing."},
    {"id": "isolation_forest", "name": "Isolation forest", "tasks": ["anomaly"], "description": "Ranks unusual feature combinations; anomalies are retained, not deleted."},
    {"id": "local_outlier_factor", "name": "Local outlier factor", "tasks": ["anomaly"], "description": "Local density anomaly scores; novelty mode supports separate future records."},
]


def make_model(task, algorithm, seed, parameters, rows, resource):
    if not any(x["id"] == algorithm and task in x["tasks"] for x in CATALOG):
        fail("Choose an algorithm supported for the selected task.")
    if not isinstance(parameters, dict) or len(parameters) > 12:
        fail("Hyperparameters must be a small JSON object.")
    classes = {
        "linear": LinearRegression, "ridge": Ridge, "logistic": LogisticRegression,
        "decision_tree": DecisionTreeRegressor if task == "regression" else DecisionTreeClassifier,
        "random_forest": RandomForestRegressor if task == "regression" else RandomForestClassifier,
        "extra_trees": ExtraTreesRegressor if task == "regression" else ExtraTreesClassifier,
        "gradient_boosting": GradientBoostingRegressor if task == "regression" else GradientBoostingClassifier,
        "svr": SVR, "svc": SVC,
        "knn": KNeighborsRegressor if task == "regression" else KNeighborsClassifier,
        "kmeans": KMeans, "gaussian_mixture": GaussianMixture, "dbscan": DBSCAN, "pca": PCA,
        "isolation_forest": IsolationForest, "local_outlier_factor": LocalOutlierFactor,
    }
    defaults = {
        "ridge": {"alpha": 1.0}, "logistic": {"max_iter": 1000, "random_state": seed},
        "decision_tree": {"max_depth": 8, "random_state": seed},
        "random_forest": {"n_estimators": 100, "max_depth": 12, "random_state": seed, "n_jobs": 1},
        "extra_trees": {"n_estimators": 100, "max_depth": 12, "random_state": seed, "n_jobs": 1},
        "gradient_boosting": {"n_estimators": 100, "max_depth": 3, "random_state": seed},
        "svr": {"cache_size": min(128, resource["memory_mb"] // 8)},
        "svc": {"cache_size": min(128, resource["memory_mb"] // 8), "random_state": seed},
        "knn": {"n_neighbors": min(5, rows), "n_jobs": 1, "algorithm": "brute"},
        "kmeans": {"n_clusters": 3, "n_init": 10, "random_state": seed},
        "gaussian_mixture": {"n_components": 3, "random_state": seed},
        "dbscan": {"eps": .5, "min_samples": 5, "n_jobs": 1},
        "pca": {"n_components": 2, "svd_solver": "full"},
        "isolation_forest": {"n_estimators": 100, "contamination": "auto", "random_state": seed, "n_jobs": 1},
        "local_outlier_factor": {"n_neighbors": min(20, rows - 1), "novelty": True, "contamination": "auto", "n_jobs": 1, "algorithm": "brute"},
    }.get(algorithm, {})
    allowed = {
        "linear": {"fit_intercept"}, "ridge": {"alpha", "fit_intercept"},
        "logistic": {"C", "max_iter", "class_weight"},
        "decision_tree": {"max_depth", "min_samples_leaf", "min_samples_split"},
        "random_forest": {"n_estimators", "max_depth", "min_samples_leaf", "max_features"},
        "extra_trees": {"n_estimators", "max_depth", "min_samples_leaf", "max_features"},
        "gradient_boosting": {"n_estimators", "max_depth", "learning_rate", "min_samples_leaf"},
        "svr": {"C", "epsilon", "kernel", "gamma"}, "svc": {"C", "kernel", "gamma", "class_weight"},
        "knn": {"n_neighbors", "weights", "p"}, "kmeans": {"n_clusters", "n_init", "max_iter"},
        "gaussian_mixture": {"n_components", "covariance_type", "max_iter", "reg_covar"},
        "dbscan": {"eps", "min_samples"}, "pca": {"n_components", "whiten"},
        "isolation_forest": {"n_estimators", "contamination", "max_samples"},
        "local_outlier_factor": {"n_neighbors", "contamination"},
    }[algorithm]
    if set(parameters) - allowed:
        fail("An unsupported hyperparameter was supplied. Execution, threads and random seeds are controlled separately.")
    integer_bounds = {"n_estimators": (1, 500), "max_depth": (1, 32), "max_iter": (10, 2000),
                      "min_samples_leaf": (1, 1000), "min_samples_split": (2, 1000), "n_neighbors": (1, rows - 1),
                      "n_clusters": (2, min(50, rows - 1)), "n_components": (1, min(50, rows - 1)),
                      "min_samples": (2, min(1000, rows)), "n_init": (1, 50), "p": (1, 2)}
    enum_values = {"kernel": {"linear", "rbf", "poly", "sigmoid"}, "weights": {"uniform", "distance"},
                   "covariance_type": {"full", "tied", "diag", "spherical"}, "class_weight": {None, "balanced"}}
    for key, value in parameters.items():
        if key in integer_bounds:
            lo, hi = integer_bounds[key]
            if type(value) is not int or not lo <= value <= hi:
                fail(f"{key} must be an integer from {lo} to {hi}.")
        elif key in enum_values:
            if not isinstance(value, (str, type(None))) or value not in enum_values[key]:
                fail(f"Unsupported {key} value.")
        elif key in ("fit_intercept", "whiten"):
            if type(value) is not bool:
                fail(f"{key} must be boolean.")
        elif key == "contamination":
            if value != "auto" and (type(value) not in (int, float) or not 0 < value <= .5):
                fail("contamination must be auto or a number in (0, 0.5].")
        elif key == "gamma" and value in ("scale", "auto"):
            pass
        elif key == "max_features" and value in ("sqrt", "log2"):
            pass
        elif key == "max_samples" and value == "auto":
            pass
        elif type(value) not in (int, float) or not np.isfinite(value) or not 0 < value <= 1_000_000:
            fail(f"{key} must be a finite positive number within the supported range.")
    if algorithm in ("svr", "svc", "dbscan", "knn", "local_outlier_factor") and rows > 10000:
        fail("This algorithm is limited to 10,000 selected rows to bound quadratic work. Select a smaller range.", "resource_limit")
    return classes[algorithm](**(defaults | parameters))


def schema_for(frame):
    return [{"name": name, "type": "numeric" if pd.api.types.is_numeric_dtype(frame[name]) and not pd.api.types.is_bool_dtype(frame[name])
             else "boolean" if pd.api.types.is_bool_dtype(frame[name]) else "categorical"} for name in frame.columns]


def prepare_frame(frame, schema):
    result = frame.copy()
    for feature in schema:
        name = feature["name"]
        if feature["type"] == "numeric":
            result[name] = result[name].astype(float).replace([np.inf, -np.inf], np.nan)
        else:
            result[name] = result[name].map(lambda x: str(x) if pd.notna(x) else np.nan).astype(object)
    return result


def make_pipeline(frame, scaling, model, resource):
    if scaling not in ("standard", "robust", "minmax", "none"):
        fail("Scaling must be standard, robust, minmax or none.")
    schema = schema_for(frame)
    numeric = [x["name"] for x in schema if x["type"] == "numeric"]
    categorical = [x["name"] for x in schema if x["type"] != "numeric"]
    encoded = len(numeric) + sum(max(1, frame[c].nunique(dropna=True)) for c in categorical)
    if encoded > 512 or encoded * len(frame) * 8 * 12 > resource["memory_mb"] * 1024 * 1024:
        fail("Estimated one-hot expansion exceeds 512 features or the RAM budget. Remove high-cardinality features or select fewer rows.", "resource_limit")
    numeric_steps = [("imputer", SimpleImputer(strategy="median", keep_empty_features=True))]
    if scaling != "none":
        numeric_steps.append(("scaler", {"standard": StandardScaler, "robust": RobustScaler, "minmax": MinMaxScaler}[scaling]()))
    transforms = []
    if numeric:
        transforms.append(("numeric", Pipeline(numeric_steps), numeric))
    if categorical:
        transforms.append(("categorical", Pipeline([
            ("imputer", SimpleImputer(strategy="most_frequent", keep_empty_features=True)),
            ("encoder", OneHotEncoder(handle_unknown="ignore", sparse_output=False, max_categories=128)),
        ]), categorical))
    return Pipeline([("preprocessing", ColumnTransformer(transforms, remainder="drop", verbose_feature_names_out=True)), ("model", model)]), schema


def preprocessing_details(pipeline, scaling):
    transform = pipeline.named_steps["preprocessing"]
    result = {"version": 1, "numericImputation": "median", "categoricalImputation": "most_frequent",
              "categoricalEncoding": "one_hot_ignore_unknown", "scaling": scaling,
              "fitScope": "training rows only for supervised tasks; all selected rows for unsupervised tasks",
              "outputFeatures": transform.get_feature_names_out().tolist(), "fitted": {}}
    for name, transformer, columns in transform.transformers_:
        if name == "remainder":
            continue
        details = {"columns": list(columns), "imputerStatistics": clean(transformer.named_steps["imputer"].statistics_.tolist())}
        if "scaler" in transformer.named_steps:
            scaler = transformer.named_steps["scaler"]
            details["scaler"] = {key: clean(getattr(scaler, key).tolist()) for key in ("mean_", "scale_", "center_", "data_min_", "data_max_") if hasattr(scaler, key)}
        if "encoder" in transformer.named_steps:
            details["categories"] = [clean(x.tolist()) for x in transformer.named_steps["encoder"].categories_]
        result["fitted"][name] = details
    return result
