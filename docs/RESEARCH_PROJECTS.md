# Local research projects — first Module B increment

Normal navigation: **More / Chat / tray → ML Lab → create or select a research
project → review its identity and storage → Open selected project → choose and
inspect a local dataset**. The Lab continues to use its existing explicit training,
prediction and export controls, and its **Ask Mu → Propose a plan → Run reviewed
plan** sequence. A project selection is not permission to run a proposed action.
Model explanations remain separate from the worker's verified result records.

**Change project…** opens the picker again. Existing queued/running work must finish
or be cancelled first. Choosing the same project creates a fresh view; closing a
Lab view retains its App-owned service/work. Reopening ML Lab resumes that same selected
project even while jobs are active, so Jobs/cancel controls remain accessible.
Switching projects is a separate guarded operation. Selecting another project retires the
old service before admitting a new one. Old view/planner references cannot execute
against either project after retirement. New views have no inherited dataset,
features, proposed action, metrics or plot; saved history is reloaded only from the
selected project's existing worker registry.

## Storage and compatibility

- All roots derive from Mu's existing local profile: `ml-lab/projects/<generated
  GUID>`. Names are literal labels, never directory names or executable input.
  Identical labels have distinct visible IDs and distinct directories.
- The original `ml-lab/default-project` appears as **Existing default project**
  only when that directory exists. Its identity is the reserved empty GUID. No
  relocation, import, rename, model loading or destructive migration occurs.
- The catalog is `ml-lab/research-projects.json`, schema version 1. It contains
  only project IDs, names and UTC creation times. Creation is an explicit write;
  listing may create the catalog lock/root but creates no project, analysis or data
  reference. A project artifact directory is created by existing worker operations.
- Bounds: 32 new projects plus the legacy project, names of 1–80 UTF-16 code units,
  catalog at most 64 KiB, JSON depth 6. Control/invisible formatting and malformed
  Unicode names, unknown/duplicate fields, duplicate/reserved IDs, malformed dates,
  oversized files and unknown schema versions are refused. Paths are never loaded
  from metadata. Catalog work runs off the dispatcher; no idle timer is added.
- The existing atomic writer flushes a new temporary file before rename/replace;
  `.bak` retains the previous catalog. A per-operation exclusive lock prevents
  concurrent catalog writers from silently losing updates. Contention fails with
  an access message; refresh/retry once the other operation finishes.

Invalid primary metadata is not silently replaced from backup. Missing primary
metadata with a backup or project directories is also refused. To recover, stop
Mu, preserve the primary/backup and all project directories, inspect a known valid
version-1 backup, then explicitly restore that catalog before reopening. Nothing
here deletes artifacts, repairs unknown versions or silently rebuilds identities.
Keep both originals for diagnosis; do not infer that an empty/new catalog represents
your old experiments. A guided recovery/editor is a later increment.

## Admission and privacy

`ResearchLabWorkspace` belongs to App. Its lock serializes job admission and project
transitions. The active count increments before calling the underlying Lab, since
`Jobs` can be empty before a queued request is published. Switching refuses active
or queued requests; a retired project's façade permanently denies new work.
The service supplied to a view is borrowed; its disposal is rejected explicitly.
Only the workspace disposes the underlying service.

Retirement waits up to six seconds for old-service disposal. An exception or timeout
poisons the workspace and retains the cleanup task; no new project service may be
created. Restart is required. App shutdown awaits the transition/cleanup with its
existing bounded outer shutdown budget and reports failure rather than treating
uncertain cleanup as success. `LocalMlLabService` now propagates its existing worker
cleanup poison from disposal instead of swallowing that ownership failure.

Catalog/root/selected paths reject UNC and existing linked/reparse ancestors using
the Lab's existing local-path policy. The profile and its Windows user ACL are the
trusted storage boundary. These checks do not claim atomic exclusion of an arbitrary
same-user process swapping profile ancestors, sandbox hostile filesystem drivers,
or provide encryption/security against another process running as the user. Do not
place the profile in a shared or attacker-writable directory.

Dataset references and scientific artifacts saved by existing inspection/training
remain local and project scoped. Units/provenance remain the existing worker
metadata; this increment invents no units or scientific analysis. Catalog metadata
is local personal data, with the same profile access controls and backup
responsibilities as existing Lab artifacts. It is never sent to Chat, models,
telemetry, a shell or a remote service. No temporary-chat content is read or copied.
Opening/creating/switching projects loads no model weights or datasets. No new
dependency, worker action, model profile, memory, advanced waveform analysis or
main-Chat bridge is introduced. PR #1 remains independent.

## Verification and remaining scope

Code `c8da7e5664faec3f8d5430bec0ab018aa8512f42` passes both required Windows gates
in two runs. Actual annotations report 492 passing tests/zero failures/one expected
platform skip, all four named native integration cases Passed, and 21 passed native
project checkpoints. Independent [development QA](qa/RESEARCH_PROJECTS_REVIEW_WINDOWS.md)
passes at 96/100=9.6/10 with zero confirmed unresolved critical defects in this slice.
Earlier incomplete/failed checkpoints are preserved. This is not release qualification.

Author/independent tests use actual catalog files, backups, corrupt and linked
paths, active-admission barriers, stale façades, cancellation, failed/late cleanup
and missing production runtime. A Windows-only test uses a real owned stdlib worker
to verify project routing. The production Python-worker subprocess test trains and
exports in A, verifies A's source is unchanged and B cannot describe A's experiment.

The ordinary required `--smoke` path now creates native picker/Lab HWNDs and checks
the actual controls, catalog/session, metadata-only selection, distinct view state,
proposal-versus-execution separation and production missing-runtime recovery. Its
local-model JSON response is an explicit fixture. Passing that smoke does not prove
physical picker/accessibility interaction or qualified model inference. Check
[Testing](../TESTING.md) and the independent reports for actual run results; a source
check or skipped case is never a passing native execution.

The close/reopen/cancel control diagnostic also uses an explicitly declared blocking
service fixture. A fixed-label smoke receipt and additive always-run CI summary
and escaped check annotation publish counts, class/method names, four selected native
method outcomes and checkpoint/type labels, never DataRow values,
error text, stack traces or private content. Missing/invalid evidence is labeled
unavailable; these diagnostics do not replace or soften either required gate.

Next: the explicitly approved Chat-to-project request handoff, captured dataset and
existing single-use reviewed Run. Then approved SQLite project memory. PR #1's
HDF5/NPZ/waveform work follows its own review; model/resource and release qualification
retain their original separate gates and historical failures.
