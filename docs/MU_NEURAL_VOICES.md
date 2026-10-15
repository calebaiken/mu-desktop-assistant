# Mu local neural voices

Mu has an optional Kokoro-82M speech backend alongside its existing Windows voices.
The neural runtime and pretrained voice assets are installed on this PC. Speech
runs locally on the CPU; no account, API key, cloud service or microphone is needed.
This extends the existing assistant rather than creating another application.

## Choose and compare voices

Restart Mu from the **Mu** Desktop shortcut after updating its executable. Open
**Chat → Voice options**, choose a voice, then select **Preview voice**. Preview
reads a fixed introduction without sending a message or creating chat history.
For a completed assistant reply, select **Read aloud**. **Stop voice** cancels
preparation or playback. Replies stay silent until one of these explicit actions.

| Voice | Variety |
|---|---|
| Heart — Neural (US) | American English, feminine |
| Bella — Neural (US) | American English, feminine |
| Michael — Neural (US) | American English, masculine |
| George — Neural (UK) | British English, masculine |

Heart is the first choice when the optional backend is available. Windows voices
remain selectable. Mu honors the selected voice; if neural synthesis fails, it
shows an error and preserves the text instead of substituting a different voice.
Voice and speed apply to the current chat window and are not saved preferences.

Use the speed selector to compare slower or faster readings. For neural voices,
its -5 through +5 range maps to 0.75 through 1.25 times normal speed, in steps of
0.05. Windows voices retain their existing rate behavior. Voice character and
naturalness are personal judgments: listen to Preview to choose your preference.
These are pretrained general voices, without custom voice cloning.

All four voices generated real inspected public samples in Mu. The rebuilt host
passes 398 regression tests, including the Stop repair. The final real normal-
speech lifecycle rerun remains memory-gated, so its completed reuse/disposal and
combined Qwen/voice resource qualification are still incomplete. This distinction
is recorded in the independent review; installed assets alone are not a full pass.

## Practical limits

The first reading loads the local model. Mu displays **Preparing** until audio is
ready, then changes to playback status and its Speaking expression. You can keep
using the interface and stop while it prepares. An operation accepts at most
2,000 UTF-16 characters and plays at most approximately 30 seconds; longer replies
are shortened, with the complete reply retained as text. One operation runs at a
time, without a speech queue or automatic reading of replies.

The CPU worker has a 1,536 MiB committed-memory cap, two inference threads and
a 90-second generation limit followed by a 30-second playback limit. Admission
requires sufficient physical and commit memory. Under memory pressure, generation
can be refused safely; close unused applications or choose an installed Windows
voice explicitly and try again. Mu never closes other applications itself.
Playback uses the Windows default output. Output-device selection, device-loss
qualification and the complete microphone/voice loop remain future work.

Normal speech keeps audio in memory. No WAV or transcript cache is created. The
phonemizer makes temporary copies of its DLL in a private operation directory;
Mu removes those library copies after the held worker exits, including Stop.
These files contain no spoken text or audio. This application storage policy
does not erase Windows paging, crash dumps or other applications' copies.

## Reproducible installation

The base application still runs without this backend. On another development
checkout, install Python 3.12 and `uv`, allow at least 2 GiB free storage, and run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/setup-neural-speech.ps1
```

Setup is an explicit network operation that installs 18 hash-locked CPU packages
and verifies two model assets totaling 353,719,767 bytes. Inference never downloads
packages or models. The runtime and model live separately at
`artifacts/neural-speech-runtime` and `artifacts/neural-speech-model`. The Desktop
shortcut already supplies the repository root. A relocated development executable
needs `--ml-root <absolute-repository-path>` to locate these optional assets.

The model uses Apache-2.0; the optional phonemizer/eSpeak components include GPL-3.0.
See the [dependency inventory](DEPENDENCIES.md) and [worker contract and licenses](../workers/neural_speech/README.md).
This private development installation is not a qualified public redistribution.

## Developer extension points

`ISpeechOutput` remains the application port. `CompositeSpeechOutput` serializes
the existing Windows provider and optional `KokoroNeuralSpeechOutput`, routing by
the selected closed voice ID. Desktop owns the providers; closing a Chat view
cancels its request. Neural generation and synchronous in-memory playback occur
in one held internal worker, under the existing Windows job abstraction.

To add a voice, update both closed catalogs, culture/language selection, independent
catalog tests and public sample fixtures. To replace a model/runtime, update the
asset byte/hash manifest, explicit setup pins, package lock, license notices and
resource policy before executing a new real-model qualification. Do not accept
arbitrary Python, pickle models, custom code, provider strings or remote voice URLs.
Keep request admission before expensive imports; retain bounded UTF-8 framing,
content-free failures, cancellation ownership and independent PCM validation.

Use the [testing receipts](../TESTING.md) and [independent review](qa/MU_NEURAL_VOICE_REVIEW.md)
for the measured scope. Installation, nonzero PCM and playback API success each
prove different things; none alone certifies subjective naturalness or scientific
pronunciation accuracy.
