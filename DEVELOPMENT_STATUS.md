# Development status

Updated: 2026-09-22. Current phase: **2 — independent follow-up QA 8.6/10; phase exit FAIL**.
Phase 0 passed at 9.4/10 and Phase 1 passed at 9.0/10. Phase 2 exceeds the numerical
threshold, but required performance and physical validation remain incomplete.
Phase 3 has not begun.

## Completed implementation

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
Desktop composition. Production remains package-free. The framework's Windows Forms
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

## Test status and evidence

- Latest executed full Release verification: **193 passed, 0 failed, 0 skipped**, 11
  architecture checks, composed smoke, zero compiler warnings/errors. This suite
  precedes the final cadence-only change; the later strict build and native run
  execute that change. Preserved TRX:
  artifacts/qa/desktop-ui-20260922-175553-c176ac05/release-193-before-cadence-only-change.trx.
- Latest complete native UI run: **12/12 process/control checks**, **43 appearance,
  state and placement assertions**, **18 tray/recovery/shortcut assertions**, no skipped
  shortcut cases or adapter errors. Evidence:
  artifacts/qa/desktop-ui-20260922-175553-c176ac05/profile. Hidden/minimized settings
  reopening and timed blink transitions at the final ten-second cadence pass. The
  latest Release build has zero warnings/errors. This native run observed one display at
  120 DPI (125%); next-display coverage is therefore a single-display no-op.
- Prior expanded UI attempt hit its 15-second deadline after all 33 appearance checks;
  it had no completed integration artifact. Retained at desktop-ui-20260922-164249-5578a5c9.
  The supervisor first increased its deadline to 30 seconds and now uses 40 seconds
  for the longer blink checks; it records stage timings.
  The subsequent 33-assertion run completed its UI stages in 4.790 s and 1.984 s.
- The later desktop-ui-20260922-174634-ef0f4899 attempt ran both application processes
  successfully with 43 appearance assertions, but its supervisor failed on the stale
  expected count of 37. That failure remains retained; the corrected 12/12 run above
  validates the updated supervisor and settings recovery.
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
- The final ten-second-blink run, performance-20260922-175657-e7f2743d, also **failed**:
  visible CPU was **1.041400%**; reduced-motion **0.520719%** and hidden **0.390566%**
  passed. All memory intervals passed (62,914,560 / 64,225,280 / 66,539,520 peak private
  bytes), with 6/0/0 completed blinks and zero owned animation clocks. The supervisor
  correctly exited 1 and retained budget-evaluation.json. The cadence is frozen;
  neither another unchanged repeat nor a relaxed threshold is used to claim a pass.
- Supported Computer Use window discovery failed because its native helper pipe was
  unavailable, including after reinitialization. No actual mouse/keyboard input was
  attempted this turn. Programmatic HWND checks do not replace this evidence.
- Production idle rendering now waits ten seconds between 120 ms blinks with no animation clocks.
  Native checks prove eyelid transitions, completed-blink counting, interrupted-blink
  exclusion and resource release. Active expressions own at most two clocks, capped
  at 20 FPS, explicitly removed on Stop; closing disposes the renderer. The ten-second
  cadence passes native regression but fails the visible-idle CPU budget. The cadence
  decision preceded its single clean measurement; the harness and limits are unchanged.

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

The latest independent score is 8.6/10 (86/100, exact arithmetic), exceeding 8.5.
Required phase-exit evidence still fails: visible-idle CPU exceeds its limit and the
physical display/input/accessibility matrix remains incomplete. The earlier 8.0/10
failed checkpoint is preserved. Phase 1 remains the last completed passing phase.
Current evidence and ranked work are in docs/qa/PHASE_2_FOLLOWUP_REVIEW.md; no advancement
is permitted. Physical scenarios are enumerated in docs/qa/PHASE_2_MANUAL_MATRIX.md.

## Pending work / known limitations

1. Profile the remaining visible-idle CPU cost, make an evidence-backed change and
   measure against unchanged limits; preserve all failed results and harness overhead.
2. Execute actual mouse/keyboard/tray recovery and accessibility scenarios, and the
   physical mixed-DPI/negative-coordinate/disconnected-monitor matrix.
3. Validate actual Windows sign-in behavior with a disposable user; the isolated
   registry adapter's 13 native cases now pass.
4. Re-review after the required fixes and evidence are complete. A numeric score above
   8.5 alone does not finalize the phase or permit Phase 3.

Arrows are constrained to the current monitor; dragging or Ctrl+Shift+M can move
between monitors. Actual multi-monitor behavior remains unverified on this one-display setup.
A companion larger than a work area anchors to its origin; tray settings/Exit remain
recovery routes. General occlusion detection is not implemented; visible occluded
avatars can keep animating. Local storage is not encrypted and same-user malware
remains outside the protection boundary. Asset packs, signing, installer/updater,
release license and all AI/conversation/voice/memory/tool features remain later work.

## Source checkpoint and next objective

Phase 1 is committed at 532879c and identified by refs/checkpoints/phase-1; Phase 0's
checkpoint is retained. This Phase 2 source snapshot is **IN PROGRESS** and preserves
the implementation, independent reviews and verification instructions. It does not
finalize Phase 2; no Phase 2 completion ref or remote CI run is claimed.
The next objective remains visible-idle
CPU diagnosis and required physical validation; Phase 3 remains deferred.
