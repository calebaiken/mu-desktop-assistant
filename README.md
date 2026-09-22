# Desktop Companion

A Windows-first personal AI companion built in C#/.NET 10 and WPF.

**Current checkpoint: Phase 1 desktop foundation, QA passed at 9.0/10.** The app opens a small transparent,
static companion and a settings window. You can change its name, keep it above other
windows, and enable content-free diagnostics. Settings persist locally with validated
backups and corruption recovery. A second launch activates the existing instance.

Chat, voice, animated/custom avatar packs, tray/hotkey recovery, tools and memory are
not implemented yet. The static character is the basic shell; Phase 2 adds the complete
avatar/desktop interaction system. No API credentials are read or required.

## Build and launch

Prerequisites: Windows 11 x64 and .NET SDK **10.0.401** (later patches in the same
feature band allowed), including WindowsDesktop reference packs. Production projects
have no external NuGet packages. MSTest dependencies are pinned, test-only and restored
from NuGet.org; first restore and vulnerability auditing require network access.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
dotnet run --project src/Companion.Desktop -c Release --no-build
```

The execution-policy option applies only to the child process. The verification script
keeps SDK setup state and package caches inside ignored workspace directories and
leaves no application running. It runs strict builds, architecture checks, discoverable
MSTest tests with TRX output, and a hidden real-composition/XAML smoke.

Open **Settings** to change preferences; select **Exit** or press Alt+F4 to quit. The
companion stays in the taskbar for recovery. It has no hidden/click-through mode yet.
Settings save explicitly; closing the settings window discards unsaved edits.

For an isolated development profile, use an absolute local directory:

```powershell
dotnet run --project src/Companion.Desktop -c Release --no-build -- --data-dir C:\Temp\CompanionDev
```

Normal data location: `%LOCALAPPDATA%/DesktopCompanion`. See [security](SECURITY.md)
for retention and recovery. Verification uses isolated fixtures instead of real data.

## Repository

| Location | Responsibility |
|---|---|
| `src/Companion.Core` | Validated configuration/domain values |
| `src/Companion.Application` | Settings and typed event-log ports |
| `src/Companion.Infrastructure` | Atomic JSON persistence, structured logs, instance activation |
| `src/Companion.Desktop` | Composition/lifetime, companion and settings windows |
| `tests/Companion.Tests` | Discoverable MSTest unit/integration/adversarial tests |
| `tests/Companion.ArchitectureChecks` | Executable repository invariants |
| `scripts` | Positive, negative and visible-window process verification |
| `docs` | Architecture contracts, requirements, decisions and independent QA |

Read [development status](DEVELOPMENT_STATUS.md), [architecture](ARCHITECTURE.md),
[testing](TESTING.md), [roadmap](ROADMAP.md), and [requirements](docs/REQUIREMENTS.md).
Phase 0 passed independent QA at 9.4/10; [Phase 1 independently passed at 9.0/10](docs/qa/PHASE_1_REVIEW.md).
Phase 2 development follows and requires a separate gate. No installer, remote repository or published release
exists. No release license has been selected.
