# Testing

Phase 3 verification covers bounded local SSE/context admission, owned worker lifetime,
SQLite history and branch recovery, separate temporary sessions, native Markdown/math
rendering and the compact reply preview. The first history regression passed 267/267
tests; that retained artifact predates later rendering/recovery/preview edits and is
not a pass claim for the final source. The [Phase 3 review](docs/qa/PHASE_3_HISTORY_REVIEW.md)
tracks executed artifacts, failed attempts and its final **9.5/10 development verdict**.
That result belongs to Phase 3; the Phase 4A speech increment independently passes at
**9.0/10** for manual output only. Earlier
224-test local-chat and compact-surface evidence keeps its original, narrower scope.

`scripts/verify-local-model.ps1` separately runs the
[frozen real-model protocol](docs/qa/PHASE_3_LOCAL_MODEL_PROTOCOL.md), retaining binary
identities, public fixture responses, errors, actual token-admission observations,
cancellation and worker cleanup/restart outcomes in a unique directory. It requires
`scripts/setup-local-ai.ps1` first. The expanded recorded run in
`artifacts/qa/local-model-20260922-214337-9c4ffcd3` reports protocol PASS; its small public
prompt set does not establish broad assistant quality, and manual content criteria
remain distinct from structural protocol checks.

`scripts/verify-ui.ps1` requires two native runs with 54 chat lifecycle, 50 history,
46 document and 34 reply-preview assertions alongside earlier desktop suites. These
are expected counts enforced by the current supervisor, not proof that a particular
binary completed them. Native method/property checks do not establish actual
keyboard/mouse or screen-reader usability. Renderer notation is not numerical validation.

Phase 4A passes **84 native fake-provider UI checks in each of two retained runs**
and **38/38 UI supervisor cases** in `desktop-ui-20260922-224213-17740e63`.
Its first expanded attempt exhausted the former 60-second
aggregate deadline after preceding suites consumed 52.516 seconds; that failed
attempt is retained. The passing attempt declared 120 seconds overall, added content-free
substage timing and retained all individual three-second waits. The UI checks cover
explicit start/stop, completed-reply eligibility, no autoplay, literal/Unicode-bounded
text, voice/rate selection, cancellation/late callbacks, native layout and accessible
names. They neither send audio nor establish physical input, hearing or device behavior.
The [frozen Phase 4A review protocol](docs/qa/PHASE_4A_SPEECH_REVIEW.md) governs required
native-engine, privacy, fault and lifecycle evidence; its **90/100** verdict approves
only this output slice. Full Phase 4 and release qualification remain unfinished.

Strict speech regression passes **305/305 tests**, **11/11 architecture checks**,
composed WPF smoke and zero warnings/errors. Native memory synthesis passed in
`speech-20260922-223108-36a69151`; explicit default-output Stop/reuse/deadline passed
in `speech-20260922-223856-66dd24ca`. Stop acknowledged in 0.0038051 seconds; the
30-second deadline ended at 30.0156752 seconds, with held worker exits confirmed.
The abrupt temporary-speech sentinel scan passed in
`private-speech-20260922-223824-cd48804a`. All paths are under `artifacts/qa`.
Physical hearing, full App Sleep/input integration, the exact sentinel's normal
exit scan and the frozen 60-second idle/ten-cycle protocol remain unexecuted.

`scripts/verify-speech.ps1` creates a unique isolated profile and retains binary/protocol
hashes and all outcomes. Its default diagnostic is **inaudible**, synthesizing public
fixtures into bounded memory and retaining metadata only. Explicitly adding
`-Playback` enables default-device audio for the public playback/Stop cases. Successful
engine events or PCM metadata do not prove a listener heard a start or stop; retain
actual observer notes separately. The supervisor's process deadline is 120 seconds;
the production per-operation and cleanup limits remain 30 and 3 seconds.

`scripts/verify-history-privacy.ps1 -Speech` starts a fresh profile, writes a real saved
SQLite decoy, then holds a fake explicit speech operation on a temporary public sentinel.
The app allows 10 seconds for readiness; the external supervisor allows 15 seconds for
the marker, terminates only its owned process and scans every bounded profile file
and sidecar for UTF-8/UTF-16LE/UTF-16BE sentinel bytes. This is whole-app voice UI
persistence evidence with no audio, not evidence about a third-party speech engine,
OS paging or forensic erasure. The existing history-only mode remains available.

Use Windows 11 x64 and .NET SDK 10.0.401. All builds enable nullable checks, built-in
analyzers, deterministic output and warnings as errors. Core and Application remain
package-free. Infrastructure owns Microsoft.Data.Sqlite and System.Speech 10.0.12; Desktop owns Markdig
1.3.2 and WpfMath 2.1.0. Their exact resolved transitives and content hashes are locked.
MSTest TestFramework/TestAdapter 4.4.1 and Microsoft.NET.Test.Sdk 18.10.0 remain pinned
test-only dependencies. See the [complete dependency inventory](docs/DEPENDENCIES.md).
NuGet vulnerability auditing remains on.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1 -Configuration Debug
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-negative.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-ui.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-speech.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-history-privacy.ps1 -Speech
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/measure-desktop.ps1
```

Run negative/UI scripts after the matching configuration has built. `verify.ps1`:

1. Restores locked dependencies from the reviewed NuGet.org feed (network needed for
   first restore or refreshed vulnerability data; an unavailable audit fails clearly).
2. Builds all six projects with zero warnings required.
3. Executes 11 architecture invariants (layer graph, solution, strict settings, SDK,
   manifest, planning documents, reviewed feed, ignored sensitive artifacts).
4. Runs **discoverable MSTest** via `dotnet test`, requires at least one executed test,
   and writes machine-readable TRX under `artifacts/test-results/<configuration>`.
5. Launches the real composed application with `--smoke` and an isolated data directory;
   exercises settings/logs/instance lock/XAML layout/cancellation and clean shutdown.

SDK state stays in `.dotnet-home`, packages in `.packages`, and evidence in `artifacts`;
all are ignored. Build servers/shared compilation and parallel restore are disabled to
avoid the restricted-environment failures observed in Phase 0/1. Test-platform telemetry
is opted out. The child PowerShell execution policy does not change machine settings.

The Codex restricted token denies some Windows File.Replace metadata operations and
current-user named-pipe access. Run these integration tests under a normal user token;
an approved outside-sandbox execution was needed here. Do not weaken atomic writes or
pipe ACLs to make restricted-token tests pass. The application requires no UAC elevation.

## Current behavioral coverage

Phase 2 adds independent integer-lattice placement and rational-coordinate oracles,
state priority/lease/sleep/concurrency checks, appearance migration/ranges, placement
schema/recovery/conflict/cancellation tests and startup command validation. The latest
13 registry integration cases use only validated unique HKCU test keys and all pass.
Startup tests never write the user's Run value or simulate a Windows sign-in.
`verify-ui.ps1` uses a 60-second per-process deadline for expanded real-HWND checks,
including eight explicitly labeled previews, native capture/restore/fallback, pending
move–Hide–Exit persistence and native click-through/tray/hotkey conflict/release.
It retains stage timings and named assertions. Shortcut subchecks report a limitation
if the user's applications already occupy all presets; a skipped case is not a pass.
Monitor-cycle policy is independently tested; Ctrl+Shift+M and the next-display action
share two-step native placement. The latest native run observed one display at 120 DPI,
so its next-display assertion verifies a no-op, not physical cross-display movement.

`measure-desktop.ps1` uses an isolated profile and opt-in `--performance-smoke`: 10 s
warmup, then 60 s each of visible animation, reduced motion and hidden state. It
records actual animation flags, process CPU and private/working-set bytes through native
counters, OS/runtime, DPI and reference hardware. The supervisor independently recomputes
CPU percentages. Limits are strictly below 1% of one logical processor over 60 seconds
and 150,000,000 private bytes; machine-normalized CPU is informational. Calibration is
never subtracted. The supervisor retains raw artifacts and fails on a breached budget.
No forced GC, OS preference change or tray is involved. This is a process-counter
baseline, not frame-time, compositor/GPU, startup-percentile or long-run leak evidence.

The retained final Phase 2 Release verification has **196/196 tests**, 11 architecture checks,
composed smoke and zero warnings/errors, now including the final-source readiness
fixes. The matching TRX is archived at
`artifacts/qa/desktop-ui-20260922-185538-0c91126a/release-196-before-final-build-confirmation.trx`.
Three added independent placement tests verify a
cancelled queued waiter does not cancel its owner or poison later saves, cancellation
after atomic commit reports success, and a failed writer preserves the known-good
baseline for retry. The earlier pre-observer 196-case TRX is preserved at
`artifacts/qa/desktop-ui-20260922-183217-1d198cb8/release-196-before-observer-abba.trx`;
the historical 193-case artifact remains retained with its original scope.
The corresponding Phase 2 native artifact, `artifacts/qa/desktop-ui-20260922-185538-0c91126a/profile`,
has **18/18 process controls, 43 appearance, 24 integration, 29 settings lifecycle
and 49 placement-initialization assertions**, with no skips or reported failures. Controlled pending
saves cover committed, cancelled and I/O-failed completion during shutdown: committed
data remains durable, the companion is not reapplied, native effects/status changes
do not run late, and controls remain disabled. Hidden/minimized settings reopening
also passes in both application runs. Timed
eyelid property transitions prove completed idle blinks; interrupted blinks do not
count. Idle owns no animation clocks, active expressions own at most two controlled
clocks capped at 20 FPS, and stopping releases timers/clocks. These behavior checks
cover the current ten-second blink cadence and 120 ms closure, using bounded 13-second
blink waits. That Release build has zero warnings/errors. The single clean
resource run for this cadence completed with the unchanged limits and failed visible CPU.

Both controlled resource runs of the earlier renderer, `performance-20260922-172039-f052a9fb` and
`performance-20260922-172825-fc26bc57`, pass memory and fail all CPU intervals. The latter
records 5.260228% visible, 2.708053% reduced-motion and 2.707961% hidden one-core CPU.
Peak private bytes are 65,101,824 / 65,290,240 / 62,021,632 respectively. The original
`performance-20260922-164711-eccda553` remains rejected because interactive settings
changes contaminated its intervals. Diagnostic traces include profiling overhead and
cannot replace a clean budget run. See the [independent follow-up review](docs/qa/PHASE_2_FOLLOWUP_REVIEW.md).

The six-second-blink run, `performance-20260922-175003-37870412`, is a valid **failure**:
visible CPU is 1.093335%, reduced-motion 0.494627% and hidden 0.416623%. Reduced/hidden
CPU and all memory intervals pass; peak private bytes are 63,504,384 / 65,265,664 /
63,492,096. Actual completed blinks are 10/0/0 with zero owned animation clocks.
Combined blink/lifetime changes improved the reduced/hidden floor, but a single cause
is not established. No overhead is subtracted. The ten-second production decision was
recorded before its single measurement, preserving this result and earlier failures.

The pre-experiment ten-second run, `performance-20260922-175657-e7f2743d`, **fails**:
visible CPU is 1.041400%, reduced-motion 0.520719% and hidden 0.390566%. Reduced/hidden
CPU and all memory intervals pass; peak private bytes are 62,914,560 / 64,225,280 /
66,539,520, with 6/0/0 completed blinks and zero owned clocks. The supervisor exits 1
and preserves `budget-evaluation.json`. The cadence is frozen; no unchanged rerun,
threshold relaxation or overhead subtraction is used to manufacture a passing result.
The resulting historical review scored **8.6/10** with a failed phase exit. That
report and its resource failure remain unchanged by subsequent experiments.

The [observer protocol](docs/qa/PHASE_2_OBSERVER_PROTOCOL.md) declares exactly **A/B/B/A**
before execution: A is `measure-desktop.ps1 -Observer threads` (the legacy default),
with two nonempty boundary thread snapshots per interval; B is `-Observer process`,
which omits only those snapshots. Both use the same ten-second blink cadence, warmup,
sixty-second intervals, native one-second counters, input freeze and calibration.
No calibration or observer cost is subtracted. `measure-observer-comparison.ps1`
requires the explicitly reviewed `-ExpectedProtocolSha256`, freezes five binary and
protocol/supervisor hashes, uses four fresh profiles, and retains complete reports
and budget results. Technical failures stop without substitution; budget failures
remain valid results and never trigger retries. Acceptance requires both B runs to
pass all intervals after all four valid runs complete. All A/B results must be
reported, without claiming statistical significance or a product optimization.
The completed comparison is
`artifacts/qa/observer-d217a835e8bc4ec9b6f27b98637024fd/comparison.json`: all four runs
are valid and all CPU/memory intervals pass. Visible one-core CPU is 0.885254% / 0.989278%
for A and 0.755076% / 0.703079% for B. These results belong to the recorded earlier binary;
they do not establish an isolated product optimization or statistical significance.

The [final-build protocol](docs/qa/PHASE_2_FINAL_BUILD_PROTOCOL.md) declares exactly
two B runs after startup-placement and confirmed-shortcut recovery fixes. Both must
pass every unchanged limit with frozen binary/protocol/script hashes and no overhead
subtraction. Use the comparison supervisor's `-FinalBuild` mode with the reviewed
protocol SHA256. Technical failures stop without substitution; valid budget failures
remain retained. The final-source strict build, 196-test suite and 18/43/24/29/49 native
checks pass. Final measurements are retained at
`artifacts/qa/final-build-1ca3041a363849aabc6b2c4e2170ce6f/comparison.json` and the aggregate
result is **FAIL**: both valid B runs had to pass, but only the second does.

| Final-build B run | Visible CPU | Reduced-motion CPU | Hidden CPU | Peak private bytes, corresponding modes | Result |
|---|---:|---:|---:|---|---|
| `performance-20260922-185649-ab4dbf46` | 1.978694% | 1.015400% | 0.364563% | 64,974,848 / 65,224,704 / 62,337,024 | Visible/reduced CPU FAIL; hidden CPU and all memory PASS |
| `performance-20260922-190004-0f75069d` | 0.755208% | 0.598859% | 0.364527% | 63,119,360 / 64,102,400 / 63,520,768 | All intervals PASS |

Each records 6/0/0 completed blinks and zero owned clocks. The first failure, exact
source/binary attribution and aggregate rule remain unchanged; no substitute run,
threshold relaxation or overhead subtraction follows. The independent development
review passes at **8.7/10** under the explicitly post-result
[resource sequence decision](docs/qa/PHASE_2_RESOURCE_SEQUENCE.md). Performance stays
**7/10 with zero points for the failed budget criterion**; this experiment remains FAIL.

The first new fixture build had a compile error, now corrected. Native attempt
`desktop-ui-20260922-185315-2646a9cb` completed 43/24/29 checks, then failed placement
assertion 42 because it compared size after warning text resized the window. The
fixture now compares Left/Top; that correction changed no production code. The failed
artifact remains retained; the subsequent full native run passes. No concurrent builds,
app tests, traces or companion input are allowed during frozen resource sequences.

The [validation sequence decision](docs/qa/PHASE_2_VALIDATION_SEQUENCE.md) moves the
unavailable physical display/input/accessibility and actual sign-in matrix to mandatory
Phase 11/12 pre-release qualification. The user has no second monitor and the supported
native helper remains unavailable. These cases remain unexecuted, receive no passing
credit, and must be completed before release. Phase 2 development completion does not
approve release. Phase 12 must use a separately predeclared repeat protocol with actual
presentation/occlusion, warmup, observer cost and scenario state recorded. The unchanged
limits are strictly below 1% of one logical processor and 150,000,000 sampled private
bytes; retain every valid failure and exact binary identity without overhead subtraction.

- Settings explicit-save/round trip, malformed/duplicate/unknown/wrong-type schema,
  unsupported versions, oversized files, quarantine, valid backups and external
  corruption, failed-load authorization, concurrency, cancellation and invalid names.
- Real atomic replacement failure through a locked target; injected disk-full writer
  failure. The latter is a **simulation**, not an actual full-disk experiment.
- Typed, content-free structured event shape, distinct channels, diagnostics opt-in,
  bounded rotation, oldest-record retention, disabled-family cleanup, malformed first
  records/unterminated tails and actual storage errors. Interior records are not scanned.
- Instance collision, activation over a current-user pipe, cancellation, isolated
  profiles, resource release and lock reacquisition.
- Independent negative/control checks verify real failure exit codes and diagnostics.
- `verify-ui.ps1` opens real companion/settings HWNDs, closes/restarts them, checks
  invalid path/flag inputs and inspects lifecycle logs in an isolated profile.

The interactive QA scenario uses the supported computer-use tools to change/save a name,
reopen/relaunch, activate an existing instance and Exit. Phase 1's completed input
evidence remains in `docs/qa/PHASE_1_REVIEW.md`. During resumed Phase 2 validation, the
supported native helper pipe remained unavailable (OS error 2) after updated skill
guidance and a fresh session reset; no actual
mouse/keyboard actions were attempted. The process smoke alone does not prove user input, screen
reader behavior, keyboard coverage, mixed DPI or compositor quality.

CI configuration in `.github/workflows/verify.yml` runs the positive and negative
scripts on Windows and uploads TRX. It has not run remotely until a repository is
published and the workflow executes. Visible interactive QA is local, separate from
headless CI. Do not equate a configuration file with a successful CI run.

## Dependency review

The runtime package allowlist is limited to SQLite and System.Speech in Infrastructure
and native rich rendering in Desktop. Transitive package versions, package-supplied repository commits,
declared licenses and WpfMath font notices are recorded in
[docs/DEPENDENCIES.md](docs/DEPENDENCIES.md). Production has no browser-rendering or
hosted-provider package. Core/Application dependency rules still prohibit package references.

The test stack uses Microsoft's MIT-licensed projects, compatible with this .NET target,
with test-only PrivateAssets and a dedicated source configuration.
Version sources: [MSTest framework](https://www.nuget.org/packages/MSTest.TestFramework/4.4.1),
[adapter](https://www.nuget.org/packages/MSTest.TestAdapter/4.4.1),
[test SDK](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/18.10.0).
Transitive versions/content hashes are recorded in packages.lock.json; review lock diffs.
The 2026-09-22 `dotnet list Companion.slnx package --vulnerable --include-transitive`
check completed across all six projects without a reported known-vulnerable package.
The post-speech-package approved run is retained in
`artifacts/qa/phase4a-package-advisories.txt`; the first restricted invocation exited 1
without output. It was not a passing check, and the advisory scan was not rerun for
these documentation edits.
This is a query of the configured NuGet advisory feed, not a source audit, exploitability
assessment or scan of the separately downloaded model/native worker. Keep normal
restore audit enabled; report network audit failures rather than suppressing NU1900.
It does not establish absence of unknown vulnerabilities.

## Future gates

Each owning phase adds unit, integration, UI, security, recovery and performance cases.
Extend the existing mock HTTP/SSE and real SQLite integration cases with each worker/storage change; no live
paid API tests without explicit configuration. Voice requires PCM plus physical-device
fixtures. Tools require schema/permission/replay/TOCTOU/cancellation/audit tests. Temporary
mode requires sentinel scans of DB/WAL/log/cache after crash and restart. See ROADMAP.md.

Scientific code must use independent oracles, stated equations/domains/units, near-zero
absolute and relative tolerances, nonfinite and boundary tests. First-order uncertainty
uses variance(f) approximately J Σ Jᵀ, with covariance, differentiability and small-error
assumptions documented; compare nonlinear cases independently. Affine temperatures
must distinguish values from intervals. Model text never supplies the numerical oracle.
No scientific calculation is implemented or verified yet. QA arithmetic uses exact
decimals and the strict score >8.5 condition; 8.5 fails.

Tests use isolated temporary directories with guarded cleanup, never the user's actual
profile. Keep failed-review history and specific commands/results. Repository-authored
native C++ additions require sanitizer/resource checks; there are none yet. The external
llama.cpp worker has its separately pinned installation and runtime protocol checks.
Repeat relevant tests after fixes,
not to manufacture evidence by rerunning unchanged successful suites.
