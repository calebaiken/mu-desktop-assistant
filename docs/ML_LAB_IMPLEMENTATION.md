# Mu Machine Learning Lab implementation record

2026-09-29. The current repository was inspected before ML changes: complete source,
test, script and documentation inventory, followed by parallel reviews of all layers.
Baseline is `22f3dae`. Existing uncommitted Phase 4B capture/recognition work is preserved;
its incomplete Desktop partial is excluded from compilation until that separate work is
qualified. Phase 4A speech output remains active. No microphone is needed for ML.

## Repository findings

Mu is C#/.NET 10 WPF, six projects with enforced dependency directions: Core values,
Application ports, Infrastructure adapters, Desktop composition, MSTest and architecture
checks. Windows Forms supplies the tray; user32/kernel32 handle placement and workers.
App owns windows and shutdown. The compact companion must retain its existing size.
IChatModel supplies bounded text streams through a local llama.cpp/Qwen worker. It does
not have native tool calls. Temporary conversations use a separate in-memory store;
saved conversations use a versioned SQLite store with search, immutable branches and
interrupted-turn recovery. Long-term memory and general tool execution remain proposed.
Markdig/WpfMath produce bounded inert native rich text. Windows installed voices run in
an owned process for manual Read aloud. Microphone work remains unqualified WIP.
Settings/placement use validated atomic JSON files; profile logging is content-free.
Existing workers use asynchronous I/O, cancellation and Windows job ownership. Resource
sampling currently measures the desktop process only. No ML Python runtime existed in
the runnable graph. NuGet dependencies are pinned/locked; strict Release build and native
smoke scripts are the current development gates. Packaging/release qualification is
unfinished, including historical physical multi-monitor and CPU-budget gates.

## Implementation sequence and boundaries

1. Add package-free Application ML action records and a closed dispatcher contract.
2. Add an optional project-local Python environment and an internal one-request worker.
   It uses NumPy/pandas/scikit-learn/matplotlib/h5py/skops; no cloud, notebook server or
   external service. Mu remains usable when this optional environment is absent.
3. Dataset inspection uses bounded selections with explicit sampling labels. Import
   stores references/fingerprints, not a duplicate of the source. HDF5 traversal and
   NumPy loading must reject unsafe external/object data. Training records selections.
4. Train-only sklearn pipelines, explicit seed/split/features/settings, appropriate
   metrics, local plots, atomic experiment metadata and restricted model persistence.
5. A native Lab window uses the same SystemColors/control language, opened from More
   or Chat. One queued job runs at a time with cancellation, elapsed time and resource
   limits. No fabricated progress percentage. Closing Mu cancels owned workers.
6. Local conversational planning creates validated typed proposals, shown before
   execution. No generated code or arbitrary shell execution. Dataset contents are
   never sent outside this PC; temporary chat does not silently create an ML project.
7. Independent leakage, numerical, schema, persistence, resource and end-to-end checks,
   native reachability/responsiveness checks, then documentation/status updates.

The first unit is an integrated dataset-to-experiment-to-prediction foundation. Full
ML Phase 1 is not complete until all accepted workflows and review gates pass. Advanced
search/SHAP/UMAP, scientific models, waveform features and deep learning remain later
units; unavailable capabilities must be reported accurately rather than shown as working.
The original assistant phase numbering and the new ML phase numbering are separate.

## Protocol

One UTF-8 JSON line in/out, protocol 1; parent assigns its Windows job before sending
input. Request fields: id, action, projectDir, parameters, limits. Actions are a closed
registry, not Python code. Response: protocol, id, ok, result or fixed error code/message.
Raw records do not enter diagnostic logs. Project artifacts are separate from chat.
Workers finish after one operation; cancellation terminates the owned job. Start with
one concurrent CPU job; fitted estimators and native math thread counts are bounded.

## Sources informing implementation

- https://scikit-learn.org/stable/modules/compose.html (train-only pipelines)
- https://scikit-learn.org/stable/model_persistence.html (serialization trust/version limits)
- https://docs.h5py.org/en/stable/high/dataset.html (partial dataset selections)

Executable evidence and remaining limitations belong in the ML QA report, not inferred
from this plan or from historical Mu test results.
