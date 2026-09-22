# Desktop Companion

A Windows-first personal AI companion built in C#/.NET 10 and WPF.

**Phase 2 development passed independent QA at 8.7/10; Phase 3 is next.** The app opens a small transparent
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
avatar packs, tools and memory are not implemented yet. Resource confirmation failed;
CPU qualification remains a release blocker. Physical display, actual input, accessibility and sign-in qualification
remain mandatory before release under the recorded validation sequence. No API credentials are
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
Hide and click-through require a successfully registered summon shortcut. If a shortcut
cannot be registered, choose another preset before making the companion inaccessible.
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
Phase 2's [final development review](docs/qa/PHASE_2_RESOURCE_REVIEW.md) passes at **8.7/10**.
The historical [8.6 failed exit](docs/qa/PHASE_2_FOLLOWUP_REVIEW.md) and
[8.0 review](docs/qa/PHASE_2_REVIEW.md) remain preserved. The final-source Release suite
passes 196 tests and 11 architecture checks.
The final native run passes 18 process controls, 43 appearance, 24 integration,
29 settings lifecycle and 49 placement-initialization assertions, with no skips or failures.
The declared A/B/B/A comparison passed every CPU/memory interval on its frozen binary;
earlier CPU failures remain preserved. New startup-placement and confirmed-recovery
fixes pass regression. Final-build resource confirmation fails: its first run exceeds
CPU limits at 1.978694% visible and 1.015400% reduced motion; the second run passes,
as do all memory intervals. Both runs had to pass, so the aggregate remains **FAIL**.
An explicit [post-result sequencing decision](docs/qa/PHASE_2_RESOURCE_SEQUENCE.md)
allows development to continue while keeping CPU qualification mandatory in Phase 12.
Performance scored 7/10, with no points for the failed budget criterion.
The [validation sequence decision](docs/qa/PHASE_2_VALIDATION_SEQUENCE.md) moves unavailable
physical/input/sign-in qualification to mandatory pre-release gates. No missing check
is counted as passed. Development approval is not release approval; Phase 3 is next.
No installer, remote repository or published release
exists. No release license has been selected.
