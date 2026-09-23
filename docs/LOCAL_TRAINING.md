# Local model training decision

**SUPERSEDED:** The user subsequently selected a pretrained local model. See
[the active local AI plan](LOCAL_AI.md). The unfinished code is preserved under
ignored `artifacts/paused-from-scratch-20260922`; no training or learning result is
claimed. The following records the earlier decision, not current implementation.

Recorded 2026-09-22 before implementation and training. The user explicitly chose
**training new model weights from scratch**, with no API key. This replaces the
earlier provider-first plan. No cloud inference, pretrained model/tokenizer, teacher
model or automatic ingestion of personal files is part of this path.

Phase 3 now has two parts. **3A** establishes bounded local training, evaluation,
safe checkpoint/resume and inference from newly initialized weights. **3B** connects
an owned local inference process to the original conversation, streaming, history,
private-session and chat-interface requirements. Passing 3A cannot complete 3B or
establish useful assistant behavior. Voice, tools, memory and release work remain.

## Architecture

C#/.NET and WPF continue to own the desktop application. A separate Python process
uses PyTorch's automatic differentiation and tensor operations, rather than adding
a handwritten numerical runtime to the desktop. Training and inference are CPU-only
initially. Python dependencies live in an ignored project environment; the desktop
does not import them or download models at startup. Later desktop IPC must be bounded,
versioned and cancellable before exposing generation in the UI.

The first model is a small, randomly initialized pre-normalized causal Transformer:
UTF-8 bytes 0–255, EOS 256, BOS 257; learned position embeddings; masked multi-head
attention; GELU feed-forward layers; no dropout or pretrained tokenizer. Configuration
and parameter-count bounds apply before allocation. This is a deliberately small
experimental architecture, not a claim that a general assistant can be trained on
this laptop. Architecture, data and compute can change through reviewed increments.

The corpus manifest identifies complete documents, split, source, permission/license
and content hash. Splits precede token windows; windows never bridge documents.
Exact duplicate documents are rejected. Near-duplicate semantic detection is not
implemented. Validation metrics are token-weighted; test evaluation is explicit.
Training never discovers or scans personal documents or conversation history.

Checkpoints contain tensors and versioned metadata in one non-executable safetensors
file. Model, optimizer, step and RNG state must belong to the same atomic generation.
No pickle loading, executable model files or cloud fallback is permitted.

## Initial machine budget

Observed: Ryzen 7 5700U (8 cores / 16 logical processors), 7,928,467,456 bytes RAM,
NVIDIA MX450 with 2,048 MiB VRAM, Python 3.14.6, and approximately 2 GB free disk.
CPU execution avoids assuming this GPU has sufficient VRAM or framework support.
No driver or CUDA toolkit installation is planned.

Keep at least 1 GiB free disk. Account for compressed dependencies, expansion and
temporary overlap before installation; refuse writes that cannot retain the reserve.
Do not delete unrelated user files. Initial training artifacts are bounded to 512 MiB
and the remaining free-space allowance. Start with bounded runs and record measured
memory/time; the first supervised worker limit is 2 GiB, with a fixed deadline.

The [independent protocol](qa/PHASE_3A_PROTOCOL.md) separates numerical correctness,
optimization sanity, held-out learning and assistant quality. Experiment criteria
must be recorded before training; failures and all declared runs are retained.
Synthetic fixtures exercise code only. A public corpus needs verified permission
and attribution before ingestion. A useful assistant requires a much larger data,
training and unseen-instruction evaluation effort than this foundation.

The earlier Phase 2 resource failure and unavailable physical display/input tests
remain mandatory release qualifications. Local training does not waive those gates.
