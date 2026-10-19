# QA REVIEW — explicit Chat-to-Research handoff, Windows checkpoint

Phase: bounded Module B explicit handoff, independent development QA, 2026-10-19.

Overall Score: **9.6 / 10**

Result: **PASS for this bounded development increment**. Zero confirmed unresolved
critical correctness/security defects remain in the reviewed handoff. This verdict
does not qualify installed pretrained inference, scientific algorithms, physical
accessibility, resource/stress targets, or a release.

Reviewed executable source:
**`a33401fe7e595bf4fb1148a32aba3b913530df4b`**.
Base: PR #3's final documented
`7ce7f639c999c79631e786a7365578773e1da530`.
[Draft PR #4](https://github.com/calebaiken/mu-desktop-assistant/pull/4) is open from
`feature/chat-project-handoff` into `feature/research-projects`; the reviewer
independently verified both refs and the exact head through GitHub REST.
Main and earlier PR histories are not merged or rewritten by this increment.

The implementation author did not assign this score. The independent agent froze
the [50 criteria](CHAT_PROJECT_HANDOFF_PROTOCOL.md) before production changes,
authored `IndependentChatProjectHandoffTests.cs`, ran independent checks, inspected
the code/receipts, identified lifecycle/harness problems, and reviewed the fixes.
The original **83/100 = 8.3 FAIL / INCOMPLETE** and all subsequent native failures
remain in [the initial review and addenda](CHAT_PROJECT_HANDOFF_REVIEW_INITIAL.md).

## Actual Windows evidence

The reviewer independently queried both runs, jobs and bounded check annotations:

| Exact-source run | Windows job/check | Required positive gate | Required negative gate | Handoff smoke |
|---|---|---|---|---|
| [Push 39307770822](https://github.com/calebaiken/mu-desktop-assistant/actions/runs/39307770822) | `118249180591` | Success | Success | Completed, 28 distinct markers, 1357 ms |
| [PR 39307779749](https://github.com/calebaiken/mu-desktop-assistant/actions/runs/39307779749) | `118249212805` | Success | Success | Completed, 28 distinct markers, 1305 ms |

Both run identities report exact head `a33401fe7e595bf4fb1148a32aba3b913530df4b`.
The unchanged Windows workflow executes:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-negative.ps1
```

Both annotations report **525 total / 524 executed and passed / 0 failed / 1
NotExecuted**. The sole NotExecuted method is the deliberately unsupported-on-
Windows `TextInspectionTests.NativeCapabilityIsUnavailableOnUnsupportedPlatforms`.
This is an explicit platform skip, not a hidden passing case. The positive script
retains locked restore, strict build, architecture checks, C# tests, and the composed
WPF process exiting successfully within its original **15-second** deadline. The
negative script runs afterwards successfully. Neither script, workflow command nor
acceptance threshold changed in this PR; a direct base-to-reviewed-head diff is empty
for `scripts/verify.ps1`, `scripts/verify-negative.ps1`, and `.github/workflows/verify.yml`.

The annotations explicitly identify these four pre-existing native cases as Passed:

- `IndependentResearchProjectTests.RealWindowsOwnedWorkerRoutesRequestsOnlyToSelectedProjectAndReloadsItsOwnMarker`
- `TextInspectionTests.WindowsRealPipelineRequiresConsentLocksIdentityAndRedactsPersistentAudit`
- `TextInspectionTests.WindowsOversizedFileIsRefusedBeforeConsentAndLeaseIsReleased`
- `TextInspectionTests.WindowsLeafAndAncestorLinksAreRefusedWithoutReadingTarget`

All 21 prior research-project native checkpoints still complete. The **28 distinct
new handoff markers** verify real App routing through real project-picker and
confirmation windows, Temporary refusal, minimum Chat/preview bounds, exact request
and destination, successful request population, zero worker/artifact transfer after
Lab Loaded, absent/inferred dataset refusal, same-project key/range retention,
Cancel/dismiss, closed destination, owner-close cancellation, duplicate pending
submission, typed proposal/exact selection and limits, fresh runnable baselines
before input mutations, mode invalidation, noncooperative late output, explicit Run
as the first worker admission, missing-runtime/model failure, and closed-Lab cleanup.
The mutation loop executes all seven inputs even though repeated labels are deduplicated.

These are actual native HWND/controls and application/service paths. The model output
and delayed callback are declared fixtures; successful Run here deliberately reaches
the production missing-runtime failure. The receipt does **not** prove pretrained
inference or a successful scientific computation. Real owned-worker routing and the
production Python project-isolation execution supply separate process/data evidence.

Detailed log/artifact download remains restricted in this environment. The reviewer
used genuine GitHub REST job step outcomes and bounded TRX/native-receipt annotations,
not a simulated Windows environment or inferred success from compilation. Diagnostics
publish only fixed labels, validated counts/durations, method identities and exception
type; they exclude requests, paths, DataRow values, output and private error payloads.

## Independent local checks and supporting receipts

The independent agent executed these in managed Linux, after a serialized strict
build shared with the implementation agent:

```text
dotnet test tests/Companion.Tests -c Release --no-build --no-restore
  -p:EnableWindowsTargeting=true
  --filter FullyQualifiedName~IndependentChatProjectHandoffTests
  --logger trx;LogFileName=independent-handoff.trx
  --results-directory artifacts/qa/chat-project-handoff/independent
exit 0: 32 passed / 0 failed / 0 skipped of 32

dotnet run --project tests/Companion.ArchitectureChecks -c Release --no-build --
  /workspace/mu-desktop-assistant
exit 0: 11/11

git diff --check
exit 0
```

The independent TRX was parsed directly, including the reentrant worker/project
admission test. Application/planner APIs under those checks are unchanged by the
later native owner/closing repairs; the exact final Windows full suite includes
these cases. The author separately reports strict cross-build zero warnings/errors,
103 passed / four explicit Windows-only skips of 107 selected regression cases,
and 49 relevant Python cases passed; the reviewer inspected their retained receipts.
These are supporting author results, not represented as commands run by the reviewer.

The reviewer additionally reran the production worker's project-isolation test:

```text
PYTHONPATH=workers/ml_lab artifacts/ml-runtime/bin/python -m unittest discover
  -s tests/ml_lab -p test_research_projects.py
```

This runs actual production train/list/describe/export subprocesses with local public
data. It checks cross-project denial, independent registries and unchanged dataset
source. It is separate from the handoff's declared model fixture and native
missing-runtime path. **Actual result: exit 0, one test passed in 11.932 seconds.**

Historical Linux limitations remain explicit: the broader initial C# planner selection
has two Windows-path fixture failures on Linux; broad Python discovery reports 21
Windows `SystemRoot` protocol errors, and an initial relevant Python invocation had
an import-path error. The corrected `PYTHONPATH=workers/ml_lab` relevant suite passes
49 tests. None of the failed/unsupported attempts is converted into passing evidence.

## Frozen criterion scores

Each criterion uses the frozen protocol's wording and order. **2** means satisfied
for this bounded increment by cited evidence; **1** records the stated partial
qualification. No scientific-accuracy feature exists in this increment.

| Category and criterion | Score | Evidence / qualification |
|---|---:|---|
| Code C1: exact bounded selected request | 2 | Independent exact-whitespace/Unicode test; native exact preview; Chat captures only MessageInput text. |
| Code C2: identity preview and cancel | 2 | Real picker/preview name-ID-storage source; native Cancel and dismissal leave request unchanged. |
| Code C3: live intended Lab, no implicit run | 2 | Exact source/view checks, lease ticket; native same-window destination and zero jobs/Run disabled. |
| Code C4: captured project dataset or explicit selection | 2 | Native same-project key/start/rows retained; new project has no inferred dataset; validated ReadDataset before model. |
| Code C5: stale proposal invalidation | 2 | Native seven input edits, mode and delayed output; project retirement tests and immutable proposal action/limits. |
| Architecture C1: four layers and pins | 2 | Independent 11/11 checks; Windows required gate; no dependency/lockfile/layer changes. |
| Architecture C2: existing ownership/catalog | 2 | Existing App store/workspace reused; no project storage/schema duplication. |
| Architecture C3: existing planner/validation/Run | 2 | Actual Propose and Run handlers; native typed proposal then explicit production admission. |
| Architecture C4: authoritative identity/lock | 2 | Same sync lock protects ticket consumption and worker admission; cross-workspace/stale lease tests. |
| Architecture C5: disposable bounded state | 2 | At most one UI transfer; Interlocked one-use/null clearing; no queue, persistence or provider additions. |
| Math C1: request bounds and Unicode | 2 | Invalid/control/format/malformed UTF-16 plus exact 2000-unit supplementary boundary tests. |
| Math C2: row/key preservation | 2 | Four file-action types reject changed path/key/start/rows; native parsed exact JSON range. |
| Math C3: compute budgets | 2 | MlValidation unchanged; limits captured once; native memory/thread/row edits invalidate before Run. |
| Math C4: proposal versus verified computation | 2 | UI review/result separation; fixture disclosures and explicit missing-runtime failure rather than claimed inference. |
| Math C5: exact QA arithmetic | 2 | All 50 values below total96; threshold applied strictly; initial83 retained. |
| Testing C1: independent core cases | 2 | 32 independent behavioral cases plus native real App success/Cancel/Temporary. |
| Testing C2: zero execution transfer including Loaded | 2 | Application port tests, metadata byte/file equality, native deferred Loaded zero jobs/artifacts, lazy host source. |
| Testing C3: adversarial lifecycle/concurrency | 2 | Pending-before-Job event, failed/completed admission, cross-workspace, 12 concurrent confirms, retirement and native late callback tests. |
| Testing C4: malformed/data/limits/shutdown/runtimes | 1 | All named request/data/limit/runtime and workspace-shutdown cases covered; full process shutdown while modal/forced-kill matrix not independently executed. |
| Testing C5: required gates and scientific regressions | 2 | Both exact-source Windows runs, native annotations and relevant49 Python receipt; independent real project test. |
| Reliability C1: closed/reopened Chat | 2 | Native owner-close and duplicate pending close; new Chat instance, exact source/token checks. |
| Reliability C2: closed/reopened Lab/retired project | 2 | Native closed destination/late model cleanup; retire-return ticket tests and exact service reference. |
| Reliability C3: active work admission | 2 | Application lock tested before Jobs publication; prior native reopened-job cancel remains passing. |
| Reliability C4: consume once and cancel | 2 | Interlocked request consumption, replay/concurrent/cancel tests; native synchronous modal cancellation before owner teardown. |
| Reliability C5: missing runtime/actionable recovery | 2 | Native missing dataset/model and explicit Run missing-runtime failure; no automatic retry/admission. |
| Security C1: Temporary boundary | 2 | UI disabled/disclosure plus direct application refusal; native and independent tests. |
| Security C2: untrusted request grants no authority | 2 | DTO is only string+destination/lease; no worker/model call in Prepare/Accept; reentrancy rejected. |
| Security C3: no invented/cross-project datasets | 2 | Planner equality on all four file actions; no-selection/invention tests; native new-project empty selection. |
| Security C4: no hidden context | 2 | Single MessageInput snapshot; API has no history/clipboard/memory payload; model sees only explicit prompt/context. |
| Security C5: no content diagnostics/persistence | 2 | Catalog bytes/file-set equality, no request in errors/files; source adds no production log/storage call; strict fixed-label smoke summary. |
| Performance C1: bounded state | 2 | 2000 UTF-16 units, one pending transfer, no retained collection or hidden history. |
| Performance C2: transfer performs no computation | 2 | Native zero Jobs/artifacts and deferred Loaded; lazy model host; model/worker only later explicit controls. |
| Performance C3: UI responsiveness/cancellation | 2 | Async catalog/workspace; synchronous bounded UI delivery; native modal cancellation returns;1357/1305ms measured handoff smoke. |
| Performance C4: existing budgets | 2 | No compute limits,15-second gate,3-second local watchdog or independent threshold weakened. |
| Performance C5: lifecycle/resource stress | 1 | Actual close/cancel/cleanup cycles and zero extra workers verified; idle CPU/memory, stress/forced termination/coexistence remain unqualified. |
| Maintainability C1: narrow coherent scope | 2 | Handoff plus essential deferred initialization/planner/lifecycle prerequisites; no memory/new algorithm. |
| Maintainability C2: typed state boundaries | 2 | Explicit ticket, typed ML proposal, single-use delivery and existing completed/failed job results. |
| Maintainability C3: fail-closed errors | 2 | Stale/cancel/closed/unknown/busy errors and generic Chat destination error; no fallback destination. |
| Maintainability C4: meaningful test boundaries | 2 | Real files/store/production missing-runtime service, real Windows App workflow, declared fixtures and process routing. |
| Maintainability C5: accurate docs/history | 2 | Usage/contracts/security/architecture/testing/status/changelog updated; initial score/failures retained and limitations explicit. |
| UX C1: explicit accessible action | 2 | WPF button/access key/automation name; native saved/Temporary enablement and minimum bounds. |
| UX C2: exact preview and persistence disclosure | 2 | Real exact request/identity preview, readable scrollable fields, Cancel focus and explicit Confirm; disclosure source. |
| UX C3: cancellation and denied state feedback | 2 | Actual Cancel/dismiss/closed destination/Temporary states; Chat status requires re-selection when uncertain. |
| UX C4: transfer/proposal/result distinction | 1 | Boundaries and no-action labels are clear; transferred 'Exact request below' label remains after manual editing and should mark edited provenance. |
| UX C5: physical a11y/keyboard | 1 | Native geometry, default Cancel focus, keyboard names, system brushes and no animation inspected; physical keyboard/screen-reader/high-contrast testing not performed. |
| Compliance C1: requested branch/stack | 2 | Exact PR3 base, draftPR4 target independently fetched; no main/PR1 merge or history rewrite. |
| Compliance C2: user-visible bounded workflow | 2 | Real App transfer and explicit Propose/review/Run paths execute with zero automatic computation; fixture qualification explicit. |
| Compliance C3: project/dataset/lifecycle isolation | 2 | Independent admission/identity tests, native cancellation/closed windows and production Python project execution. |
| Compliance C4: excluded features absent | 2 | No memory/scientific algorithm/remote inference/shell/provider/agent loop/dependency changes. |
| Compliance C5: delivery and separate memory sequence | 2 | Source/PR/test identities and limits documented; separate approved editable/deletable SQLite memory proposal only. |

Category totals:

| Category | Total |
|---|---:|
| Code Correctness | 10 |
| Architecture | 10 |
| Mathematical Correctness | 10 |
| Testing | 9 |
| Reliability | 10 |
| Security | 10 |
| Performance | 9 |
| Maintainability | 10 |
| UX/UI | 8 |
| Requirement Compliance | 10 |

Exact arithmetic: `10 + 10 + 10 + 9 + 10 + 10 + 9 + 10 + 8 + 10 = 96`;
`96 / 10 = 9.6`; **9.6 > 8.5**. No rounding changes the threshold outcome.
All deductions are retained explicitly; none is replaced by claimed fixture/model
or unsupported-platform success. No unresolved critical defect overrides the result.

## TOP 5 RANKED FIXES / IMPROVEMENTS

1. **Extend process shutdown/failure qualification.** Why: native owner-close and
   workspace disposal do not cover every whole-process interruption. Next: a separate
   controlled graceful-shutdown-while-preview/job test and forced-kill/low-memory
   matrix with private-data-safe receipts. Existing cancellation guards must stay.
2. **Qualify the installed-model and runtime success path.** Why: this PR proves
   transfer and authorization boundaries using declared proposal ports and actual
   missing-runtime failure. Next: run a configured pretrained model and installed
   ML stack through a successful reviewed action independently; retain model identity,
   resource measurements and scientific provenance rather than calling fixtures inference.
3. **Perform physical keyboard/a11y/display checks.** Why: real HWND/bounds and access
   keys do not establish screen-reader or high-DPI usability. Next: Tab/Escape/access
   keys, reader announcements, high contrast and constrained displays with actual users/devices.
4. **Mark user-edited transferred requests.** Why: 'Exact request below' currently
   remains after manual Lab edits. Next: show that the local draft was edited while
   retaining current cancellation/versioning. This is a nonblocking provenance-label
   improvement; actual initial transfer is exact and edits invalidate execution.
5. **Measure repeated lifecycle resource behavior before broader release.** Why:
   short native smoke timings prove completion, not idle CPU/memory/coexistence targets.
   Next: retain historical failed measurements and evaluate repeated view/transfer
   cycles and independent workloads without changing budgets. Develop SQLite memory
   only as the separately approved, project-scoped next PR described in usage docs.

## Regression Risks and Tests Required

Chat shutdown now begins in `Closing`, before WPF owner teardown, with an idempotent
`Closed` fallback. Future changes that cancel closing or alter modal ownership must
retest this order. Cancellation must remain bound to captured original dialogs and
check tokens before opening them. New Lab views created by a handoff deliberately
defer metadata startup; normal ML Lab navigation retains its existing initialization.
Both paths and the existing project/job-cancel smoke remain regression gates.

Changing proposal inputs must continue to invalidate the stored typed action and
cancel old inference; a late noncooperative completion must never restore Run.
Dataset equality is selection equality, not proof of file existence or unchanged
contents: the explicitly executed worker remains responsible for actual validation
and provenance. No cross-project retrieval or request persistence is authorized.

Required gates and relevant regressions pass at the reviewed checkpoint. Physical
accessibility, installed pretrained inference/coexistence, idle CPU/memory and the
broader crash/forced-kill/low-memory matrix remain explicitly unverified. Existing
release requirements and historical failures remain in force.

Final Verdict: **PASS this bounded explicit Chat-to-Research handoff at 9.6/10**,
zero confirmed unresolved critical defects. It is reviewable in draft PR #4 and may
advance within the documented development scope. It is not completion of all Module B
capabilities and is not blanket qualification of future Module C memory or release.
