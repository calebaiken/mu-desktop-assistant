# Phase 2 implementation evidence — 2026-09-22

This record attributes execution to the implementation agent. Independent review is
separate in PHASE_2_REVIEW.md. No passing phase gate is implied by a successful build.

## Executed checks

`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1` completed
under the normal Windows user token, exit 0. Release build had zero warnings/errors;
11/11 architecture assertions and the composed settings/logging/IPC/XAML lifecycle
smoke passed. Discoverable MSTest execution: **163 passed, 0 failed, 0 skipped**.
TRX: `artifacts/test-results/Release/tests.trx`. This run included independent
placement lattice/rational-grid oracles and state priority/lease/concurrency checks.

`scripts/verify-ui.ps1` completed with **11/11 process/control checks**, including
**33 named appearance/state/placement checks** and **18 native desktop integration
checks**, zero shortcut skips and zero adapter errors. It created and disposed owned
windows, tray resources and temporary hotkey registrations. It did not send keyboard
or mouse input, change display preferences or write startup registration.

Evidence profile:
`artifacts/qa/desktop-ui-20260922-164631-f14197c6/profile`.
`ui-stages.txt` records final UI-stage times of 4.790 s and 1.984 s on same-profile
restart. These are measured stage durations, not cold-start or percentile benchmarks.

The prior expanded run at `desktop-ui-20260922-164249-5578a5c9` reached all 33
appearance assertions but exceeded its 15-second process deadline before retaining
integration results. It was terminated by the supervisor. Its missing integration
result is not counted as passed. Expanded checks now have a 30-second deadline and
stage timing; the later complete run supplies the pass above.

## Rejected resource measurement

`scripts/measure-desktop.ps1` ran 10 s warmup and three 60 s measurement intervals.
Profile: `artifacts/qa/performance-20260922-164711-eccda553`.
Reference hardware: Ryzen 7 5700U, 16 logical processors, 7,928,467,456 physical-memory
bytes. It recorded process counters and performed independent arithmetic checks.

This run **cannot establish a controlled idle baseline**. Its application log records
SettingsSaved at 20:48:05Z and 20:48:58Z; saved name/scale/topmost settings changed during
the run. The reduced-motion interval also observed active animation. The supervisor
had already parsed its old version and returned exit 0; that does not validate the
experiment. Raw artifacts remain for diagnosis, and no resource-budget pass is claimed.

The revised harness labels its window Resource measurement, disables its settings,
movement and context-menu input while leaving the window/renderer enabled, and never
opens settings in performance mode. The revised supervisor rejects saved preferences,
unexpected interval animation and incorrect visibility/state endpoints. A clean
measurement with those guards is pending execution.

## Changes after the last full execution

Thirteen additional startup-registry cases now exercise the production registry
adapter through strictly validated unique HKCU test keys. They cover actual
read/write/delete, idempotence, failure preservation and restricted key paths. They
never target the user's Run value, execute a registered command or simulate sign-in.
These cases are authored but **unexecuted**. The final measurement and shutdown guards
also postdate the full native run. A successful compile does not count as these tests.

The next normal-user `verify.ps1` request was rejected before launch by automatic
approval review because its usage quota was exhausted. Its response explicitly
forbade bypassing the rejection. No registry/test execution workaround was attempted.
Ordinary sandboxed compilation and read-only source review continued. The application
remains asInvoker; needing the normal user token for native checks is not a UAC/admin
requirement. Atomic replacement, same-user IPC and test isolation were preserved.

Final compile-only command: `dotnet build Companion.slnx --no-restore -c Release
--disable-build-servers -m:1 -p:UseSharedCompilation=false`, exit 0, zero warnings/errors,
15.75 s. It included the 13 new registry cases and final measurement/shutdown guards;
it did not execute them. `git diff --check` exited 0 with line-ending notices only.

## Remaining evidence

Run latest strict verification, guarded resource measurement and relevant native
regression checks, then independently review artifacts. Record actual keyboard/mouse/
tray interactions, screen-reader/high-contrast usability, physical mixed-DPI and
monitor-removal behavior, and disposable-user Windows sign-in. No remote CI, Phase 2
Debug pass, frame-time p95, startup p95, GPU/compositor measurement, long-run leak
certification or passing Phase 2 gate is claimed.
