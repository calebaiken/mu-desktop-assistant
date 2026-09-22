# Development status

Updated: 2026-09-22. Current phase: **1 — complete, independent QA passed at 9.0/10**.
Phase 0 passed at 9.4/10. Phase 1 passed its separate strict >8.5 gate with no critical
unresolved defect in its implemented scope. Phase 2 is the next development phase.

## Completed work

- Preserved the Phase 0 source tree, architecture, requirements and QA report.
- Implemented validated versioned preferences, atomic save, known-good backups,
  unsupported-schema protection, bounded corrupt-file recovery and conflict detection.
- Implemented closed typed structured logs, separate channels, rotation/retention,
  corruption recovery and diagnostics opt-in.
- Composed application lifetime and single-instance activation over same-user IPC.
- Added transparent static companion, settings/name/topmost/diagnostics controls,
  explicit save, recovery messages, accessible named controls and clean Exit.
- Added pinned MSTest dependencies, standard discovery/TRX, Windows CI definition,
  isolated tests and independent UI/negative process checks.
- Release verification passed: zero warnings/errors, 38/38 MSTest cases,
  11/11 architecture assertions and composed startup/layout/shutdown smoke.
- Independent Release and Debug verification passed, alongside 9/9 negative controls
  and 9/9 UI/process checks. Attributed interactive save, restart, activation and Exit
  evidence is recorded in the final independent Phase 1 review.

## Pending work

- Phase 2 avatar/desktop interactions; Phase 3+ conversation, voice, memory and tools.

## Architecture decisions

C#/.NET 10 WPF; Core values, Application ports, Infrastructure adapters, Desktop
composition. Production remains package-free. Test-only MSTest dependencies use a
reviewed NuGet source, lock files and enabled vulnerability auditing. Settings back up
validated in-memory state instead of copying possibly corrupted disk data. Typed logs
cannot accept arbitrary text/exception content. Single-instance IPC accepts activation
only. No AI credentials, external calls by the application, tools or capture features.

## Defects found and fixed

Independent QA identified wrong JSON schema types bypassing recovery; cached backup
validity; save authorization after failed load; unbounded corrupt-file copying; retention
by last-write instead of oldest event; malformed log startup failure; drive-root and
UNC normalization; and truthful postcommit cancellation reporting. Fixes and regression
coverage precede the final review. Initial environment failures are retained as evidence.

## Test status and environment

Release full verify: exit 0, 38 passed / 0 failed / 0 skipped, 11 architecture checks,
0 warnings/errors and composed smoke PASS. Independent Debug verification also passed
38/38 tests and 11/11 architecture checks, with zero warnings/errors. Independent
negative and UI/process checks each passed 9/9. Phase 1 QA: **9.0/10, PASS**.

A sandboxed run failed 10 cases with UnauthorizedAccessException on File.Replace and
current-user named pipes. The same code/test suite passed under the normal user token
via approved execution outside the sandbox. This is not an administrator requirement
for the application. Restricted-network restore also failed NU1900; an approved network
restore completed with auditing enabled. No assertions/security controls were weakened.

Disk-full is simulated by an injected writer; real Windows sharing failures test atomic
replacement. CI is configured but has not executed on a remote repository. Mixed DPI,
screen readers, long-running resource behavior and future features remain unverified.

## QA history

| Checkpoint | Reviewer | Score | Result |
|---|---|---|---|
| Phase 0 final | Independent phase0_qa | 9.4/10 | PASS; docs/qa/PHASE_0_REVIEW.md |
| Phase 1 service/design checkpoints | Independent phase0_qa | Not scored | Defects found, fixed and regression-tested |
| Phase 1 final | Independent phase0_qa | 9.0/10 | PASS; docs/qa/PHASE_1_REVIEW.md |

Last completed QA score: 9.0/10 for Phase 1. The gate permits Phase 2 development;
Phase 2 requires its own independent review before advancement to Phase 3.

## Technical debt / known limitations

- Static basic character only; animated packs, drag/DPI/tray/hotkey/startup wait for Phase 2.
- No chat, AI, voice, memory, tools, scientific engine, installer or updater.
- Remote CI and published release history remain unavailable; existing Git author
  configuration is used for local source finalization.
- Local app storage is not encrypted; same-user malware is outside the protection boundary.
- Log retention is lazy on writes and may discard recent records sharing an old file.
- Published license, signing, dependency update policy and release distribution remain later work.

## Source checkpoint and next objective

Phase 0 source remains at refs/checkpoints/phase-0. The Phase 1 checkpoint is identified
by refs/checkpoints/phase-1; its finalization record is docs/qa/PHASE_1_FINALIZATION.md.
Existing Git author configuration is used unchanged. No remote is configured and no
remote CI execution is claimed.
Next objective: implement Phase 2 avatar and desktop interactions with regression
coverage and a new independent QA gate.
