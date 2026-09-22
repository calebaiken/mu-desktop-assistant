# Changelog

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
