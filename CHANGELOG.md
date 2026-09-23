# Changelog

## Unreleased — Phase 3 local chat increment (2026-09-22, independent QA 9.0/10 PASS)

- Adopted the user's pretrained-local-model choice, without API keys or cloud inference.
- Added pinned official Qwen3-0.6B weights and llama.cpp CPU setup, hash checks, lazy
  owned worker, offline loopback transport and process cleanup/memory limits.
- Added plain-text streaming chat, Stop, bounded in-memory context, edit/retry,
  close cancellation and Thinking/Sleep integration. Full Phase 3 remains incomplete.
- Passed strict 224-test regression, 11 architecture checks, 19 native process controls,
  54 chat assertions and the predeclared real model integration checks. All narrow
  prompt outcomes are retained separately from transport success; no general assistant
  capability or release approval is claimed.

## Unreleased — Phase 2 desktop interactions (2026-09-22, development complete; release blocked)

- Added a semantic renderer contract, eight default vector expressions, activity
  precedence/ownership, bounded motion and explicit temporary expression previews.
- Added size/opacity controls, reduced-motion/high-contrast behavior, mouse/keyboard
  movement, snapping and separate atomic monitor-relative placement storage.
- Added tray Show/Hide/Settings/Sleep/Exit, configurable recovery shortcuts with
  conflict retention, and session click-through that clears on recovery.
- Added explicit per-user startup preference; no automatic registry mutation on load.
- Added independent geometry/state oracles, native adapter/placement regression
  checks and a repeatable three-interval resource measurement harness.
- Fixed QA findings for move-then-hide persistence and disappearing shortcut errors.
- Added deterministic next-display movement through Ctrl+Shift+M and a menu action;
  physical cross-display behavior remains pending on the observed one-display setup.
- Scoped shortcut errors so successful registration preserves unrelated storage errors.
  Added hidden/minimized settings recovery, now verified in native checks.
- Fixed Settings Save continuations during shutdown: capture the requested native
  preferences before awaiting persistence, suppress late UI/native actions and leave
  controls disabled. Native cases cover committed, cancelled and failed saves.
- Resumed normal-user verification: 196/196 Release tests, 11 architecture checks,
  13/13 isolated-registry cases, and native 17/17 controls with 43 appearance,
  18 integration and 29 settings lifecycle assertions. Added three independent
  placement cancellation/atomic-commit/retry tests. This covers the build before
  the observer experiment and later readiness fixes; the 193-case TRX remains archived. Actual sign-in
  and physical input/accessibility remain pending.
- Replaced repeated framework memory sampling with direct native counters. Two clean
  baselines of the earlier renderer pass memory and fail CPU.
  The earlier contaminated measurement remains rejected and preserved.
- Replaced continuous idle motion with a 120 ms blink after a ten-second wait and no
  idle animation clocks. Active expressions own at most two controlled clocks capped
  at 20 FPS; Stop removes resources and window close disposes the renderer. Timed blink
  and lifetime native checks pass for the current ten-second cadence. The earlier
  six-second run fails visible CPU at 1.093335%, while reduced/hidden CPU and all memory
  modes pass. The ten-second cadence was selected before one planned remeasurement;
  its final baseline still fails visible CPU at 1.041400%, with reduced/hidden CPU and
  memory passing. The supervisor exits 1 and retains the result. Cadence and limits
  are frozen; all earlier failures are retained without overhead subtraction.
- Historical independent follow-up: **8.6/10; phase exit FAIL** due to visible-idle CPU and required
  physical validation. The earlier 8.0 failed checkpoint remains preserved.
  That historical checkpoint remained incomplete; its verdict is preserved unchanged.
- Retained in-progress source checkpoint 6306ec9. A later predeclared A/B/B/A observer
  experiment keeps `threads` as the legacy default and uses `process` to omit only
  two boundary thread snapshots per interval. Cadence, warmup, calibration and budgets
  are unchanged, without overhead subtraction; all valid failures remain retained.
  All four runs pass CPU and memory on that original frozen binary. A visible CPU is
  0.885254% / 0.989278%; B is 0.755076% / 0.703079%. No isolated product optimization
  or statistical confidence claim follows from this small experiment.
- Fixed input racing initial placement load and restore; preserve prior disabled state
  and skip stale restore/reenable after hide, minimize, close or stop. Require a
  successfully registered summon shortcut for Hide/click-through; refresh settings
  availability after shortcut changes. Final-source build and 196-test verification
  pass, with native 18 controls, 43 appearance, 24 integration, 29 settings lifecycle
  and 49 placement-initialization assertions. Corrected a fixture position assertion
  that mistakenly compared dimensions changed by warning text; its failed artifact is retained.
- Declared exactly two final-build B resource runs after these readiness fixes;
  both complete, but aggregate confirmation **fails**. Run 1 exceeds visible/reduced
  CPU at 1.978694% / 1.015400%; run 2 and every memory interval pass. The first failure,
  binary identity and unchanged acceptance rule are retained without replacement runs.
  Earlier artifacts are never relabeled as evidence for the new binary.
- Recorded independent validation-sequence decision after the user confirmed no second
  monitor is available. Physical display/input/accessibility and actual sign-in remain
  mandatory Phase 11/12 pre-release gates, with no missing check counted as passed.
- Final independent development review passes at **8.7/10**. An explicit post-result
  planning reconsideration places unresolved resource qualification in Phase 12 before
  release. Performance remains 7/10 with zero points for the failed budget criterion;
  the final-build experiment remains FAIL, with unchanged limits and acceptance rule.
  Phase 3 is next; no AI/provider functionality or release approval is claimed.

## Unreleased — Phase 1 desktop foundation (2026-09-21)

- Added real settings persistence with atomic backups, strict validation and visible recovery.
- Added typed content-free structured log channels, rotation, retention and corruption recovery.
- Added same-user single-instance activation and bounded asynchronous shutdown.
- Replaced the build-check window with a static transparent companion and working settings UI.
- Added MSTest unit/integration/adversarial coverage, TRX, Windows CI configuration and real-HWND process checks.
- Independent Phase 1 QA passed at 9.0/10, with 38/38 tests in both Release and Debug,
  9/9 negative controls and 9/9 UI/process checks. Status synchronized on 2026-09-22;
  no later feature was claimed complete at that checkpoint.

## Unreleased — Phase 0 foundation (2026-09-21)

- Documented Windows-first architecture, trust boundaries, proposed contracts, UX,
  independent QA process, full requirements traceability and phased roadmap.
- Added dependency-free .NET 10/WPF solution, SDK pin, strict analyzers/warnings,
  offline package configuration and explicit project dependency boundaries.
- Added a clearly labeled launchable foundation window and hidden XAML/layout smoke.
- Added executable repository architecture checks and repeatable verification script.
- Independently verified Release/Debug builds, 11 structural checks, XAML layout smoke
  and 9 negative/control cases; Phase 0 QA passed at 9.4/10.
- Retained an identity-free Git tree checkpoint; no fabricated author commit.
- No companion, provider, voice, storage, math or tool features are released yet.
