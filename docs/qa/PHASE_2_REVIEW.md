# QA REVIEW

Phase: 2 — avatar states, desktop interaction, placement and recovery integration

Overall Score: 8.0 / 10

Result: FAIL — Phase 2 remains incomplete; do not advance to Phase 3.

Reviewer: independent `phase0_qa` agent, 2026-09-22. This is a complete checkpoint
review of the implemented increment and its evidence, not approval of the phase.
The earlier `PHASE_2_INCREMENT_REVIEW.md` remains historical. The reviewer inspected
source concurrently, reported defects, supplied independent adversarial tests and
authored the performance harness/script. Production implementation and execution of
this checkpoint's builds, tests and native UI checks are attributed to the root agent.
The reviewer independently read the resulting TRX/JSON artifacts and checked their
contents. No actual user input or Windows startup setting was changed by this reviewer.

## Category Scores:

- Code Correctness: 9 / 10
- Architecture: 9 / 10
- Mathematical Correctness: 10 / 10
- Testing: 7 / 10
- Reliability: 8 / 10
- Security: 9 / 10
- Performance: 6 / 10
- Maintainability: 9 / 10
- UX/UI: 7 / 10
- Requirement Compliance: 6 / 10

## Evidence:

### Scoring and scope

Five criteria per category use the repository rubric: 0 missing/contradicted,
1 partially verified, 2 satisfied by phase-relevant evidence. Categories have equal
weight. Exact arithmetic is:

`(9 + 9 + 10 + 7 + 8 + 9 + 6 + 9 + 7 + 6) / 10 = 80 / 10 = 8.0`.

The strict threshold is **greater than 8.5**; 8.5 itself fails. Independently checked
decimal arithmetic confirms this sum and boundary. No rounding changes the result.
Even a higher arithmetic score would not substitute for the roadmap's missing
physical display matrix or a controlled idle baseline.

Implemented scope includes appearance migration, eight semantic avatar states,
scoped activity ownership, explicit expression previews, sleep/wake, scale/opacity,
topmost, drag/keyboard movement, physical-pixel clamping/snapping, separate saved
monitor-relative placement, tray/hotkey recovery, session click-through and explicit
startup registration. Previewing Listening does not activate a microphone. Providers,
voice jobs, chat, tools, scientific computation, asset packs and packaging remain
future work. Numerical scores here concern geometry and measurement arithmetic,
not future scientific accuracy or physical mixed-DPI behavior.

| Category | Five scored criteria and evidence |
|---|---|
| Code Correctness | **2** root's strict Release verification compiled with zero warnings/errors; **2** persisted appearance/schema recovery cases pass in the 163-test suite; **2** independent state ownership, priority, sleep invalidation and concurrent disposal tests pass; **2** actual owned-HWND movement, saved-position round trip, recovery, hotkey conflict and style-bit checks pass in the native smoke artifacts; **1** final startup registry seam, shutdown guards and measurement input freeze are source-reviewed but their final executable regression is pending. |
| Architecture | **2** geometry/state remain independent of WPF in Core; **2** the renderer implements the Application avatar port; **2** Desktop owns UI/native lifetime and Infrastructure contains persistence/native placement; **2** placement has a separate bounded file and cannot overwrite preferences; **1** desktop/startup adapters are clearly scoped, but remaining native failure injection and longer-term UI separation are partial. |
| Mathematical Correctness | **2** nearest feasible placement is checked against an independently enumerated integer lattice; **2** translation invariance, snapping idempotence and negative coordinates have independent oracle checks; **2** DIP conversion is compared with independent decimal arithmetic at multiple DPIs; **2** rational-grid saved-position expectations, one-axis oversize behavior and 1,000 resolution round trips pass reviewer-authored tests; **2** CPU formulas and exact QA arithmetic are independently recomputed without treating those calculations as a valid controlled benchmark. |
| Testing | **2** root's Release TRX records 163 executed/passed and zero failed/skipped; **2** 18 new reviewer-authored state/geometry/saved-placement cases execute, in addition to the earlier reviewer's appearance regressions; **2** the latest complete UI process run passes 11 controls, 33 appearance assertions and 18 native integration assertions with no integration skips; **0** the final registry/input-freeze/lifecycle delta has not received the required native executable regression; **1** malformed storage, concurrency and modeled topology have coverage, but actual input, physical display and full accessibility scenarios remain incomplete. |
| Reliability | **2** explicit moves are captured before hiding and pending snapshots flush on disposal, with a real-window regression; **2** placement corruption, future schemas, bounded reads, failed-load guards and atomic save failures have tests; **2** owned shortcut release/reuse and click-through recovery/disposal have native evidence; **1** initialization/disposal ordering and shutdown error containment were hardened after review but final race/fault execution is pending; **1** missing-monitor fallback is exercised through an absent identifier, not an actual disconnect during dragging or a long-running lifecycle soak. |
| Security | **2** native operations act on owned companion HWNDs without general desktop automation; **2** placement/settings enforce bounded strict schemas and preserve unsupported documents; **2** click-through/hide require a recovery route and disposal clears the native transparent bit; **2** startup commands are quoted argument-free executables, explicit opt-in, and new test seams restrict writes to a GUID test namespace rather than the real Run key; **1** source review and prior permission tests do not establish adversarial cross-account access, all registry-denial paths or broader filesystem hardening. |
| Performance | **2** file sizes, work and saves are bounded/debounced; **2** real-window checks confirm motion stops in reduced-motion, hidden, minimized, disabled and sleeping states; **1** a repeatable counter harness exists, but its corrected input freeze and contamination checks remain unexecuted; **0** the only completed measurement was contaminated and cannot establish the controlled idle baseline; **1** resource targets and reference hardware are recorded, but their attainment, frame/startup percentiles, occlusion behavior and long-run stability remain unverified. |
| Maintainability | **2** state, geometry, storage and native recovery are separate readable modules; **2** strict build settings, package locks and layer checks remain in place; **2** named smoke assertions, durable artifacts and repeatable QA scripts aid diagnosis; **2** current behavior and future feature boundaries are documented, with failed/contaminated evidence retained; **1** final source checkpoint, remote CI and final executable verification remain pending. |
| UX/UI | **2** states have text labels and previews explicitly describe their nonfunctional nature; **2** named controls, a real movement automation peer and minimum-size settings bounds have programmatic evidence; **1** native recovery is proven through adapter calls, while actual tray/shortcut/drag input for this increment remains unverified; **1** reduced-motion/high-contrast policy and layout exist, but the physical scale/screen-reader matrix is incomplete; **1** keyboard movement works within a monitor by design, without a keyboard equivalent for transferring between monitors. |
| Requirement Compliance | **2** the planned avatar/desktop implementation is substantially present; **2** scope traceability and preservation of future work remain explicit; **0** the required physical mixed-DPI/disconnect matrix and controlled idle baseline are missing; **1** independent findings/fixes and this scored report are supplied, but final regression is blocked; **1** checkpoint documentation is being finalized without claiming the phase complete. |

### Execution evidence and attribution

The following executions were performed by the root implementation agent. The
independent reviewer verified the indicated retained artifacts rather than claiming
to have independently rerun these commands in this checkpoint.

| Command / evidence | Result and limits |
|---|---|
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1 -Configuration Release` | Root reports exit 0, strict build with 0 warnings/errors, 11/11 architecture checks, 163/163 tests and composed WPF smoke. Reviewer reread `artifacts/test-results/Release/tests.trx`: total/executed/passed 163; failed, notExecuted, errors and timeouts 0. This precedes the final registry seam and measurement-freeze changes. |
| Final Release compilation only | Root reports final build session 59502 exited 0 in 15.75 s with 0 warnings/errors, including the final app stopping guard and 13 new registry cases. Those cases compiled but did not execute. See `PHASE_2_BUILD_EVIDENCE.md` for the implementation agent's commands and environment record. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-ui.ps1 -Configuration Release` | Root reports exit 0 and 11/11 process controls. `artifacts/qa/desktop-ui-20260922-164631-f14197c6/profile/` records 33 appearance checks and 18 integration checks, with empty skipped/errors arrays and null failure. `ui-stages.txt` records two completed settings stages at 4.790 s and 1.984 s. These are whole smoke scenarios, not cold-launch percentile measurements. |
| Earlier UI run | `desktop-ui-20260922-164249-5578a5c9/profile/` contains 33 appearance passes but no completed integration report. Root reports the old 15-second supervisor deadline killed the run. It is retained as an incomplete/failed run; it is not combined with another run to fabricate a full pass. The successful rerun used a 30-second deadline and stage timings. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/measure-desktop.ps1 -Configuration Release` | The old script completed and returned 0, but the reviewer rejected the baseline after finding settings writes during measurement. See below. A process exit of 0 does not override contaminated evidence. |
| Independent source/artifact review and `git diff --check` | Read-only review completed; whitespace check exited 0, with normal Git line-ending notices. Final QA decimal and contaminated-run CPU arithmetic assertions exited 0. PowerShell parser check of the corrected measurement script exited 0 without launching an app; runtime verification remains pending. |

New reviewer-authored cases are in `IndependentPlacementOracleTests.cs` (4),
`IndependentAvatarStateTests.cs` (9 including data rows), and
`IndependentSavedPlacementTests.cs` (5 including data rows). The many internal grid
iterations are not counted as thousands of separately discovered tests. Existing
Phase 1 Debug results are historical and are not a Phase 2 Debug pass. No remote CI
run, actual sign-in startup, screen-reader conformance or physical display matrix is
claimed here.

Windows atomic replacement and current-user named-pipe tests previously required an
approved execution outside the restricted sandbox token. This is a test environment
constraint, not a reason to weaken ACL/atomic semantics or require application UAC.
The final requested outside-sandbox verification was rejected by automatic approval
review because of the reported usage quota. The root did not bypass that rejection.
Final native tests, clean measurement and actual input verification therefore remain
unexecuted at this checkpoint. A compilation-only check cannot replace them.

### Rejected performance evidence

Retained profile: `artifacts/qa/performance-20260922-164711-eccda553/`.
Reference hardware records AMD Ryzen 7 5700U, 16 logical processors,
7,928,467,456 bytes physical RAM, Windows build 26200, .NET 10.0.12 x64 and 120 DPI.
The harness used a ten-second warmup followed by three approximately sixty-second
intervals. Process CPU percentages were correctly derived from measured CPU and wall
time; machine-normalized CPU divides the one-core value by 16.

However, the application log records `SettingsSaved` at 20:48:05Z and 20:48:58Z while
the measurement was active (20:47:16Z–20:50:26Z). The saved scale/topmost values differ
from the original metadata. The reduced-motion interval also reports animation
observed enabled. These facts invalidate a controlled comparison.

For reproducibility only, the contaminated run reported one-core CPU of approximately
22.081%, 6.900% and 0.859% for visible-idle, reduced-motion and hidden; corresponding
machine percentages were 1.380%, 0.431% and 0.054%. These values are observations from
an interfered-with run, **not accepted idle performance or budget attainment**.

The corrected source labels the measurement, disables content/context-menu input
without disabling the window's animation, and refuses settings opening in this test
mode. The script rejects a preferences file in its initially empty profile, animation
in reduced/hidden intervals and mismatched visibility/state endpoints. No successful
rerun of these corrections has occurred. Tray cost, arbitrary occlusion, frame
percentiles, cold launch percentiles and long-run memory behavior are outside this
measurement and remain unverified.

### Findings addressed during review

- **Move then hide lost the pending position.** Save previously read only a visible,
  normal window, so hiding before the debounce timer also defeated exit flushing.
  The session now captures an immutable snapshot at the explicit move event and
  persists that snapshot while hidden. The 33-check artifact includes this regression.
- **Geometry success erased startup shortcut errors.** A separate live desktop-status
  area now retains those messages independently of movement instructions. Source
  reviewed; successful later retries should eventually clear only their own stale
  error, without clearing unrelated storage errors.
- **Initialization/disposal ordering was incomplete.** The session now tracks and
  drains initialization, checks stopped state after awaits, and avoids restoring after
  disposal. App shutdown contains desktop-disposal exceptions and checks stopping
  before continuing post-initialization work. Final executable regression is pending.
- **Performance interference was silently accepted.** The reviewer found real settings
  saves and a resumed animation in the reduced interval. Input freeze and explicit
  artifact rejection were added; the affected run remains rejected.
- **Registry testing needed a safe native seam.** A GUID-restricted test-key store and
  tests now cover enable/read/disable, unrelated-value preservation, read-only failure,
  wrong value kinds and missing keys in source. Empty/noncanonical GUID paths are
  rejected. These new cases have not yet executed; the user's real Run value is not
  used as a test fixture.

## TOP 5 RANKED FIXES / IMPROVEMENTS

1. **Problem:** The final registry, lifecycle and measurement corrections have not
   received the complete executable verification after automatic approval rejection.
   **Why it matters:** Earlier passing artifacts cannot prove later changes correct.
   **Proposed fix:** When approved execution becomes available, run the strict locked
   Release suite and affected native UI checks, retain actual discovered counts and
   failures, and re-review this exact source snapshot before any phase advancement.

2. **Problem:** Physical mixed-DPI, negative-coordinate and disconnected-monitor
   behavior remains unverified.
   **Why it matters:** Correct arithmetic and a missing identifier do not reproduce
   WPF DPI transitions, monitor removal, taskbar changes or capture loss while dragging.
   **Proposed fix:** Execute the roadmap matrix on suitable hardware or a dedicated
   test desktop, including 100/150/200% transitions, negative offsets, restart after
   removal, work-area changes and oversized windows. Record actual configurations and
   outcomes without changing the unrelated user's desktop preferences.

3. **Problem:** No controlled idle resource baseline has been accepted.
   **Why it matters:** The only run included settings interaction and animation in the
   reduced-motion interval; its measurements cannot substantiate a lightweight host.
   **Proposed fix:** Run the corrected isolated harness without interaction, verify
   interval flags and settings absence, report both CPU denominators and private bytes,
   compare with declared budgets, and investigate any breach. Add tray/minimized and
   longer-duration profiling where needed; measure percentiles separately.

4. **Problem:** Actual tray, hotkey, drag and keyboard accessibility interaction has
   not been established for this increment.
   **Why it matters:** Calling native adapter methods and inspecting peers does not
   prove the user can reach or invoke those actions, recover from click-through, or
   perceive focus and status changes.
   **Proposed fix:** In an isolated interactive session, exercise real shortcut input,
   tray Show/Sleep/Wake/Exit, drag threshold/cancel, keyboard movement, settings save,
   click-through recovery, focus order and screen-reader announcements. Retain scoped
   screenshots/observations and stop automation on concurrent user input.

5. **Problem:** Some desktop behaviors remain partial: arrows cannot move between
   monitors, arbitrary occlusion is not detected, and actual sign-in startup is untested.
   **Why it matters:** Keyboard parity, idle resource behavior and startup expectations
   need explicit supported limits rather than inference from native calls.
   **Proposed fix:** Add an accessible next-monitor action, define and profile occlusion
   handling, and verify opt-in/opt-out/sign-in and moved-executable behavior in a
   disposable Windows account. Keep current limitations documented until verified.

## Regression Risks:

- Never replace the pending normal-state placement with minimized/hidden coordinates;
  dispose must drain initialization and saves without reviving a closing window.
- Retain strict migration allowlists, future-schema preservation and distinct placement
  storage. External settings changes must not be silently overwritten.
- Preserve unique activity ownership: late disposal cannot cancel a newer activity,
  and waking cannot revive pre-sleep work. Future real jobs require explicit cancellation.
- Keep recovery available before enabling click-through or hiding; release all owned
  shortcuts and native style changes even when shutdown has an error.
- Measurement must assert the scenario it actually observed. Do not suppress animation
  by disabling the entire window and then call the run an animated-idle baseline.
- Keep registry fixtures within their validated GUID leaf and never substitute the
  actual user startup registration for a test fixture.

## Tests Required:

The ranked final regression, controlled performance and physical/input matrix above
remain required. Run newly added registry tests and the changed shutdown/measurement
paths once approved execution is available. Preserve the existing 163-test/native
evidence but do not relabel it as covering the final unexecuted delta. A later review
must record the new evidence and recompute the same explicit rubric; no assumed or
rounding-based pass is permitted.

## Final Verdict:

**FAIL, 8.0/10.** Substantial Phase 2 functionality and useful native/adversarial
evidence exist, but final regression, a controlled idle baseline and the required
physical/input coverage are incomplete. No unresolved critical source defect is
asserted solely from those evidence gaps; they nevertheless prevent this phase gate
from passing. Preserve this failed checkpoint and continue Phase 2 when the required
execution and test environment are available. Do not advance to Phase 3.
