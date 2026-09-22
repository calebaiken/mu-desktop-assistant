# Implementation roadmap

This is a staged delivery plan, not a list of completed capabilities. **Phase 0 passed
at 9.4/10; Phase 1 independently passed at 9.0/10. Phase 2 is next.** Every phase gets an independent QA report;
none advances with a score of 8.5 or lower. Cross-cutting security is designed now,
implemented with each feature and adversarially tested again in Phase 11.

| Phase | Delivery | Exit evidence (in addition to independent score >8.5) |
|---|---|---|
| 0 | Requirements, architecture, threat model, contracts, repo and minimal Windows build | Clean strict build; architecture and real-XAML smoke checks; documented limitations, traceability and independent QA |
| 1 | Composition root, lifetime/single instance, settings, structured logging, basic companion window, standard test framework | Atomic settings round trip/corrupt recovery/full disk; log redaction; startup/shutdown/cancellation; real visible launch and accessible close |
| 2 | Default 2D avatar, animation states, drag/snap/DPI, scale/opacity/topmost/click-through, tray/hotkey/startup preference | Mixed-DPI/negative-coordinate/disconnected monitor matrix; recover from hidden/click-through; hotkey conflict; state transitions; idle baseline |
| 3 | Provider port + first real provider, bounded streaming, history/branches/search/private mode, complete chat rendering/UI | Scripted HTTP normal/malformed/timeout/offline/cancel cases; key/endpoint validation; real SQLite and temporary sentinel; Markdown/code/LaTeX/XSS; user-configured live check |
| 4 | VAD, push-to-talk/STT/TTS, playback synchronization, optional activation, device/voice/speed settings | Audio fixtures + physical device matrix; denied/unavailable/unplugged input/output; mute and stop; no feedback loops; no raw-audio persistence |
| 5 | Personality editor and prompt composer, presets, progressive disclosure | Every personality setting round trips; malformed config recovery; personality cannot change permission policy; keyboard/high contrast UX |
| 6 | Inspectable/editable/deletable approved memory, project scopes, disable/private behavior | Approval provenance, optimistic edit conflict, scope isolation, migration/backup/recovery, delete/index cleanup and temporary crash tests |
| 7 | Registry/schema/risk/capability enforcement, trusted consent, bounded task planner, audit | Unknown/malformed/denied/replayed/expired/changed-target cases; cancellation races; audit failure; tool loop limit; no unrestricted OS control |
| 8 | Notes, timers/reminders, tasks, selected-file text inspection and tightly allowlisted app launch | Durable restart semantics, DST/time changes, bounded file handling, actual tool integration and approved side-effect tests |
| 9 | Scientific expression engine, units/dimensions/uncertainty, symbolic worker and plot boundary where justified | Independent oracles, domains/nonfinite/overflow, affine units, covariance, plot limits; worker isolation before execution |
| 10 | Layered customization/pack import, expressions/themes/bubbles and asset tooling | Full default companion, manifest/version limits, malformed/huge assets/path escape, license inventory, reduced motion and accessibility |
| 11 | Security hardening, privacy/deletion/export, fault recovery and optional context permission surfaces | Cross-feature injection and exfiltration tests; crash/full-disk/migration; temporary sentinel; revoke all context; no secrets in exports |
| 12 | Profiling, installer/update architecture, release docs and distribution readiness | Declared reference-machine budgets, clean-user install/rollback/uninstall, signatures/advisories/licenses, recovery drill and full regression |

Core ports for later phases are specified now; their production implementations remain
deferred. Minimal SQLite conversation storage belongs in Phase 3, with advanced memory
storage in Phase 6. Permission boundaries are mandatory before any Phase 8 tool ships.
Deterministic calculation introduced earlier, if needed, inherits Phase 9 numerical QA
immediately; an LLM-only calculator never counts as complete.

First useful release candidate follows Phases 1–8 with validated simple calculation,
then technical depth/customization and release hardening. A phase pass is not automatic
release approval. The full feature set requires all relevant gates and a release review.

## Extension backlog

Richer documents, web research, file mutation, clipboard, approved scripts, Git, project
management, calendar/email, active-window/selected-text/screen context, local models,
additional providers, embeddings, full-duplex voice, C++ hot paths, 3D avatars and ARM64
are separate capability increments. Each must use existing ports, have scoped permissions
and a named QA review; disabled capability scaffolds are not working features. Choose
order based on use and measured need rather than expanding the initial release silently.
