# Independent review — explicit local Read aloud / Stop

QA REVIEW

Phase: **4A — bounded default-output speech increment, not full Phase 4**

Overall Score: **Not scored.**

Result: **REVIEW IN PROGRESS. No voice capability is qualified by this plan.**

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
| A07 Active deadline | A fake engine never completes; separately a native public-text attempt is measured without modifying the deadline. | Declared deadline triggers cancellation; readable text and a clear stopped/timeout state remain; cleanup outcome and any stuck native thread are recorded. Fake success alone does not prove native-driver cancellation. |
| A08 Failure fallback | No enabled voice, selected voice removed, native start/completion error and no output endpoint are injected. | Fixed understandable text fallback, no silent third-party voice/endpoint substitution, no stuck Speaking. Physical unplug/default-device changes remain outside a mock claim. |
| A09 Privacy | Use the fixed sentinel below as explicit temporary reply text; complete/stop once, then separately terminate only the owned ready app process during the operation. | Scan every bounded app-profile file and sidecar for UTF-8/UTF-16LE/UTF-16BE sentinel bytes; no audio files or content logs. Persist only content-free readiness/event metadata. OS paging/dumps/engine internals are not forensic-erasure guarantees. |
| A10 Optional audible check | S01 on the current default output; observer notes heard start, invokes Stop, then S02 once. | Retain unedited observation and event timings. Only an actual observer can mark heard start/stop; successful SAPI events alone remain engine evidence. A missing observer/device is explicitly unexecuted, not a reason to repeat until a favorable result. |
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
No executable speech evidence has yet been reviewed.

## Findings, scores and retained evidence

Pending implementation and independent execution review. No passing score is implied
by the design, registered voices, package availability or Phase 3's prior pass.

## TOP 5 RANKED FIXES / IMPROVEMENTS

Pending evidence. The final review will rank five concrete issues/improvements with
priority, regression risk and required verification even if this slice passes.

Final verdict: **NOT YET SCORED. Full Phase 4 remains incomplete.**
