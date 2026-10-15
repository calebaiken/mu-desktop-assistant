# Local neural read-aloud worker

Mu's optional Kokoro backend provides four curated English voices: Heart and Bella (US), Michael (US), and George (UK). It extends the existing `ISpeechOutput` integration; it does not replace conversation generation or speech recognition. Voice quality is subjective. These are general pretrained voices, with no cloning or representation of a user's identity.

Install with `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/setup-neural-speech.ps1` from the repository. Setup explicitly downloads hash-locked Python wheels and two pinned open model assets. It needs Python 3.12, `uv`, and at least 2 GiB free disk space. Assets are stored once under `artifacts/neural-speech-model`, separately from the runtime under `artifacts/neural-speech-runtime`. Source speech is never sent during setup. Runtime operations are offline and require no API key.

The checked model is Kokoro-82M v1.0 in FP32, from the upstream ONNX release `model-files-v1.1`. Release tags can move; the two exact byte counts and SHA256 digests in `model-manifest.json` and `assets.py` are the authoritative pins. The assets total 353,719,767 bytes. Package versions and wheel hashes are in `requirements-neural-speech.lock`; the runtime uses CPU ONNX Runtime without PyTorch or a GPU package.

## Worker protocol

The host starts one hidden, isolated Python process, assigns it to its private Windows job, and sends one UTF-8 JSON line through private standard pipes followed by EOF. Speech text never appears in argv, environment variables, console output, or runtime files.

Requests have exactly `protocol`, `id`, `kind`, `modelDir`, and `request`. Protocol is integer 1, ID is a canonical nonempty UUID, and kind is `voices`, `speak`, or `probe`. Voice enumeration uses a null request. The other kinds use exactly `text`, `voiceId`, and `rate`. Text is at most 2,000 UTF-16 units; malformed Unicode and NUL are rejected. Voice IDs are the closed four-item `kokoro:` catalog. Rate is an integer from -5 through 5, mapped to speed 0.75 through 1.25.

Events have `protocol`, `id`, and `kind`, plus the matching closed payload: `voices`, `waveBase64`, or `failure`. Voice enumeration returns the complete catalog. Probe returns a bounded PCM16 mono 24 kHz WAV through the private response, without playback. Speak sends `speaking` only after complete synthesis and audio validation, immediately before synchronous in-memory WinSound playback; `completed` follows successful playback. Failure codes are `invalid_request`, `missing_assets`, `asset_mismatch`, `memory_limit`, `runtime_missing`, `inference_failed`, `audio_limit`, or `unsupported_platform`. There is no partial speech, transcript, traceback, or native error content in failure events.

Request cap is 16 KiB. Small events are at most 64 KiB; probe framing is at most 6 MiB, and actual WAV data is at most 1,440,044 bytes. Long replies are explicitly shortened to at most 29.5 seconds, leaving terminal-event overhead inside the host's 30-second playback deadline. Their full text remains in chat. Synthesis uses successive chunks of at most 160 phonemes; it never synthesizes a full unbounded long reply before applying the duration cap.

## Admission and ownership

Voice enumeration imports only standard-library modules and checks the five critical distribution versions and both asset hashes. It does not load NumPy, ONNX Runtime, the neural model, or an audio device. All asset files must be ordinary local files in the exact two-file layout; links, junctions, UNC paths, alternate data streams, missing files, and integrity changes are refused. Read-only Windows leases prevent overwrite, rename, or deletion throughout inference.

Before native imports, synthesis requires at least 512 MiB available physical RAM, 1,536 MiB system commit headroom, and 1,024 MiB process commit headroom. Queries fail closed. The production host owns a 1,536 MiB job with one process and kill-on-close behavior, a 90-second generation deadline, a 30-second playback deadline, and bounded cleanup. ORT runs sequentially on CPU with two inference threads, one inter-operation thread, and the CPU memory arena disabled. The worker preloads its packaged phonemizer DLL and does not select a system-wide substitute.

Stop, closing chat, or shutdown terminates only the held worker. The worker performs synchronous in-memory playback, so there is no separate unowned playback thread or persisted audio cache. The upstream phonemizer creates temporary copies of its library DLL; the host must isolate those copies in an owned operation directory and remove it after the worker exits, including cancellation. These are library copies, without speech content. Python audit hooks refuse DNS, socket connections, datagrams, child processes, and shell execution. Native code is trusted code supplied by the pinned runtime; this guard is a defense against accidental fallback, not a general native-code sandbox.

## Licenses and sources

The model is Apache-2.0; `kokoro-onnx` and ONNX Runtime are MIT. The eSpeak loader is MIT, while the bundled eSpeak NG library and phonemizer are GPL-3.0 components. Their notices are preserved under `licenses/`. Do not describe the whole optional dependency set as permissive. These unchanged optional components are provisioned locally into a separate worker runtime. Bundling that runtime for public redistribution requires corresponding-source and license compliance review; the source links below alone are not a completed redistribution offer.

- [Kokoro model card and license metadata](https://huggingface.co/hexgrad/Kokoro-82M)
- [Kokoro voice descriptions and limitations](https://huggingface.co/hexgrad/Kokoro-82M/blob/main/VOICES.md)
- [Kokoro ONNX code](https://github.com/thewh1teagle/kokoro-onnx), inspected source commit `3596b26764286a7de9d90c363e988d50578918e5`; installed version 0.6.1 is pinned by its wheel hash.
- [Model release](https://github.com/thewh1teagle/kokoro-onnx/releases/tag/model-files-v1.1)
- [Phonemizer source](https://github.com/bootphon/phonemizer), installed version 3.4.0.
- [eSpeak loader and build instructions](https://github.com/thewh1teagle/espeakng-loader), installed version 0.2.4.
- [eSpeak NG source and GPL license](https://github.com/espeak-ng/espeak-ng).
- [ONNX Runtime](https://github.com/microsoft/onnxruntime), installed CPU version 1.23.2.

Installation and catalog enumeration have been executed. Real synthesis, audio inspection, cancellation, and UI integration require the separate neural-voice QA receipts; this README does not claim that installation alone qualifies playback.
