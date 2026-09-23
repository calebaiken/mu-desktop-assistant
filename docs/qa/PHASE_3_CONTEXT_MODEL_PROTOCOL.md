# Local context admission and worker recovery protocol

Declared on 2026-09-22 before execution of the expanded real-model smoke checks.
This supplements `PHASE_3_LOCAL_MODEL_PROTOCOL.md`; it does not replace or relabel
earlier attempts. Root runs the existing verification script after a clean build.
Every attempt receives a separate evidence directory, with both protocol hashes,
source/binary hashes and the complete JSON observations retained.

## Frozen runtime and scope

Use the same pinned Qwen3-0.6B Q8_0 model and llama.cpp b10964 CPU runtime as the
original protocol: context 2,048 tokens, maximum response 256 tokens, prompt budget
1,792 tokens, two CPU threads, numeric authenticated loopback, offline and no cloud
fallback. The production adapter applies the worker's actual chat template, tokenizes
the formatted prompt using the worker vocabulary, and removes oldest complete
user/assistant pairs until it fits. It preserves the latest prompt and optional system
message. This integration helper has no private endpoint/token access or token-count
diagnostic hook; it does not claim to measure the exact prompt token counts or the
number of removed pairs. Adapter unit tests establish request ordering, exact budget
decisions and pruning behavior separately.

The entire attempt has a six-minute cancellation deadline. Each production turn also
retains its existing 120-second total and 30-second idle bounds. Record all content,
typed failures, timing, completion counts and known owned worker IDs. Do not retry
individual prompts to obtain preferred wording. A failed lifecycle check makes the
integration outcome FAIL. Manual content checks remain separate and cannot be
promoted to automatic passes by the helper.

## Fixed additional cases

Execute the original A, B1, B2 and C fixtures, then the following cases in order before
the existing cancellation/disposal/startup-cancellation checks:

1. **D — oversized latest prompt.** Submit one user message equal to `"x "` repeated
   exactly **3,000** times (6,000 UTF-16 characters and 6,000 UTF-8 bytes). It is below
   the adapter's per-message and total-byte caps and has valid single-user structure.
   Its repeated public filler is designed to exceed the 1,792-token prompt budget.
   Require `ChatModelException` with `InvalidRequest`, zero text deltas and zero
   completions. Record the complete fixture, the typed outcome and event counts.
   This is live evidence of the expected admission outcome; pre-generation rejection
   is established by the production path and separate request-order unit tests, not
   by a network counter in this helper. Immediately send the unchanged Case A prompt
   and require a nonempty stream with exactly one terminal completion. Record its
   exact-word content observation separately.
2. **E — long paired history.** Build four historical user/assistant pairs. For each
   integer `i` from 1 through 4, the user content is `"Public history fixture i. "`
   followed by `"x "` repeated exactly **300** times, and the assistant content is
   `"Public archived answer i. "` followed by the same filler. Replace `i` with its
   single decimal digit; retain the trailing space in each filler. Append the final
   user message exactly: `Ignore repeated filler. Reply with one short sentence describing a tidy desk.`
   This fixed history contains 2,400 filler repetitions plus labels/template overhead
   and is designed to exceed the prompt budget. Require a nonempty streamed reply,
   exactly one completion, and no protocol/admission failure. Retain all nine input
   messages and the complete output. Together with the unit tests this exercises
   successful pair pruning; do not report an observed exact token count or removed
   pair count. There is no exact wording, sentence-count or factual-quality gate.
3. **F — unexpected owned-worker exit.** After the above successful requests, retain
   a process handle for the precise worker ID exposed by this test's ready
   `LocalModelHost`. Verify it is still the host's live worker, then call `Kill` on
   that held process only, without process-name discovery or process-tree targeting.
   Require that handle to report exit within the remaining deadline and that the
   host no longer reports a live worker. Call `GetModelAsync` on the same host,
   require a distinct live worker ID and a different model adapter, then send the
   unchanged Case A prompt. Require a nonempty stream and exactly one completion;
   record wording separately. The original worker handle stays retained for cleanup
   verification. Host disposal must exit the replacement worker, and all known owned
   worker handles must report exit. This covers an idle worker's unexpected death,
   not parent-process crash cleanup or every possible mid-stream worker failure.

After F, run the original first-delta cancellation, normal reuse, repeated disposal
and pending-startup cancellation checks against the replacement/current owned worker.
Any startup-cancellation race that fails to observe a spawned, pending worker is an
unsatisfied lifecycle check and is retained as FAIL; no favorable retry is substituted.

## Evidence interpretation

`local-model-smoke.json` records integration status separately from `contentFailures`
and `manualContentChecks`. Original Case C (three relevant desk tips) is explicitly
`REQUIRES_MANUAL_REVIEW` until an independent reviewer examines the saved response.
An empty `contentFailures` array therefore never means Case C was automatically
validated. First-delta cancellation retains the partial text, and normal turns retain
their finish reason; a length finish remains a response limit rather than an inferred
complete answer.

Peak private-byte values are samples at observed deltas/completions across sequential
known workers, not a lifetime peak or combined physical-RAM measurement. This small
public fixture suite establishes bounded local integration and recovery behavior only.
It does not establish general answer quality, scientific reliability, complete Phase 3,
parent-crash orphan protection or release qualification.
