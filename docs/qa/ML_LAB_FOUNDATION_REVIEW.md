# Independent QA: Mu Machine Learning Lab foundation

Protocol frozen 2026-09-29 before implementation execution review. Reviewer: independent
ML review agent. Baseline: `22f3dae`; pre-existing unqualified Phase 4B voice work is
outside this increment. The implementation author cannot assign this review a pass.

## Scope and decision rule

This review covers an **integrated initial ML foundation increment**, not all requested
ML Phase 1 functionality or the original assistant's Phase 4. Intended working slice:
native Lab access; bounded local dataset inspection; explicit preprocessing, supervised
and unsupervised fitting; evaluation and plots; experiment/model persistence; schema-
checked inference; typed conversational proposals; owned, cancellable CPU jobs; and
reproducible export where supported. Capabilities advertised as available must work.
Advanced algorithms, scientific ML, deep learning and unsupported UI workflows remain
explicitly future work. A narrower successful increment cannot establish full Phase 1.

Each criterion below receives 0 (absent/contradicted), 1 (partial evidence), or 2
(satisfied by cited evidence). Ten equally weighted categories, five criteria each.
Overall = sum of all 50 criterion scores / 10. Strictly greater than 8.5 is required;
85/100 = 8.5 FAIL, 86/100 = 8.6 PASS only without a critical defect. No rounding up.
Missing execution evidence is not a passing test. Preserve failed runs and repair
history. Inspect every changed ML line, plus every changed integration line.

Critical blockers regardless of score: train/test leakage; arbitrary deserialization
or code execution through actions/data/models; unintended data upload; source-data
deletion/corruption; unbounded full materialization of large selected files; orphaned
workers after application exit; or claiming mocked/nonfunctional controls complete.

## Frozen criteria

| ID | Category | Criterion | Score / evidence |
|---|---|---|---|
| C1 | Code correctness | Every advertised dataset reader handles deterministic valid and malformed fixtures, reports the actual selection and preserves the source. | 1; PY46: base formats verified; optional Parquet/Excel unqualified |
| C2 | Code correctness | Every advertised train operation selects the requested task/model/features/settings; incompatible inputs fail clearly. | 2; PY46/PY19 every advertised algorithm/task |
| C3 | Code correctness | Evaluation, plots and prediction correspond to the trained model and selected data; no fabricated results. | 2; numerical oracles, native plots and prediction |
| C4 | Code correctness | Save/load/list/export operate on actual artifacts and correctly report committed, failed and cancelled outcomes. | 2; PY19/N57 actual persistence/describe/export |
| C5 | Code correctness | Typed action validation rejects unknown operations, fields, invalid/null values and ambiguous configurations before execution. | 2; NET353 action/alias closed-boundary tests |
| A1 | Architecture | Application contracts remain package-free, production dependencies stay in their owning layers, existing project boundaries pass. | 2; ARCH11 and strict build |
| A2 | Architecture | UI and conversational proposals use the same closed typed action registry without arbitrary generated code or shell commands. | 2; typed registry and STR7 constrained transport |
| A3 | Architecture | ML worker/service lifetime is owned by the application and independent of chat lifetime; shutdown drains/cancels safely. | 2; NET353 held-child concurrent disposal/ownership |
| A4 | Architecture | Data readers, model adapters, metrics and optional scientific/deep-learning backends have documented extension points grounded in working code. | 1; documented extension seams; advanced backends not exercised |
| A5 | Architecture | Temporary conversation isolation, saved-history branching and existing assistant behavior remain intact; experimental persistence is explicit. | 2; NET353 and BASE38 existing regression |
| M1 | Mathematical correctness | MAE/MSE/RMSE/R-squared and classification confusion/averaging are checked against independent hand-computable or separately computed oracles. | 2; PY46 independent hand-computed metrics |
| M2 | Mathematical correctness | Imputer/scaler/encoder/feature-selection fit uses training rows only; held-out extremes/unseen categories prove isolation. | 2; PY46 held-out extremes and unseen categories |
| M3 | Mathematical correctness | Seeds, partition membership, stratification/group/time semantics and CV boundaries are recorded and reproducible; test scores do not drive selection. | 2; PY19 exact rows, outer-train CV, group/time |
| M4 | Mathematical correctness | PCA/clustering/anomaly results use appropriate metrics, preserve noise/outliers and label statistical findings without causal/physical claims. | 2; PY46 preserved noise/anomalies and scientific caveats |
| M5 | Mathematical correctness | Resource/size calculations, row counts, runtime units, sampling labels and reproducibility comparisons are independently checked. | 2; PY46 provenance/bounds plus NET353 hard memory cap |
| T1 | Testing | Meaningful C# and Python tests execute with deterministic fixtures and nonempty retained results; strict build/static checks pass. | 2; PY46/PY19, NET353, strict build and ARCH11 |
| T2 | Testing | Regression and classification workflows execute from import through evaluation, persisted experiment and model reuse. | 2; N57 and PY19 persisted regression/classification reuse |
| T3 | Testing | Unsupervised detector and partial-HDF5 workflows execute with preserved source data/noise and honest selection labels. | 2; PY46 partial HDF5, PCA, clusters/noise |
| T4 | Testing | Missing dependencies, corrupt files, invalid parameters, schema mismatch, changed/missing sources and resource rejection execute as negative tests. | 2; PY46/PY19/NET353 negative fixtures |
| T5 | Testing | Cancellation, process exit, queue ordering, native UI responsiveness and affected existing Mu regression suites execute. | 1; cancel/queue/disposal/dispatcher/base regression pass; forced parent-death untested |
| R1 | Reliability | Running and queued jobs cancel deterministically; late callbacks cannot revive cancelled jobs or closed UI. | 2; N57 and NET353 held/queued cancellation/disposal |
| R2 | Reliability | Worker failure, deadline and output/protocol violation become actionable failures and permit subsequent safe operations. | 2; NET353 floods, malformed/failed worker, reuse |
| R3 | Reliability | Metadata/artifact writes are atomic and restart preserves valid completed records while identifying interrupted/incomplete operations. | 1; atomic source review and interrupted record fixture; no power-loss/restart fault injection |
| R4 | Reliability | Dataset fingerprint/version changes and incompatible library/model versions prevent silent stale reuse. | 2; PY19 changed source/hash and exact library mismatch |
| R5 | Reliability | UI remains usable with absent ML runtime, optional packages or unavailable hardware; existing assistant startup and shutdown still work. | 1; missing-runtime host check and native manual Lab; absent-runtime native UI not separately executed |
| S1 | Security | Data remains local by default; worker has no external API/cloud path and inherits no unnecessary credentials/environment. | 2; closed local host/environment review and logs fixture |
| S2 | Security | Paths, HDF5 external links, NumPy object arrays, ZIP expansion and artifact traversal are validated and bounded. | 2; PY46 NPZ/HDF5/NumPy/path plus native PNG checks |
| S3 | Security | Model deserialization rejects untrusted types/artifacts; manifest hashes alone are not considered proof of trusted origin. | 2; PY19 tamper, fixed type set and unknown-type refusal |
| S4 | Security | Closed protocol/action validation, byte/depth/output limits and inert previews prevent code/instruction execution from dataset/model text. | 2; PY46 framing/bounds, NET353/STR7 typed/transport checks |
| S5 | Security | Structured logs omit raw rows/prompts/private values; operations cannot delete source files or overwrite unrelated artifacts. | 2; PY46 log sentinel/source hashes and local artifact-only writes |
| P1 | Performance | Inspection caps rows, columns, bytes, attributes/nodes and previews; large files use partial/lazy reads or fail before materialization. | 2; PY46 partial/logical-large and bounded parser fixtures |
| P2 | Performance | Training estimates and enforces practical memory/encoded-feature limits; CPU threads/concurrency/time are bounded and disclosed. | 2; PY46 admission and NET353 enforced memory cap |
| P3 | Performance | Worker process memory/CPU are measured or explicitly unavailable; desktop-only measurements are never presented as job usage. | 2; actual held interpreter monitor; native measured job state |
| P4 | Performance | Native dispatcher stays responsive during import/train/cancel; plots use bounded sampling and no fabricated progress percentage. | 2; N57 dispatcher heartbeat and bounded plot review |
| P5 | Performance | Referenced datasets are not copied unnecessarily; artifact sizes/cache/storage needs are visible and cleanup never touches source data. | 1; source references/model sizes; cache/cleanup lifecycle UI remains future |
| N1 | Maintainability | Small cohesive modules, explicit contracts/ownership and readable validation avoid monolithic UI or action conditionals. | 1; layered UI/service modules; engine/export registry duplication remains |
| N2 | Maintainability | Python/runtime dependency pins, versions, provenance and optional capability detection are documented and reproducible. | 2; pinned/hash-locked optional runtime and versions |
| N3 | Maintainability | Developer instructions demonstrate adding reader/model/metric/plot/action/backend using actual extension points. | 2; worker README extension recipes grounded in source |
| N4 | Maintainability | Exported standalone reproduction runs outside Mu internal state and records environment, source/selection, pipeline, seed and metrics. | 2; PY19 standalone execution and multiline source selection |
| N5 | Maintainability | Status/help/changelog distinguish verified foundation from full Phase 1 and future functionality; retained evidence matches current files. | 2; current status/help clearly bound scope and retained failures |
| U1 | UX/UI | Lab is reachable from existing Mu, shares its native visual/accessibility language and preserves the compact hover-only companion. | 2; native entry points and BASE38 compact companion regression |
| U2 | UX/UI | Keyboard navigation, named controls, minimum-window sizing and scrolling make all working controls reachable without clipping. | 1; N57 minimum geometry; physical keyboard/screen-reader not qualified |
| U3 | UX/UI | Dataset selection, features, transforms, split, model settings, metrics, warnings and artifact identities are inspectable before/after runs. | 2; N57 exact historical preprocessing/rows/config/artifacts |
| U4 | UX/UI | Job status, elapsed time, cancel and failures are clear; selections/drafts survive failure and UI shows no empty working-feature buttons. | 2; N57 recoverable failure, status/cancel, preserved selections |
| U5 | UX/UI | Conversational proposals can be inspected/overridden and distinguish recommended, selected, trained and scientifically validated results. | 1; N57 and live2 reviewed proposals; broad conversational accuracy unqualified |
| Q1 | Requirement compliance | User acceptance A: regression import/inspection/missing values/train/metrics/residuals/comparison/save is exercised or exact gaps declared. | 2; N57/PY19 manual regression/compare/export; exact conversational gap disclosed |
| Q2 | Requirement compliance | User acceptance B: class balance, stratified split, classification metrics/confusion/importance/model save is exercised or exact gaps declared. | 2; N57/PY46 classification/confusion/coefficients/model save |
| Q3 | Requirement compliance | User acceptance C: feature selection, PCA, clustering, cluster/noise characterization and scientific caution are exercised or exact gaps declared. | 1; PCA/clusters/noise pass; population exploration/coloring remains limited |
| Q4 | Requirement compliance | User acceptance D/E: HDF5 structure/partial selection and saved-model schema-safe prediction are exercised or exact gaps declared. | 2; PY46 bounded HDF5 and N57 saved-model schema check; tensor gaps disclosed |
| Q5 | Requirement compliance | Learn/Research/Auto, projects/history/registry/compute controls and unsupported features are represented honestly; no full Phase 1 claim before all five acceptance scenarios pass. | 1; honest modes/default project; lifecycle/Auto/full Phase1 incomplete |

## Execution and review ledger

Final independent scoring is recorded below for the bounded foundation only.
Historical Mu Phase 4A results remain baseline context, not validation of ML additions.
The frozen criteria above have not changed. Initial protocol SHA256 was
`CCCB23B13279B5C94DF45F64093587BAC099B09896926822DBD9309739AF6B3D`.

### Executed evidence, 2026-09-29

- **PY46:** `artifacts/ml-runtime/Scripts/python.exe -B -m unittest discover -s
  tests/ml_lab -p 'test_independent_*.py' -v`, with OMP/OpenBLAS/MKL threads=2:
  **46/46 passed, 44.724 s, exit 0**. Receipt
  `artifacts/qa/ml-foundation-review/independent-final-run.txt`. The reviewer authored
  these fixtures independently. They check hand-derived regression/confusion metrics,
  held-out extremes and unseen categories, train-only CV, grouped/time splits,
  absolute selected-row provenance, source mutation, every advertised task/algorithm's
  safe persistence/reload, unknown serialized type rejection even after a valid hash,
  schema errors, actual standalone script execution (including multiline CSV), full
  historical records, and exact-cohort comparison rejection. Data fixtures cover the
  six base format families, 10-million-row logical HDF5 partial reads, external/soft/
  virtual HDF5 references, compressed NPZ expansion and huge declared NPY shape,
  object arrays, byte/row/column limits and source preservation. Isolated protocol
  subprocesses check unknown/duplicate fields, framing/depth/size/UTF-8/nonfinite
  inputs, exact protocol integer, missing dependencies and content-free logs.
- **PY19:** final affected engine rerun, same command with
  `-p 'test_independent_engine.py'`: **19/19 passed, 24.387 s, exit 0** in
  `artifacts/qa/ml-foundation-review/engine-final-negative-run.txt`. Adds two cases
  proving exact library mismatch is rejected before deserialization, interrupted
  saved metadata is visible without registering its model, traversal/invalid IDs
  fail and a deleted source yields `missing_source`. With the unchanged data,
  pipeline and protocol tests in PY46 this is **48 distinct current Python tests**;
  it is not a claim that all 48 ran in a single invocation.
- **CS345:** independently read `artifacts/qa/ml-foundation/ml-foundation-host-planner.trx`:
  **345 executed, 345 passed, zero failures or inconclusive results**. Includes strict
  typed-action/planner tests and real host malformed-output/flood/memory-cap/queue
  checks. The parent owns the build/test invocation and final affected reruns.
- **N48:** `artifacts/qa/ml-native-20260929-221100-4d96ffa9/ml-lab-smoke.json`:
  **48 assertions, 84.147 s, failure=null**. Actual WPF HWND, minimum 760x560-DIP tab
  and action geometry, real worker inspection/training/prediction, decoded plots,
  dispatcher heartbeat, schema failure recovery, classification CV, PCA, clustering,
  owned-worker/queued cancellation, reuse and view reopening. This run predates new
  describe/compare integration and therefore does not validate those later UI changes.
  The retained held-out confusion PNG was visually inspected: labels and counts are
  readable and correspond to recorded classifier output.
- Native diagnostics call production control events/actions on an invisible real
  window. They are not physical keyboard/mouse, assistive-technology or mixed-DPI
  tests. Cancellation observes a held owned process/startup, not a particular
  estimator fit instruction. Window reopening is not a full application restart.

### Retained failures and repairs

- `data-initial-sandbox-failure.txt` and `data-workspace-run.txt`: restricted test
  execution could not access Python-created private-ACL temporary directories.
  The tests then ran with approved execution using repository-contained synthetic
  fixtures; `data-approved-run.txt` passed 14/14. No production ACL was weakened.
- `engine-initial-run.txt`: LOF's automatic neighbor tree could not be reloaded under
  the fixed serialization trust boundary. Author changed KNN/LOF to bounded brute
  distances, retaining one thread and the 10,000-row cap. No dynamic trust expansion.
- `independent-expanded-run.txt`: gradient boosting needed exact pinned sklearn loss/
  link value types for safe reload. Author inspected those installed classes and
  introduced an exact nine-type allowance; independent binary/regression/multiclass
  reload and adversarial unknown-type checks pass. Broad namespaces remain refused.
- `protocol-diagnostic-run.txt`: reviewer fixture accidentally omitted SYSTEMROOT
  because Windows environment keys were uppercase. Correcting the fixture's key
  normalization produced `protocol-environment-repair-run.txt`, 6/6 pass. This was
  not a worker defect and did not motivate a production security relaxation.
- Native `ml-native-20260929-220133-7d02ec03`: initial capability/list startup failed.
  The Windows venv redirector conflicted with the one-process job limit. Host now
  launches validated base Python with the configured venv identity; actual native
  operations pass without admitting a second process or reporting launcher telemetry.
- Native `ml-native-20260929-220553-a8df6421`: 22 checks then Train rejected blank
  hyperparameters. WPF interpreted `Text="{}"` as an escape; author fixed it to
  `Text="{}{}"`. A dedicated native assertion now checks literal `{}`.
- `independent-current-run.txt`: 45 pass plus one reviewer export-column assertion
  error. Standalone export intentionally writes one unnamed prediction column;
  the fixture now checks that column's numeric values and shape. PY46 passes.

### Changed-line review coverage

Reviewed the Application contracts/validation/planner; all Python common/data/model/
engine/protocol modules and the export template; Infrastructure queue, host, job and
interpreter handling; four native UI files and native diagnostic; App composition,
startup parsing, companion/tray/chat entry points; project-file changes; runtime setup,
launch/verification scripts; and implementation/user/UI/worker documentation. Existing
unqualified voice files were preserved and not presented as ML deliverables. Worker
requirements and hashes pin the optional environment; a packaged redistributable Mu
release is outside this evidence. Later changes require an affected review/rerun.

### Required adversarial fixture families

- Small hand-computable regression/classification; missing values; mixed categories;
  unseen held-out category and extreme test-only numeric value; constant column;
  missing target, repeated group identity, time order, class imbalance.
- CSV/TSV, JSON/JSONL, safe/object NPY/NPZ, HDF5 nested datasets and partial slices;
  malformed/oversized input, external HDF5 links and compressed expansion.
- Persist/reload/predict with exact, missing, extra, reordered and incompatible feature
  schema; changed dataset; corrupted model; untrusted model type; path traversal.
- PCA/clustering/anomaly deterministic checks, noise preservation, invalid cluster
  counts, unavailable metric conditions and explicitly sampled plots/statistics.
- Cancel queued/running, malformed/oversized worker response, worker crash, runtime
  unavailable, restart after incomplete save, Python export execution and UI heartbeat.

## Final evidence and independent verdict

- **NET353 / STR7:** `artifacts/qa/ml-foundation/ml-reviewed-final.trx` and
  `final-reviewed-dotnet-tests.txt`: **353/353 passed, zero skipped**, including seven
  new independent structured-generation tests. `final-structured-tests-build.txt`
  records **zero warnings/errors**. The new tests independently check all current
  selection aliases, duplicate/unknown field refusal after resolution, closed schema,
  captured feature whitelist, explicit bounded paths without a selection, cloned
  schema ownership, unchanged ordinary-chat payload, schema admission limits,
  completion/cancellation enforcement and transport reuse after failure. Existing
  `ml-current-final.trx` separately recorded 346/346 before these added tests.
- **N57:** `artifacts/qa/ml-native-20260929-230409-e1b0e71e/ml-lab-smoke.json`:
  **57 assertions, failure=null, 137.904 s**, actual native view and production worker.
  Adds exact historical description/comparison and incompatible-task refusal to N48,
  plus resource-cap preservation, no authority on a rejected proposal, real one-row
  reviewed execution and single-use execution. Parent verification process exited 0.
- **LIVE2:** `ml-planner-smoke.json` in that same final native directory: **2/2 live
  local-model prompts passed using unmodified production transport** (inspect captured
  dataset; PCA on x/z honoring no scaling). This is two bounded examples, not a
  general language understanding benchmark or a multi-step agent qualification.
- **BASE38:** `artifacts/qa/ml-foundation/existing-ui-regression.txt`: **38/38 existing
  Mu native process/control checks passed**, retained profile
  `artifacts/qa/desktop-ui-20260929-230137-f73c8c5b/profile`.
- **ARCH11:** `final-architecture-repaired.txt`: **11/11 checks passed**. Strict
  four-project Desktop candidate build in `build-candidate.txt` has zero warnings/
  errors; test-project strict build covers the remaining source graph. Candidate is
  `artifacts/qa/ml-candidate/Companion.Desktop.exe`; the running normal Mu executable
  was preserved. `verified-source-manifest.json` records production source hashes and
  the candidate/evidence mapping; new reviewer test and report additions are later.
- **AUTHOR6:** `artifacts/ml-author-tests/author-suite-foundation.txt`: **6/6 passed,
  20.209 s**; the older `author-suite-final.txt` is retained as its own 5/5 checkpoint.
  Its algorithm loop covers 22 task/algorithm pairs. These are author tests,
  supplementary to the independently authored PY46/PY19 checks rather than an
  independent score assignment.

### Additional final repairs reviewed

The earlier live planner failed both prompts with prose/incorrect JSON shapes
(`ml-native-20260929-221635-a0d6002b/ml-planner-smoke.json`). Final production adds an
optional package-free structured-model port, bounded JSON schema on the existing
authenticated local transport and a typed `current` selection alias. The alias binds
to the captured local path/range; it does not execute model-generated code. The
ordinary chat request keeps its original generation settings and no response schema.
All original output, cancellation, token-admission and endpoint guards remain.
Independent STR7 and final LIVE2 validate this repaired path.

The UI previously let a model proposal change the captured row cap and could retain
an action after limit validation failed. It now validates against unchanged user
limits before admitting the action. N57 proves over-cap proposals cannot run and a
one-row selection can use a larger valid compute cap. Listing-limit warnings are
displayed, and documentation distinguishes the backend's 200-record catalogue from
the UI's 500-row ceiling. Backend pagination remains future work.

### Scores

The 50 individual scores appear beside the unchanged frozen criteria above.

| Category | Scores in criterion order | Points / 10 |
|---|---|---:|
| Correctness C1-C5 | 1, 2, 2, 2, 2 | 9 |
| Architecture A1-A5 | 2, 2, 2, 1, 2 | 9 |
| Mathematics M1-M5 | 2, 2, 2, 2, 2 | 10 |
| Testing T1-T5 | 2, 2, 2, 2, 1 | 9 |
| Reliability R1-R5 | 2, 2, 1, 2, 1 | 8 |
| Security S1-S5 | 2, 2, 2, 2, 2 | 10 |
| Performance P1-P5 | 2, 2, 2, 2, 1 | 9 |
| Maintainability N1-N5 | 1, 2, 2, 2, 2 | 9 |
| UX U1-U5 | 2, 1, 2, 2, 1 | 8 |
| Requirements Q1-Q5 | 2, 2, 1, 2, 1 | 8 |

**Total 89/100; overall 8.9/10. PASS for the integrated bounded foundation increment.**
No remaining critical defect was identified in the reviewed source and executed
scope. This is not a complete ML Phase 1 or packaged release pass. Lack of physical
keyboard/screen-reader, forced parent-death, power-loss recovery, optional reader and
broader language-model evidence is explicitly reflected in partial scores.

### Acceptance scope and remaining work

- **A regression:** real inspection, train-only preprocessing, metrics/residuals,
  same-cohort model comparison, saved experiment and standalone reproduction pass.
  Broad conversational task/target inference and automatic multi-baseline execution
  are not qualified by the two live examples.
- **B classification:** real stratified holdout/CV, precision/recall/F1, confusion
  matrix, coefficients/tree importance and model reuse pass. Class inspection
  describes the bounded selected preview; it is not an exhaustive whole-file census.
- **C detector exploration:** PCA, clustering, noise preservation, assignments and
  scientific caution pass. Coloring arbitrary projections, representative cluster
  samples and richer population statistics still need development.
- **D HDF5:** bounded group/dataset browsing and one-/two-dimensional partial ranges
  pass, including logically huge datasets and rejected external/virtual references.
  Attribute values, higher-dimensional event/channel slicing and waveform workflows
  remain future work.
- **E model reuse:** persisted model version, strict feature name/order/type schema,
  predictions and exports pass. External serialized-model import is deliberately
  unsupported; model/project lifecycle editing and full registry pagination remain.

### Top five ranked follow-up improvements

1. Extend conversational evaluation beyond two prompts: task/target ambiguity, IDs,
   group/time/CV requests, user overrides and multi-step retained Lab context, while
   retaining reviewed typed actions and fixed resource budgets.
2. Finish detector exploration: cluster-colored projections, representative records,
   population statistics and explicit HDF5 channel/dimension selections.
3. Add real project/model lifecycle and storage management: pagination, rename/tag/
   archive/delete, cache accounting/cleanup and restart/fault-injection qualification.
4. Add versioned feature engineering, feature selection, additional preprocessing and
   richer interpretation; qualify each new transform against leakage and reproduction.
5. Qualify physical keyboard/screen-reader operation, full restart/forced parent-death,
   mixed-DPI when hardware is available, optional readers and a distributable package.

Existing unqualified voice-input work, historical assistant phase gates and lack of
a second physical monitor remain separate. No new microphone or cloud access was used.
