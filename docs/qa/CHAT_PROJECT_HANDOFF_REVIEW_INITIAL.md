# QA REVIEW — initial explicit Chat-to-Research handoff checkpoint

Phase: bounded Module B handoff, independent initial development review, 2026-10-19.

Overall Score: **8.3 / 10**

Result: **FAIL / INCOMPLETE**. The supported Windows verification and real-window
handoff checks have not run at this checkpoint. Compilation and passing portable
tests do not establish a working native workflow. No confirmed unresolved critical
production defect was found in the inspected implementation; the missing native
evidence is a qualification blocker.

Source identity: uncommitted handoff implementation over
`7ce7f639c999c79631e786a7365578773e1da530` on
`feature/chat-project-handoff`, before the first PR #4 source checkpoint. This
report does not review a later source commit or inherit PR #3's prior score.
Criteria were frozen before implementation in
[CHAT_PROJECT_HANDOFF_PROTOCOL.md](CHAT_PROJECT_HANDOFF_PROTOCOL.md).

## Evidence

The independent agent ran these commands in the managed Linux environment after
the implementation agent's serialized strict build:

```text
dotnet test tests/Companion.Tests -c Release --no-build --no-restore
  -p:EnableWindowsTargeting=true
  --filter FullyQualifiedName~IndependentChatProjectHandoffTests
  --logger trx;LogFileName=independent-handoff.trx
  --results-directory artifacts/qa/chat-project-handoff/independent
exit 0: 32 total, 32 executed/passed, 0 failed, 0 skipped

dotnet run --project tests/Companion.ArchitectureChecks -c Release --no-build --
  /workspace/mu-desktop-assistant
exit 0: 11/11 architecture checks

git diff --check
exit 0 (line-ending conversion notices only)
```

The actual TRX is
`artifacts/qa/chat-project-handoff/independent/independent-handoff.trx` (ignored
artifact, not source). All 32 case outcomes were parsed independently, including
the reentrant admission test. Tests exercise actual project catalog files and the
production `ResearchLabWorkspace`, handoff ticket, planner, and missing-runtime
`LocalMlLabService`. Controlled model/service ports are declared fixtures. The
missing-runtime test proves transfer admits zero jobs, then an explicitly invoked
production action fails with `missing_runtime`; it does not prove a working
installed runtime or scientific inference.

Manual source review covered `ResearchRequestHandoff.cs`,
`ResearchLabWorkspace.cs`, `MlConversationPlanner.cs`, `ChatWindow.Research.cs`,
the Chat history/shutdown integration, `ResearchHandoffWindow.xaml(.cs)`,
`App.MachineLearning.cs`, App shutdown, and Lab constructor/assistant code. The
new App native smoke was inspected but cannot execute on Linux. Root-reported
strict build success is supporting evidence, distinct from the commands this
reviewer ran. The root's initial wider Linux selection has two existing Windows
path-fixture failures; neither is represented as a passing native result here.

## Criterion scores

Each row follows its category's five frozen criteria in order. Values are raw
0–2 scores; source inspection is labeled separately from executed behavior.

| Category | C1 | C2 | C3 | C4 | C5 | Total | Evidence and limits |
|---|---:|---:|---:|---:|---:|---:|---|
| Code Correctness | 2 | 2 | 2 | 2 | 2 | 10 | Exact-request/catalog and identity tests pass; source shows explicit preview, deferred Lab request population and synchronous invalidation. Native workflow remains unexecuted. |
| Architecture | 2 | 2 | 2 | 2 | 2 | 10 | 11 architecture checks; existing store/workspace/services reused; typed disposable lease ticket and shared admission lock; no dependency or persistence additions. |
| Mathematical Correctness | 2 | 2 | 2 | 2 | 2 | 10 | UTF-16 and 2000-unit boundaries pass; 4 action types reject changed path/key/start/rows; immutable validated limit snapshot inspected; no scientific-accuracy claim; exact QA arithmetic below. |
| Testing | 2 | 1 | 2 | 1 | 0 | 6 | Independent portable behavior passes; zero-worker native Loaded and real UI are still source-only; malformed/request/runtime failures covered; native shutdown and mandatory Windows gates missing. |
| Reliability | 2 | 2 | 2 | 2 | 1 | 9 | Lifecycle/retirement/busy/duplicate/cancel/failed-callback/shutdown application tests pass; exact window identity and captured dialog cancellation inspected; runtime-native recovery still unexecuted. |
| Security | 2 | 2 | 2 | 2 | 2 | 10 | Temporary boundary, no context payload, captured-dataset enforcement, no worker authority, no catalog/content persistence tests and production source inspection. |
| Performance | 2 | 2 | 2 | 2 | 1 | 9 | Bounded disposable request, no transfer work admission, lazy model constructor inspected; limits unchanged; repeated native lifecycle/physical resource measurement unavailable. |
| Maintainability | 2 | 2 | 2 | 2 | 0 | 8 | Cohesive APIs, fail-closed errors, meaningful independent tests; user-facing documentation updates have not yet landed. |
| UX/UI | 1 | 1 | 1 | 1 | 0 | 4 | Labels, exact preview, persistence explanation, default Cancel focus, and separate proposal/result UI inspected; actual controls/layout/keyboard behavior are unexecuted. |
| Requirement Compliance | 2 | 1 | 2 | 2 | 0 | 7 | Requested branch/base and narrow scope inspected; native complete workflow, PR delivery/final evidence and separate memory follow-up are pending. |

Arithmetic: `10 + 10 + 10 + 6 + 9 + 10 + 9 + 8 + 4 + 7 = 83`.
`83 / 10 = 8.3`; `8.3 > 8.5` is false. The phase remains incomplete.
There is no runtime mathematical feature in this increment; the mathematical
category grants no numerical/scientific or pretrained-model qualification.

## TOP 5 RANKED FIXES / IMPROVEMENTS

1. **Run the unchanged mandatory Windows gates and inspect native evidence.**
   Why: Linux cannot establish WPF behavior or Win32 process boundaries. Fix:
   create the reviewable source checkpoint/draft PR, run both verification
   scripts on supported Windows infrastructure, retain failures and inspect named
   native checkpoints rather than just CI status.
2. **Invoke native Cancel through actual button behavior.** Why: merely raising
   `Button.ClickEvent` does not necessarily exercise `Button.OnClick`/`IsCancel`
   dialog behavior when no explicit handler is present. Fix: drive automation
   invocation/keyboard cancellation, or add an explicit production Cancel handler,
   and prove the real modal returns without timeout.
3. **Strengthen stale-input native assertions.** Why: repeated aggregate markers
   can pass after an earlier proposal already became unavailable. Fix: prove a
   fresh runnable proposal before each mutation, then assert exact dataset
   path/key/start/row count and captured limits, plus mode change invalidation.
4. **Add late-closed-view and runtime recovery evidence.** Why: portable ticket
   tests do not alone establish dispatcher/window behavior. Fix: exercise closed
   destinations/Chat, noncooperative model callbacks, and missing model/runtime
   outcomes through real native controls without treating fixtures as inference.
5. **Complete documentation and retain qualification boundaries.** Why: normal
   deferred loading, explicit-draft scope and project-selection-on-cancel behavior
   must be understandable. Fix: update usage/contracts/security/status/testing
   and changelog; retain prior receipts and disclose physical keyboard/a11y,
   installed-model inference, resource/stress and release qualification gaps.

## Regression Risks

Normal Lab initialization remains eager; handoff-created Labs use a deliberately
deferred path. Tests must guard both paths. Proposal cancellation/versioning now
applies to existing Lab flows, so run prior structured-planner/project and native
smoke regressions. App field ownership and posted dispatcher cancellation must
continue to capture exact old dialogs, never mutable later-dialog fields.

## Tests Required

Supported Windows `scripts/verify.ps1` and `scripts/verify-negative.ps1`, the
actual App handoff smoke, exact mutation baselines, existing project/A regression
tests, relevant production Python/project-isolation tests. Physical accessibility,
qualified installed pretrained inference and existing resource targets remain
separate requirements; absent evidence is not a pass.

Final Verdict: retain this initial incomplete review and re-review the exact
source checkpoint after genuine Windows/native evidence and documentation exist.

## Addendum — actual Windows failure on `6954729`

Both first Windows runs of source
`69547291c696d1a0422adc47bb0f7c2dc6e10d1e` failed the required positive gate:
[push 39305498979](https://github.com/calebaiken/mu-desktop-assistant/actions/runs/39305498979)
and [PR 39305528774](https://github.com/calebaiken/mu-desktop-assistant/actions/runs/39305528774).
Bounded annotations from the push job `118241365050` report 525 total tests,
524 executed/passed, zero failures and one expected unsupported-platform
`NotExecuted` case. The four specifically named native process/file tests Passed;
the prior research-project smoke completed all 21 checkpoints. No handoff receipt
was emitted. The positive job failed after the C# suite, and the dependent negative
gate did not run. This is a failed overall verification, not a passing native
handoff result. The PR run independently produced the same classification.

The reviewer inspected the actual annotation file
`artifacts/qa/chat-project-handoff/windows-initial-push-annotations.json` and the
source. A likely harness prerequisite is that ordinary `--smoke` only measures
the companion's content, while the new harness sets `ChatWindow.Owner` to that
companion before creating its native HWND. WPF requires the owner to have a native
source. This happens before the harness's original `try/finally`, which explains
why a constructor/owner failure could leave no receipt. This remains a source-based
hypothesis at this addendum, not a confirmed attribution from native execution.

The implementation agent is repairing the harness to create the actual owner HWND,
wrap setup in receipt-producing exception handling, and publish bounded fixed-label
progress at entry/each checkpoint so an externally killed smoke remains diagnosable.
No request/path/error payloads, 15-second threshold changes or assertion weakening
are authorized by this review. A fresh exact-source Windows run is required.
The original **83/100 = 8.3 FAIL / INCOMPLETE** verdict remains unchanged.

## Addendum — native progress and failure on `40d4021`

The next source checkpoint,
`40d4021691144321c8667021c2f406fb98b6545b`, creates the companion's actual HWND
before assigning it as Chat's owner, guards partial XAML initialization, and wraps
the complete smoke setup in bounded fixed-label receipt handling. It preserves
the original 15-second gate and production assertions.

Actual Windows push
[39306220091](https://github.com/calebaiken/mu-desktop-assistant/actions/runs/39306220091),
job `118243878059`, again reports 524 passed / 0 failed / 1 expected NotExecuted of
525 C# tests, the four named native tests Passed, and all 21 prior project smoke
checkpoints. The handoff receipt now records **14 successful distinct native
checkpoints**, including native owner creation, Temporary refusal, real minimum
Chat/preview bounds, exact preview/confirmation, zero worker/artifact transfer,
same-project dataset retention, Cancel, closed destination, and dismissed transfer
without cross-project selection. This verifies those observed boundaries on this
checkpoint; it does not qualify the unfinished later lifecycle tests.

The receipt reports `completed: false`, no `failedCheck` and no `failureType`.
The required positive gate still failed and the negative gate was skipped. The
next source operation closes Chat while its real transfer preview is modal.
Current evidence cannot distinguish a close/dispatcher failure from accumulated
smoke duration reaching the 15-second process limit. No attribution or passing
handoff verdict is inferred. The actual bounded annotation is retained locally at
`artifacts/qa/chat-project-handoff/windows-owner-push-annotations.json`.

The reviewer requested precise fixed start/end markers and bounded numeric elapsed
time, plus private-data-safe dispatcher failure type, before further changes. A
production cancellation callback must operate only on its captured live dialogs;
a callback already queued after close must not act on a later view. The next
implementation adds those narrow diagnostics/guards and requires another actual
Windows run. The independent verdict remains **FAIL / INCOMPLETE**, regardless of
the successful earlier native checkpoints and C# suite.

## Addendum — owner-close stall localized on `737d420`

Source `737d420a00e854d026e5489d247ccfc4958c9032` adds captured-visible-dialog
cancellation guards, fixed before/after close markers, private-data-safe dispatcher
failure type, and a bounded numeric smoke duration. Windows push
[39307018772](https://github.com/calebaiken/mu-desktop-assistant/actions/runs/39307018772),
job `118246618650`, still fails the positive gate after 524 passed / zero failed /
one expected NotExecuted of 525 C# tests and the prior 21 native project checkpoints.
The negative gate remains unrun.

The independent reviewer fetched this job's bounded check annotations directly.
The handoff has **16 distinct passed markers**, including both `closing Chat from
real preview started` and `closing Chat from real preview returned`. The last
receipt has elapsed time **1373 ms**, `completed: false` and no recorded failure
type/checkpoint. This rules out treating accumulated handoff runtime as established
cause: the observed stall follows a returned `Chat.Close()` while awaiting the real
preview's modal workflow. The native owner-close behavior is an actual unresolved
lifecycle blocker for this checkpoint, even though earlier transfer boundaries pass.

The next narrow repair must cancel/unwind the captured modal before owner teardown,
avoid acting on a closed/replaced dialog, check cancellation before opening a modal,
and retain the local watchdog until the awaited transfer actually returns. The
reviewer specifically recommends Chat `Closing` cancellation (with idempotent
`Closed` fallback) and synchronous UI cancellation when already on the dispatcher.
Neither the three-second local watchdog nor 15-second process gate may be relaxed.
A fresh supported Windows result is required; this addendum grants no passing score.
