# QA REVIEW — RESOURCE AND LIFECYCLE FOLLOW-UP

Phase: 2 — renderer/interactions, observer-cost investigation and lifecycle readiness

Overall Score: **8.7/10** — strictly greater than 8.5.

Result: **PASS for Phase 2 development completion under the explicitly revised validation sequence. Resource qualification FAIL; release qualification blocked.** Preserve the preceding
[8.6/10 phase-exit FAIL review](PHASE_2_FOLLOWUP_REVIEW.md) and the original
[8.0/10 failed checkpoint](PHASE_2_REVIEW.md) unchanged.

Reviewer: independent `phase0_qa` agent. Root owns production changes, application
launches, builds and test execution. The reviewer supplies independent persistence
tests, audits source and the experiment protocol/supervisor, and independently reads
retained test and measurement artifacts. No application, build or real input is
launched by this reviewer during this increment.

## Category Scores:

Scoring retains the same ten equally weighted categories, five 0/1/2 criteria each,
and strict greater-than-8.5 numerical test. The physical qualification and later
resource scheduling decisions are distinguished below. Missing physical evidence
receives no passing credit; the failed resource-budget criterion receives zero.

| Category | Five scored criteria and evidence | Score |
|---|---|---:|
| Code Correctness | **2** strict Release build has zero warnings/errors; **2** appearance/schema persistence cases pass; **2** independent state priority, ownership, sleep and concurrency tests pass; **2** owned-HWND placement/recovery/shortcut/style checks pass; **1** controlled save shutdown is verified, while every AppStop/storage-timeout interleaving is not. | 9 |
| Architecture | **2** state/geometry stay WPF-independent; **2** renderer implements the Application port; **2** native/storage responsibilities remain in their layers; **2** placement stays separate from preferences; **1** broader native fault injection and UI separation remain partial. | 9 |
| Mathematical Correctness | **2** independent nearest-placement lattice oracle passes; **2** translation/snapping/negative-coordinate properties pass; **2** independent decimal DPI conversion checks pass; **2** rational saved fractions, oversize handling and 1,000 round trips pass; **2** resource and score arithmetic are independently recomputed without altering budget denominators. | 10 |
| Testing | **2** final 196/196 Release tests pass; **2** twenty-one Phase 2 reviewer-authored state/geometry/placement/lifecycle cases execute; **2** final native evidence records 18 controls, 43 appearance and 24 integration checks; **2** 29 save/shutdown plus 49 initialization assertions exercise actual data and controlled failure/order scenarios; **1** actual input, physical display and full accessibility matrices remain incomplete. | 9 |
| Reliability | **2** immutable placement survives move/hide/dispose; **2** bounded storage, corruption/future schemas and atomic failures have coverage; **2** confirmed-shortcut-only recovery, conflict/reuse and click-through disposal have native evidence; **2** late-save and pending-placement races are fixed and exercised with real storage and controlled cancellation/failure/close ordering, alongside queued cancellation/retry tests; **1** modeled monitor loss and short scenarios do not prove physical hot-unplug or long-running stability. | 9 |
| Security | **2** native actions target owned HWNDs; **2** strict bounded schemas preserve unsupported data; **2** hidden/click-through states require recovery and disposal clears native styles; **2** explicit startup registration and thirteen registry cases use restricted test fixtures; **1** cross-account and exhaustive filesystem/registry-denial paths remain outside coverage. | 9 |
| Performance | **2** storage/work are bounded and saves debounced; **2** native checks prove stop policy and blink/clock ownership; **2** guarded fixed-hash measurement independently verifies counters and actual observer selection; **0** final-build process-only replicates fail the fixed-budget acceptance rule; **1** sampled memory passes but tray/compositor cost, percentiles, occlusion and long-run behavior remain unverified. | 7 |
| Maintainability | **2** state, geometry, persistence and recovery are separate readable modules; **2** strict builds, package locks and layer checks remain; **2** repeatable scripts retain protocol/binary identity and valid failures; **2** current/future scope and historical failed evidence remain documented; **1** final increment checkpoint and remote CI remain pending. | 9 |
| UX/UI | **2** semantic states and nonfunctional previews are labeled; **2** named controls and movement peer have native evidence; **1** actual input/recovery discoverability and focus remain unverified; **1** preference handling is implemented but screen-reader/physical-scale coverage is incomplete; **2** keyboard/menu next-display commands share the tested policy with single-display runtime limits stated. | 8 |
| Requirement Compliance | **2** Phase 2 functionality is substantially present; **2** scope traceability preserves later phases; **1** controlled resource evidence exists but the required physical matrix is absent; **2** independent adversarial findings and executable regression are supplied; **1** the development checkpoint is being finalized while release qualification is explicitly withheld. | 8 |

Exactly 50 criteria sum to **87/100**:
`(9 + 9 + 10 + 9 + 9 + 9 + 7 + 9 + 8 + 8) / 10 = 8.7`.
The numerical gate passes; 8.5 itself would fail. This does not convert a failed
experiment or an unexecuted qualification scenario into passing evidence.

## Evidence:

### Frozen question and protocol

The preceding ten-second blink run reported visible-idle CPU of 1.0413999329% of one
logical processor; that remains a valid budget failure. This increment investigates
measurement overhead without changing the production ten-second wait/120 ms blink,
the strict CPU limit of 1%, or the sampled private-byte limit of 150,000,000 bytes.

[PHASE_2_OBSERVER_PROTOCOL.md](PHASE_2_OBSERVER_PROTOCOL.md) was recorded before
execution, with SHA256
`21B08DD9E8075A4648E6256C9CB63A8A9B951F01B4735E9D522C451735562E48`.
The reviewer independently checked that digest. The fixed order is **A, B, B, A**:
A collects the existing two boundary thread snapshots per interval; B omits only
those snapshots. Both retain pre-warmup calibration, ten-second warmup, one-second
native process-counter reads, sixty-second intervals, input freeze and an absent tray.
All enabled overhead is included; no calibration or observer cost is subtracted.

Both B replicates must pass every interval for a limited process-counter-harness
pass on this machine. Any B failure fails that conclusion; a technical failure makes
the sequence incomplete. A failures are retained as valid observations. Two
replicates and uncontrolled external desktop activity do not establish statistical
significance, confidence bounds or general product resource compliance.

The reviewer audited the fixed-order supervisor before launch. It freezes five
binary hashes, the protocol hash and child measurement script; checks them before
and after each run; requires exactly one fresh profile; validates raw/budget agreement;
and accepts only the exact expected resource-budget exception as a valid failing run.
It does not replace invalid runs or select favorable outcomes. The child supervisor
verifies observer selection, actual capture-attempt counts, explicit null B snapshots
and nonempty A snapshots, in addition to existing blink, visibility and resource guards.

The ten-second warmup may straddle first blink completion/JIT, and the A observer's
first thread snapshot is not prewarmed. These are unchanged factors in this experiment.
Any warmup or calibration adjustment would require a separately declared experiment;
it cannot silently enter this comparison.

### Persistence and shutdown verification

- Root reports strict Release verification with zero warnings/errors, 11 architecture
  checks, composed smoke and **196/196 tests**. The reviewer reread the TRX and confirmed
  196 executed/passed, zero failed/not executed, including all three new independent cases.
  The exact TRX is archived at
  `artifacts/qa/desktop-ui-20260922-183217-1d198cb8/release-196-before-observer-abba.trx`;
  the reviewer copied it, matched SHA256 and reread its counts before measurement.
- `IndependentPlacementLifetimeTests.cs` covers cancellation queued behind a blocked
  commit without cancelling its owner or poisoning the next save; cancellation after
  real atomic commit returning success and publishing the committed baseline; and
  injected precommit write failure preserving the old bytes/baseline before a successful
  retry. Fixtures use isolated directories, real atomic writes and bounded coordination.
- Source review found a concrete late-save race: a committed save could resume UI/native
  work after App disposed the desktop adapter. Settings now receives BeginShutdown
  before adapter disposal, snapshots native preference requests at Save invocation,
  and suppresses late success/error/control updates and new saves after shutdown/closure.
  Already committed settings remain committed.
- The new real-window fixture uses isolated runtimes and profile directories. Its
  successful case completes a real runtime save before releasing the delayed continuation
  after shutdown; cancellation and I/O failure are injected separately. Checks cover
  persisted data, parent-profile preservation, disabled controls, no late companion/native
  effects, ignored new saves and closure. This is controlled ordering evidence, not every
  possible AppStop/storage-timeout interleaving.
- An independently found fixture issue was corrected before execution: WPF ownership
  requires an initialized/shown owner, so the opacity-zero companion is shown before the
  owned settings window is created. This follows Microsoft's
  [Window.Owner contract](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.owner?view=windowsdesktop-10.0).
- Root's pre-readiness-fix native run passes **17/17 process controls**, with 43 appearance,
  18 desktop integration and 29 settings-lifecycle assertions. The reviewer reread
  all three JSON reports under
  `artifacts/qa/desktop-ui-20260922-183217-1d198cb8/profile/`: no failure, adapter error
  or skip; motion checks enabled. The single enumerated display remains `DISPLAY1`
  with work area `(0, 0, 1920, 1080)`. Four new process controls reject malformed or
  misplaced observer arguments; they do not run performance intervals.
- Root retried the supported native interaction helper, including its updated skill
  and a fresh session, but received the same missing-pipe OS error 2. No actual input
  was performed. This limitation remains distinct from successful programmatic windows.

### Measurement results

The complete comparison is retained at
`artifacts/qa/observer-d217a835e8bc4ec9b6f27b98637024fd/comparison.json`.
Root's supervisor exits 0: four valid runs and both B replicates passing every limit.
The reviewer independently recomputed all twelve intervals' CPU percentages and
machine normalization, checked the fixed observer order and common five binary hashes,
sampled memory endpoints, scenario flags, capture attempts and absence of settings files.

| Run / retained profile suffix | Observer | Visible CPU % | Reduced CPU % | Hidden CPU % | Sampled private-byte peaks: visible / reduced / hidden |
|---|---|---:|---:|---:|---|
| A1 `183335-64a3d73d` | Threads | 0.885254 | 0.520667 | 0.208307 | 57,536,512 / 58,818,560 / 61,317,120 |
| B2 `183648-e1c44f4a` | Process | 0.755076 | 0.520774 | 0.260415 | 62,717,952 / 64,909,312 / 67,567,616 |
| B3 `184001-33ae7e27` | Process | 0.703079 | 0.468690 | 0.390573 | 62,734,336 / 63,594,496 / 67,227,648 |
| A4 `184314-cea787c1` | Threads | 0.989278 | 0.598873 | 0.286414 | 63,356,928 / 64,524,288 / 66,875,392 |

Each full profile is `artifacts/qa/performance-20260922-<suffix>/`. All four record
6/0/0 completed blinks and zero owned idle animation clocks. A has two nonempty
thread snapshots per interval; B has zero attempts and explicit null snapshots.
The exact unrounded values are retained and used for the strict limit comparison.
All A intervals also pass; A4 visible idle is close to the limit. B hidden CPU is not
uniformly lower than A. These observations support the predeclared limited acceptance,
not a blanket causal claim about observer cost or a production performance correction.
The historical 1.0413999329% failing run is unchanged and remains valid evidence.

This comparison identifies the pre-readiness-fix build (Desktop DLL SHA256
`EA85C37459E50A129445917B0017E8C6D5A8BA2F98C1CC0C21C334C4F263D88E`).
It does not certify later binary changes. The complete ABBA evidence is preserved
separately from the final startup/recovery build's failed confirmation below.

The separately predeclared [final-build protocol](PHASE_2_FINAL_BUILD_PROTOCOL.md)
requires exactly two process-only B runs after those fixes and affected regression,
both passing every unchanged interval limit. Its independently checked SHA256 is
`217CD856232210092F15773DE19062F6FB12A35B0185393C64EB771FB49CE174`.
The supervisor's `-FinalBuild` branch selects the two-run plan and distinct protocol
before launch, retaining the same binary/script freeze, guards and no-replacement rule.
Its final aggregate field is `allDeclaredRunsValid`; the earlier ABBA artifact's
`allFourRunsValid` field is preserved unchanged. The reviewer audited this branch
and parsed the script successfully without launching the app.

The final-build comparison is retained at
`artifacts/qa/final-build-1ca3041a363849aabc6b2c4e2170ce6f/comparison.json`.
Both declared runs completed and were technically valid, but the supervisor correctly
reports `complete-process-replicates-fail`, `allDeclaredRunsValid=true` and
`bothProcessRunsWithinBudget=false`, exiting 1. The first B run fails visible and
reduced-motion CPU; the second B run passes. The acceptance rule requires both.

| Final-build run / retained profile suffix | Visible CPU % | Reduced CPU % | Hidden CPU % | Sampled private-byte peaks: visible / reduced / hidden | Budget result |
|---|---:|---:|---:|---|---|
| B1 `185649-ab4dbf46` | 1.978694 | 1.015400 | 0.364563 | 64,974,848 / 65,224,704 / 62,337,024 | FAIL |
| B2 `190004-0f75069d` | 0.755208 | 0.598859 | 0.364527 | 63,119,360 / 64,102,400 / 63,520,768 | PASS |

Each full profile is `artifacts/qa/performance-20260922-<suffix>/`. The reviewer
independently recomputed all six CPU percentages from raw CPU and elapsed seconds,
checked the machine-normalized denominator, memory endpoints, common five binary
hashes, scenario flags and absence of settings files. Both runs record 6/0/0 completed
blinks, zero owned idle clocks, expected timer policy, zero thread-capture attempts
and explicit null thread snapshots. All sampled memory intervals pass. No overhead
is subtracted, and neither the earlier ABBA pass nor final B2 erases final B1.

The cause of the variation remains unresolved. Source audit found no recurring
application timer/owned-clock path surviving disabled, reduced-motion or hidden
policy: rendering stops the idle timer and owned clocks before applying the next
state. This does not rule out framework/compositor work, first-use costs or external
desktop effects. Nominal visibility and normal-window guards do not establish
unoccluded presented pixels; always-on-top is off by default. No source optimization
or generally compliant CPU budget is claimed, and no unchanged retry was selected.

### Additional code findings before finalization

A later read-only readiness audit by `desktop_recovery` found two concrete issues,
independently confirmed by this reviewer: movement could be accepted while saved-placement initialization was
pending, then ignored/overwritten by restoration; recovery availability trusted
`NotifyIcon.Visible` even though that is not native shell acknowledgement. The
[.NET 10 NotifyIcon source](https://raw.githubusercontent.com/dotnet/winforms/v10.0.0/src/System.Windows.Forms/System/Windows/Forms/NotifyIcon.cs)
stores the requested visibility and does not test the shell-add return value there.
These are code issues requiring correction and affected regression, separate from
unavailable physical qualification. No development pass is granted while they remain
unresolved. The frozen ABBA binary is not modified mid-experiment; later fixes require
explicit final-binary evidence rather than inheriting its measurements automatically.

The corrections have now been source-audited: placement disables interaction
synchronously before its first await, restores the prior enabled state only while
live, skips restoration while hidden/minimized and drains initialization on disposal.
App installs desktop actions only after this completes. Recovery availability now
requires a successfully registered hotkey; requested tray visibility alone cannot
authorize hide or click-through. Settings refreshes the corresponding checkbox after
registration/recovery. New native checks provide 49 assertions across eight controlled
placement-load scenarios and expanded recovery denial/positive paths with explicit
skips/registration attempts. Final executable regression is recorded below; final-binary
resource confirmation completed with the failed aggregate recorded above.
Root reports the first build of the new fixture failed with CS4007 because a
`SequenceEqual(await ...)` expression kept a span receiver across an await. Reading
the bytes into a local before comparison corrected that fixture compile error; the
failed build is retained in the execution history rather than described as passing.
The first native attempt on that fixture, retained at
`artifacts/qa/desktop-ui-20260922-185315-2646a9cb/profile/`, passed 43 appearance,
24 recovery and 29 settings-lifecycle assertions, then failed after 42 placement
assertions at the I/O-failure position check. It compared the whole rectangle even
though the expected error message increases the window's height. The corrected
fixture compares Left/Top only; its seeded saved location differs from the starting
right/top position, so an unwanted restoration would still fail. Production behavior
was not changed to force a pass, and this failed attempt is not combined with another
run to manufacture a complete native pass.

After correction, root reports a complete strict Release verification with zero
warnings/errors, 196/196 tests, 11 architecture checks and composed smoke. The matching
TRX is archived at
`artifacts/qa/desktop-ui-20260922-185538-0c91126a/release-196-before-final-build-confirmation.trx`;
the reviewer reread 196 executed/passed and zero failed/not executed.
Root's final native supervisor passes **18/18 controls** at
`artifacts/qa/desktop-ui-20260922-185538-0c91126a/profile/`. The reviewer independently
verified exact counts, raw-array lengths, unique names and absence of errors/skips:
**43 appearance, 24 recovery integration, 29 settings lifecycle and 49 placement
initialization assertions**. Both complete stage logs finish at 24.067 and 23.193
seconds. Those timings include deliberate blink waits and are not launch percentiles.
The final Desktop DLL is SHA256
`F86591B536A2A3BBBBF3B9DE1FBDF7CF2F6671744FDB8A65D1ABBF6C15F0DBB0`;
both final resource runs identify this exact binary, with the failed aggregate retained.

### Development sequencing and release qualification

During the final A run, before its result and this verdict, the user confirmed that
no second monitor is available. The reviewer reread the original request: it permits
roadmap adjustment for a better architectural sequence and requires independent QA
strictly above 8.5 before advancement. Physical matrix timing was an additional
requirement in our Phase 0 plan, not a user-specified Phase 2 hardware prerequisite.

The reviewer independently endorsed the recorded
[validation-sequence decision](PHASE_2_VALIDATION_SEQUENCE.md). Phase 3 conversation
and provider work does not require additional display hardware. The sequencing
decision does not waive actual code defects: the later readiness findings above must
be resolved before completion. The
[roadmap](../../ROADMAP.md) now moves unavailable physical/input qualification to
mandatory Phase 11/12 pre-release gates. At that time the plan retained automated/native
checks, the full frozen ABBA acceptance condition and independent score >8.5 for Phase 2.

This changes scheduling, not the required behavior or evidence: actual 100/150/200%
mixed-DPI/negative-coordinate/disconnect/work-area scenarios, drag/focus/tray/hotkey/
click-through input, screen-reader and settings reachability, and real sign-in remain
unexecuted. Startup and click-through remain off by default. The
[manual matrix](PHASE_2_MANUAL_MATRIX.md) keeps the exact pending scenarios, which
must be completed before release. Testing/UX scores remain partial; prior 8.0 and
8.6 failed exits remain valid historical decisions under the then-current gate.
The frozen protocol, CPU/memory limits and observer sequence were not changed.

**After the final-build pair failed**, the reviewer separately reconsidered resource
qualification timing against the original brief. The explicit
[post-result resource sequence decision](PHASE_2_RESOURCE_SEQUENCE.md) records that
the user places renderer/interactions in Phase 2 and performance optimization in
Phase 12, permits architectural resequencing and requires the weighted QA gate.
The original Phase 2 roadmap required an idle baseline; the later pass-all-resource
condition was an additional planning constraint. The current bounded motion/memory,
passing correctness regressions and absence of a known critical source defect do not
make provider/conversation development depend on resolving CPU attribution first.

Independent QA therefore approves development advancement at 8.7, with Performance
remaining 7 and the budget criterion zero. This is a **post-result scheduling change**,
not a predeclared successful experiment, resource waiver or release approval. The
failed pair and earlier failed phase exits remain unchanged. Phase 12 must satisfy
the same strict CPU and memory limits under a separately predeclared repeat protocol,
including visible-idle, reduced-motion and hidden intervals, actual presentation/
occlusion control, warmup and observer accounting. Native profiling must support any
claimed causal optimization. Critical data-loss, security or recovery defects would
still block advancement; the identified startup/recovery defects have been corrected
and exercised on the final build.

## TOP 5 RANKED FIXES / IMPROVEMENTS

1. **Problem:** The final-build resource confirmation fails: B1 exceeds visible-idle
   and reduced-motion CPU limits, while B2 passes; the cause remains unresolved.
   **Why it matters:** Selecting B2 or the prior binary's ABBA pass would conceal the
   failed repeatability requirement. Occlusion, warmup and observer effects remain
   uncontrolled, and broader tray/compositor and long-run costs are unqualified.
   **Proposed fix:** Complete mandatory Phase 12 profiling and separately predeclared
   presentation-controlled repeat/soak checks before release. Retain all outcomes,
   exact binaries and unchanged limits; do not subtract estimated observer overhead.

2. **Problem:** Physical mixed-DPI, negative-coordinate and disconnect/work-area
   qualification remains unexecuted on the user-confirmed single-monitor setup.
   **Why it matters:** Modeled geometry and one-display HWND checks cannot establish
   actual WPF transitions, hotplug or reachable layout on every supported display.
   **Proposed fix:** Complete the retained physical matrix on suitable test hardware
   before release, recording real layouts, scales, actions and resulting placement.

3. **Problem:** Actual tray/shortcut/click-through/drag input and accessibility
   qualification remain unavailable because the supported native helper cannot connect.
   **Why it matters:** Programmatic method calls and native style inspection do not
   prove input delivery, visible focus, discoverability or screen-reader usability.
   **Proposed fix:** Restore the supported interaction environment or conduct documented
   human checks in an isolated profile before release; retain unexecuted rows explicitly.

4. **Problem:** Real Windows sign-in and shell-restart behavior remain unqualified.
   **Why it matters:** Isolated registry fixtures and requested tray visibility cannot
   prove startup execution or notification-area availability in a user session.
   **Proposed fix:** Test enable/disable, sign-in, moved executable and shell recovery
   in a disposable account; keep startup opt-in and recovery fail-safe throughout.

5. **Problem:** Controlled lifecycle scenarios do not exhaust app-wide fault timing.
   **Why it matters:** Slow initialization, shutdown deadlines, native failures and
   competing storage operations can interact beyond the tested continuations.
   **Proposed fix:** Add targeted fault injection when those paths change, including
   bounded close-during-load and storage-timeout cases, without weakening atomic commits,
   current-user permissions or late-continuation guards.

## Regression Risks:

Late save completion must never re-register shortcuts, restore click-through, change
startup or revive animation after shutdown. Successful atomic commits must not be
reported as rolled back because cancellation arrived later. Keep observer mode, cadence,
warmup, hashes and actual completed-blink evidence explicit; never subtract estimated
cost or merge a diagnostic run with a clean measurement without labeling the difference.

## Tests Required:

The frozen ABBA comparison and final-build pair are complete and independently
inspected; the final pair remains FAIL. Before release, complete the predeclared
resource qualification with fixed limits and recorded presentation/occlusion,
warmup and observer conditions, plus tray/compositor, percentile and long-run checks.
Physical mixed-DPI/negative-coordinate/disconnect and actual input/accessibility
checks remain distinct from programmatic HWND assertions. Real sign-in remains
required. No additional unchanged run is presented as a remedy for the failed pair.

## Final Verdict:

**PASS for Phase 2 development completion: 8.7/10, strictly above 8.5.** The final
code/native regression evidence supports finalizing the development checkpoint and
advancing to Phase 3 under the explicitly revised roadmap. No known critical source
defect remains from this review; this is bounded evidence, not exhaustive correctness.

**Resource qualification FAIL; release qualification blocked.** The post-result
sequencing decision defers satisfaction of the unchanged resource requirement to
mandatory Phase 12 qualification. Physical/input/accessibility/sign-in obligations
also remain mandatory before release. Historical failed reviews, both final B runs,
the zero-point budget criterion and unresolved CPU cause are retained without
reinterpretation as passing measurements.
