# Phase 2 final-build resource confirmation

Declared before building or measuring the startup-placement and confirmed-recovery
fixes. The earlier frozen A/B/B/A experiment completed all four valid runs and both
B replicates passed. Its report and exact binary identity remain retained at
`artifacts/qa/observer-d217a835e8bc4ec9b6f27b98637024fd/comparison.json`.
That report cannot be relabeled as execution of a later binary.

Independent readiness review found two additional bugs: input during asynchronous
placement load could be overwritten by restored placement, and requested tray
visibility did not confirm a recovery route. Fix and regress those issues, then
freeze the resulting build for exactly two sequential **B, B** runs. This is final
binary confirmation, not another observer-effect experiment or replacement of any
earlier result. The renderer, observer implementation and resource limits stay fixed.

Use `scripts/measure-observer-comparison.ps1 -FinalBuild` with this document's
explicitly recorded SHA256. Its plan must be saved before either run. Freeze five
executable/project hashes and the child measurement script; verify them before and
after each run. Use fresh profiles and process-only observation for both runs.

Each run retains calibration, ten-second warmup, three sixty-second intervals,
one-second native process counters, ten-second wait/120 ms blink, input freeze,
absent tray, real completed-blink evidence and zero idle animation clocks. No
concurrent builds, app tests, traces or companion input. No overhead subtraction.
Each interval must remain strictly below **1% of one logical processor** and
**150,000,000 sampled private bytes**. Both complete runs must pass every interval.

Preserve both valid passes or failures; a budget failure does not trigger a retry.
A technical failure stops the sequence as incomplete, without substitution. Earlier
CPU failures remain valid historical results. Two short runs neither establish
statistical confidence nor validate tray/compositor cost or long-running stability.
The separate validation sequence decision continues to require physical display,
actual-input, accessibility and sign-in qualification before release.
