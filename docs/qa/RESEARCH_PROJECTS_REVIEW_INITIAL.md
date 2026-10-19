# QA REVIEW

Phase: B — bounded explicit research project workspace, Linux checkpoint (2026-10-19)

Overall Score: **8.6 / 10**

Result: **FAIL / phase incomplete pending supported-platform verification**

Independent reviewer: development QA agent, separate from the implementation author.
Reviewed working candidate on `feature/research-projects`, based on
`376c3206a38fa86b1639e5f72c6a67cc57153cb4`; implementation was not yet committed
when this checkpoint was recorded. The frozen
[50-criterion protocol](RESEARCH_PROJECTS_PROTOCOL.md) was written before reviewing
production changes. This report must remain as historical evidence after native
verification. No confirmed unresolved critical production defect was found in the
available review, but a lack of actual native execution prevents phase exit.

The arithmetic clears the numerical threshold. The reviewer applies the protocol's
explicit insufficient-native-evidence rule: missing required Windows gates, native
WPF behavior and real owned-worker routing cannot be inferred from cross-compilation
or synthetic services. This is an incomplete phase, not a passing independent verdict.

## Category Scores

Every criterion uses 0/1/2 and equal weights. IDs refer to the frozen protocol.

| Category | Criterion scores in protocol order | Sum |
|---|---|---:|
| Code Correctness | C1=2, C2=1, C3=2, C4=2, C5=2 | 9 |
| Architecture | A1=2, A2=2, A3=2, A4=2, A5=2 | 10 |
| Mathematical Correctness | M1=2, M2=2, M3=2, M4=2, M5=2 | 10 |
| Testing | T1=2, T2=2, T3=2, T4=0, T5=1 | 7 |
| Reliability | R1=2, R2=2, R3=1, R4=2, R5=1 | 8 |
| Security | S1=2, S2=1, S3=2, S4=2, S5=2 | 9 |
| Performance | P1=2, P2=2, P3=1, P4=2, P5=2 | 9 |
| Maintainability | N1=2, N2=2, N3=2, N4=2, N5=2 | 10 |
| UX/UI | U1=1, U2=2, U3=2, U4=1, U5=1 | 7 |
| Requirement Compliance | Q1=1, Q2=2, Q3=1, Q4=2, Q5=1 | 7 |

Exact arithmetic: `(9 + 10 + 10 + 7 + 8 + 9 + 9 + 10 + 7 + 7) / 10`
`= 86 / 10 = 8.6`. The numerical threshold is strictly greater than 8.5; the
supported-platform evidence requirement is independently unmet. No new scientific
algorithm was added, so the mathematical score covers identity/budget/state
invariants and arithmetic; it does not award scientific accuracy or future-feature
credit.

## Evidence

### Independently executed

From `/workspace/mu-desktop-assistant`, with the installed SDK 10.0.401 activated:

- `EnableWindowsTargeting=true dotnet test tests/Companion.Tests/Companion.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~IndependentResearchProjectTests --logger 'trx;LogFileName=checkpoint-independent.trx' --results-directory artifacts/qa/research-projects/independent`: **exit 0; 39 passed, 1 skipped, 0 failed, 40 total**. The skip explicitly requires Windows Job Object process integration. Real local files, backups and symlinks were used for the portable storage checks.
- `dotnet run --project tests/Companion.ArchitectureChecks -c Release --no-build -- /workspace/mu-desktop-assistant`: **exit 0; 11/11**. Receipt `independent/architecture-checkpoint.log`.
- `artifacts/ml-runtime/bin/python -m unittest discover -s tests/ml_lab -p test_research_projects.py -v`: **exit 0; one real production-worker subprocess test passed in 14.169 seconds**. Independently reproduces real training/listing/cross-project denial/describe/export and unchanged-source behavior. Receipt `independent/production-project-worker.log`. This is Linux worker evidence, not Windows host/job qualification.
- Earlier forced test-project cross-build: **exit 0, zero warnings/errors**; `independent/forced-build.log`. Its corresponding then-current test revision had **33 passed, 1 skipped**. Final independent discovery includes the additional six ownership/recovery/queued/cancellation/shutdown cases, as shown above.
- Existing ML baseline subset at the unmodified base: **19 passed, 3 failed, 22 total**. The three Windows absolute-path assumptions remain failures: `AllAdvertisedActionsRoundTripThroughOneRegistry`, `CurrentAliasesResolveToCapturedSelectionWithoutGeneratedPaths`, `CompleteLocalTextBecomesAnInspectableActionWithoutExecutionAuthority`. Receipt `independent/baseline-existing-ml.trx`; they cannot be relabeled portable successes.

Receipts above live under `artifacts/qa/research-projects/`. The initial independent
receipt (`initial-independent.trx`) retains one test-fixture failure: a lone
surrogate encoded in a `DataRow` attribute was normalized during test discovery.
The malformed UTF-16 check now constructs its input at runtime and passes. A stale
incremental test discovery during overlapping developer/reviewer builds is retained
in `portable-independent.trx`; stabilized serialized builds/runs above supersede it.
An initial architecture invocation omitted the required repository argument and
exited 2; the corrected actual check above exited 0.

### Author receipts inspected and source review

- `build-candidate.log`: serialized full Release cross-build, zero warnings/errors.
  A prior overlapping-build failure is preserved in `build-native-smoke.log` and
  was not treated as a passing build or a confirmed production defect.
- `research-projects-author.trx`: **50 passed, 1 skipped, 51 total**, combining the
  11 author cases and 40 independent cases. This is an author execution receipt,
  distinguished from the independent rerun above.
- `python-projects-final.log`: the actual production Python worker trains a real
  linear model in A, lists A/B independently, refuses B's access to A's experiment,
  describes/exports A, and preserves source data. **One real subprocess test passes**.
  `python-projects-first.log` retains the fixture's initial incorrect error-exit
  assumption; no PR #1 transport change was adopted to make it pass.
- Reviewed `ResearchProjects.cs`, `ResearchLabWorkspace.cs`,
  `JsonResearchProjectStore.cs`, App composition, project picker, Lab view changes,
  `ResearchProjectSmokeChecks.cs`, `LocalMlLabService` disposal delta, independent
  and author tests, usage/architecture/security/contracts/status/testing/changelog.
- No changes to worker production algorithms, closed action registry, pinned
  packages/lockfiles, project graph, build/negative scripts or CI workflow. No
  Chat/temporary-mode/speech/avatar production code was changed. No PR #1 HDF5,
  NumPy or waveform functionality was duplicated.

### Criterion-level interpretation and limits

- C1/C3/C4/C5: generated IDs derive managed paths, duplicate labels remain distinct,
  true artifact files stay in their own directory, legacy data is unchanged, and
  unknown/tampered metadata refuses selection. C2 is partial: projects persist;
  last selection is deliberately not restored automatically, so every open keeps
  the explicit chooser.
- A1–A5: four-layer graph remains exact; Application owns catalog/session contracts,
  Infrastructure owns JSON/filesystem/process work and Desktop composes controls.
  Admission increments synchronously under the same lock as transitions before an
  inner service can publish `Queued`. Tests demonstrate zero-Jobs admission, two
  pending requests, retired façades and exactly one admitted service factory.
- M1–M4: safe GUID-derived path identity, count/name/byte/depth bounds, exact
  cancellation/retirement transition barriers and unchanged scientific algorithms
  support the scored invariants. M5 arithmetic is displayed above.
- T1–T3 and R1/R2/R4: real persistence, capacity, corrupt/unknown/duplicate metadata,
  backup retention, interrupted write, links, pre-cancellation, synchronous failure,
  queued admission, cleanup exception, six-second timeout, cancellation during
  retirement and shutdown during transition pass independently. Borrowed façade
  disposal now denies rather than disposing the App-owned service. Missing primary
  with backup/project data now fails closed. These were review feedback repairs,
  verified by independently authored tests.
- T4 is missing. T5/R3/R5/P3/U1/U4/Q1/Q3 remain partial because full current Windows
  regressions, real process cleanup/routing and actual native control execution
  have not run. The source does include the smoke in ordinary `--smoke`, and old
  planning/view state is cancelled/closed before a fresh view is opened. Source
  inspection is not its native execution evidence.
- S1/S3–S5: catalog contains no paths, generated roots are fixed, literal names are
  validated, errors do not echo catalog text, and switching copies no Chat/dataset
  selection/proposal. Existing worker offline restrictions and explicit Run remain.
  S2 is partial pending actual Windows conditional link/reparse evidence; the
  documented trusted-profile boundary does not claim resistance to arbitrary
  malicious same-user ancestor swapping or hostile kernel/filesystem drivers.
- P1/P2/P4/P5: metadata is bounded, work runs off Dispatcher, no new idle timer,
  no recursive dataset scan/new model load from catalog operations, and existing
  worker compute budgets remain intact. No new resource benchmark was claimed.
- N1–N5: narrow contracts, centralized admission/integrity failures and ownership
  docs match the reviewed code. Schema 1 and manual preservation/restore recovery
  are described; there is no silent backup selection, destructive migration or
  unbounded recovery parser.
- U2/U3: identity/storage and legacy status are explicit, create/select is user
  driven, and exact per-action review is retained. U5 remains partial without
  executed native minimum-layout/focus tests or physical accessibility evidence.
- Q2/Q4: unselected modules and PR #1 remain separate; historical receipts and
  strict rules remain. Q5 is partial because candidate commit/PR/CI delivery has
  not yet occurred. The current work is a Module B prerequisite, not a finished
  main-Chat-to-Lab bridge, memory feature, model profile or release qualification.

## TOP 5 RANKED FIXES / IMPROVEMENTS

1. **Problem:** Required Windows verification and native/project-worker routing
   evidence are absent for this candidate. **Why it matters:** Linux cannot prove
   WPF composition, Windows cleanup or Job Object routing. **Proposed fix:** commit
   the candidate, run the unchanged positive then negative gates on Windows, verify
   exact source/run identity and inspect native/process outcomes before phase exit.
2. **Problem:** Physical keyboard/accessibility behavior and detailed native link
   outcomes are unqualified. **Why it matters:** a native HWND fixture is not
   screen-reader or physical interaction evidence. **Proposed fix:** retain the
   limits explicitly and later exercise picker focus, long names/paths, minimum
   size, high contrast and link/junction matrices on supported Windows.
3. **Problem:** Recovery is a manual, preserve-and-restore catalog workflow.
   **Why it matters:** a safe failure can still leave a user unable to reach saved
   experiments. **Proposed fix:** in a separate increment design explicit bounded
   backup inspection/recovery UI with version checks and user approval; do not
   silently rebuild identities or overwrite a damaged primary.
4. **Problem:** Ancestor swapping, kernel-stalled metadata I/O and process-death
   timing have not been qualified. **Why it matters:** pre-use path checks and a
   managed timeout are not OS sandbox or atomic filesystem-identity guarantees.
   **Proposed fix:** keep the trusted user-profile scope and perform a separate
   adversarial process/path/restart matrix before broadening the storage threat
   model. Do not claim those guarantees for this increment.
5. **Problem:** No measured catalog/switch UI/resource qualification is available;
   project selection is not remembered across reopen/restart. **Why it matters:**
   these remain usability/performance questions rather than established regressions.
   **Proposed fix:** measure a full 32-project catalog and repeated switches, and
   evaluate an explicitly confirmed last-project convenience separately. Preserve
   the chooser, current budgets and existing historical resource failures.

## Regression Risks

Modal picker closure during App shutdown, late planner completion, missing/corrupt
catalog recovery, borrowed service lifetime and current worker cleanup propagation
are the principal cross-component risks. Tests cover portable interleavings; the
required native gate and full existing process tests remain essential. Existing
scientific artifact confidentiality relies on profile access controls, not encryption
or isolation from malicious software running as the same user.

## Tests Required

Execute both unchanged Windows scripts, the real owned-worker project test and the
new ordinary native smoke at the exact candidate source. Review outcomes without
counting skipped conditional fixtures as successes. A final independent review must
cite that evidence and retain this incomplete checkpoint.

## Final Verdict

**FAIL / incomplete phase exit.** Available portable behavior passes, and no confirmed
unresolved critical production defect was found. Numerical score 8.6 does not turn
missing supported-platform evidence into a pass. Do not call this a release or an
implemented main-Chat bridge; final supported-platform independent review is pending.


## Subsequent finding at committed candidate `9ed4f51` — phase remains blocked

After this initial incomplete-native checkpoint was recorded, the independent
reviewer traced the actual close/reopen composition at
`9ed4f5122c88febb5addaa6e4195d1e88f274160` and found a concrete workflow regression.
Closing a Lab view leaves its existing App-owned job running, but `OpenMlLab()`
then routes to `SelectResearchProjectAsync()`, which rejects a busy workspace.
The user cannot reopen that project's Jobs controls to inspect or cancel the work;
the message recommends cancellation through controls that are no longer reachable.
This violates the existing close/reopen lifetime contract. A passing Windows gate
for that source cannot establish this missing behavioral scenario.

The implementation author confirmed the finding. Required repair: expose a current
borrowed selection for view resumption while work is active, reopen that selection
before trying to select another project, keep actual project changes denied while
busy, and verify cancellation through reopened native controls. Independent tests
will cover a running zero-Jobs request, same façade/factory identity, no premature
cleanup, current-selection absence during retirement/shutdown and continued switch
denial. This is a significant correctness regression and a phase blocker; no final
passing verdict applies to `9ed4f51`. The initial 8.6 arithmetic above records its
original checkpoint, not a reassessment after this newly discovered defect.

## Supported-platform evidence at diagnostic commit `72206a0` — still FAIL

The independent reviewer read the actual normal GitHub annotations API for push
run `39300426711`, job/check run `118223866106`, source
`72206a0e514912581122b946f1935521a55fe34b`. The bounded diagnostic annotation reports
**493 total, 492 executed/passed, 0 failed, 1 NotExecuted**. The sole nonexecuted
case is the deliberately unsupported-platform-only
`TextInspectionTests.NativeCapabilityIsUnavailableOnUnsupportedPlatforms`.
The real Windows owned-worker research-project routing method and all three A
native file/lease/oversize/leaf-and-ancestor-link methods are individually reported
**Passed**. This closes their earlier unknown conditional outcome for this source;
it does not qualify arbitrary same-user path swapping or physical interaction.

The native research control smoke nevertheless reports `completed: false`,
`failedCheck: minimum layout actions`, `failureType: InvalidOperationException`.
Only native picker HWND, no implicit selection, explicit create/select and trusted
storage preview are listed as passed. Required positive verification therefore
fails and the negative gate is skipped. These are real failed measurements, not
an accepted native delivery. The unit/process results cannot overrule the smoke.

The layout fixture attempted to measure an unshown Window. The author is repairing
it to display an invisible, nonactivated native window and await real minimum-size
layout, preserving the action-size assertion and the existing smoke deadline.
Initialization will share its Task across Loaded/explicit callers, with native Lab
startup jobs separated from proposal-execution assertions. A later source must pass
the unchanged gates before a final independent verdict is possible. Receipt:
`artifacts/qa/research-projects/independent/windows-diagnostic-native-annotations.json`.
