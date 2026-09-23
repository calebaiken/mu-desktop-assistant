# Independent review — explicit local Read aloud / Stop

QA REVIEW

Phase: **4A — bounded default-output speech increment, not full Phase 4**

Overall Score: **9.0/10 — exactly 90/100.**

Result: **PASS for the bounded explicit speech-output development increment. Full Phase 4 remains incomplete.**

## Scope and independence

This reviewer froze the fifty criteria below on 2026-09-22 before reviewing the new
production implementation or executing native speech. The reviewer does not author
the speech engine, coordinator, UI, production fixes or evidence supervisor. Root
executes approved build/native checks; this reviewer may author separate adversarial
tests, independently inspect artifacts and require fixes. New native runs retain
all attempts, including initialization, cancellation or device failures.

The intended slice is explicit Read aloud / Stop for a completed displayed reply,
using a selected installed Windows SAPI voice, session voice/rate controls, local
plain-text synthesis and default-output playback. It is off until a user gesture.
No microphone, recognition worker, training, hosted API, automatic spoken reply,
remote lexicon or SSML interpretation is included. Speaking state comes from owned
engine events for an actual default-output operation, with identity-aware release.
Synthesis to memory alone is not audible playback or physical-device evidence.

Phase 3's independent development exit is recorded separately at 9.5/10. A passing
4A score would approve only this output slice: push-to-talk, VAD/STT, microphone
mute/capture, input/output endpoint selection, optional activation, acoustic feedback
and physical device qualification remain explicit full Phase 4 obligations. Existing
Phase 2 resource failures and Phase 11/12 release gates are unchanged.

Reference: `docs/PHASE_4_VOICE_PLAN.md`. Its V01–V12 scenarios cover a wider future
voice feature. Only the concretely applicable cases below are required for 4A;
unimplemented recognition and capture do not receive invented passing results.

## Frozen rubric

Each criterion receives 0 (missing/contradicted), 1 (partial) or 2 (supported).
Ten equally weighted categories contain exactly five criteria each. Overall score
is the sum of all fifty values divided by ten; it must be strictly greater than
8.5 without rounding. A critical unresolved correctness/security defect blocks a
pass independently of the score. Missing physical observations remain missing.

| Category | Five frozen criteria |
|---|---|
| Code Correctness | C1 strict build/regression with no hidden skipped failures; C2 explicit Read aloud accepts only an eligible completed reply and validates selected voice/rate/text; C3 exactly one operation with no implicit queued/automatic playback; C4 Stop/deadline/failure and late-event ordering cannot restart an old operation; C5 new send/new conversation/branch/mode/close/sleep terminate the owned operation and preserve text/history. |
| Architecture | A1 UI-independent validated speech contracts with typed failures; A2 Windows/System.Speech implementation isolated from Application/Core; A3 serialization and native STA/thread ownership avoid dispatcher blocking; A4 coordinator cleanly separates eligibility, lifetime and rendering state; A5 native memory diagnostic and fake providers do not bypass production bounds or become automatic user playback. |
| Mathematical Correctness | M1 exact fifty-criterion score arithmetic and bounded slice attribution; M2 independent character/rate/buffer boundary oracles; M3 PCM format/byte-count/duration checks use independent parsing and declared sample arithmetic when diagnostic PCM is produced; M4 Speaking observations distinguish engine activity, memory synthesis and physically heard output; M5 no claim of pronunciation, recognition accuracy or full voice-loop quality from nonempty bytes. |
| Testing | T1 complete regression and reviewer-authored adversarial tests execute without hidden skips; T2 cancellation/reentrancy/late-event tests use separately controlled providers rather than mirrored expectations; T3 real installed-engine memory synthesis yields bounded valid audio and successful reuse; T4 native UI checks exercise explicit controls, relevant cancellation boundaries and absent auto-play; T5 content-bearing text/audio is absent from every app-controlled profile file after normal and abrupt termination, with synthetic fixtures and encoding policy frozen first. |
| Reliability | R1 no usable voice/no device/removed voice/native exception remain recoverable text-only failures; R2 Stop/deadline/disposal have declared measured acknowledgment bounds and no stale Speaking lease; R3 callbacks identify their operation and cannot mutate a newer conversation/window; R4 engine/thread/event handlers/buffers are released or honestly bounded when a native engine stalls; R5 repeat start/stop/reuse, close and shutdown leave no application-owned active operation or unbounded queue. |
| Security | S1 speech requires an explicit gesture and performs no microphone/network activity; S2 model text is plain text, never SSML, URI/lexicon inclusion or an executable command; S3 eligible voices are enumerated/validated and no unknown installed third-party voice is silently selected; S4 no text/audio-bearing logs, temporary audio files, command-line text or automatic transcript persistence; S5 errors/diagnostics expose fixed codes and bounded non-content metadata, with private-mode behavior preserved. |
| Performance | P1 text/work/active-operation/diagnostic PCM bounds are enforced before unbounded work; P2 native synthesis and teardown do not synchronously stall the dispatcher; P3 active deadline/stop/resource cleanup measurements distinguish deterministic fixtures from native engine limits; P4 no engine polling/capture work when speech is unused or idle; P5 package/resource measurements and existing host resource failures remain accurately separated. |
| Maintainability | N1 small readable contracts/coordinator/native adapter responsibilities; N2 pinned System.Speech dependency, package/license/lock and platform metadata reviewed; N3 deterministic fault/event injection seams do not weaken production validation; N4 commands/artifacts/hashes/voice identity make the diagnostic reproducible without storing user audio; N5 contracts/status/voice plan/limitations agree with the implemented slice and observed failures. |
| UX/UI | U1 explicit Read aloud/Stop state and no surprise speech; U2 selected installed voice and session rate controls have understandable labels, bounds and text fallback; U3 stop, interruption, timeout and unavailable-engine messages preserve the reply and permit reuse; U4 Speaking lease reflects the owned output event lifetime, including sleep/reduced motion and late completion; U5 native layout/keyboard/automation evidence and any actual audible observation are reported honestly, with physical focus/device/accessibility gaps retained. |
| Requirement Compliance | Q1 local pretrained/no-API-key direction is preserved; Q2 only the advertised output slice is implemented and scored; Q3 wider R09 microphone/STT/VAD/activation/device/feedback requirements remain explicit unfinished work; Q4 independent review, frozen criteria, retained failures and strict pass/fix loop are followed; Q5 no full Phase 4, resource qualification, human speech quality or release claim is inferred from the incremental score. |

## Protocol frozen before execution

The production limits were confirmed by root before implementation: 2,000 UTF-16
characters with safe Unicode handling, rate **-5 through +5**, one active operation
and no queue, a 30-second active utterance deadline and a three-second Stop/disposal
acknowledgment bound. Default-device SAPI does not
expose a precomputed PCM allocation limit; its text/time bounds must not be described
as a measured audio-byte cap. The separate memory diagnostic uses a strict 4 MiB
write limit, checks duration independently and retains hashes/metadata only.
Voice and rate are session-only controls for this slice; durable preferences require
a later reviewed change. No value may be loosened after observing outcomes without a distinct failed
protocol and newly identified experiment.

Public fixed utterances:

- S01: `This is the local voice playback check. Press Stop to end it.`
- S02: `My fictional pet is named Maple.`
- S03: literal markup-like text `<speak>Do not fetch https://example.invalid/voice.</speak>`.
- S04: `Maple ` repeated exactly 330 times (1,980 UTF-16 characters), rate **-5**.
  This public native default-output deadline fixture was added before the first
  native speech outcome. It must end with typed Timeout after **30–33 seconds**
  measured from the provider call, with one owned worker and confirmed exit.

S03 is a scripted plain-text routing/security fixture. It is never rendered as
SSML, passed as a remote resource or used to claim a network connectivity test.
For all native attempts, record enabled voice ID/name/culture and selected rate,
package and executable/assembly hashes, OS/runtime versions, elapsed timestamps,
event order, completion/cancellation/failure classification and count/size metadata.
Do not record user text, actual audio bytes or machine device identifiers beyond
what the local public diagnostic needs. A registered voice is not a proved usable
engine until initialization/synthesis succeeds.

| Case | Fixed action | Required observation / evidence distinction |
|---|---|---|
| A01 Idle and explicit start | Fresh isolated profile; open chat, display a scripted completed reply, open controls, then idle for a declared 60 seconds. | Zero speech starts, no capture construction, no queued operation. Read aloud is the only entry that starts the selected utterance. Source-only absence of microphone calls is distinguished from OS-wide device monitoring. |
| A02 Validation | Empty/whitespace, 2,000 and 2,001 UTF-16 characters, an astral pair at the boundary and unpaired surrogates; rates -5/+5 and -6/+6; missing/disabled/unknown voice; repeated start. | Exact admission/rejection boundaries and safe Unicode, no unbounded queue or native initialization for invalid input; content-free typed failures; valid reuse afterward. |
| A03 Native memory synthesis | S01 then S02 through one enabled installed Microsoft voice at rate zero; bounded in-memory destination only. | Nonzero independently valid PCM/WAVE, declared format, length/hash and computed duration within frozen cap; clean completion and reuse. No saved WAV, no avatar/audibility inference from memory-synthesis events. |
| A04 Bound/fault stream | Independent fake writer tries exactly the memory limit then one byte more; malformed format, declared size overflow and native failure are injected. | Bounds fail before excess allocation/write; fixed error; owned buffers disposed; a subsequent valid request works. The real voice need not generate an enormous file to test this boundary. |
| A05 Stop/order | Cancel before start, during queued initialization and active output; deliver completion-after-cancel, duplicate start/completion and old-operation events after replacement. | Old events cannot resume speech, release a newer Speaking lease or change another conversation; one completion outcome; bounded Stop acknowledgment. |
| A06 UI cancellation matrix | During fake/native controlled output: new send, new conversation, selected branch, saved/temporary mode switch, close and companion sleep. | Appropriate stop happens before subsequent operation; displayed/saved text remains; no stale callback reopens a closed UI; controls permit valid reuse where applicable. |
| A07 Active deadline | A fake engine never completes; separately run native S04 once at rate -5 without modifying the deadline. | Typed Timeout in 30–33 seconds and confirmed owned-worker exit; readable text and a clear stopped/timeout state remain. Record start/stop event counts and cleanup outcome. Fake success alone does not prove native-driver cancellation. |
| A08 Failure fallback | No enabled voice, selected voice removed, native start/completion error and no output endpoint are injected. | Fixed understandable text fallback, no silent third-party voice/endpoint substitution, no stuck Speaking. Physical unplug/default-device changes remain outside a mock claim. |
| A09 Privacy | Use the fixed sentinel below as explicit temporary reply text; complete/stop once, then separately terminate only the owned ready app process during the operation. | Scan every bounded app-profile file and sidecar for UTF-8/UTF-16LE/UTF-16BE sentinel bytes; no audio files or content logs. Persist only content-free readiness/event metadata. OS paging/dumps/engine internals are not forensic-erasure guarantees. |
| A10 Optional audible check | S01 on the current default output; Stop on its first engine-start event, then S02 once. A separate observer may report actual hearing. | S01 must have exactly one Speaking and one Stopped event, cancellation acknowledgment within three seconds and confirmed held-child exit. S02 must have exactly [Speaking, Stopped] and confirmed exit. Retain unedited timings/observations. Only an actual observer can mark heard start/stop; events alone remain engine evidence. A missing observer/device is explicitly unexecuted, not a favorable retry. |
| A11 Repeat/dispose | Ten fixed sequential S02 start/stop or completion cycles, then close and 60 seconds idle. | Per-cycle outcomes/timings, one owner, no queue accumulation; native threads/handles/private bytes sampled with method and baseline disclosed; no resource qualification inferred from these samples. |
| A12 Rich/private isolation | Read displayed completed plain text from ordinary, rich-rendered and temporary replies; reject interrupted/in-progress text if ineligible by contract. | Content is passed once as text independent of Markdown/LaTeX renderer; no synthesis auto-starts on reply/mode change; no clipboard or persistence side effect. |

Privacy sentinel (ASCII, public fixture only):
`PHASE4A_TEMP_SPEECH_6E31D790B7A94F54B2A937AD55C00812`

The abrupt-exit helper must be a separate explicit diagnostic flag with an isolated
fresh profile, no default-profile reuse and a held child-process identity. It must
never terminate processes by name. The readiness marker must be atomic and contain
no sentinel, speech text or audio. No fixture silently enables audible output; its
output sink and observation level are recorded. A mock-provider crash fixture is
whole-app persistence evidence, not proof about a third-party speech engine's OS
behavior.

Independent PCM arithmetic: for integer PCM, block alignment is
`channels * bitsPerSample / 8`, byte rate is `sampleRate * blockAlign`, and duration
is `dataBytes / byteRate`. Require positive bounded fields and aligned sample data;
use checked arithmetic and inspect the actual RIFF chunks rather than a fixed
44-byte-header assumption. Unsupported compressed formats fail the diagnostic or
are explicitly converted under a separately bounded reviewed contract. No WER or
recognition score is applicable to this synthesis-only increment.

## Quantitative confirmation

Root confirmed the stated text/time/cleanup/queue/memory limits and session rate
range on 2026-09-22, explicitly before provider implementation began. An owned
helper process may be selected to enforce a hard deadline against blocked native
COM; this changes the implementation, not the frozen limits or evidence bar.
Any introduced process/IPC boundary also requires adversarial framing, owned-child
termination and content-free diagnostics within the applicable frozen criteria.
Executed evidence is recorded below; frozen actions and limits remain unchanged.

## Findings, scores and retained evidence

The verdict is based on the distinct executed artifacts below, not the design,
registered voices, package availability or Phase 3's prior pass. Unexecuted portions
of the frozen protocol receive partial credit and remain explicit follow-up work.

### Source review and independent tests before the first build

The initial `WindowsSpeechOutput` cleared its active operation after cleanup failure
without preventing another child launch. Because failure means the previous owned
child's exit was not confirmed, this could violate the one-operation/no-accumulation
invariant. The reviewer reported the finding; the author added permanent
`CleanupFailed` rejection on that instance. This is a source finding and fix, not a
claim of a reproduced native kill failure. No arbitrary real process was used as a
kill-denial fixture.

The reviewer also examined redirected-pipe cancellation rather than assuming a
CancellationToken necessarily interrupts native I/O. Pinned .NET 10.0.12 creates
synchronous redirected FileStreams, while its asynchronous RandomAccess path uses
the runtime's I/O-cancellation helper. This is not evidence of a known ignored-token
bug. The speech author additionally made cancellation queue idempotent owned-job/
process cleanup off the caller thread, and bounded the child command/EOF awaits.
Native acknowledgment timing still requires execution. Source references:
[Process redirection](https://raw.githubusercontent.com/dotnet/runtime/v10.0.12/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/Process.Windows.cs),
[RandomAccess cancellation](https://raw.githubusercontent.com/dotnet/runtime/v10.0.12/src/libraries/System.Private.CoreLib/src/System/IO/RandomAccess.Windows.cs).

Microsoft documents that cancelling asynchronous speech still raises its completion
event, and that stream output remains referenced until the synthesizer is
reconfigured. The implementation now returns terminal worker messages after native
resource disposal and treats post-terminal parent EOF as acknowledgment. Late-event
and cancelled-completion behavior must be tested rather than inferred from the
event name. Sources: [speech cancellation](https://learn.microsoft.com/en-us/dotnet/api/system.speech.synthesis.speechsynthesizer.speakasynccancelall),
[stream-output ownership](https://learn.microsoft.com/en-us/dotnet/api/system.speech.synthesis.speechsynthesizer.setoutputtowavestream).

Reviewer-authored files:

- `IndependentSpeechProtocolTests.cs`: 16 methods covering UTF-16/voice/rate
  boundaries, literal markup, closed fields/unions/version/operation IDs, malformed
  UTF-8 and escaped surrogates, duplicate inventory IDs, bounded frame/depth/EOF,
  one-byte fragmentation, pre/during-read cancellation and reject-before-write.
- `IndependentSpeechWorkerAdmissionTests.cs`: five methods covering fixed worker
  mode/private pipes/environment allowlist, same-executable/local-path admission,
  pre-cancellation for every operation, invalid-content redaction without spawning,
  and concurrent idle disposal. These fixtures do not start a native child or audio.

The author owns separate bounded-buffer/RIFF tests. Native UI helpers use controlled
fake output; the abrupt-exit temporary-speech helper uses real WPF/SQLite with a
fake held speech operation. Their scope does not establish third-party SAPI engine
persistence, physical audibility, device loss or actual native Stop latency.

### First strict regression, before voice-selection hardening

The reviewer independently parsed
`artifacts/qa/phase4a-verification/phase4a-before-voice-selection-fix.trx`: **301/301
executed and passed**, zero failed, errored, timed out or not executed. All **21**
reviewer-authored speech tests passed. SHA256:
`6AFF085C84A1A23609741B81CD06DE7CF8ECA1BE9AF62E802AFD53AD879483FE`.
This evidence does not establish later production edits or native voice operation.

Before native execution, the speech author found that the pinned library's
`SelectVoice` path initializes a default voice before matching the requested one.
The reviewer independently checked that source path and asked whether disabling
other tokens itself loads the default. The author's read-only inspection of the
exact shipped DLL confirmed that the Enabled setter calls CurrentVoice. Therefore
sequential token disabling alone is not adequate to exclude an installed third-party
default. The author implemented conservative mixed-engine refusal before such
setters/selection; no third-party engine was activated as a test. This installation
limitation and trusted-OS-metadata boundary must be visible in final documentation.
Source: [pinned voice selection implementation](https://raw.githubusercontent.com/dotnet/runtime/v10.0.12/src/libraries/System.Speech/src/Internal/Synthesis/VoiceSynthesis.cs).

### Hardened strict regression and inaudible native engine

The reviewer independently parsed
`artifacts/qa/phase4a-verification/phase4a-final-305.trx`: **305/305 executed and
passed**, all failed/errored/timed-out/skipped counters zero. All 21 reviewer tests,
eight author buffer/RIFF tests and four author voice-policy tests passed. SHA256:
`9CE09C3368E08916F0AC6AC5473A58B51EF7E986C07CDCE174ACF2E8CA66FD77`.
Root reports 11/11 architecture checks and zero build warnings/errors. The later
Desktop-only mixed-installation help text change is attributed separately from this
provider/test build; it does not silently inherit a new full-regression result.

The reviewer read every observation in
`artifacts/qa/speech-20260922-223108-36a69151/profile/speech-output-smoke.json`
and its plan/execution files: **PASS**, no failures, process exit zero, no supervisor
timeout and `playbackRequested=false`. Five admitted voices were enumerated. The
selected voice was **Microsoft David**, ID `MSTTS_V110_enUS_DavidM`, culture `en-US`.
The fixed S01/S02 memory probes used rate zero; pre-cancellation was also admitted
as a cancelled outcome without playback. Runtime was 10.0.12 on Windows 10.0.26200.

| Public fixture | Total / data bytes | Format | Independently recomputed duration | Synthesis call elapsed |
|---|---:|---|---:|---:|
| S01 | 221,380 / 221,334 | 22,050 Hz, mono, 16-bit PCM | 5.01891156462585 s | 1.4259348 s |
| S02 | 116,650 / 116,604 | 22,050 Hz, mono, 16-bit PCM | 2.6440816326530614 s | 0.7351629 s |

For each returned chunk map, RIFF begins at offset zero, `fmt ` begins at 12 with
18 payload bytes, and `data` begins at 38. Its eight-byte chunk header plus the
reported data payload ends exactly at total length. Data counts are positive,
sample-aligned and below 4 MiB; sample byte rate is independently 44,100 bytes/s.
The production parser observed nonzero sample data and returned distinct 64-digit
SHA256 hashes. The reviewer recomputed these metadata invariants, but did **not**
receive raw native PCM for a separately implemented parser or hash verification.
No audible output, physical hearing or full pronunciation quality is inferred.

The execution plan retained Infrastructure SHA256
`D2DC53F590D7F43BA1393CDF63A956FD31D55E99AF13445B7F33275606A4EACA`,
System.Speech SHA256
`94ADAA46084BEF72461C65E38DEC2AFF3C7AFD48ABC1A27976AE87B2B9FFD37F`,
and the pre-help Desktop SHA256
`7890E50BC099A67D3F367CA927740585D5AE62FD37D03C642031D26B7166C068`.
Profile logs contain only structured Started/Stopping entries, and no WAV was
retained. This public diagnostic is not the frozen temporary-sentinel privacy test.

### Retained first combined native UI timeout

Root's first combined run after the Desktop help text change,
`artifacts/qa/desktop-ui-20260922-223218-e3d73fd1`, exceeded its existing 60-second
whole-window supervisor limit and was terminated through its held process identity.
The reviewer read `profile/ui-stages.txt`: earlier suites reached
`companion-reply-complete` at **52.516 seconds**. No speech result file was produced,
so this attempt is **not** a passing 84-case speech run. Privacy did not execute as
part of the failed command chain. This establishes a suite timeout, not by itself
a native speech-engine or production UI deadlock. Any revised overall suite budget
must be identified in a separate retained attempt; production and per-operation
limits remain frozen.

### Abrupt temporary-speech persistence check

`artifacts/qa/private-speech-20260922-223824-cd48804a/result.json` reports **PASS**
with a ready temporary session, one saved SQLite decoy, one active explicit fake
speech operation, and termination of the held owned process. The reviewer read the
ready marker and independently scanned **all four profile files** as raw bytes for
the frozen sentinel encoded in UTF-8, UTF-16LE and UTF-16BE: **12 comparisons, zero
matches**. Every retained file hash matched the supervisor report. The files were
the 32,768-byte database, empty instance lock, 92-byte content-free marker and
177-byte structured application log; no audio file was created.

This run used Desktop SHA256
`F80415BCB7EA4FE049C6E7D89BF2BABFF0D8BE3C813C7AA455EBE0420948B836`
with the same hardened Infrastructure hash as the 305-test/inaudible build. It
checks app-controlled persistence after abrupt termination of a real WPF/SQLite
session with fake output. It does not inspect native voice-engine behavior, OS
paging or crash dumps. The separately frozen normal-completion/stop-and-exit
sentinel scan has not yet executed and must not be inferred from this crash check.

### Native default-output Stop, reuse and active deadline

The reviewer independently read the plan, every observation and execution result in
`artifacts/qa/speech-20260922-223856-66dd24ca`: **PASS**, no failures, exit zero and
no supervisor timeout. Playback was explicitly requested. All six recorded binary
hashes matched the built files at review, including Desktop `F80415…8B836`,
Infrastructure `D2DC53…4EACA` and System.Speech `94ADAA…D37F` as recorded in full
above. The same Microsoft David voice was used. S01/S02 memory metadata and hashes
matched the earlier inaudible attempt; no native audio bytes were persisted.

| Frozen operation | Observed result | Event / owned-worker evidence |
|---|---|---|
| S01 Stop at first engine-start event, rate 0 | Cancelled; **0.0038051 s** Stop acknowledgment, within 3 s | Exactly one Speaking and one Stopped; held worker 32460 exited |
| S02 explicit reuse, rate 0 | Successful completion in **3.7085101 s** | Exactly Speaking → Stopped; held worker 36120 exited |
| S04 1,980 characters, rate -5 | Typed Timeout after **30.0156752 s**, within frozen 30–33 s | Exactly Speaking → Stopped; held worker 11336 exited |

The full diagnostic took 37.5404671 seconds. Its three profile files were the empty
instance lock, content-free metadata report and structured Started/Stopping log.
These results support real default-output engine operation and ownership cleanup.
They do not measure the speaker's acoustic stop time or prove that a person heard
the output: `physicalHearing` remains explicitly **UNOBSERVED** and microphone
capture **NOT_IMPLEMENTED**. S04 is a deliberately long real utterance, not a test
of an artificially hung third-party COM driver or an injected OS kill failure.

### Final controlled native UI and binary scope

The separate, predeclared **120-second aggregate** attempt is retained at
`artifacts/qa/desktop-ui-20260922-224213-17740e63`. The reviewer parsed both
`first-run` and `profile` reports: **84/84 distinct speech assertions passed in
each**, with matching declared/array/unique counts and null failure. The same checks
passed for the other nine native reports in both runs: compact 51, appearance 43,
desktop integration 24, settings lifecycle 29, placement 49, chat 54, history 50,
renderer 46 and reply preview 34. Root reports supervisor exit zero and **38/38**
process/CLI controls passed; this latter count is attributed to root's execution
output, not independently reconstructed from the JSON assertion arrays.

The reviewer also parsed both content-free speech stage sequences: eight groups
and 84 unique check records each, ending at **19.649 seconds** and **28.045 seconds**
inside the speech helper. The overall suite reached settings completion at
**62.385 seconds** and **75.407 seconds**, respectively. The first run's speech stage
ran from 41.294 to 60.966 seconds. This supports the expanded-suite budget diagnosis;
it does not erase the earlier 60-second timeout. Neither production 30/3-second
limits nor the fixture's individual three-second waits were increased.

`artifacts/qa/phase4a-verification/final-native-build.json` records final Desktop
SHA256 `178F0A74E15B2BAA14DD53EBBA393BB9C290E4AC0014AA1E1DC59FC7DF362D07`.
The reviewer matched all six manifest hashes against the built files. This Desktop
change adds only diagnostic substage records after the prior help-text build;
Infrastructure/Application/Core/System.Speech remained identical to the recorded
305-test, native engine and privacy evidence. No repeated speech-engine pass is
invented for a different provider binary.

The controlled UI results cover explicit controls, completed-only eligibility,
literal rich text, Unicode clipping, voice/rate bounds, cancellation during
enumeration, held cleanup, duplicate/old events, new sends/chats/branches/modes,
history opening/editing/close, fixed error fallback, minimum layout and automation
names. They do **not** exercise App-level sleep-to-Speaking-lease wiring, physical
keyboard/screen-reader use or real missing/unplugged audio endpoints. Source review
supports those wired cancellation paths; source evidence is not relabelled as an
executed native integration fixture.

### Narrow item-accessibility correction after the native regression

Root's subsequent actual-UI inspection found that expanded voice item peers
exposed record `ToString()` representations, although their visible labels were
friendly. The 84-case helper had checked the selector controls' names, not expanded
item peers. Root added only `ComboBox.ItemContainerStyle` bindings from
`AutomationProperties.Name` to voice `Name` and speed `Label`, then rebuilt Desktop
with zero warnings/errors. The reviewer inspected those two bindings.

The reviewer independently read
`artifacts/qa/phase4a-verification/actual-voice-item-names.txt` and
`actual-rate-item-names.txt`: the retained actual UI trees expose **five friendly
voice names** and **eleven friendly speed names**, with no raw record representations.
Root opened options without playback. Its one stale-tooltip coordinate-geometry
tool failure was recovered with a fresh screenshot; this was not a reproduced
application failure or a silently discarded product assertion.

`accessible-voice-items-build.json` attributes this narrow change to Desktop SHA256
`05F47A3E5EF45D9A206680F9178A1CCABF749A4509687A070F2F49AD3B54F91C`.
The reviewer matched all six manifest hashes against the current built files;
the other five binaries are unchanged. The full native suite and speech-engine
measurements retain their earlier binary attribution. This focused UI recheck does
not claim another 305-test/native-synthesis run or a human screen-reader assessment.
The score stays **9.0** and U5 retains partial credit for its remaining physical
input/accessibility observations.

### Fifty-criterion score

The entries refer to the unchanged frozen criteria above. Each value is 0, 1 or 2.
No unexecuted physical, idle, repeat, normal-privacy or active-disposal case receives
an invented passing observation.

| Category | Exact criterion values | Score / 10 | Evidence and partial-credit reason |
|---|---|---:|---|
| Code Correctness | C1=2, C2=2, C3=2, C4=2, C5=1 | 9 | Strict regression, native engine and controlled UI support admission, ordering and no queue. The App sleep integration remains source-reviewed rather than exercised. |
| Architecture | A1=2, A2=2, A3=2, A4=2, A5=2 | 10 | UI-independent typed port, Infrastructure-owned STA worker, off-dispatcher operation/cleanup and bounded diagnostics preserve boundaries. |
| Mathematical Correctness | M1=2, M2=2, M3=1, M4=2, M5=2 | 9 | Independent boundary and returned chunk/duration arithmetic are supported. Native raw PCM was not independently reparsed or rehashed; no accuracy/audibility inference is made. |
| Testing | T1=2, T2=2, T3=2, T4=2, T5=1 | 9 | All 305 tests, two 84-check UI runs, native reuse and abrupt privacy evidence pass. The exact-sentinel normal-stop/exit scan remains unexecuted. |
| Reliability | R1=1, R2=1, R3=2, R4=1, R5=1 | 6 | No-voice/typed-error fallback and callback identity pass. Native Stop/deadline/exit pass; active provider disposal, real endpoint removal, deliberately blocked COM/denied cleanup and ten-cycle shutdown/resource cases lack dedicated execution. |
| Security | S1=2, S2=2, S3=2, S4=2, S5=2 | 10 | Explicit plain-text-only output, private bounded protocol, pre-selection mixed-engine refusal, fixed failures and app-profile privacy checks support the scoped boundary. OS voice metadata remains trusted, not cryptographically authenticated. |
| Performance | P1=2, P2=2, P3=2, P4=1, P5=2 | 9 | Text/time/memory/one-operation bounds and measured native cancellation/deadline hold. The frozen 60-second unused/idle observation is absent; prior resource qualification remains FAIL. |
| Maintainability | N1=2, N2=2, N3=2, N4=2, N5=2 | 10 | Small typed responsibilities, pinned/locked dependency and documented provenance, separate deterministic fixtures, exact commands/hashes and explicit scope/limitations support continued work. |
| UX/UI | U1=2, U2=2, U3=2, U4=1, U5=1 | 8 | Explicit Read/Stop, session options, preserved text, fixed fallback and controlled native layout/names pass. App sleep/lease integration and physical hearing, keyboard, focus and screen-reader checks remain incomplete. |
| Requirement Compliance | Q1=2, Q2=2, Q3=2, Q4=2, Q5=2 | 10 | Local pretrained/no-key direction, frozen independent review, retained failed attempt and honest incremental scope are preserved. Full R09 and release/resource gates are not advanced. |

Arithmetic: **9 + 10 + 9 + 9 + 6 + 10 + 9 + 10 + 8 + 10 = 90**; exactly
**50 criteria**, so **90 / 10 = 9.0**, strictly greater than 8.5 without rounding.
No critical unresolved production correctness/security defect was found within this
bounded output slice. Lower reliability coverage is explicit and does not authorize
new device, capture, background activation or release claims.

## TOP 5 RANKED FIXES / IMPROVEMENTS

| Rank | Priority / improvement | Regression risk | Required verification |
|---|---|---|---|
| 1 | P2: execute active-disposal and worker fault/cleanup scenarios, including blocked work and failed cleanup poisoning. | High if lifetime or process ownership changes. | A controlled worker/process seam must retain exact production admission; record active Dispose acknowledgment, fixed errors, held-child exit or explicit poisoned state, and absence of additional children. Never kill by process name. |
| 2 | P2: close the App sleep/lease and normal temporary-speech persistence gaps. | Medium around cross-window ownership and saved/temporary transitions. | Real App wiring with controlled output must stop on sleep without a late event waking/releasing another operation. Complete/stop the frozen sentinel, exit normally and scan every profile file in all three encodings. |
| 3 | P2: execute A01/A11's declared idle and ten-cycle observations. | Low for observation; medium if optimization follows. | Preserve ten per-cycle outcomes, one-owner/no-queue checks, 60-second idle windows and disclosed thread/handle/private-byte baselines. Keep historical CPU qualification failures separate. |
| 4 | P1 before release/device claims: test physical output/fallback and accessible interaction; retain the mixed-engine installation limitation. | Medium if changing voice policy or device routing. | Actual hearing, mute/no-endpoint/unplug/default-device changes, keyboard/focus/screen-reader operation and recovery need distinct observations. Mixed third-party registrations must remain refused until a separately reviewed selection boundary exists. |
| 5 | P2: independently validate native PCM in memory. | Low if diagnostic-only; medium if format support expands. | A separately implemented bounded RIFF oracle should parse the actual native buffer, recompute sample arithmetic/hash and dispose it without writing audio. Keep compressed/unknown formats rejected unless a new bounded contract is reviewed. |

Final verdict: **PASS 9.0/10 for explicit local Read aloud / Stop development only.**
Full Phase 4 remains **incomplete**: microphone capture/mute, VAD/STT, push-to-talk,
input/output selection, optional activation and acoustic feedback/physical-device
qualification still need their own implementation and independent acceptance.
Phase 2 resource failures and Phase 11/12 release gates remain unchanged.
