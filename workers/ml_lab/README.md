# Mu Machine Learning Lab worker

This directory is the optional local computation backend of the existing WPF Mu
application. It is not a separate user-facing application. Mu starts one owned
Python worker per request. No dataset is uploaded and no generated Python is run.

## Runtime and protocol

Use the pinned runtime produced by the repository ML setup script. `worker.py`
imports only Python's standard library until it reads a complete request. The
desktop attaches its memory-limited Windows job before sending that request.
The host launches `python.exe -I -u <absolute worker.py>`, writes one UTF-8 JSON
line, closes stdin, reads one JSON reply and requires process exit. Requests are
limited to 128 KiB, nesting to 16 levels, and responses to 2 MiB. Duplicate,
unknown, malformed, extra and nonfinite inputs are rejected. Cancellation means
terminating the owned process; estimator pause/resume is unavailable.

Request:

```json
{"protocol":1,"id":"550e8400-e29b-41d4-a716-446655440000","action":"inspect","projectDir":"C:\\Research\\MuLab","parameters":{"path":"C:\\Data\\events.csv","start":0,"rowLimit":10000},"limits":{"maxRows":10000,"memoryMiB":1024,"threads":2}}
```

Replies contain `protocol`, the same `id`, and either `ok:true,result:{...}` or
`ok:false,error:{code,message}`. They contain no stack trace. `operations.jsonl`
records only IDs, operation/state/timestamp and fixed error codes. It is bounded
to 256 KiB and resets when full; training metadata is separate and retained.
Supported actions are `capabilities`, `inspect`, `browse`, `train`, `list`,
`predict`, `export`, `describe` and `compare`. `describe` returns the full saved
configuration, learned preprocessing and exact split rows. `compare` accepts
two to five completed experiments and refuses different source fingerprints,
row selections, feature schemas, targets, tasks, exact splits or CV strategies.
It reports metrics/runtime/size and caveats without automatically selecting a
winner. C# typed commands are the authority for UI and assistant
actions. Raw model text has no execution capability here.

## Implemented computation

- Bounded CSV/TSV, JSON array, JSONL, NPY, NPZ and HDF5 readers; optional Parquet
  (`pyarrow`) and XLSX (`openpyxl`). The source is referenced, never rewritten.
- CSV uses bounded physical lines, logical records, cells, columns and retained
  bytes before pandas conversion. JSON full parsing is capped at 16 MiB. JSONL
  is streamed. NumPy refuses pickles, NPY uses read-only memory mapping, and NPZ
  checks total uncompressed size before opening arrays.
- HDF5 browser lists at most 256 groups/datasets over 12 levels. Attribute names,
  dtype and shape are inspected without loading their potentially huge values.
  External/soft links, virtual/external storage, object arrays and arrays with
  more than two dimensions are refused. An explicit key, start and row count
  select a bounded slice. Tensor/channel selection is a future extension.
- Numeric/categorical/boolean inspection, missing/unique counts, constants,
  identifier heuristics, high cardinality, duplicate counts, summary statistics,
  IQR outlier counts, selected-row histograms and Pearson correlation plots.
  Diagnostics concern the selected leading row range, not the entire source or
  a random representative sample. Suspicious features and anomalies are retained.
- Regression: linear, Ridge, decision tree, random forest, extra trees, gradient
  boosting, SVR, KNN. Classification: logistic, decision tree, random forest,
  extra trees, gradient boosting, SVC, KNN. Clustering: k-Means, Gaussian mixture,
  DBSCAN. Reduction: PCA. Anomalies: Isolation Forest and novelty-mode LOF.
- Train-only `Pipeline`/`ColumnTransformer`: numeric median imputation,
  categorical most-frequent imputation, bounded one-hot encoding, and explicit
  standard/robust/min-max/no scaling. There is no arbitrary formula evaluator.
- Random, stratified, grouped and ordered time holdouts; optional 2–10 fold CV
  on the training partition only. Time CV currently requires unique training
  timestamps. Missing/nonfinite targets are excluded with exact recorded source
  indices; all held-out/training source indices are retained. Split group/time
  columns must be explicitly removed from predictive features.
- Task-specific held-out metrics, clearly named macro/weighted classification
  metrics, confusion matrix, residual/prediction plots, coefficients/impurity
  importance, PCA loadings/variance, cluster/noise counts, sampled clustering
  metrics and retained anomaly ranks. No holdout-based automatic model selection.
- Immutable experiment directories, saved configuration and library/hardware
  versions, prediction CSV, local model registry/reload, strict feature-name,
  order and type checks. Prediction previews stop at 100 rows; exported CSV
  contains all admitted selected rows. DBSCAN assignments are saved but DBSCAN
  has no out-of-sample prediction API.
- Standalone `reproduce.py` exports reload the referenced data, verify its
  fingerprint/library versions, recreate preprocessing, exact split indices,
  CV, model fit and metrics without importing Mu. No notebook export yet.

## Resource and persistence boundaries

Defaults are 10,000 selected rows, 1 GiB process job cap and two compute threads.
The row ceiling is 100,000, column ceiling 128 and transformed feature estimate
512. Kernel/density/nearest-neighbor algorithms have a 10,000 training-row cap.
Nearest-neighbor estimators use brute distances to avoid serialized native tree
objects. Full memory admission is conservative, not a guarantee of parser
allocation behavior; the separate owned job provides the final process ceiling.
GPU computation and resource telemetry are future extensions; this backend is
CPU only and records that fact.

Source identity uses full SHA256 for files up to 16 MiB. Larger files use first,
middle and last MiB plus size and modification time, explicitly labeled
`sha256-sampled-size-mtime`. That is not a complete content proof. Identity is
rechecked after reading and after training. A changed source prevents a completed
model registration. The original source is never copied into the project.

Saved models are `.skops`, not pickle/joblib imports. Loading is available only
through a UUID experiment in the selected local project. The model must match
its recorded SHA256, be no larger than 64 MiB, remain within archive expansion
limits and match exact NumPy/SciPy/scikit-learn/skops versions. Symlinks,
junctions, UNC/device paths and alternate data streams are refused.

The fixed serialization type allowance includes `numpy.dtype` and the exact
pinned sklearn gradient-boosting loss/link value objects listed in
`SAFE_SERIALIZATION_TYPES`. Their installed source and synthetic roundtrips were
inspected. It does not use namespace wildcards or automatically trust whatever
`get_untrusted_types()` discovers. Unknown types fail closed. Arbitrary external
serialized models cannot be imported in this increment.

## Extending and testing

- **Loader:** add its bounded admission and slice implementation to `data.py`,
  add its standalone export reader, and test corruption, large shape, source
  mutation and nonzero selection offsets.
- **Model/backend:** register supported tasks, exact defaults and a closed
  hyperparameter set in `models.py`; add explicit resource limits, fit/predict
  behavior, persistence roundtrip and standalone export. Installed optional
  packages alone do not enable model adapters.
- **Metric:** add only to the applicable task evaluator in `engine.py`; document
  averaging, positive class and evaluation scope, and independently calculate a
  small deterministic oracle.
- **Plot:** use an Agg figure, bounded points/columns, an owned project PNG path,
  and close the figure in `finally`. Never evaluate plotting code from a model.
- **Action:** add a closed worker parameter schema plus the corresponding typed
  C# command, validation and UI proposal. Do not add textual `if prompt contains`
  commands inside this engine.
- **Scientific ML:** extend the registry with task/result metadata for uncertainty,
  units and assumptions. Gaussian processes, symbolic regression, waveform
  extraction, PINNs and deep learning are future capabilities, not implemented
  by this foundation.

Run author tests with `artifacts/ml-runtime/Scripts/python.exe
workers/ml_lab/test_author.py`; independent tests live in `tests/ml_lab`.
The author suite trains every advertised algorithm/task pair, safely reloads its
model, predicts where supported and checks private worker protocol and group/time
splits. Separate reviewer tests cover leakage, corrupted/untrusted inputs,
provenance, source schema mismatch and export. Actual WPF responsiveness and
owned-job cancellation are verified through the desktop integration suite.

This is a working bounded foundation, not completion of every requested Phase 1
item. Rich plot coloring/3D, full HDF5 dimension selectors, editable feature
engineering, project lifecycle editing, model delete/rename/export UX, detailed
learning explanations, automatic multi-model search and advanced optional
backends remain separate increments.
