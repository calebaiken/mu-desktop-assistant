# Development status

## Current increment — explicit research projects (2026-10-19)

Branch `feature/research-projects` is stacked on approved-text-inspection commit
`376c3206a38fa86b1639e5f72c6a67cc57153cb4` (draft PR #2), with main still at `7fe3305`.
This is a bounded Module B project-selection increment with I's admission/recovery
tests. It adds explicit named local projects, stable generated identities, isolated
existing Lab services/artifacts, legacy default-project access without relocation,
and fresh views/proposals on project changes. Active/queued work blocks switching;
uncertain cleanup blocks a new service. No Chat content transfer or new computation
is implemented. See [workflow and safety boundary](docs/RESEARCH_PROJECTS.md).

Current Linux checks: strict cross-build zero warnings/errors, architecture 11/11,
project tests 50 passed/1 native skip of 51, prior-A regression 40 passed/3 native
skips of 43. Independent QA reproduces 39 portable passes/1 native skip of its 40
cases. Production Python-worker isolation passes actual training/listing/cross-project
denial/export and unchanged-source checks, and all 48 existing portable ML tests
pass (49 Python tests total). Native Windows CI and the independent phase verdict
remain pending for this candidate. The independent reviewer froze
[50 criteria](docs/qa/RESEARCH_PROJECTS_PROTOCOL.md) before review. Historical A
scores and receipts below remain intact; its pass does not approve this new phase.
The initial independent [Linux checkpoint](docs/qa/RESEARCH_PROJECTS_REVIEW_INITIAL.md)
scores 86/100 = 8.6/10 but explicitly **FAIL / incomplete phase exit** because
supported-platform gates and native behavior have not executed for this candidate.
No confirmed unresolved critical defect was found; the evidence requirement still
blocks phase advancement despite clearing the numeric threshold.

Next objective after verification: explicit Chat-to-project handoff reusing Lab's
captured selection and reviewed Run, then approved SQLite project memory and
separate model/resource qualification. D/E/F/H and PR #1 remain outside this change.

## Prior increment — approved text inspection (2026-10-19)

Source base: `main` at `7fe3305b49f755a94d65362692e783ef0e60035a`, clean checkout;
branch `feature/trusted-text-inspection`. Selected modules are A, B, C, G, I.
The [source and open-PR audit](docs/AGENTIC_EVOLUTION_AUDIT.md) records actual
capabilities, inconsistencies and dependencies. PR #1 remains separate and unmerged.

Implemented source workflow: More → Inspect text file → enable for window → select
one `.txt`/`.md` → exact metadata/limits preview → approve once → literal bounded
read → clear/disable. No model, Chat, memory or remote access is added. Typed ports,
single-use monotonic-expiry grants, Windows handle leases and fail-closed audit
form the first A slice, rather than an autonomous planner or larger module scaffold.
See [usage/security boundaries](docs/TRUSTED_TEXT_INSPECTION.md).

Current available evidence: Release cross-build 0 warnings/errors; architecture
11/11; selected new tests 40 passed, 0 failed, 3 Windows-only skipped (43 total),
independently reproduced. The pre-change Linux full gate reports 344 passed,
23 Windows-dependent failures, 31 skipped (398 total). The full candidate run before
the final additional Unicode path case had 383 passed, the identical 23 failing
test names and 34 skipped (440 total); all 48 portable ML tests pass. Native WPF and Windows
positive/negative gates are not certified by those Linux runs. Detailed current
receipts and independent review are tracked in Testing and docs/qa; the strict
score >8.5/zero-critical phase gate remains in force.

Initial independent QA failed at 6.9/10 during implementation; its report is preserved
at `docs/qa/TRUSTED_TEXT_INSPECTION_REVIEW_INITIAL.md`. Subsequent repairs cover
expired lease cleanup, noncooperative callbacks, terminal-audit revocation, late
cleanup ownership, repeated disposal and full Unicode Format checks. No independent
passing phase verdict or Windows delivery was claimed at that initial checkpoint.
The subsequent independent [Linux checkpoint](docs/qa/TRUSTED_TEXT_INSPECTION_REVIEW_LINUX.md)
scores **8.2/10, FAIL / incomplete phase exit**: available tests pass, but native
Windows behavior remains unexecuted. The missing evidence cannot receive passing credit.

The subsequent independent [Windows development review](docs/qa/TRUSTED_TEXT_INSPECTION_REVIEW_WINDOWS.md)
scores **94/100 (9.4/10), PASS for this bounded A development slice**, with zero
confirmed unresolved critical defects. It retains deductions for detailed reparse,
physical interaction, process-death and resource qualification; it does not approve
the larger selected modules or release. Both earlier failed reviews are preserved.

Subsequent native evidence supersedes that Linux-only evidence limitation: code
commit `a999ae4f59b16a9e435185726a647668a5aac51f` passes **2/2 Windows checks**,
[push run 39293630026](https://github.com/calebaiken/mu-desktop-assistant/actions/runs/39293630026)
and [PR run 39293637612](https://github.com/calebaiken/mu-desktop-assistant/actions/runs/39293637612).
The unchanged workflow executes both required scripts, including the production
reader/native-HWND WPF smoke and file/parent-directory sharing assertions. Detailed
logs/TRX redirect to a blocked storage host, so exact Windows counters and conditional
symlink skips are not claimed. Local final build remains zero warnings/errors and
the final 43-case selection is 40 passed/3 explicit Windows skips.

Remaining: detailed conditional reparse/hostile-swap evidence, native physical input/
accessibility and unchanged resource qualification. Then prioritize explicit project
identity and the existing reviewed ML proposal bridge (B), approved SQLite project
memory (C), and separately qualified model profiles/admission (G/I). D/E/F/H are
outside this increment. Historical model, neural playback, physical and release gaps
below remain unresolved. Machine-specific receipts below describe earlier candidates.

The 2026-09-30 optional neural-output increment adds Heart, Bella, Michael and
George through the existing Chat voice selector, plus explicit Preview. Its pinned
Kokoro CPU runtime/model are installed locally. With that runtime provisioned the
final .NET suite passes 398/398 with no skips. CI provisions a bare interpreter
at `artifacts/ml-runtime` and reaches the same 398/398, which confirms the
process boundary (see [AGENTS.md](AGENTS.md)).
Architecture 11/11, independent neural Python 21/21,
and native UI supervisor 50/50 with 94 speech checks in each retained run. Four
actual neural samples passed 21 native production-host synthesis checks and an
independent audio oracle. See [neural voice help](docs/MU_NEURAL_VOICES.md) and
[its separate qualification ledger](docs/qa/MU_NEURAL_VOICE_REVIEW.md) for playback,
privacy, retained failures and the final measured scope. This output increment
does not certify subjective naturalness, physical audibility or a full voice loop.
The final shortcut-targeted executable is rebuilt with the held-process Stop
repair, zero warnings/errors (`neural-voice-review/desktop-stable-final-build.txt`).
**Overall neural qualification remains INCOMPLETE:** the 33-case normal-Speak
rerun safely stopped at five checks with `memory_limit`, even after the user
reported other applications closed. Final completed reuse/disposal and Qwen
co-residency are not qualified. The current source/binary/evidence checkpoint is
`artifacts/qa/neural-voice-review/verified-source-manifest.json`; the earlier
identity manifest below is historical.

Preceding identity checkpoint, 2026-09-30: **Muon avatar customization, Galaxy theme and application icon/
desktop shortcut are implemented and pass their native development checks.**
That checkpoint's strict build is `artifacts/Mu/Companion.Desktop.exe`, with zero warnings/
errors. Actual Desktop shortcut creation and property readback passed; existing
profiles select Muon/Galaxy through Settings and Save. Its regression passed
375/375 .NET tests, 11/11 architecture checks, 12/12 independent LAYA Python tests
and 44/44 native UI process/control checks. Both retained native UI runs pass
315 avatar-customization and 40 controlled LAYA UI assertions. Physical input,
screen-reader and multi-monitor qualification remain outside this increment.

LAYA's optional local task-advice boundary is implemented and its pinned runtime/
checkpoint are installed. **Actual pretrained inference remains NOT QUALIFIED.**
The production 3 GiB host and real native view safely refused admission with
`memory_limit` in 1.65 seconds; no advice or model output was produced. The tiny
seeded float16 CPU check and synthetic protocol fixtures do not qualify the full
checkpoint. No cloud/API fallback or lowered admission threshold was introduced.
See [appearance/LAYA help](docs/MU_APPEARANCE_LAYA.md), [Testing](TESTING.md) and
[independent identity review](docs/qa/MU_IDENTITY_LAYA_REVIEW.md).

The identity review scores the bounded work **88/100 (8.8/10)** with an **INCOMPLETE**
overall verdict due to the full LAYA inference gate. Identity/customization are
delivered; that score does not certify the unfinished model behavior or a release.

The preceding **integrated ML Lab foundation is implemented and its final
bounded regression passes; full requested ML Phase 1 and release remain incomplete.**
Exact evidence for that checkpoint is listed in [Testing](TESTING.md). That checkpoint passed
353/353 .NET tests with no skips, 11/11 architecture checks, 57 native Lab assertions,
two live local schema-planning fixtures and 38 prior UI supervisor checks. Independent
Python coverage spans 48 distinct tests across retained runs; author tests cover
22 task/algorithm pairs in six cases. The [independent ML review](docs/qa/ML_LAB_FOUNDATION_REVIEW.md)
scores this bounded increment **89/100 (8.9/10), PASS**, with no identified critical
defect in the reviewed scope. Full ML Phase 1 and release gates remain open.

That historical verification used a working-tree checkpoint based on `22f3dae`,
including ML changes and separate preserved voice-input WIP then uncommitted. Those
files are now committed in the inspected `7fe3305` source; the old receipts still
identify their original tested tree and are not current-candidate or release evidence.

The preceding 2026-09-30 identity/advice checkpoint is recorded in
`artifacts/qa/identity-laya-review/verified-source-manifest.json`: nonignored source,
that candidate's files and final evidence hashes. It records the incomplete LAYA
gate separately from passing identity/UI checks. Do not attribute its evidence to
the older normal output or the preceding ML-only candidate.

The preceding ML-only foundation strict production build has zero warnings/errors and is at
`artifacts/qa/ml-candidate/Companion.Desktop.exe`. The normal binary directory was
locked by an older running Mu. Close that instance before starting the candidate
with explicit repository/model paths; ordinary single-instance activation otherwise
reuses the older binary. No installer or published release is implied.

Historical baseline: **Phase 3 development passed independent QA at 9.5/10;
the preceding local chat increment passed at 9.0/10 and compact surface at 9.1/10.**
Phase 0 passed at 9.4/10 and Phase 1 passed at 9.0/10. Phase 2's final resource
confirmation remains **FAIL**, and release is not approved. Phase 3 now implements
pretrained local inference, optional SQLite history, separate temporary sessions,
branching/search and bounded native rich chat rendering.
Phase 4A now implements manual local Read aloud/Stop, installed voice selection and
session speaking speed; its independent review passes at **9.0/10**. Full Phase 4
microphone input, recognition, VAD and complete voice-loop work remain unfinished.
The recorded validation sequence moves unavailable physical/input/sign-in qualification
to mandatory Phase 11/12 pre-release gates without marking any missing check passed.
The separate, explicitly post-result decision in docs/qa/PHASE_2_RESOURCE_SEQUENCE.md
places unresolved CPU qualification in the user's Phase 12 optimization work. Performance
remains 7/10 with zero budget-criterion points; neither limits nor measurements changed.

## Completed implementation

- Added four optional pretrained neural voices to local explicit speech, a fixed
  public Preview, separate preparation/playback status and safe bounded lifecycle.
  Normal speech keeps utterance/audio in memory; its optional runtime is separate
  from ML/LAYA and existing Windows voice selection remains available.

- Added a round muon-inspired particle mascot, five themes including Galaxy, two
  faces and three halo/trail options, a static accessible preview and schema-3
  persistence. Legacy omitted names retain Pip; new profiles use Mu. Hover-only
  connector repairs pointer travel without adding a resting card. High contrast,
  reduced motion, state expressions and explicit-save semantics remain intact.
- Added transparent SVG/PNG/seven-frame ICO and native executable/window/tray
  wiring. `scripts/create-mu-shortcut.ps1` created the actual Desktop link after
  a scratch-folder check, preserving existing links and verifying all launch/icon
  properties. Taskbar pinning remains a user action through Windows.
- Added optional, inspectable LAYA category advice with bounded App-owned CPU
  workers, offline/hash-verified loading, resource admission and cancellation.
  Advice is a fallible hint for an unchanged request, never an execution permission.
  Full checkpoint inference and domain quality remain pending as described above.

- Bounded ML foundation: native Data/Inspect/Configure/Jobs/Results/History/Models/
  Plots/Ask Mu workflows, local training/evaluation, saved models, exact-cohort comparison,
  full historical describe, schema-safe prediction and standalone reproduction export.
  A 24-package hash-locked optional Python runtime backs App-owned limited CPU workers.
  ML project artifacts persist separately from temporary Chat. Local structured proposals
  resolve a captured current dataset and require explicit review/Run; Auto is one baseline.
  Advanced feature engineering, scientific/deep-learning adapters, richer project/model
  management and full ML acceptance remain future units. See [Lab guide](docs/ML_LAB_GUIDE.md).

- Phase 4A speech output runs plain text through an owned Windows speech worker,
  with a 2,000-character input cap, 30-second deadline and three-second cleanup
  bound. Read aloud is explicit; new sends, conversation changes, Sleep and close
  cancel it. No API key, microphone or automatic spoken reply is introduced.
  Strict regression passes **305/305 tests**, **11/11 architecture checks** and
  composed WPF smoke. Native Microsoft David synthesis, Stop, reuse and the active
  deadline passed. Abrupt temporary-speech persistence checks passed. The first
  expanded UI supervisor timed out; the declared diagnostic rerun passed **38/38**
  supervisor checks and **84 speech UI assertions in both native runs**, alongside
  all prior desktop suites. The [speech review](docs/qa/PHASE_4A_SPEECH_REVIEW.md)
  scores **90/100**, approving only this output increment.

- Phase 3 history: explicit Saved chats mode, title/search/delete, immutable edited
  and regenerated branches, pending-prompt commits and interrupted-turn recovery.
  Temporary is the default and uses a separate in-memory store. Save failures retain
  visible output and offer retry or explicit temporary continuation.
- Exact local chat-template/tokenizer admission fits 1,792 prompt tokens plus a
  256-token response reserve; it drops oldest complete pairs and retains the latest
  user message. Native Markdown/code/math rendering has shared bounds and no active
  HTML, remote assets or model-supplied XAML. Reply previews keep the avatar compact.
- The pre-recovery/pre-preview source passed 267 tests, 11 architecture checks,
  50 native history and 46 native rich-document assertions in both retained runs.
  Abrupt-exit temporary sentinel and real-model context/restart checks passed.
  Independent review then found a hot-journal admission bug; the fix and final
  regression are recorded separately in the Phase 3 history evidence and review.
  Final Release passes **272/272 tests**, **11/11 architecture checks**, zero warnings/
  errors and composed WPF smoke. Native passes **28/28 process controls**, both
  50-case history, 46-case renderer and 34-case reply-preview suites. Final abrupt-
  exit privacy probe passes. Independent review scores **95/100 (9.5/10)** with no
  critical remaining defect in the Phase 3 development scope.

- Replaced the large companion card with the user-requested character-only resting
  surface and compact hover/keyboard Chat, Settings and More controls. Default
  character size is 112×88 DIPs; stable 136×132-DIP window bounds prevent hover
  displacement. Mouse focus does not latch controls; Escape dismisses them.
- Compact UI regression: 224/224 tests, 11/11 architecture checks, composed WPF
  smoke and zero warnings/errors; 20/20 native process controls, including 51 new
  reviewer-authored compact assertions. Native input sampled Chat/Settings/More,
  pointer exit after mouse focus, Tab/Shift+Tab/Enter/Escape at saved 75% scale on
  the single 125%-DPI display. Full input/accessibility/display qualification remains.
  See [surface review](docs/qa/COMPACT_AVATAR_REVIEW.md) and
  [build evidence](docs/qa/COMPACT_AVATAR_BUILD_EVIDENCE.md).

- Implemented the user's final model choice: pretrained Qwen3-0.6B Q8_0, fully local
  llama.cpp CPU inference, no API key or cloud fallback. The briefly explored
  from-scratch training draft is paused and excluded from the application.
- Added hash-pinned setup, lazy owned worker, loopback socket-owner verification,
  ephemeral internal authentication, offline launch, bounded process lifetime and
  a 2 GiB worker committed-memory job limit. No hosted provider SDK is involved.
- Added chat via menu/Ctrl+Shift+C with plain-text streaming, Stop, explicit edit/retry,
  bounded completed-turn context, close/shutdown cancellation and model unloading.
  Generation owns a Thinking expression lease; Sleep cancels the current turn.
- Strict verification passes 224 tests, 11 architecture checks, composed WPF smoke,
  and zero warnings/errors. Twelve tests are reviewer-authored. Native checks pass
  19 controls and 54 chat assertions, alongside all preceding desktop checks.
  The real-model protocol passes in `artifacts/qa/local-model-20260922-195744-9143334a`:
  exact instruction, two-turn recall, three tips, cancellation/reuse, worker disposal
  and cancellation after a worker actually spawned. All public fixture outputs retained.
  Startup observed 4.8037317 s; entire check 14.9589429 s; sampled maximum private bytes
  360,083,456. This excludes mapped-model physical memory and is not a total RAM claim.

- Preserved the committed Phase 1 foundation: validated atomic settings/backups,
  content-free bounded logs, same-user activation and composed application lifetime.
- Added scale/opacity/reduced-motion preferences and closed schema-1 migration.
- Added default vector renderer behind IAvatarRenderer, eight semantic expressions,
  activity ownership/priority, Sleep/Wake and explicitly labeled four-second previews.
- Added mouse/keyboard positioning, edge snapping, work-area constraints and bounded
  atomic monitor-relative placement separate from explicit preference saves.
- Added deterministic next-display movement through Ctrl+Shift+M and a menu action;
  physical cross-display/DPI verification remains pending.
- Added tray Show/Hide/Settings/Sleep/Exit, configurable recovery shortcuts with
  conflict retention, and session click-through cleared on recovery/activation.
- Added explicit per-user startup preference; no automatic registration on load.
- Added independent geometry/state oracles, storage/failure regressions, native
  recovery/placement smoke and a repeatable resource measurement harness.

## Architecture decisions

C#/.NET 10 WPF; Core values and pure math, Application ports, Infrastructure adapters,
Desktop composition. Infrastructure owns Microsoft.Data.Sqlite and System.Speech;
Desktop owns Markdig
and WpfMath. Core/Application remain package-free. The reviewed native llama.cpp
worker and Qwen weights are explicit Phase 3 dependencies. See docs/DEPENDENCIES.md.
The framework's Windows Forms
NotifyIcon supplies the tray; WPF still owns the companion and settings. Typed logs
cannot accept conversation text. No AI credentials, external application network calls,
qualified voice capture, approved memory or general-purpose tools exist yet. The new
ML Lab is a closed local computation subsystem with an optional Python environment.
Placement is a separate 4 KiB versioned file;
startup registration uses a validated quoted executable command without arguments.

## Defects found and fixed

Independent QA found and implementation fixed the missing movement automation peer,
minimized placement, a move immediately followed by Hide losing the pending save,
shortcut errors disappearing during layout, and initialization/shutdown races. Moves
now capture an immutable normal-state snapshot before debounce and flush it when hidden.
Recovery also refreshes the settings click-through checkbox. Shortcut success now
clears only its own error, preserving unrelated storage messages. Existing settings
windows now show and restore when reopened; hidden/minimized recovery passes native checks.
Measurement input is locked and labeled because the first performance run was changed
interactively. Direct native process counters reduce measurement overhead; the
remaining CPU cost is still under investigation.
Settings Save now captures native preferences with the save request and suppresses
later UI, shortcut, click-through and startup changes once shutdown begins. A pending
save cannot re-enable the editor or reapply the companion after native resources are
disposed. Native controlled-completion cases cover committed, cancelled and failed saves.
Two subsequent readiness fixes now pass final regression: input is disabled while
initial placement loads and restores, with prior disabled/closed/stopped state preserved;
Hide and click-through require a successfully registered summon shortcut rather than
requested tray visibility. Settings refresh click-through availability after shortcut changes.

## Test status and evidence

- Historical Phase 2 Release verification: **196 passed, 0 failed, 0 skipped**, 11
  architecture checks, composed smoke, zero compiler warnings/errors. The final-source
  verification covers both readiness fixes; its retained TRX is
  artifacts/qa/desktop-ui-20260922-185538-0c91126a/release-196-before-final-build-confirmation.trx.
  Three new independent
  placement tests cover cancelled waiters, cancellation after atomic commit and retry
  after writer failure. Preserved TRX:
  artifacts/qa/desktop-ui-20260922-183217-1d198cb8/release-196-before-observer-abba.trx.
  The earlier 193-case artifact and its cadence-only scope remain retained.
- Historical final Phase 2 native UI run: **18/18 process/control checks**, **43 appearance,
  state and placement assertions**, **24 tray/recovery/shortcut assertions**,
  **29 settings lifecycle assertions**, and **49 placement-initialization assertions**,
  with no skips or reported failures. Evidence:
  artifacts/qa/desktop-ui-20260922-185538-0c91126a/profile.
  Settings recovery, shutdown during Save, input freeze during placement and confirmed
  shortcut recovery pass. The
  associated Release build has zero warnings/errors. This native run observed one display at
  120 DPI (125%); next-display coverage is therefore a single-display no-op.
- The new readiness fixture initially failed compilation and was corrected. Strict
  Release verification now passes. Native attempt desktop-ui-20260922-185315-2646a9cb
  completed 43 appearance, 24 integration and 29 settings lifecycle checks, then failed
  placement assertion 42: its comparison included window size after warning text resized
  the window. The fixture now compares Left/Top; production code was unchanged by that
  correction. The later 18/43/24/29/49 native run above passes; the failed attempt is
  retained and is not counted as a complete run.
- Prior expanded UI attempt hit its 15-second deadline after all 33 appearance checks;
  it had no completed integration artifact. Retained at desktop-ui-20260922-164249-5578a5c9.
  The supervisor first increased its deadline to 30 seconds and now uses 40 seconds
  for the longer blink checks; it records stage timings.
  The subsequent 33-assertion run completed its UI stages in 4.790 s and 1.984 s.
- The later desktop-ui-20260922-174634-ef0f4899 attempt ran both application processes
  successfully with 43 appearance assertions, but its supervisor failed on the stale
  expected count of 37. That failure remains retained; subsequent corrected 12/12
  runs validated the supervisor and settings recovery before the current 17/17 expansion.
- First performance run completed but is **not a controlled idle baseline**: settings
  were saved during measurement. Keep performance-20260922-164711-eccda553 as rejected
  evidence. The already-running old supervisor returned 0; that does not override the
  contamination finding. New checks reject profiles containing saved settings.
- Normal-user verification resumed successfully, including **13/13 isolated-registry
  cases**, closing the earlier approval-quota execution gap. Actual sign-in is untested.
- Two earlier controlled baselines passed the private-memory limit and failed
  CPU in all three modes. The second, performance-20260922-172825-fc26bc57, measured
  **5.260228% visible, 2.708053% reduced-motion and 2.707961% hidden** of one logical
  processor. Peak private bytes were 65,101,824 / 65,290,240 / 62,021,632. Limits remain
  strictly below 1% of one processor and 150,000,000 private bytes; no calibration
  overhead is subtracted. Earlier controlled evidence and raw failures are retained.
- The six-second-blink run, performance-20260922-175003-37870412, still **failed**:
  visible CPU was **1.093335%**; reduced-motion **0.494627%** and hidden **0.416623%**
  passed. All memory intervals passed (63,504,384 / 65,265,664 / 63,492,096 peak private
  bytes). It recorded 10/0/0 completed blinks and zero owned animation clocks. The
  combined renderer/lifetime changes improved reduced/hidden CPU; no single cause is
  proven. This failed result is retained without overhead subtraction.
- The pre-experiment ten-second-blink run, performance-20260922-175657-e7f2743d, **failed**:
  visible CPU was **1.041400%**; reduced-motion **0.520719%** and hidden **0.390566%**
  passed. All memory intervals passed (62,914,560 / 64,225,280 / 66,539,520 peak private
  bytes), with 6/0/0 completed blinks and zero owned animation clocks. The supervisor
  correctly exited 1 and retained budget-evaluation.json. The cadence is frozen;
  neither another unchanged repeat nor a relaxed threshold is used to claim a pass.
- Supported Computer Use window discovery failed because its native helper pipe was
  unavailable (OS error 2), including after updated skill guidance and a fresh reset.
  No actual mouse/keyboard input was
  attempted this turn. Programmatic HWND checks do not replace this evidence.
- Production idle rendering now waits ten seconds between 120 ms blinks with no animation clocks.
  Native checks prove eyelid transitions, completed-blink counting, interrupted-blink
  exclusion and resource release. Active expressions own at most two clocks, capped
  at 20 FPS, explicitly removed on Stop; closing disposes the renderer. The ten-second
  cadence passes native regression but fails the visible-idle CPU budget. The cadence
  decision preceded its single clean measurement; the harness and limits are unchanged.
- The predeclared **A/B/B/A** observer comparison completed under
  docs/qa/PHASE_2_OBSERVER_PROTOCOL.md. A uses the default `threads` observer; B uses
  `process`, omitting only two boundary thread snapshots per interval. The production
  cadence, warmup, native counters, calibration and limits stay fixed; no overhead is
  subtracted. Five binary hashes and the protocol/supervisor hashes are frozen. Every
  valid pass/failure is retained, technical failures stop the sequence, and both B
  replicates had to pass every interval. All four runs were valid and passed all CPU
  and memory intervals. A visible CPU values were **0.885254% / 0.989278%**; B values
  were **0.755076% / 0.703079%**. This is evidence for that earlier frozen binary,
  not an isolated product optimization or statistical confidence claim. All results remain in
  artifacts/qa/observer-d217a835e8bc4ec9b6f27b98637024fd/comparison.json.
- The separately declared docs/qa/PHASE_2_FINAL_BUILD_PROTOCOL.md requires exactly two
  process-only B runs after the readiness fixes, both passing every unchanged limit.
  Its frozen SHA256 is 217CD856232210092F15773DE19062F6FB12A35B0185393C64EB771FB49CE174.
  The completed final-build sequence is retained at
  artifacts/qa/final-build-1ca3041a363849aabc6b2c4e2170ce6f/comparison.json.
  **Aggregate result: FAIL.** Both runs are valid, but both were required to pass.
  Run 1, performance-20260922-185649-ab4dbf46, measured **1.978694% visible,
  1.015400% reduced-motion and 0.364563% hidden** one-core CPU; the first two fail.
  Run 2, performance-20260922-190004-0f75069d, passed at **0.755208% / 0.598859% /
  0.364527%**. All memory intervals pass; peak private bytes were respectively
  64,974,848 / 65,224,704 / 62,337,024 and 63,119,360 / 64,102,400 / 63,520,768.
  Each run records 6/0/0 completed blinks and zero owned clocks. The first failure,
  exact binary identity and unchanged acceptance rule are preserved; no replacement
  run or overhead subtraction is used. Development approval does not change this failure.

Historical Phase 1 evidence remains: Release/Debug 38 tests each, 11 architecture
checks, 9 negative controls and 9 UI/process checks, plus attributed actual input.
No Phase 2 Debug, physical mixed-DPI, screen-reader, actual sign-in, remote CI,
frame-time percentile or long-running memory certification is claimed.

## QA history

| Checkpoint | Reviewer | Score | Result |
|---|---|---|---|
| Approved text inspection: initial implementation | Independent trusted-action QA | 6.9/10 | FAIL; TRUSTED_TEXT_INSPECTION_REVIEW_INITIAL.md |
| Approved text inspection: Linux checkpoint | Independent trusted-action QA | 8.2/10 | FAIL/incomplete native qualification; TRUSTED_TEXT_INSPECTION_REVIEW_LINUX.md |
| Approved text inspection: Windows development | Independent trusted-action QA | 9.4/10 | PASS bounded A slice; TRUSTED_TEXT_INSPECTION_REVIEW_WINDOWS.md; physical/resource/release gates remain |
| Phase 0 final | Independent phase0_qa | 9.4/10 | PASS |
| Phase 1 final | Independent phase0_qa | 9.0/10 | PASS |
| Phase 2 first appearance increment | Independent phase2_review | Unscored historical checkpoint | INCOMPLETE |
| Phase 2 desktop integration | Independent phase0_qa | 8.0/10 | FAIL; docs/qa/PHASE_2_REVIEW.md |
| Phase 2 resumed validation | Independent phase0_qa | 8.6/10 | FAIL phase exit; docs/qa/PHASE_2_FOLLOWUP_REVIEW.md |
| Phase 2 resource/lifecycle review | Independent phase0_qa | 8.7/10 | PASS development; resource confirmation FAIL; docs/qa/PHASE_2_RESOURCE_REVIEW.md |
| Phase 3 local pretrained chat increment | Independent local_model_qa | 9.0/10 | PASS bounded increment; full Phase 3 incomplete; docs/qa/PHASE_3_LOCAL_INCREMENT_REVIEW.md |
| Phase 3 history/rendering development exit | Independent local_model_adapter | 9.5/10 | PASS development; release gates retained; docs/qa/PHASE_3_HISTORY_REVIEW.md |
| Phase 4A manual local speech output | Independent local_model_adapter | 9.0/10 | PASS output slice only; full Phase 4 unfinished; docs/qa/PHASE_4A_SPEECH_REVIEW.md |
| Mu identity/avatar and local LAYA boundary | Independent ml_review | 8.8/10 | Identity delivered; overall INCOMPLETE, full LAYA inference unqualified; docs/qa/MU_IDENTITY_LAYA_REVIEW.md |

The latest Phase 2 independent score is **8.7/10 (87/100)**, strictly greater than 8.5, and
Phase 2 is complete as a development milestone. Historical 8.0 and 8.6 failed exits
remain unchanged. The later observer experiment passed on its own binary, while
final-build resource confirmation failed. The explicitly post-result sequencing
decision moves CPU qualification to mandatory Phase 12 release work; it is not a
resource pass. Performance remains **7/10**, including **zero** for the budget criterion.
The earlier physical/input/sign-in sequence decision also retains every unexecuted
manual-matrix case as mandatory before release. See docs/qa/PHASE_2_RESOURCE_REVIEW.md.

## Pending work / known limitations

0. Complete the full LAYA runtime diagnostic with sufficient physical RAM and
   Windows commit headroom, within the existing 3 GiB job limit; retain resource
   refusals as failures. Only then evaluate representative task-routing quality.
   Runtime/model installation alone is not successful inference.

1. Phase 3 development is complete at **9.5/10**. Phase 4A's explicit installed-
   voice Read aloud/Stop passed independent review at **9.0/10**; offline
   push-to-talk remains preserved, unqualified WIP while ML development takes priority.
   The 0.6B model has only
   narrow fixture evidence, not general assistant or scientific accuracy qualification.
2. Before release, diagnose CPU cost and complete a separately predeclared Phase 12
   repeat protocol controlling actual presentation/occlusion, warmup and observer cost.
   Limits remain strictly below 1% of one logical processor and 150,000,000 private bytes;
   retain every failure and exact binary identity without overhead subtraction.
3. Before release in Phases 11/12, execute physical display/input/accessibility and
   actual Windows sign-in qualification with suitable hardware and a disposable user.

Arrows are constrained to the current monitor; dragging or Ctrl+Shift+M can move
between monitors. Actual multi-monitor behavior remains unverified on this one-display setup.
A companion larger than a work area anchors to its origin; tray settings/Exit remain
recovery routes. General occlusion detection is not implemented; visible occluded
avatars can keep animating. Local storage is not encrypted and same-user malware
remains outside the protection boundary. Asset packs, signing, installer/updater,
release license, microphone/recognition, approved long-term memory and tools remain later work.

## Source checkpoint and next objective

Current reviewed action code is `a999ae4` on `feature/trusted-text-inspection`,
[draft PR #2](https://github.com/calebaiken/mu-desktop-assistant/pull/2), with the
source identity and final evidence above. Next: B's explicit project identity and
existing reviewed Chat→Lab proposal bridge, respecting PR #1; then approved project
memory (C) and independently qualified model profiles/workload admission (G/I).
The original phase-checkpoint history below predates this action increment.

Phase 1 is committed at 532879c and identified by refs/checkpoints/phase-1; Phase 0's
checkpoint is retained. Commit **6306ec9** preserves the earlier **IN PROGRESS** Phase 2
source and 8.6 failed-exit review. The current verified development milestone uses the
local source checkpoint refs/checkpoints/phase-2. No remote CI run or release is claimed. The
local pretrained chat increment has its own source checkpoint at
refs/checkpoints/phase-3-local-chat-increment. Phase 3 history/rendering is retained at
refs/checkpoints/phase-3. Phase 4A output is implemented under
docs/PHASE_4_VOICE_PLAN.md, with its approved source recorded at
refs/checkpoints/phase-4a-speech-output. The active next objective is completing the
requested ML scope after this bounded foundation and its independent review. Phase 4B
local push-to-talk retains separate capture/recognition ports and its frozen review;
its incomplete Desktop partial remains excluded from compilation.
No hosted provider is planned.
