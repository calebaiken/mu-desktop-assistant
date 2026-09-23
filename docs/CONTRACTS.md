# Application contracts

## Implemented local chat increment

`IChatModel.StreamAsync` accepts an immutable snapshot of role/content messages and
cancellation, emits bounded text deltas and one Stop/Length completion, or throws a
typed content-free failure. Interrupted output is visibly marked and excluded from
future context. The local adapter bounds requests to 32 messages / 32 KiB UTF-8,
individual SSE events to 64 KiB, output to 8,192 characters / 32 KiB, response idle
to 30 s and turns to 120 s. One request runs per instance. These are actual limits,
distinct from the future general contracts below. The CPU worker context is 2,048
tokens with 256 output tokens; byte bounds do not constitute exact token preflight.
The native chat stores only completed pairs in memory and never writes a transcript.
There is no tool-call event or executable model authority in this increment.

## Remaining proposed contracts

These are design specifications, **not implemented interfaces**. Introduce code as each
phase can supply a real implementation and contract tests. All ports are asynchronous
where I/O occurs, cancellable, injectable, and exchange owned immutable values or
explicitly disposable buffers. No secrets in DTOs, exception text or `ToString`.

| Port | Shape and invariant |
|---|---|
| `IModelProvider` | `Capabilities`; `StreamAsync(ModelRequest, CancellationToken) → IAsyncEnumerable<ModelEvent>`; adapter validates supported settings and protocol |
| `IConversationEngine` | `RunTurnAsync(ConversationIntent, CancellationToken) → IAsyncEnumerable<ConversationEvent>`; owns turn IDs, context budget, proposal round trips and terminal state |
| `ITaskPlanner` | `PlanAsync(TaskIntent, CancellationToken) → TaskPlan`; proposals only, finite DAG, bounded step/recursion counts |
| `IToolExecutor` | `ExecuteAsync(ToolProposal, CancellationToken) → ToolResult`; sole application entry to registry, validator, consent and audited execution |
| `ITool` | descriptor + `Validate(input) → ValidationResult`; internal `ExecuteAsync(AuthorizedContext, validatedInput, CancellationToken) → ToolResult`; raw implementations unavailable to model/UI bridge |
| `IPermissionService` | evaluate normalized intent/resources; issue opaque single-use grant after trusted UI decision; revalidate/consume atomically at execution |
| `IMemoryProvider` | query by explicit scope; create/update/delete with revision and approval provenance; no automatic cross-project retrieval |
| `IConversationRepository` | transactional turn/message append, branch, page, search and delete; in-memory implementation selected for temporary mode |
| `ISpeechToTextProvider` | transcribe bounded PCM stream with format/device metadata; partial/final transcript events and cancellation |
| `ITextToSpeechProvider` | list voices; synthesize text/settings into disposable audio chunks plus timing if supported; cancellation |
| `IAudioCapture` / `IAudioPlayback` | enumerate devices, bounded capture/playback, explicit mute/stop/device-lost events; playback clock drives speaking |
| `IVoiceActivityDetector` | PCM frames to speech-start/end/confidence using versioned threshold config; no implicit recording |
| `IEmbeddingProvider` | batch bounded text → vectors with model ID/dimensions; no embeddings without enabled memory/provider consent |
| `IAvatarRenderer` | load validated pack; apply semantic state/customization; update timeline; stop/dispose; capability flags for layered 2D/future 3D |
| `ISecretStore` | get/set/delete scoped credential reference via Windows credential APIs; caller controls shortest possible plaintext lifetime |
| `IScienceService` | validate/evaluate typed expressions, quantities, uncertainty and plot specs; deterministic results or explicit domain errors |
| `IDesktopContextProvider` | one-shot scope-granted context only; active indicator and revocation; disabled by default |

## Message and failure envelope

Every operation has schema version, operation/turn ID, UTC creation instant, origin
(user/model/tool/import), scope and cancellation/deadline context. IDs use opaque UUIDs,
not user text. Model options: provider ID, approved endpoint ID, model ID, temperature,
input/output budgets and capability-validated reasoning options. Endpoint options refer
to a credential key, never its value. Stream sequence is monotonically increasing;
exactly one terminal completed/cancelled/failed event, including on empty or malformed
streams. Partial text cannot be mistaken for successful completion.

Tool requests are buffered until complete structured arguments arrive; never execute
partial JSON. Proposed limit defaults: 64 KiB tool arguments, JSON depth 16, 1 MiB
individual model event, 8 tool steps/turn, 2 concurrent provider turns globally,
60 s provider response-idle timeout, 5 min turn deadline. These are design defaults to
test, not implemented protection. User-selected text ingestion initially caps at
5 MiB and bounded decoded characters; binary/archives/macros are unsupported initially.

Typed failures: validation, unsupported capability, missing credential, permission
denied, cancelled, timeout, unavailable, malformed response, rate limited, storage
conflict/corrupt, device lost and internal error. Public messages explain recovery;
diagnostics use scrubbed details. Retryability is explicit. `ToolResult` includes
operation ID, status, structured payload matching result schema, user summary,
side-effect outcome (none/committed/unknown), and optional compensation information.
Cancellation after an external commit reports committed/unknown instead of claiming
rollback. Use idempotency keys only where the downstream operation supports them.

## Tool descriptor and permission protocol

Descriptor fields: stable namespaced ID, version, human description, input/result JSON
schemas, risk, required capabilities/scopes, validation policy, timeout, audit policy,
idempotency and compensation declarations. Risk values:

| Risk | Default decision |
|---|---|
| `DISABLED_BY_DEFAULT` | Disabled, including absent/unknown risk values |
| `SAFE` | Allow only registered low-risk capability already enabled by policy |
| `CONFIRM` | Preview exact resource/action and obtain per-operation consent |
| `SENSITIVE` | Disabled until explicitly enabled; then fresh detailed per-operation consent |

Pipeline: registry lookup → size/schema/type checks → normalize → policy/scope check →
trusted preview → grant → recheck resource identity/policy/cancellation → atomic grant
consumption → audit intent → execute → audit outcome. Personality, model text, file
content and web messages cannot create grants. Unknown fields and ambiguous paths are
rejected. A grant binds user session, proposal ID, tool/version, canonical argument
hash, resource identity/version, scope, policy revision, expiry and nonce. Mutating
arguments, scope, policy or target invalidates it. Dismissed/timed-out confirmation is
denial; approvals cannot be replayed across retries or concurrent calls.

Open/inspect handles are validated using final resolved paths and file identities;
reparse points, UNC/device paths, alternate streams and traversal require explicit
policy. Avoid check-then-open races; operate on validated handles where possible and
reconfirm when identity changes. Grant tokens remain process-local opaque capabilities;
untrusted JSON cannot deserialize one. Plugin manifests never register new authority
without application review and user enablement.

## Memory and task records

Memory: ID, category, revision, content, source message/file reference, created/updated
timestamps, approval ID, enabled flag, expiry and allowed project IDs. Scope filters
precede retrieval/ranking. An edit uses optimistic concurrency; stale writes conflict.
Deleting also schedules vector/cache cleanup and discloses backup retention. The UI
offers inspect/edit/delete/disable plus per-project selection.

TaskPlan: ID, user intent reference, ordered/DAG steps, dependency IDs, typed action,
preconditions, proposed risk, status, result reference and cancellation state. The
executor recomputes risk from registered tools rather than trusting the planner.
Reminders use due instant plus timezone and a documented daylight-saving ambiguity
policy; missed reminders surface once on restart. No arbitrary serialized delegates.
