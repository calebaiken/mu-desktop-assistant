# Phase 3A independent QA protocol

**PAUSED / SUPERSEDED SCOPE:** The user chose a pretrained local model before this
training protocol executed. No test, training run or passing score is claimed here.
The active scope is the [local pretrained AI increment](../LOCAL_AI.md).

Recorded 2026-09-22 before implementation contracts, dependency installation or a
training run were supplied to this reviewer. This is a requirements protocol, not
an execution report or a passing QA score. Module contracts and an exact bounded
experiment manifest must be appended/frozen before executable review begins.

## Scope and claim boundary

The user requests a new model trained locally from random initialization, with no
API key, cloud inference or pretrained weights. Phase 3A delivers the training and
inference foundation for a small byte-level causal Transformer. It does not by
itself complete the original Phase 3 conversation engine, streaming/history/chat UI,
nor establish instruction following, factual accuracy or useful assistant behavior.

Training-code correctness, successful optimization on a sanity fixture, held-out
learning and assistant capability are separate claims. A score must not award
assistant capability for a working trainer. Retain the original independent rubric:
ten categories, five 0/1/2 criteria each and a strict overall score greater than 8.5.
No score is assigned before executable evidence exists.

## Freeze before execution

Record source revision/diff hash, Python/framework/dependency versions and hashes,
hardware, CPU thread settings, dtype, architecture, vocabulary/special-token IDs,
initialization method, parameter count, all RNG seeds, optimizer and schedule,
context/batch/accumulation sizes, clipping, limits and train/validation/test manifests.
Freeze any learning acceptance threshold before observing the corresponding run.
Record every declared run and failure; revisions receive a new manifest and rationale.

The reviewer waits for stable module/CLI contracts before adding independent tests.
Root owns installation, application launches and training execution. Reviewer test
source and artifacts must identify which checks were independently authored and
which executions were performed by root. No concurrent training or build workload
may silently contaminate resource measurements.

## Provenance and data separation

- Weights start from the declared random initializer. No pretrained weights,
  pretrained tokenizer, teacher-model generated corpus or cloud fallback enters
  this increment without a new explicit user decision. Framework code is not
  represented as pretrained model weights.
- Use a declared fixed byte vocabulary and explicit special tokens, or document
  any locally fitted tokenizer. Test UTF-8 round trips, malformed byte output policy,
  empty input, reserved tokens, boundaries and maximum-length handling.
- Require selected corpus paths and provenance/permission records. Do not scan
  arbitrary user files or automatically train on conversation logs. A public corpus
  is not assumed licensed merely because it can be downloaded.
- Split by complete source document before windows or samples are constructed.
  Hash documents and split manifests; reject exact cross-split duplicates and
  document the limits of near-duplicate detection. No overlapping windows may bridge
  documents or held-out splits. Train-only data determines any vocabulary statistics
  and unigram baseline. No test-set tuning or silent resplitting.
- Bound input bytes, document count, per-document size and encoded token count.
  Reject or explicitly report invalid inputs; no silent empty train/evaluation set.
  Corpus provenance is required even for a small execution fixture.

## Independent mathematics and model tests

Use tiny deterministic CPU float64 fixtures, dropout disabled, for mathematical
checks. The independent oracle must not call the production attention/loss routine
to derive its expected answer. Predeclared comparison tolerances are absolute
1e-6 plus relative 1e-4 for centered finite differences (step 1e-6). Any unsuitable
nondifferentiable fixture is corrected and documented, not assigned looser tolerance
after a failure. Clipping is tested separately from the differentiable loss.

1. Compute tiny embeddings, causal attention, residual/normalization and logits
   with independent scalar/array formulas; verify tensor layouts, head scaling,
   normalization epsilon, mask direction and next-token target shift.
2. Check finite-difference loss gradients for every parameter family. Freeze tested
   coordinates/seeds before execution, including repeated byte IDs to exercise
   accumulated embedding gradients. Cover small and nonzero gradients and reject
   NaN/Infinity. A tiny all-parameter fixture is preferred where affordable.
3. Independently verify stable log-sum-exp/cross-entropy, extreme logits, ignored
   targets, the actual loss denominator and no-valid-target behavior. Verify a suffix
   token change cannot alter earlier causal logits, and cross-document boundaries
   and padding cannot become unintended prediction targets.
4. Verify the first optimizer updates against scalar formulas, including moment
   state, step count, bias correction, epsilon and declared weight decay. Check
   global-norm clipping, zero norm, accumulation equivalence and schedule boundaries.
5. Verify same seed/config gives reproducible initialization in the declared CPU
   environment, different seeds change weights, and inference evaluation mode does
   not update weights or consume training randomness unexpectedly.

## Persistence, resume and hostile input

- Store tensors in a non-executable format with bounded, versioned JSON metadata.
  Do not load pickle, arbitrary Python objects or object arrays. A safe tensor
  format still requires schema, dtype, shape, size and model-key validation.
- Resume includes model weights, optimizer state, schedule/step position, RNG state
  and data-order position. Compare uninterrupted N steps with K steps plus restart
  plus N-K steps under an identical declared environment. Require exact tensors and
  counters where deterministic CPU execution is configured; if unsupported, expose
  that limitation before claiming exact resume rather than silently relaxing it.
- Test truncated/corrupt/missing files, invalid or future metadata, tensor-key/shape
  mismatches, nonfinite values, excessive declared sizes and escaping paths. Check
  bounds before allocating/loading payloads wherever the format permits.
- A checkpoint must commit as a coherent generation: interruption between tensor
  and metadata writes cannot pair different generations. Test failures at each
  publication boundary, preserving the last usable committed generation.
- Test cancellation before save, during publication and immediately after commit;
  never report a committed checkpoint as rolled back. A failed resume must not
  overwrite usable artifacts or partially publish live training state.
- Generation has token/context/time limits and documented EOS/UTF-8 behavior.
  Seeded sampling is reproducible; independent sessions do not share hidden state.
  Cancellation must terminate the worker within its declared bound.

## Resource and CLI limits

The supplied inventory is approximately 7.93GB RAM, 2GB GPU VRAM and 2GB free disk.
These are feasibility inputs, not measured training capacity. Initial implementation
is CPU-only; no GPU/framework compatibility or training throughput is assumed.

Maintain at least 1GiB free filesystem reserve. Installation admission must account
for the full transitive environment, archive/cache, unpacking and temporary overlap,
not just the compressed wheel or a partially enumerated directory. Recheck actual
free space after installation and before each large write; a check is not a disk
reservation, so I/O failure must remain recoverable. If reserve cannot be retained,
do not install or launch; do not delete unrelated user files to create room.

Proposed initial artifacts cap is the smaller of 512MiB and free bytes minus 1GiB,
including corpus copies, reports, checkpoints, temporary files and backups. Require
explicit model/parameter and training-step limits as well. Float32 parameters,
gradients and two Adam moments alone require about 16 bytes per parameter; activations,
copies, framework state and serialization add to this, so it is not a peak estimate.

Before a longer run, declare and execute a bounded pilot with measured worker peak
memory, elapsed time, throughput and disk growth. Proposed first worker memory cap
is 2GiB; the supervisor must document its sampling interval, enforcement latency,
timeout and cleanup. If limits prove infeasible, record that result and revise scope
before another declared run; do not relabel an over-limit run as passing.

CLI tests cover malformed/negative/excessive limits, incompatible head dimensions,
unknown options, missing/empty datasets, unwritable/full destinations, existing-output
policy, incompatible resume and bounded interruption. Failure exits are nonzero and
actionable; model content and selected training text do not enter ordinary logs.
No network operation is required for training or inference after installation.

## Learning experiment and evaluation

Freeze the corpus-dependent numerical success criterion after corpus selection but
before training. The criterion must distinguish improvement from training-system
correctness; it cannot be invented from observed loss or selected samples.

Report initialized-model and train-only unigram baselines, token-weighted held-out
negative log likelihood, token counts and split hashes. For byte-level reporting,
define bits per byte and treatment of special tokens precisely. Per-document mean
loss is not substituted for token-weighted loss. Check reductions independently.
Report all declared seeds/runs, chosen checkpoint rules and validation decisions.
Use sealed test data only for the declared final evaluation; repeated selection on
test results consumes that holdout and must be labeled accordingly.

A tiny memorization fixture may demonstrate that gradients/optimization work; it
does not establish generalization. Preserve fixed-prompt generated samples without
cherry-picking, alongside numerical results and failure cases. Failure to meet the
predeclared learning criterion remains a failure even if mathematical tests pass.
Assistant behavior requires a separate unseen conversation/instruction evaluation
and is explicitly unqualified in this foundation increment.

## Required final review evidence

The later PHASE_3A_REVIEW.md must include the exact ten-category/50-criterion score,
execution attribution, retained protocol/run artifacts, mathematical and fault-test
results, dependency/corpus/weight provenance, observed resource limits and held-out
results. Include the required five ranked Problem / Why it matters / Proposed fix
items, regression risks and unexecuted tests. Preserve failures and explain scope
boundaries. A foundation pass must not close original chat/UI work or waive the
remaining Phase 2 release qualifications.
