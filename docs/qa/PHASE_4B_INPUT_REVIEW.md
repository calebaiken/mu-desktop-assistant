# Independent review — offline push-to-talk to an editable draft

QA REVIEW

Phase: **4B — bounded local voice-input increment, not full Phase 4**

Overall Score: **Not scored.**

Result: **FROZEN REVIEW PROTOCOL. Implementation and execution are not a pass.**

## Scope, authority and independence

Frozen on 2026-09-22 before Phase 4B production implementation. The reviewer read
the original brief's voice, privacy and mandatory QA sections, the full
`docs/PHASE_4_VOICE_PLAN.md`, R09, current architecture/contracts and the 4A review.
The later user direction requires pretrained local models, no API key and no cloud
fallback. This reviewer does not author production capture, recognition, process
supervision or UI. Root owns implementation integration and execution; separately
authored reviewer tests and artifact/source inspection provide independent evidence.

The intended flow is explicit hold → the selected microphone endpoint → bounded
PCM → local Silero VAD and Whisper recognition → an **editable input draft**.
Recognition never sends a message, invokes a tool, changes settings or speaks an
answer automatically. Human microphone capture is not authorized merely by this
development protocol: a deliberate user recording gesture or separately confirmed
physical test is required. Default diagnostics use generated audio/fake devices,
never a real microphone or loopback capture. Enumeration must not start capture.

4A passed at 9.0/10 for explicit output only. Its physical-device, App sleep,
normal-sentinel, idle/repeat and fault-coverage gaps are not inherited as passes.
Full Phase 4 still needs its complete device/feedback/activation/voice-loop gates.
Existing Phase 2 resource failures and Phase 11/12 release qualification remain.

## Frozen fifty-criterion rubric

Each criterion is 0 (missing/contradicted), 1 (partial) or 2 (supported by evidence).
Ten equally weighted categories each contain exactly five criteria. Score is the
sum of all fifty values divided by ten and must be **strictly greater than 8.5**,
without rounding; a critical unresolved defect independently blocks a pass. No
physical, accuracy, resource or privacy result may be inferred from source alone.
The final review will retain every attempt and rank five fixes even if it passes.

| Category | Five frozen criteria |
|---|---|
| Code Correctness | C1 strict regression and independent malformed/boundary tests pass with explicit non-pass counts; C2 only an enabled, deliberate held gesture opens exactly the selected endpoint, with no implicit/default fallback or queued capture; C3 deliberate release stops capture before recognition, while loss/cancel invalidates the operation and discards its result; C4 only a valid bounded transcript for the same session/conversation and unchanged empty draft can populate input, never autosend or overwrite edits; C5 mute, Sleep, Hide, new conversation/mode/branch, close and shutdown stop owned input/recognition and prevent late restart or wrong-view mutation. |
| Architecture | A1 typed immutable device/PCM/transcript/failure ports remain independent of WPF and native APIs; A2 capture/resampling, VAD/STT and UI orchestration have separate owners; A3 event-driven capture releases native packets on their owning thread and performs no blocking pipe/model work while holding a packet; A4 one owned worker/session with private bounded IPC, atomic job admission and explicit lifetime is maintained; A5 half-duplex arbitration serializes output, capture, recognition and model inference without callback-driven implicit activation. |
| Mathematical Correctness | M1 independent byte/sample/channel/rate/duration boundary oracles agree with the actual data; M2 PCM16/float conversion, channel handling, silent packets and any resampling preserve declared amplitude/rate and reject invalid/nonfinite input; M3 WER normalization, edit-distance arithmetic and reference denominator are frozen and independently checked; M4 deterministic padding/noise/SNR and sample hashes are reproducible and measured independently of recognizer claims; M5 no-speech/VAD, timestamps and confidence are interpreted correctly, with no claim of human accuracy or mathematical truth from synthetic recognition. |
| Testing | T1 full prior regression plus reviewer-authored admission/protocol/concurrency tests execute without hidden failures/skips; T2 controlled device/worker fixtures cover ordering, overflow, partial frame, cancellation, malformed output and late events; T3 all fixed public clean/padded/noisy and silence/non-speech recognition outcomes are retained and independently scored; T4 real WPF controls exercise hold/release/loss, draft protection, mute, cancellation and state indicators using controlled providers; T5 normal and abrupt temporary-input persistence checks scan all app-profile files for frozen text/PCM fixtures while separately identifying unexecuted physical capture. |
| Reliability | R1 missing/denied/invalidated/changed-format endpoints and missing/corrupt models fail to recoverable typed text states without switching devices or models silently; R2 capture stop meets the frozen fake/native acknowledgment bounds with no retained input stream; R3 recognition cancel/deadline/dispose and unexpected worker death confirm owned exit or poison the owner rather than permit accumulation; R4 stale callbacks, in-flight draft edits and conversation/mode/close races cannot accept partial or wrong-session output; R5 sequential reuse, active disposal and parent termination leave no orphan worker, capture endpoint, lease or unbounded queue. |
| Security | S1 microphone/activation is off by default and requires explicit enablement plus an active gesture, with continuous visible recording state; S2 audio/transcript is inert untrusted data with no executable/SSML/tool/prompt authority or automatic Send; S3 pinned local assets and exact worker identity, restricted inherited handles/environment, private IPC and no inference network/cloud fallback are enforced; S4 audio, unsubmitted drafts and temporary transcripts never enter app-controlled files/argv/logs, and owned buffers are cleared on every terminal path; S5 unknown/duplicate/oversized/malformed protocol fields, counts, identities and terminal sequences fail closed with content-free errors. |
| Performance | P1 capture, ingress queue, transcript, worker time/memory and one-operation bounds are enforced before excess allocation/work; P2 capture/conversion/recognition/cleanup stay off the dispatcher and unused input performs no polling/capture work; P3 cold load, decoding, stop/cleanup and observed memory are measured separately with unchanged failed targets retained; P4 ten-cycle and declared idle observations check ownership/handle/buffer growth and no residual capture/worker; P5 the 8 GB target, model footprint, serialized STT/LLM work and historical desktop resource failures are reported without unsupported real-time or total-memory claims. |
| Maintainability | N1 small readable contracts/coordinators/native adapters have explicit ownership and cancellation semantics; N2 runtime/model/VAD/dependency versions, hashes, source/license/ABI and asset lifecycle are documented; N3 deterministic synthetic/device/protocol seams test faults without weakening production consent, process or validation boundaries; N4 pre-run manifests and bounded content-free/public-fixture artifacts reproduce every attempted result without audio files; N5 architecture/contracts/status/privacy/voice plan and missing physical/full-phase requirements agree with the implementation and evidence. |
| UX/UI | U1 clearly labeled English-only hold-to-record, selected input, mute and Stop controls are understandable and never surprise-record; U2 mouse and keyboard gesture lifetime/loss have visible recording/transcribing/stopped states and safe focus behavior; U3 existing draft/selection is preserved, a recognized draft is editable and submission remains explicit; U4 actual capture/recognition events own Listening/Thinking state and release on cancel/mute/sleep/close, with visible privacy indication under reduced motion or hidden hover controls; U5 layout, control/item accessible names, keyboard and actual device observations are honestly distinguished from property-only and fake-device evidence. |
| Requirement Compliance | Q1 local pretrained/no-key/no-cloud direction is preserved; Q2 this increment supplies only the advertised input-to-draft behavior and never claims a complete voice loop; Q3 required mute/input selection/cancel/feedback boundaries are implemented or explicitly incomplete, while output selection/activation and physical gates retain full R09 scope; Q4 independent preimplementation freeze, exact arithmetic, retained failures and strict score/fix gate are followed; Q5 no human recognition quality, acoustic echo cancellation, device/resource certification or release readiness is invented from synthetic tests. |

## Frozen limits and admission

Root confirmed these limits before production implementation:

| Boundary | Frozen value / behavior |
|---|---|
| Recognition PCM | Signed PCM16 little-endian, mono, exactly 16,000 samples/s; valid even byte count |
| Frame | 20 ms = 320 samples = 640 bytes; final partial frame may be shorter only on a whole-sample boundary |
| Ingress queue | At most 50 frames = 32,000 PCM bytes = one second; reject overflow visibly, never silently drop/reorder audio |
| Captured utterance | At most 30 seconds = 480,000 samples = 960,000 PCM16 bytes; exactly the boundary is valid, one further sample is not |
| Float staging | At most 480,000 finite mono float32 samples = 1,920,000 bytes; conversion is real conversion, not relabeled metadata |
| Concurrency | One current input/recognition operation; no capture queue; STT and chat inference are serialized |
| Capture stop | Acknowledged within 500 ms in controlled fake-device tests and within one second in separately authorized native observation; acknowledgments mean the owned input stream has stopped |
| Recognition | 60-second whole operation deadline including load/IPC/VAD/decode; 1 GiB = 1,073,741,824-byte worker commit ceiling; initial CPU inference uses two threads |
| Worker cleanup | Three seconds to confirm held owned-worker termination after cancel/deadline/disposal; failure prevents further operations on that owner |
| Recognized text | At most 2,000 valid UTF-16 units; malformed Unicode or overflow is a typed rejection, never silent truncation |
| Draft admission | Empty draft at hold start; draft revision, conversation identity and session identity must still match at completion; no merging or overwriting newer text |
| Half duplex | Output-stopped acknowledgment, then a 300 ms tail before capture; gesture must remain held throughout; release/cancel during either wait never opens input |
| VAD | Silero v6.2.0; probability threshold 0.5, minimum speech 250 ms, minimum silence 100 ms, speech padding 150 ms, maximum speech segment 30 s; remaining decoder/VAD parameters must be fixed in a pre-run manifest before the first recognition attempt |

The 50-frame queue is not the complete memory budget. Raw endpoint packet,
conversion/filter state, private IPC and whole-utterance buffers each need explicit
checked size bounds. Endpoint enumeration/startup, IPC control/result frame sizes
and supported native formats must be frozen with the concrete contract before its
first execution; no post-result widening is allowed. A 16 kHz label is not evidence
that 44.1/48 kHz input was correctly converted. Unsupported formats fail clearly.

Mute is a persisted preference and cancels capture/transcription immediately.
Unmute, startup, enumeration, device selection or restored settings never starts
recording. The selected endpoint stays fixed for the operation. Removal/default
changes never silently move recording to another microphone; recovery needs a new
gesture. Deliberate key/pointer release ends capture and requests transcription;
lost pointer capture, focus/deactivation, modifier loss, Escape/Stop, mute, Sleep,
Hide, navigation/mode change and shutdown cancel and discard. Key repeat does not
create another session. Output requested during input cannot overlap capture.

Root selected `CreateProcessW` with suspended startup, a restricted three-pipe
handle list and atomic job-list assignment; resume occurs only after creation with
ownership established. The private job allows one active process with the 1 GiB
limit. No content-bearing command/audio is sent to an unowned child. Exact asset/
executable identity, reparse/path and inherited-environment checks require review.

Before production, root additionally fixed standalone VAD segmentation followed by
one bounded first-to-last detected speech span, retaining pauses inside that span.
Zero segments means NoSpeech. `whisper_vad_detect_speech`'s Boolean is execution
success, not speech presence; it cannot be used as the activity decision. Segment
times require an independently checked conversion to clamped sample indices, with
no second application of padding already applied by VAD. Recognition is English,
CPU-only, two threads, no prior text/context, translation, debug or text printing.
The manifest must state whether any concatenation-overlap option is unused rather
than implying that all upstream VAD options influence this first-to-last strategy.

## Fixed public audio and independent quality oracle

Synthetic audio is the default. Generate these five texts entirely in memory using
the already qualified **Microsoft David**, ID `MSTTS_V110_enUS_DavidM`, culture
`en-US`, rate 0. Request PCM16 mono 16 kHz directly where supported; otherwise record
the actual native source format and use one predeclared verified converter. Never
switch voice/model or trim words after observing recognition. Record the synthesis
binary/voice configuration, generator/converter revision and actual PCM SHA256 and
sample counts **before** invoking recognition. Persist metadata/public transcripts,
not WAV/PCM. A generation failure is a retained failed setup, not a hidden retry.

| ID | Exact reference | Reference words |
|---|---|---:|
| W01 | Please open the chat window. | 5 |
| W02 | My fictional pet is named Maple. | 6 |
| W03 | The red notebook is beside the keyboard. | 7 |
| W04 | I would like a short explanation of gravity. | 8 |
| W05 | Stop speaking and let me type. | 6 |

Normalization is Unicode invariant lowercase and maximal runs of Unicode letters
or numbers as words; punctuation is ignored. No stemming, spelling correction,
number-word equivalence, name substitution or post-result normalization changes.
The fixed English references total **32 words**. A separate Levenshtein dynamic
program computes substitutions + deletions + insertions with unit cost, retaining
each per-utterance reference/hypothesis and distance. Aggregate WER is the sum of
edit distances divided by **32**, not an average of sentence percentages. WER may
exceed 100%; do not clamp it. Empty hypothesis deletes every reference word.

The clean series must have aggregate WER **≤20%**, equivalently at most **six total
edits** (6/32 = 18.75%; 7/32 = 21.875%), and W02 must contain the exact normalized
word `maple`. Two separately reported variants use the same five source utterances:
250 ms / 4,000 zero samples before and after, and that padded version mixed with
deterministic 20 dB noise. The same ≤20% target and `maple` condition are frozen for
each series; failures stay failed rather than changing model, seed or reference.
These are small synthetic integration targets, not human/noisy-room accuracy.

Noise generator: unsigned xorshift32 with initial seed `0x4B1A2026 + fixtureIndex`
(indices 1–5), applying shifts 13, 17, 5 in that order with 32-bit wrap. Convert each
output to `2 * state / 4294967296 - 1`; remove its finite-buffer mean and scale its
RMS to one tenth the original unpadded signal RMS. Decode input samples as
`sample / 32768.0`. Generate noise across the full
padded duration. Mix in float64; if any magnitude exceeds 0.98, attenuate both signal
and noise by the same factor so the maximum is 0.98. Quantize once to PCM16 by
multiplying by 32768 and rounding to nearest integer with ties away from zero.
Independently report measured SNR after
quantization, target 20 dB ±0.1 dB; preserve the seed/sample/hash and any failed target.
The SNR reference includes natural silence inside the original utterance but excludes
the added padding; the noise scale uses that specified original RMS. Report both
the scale-reference SNR and full padded-buffer SNR so silence is not hidden.

No-speech inputs, all exactly five seconds / 80,000 samples / 160,000 bytes:

- N01: all zero PCM16 samples.
- N02: 1,000 Hz sine, sample `roundAwayFromZero(0.1 * 32767 * sin(2π * 1000 * n / 16000))`.
- N03: xorshift32 as above, seed `0x4B1A2026`, samples `roundAwayFromZero(0.02 * 32767 * (2 * state / 4294967296 - 1))`.

All three must return a no-speech/empty accepted result and leave the draft alone;
retain any hallucinated text as a failed public fixture outcome. VAD is not permission
to record and is not assumed perfect. Exact fixture special-casing is not a fix.

## Predeclared execution matrix

| Case | Fixed action | Required evidence / scope |
|---|---|---|
| B01 Unused input | Fresh profile; launch, hover, open chat/settings/options, enumerate/select endpoints, then 60 seconds idle. | Zero capture starts and recognition workers; no output, loopback or background activation. Source-only microphone absence is distinguished from OS-wide monitoring. |
| B02 PCM boundaries | Zero/one sample, 639/640/641 bytes, final even short frame, 49/50/51 queued frames, 479,999/480,000/480,001 samples; malformed rate/channels/bits/counts, overflowing products and nonfinite source floats. | Independent arithmetic; rejection before excess allocation/native launch; overflow causes a visible stopped operation rather than dropped audio; subsequent valid reuse. |
| B03 Packet ownership/conversion | Fake native packets including SILENT/null data, fragmented timing, discontinuity, format change, exception during copy and absent data; known ramp/impulse/tone at each supported source rate/channel format. | Every acquired packet is released once on the correct thread even on fault; silent packets become zeros without dereferencing invalid data; actual conversion/duration/amplitude and finite samples verified. |
| B04 No speech | N01/N02/N03 through pinned VAD/recognizer. | Empty accepted result, no draft/send; retain actual VAD/result classifications and every unexpected transcript. |
| B05 Clean recognition | W01–W05 once each under the pre-run asset/decoder manifest. | All PCM hashes frozen before decode, every hypothesis retained, independent aggregate ≤20% and exact W02 `maple`; successful operation cleanup. |
| B06 Padding/noise | The two fixed five-utterance variants above. | Separate per-series WER/SNR/sample/hash results and identical frozen quality target; no post-result fixture/model changes. |
| B07 Gestures | Controlled mouse/key hold/release, repeat, pointer-capture loss, focus/deactivate/modifier loss, Escape/Stop and cancellation during startup. | One owner; fake stop ≤500 ms; loss cancels/discards and cannot trigger STT later. Native ≤1 s needs a separately deliberate/authorized physical capture run. |
| B08 Draft races | Nonempty draft at hold; type/edit/replace draft during decode; switch conversation/branch/mode, close, then deliver old result/duplicate terminal. | No overwritten text, no wrong-session draft, no autosend, no history persistence before explicit Send; valid completed draft remains editable. |
| B09 Half duplex | Fake output held in stopping state, then acknowledged; hold/release once before acknowledgment, once during 300 ms tail and once after it. | No input/output overlap; early release never opens capture; delayed callbacks/tail cannot create a recording; recognition and model calls do not overlap. |
| B10 Device/mute/lifecycle | No endpoint, denied permission, selected endpoint removed/invalidated, format/default change, mute/unmute/restart, Sleep/Hide/close/shutdown during capture and decode. | Fixed text fallback, saved mute behavior, actual lease/capture cleanup, no silent endpoint change and no automatic restart. Mock matrix is distinct from physical device observations. |
| B11 IPC/worker faults | Unknown/duplicate/miscased fields, version/UUID mismatch, negative/oversize lengths, split prefix/body, EOF/extra bytes, false terminal, malformed UTF-8, >2,000-unit result, missing/corrupt/replaced asset, failed admission, worker crash/hang and parent death. | Strict closed protocol and content-free failures; no content sent before ownership; deadline ≤60 s plus ≤3 s confirmed cleanup; held-child identity, poisoned owner if exit unconfirmed, no by-name kill. |
| B12 Privacy | Fixed canary audio and fake transcript below: normal stop/complete then normal exit; separately hold the ready temporary operation and kill only its owned app process. | Scan every bounded profile file/sidecar in raw and stated text encodings; no audio/transcript cache/log/argv. A real saved SQLite decoy remains intact; no default user profile is used. |
| B13 UI/leases | Real owned WPF controls with controlled input/STT/output, reduced motion, minimum window, keyboard equivalents and expanded device-item peers. | Listening starts only after actual capture-start acknowledgment, Thinking tracks STT, all lease ends match owners; persistent privacy indicator, clear English-only/selected-device/stop/fallback labels. Physical input/screen reader remains separate. |
| B14 Repeat/resources/offline | Ten sequential fixed W02 capture-fixture→draft→clear cycles, then dispose and 60 seconds idle; controlled inference with network unavailable without changing user network settings. | Per-cycle outcomes/cold-load/decode/cleanup, one owner, worker exit, handles/threads/private commit with baseline/method, no residual capture/polling/audio files/network need. Does not imply a sent-message/voice-response loop. |

The initial engineering target is decoding a warm ten-second clean utterance within
ten seconds. Cold loading is measured separately. A per-utterance fresh worker may
have no persisted warm state; do not label its total as warm decoding or omit load
time. A missed performance target remains failed, independent of correctness/WER.
Private bytes, working set and job commit are different measures, not numbers to
add together as if disjoint. The 1 GiB job ceiling is an enforced limit, not proof
of measured peak usage or complete 8 GB system qualification.

## Privacy fixtures and evidence policy

Public fake transcript sentinel:
`PHASE4B_TEMP_INPUT_51B3F209B54D4A15A7DA77B4272F98C1`.

Public PCM canary: 512 samples with sample `((n * 7919 + 12345) mod 65536) - 32768`,
encoded signed16 little-endian, index n=0…511. Retain its generator/hash only.
The profile scanner checks its full 1,024 raw bytes and base64 representation,
and the transcript sentinel in UTF-8/UTF-16LE/UTF-16BE. A scan is supporting evidence
for these known representations, not proof against arbitrary compression/encryption
or OS paging/dumps. Source review separately checks every persistence/log boundary.
No app-profile files are excluded, including innocuous readiness metadata.

Normal and abrupt runs use distinct fresh isolated profiles with a saved decoy.
Content-free readiness markers are atomic and establish the intended active state
before an owned held process is terminated. A fake PCM/capture fixture does not open
a microphone or prove native driver behavior. Actual microphone tests, if separately
authorized, must not save recordings/transcripts as diagnostic artifacts. Public
synthetic evaluation transcripts may be retained only in the explicitly named QA
artifact outside ordinary app-profile persistence; user audio/drafts never use it.

Before each native recognition attempt, save exact runtime/worker/DLL/model/VAD
hashes and source revisions, compiler/ABI/OS/runtime, voice generator identity,
supported/actual sample format, all VAD/decoder settings, CPU thread count, PCM
hashes/counts, budgets, output destination and whether real capture is authorized.
Record all outcomes, including setup failures, first attempts and cancellations.
Do not rerun a failed case silently, change the fixture, download a different model
or loosen a threshold and call the original experiment passing.

## Source-backed risks reviewed before production

The pinned Whisper C header declares 16 kHz input, float sample processing, context
serialization requirements, abort hooks and separate printing/debug switches. It
also exposes VAD segment boundaries and explicit state-reset APIs. These are useful
review surfaces, not guarantees that application cancellation or silence handling
is correct. Keep text/debug printing off, no prior-user prompt/context reuse, and
validate actual output independently. [Pinned Whisper C API](https://raw.githubusercontent.com/ggml-org/whisper.cpp/v1.9.4/include/whisper.h).

The upstream VAD integration extracts detected speech regions and supports Silero
v6.2.0. Padding/minimum-duration/overlap are independent parameters; record all of
them rather than calling defaults a fixed configuration. VAD does not establish
human intent or prevent every nonspeech hallucination. [Pinned VAD documentation](https://raw.githubusercontent.com/ggml-org/whisper.cpp/v1.9.4/README.md).

WASAPI GetBuffer/ReleaseBuffer pairing must occur on the same thread, and a packet
remains valid only until it is released. Frames are channel-wide audio frames, not
bytes; SILENT, discontinuity and timestamp flags need explicit policy. Releasing a
packet late risks data loss, so pipe backpressure cannot hold it hostage.
[Microsoft capture-buffer contract](https://learn.microsoft.com/en-us/windows/win32/api/audioclient/nf-audioclient-iaudiocaptureclient-getbuffer).

Device invalidation can mean removal or reconfiguration. Microsoft's recovery
guidance distinguishes default and explicitly selected endpoints. This application
deliberately uses the stricter selected-endpoint policy and requires a new gesture
after failure; automatic fallback recording would violate the user's choice.
[Microsoft invalid-device guidance](https://learn.microsoft.com/en-us/windows/win32/coreaudio/recovering-from-an-invalid-device-error).

## Findings, evidence and score

No production implementation or executable Phase 4B result is qualified by this
document. The critical review priorities are lost-gesture capture, false/no-speech
transcripts, native packet/worker ownership, stale draft commits, asset/IPC admission
and audio persistence. Concrete findings, fifty individual scores and artifact
attribution will be added after independent inspection and execution.

## TOP 5 RANKED FIXES / IMPROVEMENTS

Pending implementation/evidence. The final review must rank five concrete issues
with priority, why they matter, proposed fixes, regression risks and required tests.

Final verdict: **NOT YET SCORED. Phase 4B and full Phase 4 remain incomplete.**
