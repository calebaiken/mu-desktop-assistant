# Mu Machine Learning Lab

The Lab is a native Windows workspace for local dataset inspection, bounded CPU
experiments, saved models and prediction. Open **Machine Learning Lab** from Mu's
More menu, tray menu or Chat. It is separate from conversation storage: using the
Lab can save dataset references, experiments, models, plots and predictions even
when Chat is temporary. Source datasets stay in their original location.

This guide describes the implemented, tested bounded foundation, not completion of
all requested ML Phase 1 or a release. The final native receipt passes 57 assertions;
two live local planning prompts pass as a narrow fixture. See the current ML review
and [Testing](../TESTING.md) for exact evidence and remaining qualification.

## Setup

The optional runtime uses a local Python 3.12 or 3.13 interpreter and `uv`:

```powershell
.\scripts\setup-ml-lab.ps1 -Python 'C:\path\to\python.exe'
.\scripts\run-local-chat.ps1
```

Setup installs the hash-locked packages into `artifacts/ml-runtime`; downloading
those packages needs a network connection. Dataset operations and training then
run locally. No cloud account or API key is used. The setup script requires at
least 2 GiB free disk space. The existing desktop build and optional local language
model setup are separate prerequisites for `run-local-chat.ps1`.

The Lab title area shows the current project directory. In a normal profile its
location is `ml-lab/default-project` below Mu's data directory. Moving or deleting
source files can prevent later prediction or reproduction; saved references are
not source-data backups.

## Dataset to first experiment

1. On **Data**, choose a local file. CSV, TSV, JSON/JSONL, NumPy and HDF5 readers
   are included. Parquet and Excel depend on optional readers; a filename alone
   does not establish that its reader is installed. Unsupported input produces
   a local error rather than a substitute dataset.
2. For NPZ or HDF5, choose **Browse structure**, then select an array/dataset key.
   Set a starting row and maximum rows. This is a contiguous range, not a random
   or representative sample.
3. Select **Inspect dataset**. **Inspect** shows the selected scope, warnings,
   bounded preview, column statistics and metadata. Inspection reads at most
   1,000 selected rows; the UI preview shows at most 100. Training uses the
   configured range, which can be larger than the inspection range. Review the
   displayed selected-row count before interpreting statistics.
4. On **Configure / train**, choose the task and an available algorithm. Select
   input features explicitly. Suggested features omit detected constant,
   near-constant and possible identifier columns, but these heuristics do not
   establish which variables are scientifically appropriate.
5. For regression or classification, select the target. Mu excludes it from
   the input features. Select scaling, test fraction, seed and split. Group or
   time splits require the corresponding column. CV is off at 0; supervised
   experiments can request 2–10 folds. Consider whether subjects, batches or
   chronological ordering require a non-random split.
6. Name the experiment. Expand **Compute limits and advanced hyperparameters**
   to change memory, CPU thread limits or supported estimator parameters. The
   JSON object accepts a closed set of parameters; it cannot contain runnable
   Python. The default `{}` uses the model's recorded defaults.
7. Select **Train and save experiment**. **Jobs** displays measured elapsed time,
   process memory and available CPU information. A job has no invented percent
   complete. Select an active job and **Cancel current request** to stop it.

The worker fits preprocessing on training rows, then evaluates the configured
split. Review target choice, split assumptions and output warnings yourself;
metrics do not prove causality or validate a scientific conclusion. Repeatedly
choosing models using a held-out test score also changes what that score means.

## Results, models and prediction

**Results** presents metrics and the reproducibility record. **Plots** displays
local PNG artifacts from the current inspection or selected experiment. In
**History / export**, select an experiment and open its recorded results or
export its reproducibility bundle; the exported path appears below the buttons.
Export does not execute the generated reproduction script.

**Open selected results** fetches the full historical configuration, fitted
preprocessing and exact split rows; the compact history row is only a summary.
Control-click 2–5 experiments and choose **Compare selected** to compare their
metrics, runtime and model size. Comparison requires matching source/selection,
features, target, task, exact partitions and CV strategy. Different cohorts are
rejected rather than presented as equivalent experiments. No winner is selected
automatically.

In **Models / predict**, select a saved model and choose a prediction dataset,
array key and row range. The worker checks the saved feature schema and library
compatibility. Prediction writes a local output artifact and shows its path plus
a bounded preview. A DBSCAN clustering result has no general out-of-sample
predict method; do not interpret an unavailable operation as a missing prediction.
Changing library versions can require retraining rather than loading an old model.

**Refresh saved work** reloads saved experiment/model listings. Closing the Lab
window keeps App-owned work running. Reopening it restores current job status;
refresh saved work after completion to load its records. Exiting Mu stops owned
workers. Cancellation is not pause/resume and does not create a completed result.

## Ask Mu

**Ask Mu** uses the local language model to propose one typed action. Learn,
Research and Auto change the explanation guidance; Auto currently proposes one
baseline, not a background search or a multi-experiment agent.

Choose the dataset in **Data** first. File actions use a captured current-selection
alias; Mu cannot invent another source path. Choose a different source there before
asking for an action on another file. Describe the request, select **Propose a plan**,
then inspect the complete action:
file path, selected columns, target, split, algorithm and compute limits. Nothing
runs merely because the model answered. **Run reviewed plan** runs that exact
proposal once. The action and its limits are captured when the proposal is shown;
later edits to other controls do not silently change that reviewed action. A
new plan is needed for another execution. The model can be wrong, and an invalid
or incomplete proposal does not run. All manual Lab controls remain usable when
local conversational planning is unavailable.

## Current bounds

- The UI accepts row limits of 10–100,000. A smaller source may contain fewer
  rows. Explicit one-row selection in a larger file is not yet exposed by the UI.
- Dataset tables have at most 128 columns. Preview rows and displayed strings are
  bounded; full artifacts can contain more than the UI preview.
- The Lab runs one CPU worker at a time with cancellation and explicit process
  limits. It does not currently provide GPU training or pause/resume.
- The backend lists at most 200 saved entries and surfaces a listing warning at
  that bound. The UI also has a defensive 500-record ceiling and shows at most
  100 session job rows. These are view bounds, not evidence that omitted work was deleted.
- Advanced search, SHAP/UMAP adapters, scientific custom models, waveform feature
  extraction and deep-learning workflows remain later work. Optional package
  detection does not enable those adapters.
- Model persistence and project artifact validation reduce accidental unsafe
  loading; they do not make arbitrary third-party models trusted. Use models
  created by this Lab and retain the recorded environment.

The final validated development executable is
`artifacts/qa/ml-candidate/Companion.Desktop.exe`. An older running Mu locked its
normal build output. Close that instance first, then launch the candidate with
`--ml-root <absolute-repository-path>` and
`--local-model-dir <absolute-repository-path>\artifacts\local-ai`. Rebuilding the normal
location after closing Mu is also possible. Reopening an old instance is not running
the newly validated binary.
