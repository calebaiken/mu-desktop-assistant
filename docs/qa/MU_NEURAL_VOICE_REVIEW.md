# Independent QA: optional local neural voices

Protocol frozen on 2026-09-30 before this reviewer executes or scores the new
neural voice implementation. The reviewer does not author production adapters,
workers, integration fixes or model setup. Existing qualified Windows speech
output, ML/identity work and unqualified voice-input work remain distinct.

## Scope and rule

The requested increment adds more natural pretrained local voices to Mu's existing
explicit Read aloud workflow. It preserves local-only/no-key behavior, text chat,
Stop, session voice/rate controls, view lifetime cancellation and the App-owned
speech provider. A separate isolated internal synthesis worker is permitted.
This is not microphone qualification, a voice-loop release, voice cloning or a
guarantee of subjective human quality. A public sample must use actual selected
model inference, with its voice, settings, identity and measured duration stated.

Fifty criteria below use scores 0 (absent/contradicted), 1 (partial evidence),
2 (satisfied by cited evidence). Sum all scores and divide by 10. A bounded pass
requires strictly greater than 8.5, no unresolved critical defect, real neural
synthesis through the production local host, and functional native integration.
No mocked samples, silent fallback presented as neural synthesis, waived gates or
quality claims inferred from nonempty audio. Read every changed production and
integration line, retain failed receipts and rerun affected checks after repairs.

Critical blockers: text/data upload or runtime download without user choice;
private utterance/audio persisted unintentionally; unsupported worker/model input
acquiring arbitrary code authority; hidden voice substitution; unbounded synthesis
or stale playback after Stop/close; orphaned owned workers after normal shutdown;
corrupted existing preferences/history; or a fake neural voice completion claim.

## Frozen criteria

| ID | Category | Criterion | Score / evidence |
|---|---|---|---|
| C1 | Code correctness | Actual pinned local neural model returns usable audio through the production speech provider. | 2 — Real four-voice production/native synthesis plus independent actual-wave oracle. |
| C2 | Code correctness | Voice IDs, valid Unicode, text/rate bounds and supported options are validated before expensive work. | 2 — Reviewer C#/Python Unicode, closed catalog, UTF-16 and exact admission boundaries. |
| C3 | Code correctness | One owned synthesis/playback operation runs at a time; no implicit queue or automatic reply playback. | 2 — Real held-child busy/cancel checks and native explicit-preview/no-queue assertions. |
| C4 | Code correctness | PCM/WAVE parsing independently validates format, chunk lengths, sample alignment, duration and output cap. | 2 — Separate stdlib RIFF oracle, exact C# PCM/duration boundaries and four actual WAVs. |
| C5 | Code correctness | Missing/corrupt assets, malformed/flooded worker output and unsupported selection produce typed recoverable failures. | 2 — Actual synthetic fault children, asset leases/hash/layout tests and fixed content-free failure tests. |
| A1 | Architecture | UI-independent ISpeechOutput contract remains coherent; neural libraries stay out of Core/Application. | 2 — Existing port, optional Infrastructure adapters and 11/11 architecture checks. |
| A2 | Architecture | Neural synthesis is an optional bounded internal backend integrated into existing Mu, not a separate user application. | 2 — App composition, separate optional runtime and real native Preview integration. |
| A3 | Architecture | App owns provider disposal; a closing view cancels its utterance without disposing another view's service. | 1 — Ownership and controlled close/disposal pass; loaded-model generation disposal remains resource-gated. |
| A4 | Architecture | Preparation, synthesis and actual playback states are distinct; Speaking never starts just because inference began. | 2 — Native preparing/stale-event checks, real inaudible probes and actual playback-driven Speaking/Stop. |
| A5 | Architecture | Windows output, local chat, ML, temporary/saved history, avatar behavior and excluded voice-input WIP are preserved. | 2 — Full 398 regression, 33 preserved SAPI cases and unchanged native suites; excluded WIP stays unqualified. |
| M1 | Mathematical correctness | Sample-rate/channel/bit-depth and byte-to-duration arithmetic use correct units and independent exact oracles. | 2 — Exact byte/frame/time calculations, 30-second next-frame boundary and actual WAV receipts. |
| M2 | Mathematical correctness | Float-to-PCM conversion rejects nonfinite values and handles clipping/rounding/signed boundaries correctly. | 2 — Real NumPy production-seam oracles including finite, clipping, ties, signed bytes and zero quantization. |
| M3 | Mathematical correctness | Rate mapping has finite documented bounds; actual engine behavior is distinguished from SAPI rate semantics. | 1 — Exact 0.75–1.25 mapping and bounds verified; real four-voice samples use only rate zero. |
| M4 | Mathematical correctness | Memory/output/thread/time limits and measured latency apply to the actual owned process and have explicit units. | 2 — Held PID/job measurements, exact memory-gate tests and explicit generation/playback/cleanup budgets. |
| M5 | Mathematical correctness | A measured valid sample is not presented as proof of human preference, pronunciation quality or scientific accuracy. | 2 — Audio-format evidence and quality/hearing/pronunciation limitations remain explicitly separate. |
| T1 | Testing | Strict build/static checks plus meaningful reviewer-authored admission/protocol/audio tests execute with retained receipts. | 2 — Strict repaired builds, independent 21 Python/23 C# cases and final 398 regression; failures retained. |
| T2 | Testing | Real neural synthesis produces independently parsed finite/nonzero bounded audio on fixed public texts. | 2 — Four actual fixed-text selected-voice WAVs independently parsed and hashed. |
| T3 | Testing | Cancellation during preparation/synthesis/playback, busy admission, disposal and late-result ordering execute. | 1 — Actual playback Stop and synthetic loaded-boundary ordering pass; repaired real generation/disposal gate is incomplete. |
| T4 | Testing | Real WPF selection/status/Stop/minimum-window controls execute; diagnostic versus physical-input evidence stays labeled. | 2 — Real production/native samples, 94 controlled speech checks and actual minimum geometry; physical scope excluded. |
| T5 | Testing | Missing model/runtime, tampered files, invalid text/voice/rate, corrupt audio and output floods are independently exercised. | 2 — Independent negative parser/admission/runtime/asset tests and actual bounded fault children. |
| R1 | Reliability | Stop/new send/new conversation/mode/branch/close/sleep prevent old results starting or resuming playback. | 2 — Preserved speech generation guards and new Preview send/close/stale-progress native fixtures. |
| R2 | Reliability | Deadline and Stop cleanup have declared measured bounds; uncertain cleanup poisons reuse rather than hiding a live worker. | 1 — Narrow held-exit repair and real scheduling-race tests pass 3-second bounds; actual loaded-generation repair is not qualified. |
| R3 | Reliability | A failed optional neural voice leaves readable text and an understandable retry/voice-choice path. | 2 — Typed safe refusals, explicit retry/Windows selection and native retained-text/failure fixtures. |
| R4 | Reliability | Owned worker/audio endpoint/streams/buffers/event handlers are released on success, failure, Stop and shutdown. | 1 — Synthetic success/fault/Stop/dispose and actual playback Stop pass; complete normal loaded-model lifecycle remains incomplete. |
| R5 | Reliability | Repeated synthesis/Stop/reuse and provider disposal leave no unbounded queue, stale Speaking state or idle worker. | 1 — Repeated real stdlib children/reuse pass; actual short normal-Speak reuse failed and fresh 33-case runs cannot reach it. |
| S1 | Security | Neural synthesis runs offline with no external API credentials, network fallback or automatic package/asset fetch. | 2 — Closed runtime source and fresh-process DNS/connect/child denial; pinned actual local inference. |
| S2 | Security | Text travels only through bounded private IPC; argv/environment/logs/errors never retain private utterances. | 1 — Closed/redacted protocol and live public-marker argv/environment checks pass; final actual lifecycle persistence scan is incomplete. |
| S3 | Security | Trusted fixed local model/runtime paths, safe model formats, integrity/provenance and closed worker command are enforced. | 2 — Fixed ONNX/NumPy assets, exact hashes, safe layout/read leases and no arbitrary provider/code selection. |
| S4 | Security | Audio remains in memory for normal speech; only explicitly identified public diagnostic samples may be saved. | 2 — Production memory-only conversion/playback and explicitly isolated public sample exports; DLL exception documented. |
| S5 | Security | Owned process/job, closed environment, no shell or arbitrary inference providers constrain worker authority. | 2 — Actual owned held children/job, closed environment and CPU-only provider; trusted native boundary stated. |
| P1 | Performance | CPU-only optional dependencies and local assets do not burden or prevent the base Mu installation. | 2 — Separate 18-package CPU runtime, optional App composition and no new Core/Application/.NET base packages. |
| P2 | Performance | RAM/commit/storage admission and hard worker limits avoid blindly starting allocation under low resources. | 2 — Exact gates, setup storage guard and actual content-free memory_limit refusals without lowered thresholds. |
| P3 | Performance | Dispatcher remains responsive while preparing/synthesizing/playing/stopping; cleanup does not block native UI. | 2 — Dispatcher advances through four real native syntheses and actual native playback/Stop; cleanup runs off dispatcher. |
| P4 | Performance | Text/chunk/token/audio caps bound inference and avoid repeated full-array copies/unbounded streams where practical. | 2 — Bounded text/phoneme chunks, capped samples/IPC/errors and independent cap/flood tests. |
| P5 | Performance | Cold load, synthesis/playback timing and peak resource observations are reported honestly; no idle synthesis/polling. | 1 — Attempt-specific timing and sampled private bytes are honest; sustained/co-resident/precise-peak measurements incomplete. |
| N1 | Maintainability | Cohesive backend/protocol/audio/selection responsibilities minimize duplication and preserve existing ownership contracts. | 2 — Cohesive adapter/composite/worker and preserved shared port/path/job/audio primitives. |
| N2 | Maintainability | Model/runtime/voice pins, hashes, licenses and upstream provenance are recorded with a reproducible explicit setup. | 2 — Hash-locked packages/assets, provenance/notices and executed idempotent setup; redistribution remains separate. |
| N3 | Maintainability | Tests use seams without bypassing production limits, consent, model admission or process cleanup guarantees. | 2 — Public-host actual child coverage plus held-process seam; no fake-model production bypass or waived bound. |
| N4 | Maintainability | Setup and exported public samples are bounded, reproducible and cleanup-scoped; source/user files remain untouched. | 2 — Bounded explicit install/export, unique profiles and owned-leaf TEMP cleanup; failed-run evidence preserved. |
| N5 | Maintainability | Help/status/security/dependency documents agree with executed capabilities and retained failed/unverified checks. | 2 — Final reviewed documentation distinguishes actual synthesis/Stop, safe refusal, incomplete lifecycle and release limits. |
| U1 | UX/UI | Natural local voices are plainly named and selectable, with no silently relabeled Microsoft voice. | 2 — Four exact neural names/cultures and selected-ID routing without silent voice replacement. |
| U2 | UX/UI | Loading versus Speaking versus Stopping/status errors are understandable and keep the reply available. | 2 — Preparing until playback, Stop/stale-state guards and retained text with clear recoverable errors. |
| U3 | UX/UI | Read aloud stays explicit; Stop is reachable, disabled states coherent and selected rate/voice remain inspectable. | 2 — Explicit Preview/Read aloud, no queue/autoplay and named native voice/rate/Stop controls. |
| U4 | UX/UI | Voice choices/rate/status fit minimum native chat layout with readable transcript and accessible control names. | 2 — Real and controlled minimum-window bounds/names/transcript checks passed. |
| U5 | UX/UI | Actual sample lets the user judge voice character; backend constraints and subjective-quality limits are stated plainly. | 1 — Actual public samples and clear constraints supplied; human preference/pronunciation/accessibility observations absent. |
| Q1 | Requirement compliance | Better pretrained voices run locally without an API key, cloud service or new mandatory heavyweight base framework. | 1 — Actual local pretrained neural voices, no key/cloud/Torch; user-perceived fluency advantage is not measured. |
| Q2 | Requirement compliance | Real new voice output is integrated directly in Mu and preserves existing assistant functionality. | 2 — Actual Chat Preview/model integration, native playback Stop and broad preserved regression. |
| Q3 | Requirement compliance | User-selected voice/rate and privacy/control preferences override backend recommendations. | 2 — Closed selected-ID/rate controls, explicit Windows choice on refusal and no automatic reading/substitution. |
| Q4 | Requirement compliance | Bounded output increment does not claim complete Phase 4, microphone recognition or release certification. | 2 — Full voice loop/microphone, device, release and unrelated phase qualifications remain distinct. |
| Q5 | Requirement compliance | Evidence-backed score/fix loop, top five ranked follow-ups and honest measured/unmeasured scope are retained. | 2 — Frozen 50 criteria, retained failures/repairs, exact score and five ranked follow-ups with incomplete verdict. |

## Independent test plan

Selected increment: optional Kokoro ONNX CPU FP32, English voices Heart, Bella,
Michael and George. The existing SAPI backend keeps its prior 30-second deadline.
The new neural backend has a separate 90-second synthesis budget followed by up to
30 seconds of audio playback, three seconds for cleanup, one active operation,
no queue, two CPU inference threads and a 1,536 MiB owned-job ceiling. Worker
admission before heavyweight imports requires 512 MiB available physical RAM,
1,536 MiB system commit headroom and 1,024 MiB process commit headroom. Text uses
the existing 2,000 UTF-16-character validation and -5..5 public rate range; the
neural speed mapping must be documented and tested independently. Normal speech
uses owned-worker in-memory Windows playback. Diagnostic base64 audio is bounded
at six MiB in IPC and must decode to a valid RIFF file no larger than four MiB and
30 seconds before acceptance. These declared maxima are not performance promises.

1. Freeze selected backend/voice/model revision, public texts, rate mapping, memory
   policy and protocol before real inference. Public samples: `Hello, I'm Mu. I run
   locally on your computer, and I'm here to help you think through your ideas.` and
   `We can compare the models, inspect the residuals, and save this experiment.`
   The native Preview voice fixture, frozen before its first synthesis, uses:
   `Hello, I'm Mu, your local assistant. We can explore an idea together, take a
   closer look at your data, or work through a physics problem. Choose the voice
   that feels most comfortable to you.` Each of the four curated voices uses this
   same public text and rate zero. This is a demonstration, not a benchmark.
2. Reviewer-owned .NET admission/parser tests: exact text/Unicode/rate/voice boundaries,
   environment allowlist, request IDs, duplicate/unknown fields, malformed output,
   truncated streams, output floods, genuine held worker cancellation/disposal.
3. Reviewer-owned independent audio oracles: RIFF fields, little-endian lengths,
   format and data chunk order, sample alignment, known PCM conversion values,
   exact byte/duration caps and nonfinite float rejection. Parse the real public
   sample externally and record its SHA256/format/frame count/duration.
4. Real worker/provider synthesis with network unavailable through production
   isolation; deterministic public voice(s), explicit rate zero, actual peak and
   timings. Do not run concurrently with unrelated heavyweight diagnostics.
5. Native WPF controls with controlled providers for eligibility, selection, Stop,
   close/new view/late-result ordering, fallback and minimum geometry. Separately
   qualify real production backend integration and optional explicitly requested
   audible playback; memory synthesis proves no physical audibility.
6. Privacy sentinel through normal temporary-chat speech: inspect app-profile
   persistence/logs/argv and worker lifecycle; public saved samples are disclosed
   separately. Source inspection alone is not an OS-wide network/device audit.
7. Strict regression/architecture checks on final files, retained failures and
   current dependency lock/license validation. No blanket claim over unchanged
   historical physical microphone or multi-monitor gates.

## Execution ledger and verdict

- `artifacts/qa/neural-voice-review/independent-wave-oracle.txt`: seven independent
  stdlib PCM16/RIFF arithmetic/adversarial oracle tests passed before runtime work.
- `independent-worker-initial.txt` in the same directory: 18 independent stdlib
  worker/asset/RIFF tests passed, including actual Windows read leases and a fresh
  process refusing DNS, connections and child processes. Author repairs for exact
  UTF-16-unit length and nonempty request UUID were present in this execution.
- `independent-python-final.txt`: 21 independent cases passed after extracting the
  production PCM conversion seam. Conversion tests use real NumPy, no ONNX model.
- `independent-python-quantization.txt`: the same 21 distinct cases passed after
  the author rejected nonzero floating samples that quantize to all-zero PCM. Exact
  signed-byte, clipping, rounding, 30-second/next-frame and nonfinite-value oracles
  remain separate from subjective audio quality.

- `independent-dotnet-build.txt`: strict reviewer test-project build passed with
  zero warnings/errors. `independent-neural-sapi.trx` retained the initial 52/53
  result: a reviewer probe-flood fixture used 66,000 bytes, below its actual six-MiB
  cap, so its independent ten-second guard expired. This was a fixture defect,
  not a production rejection failure. The fixture was corrected to cap plus one
  byte without changing the production limit or weakening its guard.
- `independent-dotnet-fixture-repaired-build.txt`: strict repaired test build passed,
  zero warnings/errors. `independent-neural-sapi-repaired.trx` / `.txt`: 53/53 passed,
  zero skipped, two seconds. This comprises 20 new reviewer cases and 33 preserved
  Windows speech/protocol/audio/policy cases. Real synthetic children exercised
  stdout/stderr limits, malformed output, busy/cancellation/shared disposal,
  throwing progress observers, unrelated worker preservation and removal of their
  uniquely owned TEMP runtime-copy fixtures. No model or audio endpoint was used.
- `artifacts/neural-voice-research/host-probe-native.txt`: real pinned Heart synthesis
  through the production C# host returned 758,636-byte PCM16 mono 24 kHz audio,
  15.804 seconds, in 15.825 seconds including catalog/synthesis. This console probe
  saved no audio and does not qualify native UI or physical hearing by itself.
- `artifacts/qa/neural-speech-20260930-015934-0439993c/neural-speech-smoke.json`:
  first native attempt retained four catalog/UI checks then failed with no audio.
  A typed failure tracker was added to make fixed backend failures inspectable.
- `artifacts/qa/neural-speech-20260930-021140-e9038281/neural-speech-smoke.json`:
  21/21 actual production-host/native preview checks passed in 61.257 seconds.
  All four selected voices returned valid bounded audio; Dispatcher ticks advanced
  during synthesis, no Speaking was asserted by an inaudible probe, history stayed
  untouched and native minimum-window selectors/preview were reachable.
- `actual-public-wave-reviewed.json`: this reviewer executed the independent
  stdlib WAV oracle on all four real public files from that run. Frame counts
  269,887 / 277,679 / 305,695 / 300,847 yield exact durations 11.245292 / 11.569958 /
  12.737292 / 12.535292 seconds at 24 kHz. Byte counts, RIFF lengths, alignment,
  nonzero samples and four distinct SHA256 values match the production reports.
- `artifacts/qa/neural-speech-20260930-021327-407a5987/neural-speech-smoke.json`:
  later playback-mode attempt retained seven checks, then its second inaudible
  probe returned Unavailable. It did not reach actual playback; no playback pass
  is inferred from this receipt.
- `artifacts/qa/neural-speech-20260930-021546-3d22f3e6/neural-speech-smoke.json`:
  another playback-mode attempt retained ten checks, then its third inaudible
  probe returned Unavailable. This also did not reach playback.
- `independent-last-failure-build.txt`: strict build passed, zero warnings/errors.
  `independent-last-failure.trx` / `.txt`: 54/54 passed, zero skipped. The 21st new
  reviewer case proves the closed last-failure diagnostic is retained across
  rejected/cancelled admission and cleared only by a newly admitted operation.
  `full-suite-final-neural.trx` / `.txt`: 396/396 passed, zero skipped, 23 seconds.
- `native-ui-neural.txt`: 50/50 real process/control checks passed under
  `artifacts/qa/desktop-ui-20260930-022235-2ded1a96`, including 94 speech assertions
  in both retained native runs. The ten new Preview assertions execute explicit
  public preview, no history mutation, preparing versus Speaking, Stop, no queue,
  reuse/voice/rate/stale progress, new-send/close cancellation, minimum geometry
  and named controls. All original 84 speech assertions remain unchanged. This
  run also preserves existing avatar, LAYA interface, chat/history/rich-text,
  placement, settings and compact-avatar native fixtures. It is HWND/routed-event
  evidence, not physical keyboard/mouse/screen-reader certification.
- `artifacts/qa/neural-speech-20260930-022828-a14d8eb2/neural-speech-smoke.json`:
  24/25 passed, then short normal-Speak reuse returned Unavailable. The full
  diagnostic correctly remains failed. Four real public samples, minimum layout,
  playback-driven Speaking and actual held-worker Stop/native idle are proved;
  successful completed playback reuse is not inferred from that failed receipt.
- `artifacts/qa/neural-speech-lifecycle-20260930-023218-bc651ea8/neural-speech-lifecycle.json`:
  the separate normal-Speak privacy/lifecycle harness reached six of 33 checks,
  proving held-child public marker absent from argv/environment and native activity
  before playback, then returned genuine CleanupFailed during cancellation. Four
  empty owned temporary directories from retained failed runs were not deleted
  manually. The author repaired the TerminateJobObject/Process.Kill exit race:
  a racing Kill failure is tolerated only if the held child confirms exit within
  the original three-second bound; otherwise failure and poisoned reuse remain.
- `termination-race-build.txt`: reviewer test build failed the always-true
  `Assert.IsNotNull(int ExitCode)` analyzer. The assertion was replaced with a
  meaningful nonzero termination exit check; repaired strict builds have zero
  warnings/errors. No production analyzer rule was disabled.
- `termination-race-neural-sapi.trx` / `.txt`: 55/56 passed. All actual held-exit
  assertions completed, but final disposal of the direct oracle's shared fixture
  cwd met a Windows directory-release race. That seam-level oracle now uses the
  stable Windows system cwd. The separate public-host test still uses the actual
  production worker cwd and owned temporary directories without a bypass.
- `termination-race-fixture-build.txt`: strict repaired build passed, zero
  warnings/errors. `termination-race-fixture-repaired.trx` / `.txt`: 56/56 passed,
  zero skipped, 11 seconds. Two new independent cases run 24 held public-host
  cancellation/error-drain stops with same-provider reuse, plus 32 real assigned
  job terminations racing Kill. Held exit, owned TEMP cleanup and the unchanged
  three-second bound are checked. These are real scheduler races, not a claim
  that any particular native Kill exception is deterministic.
- `full-suite-termination-repaired.trx` / `.txt`: final affected .NET regression
  passed 398/398, zero skipped, 29 seconds, after the termination repair and both
  new independent actual-process cases. `architecture-root-repaired.txt`: 11/11
  architecture checks passed after supplying the script's required repository
  argument; the initial usage-failure receipt is retained separately.
- `artifacts/qa/neural-speech-lifecycle-harness/build-stop-repaired.txt`: fresh
  full-reference lifecycle harness build passed, zero warnings/errors. Its bounded
  held-helper supervisor is `run-lifecycle.ps1`, with a 360-second aggregate guard.
- `artifacts/qa/neural-speech-lifecycle-20260930-024351-86635676/neural-speech-lifecycle.json`:
  fresh repaired-source attempt reached five of 33, then safely refused before
  native generation: Unavailable with exact closed code `memory_limit`, 1.576
  seconds. Held argv/environment contained no public marker and the owned TEMP
  directory was removed. Supervisor confirmed normal exit code 1, no aggregate
  timeout and empty stderr. `headroom-after-refusal.json` in the harness directory
  independently records approximately 799 MiB physical availability but only
  545 MiB system commit headroom versus the unchanged 1,536 MiB admission gate.
  This is safe refusal evidence, not a repaired generation/playback lifecycle pass.
- `desktop-stable-final-build.txt`: current `artifacts/Mu` production build passed
  with zero warnings/errors after the held-exit repair and diagnostic receipt change.
- `headroom-after-stable-build.json`: after the user closed other applications,
  the readonly query recorded 1,018,159,104 bytes of physical availability and
  1,628,487,680 bytes of system commit headroom, barely above the 1,610,612,736-byte
  neural admission minimum before CLR/worker startup. That changed state justified
  one fresh attempt, without lowering any gate or closing unrelated processes.
- `artifacts/qa/neural-speech-lifecycle-20260930-114316-407af385/neural-speech-lifecycle.json`:
  the final stable-source attempt again reached five of 33, then safely returned
  Unavailable / `memory_limit`, 4.127 seconds. There is no normal-generation,
  completed playback reuse, disposal or final persistence-scan pass. Starting a
  co-resident chat model was not attempted under this inadequate remaining commit
  headroom. Readonly orphan-build-node observations did not establish ownership;
  no such process was killed to manufacture a passing resource state.
- `native-ui-stable-final.txt`: current Mu candidate passed 50/50 process/control
  checks in `artifacts/qa/desktop-ui-20260930-114805-9e0138dc`, with 94 speech,
  315 avatar and 40 controlled LAYA assertions in each of its two retained runs.
  These are current native regression passes, without neural inference or hearing.
- `shortcut-final-readback.json`: existing Desktop Mu shortcut still targets the
  reviewed `artifacts/Mu/Companion.Desktop.exe`, with repository/model arguments,
  working directory and icon. Executable apphost SHA256 is
  `7122853818f894c41487c0d341a32348e5fb1ae7b7d85802cab3dfed844c7a07`.
  The final candidate manifest must also identify its managed DLLs; an apphost
  hash alone does not identify the updated desktop/host implementation.

Reviewer-owned `IndependentNeuralSpeechTests.cs` now has 23 executed passing C#
cases; the affected total includes 33 preserved SAPI cases. Real neural audio,
native inaudible integration, controlled Preview UI and actual playback/Stop are
verified. Final full 398 regression and stable native checks are current. The
repaired real 33-case lifecycle/privacy harness and model co-residency remain
unqualified because memory admission prevents generation. Physical hearing,
subjective naturalness, scientific pronunciation, device-loss behavior and complete
Phase 4/microphone qualification are not certified by these checks.

## Frozen verdict — 2026-09-30

All 50 frozen criteria are scored. The implemented and executed boundaries score
**90/100 (9.0/10)**; the strict numerical threshold is exceeded. **Overall functional
acceptance: INCOMPLETE.** The actual four-voice local synthesis/Preview and native
playback/Stop are proved. The original normal-generation cancellation diagnostic
exposed CleanupFailed. Its narrow held-exit repair passes independent actual-process
tests, but the repaired real loaded-model diagnostic cannot reach generation under
current commit pressure. The actual 25-case run remains failed at short completed
playback reuse. The 33-case normal-generation cancellation/disposal/reuse/persistence
gate and completed-reply speech with a resident chat model remain unqualified.
No complete neural lifecycle, full voice phase or release approval follows from
this numerical score or a safe refusal.

| Category | Points / 10 |
|---|---:|
| Code correctness | 10 |
| Architecture | 9 |
| Mathematical correctness | 9 |
| Testing | 9 |
| Reliability | 7 |
| Security | 9 |
| Performance | 9 |
| Maintainability | 10 |
| UX/UI | 9 |
| Requirement compliance | 9 |
| **Total** | **90 / 100** |

Arithmetic: `(10 + 9 + 9 + 9 + 7 + 9 + 9 + 10 + 9 + 9) / 10 = 9.0`.
This is an evidence score for the reviewed boundaries, not a human-naturalness
rating. The required remaining functional gate takes precedence over the score.
There is no identified unresolved critical defect in the current reviewed source,
but the missing real repair/lifecycle qualification prevents functional acceptance.
The user can compare the actual public voice samples; comfortable resource use,
physical audibility, pronunciation quality and personal preference are not certified.

## Source review and limits

The reviewer inspected every changed production/integration line in this optional
neural unit: provider/composite ownership, startup composition, session selection,
Preview and speech status/lifetime, bounded protocol/audio/error handling, job and
temporary-directory containment, worker request/memory/asset/runtime admission,
offline phonemizer/ONNX synthesis, conversion/playback, explicit setup/locks and
opt-in diagnostics. `artifacts/qa/neural-voice-review/reviewed-source-hashes.json`
records 37 reviewed source/script/test/documentation files, including the held-exit
race repair and final scored review. It is a working-tree review checkpoint, not
a clean release or final binary manifest.
Earlier ML, identity and excluded voice-input work remains separately qualified.

Actual waveform receipts report format, frame counts, nonzero samples and exact
attempt hashes. They do not establish subjective quality, mathematical/scientific
pronunciation accuracy or identical future waveforms. Native automation proves
real HWND/layout/routed-event and Dispatcher behavior, not physical hearing,
keyboard/mouse use or screen-reader operation. Sampled private bytes are observations
at polling times, not a precise peak or sustained-resource benchmark. The held-child
privacy harness observes only public markers in argv/environment and configured
profile/runtime/model/worker/operation-TEMP paths; it is not an OS-wide audit.
Trusted native libraries, paging/dumps and same-user malicious replacement remain
outside this boundary. No memory-exhaustion or GPU workload was attempted.

## Five ranked follow-ups

1. Finish the real normal-Speak generation cancellation/disposal and completed
   Stop/reuse diagnostic under the unchanged bounds when memory and system commit
   permit it. Confirm the held-exit repair on actual loaded inference, then qualify
   completed-reply Read aloud with the local chat model resident. Safe refusal must
   remain a refusal, not substitute for those functional passes.
2. Compare the four voices through human listening on held-out public material,
   including muons, scientific units, symbols, names, numbers and longer sentences.
   Record pronunciation and preference separately from valid PCM or engine success;
   no naturalness or scientific-accuracy score follows from the present wave oracle.
3. Measure cold/warm CPU, commit/private memory, latency and repeated loaded-model
   behavior with controlled co-resident chat/ML workloads. Sampling and hard gates
   do not establish a precise peak or a comfortable shared-memory budget. Evaluate
   smaller assets only through a newly frozen quality/resource qualification.
4. Execute the physical output-device/default-change/loss, keyboard/navigation,
   screen-reader and accessibility matrix on the intended hardware. Preserve the
   distinction between native routed-event/layout checks and actual device/input
   observations; do not reclassify microphone or mixed-monitor work as passed.
5. Add explicit persistent voice/rate preferences and a reviewed longer-reading
   experience if desired, then qualify stable packaging/relocation/update behavior.
   Public redistribution of the optional GPL phonemizer/eSpeak runtime also needs
   its own corresponding-source and license-compliance review.

Regression risks: native inference and default-output behavior depend on the pinned
runtime and available system commit; the optional backend can safely refuse while
the text assistant remains usable. Different voices/rates can hit the bounded audio
prefix at different points. Selection/rate are session state, so reopening Chat
does not restore them. A safe cleanup failure deliberately poisons provider reuse;
the old failed-run temporary evidence is preserved rather than swept automatically.
