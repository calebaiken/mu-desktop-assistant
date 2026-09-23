# Independent review — local pretrained conversation increment

QA REVIEW

Phase: **3 — local pretrained conversation increment only**

Overall Score: **9.0/10 (90/100)** — strictly greater than 8.5.

Result: **PASS for this bounded development increment. Full Phase 3 remains
INCOMPLETE; resource qualification remains FAIL and release remains blocked.**

Scope recorded before executable review on 2026-09-22. The user's latest choice,
“I think I want a pretrained model,” supersedes the earlier from-scratch training
direction. The current increment is a local, key-free chat path using downloaded
pretrained weights. It does not complete the original Phase 3 conversation/history/
rich-rendering requirements. No cloud inference or automatic training on chats is
authorized by this change.

The independent `local_model_qa` reviewer owns this report and separately identified
adversarial tests. Root owns installation, application launches and execution;
implementation agents own the adapter and native chat UI. This report will distinguish
source inspection, fake-transport checks, actual native window checks and real model
execution. No unexecuted scenario earns passing evidence.

Phase 2 remains a completed development milestone at `f032979` with its independent
8.7/10 review. **Its resource qualification remains FAIL and release qualification
remains blocked.** New model-worker measurements cannot overwrite that result.

## Review criteria frozen before execution

The review uses the existing equal-weight rubric: ten categories, five criteria
each, scored 0 (absent/contradicted), 1 (partial), or 2 (supported). Category scores
sum to 0–10; overall is their exact sum divided by ten. Strictly greater than 8.5 is
necessary, and unresolved critical defects still block advancement. An increment
verdict cannot be represented as a full Phase 3 pass.

| Category | Five criteria to score from actual evidence |
|---|---|
| Code Correctness | Strict build; request validation; complete/partial stream semantics; turn lifecycle; independent adverse-input regression. |
| Architecture | Application port independence; transport/process separation; WPF thread isolation; ownership/disposal boundaries; model/backend replacement boundary. |
| Mathematical Correctness | Exact byte/character boundary arithmetic; context/token accounting limits stated; deterministic limit tests; resource-unit arithmetic; independent QA score arithmetic without an inference-accuracy claim. |
| Testing | Existing regression; provider success/failure cases; reviewer-authored independent cases; native lifecycle checks; real model execution with candidate identity and retained outcomes. |
| Reliability | Bounded start/idle/turn deadlines; cancellation; child failure/cleanup; retry without history corruption; close/late-continuation and resource bounds. |
| Security | Numeric loopback/no redirect/proxy; owned worker authentication/isolation; checked artifact provenance; plaintext/no action authority; no prompt logging, hidden persistence or cloud fallback. |
| Performance | Bounded context/output/history; lazy worker load; measured generation/load resource evidence; responsiveness and cancellation evidence; historical resource failure and measurement limitations retained. |
| Maintainability | Readable modules and explicit contracts; reproducible pinned setup; structured sanitized errors; runnable retained evidence; synchronized scope/documentation. |
| UX/UI | Reachable named controls; clear local/setup state; visible streaming and stop; partial/error/retry semantics; actual input/accessibility limitations distinguished from native method tests. |
| Requirement Compliance | Latest local pretrained choice; functional local streaming slice; independent concurrent QA; original remaining Phase 3 scope retained; honest capability and release limitations. |

## Evidence to collect

- Exact executable/model/source identities, source URLs/licenses and hashes; checked
  setup/install footprint and configured context/thread/output limits.
- Strict Release verification, TRX counts, architecture checks and native JSON
  assertion counts, including retained failed attempts and subsequent changes.
- Independent transport tests exercising fragmented multibyte UTF-8, bounded output,
  malformed/ambiguous terminal state, cancellation and immutable request snapshots.
- Native checks for stop/retry/close ordering and no post-close updates, clearly
  separated from actual human input or screen-reader qualification.
- A fixed, small real-model prompt set with all responses retained. This can establish
  integration behavior, not general factual accuracy, mathematical accuracy or safe
  autonomous task execution. There is no tool executor in this increment.
- Worker start/load/generation/cleanup measurements separate from the failed Phase 2
  desktop-idle qualification. No unchanged favorable rerun replaces a failure.

The criteria above were recorded before executable review. The following scored
review records the actual evidence and its limitations without changing those criteria.

## Findings and evidence

The reviewer found two protocol defects by source inspection: a finish marker plus
`[DONE]` could report success without any text, and duplicate JSON properties could
hide an unsupported finish reason. The adapter now rejects both as protocol failures.
These were source findings, not claimed failed test executions before the fix.

The reviewer also identified worker cleanup that could skip releasing admitted file
leases after a termination/wait failure. The host now releases leases and its process
wrapper in `finally`. The initial 400×440 chat minimum and CenterOwner placement
needed explicit layout evidence; the UI author raised the minimum to 440×560,
constrains the actual chat HWND to its work area, and added minimum-layout/edge-owner
checks. Their executed evidence is recorded below.

The first root-run strict verification reports 224/224 tests, 11/11 architecture
checks and a composed WPF smoke with zero build warnings/errors. The reviewer
independently read `artifacts/test-results/Release/tests.trx`: 224 executed and passed,
zero failures/not-executed, including all twelve reviewer-authored local model tests.
This first verification precedes the final native layout changes. Its archived TRX
is `artifacts/qa/local-increment-verification/initial-224.trx`; the subsequent
`final-224.trx` retains the second complete verification.

Reviewer-authored tests cover one-byte UTF-8/CRLF fragmentation; exact 32,768-byte
request acceptance and 32,769-byte rejection; exact 8,192-character output acceptance
and 8,193-character rejection; empty and ambiguous terminal failures; late output;
caller history mutation after send begins; partial iterator disposal/reuse; malformed
UTF-8; matching-inventory/hash rejection releasing file leases; concurrent repeated
host disposal; and sanitized missing-model failure/cancellation. Their expected byte
boundary uses 8,192 three-byte euro signs plus 4,096 two-byte accented characters:
`8192 × 3 + 4096 × 2 = 32768`. These are transport/lifecycle checks, not model
mathematical reasoning or token-count accuracy tests.

The disposal test was then strengthened to schedule sixteen callers behind an
asynchronous start gate. Its earlier idle-host loop only proved repeated disposal;
it was not described as proof of simultaneous execution. Root's final complete
verification again passes **224/224 tests**, **11/11 architecture checks**, composed
WPF smoke and zero build warnings/errors. The reviewer independently read
`artifacts/qa/local-increment-verification/final-224-concurrent-disposal.trx`:
224 total/executed/passed, zero failed, errors or not-executed, including the refined
disposal case. Its SHA256 is
`64A4E5307E88AAD128773B1C9139CB7E6875184663DB6DD53DBA64AC538A2AA6`.

An additional source finding concerned optional model configuration: constructing a
host for an existing reparse-point path could throw while opening chat, invoking the
global shutdown handler. `OpenChat` now contains IO/access/argument construction
failures and presents the recoverable unavailable-model state. Startup parsing also
rejects forward-slash UNC paths after normalization. Eight separately retained
process controls all return the expected exit code 2 for absent, relative/network,
duplicate and conflicting local-model options:
`artifacts/qa/local-options-f7d63cfb39d645b6b43523c1ae7f92bf/results.json`.
The reviewer independently inspected all eight outcomes. The reparse-point recovery
catch itself is source-reviewed, not claimed to have an executed hostile-junction UI
fixture. These are late setup-validation changes; adapter, host and chat UI source
remain unchanged from the native/live evidence below.

### Native window evidence

Root's native supervisor reports **19/19 process/control checks** under
`artifacts/qa/desktop-ui-20260922-195825-69e2c07d/profile/`.
The reviewer independently read the five reports and checked count, array length,
unique names and error/skip fields: **43 appearance, 24 desktop integration,
29 settings lifecycle, 49 placement initialization, and 54 chat assertions**.
There are no reported failures or skips. Both stage sequences reach chat completion
and settings recovery; their full durations are 26.728 and 26.263 seconds, including
deliberate animation waits. These are not startup percentiles.

The 54 chat checks include pending factory completion after close, ignored-cancellation
late stream events, single-flight ownership, cancelled/failed partial exclusion from
history, successful paired context, literal script/Markdown text, missing completion,
oversized output, eighteen Unicode turns with paired pruning, transcript bounds,
minimum-size controls/readable transcript, and a chat HWND constrained when its owner
is at the work-area edge. Actual keyboard/mouse input, visual focus quality and
screen-reader behavior remain unverified; method calls and HWND geometry do not prove
those scenarios. The two native runs overwrite their per-scenario reports in the same
profile; both completion-stage markers are retained, while detailed final assertions
describe the latter run. Do not claim two separately archived complete assertion sets.

### Real pretrained model evidence

The independent [fixed prompt protocol](PHASE_3_LOCAL_MODEL_PROTOCOL.md) was written
before generation. Its independently checked SHA256 is
`96C29636EA67F985DC38429C9494C5AA6617C368D78242545DAE59C1B4BE1B45`, matching
`artifacts/qa/local-model-20260922-195744-9143334a/plan.json`.
That plan identifies the model digest and five executed binary hashes. The frozen
Desktop DLL is `1F5F2D75EE284CA0562DBBA73392DBAFC4121431BE72DFAA4D0FD00E845AE860`;
the Infrastructure DLL is
`47B7DA5EBE469EF00EAEEEA12D9C0854343B7386136086AA05C3A661F7DD8BE5`.
The source checkpoint field explicitly says `f032979` plus working-tree changes;
it does not misidentify the Phase 2 commit as containing this increment.

The reviewer independently inspected `execution.json` (exit 0, no timeout) and
`local-model-smoke.json` (PASS, empty integration/content failure arrays). This was
the first declared real-model run; no favorable response was selected from retries.

| Check | Recorded outcome |
|---|---|
| A: exact marker | `LOCAL_READY`; two deltas, one Stop completion. |
| B1: fictional pet | `Maple`; two deltas, one Stop completion. |
| B2: retained prior turn | Request includes B1 and its completed response; `Maple`, two deltas, one Stop completion. |
| C: three desk tips | Three numbered, distinct, relevant short tips; 57 deltas and one Stop completion. Independently inspected by reviewer; no broader quality inference. |
| Cancellation | Partial text `Here`, first delta observed, cancellation observed. |
| Reuse after cancellation | Fresh A returns `LOCAL_READY` with one Stop completion. |
| Orderly disposal | Owned worker PID 36896 exits after disposal. |
| Startup cancellation | A new worker actually spawns (PID 26916); cancellation arrives while readiness is pending; cancellation and child exit are recorded. |

The host's successful readiness path requires the listener to belong to its owned
child, valid authenticated readiness, and HTTP 401 for an invalid internal secret.
Thus the successful execution traverses those guards; there is no user API credential.
Source inspection of the pinned server README confirms the used offline, no-Web-UI,
no-slots, internal-authentication, reasoning-off and cache-disable flags are supported.
Configured offline inference is not an OS-level network sandbox; no physical network
disconnection or full traffic-capture experiment is claimed.

The production request parameters in the executed adapter are temperature 0.7,
top-k 20, top-p 0.8, min-p 0, presence penalty 1.5, max output 256, prompt cache off
and thinking off. No fixed sampling seed or deterministic model-quality claim exists.
The worker is CPU-only, two threads, one slot and 2,048-token context. Character/UTF-8
bounds cannot predict exact model token usage; oversized token contexts can still
receive a visible InvalidRequest failure rather than automatic exact truncation.

Readiness is **4.8037317 seconds**, the whole model fixture **14.9589429 seconds**,
and the maximum sampled worker private bytes **360,083,456** (343.40234375 MiB).
These arithmetic conversions were independently checked. The private-byte samples
occur at emitted stream events; they do not establish lifetime peak, mapped GGUF
physical memory, system-wide RAM, load peak, CPU budget, token throughput or percentiles.
The model file itself is **639,446,688 bytes** (609.8238830566406 MiB). Neither number
is a whole-product memory measurement. Cancellation evidence proves the client stops
and can reuse the model, not an instantaneous zero-CPU worker state after Stop.

The live/native build predates only the final optional setup-validation guards and
reviewer-test refinement. Final strict regression and eight targeted process controls
cover those changes. No unchanged model benchmark was rerun or selected to replace
earlier evidence, and the report does not claim live inference ran against the final
Desktop DLL (`28761CDFB4E06720A58B323956FDF1FCC36DF49C6F525E8E1F26E85943276F71`).

## Category Scores

Each bold value is one of the five predeclared 0/1/2 criteria for that category.
Exactly fifty values are scored; partial coverage receives one point.

| Category | Criterion-level scores and basis | Score |
|---|---|---:|
| Code Correctness | **2** strict final build; **2** independent request limits/validation; **2** terminal, partial, duplicate and malformed stream checks; **2** controlled turn/close lifecycle; **2** twelve reviewer-authored adversarial tests pass. | 10 |
| Architecture | **2** Application model port has no transport/UI dependency; **2** HTTP/process/file admission are separated; **2** worker and asynchronous I/O keep model work outside WPF; **2** explicit host/model/window ownership and disposal; **1** replacement boundary exists but only one pinned backend/candidate is integrated. | 9 |
| Mathematical Correctness | **2** independent exact UTF-8/character arithmetic; **1** byte bounds and context failures are honest but token preflight is absent; **2** deterministic limit boundary tests; **2** diagnostic units and exclusions independently checked; **2** exact score arithmetic with no model reasoning-accuracy credit. | 9 |
| Testing | **2** 224-case existing/new regression; **2** scripted provider success/failure coverage; **2** twelve independent adversarial cases; **2** 54 native lifecycle/history/layout assertions plus retained desktop regression; **2** identified real model run with all fixed outputs and lifecycle outcomes retained. | 10 |
| Reliability | **1** configured startup/idle/turn deadlines and idle-stall tests exist, but heartbeat-versus-total-deadline coverage is missing; **2** client/live startup cancellation; **1** cleanup/job checks pass while unexpected worker crash and create-to-job crash timing remain incomplete; **2** cancelled/failed turns preserve context and model reuse; **2** close suppresses late updates and bounds retained data. | 8 |
| Security | **2** numeric loopback, no proxy/redirect, socket ownership; **1** authenticated owned worker is tested but is not an OS sandbox and retains the short create-to-job gap; **2** pinned provenance/hash inventory and file admission; **2** output is literal text without tool authority; **2** content-free errors/logs, temporary history, no automatic training or cloud fallback. | 9 |
| Performance | **2** bounded requests/output/history; **2** lazy model load and close disposal; **1** one diagnostic load/generation/private-byte sample sequence, not qualification; **1** controlled cancellation/asynchronous UI evidence, no actual input-latency or frame-time distribution; **2** historical resource failure and measurement limits explicitly retained. | 8 |
| Maintainability | **1** modules are readable but conversation/history policy remains in window code pending extraction; **2** pinned reproducible setup and guarded hashes; **2** structured sanitized failure types; **2** scripts and uniquely retained test/live evidence; **2** root docs/contracts distinguish implemented and remaining scope. | 9 |
| UX/UI | **2** named controls and minimum geometry are checked; **1** local/setup states are visible but setup still requires development scripts; **2** streaming/Stop behavior is evidenced; **2** interrupted replies and explicit edit/retry preserve context integrity; **1** native method/accessibility names exist but actual input and screen-reader qualification remain absent. | 8 |
| Requirement Compliance | **2** latest local pretrained choice honored; **2** functional local streaming slice; **2** concurrent independent review plus source findings/regressions; **2** original remaining Phase 3 work retained; **2** narrow model capability and release limitations are explicit. | 10 |

Exact arithmetic:
`(10 + 9 + 9 + 10 + 8 + 9 + 8 + 9 + 8 + 10) / 10 = 90 / 10 = 9.0`.
This passes the strict greater-than-8.5 increment gate. It is not a numerical score
for the unfinished entire Phase 3 or a replacement for the failed resource experiment.

## TOP 5 RANKED FIXES / IMPROVEMENTS

1. **Problem:** The 24-KiB UI history budget is not a token budget for the 2,048-token
   worker. **Why it matters:** A seemingly short Unicode message or growing history
   can be rejected before the UI trims enough context. **Proposed fix:** Add bounded,
   model-specific tokenization/preflight and reserve template/output tokens, with
   independent boundary tests and a visible context policy.
2. **Problem:** Full Phase 3 conversation behavior remains incomplete and current
   turn/history policy lives in the window. **Why it matters:** Persistent/searchable
   history, branches, regeneration/edit-resubmit and rich safe rendering cannot be
   claimed from this temporary plain-text slice. **Proposed fix:** Extract the
   conversation service, add transactional storage/private-session tests and safely
   bounded Markdown/code/LaTeX features in reviewed increments before advancing phase.
3. **Problem:** Worker fault timing and some deadline evidence remain incomplete.
   **Why it matters:** A parent crash between Start and job assignment can leave the
   worker outside the job; orderly cancellation does not prove unexpected crash recovery.
   **Proposed fix:** Use suspended creation/assignment before resume, exercise worker
   death and readiness faults, and add a heartbeat stream that proves the absolute
   turn deadline independently of idle timeout. Preserve content-free errors.
4. **Problem:** Actual keyboard/mouse, screen-reader, physical display and sign-in
   qualification remain absent. **Why it matters:** Programmatic controls and HWND
   containment do not prove focus, input delivery or accessible operation on real
   displays. **Proposed fix:** Complete the retained Phase 11/12 physical/input matrix,
   including new chat Send/Stop/retry/close and minimum layout, before release.
5. **Problem:** Phase 2 resource qualification still fails, and model resource results
   are narrow diagnostics. **Why it matters:** Private-byte samples omit mapped model
   physical memory and do not qualify combined desktop/worker idle behavior or CPU.
   **Proposed fix:** Execute the mandatory separately predeclared profiling/repeat
   protocol, distinguish active generation from loaded/closed idle, measure whole-product
   memory and responsiveness, and retain all results without overhead subtraction.

## Regression Risks

Do not admit arbitrary endpoints, redirects, inherited runtime configuration, unpinned
executables or executable model output. Preserve literal rendering, strict bounded
streaming and unique terminal interpretation. A cancelled/failed turn must never enter
future context or update a closed window. Maintain exact pair pruning and explicit
Length completion. Keep model load/cancel/dispose independent of desktop recovery.
Do not label UTF-8 bytes as tokens or worker private bytes as total model RAM.

## Tests Required

The current increment's strict/native/live checks are complete within their stated
scope. Before full Phase 3 completion, test exact tokenizer admission, remaining
conversation/storage/rendering behavior, temporary-session persistence sentinels,
unexpected worker failure and the absolute-deadline case. Before release, complete
the existing resource, physical/input/accessibility/sign-in gates and distribution
license/security review. Add an actual reparse-point setup-recovery UI fixture when
that configuration path is expanded; the current catch is source-reviewed only.

## Final Verdict

**PASS — local pretrained conversation development increment, 9.0/10.** The inspected
evidence supports checkpointing this bounded feature and continuing work within
Phase 3. No unresolved critical correctness/security defect was found in this review;
the verdict does not assert exhaustive correctness, broad assistant intelligence or
scientific accuracy. Root may finalize the source checkpoint and synchronize this
verdict into status documents without another unchanged test run.

**Full Phase 3 remains INCOMPLETE. Resource qualification remains FAIL; release remains
blocked.** Phase 2's historical 8.0/8.6 failed exits, 8.7 development pass and failed
final resource pair retain their original meanings. This review neither waives their
remaining obligations nor authorizes advancement to Phase 4.
