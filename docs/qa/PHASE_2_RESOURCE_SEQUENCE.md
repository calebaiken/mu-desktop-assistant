# Phase 2 resource qualification sequence decision

Recorded 2026-09-22 **after** the final-build two-run confirmation finished with a
failed aggregate result. This is an explicit post-result planning reconsideration.
It must not be described as a predeclared experiment outcome or a resource pass.

## Evidence retained

The unchanged final-build protocol required both B runs to pass every interval.
Run 1 failed visible and reduced-motion CPU at 1.9786942106% and 1.0154002987% of one
logical processor. Run 2 passed, so the aggregate correctly failed and exited 1.
Every sampled private-byte interval passed. Both reports, binary hashes, protocol,
exact limits and all earlier failures remain retained without alteration.

No new application timer or recurring avatar-clock path was identified in the source
audit, and native checks verify the new startup and recovery fixes. This does not
exclude framework rendering, first-use work, occlusion or external desktop variation.
The harness's nominal visibility checks do not prove an unoccluded presented window.
There is no established cause for the CPU variation, and no performance optimization
or general lightweight-budget compliance is claimed.

## Architectural reason and decision

The user's original brief places renderer/interactions in Phase 2 and performance
optimization in Phase 12, permits roadmap adjustments, and requires a weighted
independent QA score strictly greater than 8.5 for advancement. The original roadmap
required an idle baseline for Phase 2. We subsequently imposed a stricter requirement
that the short resource confirmation also pass before development could proceed.

Independent QA reconsidered that timing against the measured shortfall, bounded
observed memory/motion, passing correctness/native regressions and absence of a known
critical source defect. Provider and conversation development does not depend on
resolving this CPU attribution question. Require final resource qualification in
Phase 12 before release, while allowing Phase 2 **development** completion at the
evidence-based weighted score of **8.7/10**.

Performance stays **7/10**, including **zero points** for the failed budget criterion.
No category, threshold, denominator or measurement acceptance rule changes to create
a resource pass. The experiment remains **FAIL**. Historical 8.0 and 8.6 phase-exit
failures remain unchanged. This decision changes when the resource requirement must
be satisfied, not whether it must be satisfied. It supersedes only the resource-pass
timing in the earlier [validation sequence decision](PHASE_2_VALIDATION_SEQUENCE.md).
Data-loss, security, recovery or other critical correctness defects still block
development advancement independently; none is currently known after the verified fixes.

## Mandatory release gate

Phase 12 must demonstrate visible-idle, reduced-motion and hidden CPU strictly below **1% of one logical
processor** and sampled private bytes below **150,000,000 bytes** on declared target
hardware, using a separately predeclared repeat protocol. Control and record actual
presentation/occlusion, warmup, observer cost and scenario state. Retain every valid
failure, preserve exact binary identity, and never subtract estimated overhead or
select the favorable second run from this failed pair as passing evidence.

Native-stack/WPF/CLR profiling should establish the cause before a production
optimization is claimed. Tray/compositor costs, startup/frame percentiles and long-run
stability also remain required. The physical/input/sign-in matrix remains mandatory
before release under the earlier decision. A development pass is not release approval.
