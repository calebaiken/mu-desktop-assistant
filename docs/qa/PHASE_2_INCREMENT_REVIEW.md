# QA REVIEW

Phase: 2 — first appearance and desktop-movement increment, following `532879c`.

Overall Score: Not assigned; this is an implementation checkpoint, not the Phase 2 gate.

Result: INCOMPLETE for Phase 2. No advancement to Phase 3 is authorized by this review.

Reviewer: independent `phase2_review` agent, 2026-09-22. The reviewer inspected the
production changes, authored six additional settings recovery cases, and reported
input/accessibility and minimized-placement findings. The reviewer did not author
production code, operate the user's UI, or change Windows preferences. Build/process
execution below is attributed separately from independently inspected evidence.

## Scope and category observations

This increment adds validated scale/opacity preferences, strict in-memory schema 1 to
2 migration, explicit settings controls, restrained idle motion, a mouse/keyboard
movement surface, pure physical-pixel placement rules, and an owned-window Win32
placement adapter. Tray/summon recovery, startup preference, click-through, persisted
position, full avatar states, measured idle performance and the physical monitor/DPI
matrix remain unfinished. A completed subset cannot establish the full Phase 2 score.

The ten categories are therefore observations, not numerical scores. A full gate must
still supply the five scored criteria per category and strict arithmetic required by
`docs/QA_PROCESS.md`.

| Category | Inspected evidence and limits |
|---|---|
| Code Correctness | Preference ranges reject nonfinite values; schema migration preserves legacy fields/defaults; placement avoids invalid clamp ranges; WPF placement is guarded against minimized/closed windows. Execution evidence is recorded below. |
| Architecture | Pure geometry stays in Core, Win32 access in Infrastructure, and input/layout/composition in Desktop. Production gains no package dependency. Native movement accepts an owned HWND and does not add general desktop automation. |
| Mathematical Correctness | Physical-pixel coordinates support negative monitor positions; snap thresholds are inclusive with nearest-edge/tie rules; oversize behavior is explicit; invalid/nonfinite/overflow inputs have boundary cases. Real mixed-DPI behavior cannot be inferred from pure arithmetic tests. |
| Testing | Author tests cover appearance persistence/migration and placement boundaries. Six reviewer-authored cases cover recovery through a legacy backup and malformed schema-2 appearance values. Programmatic HWND smoke is distinct from user-input or screen-reader testing. |
| Reliability | Settings load remains read-only until explicit save; known-good backups and quarantine remain in use. Placement callbacks coalesce, recheck state, and avoid minimized coordinates. System-preference and native hooks are removed on close. Display disconnect during actual dragging remains untested. |
| Security | Closed legacy field allowlist prevents new fields being smuggled into schema 1. Duplicate/unknown/wrong-type settings remain rejected; later schemas remain protected. The increment introduces no external service, credentials, capture, startup registration, or new elevation requirement. |
| Performance | Idle motion requests 20 fps and stops for reduced-motion preference, Windows animation preference, high contrast, hidden/minimized/disabled/closed state. No measured CPU/private-bytes/startup/compositor baseline is available. |
| Maintainability | Placement policy is independent of native APIs; new contracts explain units, edge snapping and oversize behavior. Smoke assertions persist named outcomes. Final source/status synchronization is owned by the implementation agent. |
| UX/UI | Scale leaves button targets at their usual size; opacity honors high contrast; settings include labels and explicit save. The movement surface now has a system-color focus visual and a real automation peer. Actual keyboard traversal, screen-reader behavior, physical dragging and scaled-settings usability still need interaction evidence. |
| Requirement Compliance | Useful Phase 2 functionality is implemented, but the roadmap's recovery, state, monitor and performance exit criteria are incomplete. This report preserves that distinction and cannot replace the later phase gate. |

## Evidence

The reviewer independently read the changed production files, new tests, manifest,
verification scripts, Phase 1 report and Phase 2 roadmap/UX requirements. No production
file was changed by this reviewer. `git diff --check` exited 0 during inspection;
Git emitted only expected line-ending normalization notices.

An independent no-window reflection check loaded `PresentationFramework` and examined
the original focusable `Border`: `OnCreateAutomationPeer` was inherited from
`System.Windows.UIElement`, and `UIElementAutomationPeer.CreatePeerForElement` returned
null despite an attached accessible name. The command exited 0. This reproduced the
missing-peer issue without UI automation. The implementation now uses
`src/Companion.Desktop/Controls/MovementSurface.cs`, overriding peer creation with a
named, keyboard-focusable pane. The real-window smoke now checks that peer's name,
automation ID and help text.

Reviewer-authored `tests/Companion.Tests/IndependentAppearanceTests.cs` adds six cases:

- A damaged primary plus valid schema-1 backup loads a schema-2 in-memory value without
  rewriting either file. Explicit repair preserves the damaged bytes in quarantine,
  saves a migrated known-good backup and survives a new store instance.
- Duplicate scale, null opacity, string scale, Boolean opacity and incorrectly cased
  scale properties in schema 2 each recover the legacy backup without altering files.

Execution results and independently reread artifacts are pending the implementation
agent's sequential validation run. No unexecuted case is counted as passed here.

## Findings addressed during review

1. **Movement focus indication:** the first inspected movement `Border` had no explicit
   focus visual. The implementation added a dashed system-highlight focus style.
2. **Missing automation peer:** attached automation properties on a plain `Border` did
   not expose the new movement action. A custom movement surface now supplies a real
   peer; smoke checks inspect peer behavior rather than only attached metadata.
3. **Minimized placement:** queued placement previously accepted an `IsVisible`
   minimized HWND. Guards now require normal window state both before queueing and
   before constraining, and restoration queues a fresh placement. Smoke checks cover
   minimized motion and restoration-position preservation.

## TOP 5 RANKED FIXES / IMPROVEMENTS

1. **Complete recoverable desktop modes before the Phase 2 gate.**
   Problem: tray/summon recovery, startup preference, click-through, saved position and
   complete avatar states remain deferred.
   Why it matters: the full roadmap promises usable recovery and state transitions.
   Proposed fix: implement and independently test those behaviors, including shortcut
   conflicts and recovery from hidden/click-through modes, before phase advancement.

2. **Execute the physical monitor and user-input matrix.**
   Problem: arithmetic and programmatic HWND checks do not establish actual dragging,
   input thresholds, capture loss, keyboard traversal, visible focus or mixed-DPI moves.
   Why it matters: WPF/native-coordinate and input behavior can fail despite unit passes.
   Proposed fix: record negative-coordinate, 100/150/200% DPI, work-area/taskbar,
   disconnect/reconnect, drag/cancel, restore and screen-reader scenarios on appropriate
   hardware without changing an unrelated user session's preferences.

3. **Keep settings actions reachable on smaller scaled work areas.**
   Problem: the initial settings height is 650 DIP, which can exceed a 1080p work area
   at 200% scaling; a content ScrollViewer alone does not constrain the outer window.
   Why it matters: Save/Close can start outside the visible work area.
   Proposed fix: bound initial settings dimensions to the target monitor's usable area
   and test the smallest supported layout. This is a source/layout risk, not a claimed
   physical 200% reproduction.

4. **Measure the animation resource budget.**
   Problem: the capped motion and lifetime guards have no real resource baseline.
   Why it matters: a transparent desktop window should remain inexpensive when idle.
   Proposed fix: retain repeatable Release CPU/private-bytes/startup/render measurements
   for animated, reduced-motion, hidden and minimized states on declared hardware.

5. **Define cross-monitor keyboard movement.**
   Problem: `MoveBy` deliberately constrains the requested position to the current
   monitor's work area, so arrows cannot transfer the companion to another monitor.
   Why it matters: keyboard reachability should eventually cover equivalent desktop
   placement actions.
   Proposed fix: document the current limitation and add an explicit next-monitor
   keyboard action or a tested monitor-selection policy in a later increment.

## Regression Risks

- Relaxing the legacy allowlist or new-field validation can turn malformed data into
  accepted preferences; migrations must not silently rewrite files during load.
- Reintroducing native placement while minimized can use icon/minimized coordinates
  rather than normal restored bounds.
- Scaling the entire control tree can shrink keyboard/button targets; current scaling
  intentionally changes the avatar and window dimensions.
- Changing DPI/coordinate policy requires both pure geometry regression and actual
  monitor evidence; the manifest alone does not prove correct scaling.
- Attached automation metadata alone is insufficient for elements without peers.

## Tests Required

Complete the current sequential build, discovered tests, architecture checks,
programmatic appearance/process checks and negative controls. After relevant fixes,
rerun the affected checks. The ranked physical interaction, monitor, accessibility and
performance scenarios remain requirements for the later Phase 2 gate. No remote CI,
screen-reader conformance, actual mouse/keyboard automation or physical DPI matrix is
claimed by this checkpoint.

## Final Verdict

Pending execution evidence, no unresolved critical source-level defect has been found
in the implemented appearance/movement subset after the fixes above. Phase 2 remains
incomplete and unscored. This report does not authorize advancement to Phase 3.
