# Independent QA protocol — explicit Chat-to-Research handoff

Frozen by the independent QA agent before implementation on 2026-10-19.
Starting implementation baseline: PR #3 documented head
`7ce7f639c999c79631e786a7365578773e1da530`; PR #3's earlier QA verdict does
not qualify this increment. This protocol applies only to the bounded Module B
handoff requested for PR #4, targeting `feature/research-projects`.

## Scope and scoring

Ten equally weighted categories, five criteria per category, each scored 0
(missing/contradicted), 1 (partial), or 2 (satisfied by cited evidence). A
category totals 0–10. Overall = all 50 criterion scores / 10, using exact
arithmetic. Passing requires an unrounded result strictly greater than 8.5/10,
zero unresolved critical correctness/security defects, and sufficient evidence.
A numerical pass cannot override a critical defect or missing end-to-end proof.

No numerical analysis algorithm is added. Mathematical correctness evaluates
bounded selections/resource values, typed proposal preservation, and QA
arithmetic; it does not qualify scientific algorithms or pretrained inference.

## Frozen criteria

### 1. Code Correctness

1. An explicit Chat action captures and transfers exactly one bounded user-selected request without trimming or hidden context.
2. The selected stable project ID and exact request appear in a confirmation preview; cancel/dismiss performs no transfer.
3. Confirm populates only the intended live Lab's request and does not implicitly propose or run an action.
4. A valid explicitly captured dataset snapshot is reused only within its owning project; missing selection requires explicit Lab selection.
5. Changing request, dataset/path/key/range, project, or limits invalidates a stale proposal before execution.

### 2. Architecture

1. Existing four-layer dependency rules, pinned packages, and architecture checks remain intact.
2. Application-owned research project selection and workspace/service ownership are reused, with no parallel catalog or storage format.
3. `MlConversationPlanner`, `MlValidation`, resource controls, and existing reviewed Run remain the proposal/execution route.
4. Transfer identity and admission/retirement checks share an authoritative lifecycle boundary rather than relying solely on mutable UI labels.
5. Temporary handoff state is bounded and disposable, without new persistent memory, worker protocols, or hosted providers.

### 3. Mathematical Correctness

1. Request length boundaries, Unicode handling, and exact-preview equality are verified at valid and invalid boundaries.
2. Captured row start/count and selection key are preserved exactly, validated, and never expanded by generated proposals.
3. Compute-limit snapshots are validated and stale changes cannot silently expand execution budgets.
4. Generated typed actions remain distinct from verified computations; fixtures are not claimed as pretrained or scientific qualification.
5. Review scores show all 50 raw criterion values and exact arithmetic, applying the strict greater-than-8.5 threshold.

### 4. Testing

1. Independent automated tests exercise successful selected-project handoff, cancel/dismiss, and Temporary refusal.
2. Tests prove zero model calls, worker executions, dataset inspections, and artifact writes during transfer, including Lab opening/Loaded.
3. Adversarial tests cover stale/retired project identity, admission races, active work, duplicates, cancellation, and late callbacks.
4. Tests cover malformed/oversized requests, missing/changed datasets, changed limits, shutdown, and missing model/ML runtimes.
5. Supported Windows mandatory positive/negative gates and native real-window smoke execute with inspectable results; Python/project-isolation regressions run where supported.

### 5. Reliability

1. Closed/reopened Chat cannot deliver a stale handoff or leak request state into a replacement view.
2. Closed/reopened Lab and project retirement cannot redirect a late transfer into another lifecycle/project.
3. Pending/active jobs prevent unsafe switching or request replacement while retaining existing cancellation controls.
4. Duplicate confirmation/clicks consume at most one handoff and cancellation/shutdown cleanly invalidate pending state.
5. Missing runtimes, failed initialization, and cancellation errors leave actionable UI status with no implicit retry/execution.

### 6. Security

1. Temporary Chat handoff is disabled and independently refused at the application boundary, with project persistence explained.
2. Transferred text is treated only as untrusted request data and grants no file access, model authority, or execution permission.
3. Dataset paths are never extracted/invented from Chat text or model output; captured selection is authoritative and project-bound.
4. No hidden conversation, assistant messages, clipboard/file contents, cross-project context, or memory enters the transfer.
5. Request, dataset contents/paths, and other private content are absent from new audit/diagnostic logs and hidden persistence.

### 7. Performance

1. Transfer is bounded in request size and retained state, with no accumulating queue/history.
2. Transfer/open does not start model inference, runtime installation, background jobs, worker metadata calls, or scientific computation.
3. UI work remains asynchronous where needed, bounded, cancellable, and avoids blocking waits on the UI thread.
4. Existing CPU/memory budgets, timeout thresholds, and admission limits are preserved rather than weakened to pass verification.
5. Shutdown/cleanup and repeated open/cancel cycles release transient state/events without unbounded retention or extra worker processes.

### 8. Maintainability

1. Changes are cohesive and limited to handoff and essential proposal/lifecycle prerequisites.
2. Typed APIs make proposal, transfer, accepted request, and executed result states clear and independently testable.
3. Errors fail closed with useful private-data-safe explanations and no silent fallback destination.
4. Tests use behavioral boundaries and meaningful integration evidence, documenting fixtures and platform prerequisites.
5. Usage/security/architecture/testing/status/changelog documents describe actual behavior and limitations without altering historical receipts.

### 9. UX/UI

1. Chat exposes a clear keyboard-reachable Send to Research Lab action within current WPF/layout conventions.
2. Preview presents the complete exact request, destination project name/ID, and local persistence disclosure before explicit confirmation.
3. Cancel, dismissal, disabled Temporary state, and busy/invalid-destination states provide clear feedback.
4. Lab visibly distinguishes transferred request, generated proposal, and executed result, and prompts for explicit dataset choice when missing.
5. Real-window checks support focus/navigation, readable bounds, reduced-motion compatibility, and existing avatar/chat behavior; manual-only gaps are reported.

### 10. Requirement Compliance

1. Dedicated branch derives from verified current PR #3 without merging main/PR #1 or rewriting earlier work; draft PR targets the requested branch.
2. User-observable workflow reaches exact transfer, explicit proposal, review, and existing Run without automatic execution.
3. Selected project and dataset identity remain isolated across switching, stale callbacks, and application shutdown.
4. No long-term memory, new algorithms, remote inference, shells, autonomous loops, or unrelated features are introduced.
5. Delivery names source/review commits, actual test evidence, independent verdict, unverified conditions, and a separate approved SQLite-memory follow-up proposal.

## Evidence and review procedure

The independent agent owns separate tests and scored reports and makes no
production edits. Builds are serialized with the implementation agent because
the checkout and intermediate outputs are shared. Behavioral tests take
precedence over string scans; source inspection supplements checks that cannot
run in Linux. Native WPF/Win32 evidence requires a supported Windows runner.
Missing or skipped infrastructure checks remain explicitly unverified.

Preserve failed reviews and historical measurements. At each review cite the
exact implementation checkpoint, commands/exit codes, test counts/skips,
native-window evidence, fixtures, and limitations. Report the top five ranked
fixes/improvements even for a passing bounded-development verdict. Existing
resource, pretrained-model, physical-device and release qualification are not
implicitly granted by this handoff review.
