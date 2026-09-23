# Local pretrained model integration protocol

Recorded on 2026-09-22 before the first model generation for this increment. Root
executes these checks; independent QA inspects retained output and raw measurements.
No scores or observed outputs are asserted by this protocol.

## Frozen candidate and configuration

- Model: official `Qwen/Qwen3-0.6B-GGUF`, revision
  `23749fefcc72300e3a2ad315e1317431b06b590a`, `Qwen3-0.6B-Q8_0.gguf`.
- Model size: **639,446,688 bytes**; SHA256
  `9465e63a22add5354d9bb4b99e90117043c7124007664907259bd16d043bb031`.
- CPU runtime: llama.cpp stable `v0.4.1`, build `b10964`, source commit
  `b29c606e28a01b1bc8c1351026a0fa6e616bf6c4`.
- Runtime archive: **18,427,629 bytes**; SHA256
  `917f39c076402c421224824607397af20f53625a60defc20e8dd22446bf4c5d7`.
- Runtime launch: CPU only, two generation/batch threads, context 2,048, one slot,
  maximum 256 generated tokens, thinking disabled, no context shifting, offline,
  numeric `127.0.0.1`, Web UI disabled. Record actual request sampling parameters
  and final source/binary hashes with the evidence rather than infer determinism.
- Owned child has a two-GiB job committed-memory ceiling. This is not a two-GiB
  total physical-memory or model-size claim. Loading and generation are active work,
  distinct from the previously failed Phase 2 desktop idle-resource qualification.

Retain every attempted execution, including setup/startup/protocol errors and partial
outputs. Technical fixes receive a new evidence directory and stated rationale; do
not silently discard unsuccessful responses or retry until a preferred answer appears.

## Fixed public fixture prompts

Use the exact user messages below. Use the production system message if the product
has one, and record it verbatim because this is public test text. Cases A and C start
new conversations. Case B sends both turns with the first completed reply retained.

| Case | Exact user message | Narrow content observation |
|---|---|---|
| A | `Reply with exactly this word: LOCAL_READY` | Record whether the trimmed reply is exactly `LOCAL_READY`. |
| B1 | `For this conversation, remember that my fictional pet is named Maple. Reply with only the pet's name.` | Record whether the reply is exactly `Maple`, ignoring surrounding whitespace. |
| B2 | `What is my fictional pet's name? Reply with only the name.` | Record whether the completed prior turn is supplied and the reply is exactly `Maple`, ignoring surrounding whitespace. |
| C | `Give exactly three short tips for organizing a desk.` | Record whether there are three distinct, relevant short tips. Do not infer general factual or reasoning accuracy. |

Content observations are reported separately from transport/desktop correctness.
Any missed content expectation remains a failed observation. Four short replies from
a 0.6B model cannot establish assistant quality, scientific accuracy, hallucination
rate, safety, or autonomous task competence. No tool execution is part of these checks.

## Integration acceptance checks

1. Launch the pinned owned worker lazily, verify socket owner and authenticated
   readiness, reject a wrong internal credential, and record PID and elapsed startup.
   No user API key or remote inference endpoint is involved.
2. Each A/B/C response emits at least one nonempty text delta and exactly one clean
   completion, within the configured output and time limits. Record full public
   fixture output, finish reason, delta count and timing. Length completion is
   reported as truncated, never relabeled a complete answer.
3. For a separate cancellation case use exactly:
   `Write a long list of simple ideas for a weekend at home.`
   Request cancellation immediately after the first nonempty delta. Retain the
   partial text, require cancellation rather than normal success, and require the
   model to serve a subsequent fresh Case A request. This tests cancellation/reuse,
   not answer quality. If generation finishes before cancellation is observed, report
   that race as inconclusive rather than a cancellation pass.
4. Dispose the host after generation; confirm the owned worker exits within the
   declared shutdown bound. Exercise cancellation during startup and repeated dispose
   separately. No unrelated process is terminated. Record whether a worker was
   actually spawned in the startup-cancellation scenario.
5. Measure only as a diagnostic in this increment: monotonic elapsed times, generated
   characters/deltas, native worker peak private bytes and any available working set.
   Describe sampling vs lifetime-peak semantics. No tokens/second claim without
   actual generated token counts. Do not replace a failed run with a favorable retry.

The real-model integration outcome is PASS only for executed, successful integration
checks, with any unexecuted required row marked INCOMPLETE. Content expectations retain
their own outcomes. Full Phase 3 and release qualification remain separate gates.
