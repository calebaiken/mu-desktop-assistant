# Mu

A Windows-first personal AI companion built in C#/.NET 10 and WPF.

**Approved text inspection candidate (2026-10-19).** More → Inspect text file opens
a separate read-only panel: enable, select one `.txt`/`.md`, review its exact target,
then approve one bounded read. Text is literal and never automatically enters Chat
or memory. Native Windows qualification is still pending; see
[usage and limits](docs/TRUSTED_TEXT_INSPECTION.md), [current testing](TESTING.md)
and the [source/PR audit and selected-module sequence](docs/AGENTIC_EVOLUTION_AUDIT.md).
The machine-specific installation/QA receipts below are historical; a new checkout
does not contain their ignored runtime, model or binary artifacts.

| | |
|---|---|
| Solution | `Companion.slnx` — four production projects, two test projects |
| Target framework | `net10.0`, nullable reference types enabled |
| SDK | 10.0.401 (`global.json`, `rollForward: latestPatch`, prerelease disabled) |
| UI | WPF (WindowsDesktop reference packs required) |
| Optional runtimes | Python 3.12/3.13 workers — ML Lab, neural speech, LAYA advisor |
| Cloud services | None. No API key is read or required. |
| Platform | Windows 11 x64 |
| License | [MIT](LICENSE) — see also third-party notices below |

> **Scope note.** This repository contains the *entire* Mu desktop application.
> The **Machine Learning Lab is a subsystem of Mu, not a separate program.** Its
> C# services live in `Companion.Application.MachineLearning`,
> `Companion.Infrastructure.MachineLearning` and `Companion.Desktop.MlLab*`, and
> its Python backend (`workers/ml_lab`) is an owned worker that Mu starts per
> request. It cannot be built or run independently of the other four projects.

**Local neural voices are integrated (2026-09-30).** In **Chat → Voice options**,
choose Heart, Bella, Michael or George, then select **Preview voice** to compare.
Read aloud and Stop use the same local controls. The optional Kokoro CPU runtime
and voice assets are installed on this PC; no API key, cloud service or microphone
is involved. Voice/speed choices apply to the current chat window. Long readings
are shortened to approximately 30 seconds, and memory pressure can safely refuse
generation. All four real public samples passed audio inspection, and the
repaired host passes all 398 .NET tests. CI now provisions a bare interpreter at
`artifacts/ml-runtime` so those checks execute there too: 398 passed, 0 failed,
0 skipped. **Final real playback/reuse qualification is
still incomplete:** the latest run safely returned `memory_limit`. See [voice help](docs/MU_NEURAL_VOICES.md) and
[the measured qualification](docs/qa/MU_NEURAL_VOICE_REVIEW.md).

**Muon avatar customization and the Galaxy theme are available (2026-09-30).**
Open Settings, choose **Muon**, select **Galaxy** under **Color / theme**, and
Save. Five themes, two faces and three particle decorations are available; existing
profiles retain their previous appearance until saved. The particle mascot keeps
the compact transparent surface and hover controls. Mu now has matching executable,
window, tray and shortcut icons. The verified development executable is
`artifacts/Mu/Companion.Desktop.exe`; the requested **Mu.lnk** was created and
verified in this PC's Windows Desktop folder. See the
[appearance and shortcut guide](docs/MU_APPEARANCE_LAYA.md).

Optional local LAYA task advice is integrated into **ML Lab → Ask Mu**. Its pinned
runtime and checkpoint are installed, but **full checkpoint inference is not yet
qualified**: the actual native run returned `memory_limit` before loading it.
Existing chat and ML tools remain available. It requires no API key and cannot
execute an ML action. The identity checkpoint passed 375 .NET tests, 11 architecture
checks, 12 independent LAYA worker tests and 44 native UI supervisor checks;
each of those two native runs passed 315 avatar and 40 controlled LAYA UI assertions.
These controlled fixtures do not substitute for successful pretrained inference.

**Mu Machine Learning Lab now has a verified bounded foundation (2026-09-29),
independently reviewed at 8.9/10.**
Open it from More, the tray or Chat for local dataset inspection, explicit CPU
training, metrics/plots, saved models, schema-checked prediction, reproducible export
and comparison of 2–5 experiments with matching data and split. Ask Mu proposes a
typed action through the local model; review it and explicitly Run before anything
executes. This is not completion of the full requested ML Phase 1 or release approval.
See the [Lab guide](docs/ML_LAB_GUIDE.md) and
[independent review](docs/qa/ML_LAB_FOUNDATION_REVIEW.md).

The preceding ML foundation candidate passed 353 .NET tests, 11 architecture checks, 57 native Lab
assertions and two live local planning prompts. The latter is a narrow public fixture,
not a general language-understanding benchmark. Prior desktop UI regression passes
38 supervisor checks. Historical display, CPU and release gates below remain open.

**Phase 3 development passed independent QA at 9.5/10.** The app opens a small transparent
character with no surrounding card. Hover over it to reveal **Chat**, **Settings**
and **More**; the controls disappear when the pointer leaves. Keyboard navigation
also reveals them: Tab through the character and buttons, Enter to activate, Escape
to dismiss. More/right-click contains Hide, Sleep/Wake and Exit. At default size the
character is 112×88 DIPs, inside a stable 136×132 transparent window so revealing
controls never moves it. Drag the character to move it and snap to a display's
work-area edges, or focus it with Tab and use arrow keys (Shift moves farther) within
the current display. **Ctrl+Shift+M**, while the companion is focused, or the
next-display menu action cycles through available displays. Settings control its name, avatar size (75–150%), opacity
(40–100%), reduced motion, topmost behavior and content-free diagnostics.
Settings persist locally with validated backups and corruption recovery. A second
launch activates the existing instance.
The [compact surface review](docs/qa/COMPACT_AVATAR_REVIEW.md) passed at 9.1/10,
including 51 native layout/disclosure assertions and a sampled real-input check.

The built-in vector character supports eight semantic expressions and a brief idle blink
about every ten seconds. Motion stops while hidden/minimized, sleeping, with reduced motion or when Windows disables
animations. High contrast uses system colors and keeps the companion opaque. Tray
actions and a configurable summon shortcut recover hidden or click-through windows.
Monitor-relative position persists separately from preferences. The first chat increment
uses a pretrained Qwen model entirely on this PC: streaming, Stop, retry, saved or
temporary conversations, search, titles and preserved edit/regenerate branches.
Markdown, highlighted code and a bounded LaTeX subset render in native WPF, with a
plain-text switch and Copy reply. Phase 4 adds explicit **Read aloud** / **Stop voice**
using optional local neural or installed Microsoft voices, with session voice/speed
controls and Preview under Voice options.
It does not speak replies automatically. Microphone input remains unqualified WIP;
custom avatar packs, general-purpose tools and long-term memory remain future work.
The new ML Lab is a separate, explicitly persistent workspace. Resource confirmation failed;
CPU qualification remains a release blocker. Physical display, actual input, accessibility and sign-in qualification
remain mandatory before release under the recorded validation sequence. No API credentials are
read or required.

For local AI, run `scripts/setup-local-ai.ps1` once, then `scripts/run-local-chat.ps1`
after building. Open **Chat** from the hover strip, companion/tray menu or press **Ctrl+Shift+C**
with the companion focused. Chat starts in **Temporary** mode: closing it discards
that session and unloads the model. Choose **Saved chats** to keep conversations in
the local profile; this starts a separate conversation and does not copy temporary
messages. Saved history survives restart. Replies completed while chat is inactive
briefly appear beside the character; opening chat or dismissing the preview clears it.
The small model can make mistakes. Read aloud covers up to 2,000 characters or
30 seconds; it preserves the full reply as text. The Windows voice adapter requires
an installation with only Microsoft speech engines enabled. Neural voices use their
own optional runtime. Both use the default audio output and leave Windows settings intact.
See [local AI setup and limits](docs/LOCAL_AI.md).
The [increment review](docs/qa/PHASE_3_LOCAL_INCREMENT_REVIEW.md) verifies 224 tests,
54 native chat assertions and real local inference/cancellation. This is a development
increment, not a finished assistant or release.

## Architecture at a glance

Dependencies point in one direction only. `Companion.Core` has no package
references and is referenced by everything; `Companion.Desktop` references the
other three and is the only project that composes Windows types.

```
Companion.Desktop  (WPF: companion, settings, chat, ML Lab windows)
        |
        v
Companion.Infrastructure  (SQLite persistence, logging, activation,
        |                   owned model worker, streaming adapter)
        v
Companion.Application  (settings, typed logging, local chat ports,
        |                ML contracts & proposal schema)
        v
Companion.Core  (validated configuration and domain values; no packages)

Companion.Desktop --spawns--> workers/ml_lab           (bounded local ML, CPU only)
                    --spawns--> workers/neural_speech  (optional offline synthesis)
                    --spawns--> workers/laya_advisor    (optional local task advice)
```

`Companion.Core` and `Companion.Application` are deliberately package-free;
`Companion.Infrastructure` and `Companion.Desktop` carry the pinned
dependencies. Full contracts are in [ARCHITECTURE.md](ARCHITECTURE.md).

## Build and launch

For the optional ML runtime, install local Python 3.12/3.13 and `uv`, then run
`scripts/setup-ml-lab.ps1 -Python C:\path\to\python.exe`. It installs 24 hash-locked
packages locally; setup needs network access and at least 2 GiB free storage.
Training and datasets stay on this PC and require no API key. The default job uses
10,000 rows, two CPU threads and a 1 GiB committed-memory cap. Lab artifacts persist
under the profile's `ml-lab/default-project`, independently of temporary chats.

The current Desktop shortcut targets `artifacts/Mu/Companion.Desktop.exe`. The
preceding ML-only candidate remains under `artifacts/qa/ml-candidate` as historical
evidence. Close an older running Mu before launching a rebuilt executable with
`--ml-root <absolute-repository-path>` and
`--local-model-dir <absolute-repository-path>\artifacts\local-ai`; otherwise single-
instance activation reuses the older app. This is a development candidate, not an installer.

Prerequisites: Windows 11 x64 and .NET SDK **10.0.401** (later patches in the same
feature band allowed), including WindowsDesktop reference packs. Production projects
use pinned Microsoft.Data.Sqlite, System.Speech, Markdig and WpfMath dependencies in their owning
layers. Core and Application remain package-free. All package versions are locked;
first restore and vulnerability auditing require network access. See the
[dependency inventory](docs/DEPENDENCIES.md).

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
Version-1/2 preferences migrate in memory and are written as version 3 only on Save.
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

Remote: `https://github.com/calebaiken/mu-desktop-assistant` (public, checked 2026-10-19).
Default branch `main`. No installer or published release exists.

Copyright (c) 2026 Caleb Aiken. Released under the [MIT License](LICENSE).

This project reuses third-party components under their own terms. `docs/DEPENDENCIES.md`
is the source-level inventory, and `workers/*/licenses/` carries the verbatim
notices for the optional Python runtimes and models. **This project license does
not relicense those components.**

| Location | Responsibility |
|---|---|
| `src/Companion.Core` | Validated configuration/domain values |
| `src/Companion.Application` | Settings, typed logging and local chat ports |
| `src/Companion.Infrastructure` | Persistence, logging, activation, owned model worker and streaming adapter |
| `src/Companion.Desktop` | Composition/lifetime, companion, settings and chat windows |
| `workers/ml_lab` | Optional bounded local Python data/model/evaluation worker |
| `workers/neural_speech` | Optional pinned offline CPU synthesis and in-memory playback worker |
| `tests/ml_lab` | Independent numerical, data, protocol and persistence checks |
| `tests/Companion.Tests` | Discoverable MSTest unit/integration/adversarial tests |
| `tests/Companion.ArchitectureChecks` | Executable repository invariants |
| `scripts` | Positive, negative and visible-window process verification |
| `docs` | Architecture contracts, requirements, decisions and independent QA |

Read [development status](DEVELOPMENT_STATUS.md), [architecture](ARCHITECTURE.md),
[testing](TESTING.md), [roadmap](ROADMAP.md), and [requirements](docs/REQUIREMENTS.md).
Phase 0 passed independent QA at 9.4/10; [Phase 1 independently passed at 9.0/10](docs/qa/PHASE_1_REVIEW.md).
Phase 2's [final development review](docs/qa/PHASE_2_RESOURCE_REVIEW.md) passes at **8.7/10**.
The historical [8.6 failed exit](docs/qa/PHASE_2_FOLLOWUP_REVIEW.md) and
[8.0 review](docs/qa/PHASE_2_REVIEW.md) remain preserved. Phase 2's final-source Release suite
passes 196 tests and 11 architecture checks.
Phase 2's final native run passes 18 process controls, 43 appearance, 24 integration,
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
is counted as passed. Development approval is not release approval. Phase 3's
[history and rendering review](docs/qa/PHASE_3_HISTORY_REVIEW.md) passes at **9.5/10**,
with 272 tests, 11 architecture checks and 28 native process controls. Phase 4A's
[speech-output increment](docs/qa/PHASE_4A_SPEECH_REVIEW.md) passes at **9.0/10**,
with 305 tests, 38 native process controls and 84 speech UI checks in both runs.
Microphone/VAD/STT and the complete
voice loop remain unfinished. See the [voice plan](docs/PHASE_4_VOICE_PLAN.md).
No installer or published release exists.

The optional Python runtimes are **installed on the user's machine at runtime,
not redistributed** by this project — speech and ML Lab dependencies arrive
through `uv pip sync` into an isolated environment, and model weights are
fetched into `%LOCALAPPDATA%`. That separation is what keeps this project's own
MIT license valid, because two of those runtime dependencies (espeak-ng and
phonemizer) are **GPL-3.0**. Shipping an installer that bundled them would be
distribution, and would require relicensing the combined work under GPL-3.0.
See [DEPENDENCIES](docs/DEPENDENCIES.md).
