# Phase 4: local voice implementation plan

Status update 2026-09-30: optional **Kokoro local neural output** now extends the
existing Read aloud/Stop path, with four English choices and explicit Preview.
The pinned CPU runtime/model are installed; new qualification is recorded in the
[neural voice review](qa/MU_NEURAL_VOICE_REVIEW.md), separately from the original
SAPI evidence. It does not complete microphone input, output-device selection or
the full voice loop. See [neural voice help](MU_NEURAL_VOICES.md).

Historical baseline: **4A output increment independently passed at 9.0/10**, prepared and started 2026-09-22 after
Phase 3 passed independent QA at9.5/10. The original planning pass installed nothing
and activated no audio. The implementation now adds pinned System.Speech10.0.12 and
explicit Read aloud/Stop through installed Windows voices; that first slice downloaded
no voice model. Microphone input remains unqualified WIP. The separate
[Phase 4A review](qa/PHASE_4A_SPEECH_REVIEW.md) approves the output increment only.
Phase 4B push-to-talk is next. No full Phase4 pass follows from this plan.

The original brief requires microphone → VAD → speech recognition → conversation →
speech synthesis → playback → avatar synchronization, plus push-to-talk, optional
voice activation, mute, stop, speed, voice and input/output selection. The later user
decision selects pretrained models running on this PC without an API key. These
requirements supersede earlier optional cloud-provider language. See
[architecture](../ARCHITECTURE.md#voice-and-personality) and
[requirement R09](REQUIREMENTS.md).

## Decision and verified candidates

Start with **installed Windows speech for an explicit Read aloud / Stop slice**,
then add **whisper.cpp base.en plus Silero VAD for push-to-talk**. Keep recognizer,
synthesizer, capture and playback behind separate application ports. Kokoro is now
the selected optional neural output adapter for the later request for more fluent
voices. Piper remains an unselected alternative; its historical source observations
below do not certify an installed backend.

The following are source observations, not measurements on this PC. Sizes shown by
publishers are rounded; the download manifest must record exact bytes, revision and
SHA256 before any installation.

| Candidate | Verified source facts | Planned disposition |
|---|---|---|
| whisper.cpp `v1.9.4` | Current release page lists Sep 11, 2026. CPU inference and Windows MSVC are supported. Upstream estimates tiny at approximately 273 MB memory and base at approximately 388 MB. [Release](https://github.com/ggml-org/whisper.cpp/releases/tag/v1.9.4), [runtime documentation](https://github.com/ggml-org/whisper.cpp/blob/v1.9.4/README.md#memory-usage). | Pin the CPU runtime and all native dependencies. Start with base.en, two inference threads, one active utterance. Benchmark before describing it as real-time. |
| Whisper `ggml-base.en.bin` / `ggml-tiny.en.bin` | Publisher file listing shows approximately 148 MB / 77.7 MB respectively; these correspond to upstream rounded 142 MiB / 75 MiB categories. Quantized alternatives exist, but would be separate accuracy candidates. [Model files](https://huggingface.co/ggerganov/whisper.cpp/tree/main). whisper.cpp and the Whisper repository use MIT licenses; the converted model repository also declares MIT. [Runtime license](https://github.com/ggml-org/whisper.cpp/blob/v1.9.4/LICENSE), [Whisper license](https://github.com/openai/whisper/blob/main/LICENSE). | English-only initial capability, visibly labeled. Tiny.en is a separately evaluated fallback, never a silent model substitution. No training or OpenAI API is involved. |
| Silero VAD through whisper.cpp | Upstream supports `silero-v6.2.0`; its documented download is about 864 KiB. VAD extracts detected speech regions before transcription. [VAD integration](https://github.com/ggml-org/whisper.cpp/blob/v1.9.4/README.md#voice-activity-detection-vad), [Silero MIT license](https://github.com/snakers4/silero-vad/blob/master/LICENSE). | Use the same native worker rather than adding a Python/ONNX stack for VAD. VAD is a signal filter, not permission to start capture. |
| Installed Windows voices through `System.Speech` | Stable `System.Speech` 10.0.12 is Windows-only, MIT, approximately 1.27 MB, with no net10.0 package dependencies. It is a compatibility API that is not accepting new features. [Microsoft package metadata](https://www.nuget.org/packages/System.Speech/10.0.12). Installed voices can be enumerated, selected, rate-adjusted and synthesized to a stream. [Microsoft synthesis API](https://learn.microsoft.com/en-us/dotnet/api/system.speech.synthesis.speechsynthesizer). | First adapter: selected installed Microsoft SAPI voice, plain text, bounded memory output. No voice-model download if a usable voice is already installed. Windows voice components remain OS components; do not redistribute them as app model assets. |
| Windows WinRT synthesis | `Windows.Media.SpeechSynthesis` supports Microsoft-signed installed voices and returns an audio stream. [Microsoft WinRT API](https://learn.microsoft.com/en-us/uwp/api/windows.media.speechsynthesis.speechsynthesizer). | Alternative if SAPI voice/device behavior blocks acceptance. It requires a separately reviewed Windows SDK targeting/interop change; do not add both engines in the first slice. |
| Maintained OHF Piper `v1.8.0` | Current release is Sep 4, 2026; July's 1.5 release restored a C++ CLI and Windows build fixes. C API builds use eSpeak-ng and ONNX Runtime. Engine license is GPL-3.0. [Releases](https://github.com/OHF-Voice/piper1-gpl/releases), [C API](https://github.com/OHF-Voice/piper1-gpl/tree/main/libpiper), [license](https://github.com/OHF-Voice/piper1-gpl/blob/v1.8.0/COPYING). | Optional later adapter after native dependency, redistribution and per-voice license review. Use the maintained project, not an obsolete binary chosen for a different license. No Python environment is needed if its reviewed native API is used. |
| Piper voice example, not an approved asset | `en_US-lessac-medium.onnx` is approximately 63.2 MB plus configuration. Its model card specifies 22,050 Hz and links a separate dataset license. A repository-wide license label does not resolve every voice's terms. [Weight metadata](https://huggingface.co/rhasspy/piper-voices/blob/main/en/en_US/lessac/medium/en_US-lessac-medium.onnx), [individual model card](https://huggingface.co/rhasspy/piper-voices/blob/main/en/en_US/lessac/medium/MODEL_CARD). | Illustrates download scale only. No Piper voice is selected, installed or licensed for redistribution by this plan. Measure complete engine RAM/latency if this alternative is pursued. |

A read-only registry inventory on this PC found SAPI tokens for Microsoft David and
Zira, and OneCore tokens for David, Mark and Zira. That establishes registration only;
engine initialization, audible output, language and device compatibility remain
untested. The attempted fresh CIM CPU query was denied. Use the existing machine
evidence and declared 8 GB RAM target rather than claim a new CPU measurement.

The small Whisper candidates appear feasible for an 8 GB machine based on upstream
memory estimates; this is an engineering inference, not a resource pass. The OS,
desktop and already installed Qwen model also consume memory. Serialize STT and LLM
inference, release STT after each utterance in the initial slice, and measure combined
working set/private bytes without adding the two as if they were disjoint. Do not
choose medium/large Whisper models or GPU runtimes for this initial target.

## Bounded implementation sequence

| Slice | Concrete deliverable | Required gate before extending it |
|---|---|---|
| 4A: explicit speech output | Application speech/playback contracts, serialized voice coordinator, installed-voice adapter, Read aloud and Stop on a completed reply, voice/rate settings, actual playback-driven Speaking lease. Manual playback does not enable automatic spoken responses. | Mock lifecycle/late-event tests, bounded stream tests, one native audible device run, stop/close/sleep behavior, independent incremental QA. Microphone remains unopened. |
| 4B: push-to-talk to draft | Selected input endpoint, visible hold-to-record control with keyboard equivalent, bounded PCM capture, local VAD/STT worker, editable transcript in the existing input box. Release stops capture; Send remains explicit initially. | Fixed audio fixtures, no-speech suppression, byte/duration limits, physical permission/unplug/mute matrix and no user-audio files. |
| 4C: complete voice loop | Opt-in spoken replies, output endpoint selection, half-duplex coordination, capture/playback cancellation, saved versus temporary text behavior, resource qualification. | End-to-end offline voice turn plus device-loss/feedback tests; all Phase 4 required capabilities receive explicit evidence or remain incomplete. |
| 4D: optional activation | Separate off-by-default activation mode with persistent recording indication and independently tested gating. It may be deferred as an explicit remaining Phase 4 requirement; push-to-talk completion does not imply activation completion. | Dedicated false-trigger/no-speech/feedback protocol and independent review before enabling the option. |

Suggested ownership follows the existing project graph: Application owns
`ISpeechToTextProvider`, `ITextToSpeechProvider`, `IAudioCapture`, `IAudioPlayback`,
immutable PCM/device/result values and `VoiceSession`; Infrastructure owns Windows
audio, installed speech and the native STT worker; Desktop composes them and presents
state. Core receives only validated voice preferences if needed. Providers return
audio/text and typed failures, never WPF controls or model-generated commands.

For 4A, review and pin `System.Speech` 10.0.12 in Infrastructure, update the package
allowlist/lock files and notices, and enumerate actual enabled SAPI voices at runtime.
Do not select third-party installed engines automatically or equate OneCore registry
entries with SAPI availability. Use text-only synthesis; do not interpret model text
as SSML or allow remote lexicons. Initially cap one utterance at 2,000 characters,
30 seconds synthesized duration and 4 MiB PCM; use a bounded stream that fails at its
limit, not an unbounded MemoryStream. No arbitrary text is placed on a command line.

Separate synthesis completion from playback completion. Synthesis-to-memory events
do not mean the user is hearing audio. Feed the resulting PCM to the owned playback
adapter and start/stop the avatar from its actual start/drain/cancel notifications.
If a temporary 4A adapter uses default-device SAPI playback directly, label it as a
default-output increment; output-device selection and independently observed buffer
drain remain required before full Phase 4 completion. Microsoft documents that
cancellation still raises `SpeakCompleted`, so completion callbacks need operation
identity and a cancellation flag. [Cancellation API](https://learn.microsoft.com/en-us/dotnet/api/system.speech.synthesis.speechsynthesizer.speakasynccancelall).

For capture/output selection, use a narrowly wrapped, event-driven WASAPI adapter
with endpoint IDs and device notifications. Capture must release each endpoint
packet and stop/dispose the client deterministically; do not copy the polling loop
from a documentation sample into idle operation. A vetted audio library can replace
the wrapper only after its package/version/license is separately reviewed.
[Windows capture contract](https://learn.microsoft.com/en-us/windows/win32/coreaudio/capturing-a-stream),
[device invalidation behavior](https://learn.microsoft.com/en-us/windows/win32/coreaudio/recovering-from-an-invalid-device-error).

Use a small owned native `Companion.SpeechWorker` around pinned whisper.cpp C APIs,
receiving versioned, length-prefixed PCM over inherited local pipes. This avoids
temporary WAV files and a persistent HTTP listener. No network operation is part of
inference. Pin executable/DLL/model hashes, reject reparse/foreign paths, restrict
inherited handles and environment, and create the worker suspended with job assignment
before resume to avoid the existing model-host parent-crash admission gap. Fail closed
if ownership cannot be established. Drain bounded content-free diagnostics; return
transcript only through the explicit result channel. Do not invoke a general shell,
FFmpeg or a CLI mode that writes a user's audio/transcript to disk.

## Capture, cancellation and data policy

- All microphone capture and voice activation are off by default. Opening the app,
  hovering over the compact avatar, reading history, selecting a device or opening
  settings must not create a capture stream. User enablement plus an active explicit
  push-to-talk gesture is required. Never resume a recording automatically at startup.
- Hold begins a new session UUID after prior playback has stopped; release closes
  capture and starts transcription. Pointer-capture loss, keyboard key-up, focus loss,
  app deactivation, mute, Sleep, Hide, New conversation, mode switch and shutdown
  cancel/close capture. A lost release cannot leave the microphone open.
- Use mono 16 kHz PCM at the STT boundary. Twenty-millisecond frames contain 320
  samples. A 30-second cap contains 480,000 samples: 960,000 bytes at PCM16 or
  1,920,000 bytes as float32. Bound the ingress queue to 50 frames (one second), stop
  visibly on overflow, and validate channel/sample format and sample counts before
  allocation. Resampling must be tested; changing the metadata is not resampling.
- VAD processes captured audio only. Begin with an explicitly recorded configuration
  (threshold 0.5, minimum speech 250 ms, speech padding 150 ms); tune only through a
  new versioned fixture protocol. Empty/no-speech clips produce no submitted message.
  The recognizer has a 60-second deadline and an initial 1 GiB worker commit ceiling;
  failure restores typed chat. These are proposed bounds to verify, not measured passes.
- Microphone mute is a persisted app preference and immediately cancels active capture
  and transcription. Unmute alone never starts recording. Stop speaking cancels queued
  synthesis, clears playback buffers and releases Speaking. Stop voice cancels the
  whole current voice operation without deleting an already committed conversation turn.
- Disable capture throughout synthesis/playback. A new hold first stops output, waits
  for the playback-stopped acknowledgment and an initial 300 ms tail, then opens input
  only if the gesture is still held. A release during that wait cancels it. This is
  half-duplex exclusion; it is not acoustic echo cancellation. Measure speaker-tail
  behavior on the physical setup before claiming feedback prevention.
- Keep the selected endpoint fixed for the operation. On unplug, invalidation or OS
  permission denial, stop, dispose and present a recoverable text error. Do not switch
  to another microphone silently. Reconnection requires a new gesture. User-selected
  output disappearance leaves the reply readable and stops Speaking.
- Never write captured or synthesized user audio, cached WAVs, transcript sidecars or
  audio-bearing diagnostic output. Zero owned PCM arrays before returning them to pools;
  stop and dispose native buffers and pipes. This is an application persistence promise,
  not a guarantee about OS paging, hardware buffers or crash dumps. Temporary transcripts
  remain in memory; saved mode persists submitted text under the existing history policy.
- Every async callback carries voice-session and conversation IDs. Cancel invalidates
  them before awaiting cleanup. Late VAD/STT/synthesis/playback callbacks cannot restart
  audio, append into another conversation or change a newer avatar state.

Avatar leases map to actual work: Listening only after capture has started; Thinking
while recognition or model inference is active; Speaking only while playback is active.
No mouth animation starts merely because text arrived or synthesis began. Mute/Stop/
device failure releases the owning lease; Sleep invalidates it. Reduced motion preserves
a visible state label/static expression. A small persistent recording indicator remains
visible during capture even if hover buttons disappear; this is necessary privacy state,
not a return to the large companion card.

## Predeclared independent acceptance fixtures

This is a candidate protocol for the independent QA agent to freeze **before any live
voice run**. The reviewer must save exact runtime/model hashes, fixture generator and
voice ID/version, each PCM hash, sample format/duration, thresholds and proposed budgets
before executing a candidate. No audio was generated by preparing this document.
Keep all attempted public-fixture outcomes, including failed setup and cancellation.

| Fixture | Fixed input/action | Required observation |
|---|---|---|
| V01 | Fresh profile; launch, hover, open chat/settings; wait 60 seconds. | Zero capture starts, zero native voice worker and no audio output without a gesture. |
| V02 | 5 seconds of 16 kHz mono zero samples, then a separate deterministic non-speech signal fixture. | No transcript auto-submission; silence produces no accepted speech. Freeze non-speech generator/seed before running it. |
| V03 | Five public texts: “Please open the chat window.”; “My fictional pet is named Maple.”; “The red notebook is beside the keyboard.”; “I would like a short explanation of gravity.”; “Stop speaking and let me type.” | Feed fixed PCM generated by one installed voice through STT, entirely in memory. Preserve every transcript. Aggregate word error rate ≤20% and exact normalized `Maple` in its fixture; this is synthetic integration evidence, not human speech accuracy. Text is a draft, never an executed command. |
| V04 | V03 PCM with predeclared 250 ms leading/trailing silence and deterministic 20 dB signal-to-noise variant. | Report original/variant WER separately. No post-result text substitutions. Failure does not silently change model or lower the acceptance threshold. |
| V05 | Hold/release, lost pointer capture, focus loss, modifier/key release, and repeated hold attempts. | Exactly one capture owner; stop acknowledgment ≤500 ms in the fake-device oracle and ≤1 second in native observation. Never retain capture after losing the gesture. |
| V06 | Cancel during capture, STT startup, STT decoding, synthesis, queued playback and active playback; inject late callbacks. | No stale result, no later audio, no wrong-conversation append, no remaining worker after the declared three-second termination bound. |
| V07 | Mute, Sleep, Hide, close and saved/temporary switch during each stage. | Privacy/state policy holds; no raw audio or transcript sidecar appears. Temporary profile scan excludes only the predeclared non-content metadata files. |
| V08 | Play the fixed text “This is the local voice playback check. Press Stop to end it.”; stop after audible start; repeat once. | Nonempty valid PCM, audible start, prompt stop, playback-driven avatar state and successful subsequent reuse. Do not score pronunciation automatically from nonempty bytes. |
| V09 | Output playing; press/hold push-to-talk, then release once before and once after the tail expires. | Capture never overlaps output; the early release never opens input; tail timer cannot create an unowned recording. |
| V10 | No input/output, denied microphone permission, input unplug during capture, output unplug during playback, selected voice removed, default device changed. | Deterministic text fallback; no silent endpoint substitution or stuck Listening/Speaking. Mocks do not count as the physical device matrix. |
| V11 | Empty text, 2,000/2,001 characters, 30-second audio boundary, malformed PCM format, oversized declared frame, NaN/Infinity float input and queue overflow. | Bounds enforced before allocations/work; fixed errors contain no spoken content; the next valid operation works. |
| V12 | Offline network state; ten sequential capture→draft→send→reply→playback cycles, followed by stop/unload/60-second idle. | No network dependence, no user audio files, bounded handles/buffers, no orphan worker, no active capture or polling after idle. Record latency and memory for every attempt. |

Compute WER independently as `(substitutions + deletions + insertions) / reference
word count`, using a separate edit-distance oracle. Freeze normalization to lowercase
Unicode letter/number words and ignore punctuation; do not add equivalences after
seeing output. Validate silence/sample arithmetic using an independent fixture parser.
For V03, synthesis-generated PCM is held in memory; retain its hash and transcription,
not an audio file. Human speech/noisy-room evaluation requires a separately declared,
consented physical protocol and remains outstanding until executed.

Predeclare performance targets separately from correctness: a warm ten-second clean
utterance should transcribe within ten seconds on this PC; cancellation controls should
remain responsive; STT private commit should stay under its 1 GiB ceiling. Record cold
load separately. These are initial engineering targets, not upstream performance claims.
An unmet target is a retained failed target, not grounds to substitute a smaller model
and present the same run as passing. Voice-disabled desktop idle must also preserve
the existing resource qualification budget; Phase 2 release limitations remain visible.

The independent reviewer owns the final 10-category × 5-criterion rubric (0/1/2 per
criterion), evidence mapping, score and ranked top five fixes. Before implementation,
freeze criteria covering correctness, boundaries, sample/rate/WER arithmetic, tests,
fault recovery, privacy, resources, maintainability, accessible UI and full R09 scope.
Pass requires strictly greater than 8.5 and no critical defect. Unavailable physical
checks and deferred optional activation remain explicit gaps; a 4A output increment
cannot be presented as complete Phase 4.

## Immediate next task after the Phase 3 gate

Implement 4A with fake synthesis/playback ports first, then the selected installed-voice
adapter and one explicit Read aloud / Stop flow. Have independent QA freeze that
increment's criteria before native execution. Review/pin the small Microsoft package
through the existing dependency process. Do not download Whisper/Piper assets, enable
the microphone or introduce optional activation as a side effect of speech output.
