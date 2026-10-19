# Independent QA protocol — bounded research project workspace

Frozen by the independent development QA reviewer on 2026-10-19 before reviewing
implementation changes. Baseline: `feature/trusted-text-inspection@376c320`.
The implementation branch will be `feature/research-projects`. This protocol is
phase-specific and does not award credit for future Module B, C or G features.

## Scope and evidence rules

The candidate should let the user explicitly create/select a locally managed
research project, open the existing ML Lab with that project's identity and
artifact/history directory, and return to a visible legacy `default-project`
without moving its contents. A single App-owned ML session must serialize
admission and switching, deny switching while work is active, and confirm old
session cleanup before admitting another. No main-Chat transcript transfer,
scientific algorithms, PR #1 changes, memory retrieval or extra dependencies are
part of this increment.

Each criterion below scores 0 (missing/contradicted), 1 (partial) or 2 (satisfied
by cited evidence). Each category has five equally weighted criteria. Overall =
all 50 criterion scores / 10, yielding a 0–10 score. The exact unrounded score
must exceed 8.5 and there must be zero unresolved critical defects to pass.
Insufficient native evidence can leave the phase incomplete; no simulated result
counts as a real worker/native integration pass. Reports will distinguish source
inspection, synthetic contract tests, real filesystem/worker checks, native WPF
checks and physical-device qualification. Failed reviews remain in source.

No numerical algorithm is being added. The mathematical category evaluates the
new identity/boundary/accounting invariants, unchanged scientific scope and QA
arithmetic; it cannot establish future scientific accuracy.

## Frozen 50 criteria

### Code Correctness

- C1: Create/select returns the intended immutable project identity, label and directory.
- C2: Reopen/restart preserves created projects and selection without duplicate identities.
- C3: Existing artifacts and history remain isolated by the selected project directory.
- C4: Legacy default-project is accessible unchanged; creating/switching does not relocate or overwrite it.
- C5: Invalid selection/catalog state fails clearly without silently selecting another project's data.

### Architecture

- A1: Core/Application/Infrastructure/Desktop references and package conventions stay intact.
- A2: Project contracts separate identity/storage/session ownership from WPF controls and worker internals.
- A3: Exactly one app-owned ML service/session admits work, including close/reopen behavior.
- A4: Switching and ExecuteAsync admission share serialized ownership; no Jobs-snapshot race exists.
- A5: Existing typed ML actions, worker protocol, model boundaries and proposal approval remain authoritative.

### Mathematical Correctness

- M1: Generated identifiers, project equality and derived paths are deterministic/unambiguous within their stated scheme.
- M2: Catalog count/text/byte/depth bounds are explicit and validated at the actual read/write boundaries.
- M3: Concurrent admission and switching uphold an explainable state invariant with exact transition tests.
- M4: No new scientific accuracy claims or changes to numerical algorithms, source selections or historical QA receipts occur.
- M5: QA reports show all 50 scores and exact arithmetic; baseline/final evidence is scoped accurately.

### Testing

- T1: Real filesystem create, reload, legacy preservation and project isolation checks execute.
- T2: Independent tests exercise malformed/duplicate/oversized/tampered catalog and unauthorized path inputs.
- T3: Independent concurrency tests cover active/queued admission, switching, disposal failure and shutdown.
- T4: Real supported Windows verification and negative gate run on candidate source with native project-control smoke evidence.
- T5: Relevant existing ML worker/process tests and architecture/build checks run; skips/failures and source identity are retained.

### Reliability

- R1: Switch is denied during queued/running work and does not cancel/misattribute existing jobs.
- R2: Old session disposal completes before a new one admits work, and uncertain cleanup blocks further admission.
- R3: Cancellation/shutdown/racing repeated calls settle boundedly without orphaned worker/session ownership.
- R4: Catalog updates are recoverable/versioned and failures leave existing projects/catalogs usable.
- R5: Closed/stale Lab views or planners cannot execute/display a proposal as belonging to a different project.

### Security

- S1: Project directories derive from trusted managed root plus validated internal IDs, never an arbitrary catalog/UI path.
- S2: Linked/reparse/remote/ambiguous paths and catalog identity tampering fail closed within the stated boundary.
- S3: Names/catalog fields and exceptions are bounded, validated and rendered literally with no command or markup interpretation.
- S4: Switching projects does not transfer chat/history/dataset selection/proposals or imply memory approval.
- S5: Offline process environment, approval requirements and credential/telemetry/dependency restrictions remain intact.

### Performance

- P1: Project listing/catalog parsing are bounded and avoid recursive data/model scans.
- P2: Switching does not run training, load local models or create concurrent ML worker services.
- P3: UI stays responsive while a session is draining/switching; project controls prevent duplicate reentry.
- P4: Stored metadata/job/view state has explicit count/size limits; idle polling or animation work is not added.
- P5: Existing CPU/memory/worker limits remain unchanged and new measurements/qualification gaps are stated honestly.

### Maintainability

- N1: Implementation is narrow, cohesive and uses existing conventions without broad unrelated refactoring.
- N2: Failure/state handling is centralized and understandable, with no swallowed ownership/integrity error.
- N3: Tests verify observable invariants rather than merely repeat implementation/private code.
- N4: Public contracts explain lifetime, cleanup failure, identity/version and compatibility behavior.
- N5: Status/changelog/testing/architecture/security/usage documentation match actual behavior and scope.

### UX/UI

- U1: Actual existing application navigation reaches project create/select and the selected ML Lab.
- U2: Current project label/identity and legacy status are visible before consequential ML work.
- U3: Create/select are explicit user actions with clear labels, validation feedback and no silent data import.
- U4: Active-work/switch/disposal/catalog failures are visible and permit a safe next step.
- U5: Native project controls support keyboard focus and practical minimum-size layout; untested physical/a11y behavior is disclosed.

### Requirement Compliance

- Q1: The selected bounded Module B prerequisite works end-to-end and is distinguished from a Chat-to-Lab bridge.
- Q2: PR #1 scientific functionality remains separate; no parallel HDF5/NumPy/waveform implementation is introduced.
- Q3: Existing conversations/settings/temporary mode/avatar/read-only tool workflow remain unaffected by scope.
- Q4: Historical failed measurements and independent review receipts remain preserved; no weakened gates or fabricated passes occur.
- Q5: Dedicated reviewable branch/PR, source identity, modified architecture, test results, limitations and next milestone are delivered.

## Review checkpoints

1. Inspect contracts and state machine; add independent adversarial tests without editing production files.
2. Review candidate and execute available portable/filesystem checks; record failures and rank the top five fixes.
3. Re-review repairs and genuine supported-platform evidence. A passing scoped verdict does not qualify the entire application for release or future scientific features.
