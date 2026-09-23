# Compact avatar implementation evidence — 2026-09-22

Scope: user-requested character-only companion with hover controls. No model,
provider, storage format or permissions change. The default character is 112×88
DIPs; the stable window is 136×132 DIPs. Chat, Settings and More use 32×32-DIP
targets independent of avatar scale. Keyboard navigation also reveals controls.

## Build and regression

`scripts/verify.ps1` completed under normal user permissions: strict Release build,
zero warnings/errors, 11/11 architecture checks, 224/224 tests with no skips,
and composed WPF startup/shutdown smoke. Retained TRX:
`artifacts/qa/compact-avatar-verification/release-224.trx`.

The earlier restricted-sandbox run had 197 passes and 27 permission failures in
isolated atomic-file/registry/IPC fixtures. Its report remains at
`artifacts/qa/compact-avatar-verification/sandbox-restricted.trx`. Tests were
rerun with the real normal-user capabilities; no fixture checks were weakened.

Reviewer-authored native checks cover logical dimensions at 75/100/150% scale,
physical-pixel rounding, stable HWND bounds, 32-DIP buttons, automation names,
alpha-transparent resting strip, continuously painted center path onto controls,
hidden hit testing, direct disclosure-policy transitions, warning metadata,
disabled/hidden/minimized/closed gates and isolated routed command dispatch.

The first native run failed an over-strict height oracle. A diagnostic-only rerun
recorded 110.4 DIPs at 125% DPI for a nominal 110-DIP window: 137.5 physical pixels become
138 pixels. The independent oracle now allows at most half a physical pixel for
HWND dimensions, while retaining exact avatar dimensions. Both failed runs remain:
`artifacts/qa/desktop-ui-20260922-203016-f25504c5` and
`artifacts/qa/desktop-ui-20260922-203347-4675cd13`.

Final native evidence: `artifacts/qa/desktop-ui-20260922-203534-52642fba/profile`.
Both runs passed: 20/20 process controls, 51 compact surface, 43 appearance,
24 integration, 29 settings lifecycle, 49 placement and 54 chat assertions;
no reported failures or skips. Default-scale
`compact-avatar-idle.png` and `compact-avatar-hover.png` were inspected. These
are 96-DPI WPF content renders, not desktop screenshots or physical pointer tests.

## Actual input observations

The supported computer-use helper operated the real application at the user's
saved 75% avatar scale on one 125%-DPI display. Idle capture showed only the character.
Clicking the character exposed all three buttons. More opened its actual menu and
Escape dismissed it. Settings opened from the strip. Moving the pointer to Settings
and closing that window returned the companion to its resting view; mouse-derived
focus did not latch the controls. Tab reached the movement surface; the next Tab
revealed and focused Chat; Enter opened Chat. Closing Chat returned to the resting
surface. After the final build restarted, Shift+Tab returned from Chat to movement
with controls visible, and Escape hid them in a settled accessibility snapshot.
These checks submitted no chat messages and saved no preferences. The final build
was left running with the installed local model directory and existing settings.

Exact action record: `artifacts/qa/compact-avatar-verification/interactive-input.md`.
Native screenshots and accessibility snapshots are retained in the tool transcript.
The helper has no unpressed pointer-move action: this sample does not certify pure
hover entry/travel at all scales. Full drag, keyboard, high-contrast, screen-reader
and multi-monitor qualification remains separate. A second monitor is unavailable.

The existing failed resource qualification remains a release blocker. This bounded
presentation increment does not complete Phase 3 or approve the product for release.
See the independent [compact surface review](COMPACT_AVATAR_REVIEW.md).
