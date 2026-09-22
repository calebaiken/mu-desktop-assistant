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
reopen/relaunch, activate an existing instance and Exit. Record what actually ran in
`docs/qa/PHASE_1_REVIEW.md`; the process smoke alone does not prove user input, screen
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
