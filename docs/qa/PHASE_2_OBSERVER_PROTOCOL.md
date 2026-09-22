# Phase 2 observer-cost protocol

Recorded before execution of this experiment, following checkpoint `6306ec9`.
The historical 1.0413999329% visible-idle result remains a failure. This experiment
does not change a resource limit or subtract estimated measurement overhead.

## Question and fixed factors

Do the two in-process thread snapshots materially affect the measured idle CPU?
The production renderer remains a ten-second wait and 120 ms blink. One selected
Release build is used for every run; its five executable/project hashes must match.
Settings, native one-second process counters, sixty-second intervals, ten-second
warmup, input freeze, absent tray and pre-warmup counter calibration remain identical.
No concurrent application tests, builds, trace sessions or interactive companion
input are allowed during the sequence. External desktop activity is not controlled,
so two replicates cannot establish statistical significance or general certification.

The existing ten-second warmup may straddle the first completed blink and its first-use
cost. Thread diagnostics also have first-use cost because they are not prewarmed.
Both facts are preserved here. Changing warmup or calibration would require a separate
declared experiment and cannot silently be folded into this comparison.

## Conditions and sequence

- A: `scripts/measure-desktop.ps1 -Observer threads`; legacy timed boundary thread
  snapshots enabled. Each interval must record two nonempty snapshots, six attempts
  total. This remains the default observer.
- B: `scripts/measure-desktop.ps1 -Observer process`; only thread snapshots omitted.
  Each interval must record zero attempts and null thread snapshots. Native process
  counters and all their overhead remain inside the measured workload.
- Execute exactly **A, B, B, A**, sequentially, in four fresh profiles. Declare the
  sequence before reading results and preserve every valid pass or failure.
- A technical failure (missing/contaminated report, changed hashes or invalid scenario)
  makes the sequence incomplete. Stop and document it; do not silently substitute a run.

## Acceptance and reporting

Every measured interval must be strictly below **1% of one logical processor** and
**150,000,000 sampled private bytes**. Keep the exact CPU arithmetic and report all
three modes, actual completed blinks, policy/timer flags, zero idle clocks, observer
selection and snapshot-attempt counts. Existing visibility and contamination guards
remain mandatory. CPU includes all enabled harness overhead, with no subtraction.

Both B runs must pass all intervals to establish only that this process-counter-only
harness reproducibly passed in these two runs on this machine. Any B failure leaves
that conclusion failed. A/B differences are descriptive evidence about the observer
factor; do not claim an isolated product optimization or a confidence bound.
This does not validate tray/compositor cost, physical input, mixed DPI, sign-in or
long-running resource stability, and cannot alone approve Phase 2.
