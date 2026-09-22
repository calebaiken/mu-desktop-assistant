# QA REVIEW

Finalization note (2026-09-22): see [PHASE_1_FINALIZATION.md](PHASE_1_FINALIZATION.md)
for synchronized status, source checkpoint and later artifact observations. This
dated review's original scoring and evidence attribution are preserved below.

Phase: 1 — application composition, lifetime, settings, logging and basic companion UI

Overall Score: 9.0 / 10

Result: PASS

Reviewer: independent `phase0_qa` agent, continuing as the Phase 1 reviewer,
2026-09-21. The reviewer inspected development concurrently, supplied adversarial
findings, wrote seven settings regression cases and the independent UI process script,
and independently executed verification. Production code was implemented by the root
agent. Interactive Computer Use observations below are explicitly attributed to that
agent, with persisted evidence independently inspected afterward.

Category Scores:
- Code Correctness: 9 / 10
- Architecture: 10 / 10
- Mathematical Correctness: 9 / 10
- Testing: 9 / 10
- Reliability: 9 / 10
- Security: 9 / 10
- Performance: 8 / 10
- Maintainability: 9 / 10
- UX/UI: 9 / 10
- Requirement Compliance: 9 / 10

## Evidence:

### Scoring and scope

Five criteria per category are scored 0 (missing), 1 (partially verified), or 2
(satisfied by phase-relevant evidence), with equal category weights. The score is:

`(9 + 10 + 9 + 9 + 9 + 9 + 8 + 9 + 9 + 9) / 10 = 90 / 10 = 9.0`.

The reviewer independently executed decimal arithmetic assertions for that sum and
verified the strict boundary: 8.5 fails; 8.6 passes. Those assertions exited 0.
No critical unresolved defect was found in the implemented Phase 1 scope after fixes.

This is a basic static companion and working local foundation. Animation, drag/snap,
tray/hotkey/startup integration, advanced monitor behavior, conversation, providers,
voice, memory, tools, scientific computation and packaging are not completed by this
phase. The score does not certify those features, accessibility conformance, measured
performance targets or production release readiness.

| Category | Five criteria and evidence |
|---|---|
| Code Correctness | **2** strict Release build reported by implementation and independently strict Debug build, both zero warnings/errors; **2** real persistence/logging/instance behavior passes 38 discovered tests in independent Release and Debug runs; **2** malformed schemas, invalid arguments, prohibited references and failure exit codes have executable negative evidence; **2** composed startup, real HWND creation, settings-window close and restart pass independent process checks; **1** rare postcommit cancellation and shutdown-timeout interleavings are source-reviewed but not exhaustively driven through the UI. |
| Architecture | **2** all four production layer boundaries pass executable checks; **2** domain settings and Application settings/event ports remain separate from concrete persistence and WPF; **2** Desktop composition owns startup, services, cancellation and shutdown; **2** profile/session instance ownership and bounded same-user activation IPC are distinct from future tool authority; **2** current implementation and deferred provider/avatar/memory/tool contracts are clearly separated in architecture, README and roadmap. |
| Mathematical Correctness | **2** score arithmetic and strict threshold independently asserted with decimals; **2** byte/file limits are checked against actual filesystem sizes/counts by rotation tests; **2** dated retention fixtures use an independent expected timeline (January 1 plus 15 days is January 16; January 1 is then beyond a 14-day bound), including disabled-channel expiry; **2** no scientific implementation or LLM numerical verification is falsely claimed, and future independent-oracle requirements remain documented; **1** exact retention-boundary, long-range clock-change and exhaustive numerical-limit matrices remain incomplete. |
| Testing | **2** standard MSTest discovery executes 38 tests with zero skipped and durable TRX; **2** seven reviewer-authored settings cases exercise actual public services and real files; **2** independent negative and UI process scripts each pass 9/9 controls; **2** real Windows failure, corruption, concurrency, cancellation and instance-release cases run, alongside attributed interactive evidence; **1** remote CI has not executed and user-input UI regression automation/coverage metrics remain incomplete. |
| Reliability | **2** real atomic replacement and failure cleanup are tested; **2** known-good backup recovery, externally introduced corruption, failed-load guards and unsupported schemas are tested; **2** log rotation, corrupt/torn-tail recovery and retention pass; **2** real IPC activation, lock reacquisition, composed shutdown and restart pass; **1** physical disk exhaustion, power loss, crash-at-every-commit-point and prolonged shutdown-fault tests remain unperformed. |
| Security | **2** typed enum/UUID/duration log payload excludes arbitrary content, with schema tests and an independent synthetic-name log scan; **2** strict bounded settings parsing rejects unknown/duplicate/wrong-type input and protects future schemas; **2** `asInvoker`, no UIAccess, same-user pipe controls and no generic execution payload are present; **2** UNC normalization and malformed command-line rejection are tested without contacting remote shares; **1** adversarial cross-account IPC and broader same-user filesystem hardening are not established by these tests. |
| Performance | **2** settings reads, log prefix reads, log rotation and IPC requests are bounded; **2** file operations run off the UI thread and saves are serialized; **2** the static shell has no continuous render/poll/capture/provider loop; **2** cancellation/deadlines, process exit and instance-lock release have implementation and execution evidence; **0** no reproducible CPU/private-bytes/startup/render-latency profiling baseline was collected. |
| Maintainability | **2** coherent small modules and explicit ports/dependencies; **2** pinned test packages/lock files, analyzers and warnings-as-errors without added production packages; **2** repeatable verification scripts, TRX and a Windows CI definition; **2** current behavior, retention limits, environment constraints and future scope are documented; **1** no legitimate local Git identity/authored commit exists and remote CI history is not available. |
| UX/UI | **2** root agent observed and used named Settings/editor/Save/Close/Exit controls through supported Computer Use; **2** a saved name changed the actual companion and survived a full restart; **2** first Exit stopped its process, and secondary launch left a single companion; **2** root agent observed readable/unclipped windows, while source provides native controls, explicit save and recovery/conflict messages; **1** comprehensive keyboard, screen-reader, high-contrast, scaling and monitor coverage remains absent. |
| Requirement Compliance | **2** planned composition/lifetime/settings/logging/basic transparent companion are implemented; **2** required documentation and current/future scope are maintained; **2** independently discovered defects received fixes and regression evidence without weakening security for the sandbox; **2** required independent report/categories/top five/strict gate are supplied; **1** final source/status synchronization follows review and a normal author-attributed checkpoint still awaits a real Git identity. |

### Independently executed commands and artifacts

Environment: Windows 11 x64, SDK 10.0.401, WindowsDesktop 10.0.12. Tests requiring
atomic file replacement and current-user named pipes ran through approved execution
outside the restricted sandbox token. This does not change the application's manifest
or impose an administrator/UAC requirement.

| Command | Actual result |
|---|---|
| `dotnet test tests/Companion.Tests/Companion.Tests.csproj --configuration Release --no-build --no-restore --logger 'trx;LogFileName=independent-tests.trx' --results-directory artifacts/test-results/IndependentRelease` | Exit 0; 38 passed, 0 failed, 0 skipped. SDK/test telemetry opt-out and workspace CLI home were set for this run. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1 -Configuration Debug` | Exit 0; locked restore; six projects; 0 compiler warnings/errors; 11/11 architecture assertions; 38/38 MSTest; real composed XAML/startup/shutdown smoke. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-negative.ps1 -Configuration Release` | Exit 0; 9/9 controls covering valid/restored fixtures, invalid invocation/root, forbidden reference, empty required document, malformed XML and invalid/extra app flags. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-ui.ps1 -Configuration Release` | Exit 0; 9/9 process/control checks, including real companion/settings HWND creation and close twice on the same isolated profile, composed headless lifecycle, both UNC slash forms, relative/conflicting/duplicate flags and actual lifecycle-log schema. |
| Decimal score/boundary assertions | Exit 0; 90/10 = 9.0, with 8.5 rejected and 8.6 accepted. |
| `git diff --check` | Exit 0, no whitespace errors; Git emitted expected line-ending normalization notices. |

Evidence retained in ignored local artifact directories:

- `artifacts/test-results/IndependentRelease/independent-tests.trx`
- `artifacts/test-results/Debug/tests.trx`
- `artifacts/qa/negative-20260921-211614-963f1d65/`
- `artifacts/qa/phase1-ui-20260921-120902-717082dc/profile/`
- `artifacts/qa/interactive-1408d25f186b436597dc9a68a71d420e/`

The reviewer reread both TRX summaries: each records total/executed/passed = 38,
failed = 0. Breakdown: 17 settings cases, 11 logging cases, 3 lifetime cases and
7 independently authored settings cases. Counts include data-driven cases, not a
claimed coverage percentage. No test was disabled to reach this result.

### Interactive evidence supplied by the implementation agent

The root agent used the supported Computer Use `sky` APIs on the isolated interactive
profile above. These were user-interface observations, not actions performed by the
independent reviewer:

1. Inspected actual companion/settings windows and accessibility names. The observed
   initial name was `Mu`; external/user interaction occurred in this test window.
2. Set the editor to synthetic name `Pip QA`, clicked Save, observed `Saved.`, clicked
   Close, and observed `Pip QA` rendered on the companion.
3. Launched a second process using the same profile: it exited 0 and the window list
   contained exactly one companion.
4. Clicked Exit and confirmed first process PID 208868 was absent. A Stopping event
   was recorded.
5. Relaunched with the same profile (PID 209288). Actual companion and reopened editor
   both showed `Pip QA`, proving the completed save/restart path.
6. During an attempt to restore the initial `Mu` value, user interaction invalidated
   subsequent action geometry. The Computer Use tool explicitly reported user input;
   automation stopped. **A second automated Exit was not completed or claimed.**

The independent reviewer then inspected the profile on disk without manipulating its
windows: application logs contained two Started events and one Stopping event; the
saved primary name was `Mu` and its valid backup name was `Pip QA`. Seven SettingsSaved
events reflect all interactions in this profile and must not be attributed exclusively
to automation. The reviewer independently checked that `Pip QA` was absent from the
application log. The second test instance was intentionally left to the user after
input was detected; no real/default user profile was used by these tests.

### Defects identified and resolved before this review

- Non-number JSON schema values previously threw rather than entering corrupt-settings
  recovery. Value-kind validation and five independent malformed-type cases now pass.
- Cached primary validity could replace a good backup with external corruption.
  Backups now come from validated in-memory state, with primary revalidation/conflict
  detection; the independent corruption-between-load-and-save case passes.
- Failed backup reads could leave saving authorized. Load state now publishes only
  after successful completion; the real locked-backup regression passes.
- Quarantine originally copied an arbitrarily large invalid file. It now uses atomic
  replacement into bounded-count quarantine, with oversized-file evidence.
- Retention based on last write could retain old records indefinitely. The oldest
  event now controls expiry and all known families are swept on actual writes.
- Empty/malformed initial log records and unterminated tails could block startup.
  Bounded inspection/quarantine and visible recovery now pass six corruption fixtures.
- Path handling lost drive-root semantics and checked UNC before normalization.
  Root-preserving normalization and post-normalization UNC rejection were reviewed;
  both UNC spelling variants now exit 2 in real process tests.
- Cancellation of optional logging after settings commit could falsely report a
  cancelled save. Committed settings now produce a logging-warning outcome instead.
- Expected external settings conflicts now produce a fixed restart message rather
  than an unhandled-exception exit.

Log recovery does not validate every complete interior/final JSON record. It validates
the initial record and detects an unterminated tail; no broader integrity guarantee is
claimed. Recovery quarantine keeps one latest file per channel, and lazy expiry may
discard recent entries sharing a file with expired ones, as documented.

### Environment failures and interruptions retained

The implementation agent reported 10 failures under the restricted token, involving
real Windows atomic replacement/current-user pipe permissions. The same suite passed
outside that token, including the independent runs above. The controls were preserved.
Restricted-network restore also reported NU1900 when vulnerability data was unreachable;
an approved network-capable restore passed with auditing still enabled.

An independent interactive launch request was rejected by automatic approval review
because a service usage limit had been reached. That call did not launch the app, and
the reviewer did not bypass the rejection. After the user resumed work, the root agent
completed the attributed interactive checks. That interrupted launch is not counted as
test evidence. Current automated results are actual executed passes, not predictions.

## TOP 5 RANKED FIXES / IMPROVEMENTS

1. **Make user-input UI regressions repeatable before expanding the shell.**
   Problem: Named controls/save/restart/Exit were observed interactively, but the durable
   automated UI script only exercises program-driven HWND lifecycle and command inputs.
   Why it matters: Phase 2 can regress focus, save feedback, recovery and window closure
   even while service tests pass.
   Proposed fix: Add a maintained UI test path for editor validation, explicit save,
   reopen/restart, keyboard activation and accessible names; extend with high contrast,
   scaling and monitor cases in their owning phase. Keep interactive and CI evidence distinct.

2. **Extend interruption and storage-failure coverage.**
   Problem: Disk-full is injected and atomic sharing violations are real, but physical
   exhaustion, process termination at commit boundaries and shutdown-timeout races were
   not exercised comprehensively.
   Why it matters: Persistent preferences and clean lifetime behavior are foundational.
   Proposed fix: Add isolated interruption/fault tests before and after backup/primary
   commit, during optional postcommit logging, and during stalled/disconnecting IPC.
   Verify reported outcomes, recoverability and handle release without real user data.

3. **Measure startup and idle performance with a declared workload.**
   Problem: No actual resource/latency profile exists; bounded implementation alone does
   not demonstrate the planned budgets.
   Why it matters: Transparent rendering and later animation can affect desktop usability.
   Proposed fix: Record reference hardware, Release startup, idle CPU/private bytes,
   cancellation/shutdown latency and long-running resource trends in Phase 2; retain
   trace artifacts. Do not substitute test/build duration for application performance.

4. **Strengthen log-integrity diagnostics as the application grows.**
   Problem: Current recovery detects malformed first records and unterminated tails,
   not arbitrary complete-record corruption; quarantine keeps only the latest damaged file.
   Why it matters: Richer audit requirements will need clearer integrity and retention
   guarantees than the present content-free application logs.
   Proposed fix: Define bounded complete-record/integrity validation and explicit audit
   recovery semantics before consequential tools ship, preserving current content exclusion
   and avoiding unbounded scans or silent claims of complete audit history.

5. **Complete normal source and remote verification history when available.**
   Problem: Local Git identity is still unconfigured; CI is a definition without a remote
   run, and phase source finalization follows this report.
   Why it matters: Authored checkpoints and actual CI results improve reproducibility,
   review and rollback as phases accumulate.
   Proposed fix: Retain the reviewed identity-free Phase 1 tree now; use legitimate user
   identity for a conventional commit when supplied, and execute/review the Windows
   workflow when a remote is established. Do not fabricate either kind of evidence.

## Regression Risks:

- Changing backup ordering, commit reporting or semaphore ownership can reintroduce
  corruption or misleading cancellation outcomes.
- Simplifying current-user pipe security to accommodate a sandbox would weaken the
  product boundary and invalidate this review.
- New settings/log payload fields can accidentally introduce secrets or unbounded data.
- WPF layout/lifetime checks do not cover all user-input, accessibility or monitor behavior.
- Package/SDK changes require lock/audit review and genuine test execution.
- Leaving a user-controlled test instance open can lock its executable during rebuild;
  respect the user's control rather than forcibly closing it after input is detected.

## Tests Required:

The current Phase 1 gate is supported by the executed tests and explicitly attributed
interactive evidence. No further broad rerun is required without relevant changes.
Changes to reviewed code/scripts require targeted regression checks. Final documentation
and source checkpoint synchronization need a consistency check. The ranked improvements
and roadmap govern subsequent testing; screen-reader, mixed-DPI, long-duration resource,
physical disk-full, power-loss, remote CI and all Phase 2+ feature tests are not claimed
as completed.

## Final Verdict:

**PASS for Phase 1: 9.0 > 8.5.** The implemented desktop foundation meets its scoped
gate with independently verified behavior and retained limitations. This is the first
complete scored Phase 1 review; preliminary findings and environment failures are
preserved above. Finalize the reviewed source/status, then require a new independent
review for the next phase. Do not reinterpret this pass as a completed AI companion.
