# Development status

Updated: 2026-09-22. **Compact surface increment passed independent QA at 9.1/10;
local chat increment passed at 9.0/10; full Phase 3 remains incomplete.**
Phase 0 passed at 9.4/10 and Phase 1 passed at 9.0/10. Phase 2's final resource
confirmation remains **FAIL**, and release is not approved. Phase 3 now implements
pretrained local inference and a bounded in-memory plain-text chat window.
The recorded validation sequence moves unavailable physical/input/sign-in qualification
to mandatory Phase 11/12 pre-release gates without marking any missing check passed.
The separate, explicitly post-result decision in docs/qa/PHASE_2_RESOURCE_SEQUENCE.md
places unresolved CPU qualification in the user's Phase 12 optimization work. Performance
remains 7/10 with zero budget-criterion points; neither limits nor measurements changed.

## Completed implementation

- Replaced the large companion card with the user-requested character-only resting
  surface and compact hover/keyboard Chat, Settings and More controls. Default
  character size is 112×88 DIPs; stable 136×132-DIP window bounds prevent hover
  displacement. Mouse focus does not latch controls; Escape dismisses them.
- Compact UI regression: 224/224 tests, 11/11 architecture checks, composed WPF
  smoke and zero warnings/errors; 20/20 native process controls, including 51 new
  reviewer-authored compact assertions. Native input sampled Chat/Settings/More,
  pointer exit after mouse focus, Tab/Shift+Tab/Enter/Escape at saved 75% scale on
  the single 125%-DPI display. Full input/accessibility/display qualification remains.
  See [surface review](docs/qa/COMPACT_AVATAR_REVIEW.md) and
  [build evidence](docs/qa/COMPACT_AVATAR_BUILD_EVIDENCE.md).

- Implemented the user's final model choice: pretrained Qwen3-0.6B Q8_0, fully local
  llama.cpp CPU inference, no API key or cloud fallback. The briefly explored
  from-scratch training draft is paused and excluded from the application.
- Added hash-pinned setup, lazy owned worker, loopback socket-owner verification,
  ephemeral internal authentication, offline launch, bounded process lifetime and
  a 2 GiB worker committed-memory job limit. No hosted provider SDK is involved.
- Added chat via menu/Ctrl+Shift+C with plain-text streaming, Stop, explicit edit/retry,
  bounded completed-turn context, close/shutdown cancellation and model unloading.
  Generation owns a Thinking expression lease; Sleep cancels the current turn.
- Strict verification passes 224 tests, 11 architecture checks, composed WPF smoke,
  and zero warnings/errors. Twelve tests are reviewer-authored. Native checks pass
  19 controls and 54 chat assertions, alongside all preceding desktop checks.
  The real-model protocol passes in `artifacts/qa/local-model-20260922-195744-9143334a`:
  exact instruction, two-turn recall, three tips, cancellation/reuse, worker disposal
  and cancellation after a worker actually spawned. All public fixture outputs retained.
  Startup observed 4.8037317 s; entire check 14.9589429 s; sampled maximum private bytes
  360,083,456. This excludes mapped-model physical memory and is not a total RAM claim.

- Preserved the committed Phase 1 foundation: validated atomic settings/backups,
  content-free bounded logs, same-user activation and composed application lifetime.
- Added scale/opacity/reduced-motion preferences and closed schema-1 migration.
- Added default vector renderer behind IAvatarRenderer, eight semantic expressions,
  activity ownership/priority, Sleep/Wake and explicitly labeled four-second previews.
- Added mouse/keyboard positioning, edge snapping, work-area constraints and bounded
  atomic monitor-relative placement separate from explicit preference saves.
- Added deterministic next-display movement through Ctrl+Shift+M and a menu action;
  physical cross-display/DPI verification remains pending.
- Added tray Show/Hide/Settings/Sleep/Exit, configurable recovery shortcuts with
  conflict retention, and session click-through cleared on recovery/activation.
- Added explicit per-user startup preference; no automatic registration on load.
- Added independent geometry/state oracles, storage/failure regressions, native
  recovery/placement smoke and a repeatable resource measurement harness.

## Architecture decisions

C#/.NET 10 WPF; Core values and pure math, Application ports, Infrastructure adapters,
Desktop composition. Managed production assemblies have no NuGet dependencies; the
reviewed native llama.cpp worker and Qwen weights are explicit Phase 3 dependencies.
The framework's Windows Forms
NotifyIcon supplies the tray; WPF still owns the companion and settings. Typed logs
cannot accept conversation text. No AI credentials, external application network calls,
voice capture, memory or tools exist yet. Placement is a separate 4 KiB versioned file;
startup registration uses a validated quoted executable command without arguments.

## Defects found and fixed

Independent QA found and implementation fixed the missing movement automation peer,
minimized placement, a move immediately followed by Hide losing the pending save,
shortcut errors disappearing during layout, and initialization/shutdown races. Moves
now capture an immutable normal-state snapshot before debounce and flush it when hidden.
Recovery also refreshes the settings click-through checkbox. Shortcut success now
clears only its own error, preserving unrelated storage messages. Existing settings
windows now show and restore when reopened; hidden/minimized recovery passes native checks.
Measurement input is locked and labeled because the first performance run was changed
interactively. Direct native process counters reduce measurement overhead; the
remaining CPU cost is still under investigation.
Settings Save now captures native preferences with the save request and suppresses
later UI, shortcut, click-through and startup changes once shutdown begins. A pending
save cannot re-enable the editor or reapply the companion after native resources are
disposed. Native controlled-completion cases cover committed, cancelled and failed saves.
Two subsequent readiness fixes now pass final regression: input is disabled while
initial placement loads and restores, with prior disabled/closed/stopped state preserved;
Hide and click-through require a successfully registered summon shortcut rather than
requested tray visibility. Settings refresh click-through availability after shortcut changes.

## Test status and evidence

- Historical Phase 2 Release verification: **196 passed, 0 failed, 0 skipped**, 11
  architecture checks, composed smoke, zero compiler warnings/errors. The final-source
  verification covers both readiness fixes; its retained TRX is
  artifacts/qa/desktop-ui-20260922-185538-0c91126a/release-196-before-final-build-confirmation.trx.
  Three new independent
  placement tests cover cancelled waiters, cancellation after atomic commit and retry
  after writer failure. Preserved TRX:
  artifacts/qa/desktop-ui-20260922-183217-1d198cb8/release-196-before-observer-abba.trx.
  The earlier 193-case artifact and its cadence-only scope remain retained.
- Latest complete native UI run: **18/18 process/control checks**, **43 appearance,
  state and placement assertions**, **24 tray/recovery/shortcut assertions**,
  **29 settings lifecycle assertions**, and **49 placement-initialization assertions**,
  with no skips or reported failures. Evidence:
  artifacts/qa/desktop-ui-20260922-185538-0c91126a/profile.
  Settings recovery, shutdown during Save, input freeze during placement and confirmed
  shortcut recovery pass. The
  latest Release build has zero warnings/errors. This native run observed one display at
  120 DPI (125%); next-display coverage is therefore a single-display no-op.
- The new readiness fixture initially failed compilation and was corrected. Strict
  Release verification now passes. Native attempt desktop-ui-20260922-185315-2646a9cb
  completed 43 appearance, 24 integration and 29 settings lifecycle checks, then failed
  placement assertion 42: its comparison included window size after warning text resized
  the window. The fixture now compares Left/Top; production code was unchanged by that
  correction. The later 18/43/24/29/49 native run above passes; the failed attempt is
  retained and is not counted as a complete run.
- Prior expanded UI attempt hit its 15-second deadline after all 33 appearance checks;
  it had no completed integration artifact. Retained at desktop-ui-20260922-164249-5578a5c9.
  The supervisor first increased its deadline to 30 seconds and now uses 40 seconds
  for the longer blink checks; it records stage timings.
  The subsequent 33-assertion run completed its UI stages in 4.790 s and 1.984 s.
- The later desktop-ui-20260922-174634-ef0f4899 attempt ran both application processes
  successfully with 43 appearance assertions, but its supervisor failed on the stale
  expected count of 37. That failure remains retained; subsequent corrected 12/12
  runs validated the supervisor and settings recovery before the current 17/17 expansion.
- First performance run completed but is **not a controlled idle baseline**: settings
  were saved during measurement. Keep performance-20260922-164711-eccda553 as rejected
  evidence. The already-running old supervisor returned 0; that does not override the
  contamination finding. New checks reject profiles containing saved settings.
- Normal-user verification resumed successfully, including **13/13 isolated-registry
  cases**, closing the earlier approval-quota execution gap. Actual sign-in is untested.
- Two earlier controlled baselines passed the private-memory limit and failed
  CPU in all three modes. The second, performance-20260922-172825-fc26bc57, measured
  **5.260228% visible, 2.708053% reduced-motion and 2.707961% hidden** of one logical
  processor. Peak private bytes were 65,101,824 / 65,290,240 / 62,021,632. Limits remain
  strictly below 1% of one processor and 150,000,000 private bytes; no calibration
  overhead is subtracted. Earlier controlled evidence and raw failures are retained.
- The six-second-blink run, performance-20260922-175003-37870412, still **failed**:
  visible CPU was **1.093335%**; reduced-motion **0.494627%** and hidden **0.416623%**
  passed. All memory intervals passed (63,504,384 / 65,265,664 / 63,492,096 peak private
  bytes). It recorded 10/0/0 completed blinks and zero owned animation clocks. The
  combined renderer/lifetime changes improved reduced/hidden CPU; no single cause is
  proven. This failed result is retained without overhead subtraction.
- The pre-experiment ten-second-blink run, performance-20260922-175657-e7f2743d, **failed**:
  visible CPU was **1.041400%**; reduced-motion **0.520719%** and hidden **0.390566%**
  passed. All memory intervals passed (62,914,560 / 64,225,280 / 66,539,520 peak private
  bytes), with 6/0/0 completed blinks and zero owned animation clocks. The supervisor
  correctly exited 1 and retained budget-evaluation.json. The cadence is frozen;
  neither another unchanged repeat nor a relaxed threshold is used to claim a pass.
- Supported Computer Use window discovery failed because its native helper pipe was
  unavailable (OS error 2), including after updated skill guidance and a fresh reset.
  No actual mouse/keyboard input was
  attempted this turn. Programmatic HWND checks do not replace this evidence.
- Production idle rendering now waits ten seconds between 120 ms blinks with no animation clocks.
  Native checks prove eyelid transitions, completed-blink counting, interrupted-blink
  exclusion and resource release. Active expressions own at most two clocks, capped
  at 20 FPS, explicitly removed on Stop; closing disposes the renderer. The ten-second
  cadence passes native regression but fails the visible-idle CPU budget. The cadence
  decision preceded its single clean measurement; the harness and limits are unchanged.
- The predeclared **A/B/B/A** observer comparison completed under
  docs/qa/PHASE_2_OBSERVER_PROTOCOL.md. A uses the default `threads` observer; B uses
  `process`, omitting only two boundary thread snapshots per interval. The production
  cadence, warmup, native counters, calibration and limits stay fixed; no overhead is
  subtracted. Five binary hashes and the protocol/supervisor hashes are frozen. Every
  valid pass/failure is retained, technical failures stop the sequence, and both B
  replicates had to pass every interval. All four runs were valid and passed all CPU
  and memory intervals. A visible CPU values were **0.885254% / 0.989278%**; B values
  were **0.755076% / 0.703079%**. This is evidence for that earlier frozen binary,
  not an isolated product optimization or statistical confidence claim. All results remain in
  artifacts/qa/observer-d217a835e8bc4ec9b6f27b98637024fd/comparison.json.
- The separately declared docs/qa/PHASE_2_FINAL_BUILD_PROTOCOL.md requires exactly two
  process-only B runs after the readiness fixes, both passing every unchanged limit.
  Its frozen SHA256 is 217CD856232210092F15773DE19062F6FB12A35B0185393C64EB771FB49CE174.
  The completed final-build sequence is retained at
  artifacts/qa/final-build-1ca3041a363849aabc6b2c4e2170ce6f/comparison.json.
  **Aggregate result: FAIL.** Both runs are valid, but both were required to pass.
  Run 1, performance-20260922-185649-ab4dbf46, measured **1.978694% visible,
  1.015400% reduced-motion and 0.364563% hidden** one-core CPU; the first two fail.
  Run 2, performance-20260922-190004-0f75069d, passed at **0.755208% / 0.598859% /
  0.364527%**. All memory intervals pass; peak private bytes were respectively
  64,974,848 / 65,224,704 / 62,337,024 and 63,119,360 / 64,102,400 / 63,520,768.
  Each run records 6/0/0 completed blinks and zero owned clocks. The first failure,
  exact binary identity and unchanged acceptance rule are preserved; no replacement
  run or overhead subtraction is used. Development approval does not change this failure.

Historical Phase 1 evidence remains: Release/Debug 38 tests each, 11 architecture
checks, 9 negative controls and 9 UI/process checks, plus attributed actual input.
No Phase 2 Debug, physical mixed-DPI, screen-reader, actual sign-in, remote CI,
frame-time percentile or long-running memory certification is claimed.

## QA history

| Checkpoint | Reviewer | Score | Result |
|---|---|---|---|
| Phase 0 final | Independent phase0_qa | 9.4/10 | PASS |
| Phase 1 final | Independent phase0_qa | 9.0/10 | PASS |
| Phase 2 first appearance increment | Independent phase2_review | Unscored historical checkpoint | INCOMPLETE |
| Phase 2 desktop integration | Independent phase0_qa | 8.0/10 | FAIL; docs/qa/PHASE_2_REVIEW.md |
| Phase 2 resumed validation | Independent phase0_qa | 8.6/10 | FAIL phase exit; docs/qa/PHASE_2_FOLLOWUP_REVIEW.md |
| Phase 2 resource/lifecycle review | Independent phase0_qa | 8.7/10 | PASS development; resource confirmation FAIL; docs/qa/PHASE_2_RESOURCE_REVIEW.md |
| Phase 3 local pretrained chat increment | Independent local_model_qa | 9.0/10 | PASS bounded increment; full Phase 3 incomplete; docs/qa/PHASE_3_LOCAL_INCREMENT_REVIEW.md |

The latest Phase 2 independent score is **8.7/10 (87/100)**, strictly greater than 8.5, and
Phase 2 is complete as a development milestone. Historical 8.0 and 8.6 failed exits
remain unchanged. The later observer experiment passed on its own binary, while
final-build resource confirmation failed. The explicitly post-result sequencing
decision moves CPU qualification to mandatory Phase 12 release work; it is not a
resource pass. Performance remains **7/10**, including **zero** for the budget criterion.
The earlier physical/input/sign-in sequence decision also retains every unexecuted
manual-matrix case as mandatory before release. See docs/qa/PHASE_2_RESOURCE_REVIEW.md.

## Pending work / known limitations

1. The local chat increment passed at **9.0/10 (90/100)**. Continue Phase 3 history,
   branches/search/private-session behavior, rich Markdown/code/LaTeX rendering and
   tokenizer-aware context admission. The model currently rejects oversized token
   contexts visibly; UTF-8 bounds are not exact token counts. The 0.6B model has only
   narrow fixture evidence, not general assistant or scientific accuracy qualification.
2. Before release, diagnose CPU cost and complete a separately predeclared Phase 12
   repeat protocol controlling actual presentation/occlusion, warmup and observer cost.
   Limits remain strictly below 1% of one logical processor and 150,000,000 private bytes;
   retain every failure and exact binary identity without overhead subtraction.
3. Before release in Phases 11/12, execute physical display/input/accessibility and
   actual Windows sign-in qualification with suitable hardware and a disposable user.

Arrows are constrained to the current monitor; dragging or Ctrl+Shift+M can move
between monitors. Actual multi-monitor behavior remains unverified on this one-display setup.
A companion larger than a work area anchors to its origin; tray settings/Exit remain
recovery routes. General occlusion detection is not implemented; visible occluded
avatars can keep animating. Local storage is not encrypted and same-user malware
remains outside the protection boundary. Asset packs, signing, installer/updater,
release license and persistent conversation/voice/memory/tool features remain later work.

## Source checkpoint and next objective

Phase 1 is committed at 532879c and identified by refs/checkpoints/phase-1; Phase 0's
checkpoint is retained. Commit **6306ec9** preserves the earlier **IN PROGRESS** Phase 2
source and 8.6 failed-exit review. The current verified development milestone uses the
local source checkpoint refs/checkpoints/phase-2. No remote CI run or release is claimed. The
local pretrained chat increment has its own source checkpoint at
refs/checkpoints/phase-3-local-chat-increment. The next objective is Phase 3's
persistence, branching, search and richer rendering. No hosted provider is planned.
