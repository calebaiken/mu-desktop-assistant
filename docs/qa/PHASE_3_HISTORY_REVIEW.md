# Independent review — saved and temporary conversation history

QA REVIEW

Phase: **3 — history, branches, search, privacy, context, rich rendering and reply preview**

Overall Score: **9.5/10 (95/100 criterion points).**

Result: **PASS — Phase 3 development exit. Release is not approved.**

## Scope and independence

The criteria below were frozen on 2026-09-22 before this reviewer examined executable
evidence for the new history implementation. The reviewer owns this report and
separately named adversarial test files, and does not author the new storage,
history UI, context-preflight implementation, production fixes or evidence runner.
The reviewer previously authored the original local streaming adapter; that adapter's
independent assessment remains the separate `PHASE_3_LOCAL_INCREMENT_REVIEW.md`
report. This review will not present an author's own code as newly independent QA.

Root executes build, database, native-window and real-worker checks. The reviewer
inspected retained artifacts and distinguishes source inspection, scripted model
fixtures, real SQLite operations, native method invocations and actual user input.
No unexecuted scenario receives passing evidence.

The preceding local pretrained chat increment passed separate independent review at
9.0/10. History alone would not complete Phase 3. The additional conversation
features, native Markdown/code/LaTeX, short reply preview, rendering security and
applicable worker-failure checks now have evidence below. Together with that prior
review, this review supports the Phase 3 development exit. Unseen assistant evaluation is required before broad capability
claims; the original brief does not make such claims a prerequisite for the bounded
conversation functionality itself. Phase 2 development remains complete at 8.7/10; its failed
resource qualification and deferred physical/input gates remain mandatory in
Phases 11/12. New measurements cannot replace those historical results.

## Frozen rubric

Each listed criterion receives 0 (missing or contradicted), 1 (partial), or 2
(supported by cited evidence). There are ten equally weighted categories and five
criteria each. Category totals are 0–10; overall is the exact sum divided by ten.
An unrounded score strictly greater than 8.5 is necessary, and a critical unresolved
correctness/security defect independently blocks a pass. Criterion definitions and
weights will not change after outcomes are known. Missing full-phase requirements
remain explicit even if the bounded increment earns a passing numerical score.

| Category | Frozen five criteria |
|---|---|
| Code Correctness | C1 strict build and existing regression; C2 successful/interrupted turn state and restart ordering; C3 immutable branch ancestry for edit/regenerate; C4 title/search/page/delete behavior and bounds; C5 saved/temporary selection and transitions preserve the intended conversation only. |
| Architecture | A1 UI-independent immutable domain and repository contracts; A2 SQLite adapter isolated from Application/Core; A3 blocking database work runs off the dispatcher with serialized writes; A4 explicit in-memory temporary repository with no implicit persistence conversion; A5 lifetime/context/model boundaries retain local-only inference and permit replacement. |
| Mathematical Correctness | M1 exact 50-criterion arithmetic and stated scope; M2 independently checked character/UTF-8/count boundaries; M3 independent ordering/ancestry oracles rather than implementation-mirrored expectations; M4 model-template token preflight plus output reserve agrees with actual worker accounting; M5 no inference mathematical-accuracy claim without deterministic verification, with future numerical obligations retained. |
| Testing | T1 full regression and named independent tests execute without hidden skips; T2 real SQLite reopen/restart and transaction tests; T3 unique temporary sentinel absent from app-controlled persistent content after normal close and abrupt exit; T4 native history/mode/search/branch/lifecycle behavior; T5 corruption/newer-schema/busy/full-write/cancellation fault cases with preservation assertions. |
| Reliability | R1 atomic updates and transaction rollback preserve prior records; R2 unfinished turns recover to an explicit interrupted state after restart; R3 corrupt/unsupported data is preserved and recovery/backup behavior is explicit; R4 Stop/close/mode-switch/save races do not apply late updates to another conversation; R5 bounded shutdown plus worker/cancellation failures leave history recoverable. |
| Security | S1 temporary content is excluded from database/WAL/backups/logs/search and automatic training; S2 parameterized queries and literal bounded search resist query/FTS injection; S3 local data paths, schema and persisted role/status/ancestry are validated; S4 no automatic HTML/script/link/tool execution or remote inference; S5 deletion/search scope and content-free errors/logging are accurately specified and tested. |
| Performance | P1 history/context/search/page results and allocations are bounded; P2 database/file/WAL/backup growth has enforced or honestly limited controls; P3 dispatcher remains responsive during database/model operations; P4 token preflight and active turns have bounded work/deadlines/cancellation; P5 worker measurements and unresolved host idle-resource qualification remain separate and honestly reported. |
| Maintainability | N1 readable responsibilities and validated contracts; N2 explicit schema/version/migration/backup design; N3 meaningful injectable fault seams without production bypasses; N4 reviewed pinned dependency provenance and reproducible commands/artifacts; N5 synchronized current behavior, limitations and next steps across required docs. |
| UX/UI | U1 saved/temporary mode and persistence implications are visible before sending; U2 usable history selection/title/search/reopen; U3 understandable edit/regenerate/branch navigation preserves originals; U4 Stop/retry/copy/error/keyboard controls and interrupted states work; U5 layout, accessibility and rendering scope are verified to the stated level without substituting native method tests for physical input. |
| Requirement Compliance | Q1 honors the user's local pretrained/no-API-key choice; Q2 functional history/title/search/new/private behavior; Q3 branch/edit/regenerate plus partial/interrupted semantics; Q4 independent QA, retained failures and strict phase gates; Q5 original full Phase 3 rendering/worker/evaluation gaps and release gates remain visible with no premature advancement. |

## Evidence plan

- Freeze a source/build identity and retain exact commands, exit codes, TRX counters,
  native assertion reports, dependency advisory/license output and any failed attempts.
- Exercise fresh and reopened real SQLite files, special-character search, cross-branch
  isolation, cancellation, conflict and transaction rollback. Preserve unsupported or
  corrupt bytes. Distinguish simulated exceptions from genuine SQLite/OS failures.
- Use unique synthetic temporary sentinels; inspect every app-controlled profile file
  including database sidecars, backups and logs after orderly and abrupt process exit.
  Do not claim forensic erasure, memory/dump/pagefile exclusion or other OS guarantees.
- Verify saved UI changes and branch operations through owned WPF windows, including
  delayed persistence completions and close/mode changes. Record physical-input and
  screen-reader limits separately.
- Validate worker-template context accounting, output reserve and oversized-input
  rejection before chat generation, with unselected retained real-worker outcomes.
- Determine the remaining full-phase rendering/math-formatting, worker crash/fault and
  unseen assistant-evaluation work from actual evidence, without manufacturing a pass.

## Findings, criterion scores and evidence

The scores below use the frozen criteria and retained execution described here.
Historical failures and pre-final binaries remain separately attributed.

### Source findings before execution

1. The first history contract could append only a terminal turn. A crash during
   generation would lose the sent prompt and leave no unfinished record to recover.
   Root and the storage author added an `InProgress` record before generation and a
   separate terminal completion operation. Loading pending saved turns marks them
   interrupted. Response deltas are not checkpointed per token; an abrupt close can
   still lose the uncommitted partial reply. That limitation must remain disclosed.
2. The initial database constructor checked raw backslash UNC syntax before path
   normalization. Forward-slash UNC syntax needed rejection after normalization too.
   The storage author corrected this and the reviewer added an explicit fixture.
3. The initial path inspection omitted SQLite sidecar reparse points. The storage
   author extended checks to the database, `-wal`, `-journal` and `-shm`. This is source
   evidence until an actual reparse fixture is executed, not a claim of race-free
   protection against malicious software running as the same Windows user.
4. SQLite's built-in `lower()` did not match the temporary repository's Unicode
   `OrdinalIgnoreCase` search. The author replaced it with a deterministic literal
   comparison function; reviewer fixtures exercise accented Latin, Greek and query
   punctuation without treating `%`, `_` or quotes as executable query syntax.
5. A failed terminal save disabled history cleanup, new/mode controls and Send while
   offering only Retry saving. Persistent full/unavailable storage could trap the
   conversation until close. The reviewer requested an explicit recovery path that
   preserves pending data until the user chooses how to handle it. Root added an
   explicit Continue temporarily action that copies the displayed branch and unsaved
   reply into a new in-memory store; it performs no durable conversion or deletion.
   A further reviewer finding required a persistent privacy label explaining that
   earlier saved messages remain, because a transient status was replaced on Send.
   That label is now present and passes the native history recovery checks.
6. Parsing one joined transcript would let a message's unclosed Markdown fence or
   emphasis change later messages and their role labels. The reviewer requested
   separate parsing per message with trusted labels outside model-controlled markup.
   The renderer author implemented that boundary; the frozen 46-check renderer run
   below includes the cross-message fence and trusted-header cases.
7. A later source review found that opening every existing database read-only for
   validation prevents SQLite from rolling back a hot journal left by an interrupted
   write. This is a concrete recovery concern, not a claim that a crash fixture has
   already failed. SQLite documents that a hot journal must be rolled back before
   reading, and reports `SQLITE_READONLY_ROLLBACK` when read-only access prevents it.
   A bounded genuine-journal regression and a recovery path that still preserves
   corrupt/unsupported originals are required before claiming write-crash recovery.
   Sources: [SQLite journal recovery](https://www.sqlite.org/lockingv3.html#dealing_with_hot_journals),
   [SQLite read-only rollback result](https://www.sqlite.org/rescode.html#readonly_rollback).

These are source-review findings; no pre-fix failed execution is claimed.

### Reviewer-authored history tests

`tests/Companion.Tests/IndependentConversationHistoryTests.cs` currently contains
16 test methods: literal search/injection, cross-conversation parent isolation,
immutable forks and restart, independent ancestry and multibyte context oracles,
cycle/missing-parent rejection, temporary repository separation, newer-schema and
corrupt-byte preservation, a real SQLite trigger abort between turn insertion and
leaf update, exclusive database locking, corrupt unselected branches, delete/search
cleanup, a real SQLite page-ceiling FULL failure, Unicode search parity, and normalized
UNC rejection. The page-ceiling seam keeps the fixture at tens of KiB and does not
fill the user's filesystem. Its result cannot be described as an actual OS disk-full
experiment. Millisecond timestamp comparisons match the declared stored format.

Repository separation alone does not prove whole-app temporary privacy after a
crash; the separately executed profile-wide sentinel experiment supplies that evidence.

### First executed history regression

Root's first test run completed with **267/267 tests passed**, zero failed, errors,
timeouts or not-executed tests. The reviewer independently parsed
`artifacts/qa/phase3-history-verification/phase3-history-first.trx`: all **16** methods
in `IndependentConversationHistoryTests` were executed and passed. SHA256:
`AE50903D0A118A8D4E4ECF617F6C7BECDDAA2C2144B0607FE2E3E0ABEBFBE4B0`.
The genuine SQLite FULL page-ceiling check, transactional trigger rollback,
corrupt-unselected-branch preservation, future-schema preservation and temporary
repository separation all passed. The exclusive-lock case passed in 2.1909976 seconds.

Root reported earlier strict compilation failures from six obsolete DataTestMethod
attributes and a compile-time constant assertion in newly authored tests. Those were
fixed before this successful execution; no assertion failure is claimed for them.
This evidence precedes the final renderer integration and native helper build and
therefore does not establish that later source state passed regression.

### First native history and document execution

The reviewer independently parsed both retained sets in
`artifacts/qa/desktop-ui-20260922-213916-c60d9fbc/first-run` and `profile`. Each reports
43 appearance, 24 desktop integration, 29 settings lifecycle, 49 placement
initialization, 54 chat lifecycle, **50 independent history** and **42 document**
assertions. Every reported count equals the passing-array length and unique-name
count, and all failure fields are null. History checks include real SQLite/WPF
reopening, title/search/branch/edit/regenerate, minimum layout, delayed load and
postcommit completion after close, unsaved-reply recovery into temporary mode,
persistent disclosure of earlier saved history and encoded sentinel scans.

The current document helper contains 46 checks; the executed binary contained only
42 because its build raced the final renderer/helper write. This discrepancy was
identified independently and reported to root. The 42-check results remain valid
historical evidence, but they do not establish the newer per-message parsing cases.
A frozen final build and native rerun are required. These native method/property
checks do not establish physical input, screen-reader behavior or visual typography.

### Frozen pre-recovery execution

The reviewer independently parsed both sets in
`artifacts/qa/desktop-ui-20260922-214203-090a483a/{first-run,profile}`. Each reports
43 appearance, 24 integration, 29 settings lifecycle, 49 placement, 54 chat,
**50 history and 46 renderer** checks. Counts equal array lengths and unique names;
all failure fields are null. These results cover the final per-message renderer
boundary, but precede the reply-preview feature and the hot-journal recovery fix.

The abrupt-process privacy result
`artifacts/qa/private-history-20260922-214333-88549cc1/result.json` is PASS, with
readiness and termination of the owned app process confirmed. The scanned profile
contains a 32,768-byte real SQLite saved decoy, instance lock, content-free ready
marker and application log. The supervisor searched every profile file for the
temporary prompt/reply encoded as UTF-8, UTF-16LE and UTF-16BE, and recorded file and
managed-assembly hashes. This is whole-app persistent-profile evidence with a
scripted provider, not an actual model process or OS pagefile/dump-erasure claim.

The retained real-worker result
`artifacts/qa/local-model-20260922-214337-9c4ffcd3/local-model-smoke.json` is PASS
with no process or fixed-content failures. Total elapsed time is 36.6946538 seconds;
the sampled worker private-byte maximum is 360,431,616, not a lifetime peak or total
RAM measurement. The 6,000-character oversized prompt fails with InvalidRequest
and zero streamed events; the nine-message context case completes once. Idle-worker
death uses the held owned process handle, and the same host supplies a new process
and adapter. Original, replacement and startup-cancelled owned workers all exit.

The reviewer manually inspected the unselected public case C response: its three
numbered actions are clearing clutter, using designated containers and regularly
cleaning the desk. It passes the predeclared narrow criterion of three distinct,
relevant, short desk-tidying tips. This does not imply broad assistant competence or
mathematical accuracy. The raw artifact correctly retains its manual-review flag;
the independent decision is recorded here. Actual tokenizer count parity and the
number of context pairs removed were not instrumented by this live helper.

### Late recovery and reply-preview review

Five additional independent tests use a real SQLite-generated flushed rollback
journal copied while a held transaction is synchronously idle. They cover committed
tree recovery, unsupported pre-transaction schema preservation, corrupt row
preservation, a header requesting one page beyond the 64 MiB database cap and a
super-journal trailer marker. The fixture independently requires the read-only
clone to report extended SQLite error 776 before testing ordinary recovery. It is
a transaction snapshot experiment, not an OS process-kill claim.

The first strict execution built with zero warnings/errors and ran 272 tests, but
root reported 32 failures: 27 established ACL/registry fixtures were denied by the
restricted process environment, and all five new journal fixtures failed while
reading SQLite's still-open write handle. The latter failed before calling recovery.
The reviewer changed only fixture reads to bounded FileStreams sharing the held
writer handle while the synchronous connection is idle. Root retains the failed
TRX and will rerun under the established normal-user validation context. No passing
claim is made from the failed attempt.

Source inspection of the production fix confirms narrowly handled error 776,
bounded copy validation before original mutation, full tree/schema checks, journal
header and super-journal rejection, source hashes rechecked before original recovery
and exact owned scratch cleanup. The read leases necessarily close before SQLite's
read/write open; the contract is a single app profile writer, not atomic protection
against arbitrary same-user file mutation. Unknown preexisting scratch content is
refused untouched in source; a dedicated executable fixture was deferred at the
build freeze and receives no executed-test claim.

The reply-preview source publishes only after successful terminal persistence and
completion cleanup. The App shows it for an inactive chat with a visible companion,
and dismisses it on opening, activating or closing chat. The compact native popup
has a bounded Rune-safe preview, one timer and explicit hide/disable/sleep/move/close
cleanup. This is source evidence until the final 34-check native helper executes;
even that helper will not establish physical pointer, focus or screen-reader use.

### Final frozen execution and independent artifact checks

Root's normal-user strict run completed with **272/272 passed**, zero failures,
errors, timeouts or skipped/not-executed cases. The reviewer independently parsed
`artifacts/qa/phase3-history-verification/phase3-history-final-272.trx` and verified
SHA256 `C8C33B7272B93BF579FEB4FD404D0FC1A1CC4433BC773F4A30675EA511D0A06A`.
All five hot-journal tests passed, in 0.224–0.287 seconds each. They establish both
recovery/reuse and byte-preservation assertions, including removal of owned scratch
after successful and rejected recovery. Root's strict command also reports zero
compiler warnings/errors, **11/11 architecture checks** and the composed WPF smoke
pass. The restricted failed attempt remains at
`artifacts/qa/phase3-history-verification/phase3-history-restricted-failure.trx`.
Independent parsing confirms its 240 passes/32 failures and the failure causes
described above; no failed test was removed or skipped to obtain the final result.

For `artifacts/qa/desktop-ui-20260922-215621-2d737c27`, the reviewer independently
parsed both `first-run` and `profile`: each has **51 compact, 43 appearance, 24
integration, 29 settings, 49 placement, 54 chat, 50 history, 46 renderer and 34 reply**
checks. All counts equal array lengths and unique names; every failure is null.
Root reports **28/28 supervisor controls**, including five negative privacy-probe
command-line cases. Native assertions are independently retained JSON; the process
supervisor total is attributed to root's command output. This binary includes the
recovery and reply-preview code. The subsequent strict rerun changed only test
fixture sharing, not the production assemblies exercised here.

The final abrupt-exit result is
`artifacts/qa/private-history-20260922-215831-6eee7933/result.json`: PASS, ready true,
owned process terminated true, no failure. The reviewer independently rescanned all
four raw profile files for both sentinels in UTF-8, UTF-16LE and UTF-16BE: **zero
matches**. The same independent rescan also found zero matches in the earlier
privacy profile. Final recorded Desktop DLL SHA256 is
`04ADC9350BF0112091A769A01CBF871D22ACB8331095A776A2DA3E4A268150F3` and Infrastructure
DLL SHA256 is `945BA61255190713BA4A08E04887E4DB9F64FA2749C221F563C2C222C11CCFE3`.
The real-worker evidence above remains tied to its earlier binary and protocol;
the late recovery/preview changes did not change the inference implementation.

Dependency versions, package-supplied licenses/provenance, lock hashes, restored
archive/metadata checks and the dated NuGet advisory query are recorded in
`docs/DEPENDENCIES.md`. Native runtime/model hashes and separate licenses remain in
`docs/LOCAL_AI.md` and `docs/licenses/`. This supports reproducibility, not a claim
of a complete source/security audit or final redistribution clearance.

Root additionally reports an exploratory native-input sample against a separate
public-fixture saved profile: Chat opened, a public prompt was sent, minimizing chat
allowed the compact reply preview to appear, and the chat was restored after preview
expiry. The attempted preview Open chat click arrived after expiry and failed with
a stale element; it is not a passing click test. The model produced bullets when
asked for a numbered list. That instruction-following miss is retained in
`PHASE_3_HISTORY_BUILD_EVIDENCE.md`; it earns no model-quality credit. This review
did not directly observe that input session and keeps physical/accessibility credit
partial. The report's score relies on the independently inspected artifacts above.

### Frozen criterion scoring

The five numbers in each row correspond exactly to that category's five frozen
criteria above; no criterion or weight was changed after execution.

| Category | Criterion points | Total | Evidence and remaining weakness |
|---|---|---:|---|
| Code Correctness | C1=2, C2=2, C3=2, C4=2, C5=2 | 10 | Final 272-test regression, real SQLite recovery and immutable tree oracles; 50 native history assertions in both runs. |
| Architecture | A1=2, A2=2, A3=2, A4=2, A5=2 | 10 | Application contracts, isolated SQLite adapter, serialized Task.Run boundary, separate memory store, 11 architecture checks and local-worker boundary. |
| Mathematical Correctness | M1=2, M2=2, M3=2, M4=1, M5=2 | 9 | Independent UTF-8/ancestry oracles and exact arithmetic; token-boundary fixtures plus actual worker admission. Live tokenizer-count parity was not directly instrumented. Typesetting is not numerical verification. |
| Testing | T1=2, T2=2, T3=2, T4=2, T5=2 | 10 | 272 tests with no skips; independently authored 16 history and five journal tests; native suites and separate raw-profile sentinel rescan. Genuine SQLite FULL is distinguished from OS disk exhaustion. |
| Reliability | R1=2, R2=2, R3=2, R4=2, R5=1 | 9 | Transaction trigger rollback, pending-turn recovery, corrupt/future preservation, close/save races and bounded cancellation. Actual midstream worker death and parent-death during create-to-job assignment remain untested hardening cases. |
| Security | S1=2, S2=2, S3=2, S4=2, S5=2 | 10 | Temporary sentinel evidence, literal parameterized search, bounded validated persisted trees, inert native rendering, local-only inference and content-free failures. Same-user hostile file races and forensic erase are outside the stated guarantee. |
| Performance | P1=2, P2=1, P3=1, P4=2, P5=2 | 8 | Bounded histories/files/events/preflight/deadlines. Per-file recovery bounds do not provide a single aggregate profile quota; off-dispatcher I/O and native responsiveness paths lack a quantified latency experiment at maximum history size. Historical CPU failure remains unchanged. |
| Maintainability | N1=2, N2=2, N3=2, N4=2, N5=2 | 10 | Explicit repository/turn contracts, versioned schema and refusal semantics, meaningful fault seams, pinned dependency inventory, required docs and retained evidence. No schema upgrade or general backup/export feature is claimed. |
| UX/UI | U1=2, U2=2, U3=2, U4=2, U5=1 | 9 | Visible Temporary/Saved state and earlier-saved warning, history/search/title/branches, stop/retry/copy and bounded rich documents/reply preview. Native automation/layout checks do not complete physical input, screen-reader, typography or mixed-DPI qualification. |
| Requirement Compliance | Q1=2, Q2=2, Q3=2, Q4=2, Q5=2 | 10 | Local pretrained/no-key choice, complete Phase 3 conversation surface, independent review and retained failure loop. Broad assistant/numerical claims and release approval remain excluded; later phase obligations stay explicit. |

Arithmetic: **10 + 10 + 9 + 10 + 9 + 10 + 8 + 10 + 9 + 10 = 95** criterion
points, divided by ten = **9.5/10**, unrounded and strictly greater than 8.5.
No unresolved critical correctness/security defect remains in the reviewed scope.

## TOP 5 RANKED FIXES / IMPROVEMENTS

1. **High — extend owned-worker crash hardening (Phase 11).** Close or explicitly
   supervise the create-to-job-assignment parent-death window and exercise real
   midstream worker death. Preserve saved pending prompts, report interruption and
   prove no owned orphan remains. Current idle-worker death and scripted stream
   faults do not establish these additional cases.
2. **Medium — strengthen recovery quota and refusal coverage (Phases 6/11).** Add
   an aggregate saved-profile/recovery budget and independent unknown-scratch,
   interrupted-recovery and actual sidecar-reparse fixtures. Verify refusal leaves
   unknown files byte-identical and app-owned scratch is cleaned after retry.
   Current per-file bounds and five successful journal cases remain valid.
3. **Medium — instrument exact live token accounting (model-validation increment).**
   Record the same pinned worker's prompt count for preflight and generation around
   the 1,792/1,793 boundary and expose pruned pair count without logging private
   content. This would close M4's partial credit; current fixture and source-path
   agreement is not an observed live equality measurement.
4. **Medium — measure dispatcher latency under bounded maximum histories (Phase 12).**
   Predeclare latency limits for history loading/search, settled rich rendering and
   repeated streaming updates, with observer cost recorded. Off-thread SQLite and
   bounded parsing are necessary but do not by themselves quantify UI responsiveness.
5. **High before release — complete physical/accessibility and resource qualification
   (Phases 11/12).** Exercise keyboard/pointer focus and dismissal, screen-reader
   announcements, high-contrast typography, mixed DPI and monitor removal when
   hardware becomes available. Repeat the unchanged host CPU/memory protocol;
   Phase 2's required aggregate remains FAIL. No model working-set sample or native
   method invocation substitutes for these gates.

## Regression risks and required tests

Primary risks are temporary content entering a durable repository, branch operations
silently rewriting original turns, completed/partial state crossing an async boundary,
SQLite synchronous work freezing the dispatcher, unbounded storage/results, and a
successful build being mistaken for privacy/recovery or full-phase qualification.

The saved-copy recovery path may temporarily hold saved transcript bytes in its
fixed app-owned scratch files; these are not temporary-chat content or automatic
user backups. Ordinary success/failure cleans them; process death during recovery
can leave them for the next recovery attempt. Unknown scratch is refused. The
hash/lease boundary is scoped to the app's single-profile writer. Per-token reply
checkpointing is absent, so a crash can lose uncommitted partial text while the
already committed sent prompt reopens as interrupted.

Final verdict: **PASS, 9.5/10 — Phase 3 development can advance to Phase 4.** This
combines this independent new-feature review with the separately independent 9.0
local-chat increment review. It does not approve release, broad model capability,
deterministic calculation, OS-forensic privacy or the unexecuted qualification
cases listed above. No Phase 4 implementation is evaluated by this report.
