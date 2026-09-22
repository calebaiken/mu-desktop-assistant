# Phase 2 remaining desktop validation

All rows below are **pending actual interaction**. Programmatic smoke and pure math
are separate evidence, not substitutes. Use an isolated profile and a clearly named
QA companion. Do not change an active user's display, accessibility or startup
preferences to satisfy this matrix; use appropriate test hardware/disposable accounts.
Record OS/build, monitor layout/work areas, scale factors, executable revision, exact
actions, observed result and any artifact path for each row.

Availability recorded 2026-09-22: this machine exposes one display at 125% scale,
and the user confirmed that no second monitor is available. The supported native
interaction helper also fails window discovery with missing-pipe OS error 2.
Physical monitor and actual input rows therefore remain unexecuted. Neither pure
geometry tests nor programmatic window checks change those rows to passed.

| Scenario | Actions | Expected result |
|---|---|---|
| Keyboard placement | Tab to Move companion; arrow and Shift+arrow; Tab onward | Visible focus; moves by 10/40 DIP within current work area; Settings/Exit reachable |
| Drag threshold/capture | Click without motion; drag across threshold; release; interrupt capture | No jump on click; tracks drag; snaps within 12 DIP; no stuck capture |
| Saved movement | Move, immediately Hide, Exit from tray, restart same profile | Last normal-state move restored despite debounce/hidden state |
| Actual summon shortcut | Choose available preset, hide through menu, invoke keys | Same companion reappears and becomes interactive; no second instance |
| Shortcut conflict | Occupy a preset with a test app; save conflicting selection | Clear persistent error; prior working shortcut retained; tray recovery works |
| Click-through | Enable and Save; test pointer passthrough; summon/Show | Underlying app receives clicks; recovery clears click-through and refreshes checkbox |
| Tray actions | Open tray menu; Show/Hide/Settings/Sleep/Wake/Exit | Matching visible state; sleeping has no animation; Exit releases tray/hotkey |
| Mixed DPI | Move between 100%, 150%, 200% displays in positive/negative coordinates | No inaccessible drift; appropriate size; correct work-area snap and restore |
| Monitor removal | Save on secondary display; detach; restart; reconnect | Surviving-monitor fallback visible; no off-screen trap or native failure |
| Work-area changes | Taskbar on each edge; resize work area; use largest scale | Companion clamps; settings Save/Close remain reachable; tray recovery works |
| Small/scaled settings | Open and resize to minimum on smallest supported work area | Outer window fits; content scrolls; action buttons and errors visible |
| Accessibility | Keyboard-only flow, screen reader, high contrast, reduced motion | Named controls/text states; focus visible; no color-only status; motion policy honored |
| Startup in disposable account | Enable, sign out/in, disable, sign out/in | Correct executable launches once only when enabled; failure messages truthful |

After restoring the test machine's prior configuration, run the automated regression
suite and attach the independent QA assessment. Report unavailable hardware/configurations
as pending; never infer their outcome from coordinate calculations alone.

Resource follow-up uses `scripts/measure-desktop.ps1` under a normal user token. The
window is labeled and input-disabled during measurement; closing it cancels the run.
Reject contaminated results. Record one-core and machine-normalized CPU separately,
actual animation flags and private bytes. Compositor/GPU cost, frame-time/startup p95
and extended hide/show resource stability need separate measurement; the three-interval
harness does not certify those properties.
