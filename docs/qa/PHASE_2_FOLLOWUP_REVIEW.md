# QA REVIEW — FOLLOW-UP

Phase: 2 — resumed desktop validation and resource investigation, 2026-09-22

Overall Score: 8.6 / 10

Result: FAIL — the numerical threshold passes, but required phase-exit evidence and
the visible-idle resource budget do not. Phase 2 remains incomplete. This follow-up
does not replace the preserved **8.0/10 FAIL** checkpoint in
[PHASE_2_REVIEW.md](PHASE_2_REVIEW.md).

Reviewer: independent `phase0_qa` agent. The root implementation agent owns execution
of builds, tests, native window checks and profiling. The reviewer independently
audits source and retained artifacts, recomputes numerical claims, and maintains the
measurement supervisor. No build or application was launched by this reviewer during
the resumed validation, avoiding interference with root's sequential measurements.

## Category Scores:

Five criteria per category use 0 missing/contradicted, 1 partially verified and 2
satisfied by phase-relevant evidence. Equal weights and the strict greater-than-8.5
threshold are unchanged. Exact arithmetic is
`(9 + 9 + 10 + 9 + 8 + 9 + 7 + 9 + 8 + 8) / 10 = 86 / 10 = 8.6`.
Thus the numerical test **8.6 > 8.5 passes**; 8.5 itself would fail. The phase is still
incomplete because its required physical exit matrix is missing and visible-idle CPU
exceeds the declared limit. No score rounding or resource threshold adjustment is used.
This is a pre-existing completion condition, not a new review requirement:
[ROADMAP.md](../../ROADMAP.md) labels its exit evidence as additional to the score
threshold, and the Phase 2 row requires the mixed-DPI/negative-coordinate/disconnected
monitor matrix. The reviewer verified that identical row with `git show HEAD:ROADMAP.md`
in the preceding Phase 1 checkpoint. The one-display results cannot satisfy it.

| Category | Five scored criteria and evidence | Score |
|---|---|---:|
| Code Correctness | **2** strict Release build has zero warnings/errors; **2** appearance persistence and schema cases pass; **2** independent state priority, owner identity, sleep invalidation and concurrency cases pass; **2** owned-HWND placement/recovery/shortcut/style regressions pass; **1** cancellation and disposal guards are reviewed, while exhaustive injected lifetime races remain incomplete. | 9 |
| Architecture | **2** state/geometry are WPF-independent; **2** renderer implements the Application port; **2** native and storage responsibilities stay in their intended layers; **2** saved placement is separate from preferences; **1** native failure injection and longer-term UI separation remain partial. | 9 |
| Mathematical Correctness | **2** nearest placement uses an independent integer-lattice oracle; **2** translation, snapping and negative-coordinate properties pass; **2** DPI conversion is compared with decimal arithmetic; **2** rational saved fractions, oversize behavior and 1,000 round trips pass independent checks; **2** resource formulas and score arithmetic are independently recomputed. | 10 |
| Testing | **2** 193/193 Release tests pass; **2** eighteen reviewer-authored state/geometry/saved-placement cases execute; **2** latest native evidence passes 12 controls, 43 appearance and 18 integration checks; **2** final registry seam, settings reopening and blink interruption/clock-release regressions execute; **1** actual input, physical display and accessibility matrices remain incomplete. | 9 |
| Reliability | **2** immutable pending placement survives move/hide/dispose; **2** bounded storage, corruption/future schemas and atomic failures have coverage; **2** shortcut conflicts/reuse and click-through recovery/disposal have native evidence; **1** initialization/shutdown containment is source-reviewed without exhaustive fault/race execution; **1** modeled missing monitors do not establish hot-unplug or long-run stability. | 8 |
| Security | **2** native actions target owned companion windows; **2** strict bounded schemas preserve unsupported documents; **2** hidden/click-through states require recovery and dispose clears native styles; **2** startup registration is explicit and thirteen registry cases use a validated GUID test namespace; **1** cross-account and all filesystem/registry-denial scenarios remain outside executed coverage. | 9 |
| Performance | **2** work/storage are bounded and saves debounced; **2** native tests verify stop behavior and blink/clock ownership; **2** a guarded reproducible harness retains hashes and independently checked counters; **0** the final visible-idle interval exceeds the fixed CPU budget; **1** sampled memory passes but tray/compositor cost, percentiles, occlusion and long-run behavior remain unverified. | 7 |
| Maintainability | **2** modules separate state, geometry, persistence and recovery; **2** strict builds, lockfiles and layer checks remain; **2** named assertions and scripts preserve repeatable evidence; **2** current/future scope and historical failures remain documented; **1** final source checkpoint and remote CI remain pending. | 9 |
| UX/UI | **2** states and nonfunctional previews are labeled; **2** named controls and movement peer have native evidence; **1** real input/recovery discoverability and focus remain unverified; **1** preference handling is implemented but screen-reader/physical-scale coverage is incomplete; **2** keyboard and menu next-display actions now share the tested monitor-cycle policy, with single-display runtime limits stated. | 8 |
| Requirement Compliance | **2** planned Phase 2 functionality is substantially implemented; **2** scope traceability keeps providers/voice/tools/packaging in future phases; **1** a controlled baseline exists but the required physical matrix is missing; **2** independent adversarial review, ranked fixes and final executable regression are supplied; **1** the increment checkpoint is being finalized while phase completion remains explicitly withheld. | 8 |

## Evidence:

### Completed resumed verification

- The root resumed normal-user verification. The reviewer reread the Release TRX with
  176/176 passes, including all 13 previously unexecuted registry cases. This closes
  that earlier execution gap; it does not verify actual Windows sign-in behavior.
- After native resource sampling and next-monitor work, root reports Release build
  with zero warnings/errors, 193/193 tests, 11/11 architecture checks and composed
  smoke. The reviewer reread `artifacts/test-results/Release/tests.trx` and confirmed
  total/executed/passed 193 with no errors, failures or skipped cases.
  The exact report is archived at
  `artifacts/qa/desktop-ui-20260922-175553-c176ac05/release-193-before-cadence-only-change.trx`;
  the reviewer independently reread that copy and confirmed 193 executed/passed,
  zero failed and zero not executed. This full suite precedes the cadence-only
  production constant change; the later strict build and ten-second native smoke
  verify that final change, as separately recorded below.
- Root reports 11/11 UI process controls for
  `artifacts/qa/desktop-ui-20260922-172758-2645cfbb/profile/`. Its independently read
  JSON records 37 appearance assertions and 18 native integration assertions with no
  integration skips/errors. New checks cover monitor enumeration, next-display action
  and scoped desktop-error preservation/clearing.
- That artifact enumerates **one** display, `DISPLAY1`, with work area
  `(0, 0, 1920, 1080)`. Performance metadata records 120 DPI. The next-display result
  therefore exercises the single-display no-op, not a physical cross-monitor transfer.
- The root reports supported Computer Use window discovery failed repeatedly because
  the native helper pipe was unavailable (OS error 2), including after reinitializing
  its session. No actual user-input actions were attempted. Programmatic HWND evidence
  remains distinct from physical mouse/keyboard/tray and accessibility validation.
- The final corrected supervisor run at
  `artifacts/qa/desktop-ui-20260922-174916-bd8a448e/profile/` passes **12/12 controls**
  (root execution). The reviewer reread 43 distinct appearance assertions, 18 native
  integration assertions, empty skips/errors and null failure. `blinkChecksEnabled`
  is true. Two settings-recovery stage markers complete at 14.485 and 14.462 seconds.
  These include interrupting a blink without counting it, a completed timed blink,
  two owned speaking clocks and their release on returning to idle, hidden resource
  release, and reopening the same hidden/minimized settings editor.
- The preceding `desktop-ui-20260922-174634-ef0f4899/profile/` run completed its two
  app processes and 43 appearance assertions, but the already loaded old supervisor
  expected 37 and failed. That failed run is preserved. The corrected script requires
  exactly 43 checks when OS motion permits blinking, or 38 with one explicit skip
  reason otherwise; it checks the five conditional assertion names individually.
- After the ten-second cadence adjustment, root reports a zero-warning/error Release
  build and another **12/12** native supervisor pass at
  `desktop-ui-20260922-175553-c176ac05/profile/`. The reviewer reread 43 appearance and
  18 integration passes, empty skip/error arrays, enabled blink checks and completed
  recovery stages at 22.796 and 21.400 seconds. Core test logic was unchanged; the
  193-test suite predates this cadence-only change, while this native run executes it.

### Resource definitions fixed before controlled measurement

The reviewer and implementation agent resolved the previously ambiguous architecture
units before the resumed controlled run. CPU must be **strictly below 1% of one
logical processor over sixty seconds**, calculated as
`100 * process CPU seconds / elapsed seconds`. Private bytes must be **strictly below
150,000,000 bytes**. Dividing CPU by logical-processor count is informational only.
No calibration cost is subtracted and no limit is relaxed after seeing results.
The decision is recorded in `ARCHITECTURE.md`.

Reference hardware: AMD Ryzen 7 5700U, 16 logical processors, 7,928,467,456 physical
RAM bytes; Windows build 26200, .NET 10.0.12 x64, 120 DPI. The three-interval harness
uses ten seconds of warmup and approximately sixty seconds per mode. It excludes tray
integration and does not measure another process's compositor/GPU cost or percentiles.

| Retained run | Visible-idle one-core CPU | Reduced-motion one-core CPU | Hidden one-core CPU | Peak private bytes, corresponding modes | Interpretation |
|---|---:|---:|---:|---|---|
| `performance-20260922-164711-eccda553` | 22.081% | 6.900% | 0.859% | See historical report | Rejected as contaminated: two settings saves and animation during reduced-motion. Preserved, never treated as a controlled baseline. |
| `performance-20260922-172039-f052a9fb` | 5.832785% | 3.098500% | 1.900506% | 64,126,976 / 64,860,160 / 65,970,176 | Valid controlled run using framework process-memory sampling. All CPU modes fail; all memory modes pass. Harness overhead is included. |
| `performance-20260922-172825-fc26bc57` | 5.260228% | 2.708053% | 2.707961% | 65,101,824 / 65,290,240 / 62,021,632 | Valid controlled run with direct native counters and unchanged continuous renderer. All CPU modes still fail; all memory modes pass. |
| `performance-20260922-175003-37870412` | 1.093335% | 0.494627% | 0.416623% | 63,504,384 / 65,265,664 / 63,492,096 | Valid untraced six-second-blink run. Visible idle still fails the unchanged CPU limit; reduced/hidden CPU and all sampled memory modes pass. Supervisor exits unsuccessfully and preserves evidence. |
| `performance-20260922-175657-e7f2743d` | 1.041400% | 0.520719% | 0.390566% | 62,914,560 / 64,225,280 / 66,539,520 | Final valid untraced ten-second-blink run. Visible idle still fails; reduced/hidden CPU and all sampled memory modes pass. Fixed-budget supervisor reports failure. |

The reviewer independently recomputed CPU percentages and machine normalization,
checked peaks include endpoints, checked mode animation/visibility flags, and confirmed
no settings file or `SettingsSaved` event in the two controlled profiles. Read-only
assertions exited 0. Those successful assertions validate the arithmetic and scenario,
not resource-budget attainment.

The six-second-blink run records 10/0/0 completed timed blinks, an idle timer observed
only during visible idle and zero owned clocks in every interval. The reviewer reread
raw/budget JSON, recomputed the CPU arithmetic and checked absence of a settings file.
The combined renderer/clock-lifetime change is associated with lower measured CPU;
this experiment does not isolate which change caused the reduction. A subsequent
ten-second cadence was recorded in `ARCHITECTURE.md` before a new run, retaining 120 ms eye closure, actual
completed-motion guards and the same resource limits. The failing run is not erased.
Native smoke waits were increased from nine to thirteen seconds to accommodate the
actual cadence; the supervisor bound increased from thirty to forty seconds for two
such waits plus setup/integration. Neither deadline is a claimed launch percentile.

The final run records exactly 0.625 / 0.3125 / 0.234375 process CPU seconds over
60.0153678 / 60.0131677 / 60.0090508 elapsed seconds. Six completed blinks occur only
in visible idle; the idle timer is observed only there, with zero owned animation
clocks in all modes. The reviewer independently recomputed both CPU denominators,
checked sampled peaks cover endpoints, confirmed the intended animation/visibility
and Normal window-state flags, found no settings file, and matched all five retained
SHA256 hashes to the unchanged Release executable/project DLLs. The 1.0413999329%
visible-idle result is a failure even though it is close to 1%; it is not rounded down.
These are single runs and sampled memory peaks, not statistical confidence bounds,
continuous memory maxima or a cross-hardware performance guarantee. No further
same-build rerun is used to select a favorable result.
Counter granularity and uncontrolled single-run variation also prevent claiming that
the small six-to-ten-second difference demonstrates a general CPU improvement.

The native-counter run's separate calibration records 60 back-to-back framework reads
using 0.375 CPU seconds and 0.3313087 elapsed seconds; 60 native captures took 0.0000394
elapsed seconds and recorded zero CPU ticks. The latter is below counter resolution,
not proof of literally zero overhead. These are whole-process diagnostic observations
before warmup; no result is subtracted from the timed intervals. At that historical
point, hidden and reduced-motion CPU still exceeded the budget and required diagnosis;
it could not all be attributed to visible animation or measurement overhead. The
later combined renderer/lifetime change passes both modes in the final measured run,
without establishing the isolated cause of their improvement.

The separate `trace-20260922-173917-2d202e7e` profile retains EventPipe/nettrace and
Speedscope diagnostics, using Microsoft's workspace-only pinned `dotnet-trace`
10.0.745401. It is **not a budget run** and is not added as an application dependency.
Managed samples dominated waits and did not establish the cause of the process CPU
floor. Thread identifiers can be reused, exited threads can disappear, and start
addresses were zero on this OS. No thread-role attribution is treated as proven.
The later untraced harness retains two boundary thread snapshots inside each timed
interval; their overhead is included and never deducted.

The native sampler was independently checked against Microsoft's primary API contracts:
[GetProcessTimes](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getprocesstimes)
returns cumulative kernel/user time in 100-nanosecond units; the
[memory structure](https://learn.microsoft.com/en-us/windows/win32/api/psapi/ns-psapi-process_memory_counters_ex)
uses pointer-sized fields and `PrivateUsage` for private committed bytes.
Its borrowed current-process pseudo-handle is not closed. Four tests compare native
and framework CPU/memory readings; they are included in the 193-pass artifact.

The QA supervisor now records fixed-threshold per-interval results in
`budget-evaluation.json` and exits unsuccessfully on a budget breach after preserving
raw evidence. It rejects settings saves, unexpected animation in reduced/hidden mode,
unexpectedly stopped visible animation when system preferences permit it, and wrong
visibility/state endpoints. Earlier running PowerShell copies may predate these
supervisor edits; their exit 0 never supersedes the independently assessed raw failure.

### Source findings and diagnostic limits

- Placement's pending snapshot, initialization drain and stopped guards remain intact.
  App checks stopping before continuing post-initialization work and contains desktop
  disposal errors. No new critical defect was found in those changes.
- Scoped shortcut messages now clear after successful registration without clearing
  unrelated storage messages. Status height follows measured text height. The latest
  37-check artifact verifies error preservation and selective clearing.
- Ctrl+Shift+M and a next-display menu action now use a deterministic monitor cycle
  and the existing two-step restore. Entry/continuation guards prevent transfer while
  hidden, disabled, closing or during measurement input freeze. Mathematical policy
  tests do not establish physical DPI transition behavior.
- The root found that activating an existing hidden/minimized settings window did
  not restore it. Source now recovers the companion and shows/restores the existing
  editor. The latest twelve-control native run passes both reopening regressions.
- Direct process counters avoid repeated system-wide process-memory snapshots.
  Thread CPU snapshots and EventPipe profiling were added as diagnostics; runs
  with profiling overhead are not clean resource-budget evidence.
- Clock lifetime is a hypothesis under investigation. Microsoft documents that
  [removing a property animation](https://learn.microsoft.com/en-us/dotnet/api/system.windows.uielement.beginanimation?view=windowsdesktop-10.0)
  does not necessarily stop the associated timeline. Current WPF source detaches
  property clock handlers, maintains root clock weak references and cleans collected
  clocks; it also clears `NeedsTicksWhenActive` when handlers disappear. Thus retention
  alone does not prove the observed CPU cause. A retained controllable clock can request
  explicit removal, but runtime trace and measured comparison are required before
  attributing the performance floor to that mechanism. No forced GC is accepted as an
  idle product fix or benchmark adjustment.
- The new idle renderer initially waited six seconds (now ten), closes the eye transforms for 120 ms,
  then reopens them, with no idle animation clocks. Non-idle states explicitly own
  controllable clocks and stop/remove them before clearing property animations.
  Stop restores interrupted eyes without incrementing the completed-blink counter;
  disposal unsubscribes the timer and Unloaded handler. Companion closure disposes
  the renderer, and later motion updates return through idempotent Stop. The reviewer
  found no new blocking source defect in this lifecycle. Timed property transitions
  and enabled-policy flags are not pixel capture or frame-timing proof.
- The measurement supervisor now requires completed blinks and an observed timer
  for visible idle when permitted by OS preferences, zero owned idle clocks, and
  zero blinks/timers/clocks in motion-disabled intervals. Eight independent in-memory
  fixture checks exercise valid enabled/disabled reports and six invalid resource
  cases (`artifacts/qa/measurement-guard-checks.txt`); no application was launched.
  Before every future run it records SHA256 for the executable and all four project
  DLLs. Both edited PowerShell scripts pass parser checks.

## TOP 5 RANKED FIXES / IMPROVEMENTS

1. **Problem:** Final visible-idle CPU is 1.0413999329%, exceeding the strict 1% limit.
   **Why it matters:** Reduced-motion and hidden improvements do not establish that
   the animated-idle requirement has been met; rounding or selecting another run
   would hide the remaining breach.
   **Proposed fix:** Diagnose the remaining visible-window cost with bounded profiling,
   make an evidence-based correction, then use a declared repeat protocol with the
   same untraced intervals, active-motion guards and unchanged budget. Preserve all
   outcomes and avoid inferring a cause from blocked managed samples alone.

2. **Problem:** The physical mixed-DPI/negative-coordinate/disconnect matrix remains
   unavailable on the observed one-display setup.
   **Why it matters:** Pure geometry and single-display native checks cannot exercise
   actual DPI transition, hotplug or cross-display input behavior.
   **Proposed fix:** Use a suitable dedicated display configuration and record the
   pending matrix in `PHASE_2_MANUAL_MATRIX.md` without altering an unrelated session.

3. **Problem:** Actual keyboard/mouse/tray and accessibility verification remains
   incomplete because the supported native interaction helper is unavailable.
   **Why it matters:** Programmatic adapter calls do not prove discoverability, focus,
   click-through delivery, shortcut invocation or screen-reader usability.
   **Proposed fix:** Restore the supported helper or perform documented human checks
   in an isolated profile; do not silently substitute another automation route.

4. **Problem:** Resource evidence covers a single machine and three short modes with
   tray disabled; boundary diagnostic overhead remains included.
   **Why it matters:** This does not measure tray/compositor cost, launch/frame
   percentiles, arbitrary occlusion or longer-term resource stability.
   **Proposed fix:** Keep this controlled baseline and add separately labeled,
   predefined repeat/soak and tray-enabled scenarios on reference hardware. If the
   diagnostic method changes, record that change and retain both methods' results;
   never subtract estimated overhead or silently compare unlike runs.

5. **Problem:** Actual sign-in startup and exhaustive shutdown/failure races remain
   unverified despite passing isolated registry and normal lifecycle checks.
   **Why it matters:** Test registry keys do not prove Windows launches the intended
   executable at sign-in or that every interrupted native/storage operation cleans up.
   **Proposed fix:** Use a disposable account for opt-in/out, sign-in and moved-binary
   cases; inject the remaining bounded lifecycle failures and record resulting cleanup.

## Regression Risks:

Preserve late-activity ownership, stopped animation clocks, hidden-position flushing,
safe startup test-key boundaries, shortcut rollback/recovery, measurement input freeze
and phase-specific evidence attribution. Never mix a trace-instrumented run with a
clean budget run, subtract calibration estimates or treat a single-display action as
proof of multi-monitor behavior.

## Tests Required:

The final cadence is built and its native blink/settings regressions pass; a new
blanket rerun is not required solely to restate those results. Required next work is
the existing physical display matrix, actual input/accessibility scenarios, remaining
sign-in/fault coverage and an unchanged-budget measurement after a justified resource
correction. Future executable changes require affected regression checks. Preserve
the 193-test and final 12/43/18 native evidence with their exact scope and retain all
failed resource artifacts. Phase 1 Debug results do not become Phase 2 Debug evidence;
no remote CI execution is claimed.

## Final Verdict:

**FAIL phase exit; 8.6/10 numerical score.** The strict score condition passes, but
the pre-existing roadmap additionally requires the physical mixed-DPI/disconnected
monitor matrix. That evidence is absent on the observed one-display setup, actual
input validation remains unavailable, and the final visible-idle CPU budget fails.
No unresolved critical source defect is invented from these evidence gaps. Preserve
this as an in-progress source checkpoint, retain the historical 8.0 report, continue
Phase 2 validation/correction and do not advance to Phase 3.
