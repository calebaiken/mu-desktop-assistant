# Testing

Use Windows 11 x64 and .NET SDK 10.0.401. All builds enable nullable checks, built-in
analyzers, deterministic output and warnings as errors. Production has no third-party
packages; MSTest TestFramework/TestAdapter 4.4.1 and Microsoft.NET.Test.Sdk 18.10.0 are
pinned test-only dependencies with lock files. NuGet vulnerability auditing remains on.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1 -Configuration Debug
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-negative.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-ui.ps1
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
`verify-ui.ps1` uses a 40-second per-process deadline for expanded real-HWND checks,
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

Latest completed Release verification has **193/193 tests**, 11 architecture checks,
composed smoke and zero warnings/errors. The suite predates the final cadence-only
change; its TRX is preserved at
`artifacts/qa/desktop-ui-20260922-175553-c176ac05/release-193-before-cadence-only-change.trx`.
The final cadence has subsequent strict-build and native evidence. The latest native artifact,
`artifacts/qa/desktop-ui-20260922-175553-c176ac05/profile`, has **12/12 process controls,
43 appearance assertions and 18 integration assertions**, with no skips or adapter
errors. Hidden/minimized settings reopening passes in both application runs. Timed
eyelid property transitions prove completed idle blinks; interrupted blinks do not
count. Idle owns no animation clocks, active expressions own at most two controlled
clocks capped at 20 FPS, and stopping releases timers/clocks. These behavior checks
cover the current ten-second blink cadence and 120 ms closure, using bounded 13-second
blink waits. The latest Release build has zero warnings/errors. The single clean
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

The final ten-second run, `performance-20260922-175657-e7f2743d`, also **fails**:
visible CPU is 1.041400%, reduced-motion 0.520719% and hidden 0.390566%. Reduced/hidden
CPU and all memory intervals pass; peak private bytes are 62,914,560 / 64,225,280 /
66,539,520, with 6/0/0 completed blinks and zero owned clocks. The supervisor exits 1
and preserves `budget-evaluation.json`. The cadence is frozen; no unchanged rerun,
threshold relaxation or overhead subtraction is used to manufacture a passing result.
The independent score is **8.6/10**, but phase exit remains **FAIL** because visible-idle
CPU and required physical display/input/accessibility evidence do not meet the gate.

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
supported native helper pipe was unavailable even after reinitialization; no actual
mouse/keyboard actions were attempted. The process smoke alone does not prove user input, screen
reader behavior, keyboard coverage, mixed DPI or compositor quality.

CI configuration in `.github/workflows/verify.yml` runs the positive and negative
scripts on Windows and uploads TRX. It has not run remotely until a repository is
published and the workflow executes. Visible interactive QA is local, separate from
headless CI. Do not equate a configuration file with a successful CI run.

## Dependency review

Selected Microsoft's maintained test stack, MIT-licensed upstream projects, compatible
with this .NET target, with test-only PrivateAssets and a dedicated source configuration.
Version sources: [MSTest framework](https://www.nuget.org/packages/MSTest.TestFramework/4.4.1),
[adapter](https://www.nuget.org/packages/MSTest.TestAdapter/4.4.1),
[test SDK](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/18.10.0).
Transitive versions/content hashes are recorded in packages.lock.json; review lock diffs.
Keep normal restore audit enabled; report network audit failures rather than suppressing
NU1900. This is not a guarantee of absence of unknown vulnerabilities.

## Future gates

Each owning phase adds unit, integration, UI, security, recovery and performance cases.
Use mock HTTP/SSE and real SQLite integration before provider/storage rollout; no live
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
profile. Keep failed-review history and specific commands/results. Native C++ additions
require sanitizer/resource checks; none exists yet. Repeat relevant tests after fixes,
not to manufacture evidence by rerunning unchanged successful suites.
