# Request traceability

Source: user's attached project brief received 2026-09-21. Initial workspace inspection
found no files, Git repository or ancestor AGENTS.md; no existing code was removed.
The brief's initial deliverable explicitly starts with Phase 0. The table maps the
entire vision so deferred requirements remain visible and testable.

| ID | Requirement group | Design reference | Implementation / acceptance owner |
|---|---|---|---|
| R01 | Windows-first maintainable C#/native/optional science stack, modular IPC and Git-friendly build | ARCHITECTURE technology/dependencies | 0 architecture/build; 12 packaging; C++ only after profiling |
| R02 | Transparent borderless avatar, drag, size/opacity, topmost/click-through, snap/multi-monitor/DPI | ARCHITECTURE avatar | 1–2; monitor/DPI/recovery matrix |
| R03 | Hide/summon, tray, startup preference, click/double/right click, hotkey, popup/mic/palette | ARCHITECTURE avatar; UX_SPEC | 2–4; keyboard/focus/tray/hotkey tests; palette in 8 |
| R04 | Idle/speaking/listening/thinking/working/notification/rest states | ARCHITECTURE avatar/voice | 2/4; transition and cancellation tests |
| R05 | Avatar/body/face/eyes/hair/clothes/accessory/color/scale/animation/expression/bubble/theme/name customization, 3D extension | ARCHITECTURE asset manifest; UX_SPEC | 2 default; 5 name/expression preferences; 10 packs; 3D future |
| R06 | Chat history/stream/Markdown/code/highlight/math/LaTeX/copy/regenerate/stop/edit/title/search/new/private | ARCHITECTURE conversation; CONTRACTS | 3; provider/rendering/database/UI tests |
| R07 | Short bubbles versus long panel, unobtrusive persistent character | ARCHITECTURE avatar; UX_SPEC | 2–3; no focus theft, long-text containment |
| R08 | Provider/model/endpoint/key/temperature/tokens/reasoning/personality/permissions config, future local and other providers | CONTRACTS; ARCHITECTURE providers | 3/5/7; capability validation; credential/redirect checks |
| R09 | Push-to-talk, VAD/STT/TTS/playback, voice activation/mute/cancel/speed/voice/devices, feedback suppression | ARCHITECTURE voice | 4; audio fixtures and device matrix |
| R10 | All personality fields separate from security and software rules | ARCHITECTURE personality; UX_SPEC | 5; round trip and policy independence |
| R11 | Conversation/working/approved/project/preference memory; inspect/edit/delete/disable/private/project controls | ARCHITECTURE memory; CONTRACTS | 3/6; scope/approval/deletion/recovery tests |
| R12 | Tool ID/description/schemas/permissions/validation/errors/audit; four risk levels; model proposes/app authorizes | CONTRACTS; SECURITY | 7; grant replay/change/race/deny cases |
| R13 | Open/inspect/search/create/rename/move files, app launch, scripts, clipboard, notes/reminders, web/calendar/email/Git/projects | ARCHITECTURE tools; ROADMAP | 8 initial subset; remaining explicit extension backlog with individual gates |
| R14 | Opt-in active app/window/selection/clipboard/screenshots/vision and visible capture indicators | SECURITY; CONTRACTS | 11 permission surface; context adapters future reviewed capabilities |
| R15 | Shared task/command representation and primary/planner/executor/runtime QA roles | CONTRACTS task plan; ARCHITECTURE agents | 3 primary; 7 planner/executor; 9 runtime numerical QA |
| R16 | Scientific math, symbolic/numeric/dimensional/unit/uncertainty/plots/physics/statistics/code analysis; deterministic verification | ARCHITECTURE science; TESTING numerical QA | 9 and future science increments; independent oracles |
| R17 | Mandatory independent concurrent development QA, ten categories, exact report, top five, strict gate/fix loop | QA_PROCESS | Every phase; never infer from a successful build |
| R18 | Automated unit/integration/UI/permission/DB/serialization/math/provider/recovery tests and specified adverse scenarios | TESTING; ROADMAP | Every owning phase; release regressions in 11–12 |
| R19 | Structured application/diagnostic/error/audit logs, useful errors, no secrets | SECURITY; ARCHITECTURE reliability | 1 logs; 7 audit; 11 adversarial redaction |
| R20 | Least privilege/injection/path/URL/shell/document/serialization/credential/destructive-action defense | SECURITY matrix | Enforced at each capability; full hardening 11 |
| R21 | Light idle resource use, async nonblocking UI, cancellation, bounded work, measured performance | ARCHITECTURE performance; TESTING | 2 baseline; 3/4 resource tests; 12 profile |
| R22 | Polished accessibility, keyboard/reduced motion/state clarity/sensible defaults/progressive disclosure | UX_SPEC | 1–5/10; UI Automation and manual checks |
| R23 | Required docs/status/QA history/known bugs/next objective/decisions/debt/changelog | Root docs; QA_PROCESS | Every checkpoint; independent consistency check |
| R24 | Installer, update architecture, source checkpoints, extensibility and documentation | ARCHITECTURE packaging; ROADMAP | 0 local checkpoint; 12 distribution |

## Phase 0 acceptance checklist

1. Inspect workspace, record preservation decision and available toolchain.
2. Define whole-system architecture, stack choices and explicit layer dependencies.
3. Specify provider/conversation, avatar/rendering, tool/permission and memory contracts.
4. Specify independent QA process and testing/numerical/security strategy.
5. Create buildable project structure and all seven requested root documents.
6. Run genuine restore/build/checks plus Windows/XAML smoke; record limitations.
7. Independent reviewer inspects, calculates ten-category score and supplies top five.
8. Fix/retest/review until strictly >8.5; record source checkpoint or precise blocker.

No Phase 0 requirement implies completed chat, voice, avatar rendering or other Phase
1–12 features. Passing this checklist approves the foundation, not the whole product.
