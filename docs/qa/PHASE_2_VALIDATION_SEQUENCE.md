# Phase 2 validation sequence decision

Recorded 2026-09-22 during the final A run of the frozen observer comparison,
before its final result or the resource follow-up verdict.

The later [resource qualification sequence decision](PHASE_2_RESOURCE_SEQUENCE.md)
explicitly reconsiders the resource-pass timing after final-build measurement failed.
The conditions below preserve the earlier plan; they are not rewritten as if the
failed experiment passed. Physical/input obligations remain unchanged.

The user confirmed that no second monitor is available. The supported native
interaction helper also cannot discover windows because its native pipe is missing.
These constraints prevent the physical display and actual-input matrix from being
executed in this environment. They do not constitute passing test results.

The original request permits roadmap changes when architectural analysis supports
a better sequence, and requires an independent score strictly greater than 8.5
before advancing. Physical matrix timing was an additional requirement in our
roadmap. Independent QA reviewed the implemented source and available evidence and
found no known critical defect that makes conversation/provider development depend
on having additional monitor hardware. Delaying that independent work would not
improve physical validation.

## Decision

Move the unavailable physical and actual-input qualification from the Phase 2
development exit to explicit, mandatory Phase 11/12 pre-release gates. This changes
validation timing, not expected behavior, scoring evidence or the obligation to test.
Phase 2 may complete as a **development milestone** only after its automated and
programmatic native checks pass, all four frozen observer runs are valid, both
process-only B replicates pass every unchanged resource limit, and independent QA
scores the checkpoint strictly above 8.5. A development pass is not release approval.

Subsequent readiness review identified startup-placement and tray-recovery defects.
They require regression checks and the separately declared
[two-run final-build confirmation](PHASE_2_FINAL_BUILD_PROTOCOL.md) before completion.
The completed observer comparison remains evidence for its original binary only.

The observer protocol, build, measurement script, sequence, limits and overhead
accounting remain frozen. Prior failures, including the 8.0 and 8.6 failed phase exits
and the 1.0413999329% visible-idle CPU interval, remain historical evidence. No missing
physical test receives credit as passed, and partial testing/UX scores stay partial.

## Required before release

- Physical 100%, 150% and 200% mixed-DPI layouts; negative monitor coordinates;
  monitor disconnect/reconnect, persisted placement fallback and work-area changes.
- Actual drag/capture, keyboard movement and visible focus; tray actions, hotkey
  conflict/recovery and click-through delivery to another window.
- Screen reader, high contrast, reduced motion and settings reachability on small
  scaled work areas.
- Startup enable/disable across real Windows sign-in using a disposable test account.
- Broader performance qualification including tray/compositor costs, startup/frame
  percentiles and extended resource stability.

The [manual matrix](PHASE_2_MANUAL_MATRIX.md) retains each pending scenario. Physical
validation must use appropriate hardware and a working supported input mechanism;
programmatic checks and pure geometry tests cannot substitute for it. Startup and
click-through stay off by default. Phase 11/12 must not claim these checks complete
or approve a release while their required evidence is absent.
