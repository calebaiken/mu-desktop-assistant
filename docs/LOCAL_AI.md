# Local pretrained AI

The user selected a **pretrained model running entirely on this PC**, without an
API key, after considering training new weights from scratch. This is the active
Phase 3 direction. The unfinished training experiment is paused; it is not part of
the application and no model was trained or evaluated by that experiment.

The first chat increment uses the official **Qwen3-0.6B Q8_0 GGUF** (639,446,688
bytes) and **llama.cpp v0.4.1 / build b10964**, Windows x64 CPU. A small model limits
the initial memory burden on this 8 GB PC. It is a starting backend, with limited
reasoning and factual reliability; performance and useful behavior require actual
tests. Larger models remain a later measured choice, not an automatic download.

## Installation and use

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/setup-local-ai.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/run-local-chat.ps1
```

Setup downloads only the pinned runtime and model into `artifacts/local-ai`, checks
their SHA256 hashes, and preserves at least 1 GiB free disk. It does not install a
service, driver, CUDA toolkit, Python runtime or provider account. The unused Python
environment from the paused experiment has been removed; its unfinished source and
dependency metadata remain archived. Close an already-running companion before launching with the model path;
single-instance activation does not reconfigure the running instance.

The desktop starts with `--local-model-dir` pointing to these files. Open Chat from
the companion menu. The first increment offers plain-text streaming,
Send/Stop and an in-memory conversation. Closing chat discards that conversation
and unloads its worker. Persistent history, branches, search, rich Markdown/code/
LaTeX rendering and all voice/tool/memory features remain later Phase 3+ work.

## Process and privacy boundaries

The app verifies pinned model/runtime hashes before launching the worker. The
worker runs only when chat needs it, with a 2,048-token context, two CPU threads,
one request slot and at most 256 generated tokens per reply. A Windows job object
limits worker committed memory to 2 GiB and terminates assigned processes when its
handle closes. Process creation and job assignment are separate operations; a
parent crash in that brief interval is not yet eliminated by suspended creation.

Inference uses a numeric loopback address, no proxy/redirects and offline mode.
The app verifies that the listening socket belongs to its child process. A random
per-launch secret protects this local connection; it is created internally, never
entered by the user, persisted, or used with a cloud provider. Web UI, exposed slot
monitoring, disk prompt caching and runtime logs are disabled. Output is untrusted
plain text and grants no access to files, shell commands, tools or desktop control.

No cloud fallback exists. Setup needs internet access to download software and
weights; installed inference does not require it. This is a local application
boundary, not an OS network sandbox or protection against same-user malware.
Memory may be paged or captured by the OS; in-memory history is not secure erasure.

## Pinned provenance

- [Official model card](https://huggingface.co/Qwen/Qwen3-0.6B-GGUF), Apache-2.0,
  revision `23749fefcc72300e3a2ad315e1317431b06b590a`.
  Model SHA256 `9465e63a22add5354d9bb4b99e90117043c7124007664907259bd16d043bb031`.
- [llama.cpp stable release](https://github.com/ggml-org/llama.cpp/releases/tag/v0.4.1)
  points to [b10964](https://github.com/ggml-org/llama.cpp/releases/tag/b10964),
  commit `b29c606e28a01b1bc8c1351026a0fa6e616bf6c4`, MIT.
  CPU ZIP SHA256 `917f39c076402c421224824607397af20f53625a60defc20e8dd22446bf4c5d7`.
  Extracted executable/DLL hashes are embedded in Infrastructure's runtime catalog.
  The package includes LLVM OpenMP and its license notice.

This increment passed independent QA at **9.0/10** and does not complete Phase 3.
[Executed checks](qa/PHASE_3_LOCAL_BUILD_EVIDENCE.md) and the
[independent review](qa/PHASE_3_LOCAL_INCREMENT_REVIEW.md) preserve their limitations.
Phase 2's failed CPU confirmation and
unavailable physical/input/accessibility qualification remain release blockers.
