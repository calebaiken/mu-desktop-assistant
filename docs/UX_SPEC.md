# Companion interaction design

Phase 0 specification; the foundation window is only a build check.

Default product name is a working repository name; companion name is user-editable.
Begin with one friendly, simple 2D character, restrained movement, medium scale, full
opacity, no startup registration, no microphone activation and no desktop awareness.
No generated artwork is needed until the rendering/asset phase.

Single click opens a compact input bubble; double click opens the conversation panel;
drag moves the companion without accidentally sending a click. Right click exposes
Talk, Open chat, Appearance, Mute, Sleep, Hide and Exit. Tray and a configurable summon
shortcut recover from hide/click-through. Initial shortcut candidate Ctrl+Alt+Space
must be registered successfully or show a conflict; do not steal an occupied shortcut.
Escape dismisses transient surfaces; a generation's Stop control remains distinct from
closing the panel. Keyboard users can reach the same actions through tray/palette.

The avatar may show a short response (target at most 240 characters, 3 lines) with an
Open chat action; longer content stays in chat. Notifications do not take keyboard focus.
Chat lays out a searchable conversation list, transcript, composer, Attach, microphone
and Send/Stop. Edit/resubmit/regenerate creates visible branches rather than silently
destroying prior messages. Show partial output and interrupted status. Preserve unsent
drafts only in normal mode with explicit documented storage behavior.

Top-level settings: Appearance, Personality, Voice, Models, Memory and Privacy.
Advanced provider/token/reasoning and capability controls expand on demand. Destructive
actions show concrete resources and consequences. The trusted consent dialog never
renders model HTML and cannot be triggered as an implicit acceptance by closing it.

Visible labels accompany listening, thinking, working, speaking, muted and error states;
do not rely on color or animation alone. Screen reader announcements are throttled and
do not read every stream token. Standard focus rings, logical tab order, high contrast,
scalable text, reduced motion and controls with at least 32 DIP hit targets are initial
design criteria. Exact theme contrast and control layout require measured implementation
checks, not a Phase 0 accessibility conformance claim.

Memory view shows each fact's content, source, approval, category and project scope,
with edit/delete/disable controls. Temporary mode is unmistakably labeled, explains
remote processing, and disables persistence-dependent actions. File attachments show
what will be sent before provider submission. Microphone listening is always visible;
mute cancels capture immediately and cancels any pending voice submission.

Failure UX: retain user input on provider errors, offer bounded retry, explain missing
credentials/device permissions plainly, show storage recovery without deleting data,
and distinguish cancellation from success. No unsupported feature buttons ship as if
functional; phase releases expose only implemented, tested actions.
