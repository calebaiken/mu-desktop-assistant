# Independent review — compact avatar and progressive controls

QA REVIEW

Scope: **Companion surface UX increment within Phase 3**, following the user's
request for just the character with buttons that disappear unless hovered.

Phase: **3 — compact companion surface increment only**

Overall Score: **9.1/10 (91/100)** — strictly greater than 8.5.

Result: **PASS for this bounded UX development increment. Full Phase 3 remains
INCOMPLETE; resource qualification remains FAIL and release remains blocked.**

The independent `compact_avatar_review` agent owns this report. Root owns the
production window implementation and executable verification. The scope is the
compact transparent companion surface, hover disclosure, equivalent keyboard
access, placement stability, and preservation of existing settings/chat/recovery
behavior. It does not complete Phase 3 or alter the local pretrained AI decision.
Historical Phase 2 resource qualification remains **FAIL**; release remains blocked.

## Review criteria recorded before execution

Ten equally weighted categories each contain five criteria, scored 0 (absent or
contradicted), 1 (partial), or 2 (supported). Each category totals 0–10 and the
overall score is their exact sum divided by ten. Passing requires **strictly
greater than 8.5/10** and no unresolved critical defect. Passing this bounded UX
increment does not assert full product, Phase 3, or release qualification.

| Category | Five criteria to score from actual evidence |
|---|---|
| Code Correctness | Strict build; compact scale/layout arithmetic; disclosure transitions; existing command dispatch; independent adverse-transition review. |
| Architecture | Window-only presentation scope; renderer/state separation; retained placement/storage ownership; explicit disclosure state ownership; no model/provider coupling. |
| Mathematical Correctness | Independently checked logical dimensions; target sizes across supported scales; stable HWND/placement during disclosure; work-area containment; exact QA arithmetic and honest limits. |
| Testing | Existing regression suite; composed/native window checks; independent source-driven edge cases; disclosure/keyboard/lifecycle checks; evidence boundaries for actual pointer and accessibility input. |
| Reliability | Avatar-to-control continuity; pointer exit after mouse focus; keyboard and Escape behavior; hide/minimize/deactivate/close cleanup; status/placement/save regressions. |
| Security | No additional action authority; hidden controls not interactive; existing guarded hide/click-through recovery; no new external data path; unchanged settings/session privacy boundaries. |
| Performance | No polling for disclosure; bounded presentation resources; no continuous decorative animation; native layout and responsiveness checks; failed resource qualification explicitly retained. |
| Maintainability | Readable presentation/state implementation; meaningful tests; no obsolete old-card assumptions; actionable status/help retained; synchronized documentation and runnable evidence. |
| UX/UI | Character-only resting view; compact unobtrusive dimensions; usable hover buttons; keyboard names/focus/targets; visual and real-input limitations explicitly stated. |
| Requirement Compliance | Latest requested surface behavior; useful chat/settings access; independent concurrent review; original remaining scope preserved; truthful test/release claims and ranked follow-up fixes. |

## Risks identified before implementation

- A toolbar hidden by `IsMouseOver` on the avatar alone can disappear while the
  pointer crosses into its controls. Use a coherent interaction region and avoid
  relocating the character or window while revealing controls.
- Plain `IsKeyboardFocusWithin` can keep controls open forever after a mouse drag
  focuses the movement surface. Preserve a keyboard path without turning a mouse
  click into permanent disclosure.
- Transparency must remove the old card visually; hidden controls must not retain
  hit targets. A fully transparent but hit-testable oversized background can still
  interfere with the desktop and needs explicit inspection.
- Name/state/movement/error text currently carries both user feedback and existing
  smoke-fixture sentinels. Preserve useful error access and automation semantics
  when removing persistent labels. Warnings must not silently grow the window.
- Native method/property tests can verify real HWND geometry and WPF state, but
  cannot certify actual mouse travel, keyboard navigation, screen readers, or
  physical mixed-DPI monitor behavior. Those limitations must stay explicit.

## Executed evidence

The normal-user strict Release verification reports
224/224 tests, 11/11 architecture checks, a composed WPF smoke, and zero build
warnings/errors. The reviewer independently read
`artifacts/qa/compact-avatar-verification/release-224.trx`: 224 total, executed and
passed, zero failed, error, aborted or not-executed. SHA256:
`23E2AA64F7D27CC54A0C0259DF91E65CA2EE189B95188F51161414FC4608C1B4`.

The earlier restricted-sandbox TRX is also retained as
`artifacts/qa/compact-avatar-verification/sandbox-restricted.trx`. Independent
inspection found 197 passed and 27 failed, all failed results reporting
`UnauthorizedAccessException` for filesystem/atomic-file, registry-fixture or IPC
operations. The subsequent approved normal-user run exercises those operations
successfully; it does not require administrator elevation or waive test failures.

The first native attempt,
`artifacts/qa/desktop-ui-20260922-203016-f25504c5/profile`, failed the 0.75-scale
dimension check after two assertions. The report and its failed outcome are
retained. The reviewer then added actual/requested dimensions and DPI to failure
diagnostics without changing the assertion or tolerance. This failure is not
presented as a passing UX test. A second diagnostic attempt,
`artifacts/qa/desktop-ui-20260922-203347-4675cd13/profile`, again failed after two
assertions and established the cause: requested/actual HWND size was 136×110.4
DIPs, avatar size remained exactly 84×66, and desktop scale was 1.25×1.25. The
110-DIP target is 137.5 physical pixels and the HWND becomes 138 pixels, or
110.4 DIPs, after WPF/native placement. The reviewer corrected only the HWND
expectation to admit at most half one physical pixel plus 0.000001 DIP; avatar
layout remains checked to 0.01 DIP. This is a diagnosed fixture quantization
error, not removal of a product-size requirement or an unexplained relaxation.
Both failed reports remain retained.

The reviewer authored `src/Companion.Desktop/CompactAvatarSmokeChecks.cs` against
the implementation contract supplied by root, before executable review. It checks
the three independently calculated scale examples (136×110, 136×132, 184×176 DIPs),
real HWND stability, geometric toolbar overlap, fixed 32-DIP targets and names,
hidden-region WPF hit testing, disclosure cases including mouse-derived focus,
warnings without resizing, and disabled/minimized/hidden/closed gates. A separate
isolated companion checks routed Chat/Settings click dispatch without opening the
application's real dialogs. The fixture calls the production disclosure policy
directly; it cannot establish that physical input reaches every event handler.
Rendered alpha checks additionally require a blank lower strip at rest and a
continuous painted center path from the character into the revealed toolbar at
each scale. Two default-scale PNGs capture WPF content at 96 DPI; they are not
desktop screenshots or proof of native pointer hit routing.

Required later physical checks: pointer entry/exit and avatar-to-toolbar crossing
at all supported scales; drag then pointer exit; remaining keyboard/context-menu
routes beyond the sample recorded below; tooltip and
warning discoverability; high contrast and screen-reader output; and mixed-DPI
placement when suitable hardware is available. These must not be relabeled passed
from direct policy calls, source inspection, or rendered snapshots.

The corrected native run retained at
`artifacts/qa/desktop-ui-20260922-203534-52642fba/profile` completed successfully:

| Evidence | Passing assertions |
|---|---:|
| Compact avatar layout, alpha, disclosure policy and lifecycle | 51 |
| Appearance, animation and placement | 43 |
| Desktop integration and guarded recovery | 24 |
| Settings save/shutdown lifecycle | 29 |
| Placement initialization | 49 |
| Chat lifecycle, history and layout | 54 |

The reviewer independently read all six JSON reports, verified their named-assertion
counts and uniqueness, and found no reported failure. Animation skips were empty;
desktop integration recorded zero skipped checks and no adapter errors. The stage
log records both required application runs through settings completion. The process
harness reports **20/20 checks**; the retained application log contains three Started
and three Stopping records for the two visible runs and composed lifecycle smoke.
The second run replaces the first run's assertion JSONs in this shared profile;
both completion-stage sequences are retained. These are **51 new native assertions,
not 51 rubric criteria**: the review rubric still contains exactly 50 criteria.

The reviewer independently opened `compact-avatar-idle.png` and
`compact-avatar-hover.png` from that profile. The first shows the character with no
card or persistent labels; the second shows three readable glyph controls in the
small strip. Pixel assertions establish the blank resting strip and painted
character-to-toolbar center path at 75%, 100% and 150% avatar scales on the current
display. A continuous rendered center path is narrower evidence than arbitrary
physical mouse trajectories or OS hit testing.

The root agent also executed actual native actions through the supported desktop
helper. The reviewer inspected the durable record at
`artifacts/qa/compact-avatar-verification/interactive-input.md`; native screenshots
and accessibility snapshots remain in the tool transcript. At the user's saved
75% avatar scale and 125% display scale, clicking the character exposed named Chat,
Settings and More controls. More opened the real menu and Escape dismissed it.
Settings opened; moving to that window and closing it returned the companion to
its character-only state. Tab reached the movement target; another Tab revealed
and focused Chat; Enter opened Chat, and closing Chat returned to the resting
surface. A further actual-input sample after final-build relaunch used Tab to
reach movement and Chat, Shift+Tab to return from Chat to movement while keeping
controls visible, and Escape to dismiss the controls in the settled accessibility
tree. No preferences were saved or chat message sent. These observations
strengthen the named-command/focus evidence but do not establish unpressed hover
travel, drag/exit, every keyboard/menu route, all avatar scales, or screen-reader
behavior.

## Source findings and scope

The old 260×352-DIP card has been replaced with a default 136×132-DIP transparent
window containing a 112×88-DIP character. Nominal window area changes from 91,520
to 17,952 square DIPs, approximately 80.4% less; character dimensions are 20% less
in each direction. Native dimensions may differ by half one physical pixel.
Disclosure reserves its space, so revealing controls does not move or resize the
HWND. The 124×42-DIP strip overlaps the avatar by 10 DIPs and accommodates three
32×32 targets without scaling those targets with the character.

Production event handlers separate keyboard navigation from mouse-derived focus.
Mouse input clears keyboard mode; Escape clears it before returning focus to the
character; deactivation clears it; an open menu keeps controls reachable. Hidden,
disabled, minimized and closed states gate disclosure. Controls use system colors
and explicit names, IDs and focus borders. No polling, hover timer, continuous
decorative animation, model/backend dependency or additional action authority was
introduced. Existing drag/arrow movement, command dispatch, recovery and settings
ownership remain in their previous layers.

The name/state/movement fields now provide tooltip and automation metadata; storage
and shortcut notices additionally appear as a small badge and menu text without
resizing the companion. Collapsed TextBlocks with LiveSetting do not by themselves
prove useful spoken announcements. Full assistive-technology qualification remains
pending. No unresolved critical defect was found in this bounded review.

## Category scores

C1–C5 correspond in order to the five criteria frozen above. A 1 records partial
evidence; it is not converted to a pass because a related source or policy test
passed.

| Category | C1 | C2 | C3 | C4 | C5 | Total | Evidence and remaining limitation |
|---|---:|---:|---:|---:|---:|---:|---|
| Code Correctness | 2 | 2 | 2 | 2 | 1 | 9 | Strict build and native dimensions/policy/dispatch pass; complete adverse physical event-routing matrix is pending. |
| Architecture | 2 | 2 | 2 | 2 | 2 | 10 | Presentation-only change; renderer, placement/storage, disclosure ownership and local-model boundaries remain separate. |
| Mathematical Correctness | 2 | 2 | 2 | 1 | 2 | 9 | Independent dimensions, pixel quantization, target sizes and stable bounds verified; physical work-area evidence covers one display. |
| Testing | 2 | 2 | 2 | 2 | 1 | 9 | 224 tests, composed/native regression, 51 reviewer-authored checks and actual input sample; pointer/accessibility coverage remains incomplete. |
| Reliability | 1 | 1 | 2 | 2 | 2 | 8 | Painted continuity and policy cases pass; unpressed crossing and drag/exit need real input. Keyboard/menu, lifecycle and status regressions have executed evidence. |
| Security | 2 | 2 | 2 | 2 | 2 | 10 | Hidden controls are absent from resting hit/automation targets; guarded recovery and existing privacy/action boundaries remain intact. |
| Performance | 2 | 2 | 2 | 1 | 2 | 9 | Event-driven bounded presentation adds no animation/polling; no latency or resource benchmark was rerun, and the historical failed qualification is retained. |
| Maintainability | 2 | 2 | 2 | 1 | 2 | 9 | Small explicit implementation and retained evidence; obsolete card-size assumptions corrected. Spoken status/help behavior still needs qualification. |
| UX/UI | 2 | 2 | 1 | 2 | 1 | 8 | Character-only rendering, compact dimensions and keyboard/named targets verified. Complete hover and accessibility/theme matrix remain partial. |
| Requirement Compliance | 2 | 2 | 2 | 2 | 2 | 10 | Requested surface implemented, real chat/settings access retained, concurrent independent QA performed, broader scope and limits preserved. |

Arithmetic: **9 + 10 + 9 + 9 + 8 + 10 + 9 + 9 + 8 + 10 = 91**;
**91 / 10 = 9.1/10**. The performance category assesses this bounded presentation
change and evidence discipline; it does not change the Phase 2 resource score,
failed measurements, fixed budgets, or release verdict.

## Top 5 ranked fixes / improvements

1. **Complete real pointer qualification.** Problem: unpressed hover entry,
   avatar-to-button travel, and drag followed by pointer exit were not exercised
   by the supported helper. Why it matters: native hit routing can differ from a
   direct policy call or rendered center path. Proposed fix: perform the recorded
   physical trajectories at 75%, 100% and 150%, retaining visible outcomes and
   correcting any flicker or focus latch discovered.
2. **Complete keyboard and assistive-technology qualification.** Problem: the
   executed Tab/Shift+Tab/Enter/Escape sample does not cover all controls and
   context-menu keyboard invocation, high contrast or spoken state/error changes. Why it
   matters: hidden actions and tooltip-only details must remain discoverable.
   Proposed fix: execute that matrix and add explicit live-peer announcements if
   actual screen-reader behavior shows the current metadata is insufficient.
3. **Resolve the existing resource failure before release.** Problem: prior
   visible-idle CPU qualification failed. Why it matters: a smaller
   window is not proof that the fixed idle budget is met. Proposed fix: follow
   the existing profiling and predeclared qualification sequence in Phase 12;
   retain every outcome and do not select a favorable run to replace a failure.
4. **Complete physical multi-display placement qualification when available.**
   Problem: only one display is available. Why it matters: compact geometry and
   control accessibility still require mixed-DPI, disconnect and work-area edge
   verification. Proposed fix: retain the mandatory physical matrix for suitable
   hardware; do not mark it passed from synthetic or single-display cases.
5. **Retain separate assertion artifacts for each native restart.** Problem:
   `verify-ui.ps1` retains both completion-stage sequences but overwrites the
   first run's JSON/PNG files on the second run. Why it matters: separate outcomes
   make future intermittent layout/input defects easier to audit. Proposed fix:
   introduce per-run evidence subdirectories or archive each run before restart
   when that harness next changes; current stage/check evidence remains valid.

## Regression risks and required later checks

Changing hover event ownership, toolbar overlap, focus clearing, or Visibility
semantics can reintroduce flicker, permanent controls after a drag, or invisible
hit targets. Keep hidden controls out of hit testing, preserve exact command
dispatch and recovery guards, and keep disclosure independent of HWND size.
Adding names/status presentation must retain lifecycle suppression after shutdown
and must not turn warnings into uncontrolled window growth.

Current strict/native checks and the recorded real-input sample are complete for
their stated scopes. The remaining pointer/accessibility/multi-display matrix and
resource qualification are required before release. Remaining Phase 3 persistence,
conversation and rich-rendering requirements are unchanged.

## Final verdict

**PASS — compact companion surface development increment, 9.1/10.** The inspected
source, retained regression/native evidence, rendered pixels and bounded real-input
sample support checkpointing this requested change. Root may finalize the source
checkpoint and synchronize the verdict in project status documents without an
unchanged test rerun. This does not approve Phase 4 advancement or product release.

**Full Phase 3 remains incomplete. Resource qualification remains FAIL; release
remains blocked.**
