# Implementation roadmap

This is a staged delivery plan, not a list of completed capabilities. **Phase 0 passed
at 9.4/10; Phase 1 independently passed at 9.0/10; Phase 2 development passed at 8.7/10.** Every phase gets an independent QA report;
none advances with a score of 8.5 or lower. Cross-cutting security is designed now,
implemented with each feature and adversarially tested again in Phase 11.

**2026-10-19 priority: modules A, B, C, G, I.** The first candidate implements only
A's selected plain-text read with I's adversarial policy/lifecycle tests and an actual
WPF panel. Native Windows and independent qualification remain pending. Follow with
PR #1 review/integration and explicit project identity/Chat→Lab approval reuse (B),
approved project SQLite memory (C), then independently qualified model profiles and
workload admission (G/I). See [the audited dependency sequence](docs/AGENTIC_EVOLUTION_AUDIT.md).
This does not deliver the larger roadmap phases or any unselected module.

Phase 3's first **local pretrained chat increment passed at 9.0/10**. This approves
the bounded in-memory chat slice. History, branching/search, separate temporary mode,
exact token admission and native Markdown/code/math now pass the
[Phase 3 development review](docs/qa/PHASE_3_HISTORY_REVIEW.md) at **9.5/10**.
Phase 4A's [explicit local speech output](docs/qa/PHASE_4A_SPEECH_REVIEW.md) passes
independent review at **9.0/10**. This covers manual Read aloud/Stop and session
voice/speed controls; Phase 4B push-to-talk and the wider voice loop remain unfinished
under the [voice plan](docs/PHASE_4_VOICE_PLAN.md). See the
[independent increment review](docs/qa/PHASE_3_LOCAL_INCREMENT_REVIEW.md).

The user's 2026-09-29 priority is the integrated Mu Machine Learning Lab. Its phase
numbering is separate from the assistant roadmap below. The bounded foundation now
implements local inspection, supervised/unsupervised CPU fitting, metrics/plots,
saved models, prediction, exact-cohort comparison, historical detail, export and
reviewed local conversational proposals. Executed regression includes 353 .NET tests,
11 architecture checks, 57 native Lab checks and two live planning fixtures. This does
not complete all requested ML Phase 1 or authorize advancement by itself; the
[independent ML review](docs/qa/ML_LAB_FOUNDATION_REVIEW.md) owns that decision.

Next ML units include richer editable feature engineering, multidimensional scientific
data/waveforms, project/model lifecycle UX, fuller learning explanations, advanced
search/interpretability and scientific/deep-learning backends. Auto currently proposes
one reviewed action. GPU execution, pause/resume, notebook export and broad assistant
competence remain unqualified. Existing Phase 4B files are preserved, with the incomplete
Desktop partial excluded from compilation; microphone work is not counted as delivered.

| Phase | Delivery | Exit evidence (in addition to independent score >8.5) |
|---|---|---|
| 0 | Requirements, architecture, threat model, contracts, repo and minimal Windows build | Clean strict build; architecture and real-XAML smoke checks; documented limitations, traceability and independent QA |
| 1 | Composition root, lifetime/single instance, settings, structured logging, basic companion window, standard test framework | Atomic settings round trip/corrupt recovery/full disk; log redaction; startup/shutdown/cancellation; real visible launch and accessible close |
| 2 | Default 2D avatar, animation states, drag/snap/DPI, scale/opacity/topmost/click-through, tray/hotkey/startup preference | Independent geometry/storage tests; programmatic owned-window recovery/hotkey/state/lifetime checks; recorded resource baseline and independent QA >8.5. Historical budget failures stay explicit; resource and physical qualification remain mandatory release gates under the recorded sequence decisions. |
| 3 | Pretrained local model with owned worker, bounded streaming, history/branches/search/private mode, complete chat rendering/UI | Local worker normal/malformed/timeout/crash/cancel cases; pinned model/runtime provenance and offline checks; real SQLite and temporary sentinel; Markdown/code/LaTeX/XSS; unseen assistant evaluation before broad capability claims |
| 4 | VAD, push-to-talk/STT/TTS, playback synchronization, optional activation, device/voice/speed settings | Audio fixtures + physical device matrix; denied/unavailable/unplugged input/output; mute and stop; no feedback loops; no raw-audio persistence |
| 5 | Personality editor and prompt composer, presets, progressive disclosure | Every personality setting round trips; malformed config recovery; personality cannot change permission policy; keyboard/high contrast UX |
| 6 | Inspectable/editable/deletable approved memory, project scopes, disable/private behavior | Approval provenance, optimistic edit conflict, scope isolation, migration/backup/recovery, delete/index cleanup and temporary crash tests |
| 7 | Registry/schema/risk/capability enforcement, trusted consent, bounded task planner, audit | Unknown/malformed/denied/replayed/expired/changed-target cases; cancellation races; audit failure; tool loop limit; no unrestricted OS control |
| 8 | Notes, timers/reminders, tasks, selected-file text inspection and tightly allowlisted app launch | Durable restart semantics, DST/time changes, bounded file handling, actual tool integration and approved side-effect tests |
| 9 | Scientific expression engine, units/dimensions/uncertainty, symbolic worker and plot boundary where justified | Independent oracles, domains/nonfinite/overflow, affine units, covariance, plot limits; worker isolation before execution |
| 10 | Layered customization/pack import, expressions/themes/bubbles and asset tooling | Full default companion, manifest/version limits, malformed/huge assets/path escape, license inventory, reduced motion and accessibility |
| 11 | Security hardening, privacy/deletion/export, fault recovery and optional context permission surfaces | Cross-feature injection and exfiltration tests; crash/full-disk/migration; temporary sentinel; revoke all context; no secrets in exports; actual drag/focus/tray/hotkey/click-through, screen-reader/accessibility and sign-in qualification |
| 12 | Profiling, installer/update architecture, release docs and distribution readiness | Predeclared controlled repeat protocol: idle/hidden CPU strictly below 1% of one logical processor and sampled private bytes below 150,000,000, with presentation/occlusion, warmup and observer cost recorded and no overhead subtraction; tray/compositor costs and broader stability; clean-user install/rollback/uninstall, signatures/advisories/licenses, recovery drill and full regression; physical mixed-DPI/negative-coordinate/disconnected-monitor/work-area matrix and all deferred input qualification before release |

Core ports for later phases are specified now; their production implementations remain
deferred. Minimal SQLite conversation storage belongs in Phase 3, with advanced memory
storage in Phase 6. Permission boundaries are mandatory before any Phase 8 tool ships.
Deterministic calculation introduced earlier, if needed, inherits Phase 9 numerical QA
immediately; an LLM-only calculator never counts as complete.

The [2026-09-22 validation sequence decision](docs/qa/PHASE_2_VALIDATION_SEQUENCE.md)
records the user-confirmed lack of a second monitor and independent QA's rationale
for moving unavailable physical/input qualification to mandatory pre-release gates.
It preserves every unexecuted scenario and historical failure; no score credit or
resource limit changes. The original frozen experiment passed. Subsequent final-build
confirmation failed because both B runs had to pass and the first exceeded
CPU limits. The explicitly [post-result resource sequence decision](docs/qa/PHASE_2_RESOURCE_SEQUENCE.md)
allows Phase 2 development completion at **8.7/10**, placing unresolved resource
qualification in the user's original Phase 12 optimization scope. The experiment
remains FAIL; Performance stays 7/10 with zero budget-criterion points. No resource
limit, acceptance rule or failed result changes, and release remains blocked.

Phase 2 now implements appearance migration/controls, an eight-state vector renderer,
scoped activity arbitration, drag/keyboard movement, work-area snapping, durable
monitor-relative placement, tray/hotkey recovery, session click-through and explicit
startup registration, plus a deterministic next-display command (Ctrl+Shift+M).
The final-source build passes 196 tests and 11 architecture checks; native regression
passes 18 process controls, 43 appearance, 24 integration, 29 settings lifecycle and
49 placement-initialization assertions, including shutdown
during Save, hidden/minimized settings recovery and timed blink transitions. The
observed display is a single monitor at 125% scale; actual multi-monitor transfer and
physical input/accessibility remain unverified. Thirteen isolated-registry cases pass;
actual sign-in remains pending. Two controlled baselines of the earlier renderer pass
memory and fail all CPU modes against unchanged limits. A six-second blink passes
reduced/hidden CPU and memory, but visible CPU still fails at 1.093335%. Production
now uses a ten-second wait and 120 ms blink without animation clocks. Native regression
passes; its single clean baseline still fails visible CPU at 1.041400%, while reduced/
hidden CPU and memory pass. The cadence and resource limits are frozen; all prior
failures remain preserved. The predeclared A/B/B/A experiment compared the default
thread-snapshot observer with process counters alone, changing only the two boundary
snapshots per interval. All four runs passed CPU and memory on their original frozen
binary, without changing cadence, limits or overhead accounting. This does not prove
an isolated product optimization. Later readiness fixes disable input during placement
initialization and require a confirmed shortcut for Hide/click-through. Their strict
build/tests and native regression pass. Final-build B run 1 fails visible/reduced CPU
at 1.978694% / 1.015400%; run 2 passes at 0.755208% / 0.598859% / 0.364527%. Both hidden
intervals and all memory intervals pass, but the required aggregate remains **FAIL**.
The [final development review](docs/qa/PHASE_2_RESOURCE_REVIEW.md) passes at **8.7/10**.
Historical 8.0/8.6 failed exits and source checkpoint 6306ec9 remain preserved. Phase 3
has begun with the user's selected [pretrained local model path](docs/LOCAL_AI.md);
the first increment supplied in-memory plain-text chat; history and rich rendering
passed final development review at 9.5/10. Physical display/input/
accessibility, sign-in and unchanged resource qualification remain mandatory before release in Phases 11/12.
Implementation alone does not complete the phase.

First useful release candidate follows Phases 1–8 with validated simple calculation,
then technical depth/customization and release hardening. A phase pass is not automatic
release approval. The full feature set requires all relevant gates and a release review.

## Extension backlog

Richer documents, web research, file mutation, clipboard, approved scripts, Git, project
management, calendar/email, active-window/selected-text/screen context,
additional providers, embeddings, full-duplex voice, C++ hot paths, 3D avatars and ARM64
are separate capability increments. Each must use existing ports, have scoped permissions
and a named QA review; disabled capability scaffolds are not working features. Choose
order based on use and measured need rather than expanding the initial release silently.
