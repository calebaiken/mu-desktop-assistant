# Changelog

## Unreleased — Phase 2 desktop interactions (2026-09-22, gate pending)

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
- Resumed normal-user verification: 193/193 Release tests, 11 architecture checks,
  13/13 isolated-registry cases, and native 12/12 controls with 43 appearance and
  18 integration assertions. Actual sign-in and physical input/accessibility remain pending.
  The 193-test suite predates the final cadence-only change; the later strict build
  and native run execute that change, with the earlier TRX archived separately.
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
- Independent follow-up: **8.6/10; phase exit FAIL** due to visible-idle CPU and required
  physical validation. The earlier 8.0 failed checkpoint remains preserved.
  Phase 2 remains incomplete; later phases have not begun.

## Unreleased — Phase 1 desktop foundation (2026-09-21)

- Added real settings persistence with atomic backups, strict validation and visible recovery.
- Added typed content-free structured log channels, rotation, retention and corruption recovery.
- Added same-user single-instance activation and bounded asynchronous shutdown.
- Replaced the build-check window with a static transparent companion and working settings UI.
- Added MSTest unit/integration/adversarial coverage, TRX, Windows CI configuration and real-HWND process checks.
- Independent Phase 1 QA passed at 9.0/10, with 38/38 tests in both Release and Debug,
  9/9 negative controls and 9/9 UI/process checks. Status synchronized on 2026-09-22;
  no later feature is claimed complete.

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
