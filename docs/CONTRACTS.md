# Application contracts

## Research Projects UI v3

The picker is an opt-in client of IResearchProjectStore. Selection updates only
transient review; explicit Open requires the same stable ID, unchanged immutable
metadata and same selected row after asynchronous resolution. Refresh clears
acceptance/selection before listing. Closing cancels UI lifetime and ignores late
results. No dataset/planner/worker/catalog/permission contract changes; workspace
still owns switching and job admission. See [design/evidence](UI_DESIGN_V3.md).

## Implemented Chat-to-Research transfer increment

`ResearchRequestHandoff` is bounded process-local request data, not action authority.
`ResearchLabWorkspace.PrepareHandoff(projectId, request, temporary)` captures its
current idle lease/admission revision. `AcceptHandoff(ticket, deliver, token)` consumes
once before validating cancellation and destination under the existing lock; its
trusted synchronous UI-only callback cannot reenter job/project admission. Disposal
clears request references. Later work or project retirement requires a new review.
Desktop additionally binds source and destination window lifecycles. The existing
planner must resolve file actions to the explicitly captured dataset; editable Lab
inputs cancel/invalidate proposal revisions before reviewed Run. See the full
[handoff contract](CHAT_PROJECT_HANDOFF.md). No persistence format changes.

## Implemented research-project increment

`IResearchProjectStore` explicitly creates, lists and resolves a project by ID.
`ResearchProject` is immutable metadata: generated ID, literal name, derived directory,
UTC creation time and a legacy indicator. A record supplied by a caller is never
accepted as a directory override. The reserved empty GUID identifies existing
default-project work; no automatic migration or project selection is performed.

`ResearchLabWorkspace.OpenAsync(id)` returns `ResearchLabSelection(Project, Service)`.
The service is a borrowed, project-bound façade: views may execute/cancel/observe,
but disposal is explicitly rejected because App owns the workspace. Admission is
counted synchronously before calling the inner Lab; queued/running jobs and another
transition block switching. Old façades cannot admit work after retirement. The
`Current` selection remains borrowable while work runs, so closing/reopening a view
never hides its owned jobs/cancel controls. It is unavailable during a project
transition or after shutdown/poison, and grants no new service admission. The
old inner service must finish disposal before factory admission for a new project;
six-second timeout or failure poisons the workspace and retains cleanup ownership.
`DisposeAsync` is shared/idempotent and awaits transitions and owned cleanup; App
applies its bounded shutdown deadline and reports uncertainty. No success claim is
made for stalled native I/O/process cleanup. See [storage/limits/recovery](RESEARCH_PROJECTS.md).

## Implemented local chat increment

`IChatModel.StreamAsync` accepts an immutable snapshot of role/content messages and
cancellation, emits bounded text deltas and one Stop/Length completion, or throws a
typed content-free failure. Interrupted output is visibly marked and excluded from
future context. The local adapter bounds requests to 32 messages / 32 KiB UTF-8,
individual SSE events to 64 KiB, output to 8,192 characters / 32 KiB, response idle
to 30 s and turns to 120 s. One request runs per instance. These are actual limits,
distinct from the future general contracts below. The CPU worker context is 2,048
tokens with 256 output tokens. Exact preflight uses the authenticated worker's
apply-template and tokenize endpoints with the same nonthinking template settings
as inference. At most 16 candidates are admitted under the same turn deadline;
oldest complete pairs are removed until the prompt fits 1,792 tokens. The latest
user message is never trimmed; an oversized sole prompt is InvalidRequest.
There is no tool-call event or executable model authority in this increment.

`IConversationStore` supplies list/search, create/load, append/complete, branch
selection, rename and delete. Immutable snapshots contain a turn tree and selected
leaf. Saved prompts commit as InProgress before inference; only that pending turn
can become Completed, Interrupted or Failed. Terminal turns are immutable. Loading
an idle conversation recovers pending turns as Interrupted; callers must not Load
during active generation. Context includes completed ancestors only, bounded to
24 messages / 24 KiB before exact token admission. Editing/regenerating appends a
new sibling and preserves the original. Limits: 100 conversations, 256 turns each,
4,000,000 UTF-16 characters overall, 2,000-character prompts and 8,192-character replies.

`InMemoryConversationStore` has no filesystem dependency. It is selected by default
for Temporary chat; switching to Saved starts separately. `SqliteConversationStore`
uses a fixed local profile path, schema/application ID validation, transactions,
foreign keys, bounded reads and a 64 MiB database ceiling. Future/corrupt originals
are preserved and visible failures are content-free. The UI retains unsaved output
and blocks subsequent saved sends until Retry saving or explicit temporary recovery.

## Implemented speech output contract

Phase 4A also implements `ISpeechOutput`: cancellable installed-voice enumeration and
`SpeakAsync(SpeechRequest, IProgress<SpeechOutputState>, CancellationToken)`, plus
async disposal. Request text is literal, at most2,000 UTF-16 units with valid Unicode;
voice identity is explicit and rate is -5..+5. Only one operation is active, with no
queue, a30-second deadline and3-second owned cleanup bound. Speaking/Stopped are
engine lifecycle observations; memory-only diagnostics emit no Speaking state.
Typed fixed failures exclude input text and native exception payloads. Failed cleanup
poisons the provider instance. Separate synthesis/capture/playback/recognition ports
below remain the broader voice-loop design; this output slice does not implement them.

## Implemented text inspection candidate (2026-10-19)

`Application.Tools` now implements the narrow `IToolExecutor`, `ITool` and
`IPermissionService` slice in the design below. `PrepareAsync` returns a sealed
metadata-only preview; the trusted UI alone receives the approval port. The executor
retains the prepared resource, binds its exact immutable proposal/arguments, descriptor
and identity to an opaque one-use grant, and emits a typed completed/denied/cancelled/
failed result. The one registered tool denies by default and requires fresh consent.
Neither untrusted JSON nor a model can deserialize a preview/grant/context.

The first proposal is one typed selected text-file read, not a general DAG planner or
a model tool-call protocol. No raw implementation executes without an internally
constructed `AuthorizedContext`. Cancellation, expiry, stale identity and all failed
attempts consume/invalidate authority. The Windows adapter holds no-follow,
write/delete-denying ancestor/file leases through its bounded read. See
[actual limits and outstanding qualification](TRUSTED_TEXT_INSPECTION.md); the broader
future descriptor JSON schemas and multi-step task contracts below remain design.

## Remaining proposed contracts

Except for the narrow Tools candidate above, these are design specifications,
**not implemented interfaces**. Introduce code as each
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
