# Desktop Companion

A Windows-first personal AI companion built in C#/.NET 10 and WPF.

**Phase 1 passed QA at 9.0/10; Phase 2 is in progress.** The app opens a small transparent
companion and a settings window. Drag the character to move it and snap to a display's
work-area edges, or focus it with Tab and use arrow keys (Shift moves farther) within
the current display. **Ctrl+Shift+M**, while the companion is focused, or the
next-display menu action cycles through available displays. Settings control its name, avatar size (75–150%), opacity
(40–100%), reduced motion, topmost behavior and content-free diagnostics.
Settings persist locally with validated backups and corruption recovery. A second
launch activates the existing instance.

The built-in vector character supports eight semantic expressions and a brief idle blink
about every ten seconds. Motion stops while hidden/minimized, sleeping, with reduced motion or when Windows disables
animations. High contrast uses system colors and keeps the companion opaque. Tray
actions and a configurable summon shortcut recover hidden or click-through windows.
Monitor-relative position persists separately from preferences. Chat, voice, custom
avatar packs, tools and memory are not implemented yet. Phase 2 is not complete;
CPU measurements currently exceed the declared idle budget, and physical mixed-DPI,
actual input and accessibility evidence remains pending. No API credentials are
read or required.

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
companion stays in the taskbar and has a tray menu with Show, Hide, Settings, Sleep/Wake
and Exit. The default summon shortcut is **Ctrl+Alt+Space**; two alternatives are in
Settings → Desktop behavior. Conflicts are reported. Summoning or starting the app
again turns off click-through. Click-through is intentionally limited to the session.
Settings save explicitly; closing the settings window discards unsaved edits.
Reopening Settings shows and restores an existing hidden or minimized editor.
Version-1 preferences migrate in memory and are written as version 2 only on Save.
The expression preview lasts four seconds and does not start a microphone or task.
Windows sign-in startup is opt-in under Desktop behavior, available with the default
profile; it registers the current executable path, so update it if you move the app.
Position saves after explicit movement, including a move immediately followed by Hide
or Exit. The saved monitor and relative position are restored and constrained to the
available work area. A missing monitor falls back to a surviving display. Display geometry
and monitor-cycle policy have automated coverage; the observed desktop has one display
at 125% scale, so physical cross-display, mixed-DPI and screen-reader checks remain pending.

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
Phase 2's [independent follow-up](docs/qa/PHASE_2_FOLLOWUP_REVIEW.md) scores **8.6/10**,
but the **phase-exit result is FAIL**; the [earlier 8.0 review](docs/qa/PHASE_2_REVIEW.md)
is preserved. The Release suite passed 193 tests before the final cadence-only change;
the subsequent strict build and native run pass, with 12 process/control checks, 43 appearance
assertions and 18 integration assertions, including settings recovery and blink lifetime.
The final ten-second-blink baseline measures 1.041400% visible-idle CPU against a
strictly below 1% limit of one logical processor. Reduced/hidden CPU and all memory
intervals pass. Earlier failures remain preserved. CPU diagnosis and required physical
display/input/accessibility evidence must be completed before Phase 3.
No installer, remote repository or published release
exists. No release license has been selected.
