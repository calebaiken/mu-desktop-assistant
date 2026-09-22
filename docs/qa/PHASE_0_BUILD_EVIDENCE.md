# Phase 0 implementation verification evidence

Date: 2026-09-21. This is the implementation agent's execution record, **not** the
independent QA verdict. See PHASE_0_REVIEW.md when finalized.

## Environment and preservation

`Get-ChildItem -Force` at initial workspace inspection returned no entries. `git status`
reported no repository. Ancestor/workspace AGENTS.md checks found no instructions.
No pre-existing source or user data was modified. `dotnet --info` reported SDK 10.0.401,
MSBuild 18.9.11, Windows 10.0.26200 (Windows 11), win-x64, WindowsDesktop 10.0.12;
WindowsDesktop reference packs were present. Git 2.54.0.windows.1 is available.

## Failures found and addressed

| Attempt | Observed outcome | Correction |
|---|---|---|
| Plain `dotnet restore Companion.slnx` | Exit 1 without diagnostics | Moved CLI first-use state to ignored workspace `.dotnet-home` |
| Restore with local CLI home and normal verbosity | Exit 1, 0 warnings/errors after solution initialization | Disabled build servers/shared compiler and used `-m:1`; same solution then restored successfully |
| `powershell -NoProfile -File scripts/verify.ps1` | Script execution blocked by host execution policy | Documented child-process-only `-ExecutionPolicy Bypass`; no persistent policy change |
| First full Release verify | Build 0 warnings/errors, 11/11 checks; WPF smoke exit 1 | A never-shown Window has no presentation source; measure/arrange actual XAML content instead |
| Independent negative harness initial run | PowerShell fast-process exit-code capture was unreliable | QA replaced launch with direct Process/async drains; main verification adopted direct Process lifecycle too |

The restore behavior is consistent with a restricted build-process environment, but
the exact internal failure was not diagnosed from the zero-error output. The supported
verification path avoids build servers rather than claiming a proven SDK defect.

## Successful verification

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
```

Actual result: **exit 0**, locked restore succeeded, all five projects compiled in
Release, **0 warnings / 0 errors**, **11/11 architecture checks passed**, WPF
XAML/STA/content-layout/startup/shutdown smoke passed. The corrected build elapsed
5.38 seconds according to MSBuild (incremental build, not a performance benchmark).

The 11 checks cover four layer graphs plus solution inventory, strict properties,
SDK pin, least-privilege/DPI manifest, document presence, no external package feeds,
and ignore rules. Smoke uses the actual FoundationWindow XAML constructor and
nonzero FrameworkElement layout on the application dispatcher, then shuts down.

No visible HWND/compositor, interactive UX, audio device, remote provider, database,
scientific result, tool or installer was tested. These features do not exist yet.
No key was read, created or used; no API calls or external packages were required.

## Source-control status

`git init -b main` succeeded. Neither `user.name` nor `user.email` is configured.
Use an identity-free Git tree reference `refs/checkpoints/phase-0` to retain the reviewed
source snapshot; this is a recoverable tree, **not an authored commit**. The independent
report and final development status record completion. No remote is configured/pushed.
