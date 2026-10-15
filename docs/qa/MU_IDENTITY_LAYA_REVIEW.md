# Independent QA: Mu identity, avatar customization and local LAYA advice

Protocol frozen before execution review of this increment. Reviewer is independent
of its production authors. Baseline is the existing working tree containing the
qualified bounded ML foundation (8.9/10) and preserved unqualified voice-input work.
This review does not replace or silently extend that prior qualification.

## Scope and decision rule

Scope: real application/tray/window icon wiring, an explicit per-user desktop
shortcut, native avatar appearance settings with backward compatibility, and a
separate typed local LAYA advisory service integrated into Mu. Advice has no tool,
shell, file-mutation or autonomous execution authority. Offline setup requirements
and absent optional resources must be stated honestly. A mocked provider can test
the boundary but cannot qualify actual LAYA inference.

Ten categories, five criteria each, scored 0 absent/contradicted, 1 partial evidence,
2 satisfied by cited evidence. Sum all 50 scores and divide by 10. Strictly greater
than 8.5 and no unresolved critical defect is required for a bounded increment pass.
Do not infer general decision quality from two example prompts or boundary tests.
Read every changed production/integration line; preserve failures and affected reruns.

Critical blockers: unrequested uploads/downloads at runtime; arbitrary code or unsafe
model deserialization from supplied data; model output acquiring execution authority;
silently expanding user resource limits; corrupting existing preferences/source data;
orphaned workers after normal shutdown; a shortcut invoking a shell or different binary
than the reviewed target without clear disclosure; or fake completion.

## Frozen criteria

| ID | Category | Criterion | Score / evidence |
|---|---|---|---|
| C1 | Correctness | Icon assets decode at advertised sizes and are wired to PE, native windows and tray without missing resources. | 2 — Seven frames decoded; PE extraction and repaired native window/tray resources. |
| C2 | Correctness | Saved shortcut has the actual verified executable, argument boundaries, working directory and icon; existing unrelated links remain unchanged. | 2 — Independent actual Desktop .lnk readback verifies target/arguments/cwd/icon; existing-link guard retained. |
| C3 | Correctness | Avatar selection/palette settings roundtrip and repaint at unchanged semantic state; old profiles retain behavior. | 2 — Settings roundtrips for all 60 combinations; 315 native appearance/hover checks. |
| C4 | Correctness | Advertised LAYA operation completes through real local inference and displays the returned advice with its actual identity. | 0 — Actual host/native checkpoint run returned memory_limit; no advice produced. |
| C5 | Correctness | Invalid input, unknown/duplicate fields, unsupported actions and malformed model output fail clearly without mutation. | 2 — Independent closed-schema, malformed output, numerical and invalid preference checks. |
| A1 | Architecture | Core/Application remain free of GUI/ML implementation dependencies and architecture checks pass. | 2 — 11/11 current-source architecture checks. |
| A2 | Architecture | LAYA uses a typed advisory port and owned bounded internal worker; existing ML and chat are not rewritten. | 2 — Separate ILayaDecisionService; one owned worker, existing action/chat interfaces retained. |
| A3 | Architecture | App owns service/worker lifetimes; view closure, cancellation and App disposal are explicit and independently tested. | 2 — Held child identity, concurrent disposal, other-service ownership and late-view tests. |
| A4 | Architecture | Advice is inspectable data without action/tool/shell execution or hidden settings changes. | 2 — Advice/proposals create no jobs; explicit reviewed execution remains separate. |
| A5 | Architecture | Existing temporary/saved chats, ML jobs, voice-output and compact companion behavior remain isolated and usable. | 2 — 375 .NET and 44 native process/control checks preserve existing assistant/history/speech UI. |
| M1 | Numerical integrity | Model input/output shapes, lengths and dtype assumptions are validated against the selected backend. | 1 — Strict weight/shape/dtype admission and seeded tiny native execution; full checkpoint unverified. |
| M2 | Numerical integrity | Scores/probabilities/confidence are mathematically labeled or omitted; no fabricated calibration or reasoning-quality metric. | 2 — Explicit uncalibrated scores, exact probability checks; no accuracy claim. |
| M3 | Numerical integrity | RAM/token/thread/time limits and telemetry use correct units and measure the actual owned process. | 1 — Correct MiB/GiB, CPU and time limits; full-model owned-process peak still unmeasured. |
| M4 | Numerical integrity | Advice parsing preserves choices and explanations without converting model preference into a scientifically validated conclusion. | 2 — Fixed category explanations, nonbinding hint and scientific caveats. |
| M5 | Numerical integrity | QA counts/arithmetic and model/runtime versions are measured, reproducible and separate from unqualified future capabilities. | 2 — Separate actual, synthetic and failed receipts; pinned runtime/asset versions. |
| T1 | Testing | Strict build/static checks and meaningful independent tests run with nonempty retained receipts. | 2 — Strict build and independent tests with retained nonempty receipts. |
| T2 | Testing | Avatar schema migration, malformed settings, backup/external-edit behavior and existing settings tests execute. | 2 — All migration, invalid enum, backup, external-edit and existing settings tests passed. |
| T3 | Testing | Real native avatar/icon/settings interactions and minimum-size reachability execute; physical versus diagnostic evidence is distinguished. | 2 — Actual native rendering/layout/save/failure checks; physical limits explicitly distinguished. |
| T4 | Testing | Actual local advice, missing dependency/model and offline behavior execute on deterministic public/synthetic fixtures. | 1 — Actual missing-dependency/offline/resource refusals tested; successful pretrained inference absent. |
| T5 | Testing | Cancellation, queue/admission, malformed/flooding worker, memory cap and disposal failures are exercised. | 1 — Real cancellation, busy admission, flood/malformed/disposal tests; hard memory exhaustion not exercised. |
| R1 | Reliability | Queued/running requests cancel and late callbacks cannot mutate closed views or revive cancelled requests. | 2 — Cancellation, closing, stale hints and late callbacks tested. |
| R2 | Reliability | Worker failures are bounded, produce fixed actionable errors and permit safe reuse or require restart after uncertain cleanup. | 2 — Fixed failure categories, bounded reads, kill/reuse and poisoned cleanup policy. |
| R3 | Reliability | Settings migration is in-memory until explicit save; atomic persistence and known-good backup survive failures. | 2 — Explicit-only migration save, atomic store and external-edit regression coverage. |
| R4 | Reliability | Icon resources and shortcut remain valid after process restart; old running binary/cache ambiguity is disclosed. | 1 — Two native restarts and PE resources verified; Windows shell icon cache/pin restart untested. |
| R5 | Reliability | Missing optional LAYA runtime/weights does not prevent normal Mu startup, chat, settings or ML use. | 2 — Native controls work without model; actual memory refusal leaves normal application shutdown clean. |
| S1 | Security | Runtime remains local/offline; only explicit setup can fetch pinned dependencies or selected weights. | 2 — Pinned explicit setup; fresh-process network-audit denial and closed offline environment. |
| S2 | Security | Closed paths/model sources and bounded safe formats refuse linked/remote/device/executable inputs as applicable. | 2 — Hashes, closed files, path guards, safetensors and actual Windows read leases. |
| S3 | Security | Worker protocol has input/output/depth/identity bounds, closed environment and no shell invocation. | 2 — Input/output/depth/identity bounds, isolated interpreter and no shell. |
| S4 | Security | Model output cannot raise captured resource limits or execute arbitrary actions; configuration authority stays with the user. | 2 — Advice has no action authority; existing captured ML limits unchanged. |
| S5 | Security | Logs omit private prompts/advice/data; shortcut creation is scoped to the requested destination and does not modify taskbar pins automatically. | 2 — Fixed content-free logs/errors; shortcut script preserves existing links and never pins taskbar. |
| P1 | Performance | CPU/RAM/concurrency/token bounds are enforced and optional heavyweight packages do not burden the base installation. | 2 — Owned job ceiling, process/thread/token bounds and exact memory admission; separate optional runtime. |
| P2 | Performance | Avatar settings preserve reduced-motion/high-contrast/sleep/hidden animation bounds and do not add continuous idle work. | 2 — 60 combinations across semantic states, no motion resources under static/high contrast/sleep. |
| P3 | Performance | Native UI remains responsive during local inference, cancellation, startup and settings save. | 1 — Native asynchronous boundary/cancellation responsiveness; full-model responsiveness unmeasured. |
| P4 | Performance | Images/resources have bounded dimensions/bytes, appropriate icon resolutions and no remote asset loads. | 2 — Local bounded vectors and 20,799-byte multi-frame ICO; no remote images. |
| P5 | Performance | Worker/model storage is explicit, referenced efficiently and cleaned up without deleting user source data. | 2 — Explicit isolated runtime/checkpoint, streamed hashing/downloads and preserved user data. |
| N1 | Maintainability | Cohesive modules and documented ownership minimize duplication or regressions in the trusted existing process host. | 2 — Cohesive typed adapter and shared small path/interpreter/job primitives. |
| N2 | Maintainability | Runtime/model/version/license/provenance and offline setup instructions are concrete and reproducible. | 2 — Concrete pins, hashes, license, source/model revisions and setup instructions. |
| N3 | Maintainability | New avatar style/palette and advisory backend extension instructions reflect working interfaces. | 1 — Maintenance/extension boundaries documented; no full avatar/backend authoring guide yet. |
| N4 | Maintainability | Settings schema evolution uses explicit legacy contracts; future-version handling remains protective. | 2 — Explicit v1/v2 contracts, schema 3 enum checks, future read-only protection. |
| N5 | Maintainability | Current status/help/changelog state verified scope, failed receipts and remaining limitations accurately. | 2 — Final status/help/changelog distinguish delivered identity and tested boundaries from unqualified full LAYA inference. |
| U1 | UX | Application identity is recognizable across desktop/taskbar/tray/windows without enlarging the character-only resting surface. | 1 — Delivered recognizable native identity; physical Explorer/taskbar/pin presentation not certified. |
| U2 | UX | Avatar controls are named, keyboard reachable, scrollable and preserve preview/save/cancel expectations. | 2 — Named controls, minimum viewport scrolling, preview/save/close and failure behavior. |
| U3 | UX | Advice request/status/result/errors are clear and visibly distinct from a performed operation. | 2 — Advice and proposals visibly distinct from execution; native failure/cancellation checks. |
| U4 | UX | LAYA identity/resource/offline limitations are understandable; unavailable capability is not represented by a fake working button. | 1 — Optional limitation/errors are clear; full model remains unqualified on this machine. |
| U5 | UX | High contrast, scale, opacity, reduced motion and native hover/focus behavior remain compatible. | 2 — Current scale/opacity/hover/focus/reduced-motion and forced-contrast coverage. |
| Q1 | Requirements | Requested icon and shortcut are actual delivered assets/artifacts, with executable wiring verified. | 2 — Actual Desktop Mu.lnk targets the reviewed existing executable directly; PE and native icons verified. |
| Q2 | Requirements | Requested avatar customization operates in existing Mu and persists without resetting unrelated preferences. | 2 — Muon/Galaxy/faces/decorations integrated and persistent; unrelated preferences preserved. |
| Q3 | Requirements | LAYA advisory integration uses the selected local model and needs no API key or cloud account. | 1 — Pinned selected local model and no API key; successful full-checkpoint inference absent. |
| Q4 | Requirements | User resource/privacy/control preferences remain authoritative; existing qualified ML and assistant scope are preserved. | 2 — Data-local resource/user authority retained; existing regression checks pass. |
| Q5 | Requirements | Completion claims distinguish executed product behavior, boundary-only tests and deferred optional functionality. | 2 — Explicit distinction between delivered identity, tested boundary and unqualified full inference. |

## Independent test plan and execution ledger

Planned fixtures: old v1/v2 and future settings; invalid enum/color text; unchanged
avatar state with changed palette; all semantic expressions with reduced motion/high
contrast; PE/ICO frame inspection and shortcut property readback; model absent and
dependency absent with networking disabled; malformed/duplicate/oversized protocol;
no-output/late-output/flood worker; held owned worker cancel/dispose; malicious advice
text rendered inert; resource requests above the configured cap; deterministic backend
boundary fixtures plus separately labeled real LAYA inference.

Retained execution ledger (earlier failures and their later repairs remain visible):

- `artifacts/qa/identity-laya-review/independent-build-release.txt`: strict .NET test build, zero warnings/errors.
- `independent-initial.trx` in the same folder: 21/21 independent cases passed (8 settings methods covering all 60 preset/theme/decoration/face combinations; 13 LAYA parser and actual synthetic-worker cases).
- `python-independent-memory-final.txt`: 12/12 independent Python tests passed. Includes fresh-process DNS/connect denial, actual Windows model read leases refusing writes/renames, closed layout/hash checks, bounded ingress, numerical/provenance validation and simulated exact memory-admission thresholds. These are not model-quality tests.
- `icon-inspection.json`: seven bounded PNG frames at 16/24/32/48/64/128/256; ICO 20,799 bytes. The reviewer inspected the actual transparent particle PNG visually.
- `final-tests-build.txt`: later strict build with omitted-legacy-name compatibility regression, zero warnings/errors. Initial full run `full-suite-final.trx`: 374/375 passed, one old test expectation still used new-profile Mu instead of migrated legacy Pip. The receipt is retained; the corrected expectation passed in the subsequent full run.
- `full-suite-reviewed.trx` / `full-suite-reviewed.txt`: repaired full suite **375/375 passed, zero skipped, 18 seconds**. `final-reviewed-tests-build.txt` has zero warnings/errors.
- `python-independent-admission-final.txt`: **12/12 passed** after correcting Windows system versus per-process commit admission. Exact limits and query-failure conditions are covered. The later transformer/tokenizer pin and half-parameter assertion were reviewed; these lightweight tests do not load the checkpoint.
- `icon-decoded.json`: actual WPF decoder accepts all seven icon frames as Bgra32 at their advertised sizes.
- `artifacts/qa/desktop-ui-20260930-004749-16bd2937/profile/avatar-customization-smoke.json`: **283 native avatar checks passed**. The reviewer visually inspected `avatar-presets.png` and `muon-options.png`: muon particle, five themes including Galaxy, two faces and three decorations render as intended.
- That same native run retained `laya-ui-smoke.json`, which passed 15 checks then found **Run reviewed plan below the minimum expanded Ask Mu viewport**. The actual UI defect was repaired and rerun below; existence of the button did not satisfy reachability.
- Earlier `desktop-ui-20260930-004141-3ed7cbed/profile/compact-avatar-smoke.json` retained the particle-to-toolbar hover gap at check 7. A hover-only painted connector was added, with its brushes refreshed from the actual avatar palette/high-contrast rendering; the later run passed compact checks without weakening their assertion.
- `desktop-ui-20260930-005356-6d7b0e51/profile/avatar-customization-smoke.json`: expanded **315/315 passed**, including 15 hover configurations and a forced high-contrast connector check. Its LAYA fixture reached **39 checks**, proving the minimum-window repair, then rejected a missing derived-window icon. An implicit Window style alone did not apply to the derived Lab view; explicit native icon wiring was added and passed the final run below.
- `executable-icon.json` / `executable-icon.png`: independent extraction from the actual built executable verifies the PE icon is the particle mascot. This does not substitute for the failed derived WPF Window.Icon check.
- `architecture-final.txt`: **11/11 existing architecture checks passed** on current source. `dependency-lock-inspection.json` validates all generated lock lines structurally and reports 28 exact-version packages, each with hashes; installation uses `--require-hashes` and binary wheels.

Retained environment failures: `independent-build.txt` and `independent-build-serial.txt` record restricted-sandbox compiler failure/named-pipe access failure. Native approved build passed without weakening IPC. `python-independent-initial.txt` records temporary-directory ACL failure; the identical tests passed with ordinary native permissions. These are distinguished from product failures.

Reviewer repairs requested: dispatcher marshalling for settings system-color callbacks; preserved legacy omitted-name default; shortcut IconLocation readback; bounded download-body deadline; strict malformed numeric response normalization. Authors applied the repairs. Current runtime work still requires real inference under the 3 GiB owned-job cap before qualification; earlier access violation/commit-pressure receipts remain in `artifacts/qa/laya/`.

The reviewer-owned, opt-in `LayaRuntimeSmokeChecks.cs` runs the production local service through the real native analysis view on two public synthetic prompts. It records actual model outputs separately from expected categories, proves no ML execution, measures Dispatcher progress, and tests cancellation admission followed by service reuse. Its receipt must pass before claiming actual model integration. Synthetic worker tests separately hold exact process identities for cancellation/disposal ownership.

## Frozen verdict — 2026-09-30

Identity and native customization are delivered and pass their executed regression gates.
The optional LAYA protocol, resource guards, advisory interface and cancellation boundary
are implemented and tested. **Successful inference with the full pretrained LAYA checkpoint
is not qualified.** This is a remaining functional gate, not a calibrated-model or scientific
accuracy claim. **Overall feature completion: INCOMPLETE.** No release approval is given.
No unresolved critical code defect was identified in the reviewed boundaries, but the
missing successful real-model gate prevents a full functional acceptance claim regardless
of the numerical score.

All 50 frozen criteria are scored. The measured bounded review is **88/100 (8.8/10)**:

| Category | Points / 10 |
|---|---:|
| Correctness | 8 |
| Architecture | 10 |
| Numerical integrity | 8 |
| Testing | 8 |
| Reliability | 9 |
| Security | 10 |
| Performance | 9 |
| Maintainability | 9 |
| UX | 8 |
| Requirements | 9 |
| **Total** | **88 / 100** |

Arithmetic: `(8 + 10 + 8 + 8 + 9 + 10 + 9 + 9 + 8 + 9) / 10 = 8.8`.
This exceeds the numerical threshold for the implemented boundaries; it does not
substitute for C4, successful full-checkpoint inference, which remains zero.

The actual delivered shortcut is `%USERPROFILE%\Desktop\Mu.lnk` (recorded here in
generic form; the local path is intentionally not committed).
Independent final COM readback in `artifacts/qa/identity-laya-review/shortcut-independent-final-readback.json`
confirms an existing direct executable target, quoted repository/model arguments,
repository working directory, executable icon and the ASCII description
`Mu - local desktop assistant and Machine Learning Lab`. The target's SHA256 is
`7122853818F894C41487C0D341A32348E5FB1AE7B7D85802CAB3DFED844C7A07`.
The earlier description encoding defect was corrected without changing launch properties.
Final README, status, testing, changelog, architecture, security, dependency inventory
and appearance/worker help were checked against the receipts and remaining limitations.

Final native evidence: `artifacts/qa/identity-laya-review/native-ui-icon-repaired.txt`
reports **44/44 process/control checks**, including two retained native runs under
`desktop-ui-20260930-005702-9eb5cf24`. Both runs passed **315 avatar checks** and
**40 controlled LAYA interface checks**; they also execute the existing chat/history,
rich text, read-aloud UI, placement, settings and compact-avatar regression fixtures.
The repaired Desktop build has zero warnings/errors in `desktop-icon-repair-build.txt`.

The reviewer executed the actual optional diagnostic through the production
`LocalLayaDecisionService` and native Lab: `artifacts/qa/laya-runtime-20260930-010310-74c36871/laya-runtime-smoke.json`.
It passed three admission/no-action checks, then returned **memory_limit** in **1.652 s**.
There were zero advice observations. The process shut down normally through App's error
path; the script correctly failed instead of treating refusal as inference success.
The attempted `real-host-native-laya.txt` capture is empty and is retained as a logging
failure, not supporting evidence; the nonempty JSON receipt above is authoritative.
Earlier full-precision native
failure and later safe Python refusals remain separately retained in `artifacts/qa/laya/`.

## Source review and limits

The reviewer inspected each changed production/integration line for this increment:
schema/enums and legacy migration; renderer/cache/motion policy; native settings/preview;
compact hover connector; icon assets/resources/tray; App ownership/startup/shutdown;
typed advice/host/planner/view; setup/download/shortcut/diagnostic scripts; Python admission,
asset leases, runtime precision and memory policy. Upstream pinned local loading was checked
for safetensors, strict architecture matching, local config/tokenizer loading and disabled
compilation/hooks. Generated dependency lock data was checked for exact pins and hash syntax.
`artifacts/qa/identity-laya-review/reviewed-source-hashes.json` records 40 final reviewed
source/script/asset hashes, including the final ASCII shortcut-description repair;
`candidate-binary-hashes.json` records the actual candidate binaries. This is an
uncommitted working-tree checkpoint based on `22f3dae`, not a clean release checkout.
Unchanged prior ML/voice work was preserved and is not reclassified as newly complete.

Native automation is real HWND/layout/rendering/routed-event evidence, not physical mouse,
keyboard, screen-reader, Windows taskbar pin or mixed-monitor certification. A second monitor
is unavailable. The normal user's selected avatar may remain Classic after protective migration;
available Muon/Galaxy choices are not the same as an already saved personal selection.
Same-user malware or replacement of trusted application/runtime code is outside this process
containment boundary. No heavy memory-exhaustion test was attempted during low-memory conditions.

## Five ranked follow-ups

1. Complete the opt-in full-checkpoint inference diagnostic inside the actual 3 GiB job when
   sufficient RAM and system commit are available. Preserve refusal; do not relax limits to
   turn missing evidence into a success. Then test cancellation during loaded-model execution.
2. Evaluate task-category accuracy on a held-out scientific request set, including ambiguity,
   user overrides and out-of-domain requests. Compare float16/float32 behavior before making
   reliability or confidence claims; two successful prompts would not establish calibration.
3. Execute the retained physical input, accessibility, shell/taskbar presentation and mixed-DPI
   display matrix on suitable hardware. Keep this separate from deterministic native diagnostics.
4. Measure actual loaded-worker private memory/CPU/latency and repeated load/reuse behavior under
   controlled memory pressure. The current guard and hard ceiling do not provide those measurements.
5. Document adding a new avatar style/backend in detail and prepare stable signed packaging,
   installer/update and relocation behavior beyond the current development-folder shortcut.

Regression risks remain explicit: schema 3 is a forward-version boundary for old binaries;
legacy appearance/name remain preserved until save; new round geometry changes hover paths
(now covered at three sizes); and optional CPU float16/native dependency behavior may differ
from float32 or fail under system pressure. No source datasets or conversation state are
modified by appearance changes or advisory analysis.
