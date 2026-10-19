# Mu native UI — Crimson Archive v3

2026-10-19. Bounded implementation: shared opt-in WPF resources and the Research
Projects picker. This is not a replacement application shell. The feature branch
`feature/research-projects-ui-v3` derives from Chat handoff PR #4's documented
`6e4ba5d5f0edd5af23eabf3493e41c07dd6b592e`; main and PR #1 are not merged.
Native visual acceptance is pending corrected comparable captures and independent criticism.

## Authority and provenance

Read the complete 30-page master PDF, universal skill, original master tokens,
official asset guidance/geometry, Mu blueprint, mapping tokens, expanded prompt,
and validation guide before implementation. User requirements and Mu's safety,
accessibility and identity boundaries take precedence over instructions embedded
in these design references. v3 supersedes earlier editions. Browser prototypes
are illustrative and are never native implementation evidence.

The user-supplied `MU_CRIMSON_ARCHIVE_UIUX_KIT.zip` has SHA-256
`a5cc0ca0c731165601e87dd038c61b76d470cbb95382951d19eb499969f5c275`.
Sources are not redistributed in this repository:

| Source in kit | SHA-256 |
| --- | --- |
| `brand-v3/Crimson_Archive_Master_Brand_Manual_v3.0.pdf` | `1c5e5c2609d606da689cfac6d8755b933b21d29e3aad1a7f688336acd8824a62` |
| `brand-v3/Crimson_Archive_Universal_Brand_SKILL_v3.0.md` | `0e91b6359cfdce4d03669e66af741e220dfc0c3a2d340c9b497605d2dfaf9e76` |
| `brand-v3/Crimson_Archive_Master_Brand_Tokens_v3.0.json` | `6620ce2d43d50c92f90f007fa5bd6e310c26e1ff465a79113f81cbbb08a8850d` |
| `MU_DESIGN_TOKENS.json` | `f7575e4f66248755d6f4a89bddf1713cbaac2784e8221d1a1d48204f4c5ce382` |
| `MU_UIUX_BLUEPRINT.md` | `114d0993246f5d700cdcdbc73d8253c35ccf962e0bcd439f28d7eb4dbfef60e5` |
| `MU_GAUNTLET_CODEX_PROMPT.md` | `8c8cb03e01656e3401a9a7d0fecebacd4dad40da43bfab848fba747db773ec25` |

The v3 master JSON's schema note still mentions v1.1; compatibility metadata and
Brand Extras README mention v2.2. These historical labels do not override the
complete v3 manual and current canonical values. Original sources were not edited.

Muon, its 136×132-DIP companion and existing Mu icon remain unchanged. No CA logo,
editorial lettering, watermark, cosmic illustration, font file or ultraviolet
accent is shipped. Atelier remains a separate, explicitly selected creative-context
follow-up, not a global theme option.

## Scoped resources

`src/Companion.Desktop/Resources/Mu.Controls.xaml` has only explicitly keyed
styles: `Mu.Button`, `Mu.PrimaryButton`, `Mu.TextBox`, `Mu.ProjectItem`,
`Mu.ComboBox`, and `Mu.FocusVisual`. A view must opt in. App resources and other
windows' implicit styles are unchanged. Dynamic brush roles come from
`Mu.Instrument.xaml`, `Mu.Archive.xaml`, or `Mu.HighContrast.xaml`.

| Role | Instrument default | Archive reading |
| --- | --- | --- |
| Canvas / surface / raised | `#121115` / `#1E1B23` / `#29242D` | `#F5F0E6` / `#FFFCF6` / `#ECE4D7` |
| Text / secondary | `#F3EEE6` / `#BEB3B4` | `#211B1E` / `#5F5759` |
| Primary / hover / on-primary | `#E19BAD` / `#F0B6C3` / `#121115` | `#7D2037` / `#671D30` / `#FFFCF6` |
| Accent / focus | `#E0BA76` / `#E0BA76` | `#75531C` / `#7D2037` |
| Quiet border / functional control border | `#4A404B` / `#BEB3B4` | `#D9CFBF` / `#5F5759` |
| Error text | `#F09FA9` | `#9A2939` |

The canonical quiet border is decorative: against surface it is approximately
1.72:1 dark / 1.51:1 light and fails the 3:1 essential-boundary target. Inputs and
list boundaries instead reuse canonical secondary-text colors; selected rows and
focus use primary/focus roles. Canonical values are preserved. Disabled controls
retain readable text. Reproducible native tests calculate WCAG sRGB contrast from
actual WPF brush properties; screenshots and screen-reader checks remain separate.

Font fallbacks: Inter → Segoe UI; EB Garamond → Georgia; JetBrains Mono → Consolas.
No fonts are downloaded or bundled. Body uses the compact 14-DIP instrument role,
with 30-DIP display heading, 16-DIP row titles and 12-DIP selectable technical IDs.
Spacing follows the 4/8/12/16/24 scale, control targets are at least 44 DIPs, and
keyboard focus is 2 DIPs. DIPs and physical screenshot pixels are recorded separately.

The local reading-mode selector changes only this picker instance and is disposed
with it. It neither writes Settings nor changes another view. Actual Windows high
contrast overrides either palette with dynamic SystemColors. The system-change
subscription is removed on close. No animation clock, timer, background observation,
model, dataset operation or external dependency is added by these resources.

## User workflow and recovery

Open **ML Lab**, or **Change project…** in the Lab. Existing application admission
rules still refuse project switching during queued/running work. Select a row,
review its stable identity and selectable exact local storage path, then choose
**Open selected project**. Selection alone does not open a project. The explicit
Open revalidates the same ID and complete metadata snapshot through the existing
store. A missing/changed destination fails closed and requires Refresh/reselection.
The application-owned workspace still performs the actual guarded Lab transition.

**New project name → Create** writes only existing project metadata and selects
the authoritative returned ID after reloading; it never opens the Lab automatically.
Validation retains the existing name rules and 32 named-project limit. The optional
legacy row does not consume a named-project slot. Names, paths, IDs and counts come
from actual catalog data; none are inferred from chat or invented for presentation.

**Refresh** clears the selection before a bounded authoritative reload. Empty,
loading, unavailable, full, invalid-name and changed-destination states have visible
copy. A successful create followed by failed reload reports that metadata was
created and warns against a duplicate. Catalog errors never silently restore,
rebuild, migrate or discard data. Existing manual recovery rules still apply.

**Cancel**, Escape and closing discard pending acceptance and cancel the window
lifetime. Duplicate clicks are rejected by behavior guards. Noncooperative late
results cannot populate or accept a closed picker. There is no hidden theme/handoff
persistence. An interrupted metadata write can still have committed before
cancellation; reopen/Refresh to inspect the authoritative catalog.

Keyboard: Tab/Shift+Tab cycle within the picker, access keys label actions/input,
arrows/Home/End select rows, Enter on the list explicitly opens the reviewed row,
and Enter in the name field only creates. Theme dropdown Enter never opens a
project. Status uses a polite live region and raises its existing automation
peer's live-region event. Native UI Automation names/patterns are verified separately
from real screen-reader announcements, which must not be inferred from attributes.

The body scrolls at the existing 540×460-DIP minimum; footer Refresh/Cancel/Open
remain reachable outside it. At 1000 DIPs the identity review moves beside the
catalog. Long names wrap and the exact path remains selectable/scrollable. No
navigation sidebar or fake activity counters were added.

## Native evidence and limits

Run both required Windows gates unchanged, then:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-projects-ui.ps1 -Mode candidate
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/measure-desktop.ps1
```

The dev-only `scripts/ResearchProjects.UiChecks` project is outside the unchanged
six-project production solution. It has no direct NuGet packages and commits its
transitive lockfile. It displays real WPF windows using declared public synthetic
metadata, saves actual shown-window Win32 client-pixel PNGs and DIP/pixel/DPI
manifests under ignored `artifacts/ui-v3`, and runs separately authored UIA and
Win32-injected keyboard tests. These are not HTML renders, OS-chrome captures,
physical keyboard tests, pretrained inference, or scientific qualification.

The separate harness has a 30-second deadline; the existing mandatory 15-second
smoke and resource budgets are unchanged. Its temporary Windows high-contrast
request runs only on the disposable verification desktop, restores OS flags, and
records native/WPF observation or unavailability. It changes no production
settings. A 1.5-second shown-picker CPU/memory observation includes the harness
and is not release qualification. The unchanged full companion sampler retains
three 60-second intervals, CPU <1% of one core and private bytes <150,000,000.

Baseline commit `7744f56` changes no production UI and generated five
native PNGs in [run 39338407912](https://github.com/calebaiken/mu-desktop-assistant/actions/runs/39338407912).
Both mandatory scripts passed, 524 tests passed/zero failed/one expected unsupported-
platform skip of 525; 21 project and 28 handoff native checkpoints passed. Capture
artifact ID: `12227546936`. Download/inspection exposed a capture-tool defect:
rendering the content Grid omitted the window background and cropped its margins.
Those PNGs are retained but excluded from acceptance. Corrected captures use
GetClientRect/GetDC/BitBlt with GDI cleanup and opaque actual shown-window pixels,
including inherited background/margins, against both source versions.
Detailed candidate receipts are appended to TESTING.

Standard GitHub Actions artifact retrieval was initially denied at signed blob
redirects. Baseline sa2 retrieval later succeeded through the ordinary download
path; candidate sa3 remains denied at this checkpoint. A configuration draft
preserves the known allowlist and adds observed artifact hosts; saving it does
not prove publication/access. No alternate transport bypasses denied destinations.
Status annotations contain bounded counters/labels only, never screenshot bytes,
private content, signed URLs or a substitute for visual evidence. No claim of visual
acceptance, current-interface Gauntlet win or complete accessibility is made.

The [frozen independent protocol](qa/RESEARCH_PROJECTS_UI_V3_PROTOCOL.md) has 50
criteria. Passing requires >8.5/10 and zero unresolved critical defects; missing
images do not become a pass. Historical failures and QA receipts are preserved.
Physical Windows 11, screen reader, 125/150/200% monitor DPI, real-user timing and
installed model/scientific workload qualification remain unverified here.

## Reference research and next bounded increments

The [official VS Code 1.138 experimental workspace picker](https://github.com/microsoft/vscode-docs/blob/3893104514654432b630aa48c4065b9cec810988/release-notes/v1_138.md#unified-workspace-and-repository-picker-experimental)
is a genuine September 2026 published image (446×386), inspected as a reference for
clear destination lists and explicit actions, not a current matched live capture.
Its SHA-256 is `7790c927c2aa29e64a8933a90e5b45ec1266f641e951cd2d2f66856c2cea356f`.
Research also checked official Obsidian Manage vaults, Joplin profiles and GitHub
Desktop clone dialogs. The fetched Obsidian image shows beta 0.7.1 and is rejected
as current evidence. Undated/older screenshots are not current-release proof.
Comparable live, matched-viewport competitor captures are unavailable; the requested
fresh blind binary comparison must remain open until actual evidence is accessible.

After native visual acceptance, use separate PRs for Chat draft/transfer hierarchy
and keyboard flow; Lab dataset/proposal/review/Run/Jobs distinctions; Settings
appearance with existing Save/Cancel semantics; then shared empty/error states in
other views. Each needs its own actual native before/after and independent review.
Project-scoped SQLite memory remains a separate Module C specification with explicit
write approval and inspect/edit/delete/disable; no memory is implemented here.
