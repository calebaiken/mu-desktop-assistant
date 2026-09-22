# Mandatory independent development QA

The development QA agent is an independent critical reviewer working alongside the
implementation agent at logical checkpoints. It inspects architecture, code, numerical
logic, UX, safety, concurrency, reliability, performance, tests and documentation.
It is not a runtime LLM self-review feature. The implementation author cannot replace
its verdict with a self-assigned passing score.

Loop: plan → implement → build → test → independent QA → rank top five → fix → retest
→ re-review. A phase may advance **only if the unrounded score is strictly > 8.5/10**
and no critical unresolved correctness/security defect remains. 8.5 fails. Insufficient
evidence leaves a phase incomplete; never increase scores just to advance.

## Evidence-based rubric

Ten equally weighted categories: Code Correctness, Architecture, Mathematical
Correctness, Testing, Reliability, Security, Performance, Maintainability, UX/UI,
Requirement Compliance. For each, the reviewer defines five phase-relevant criteria
and scores each 0 (missing/contradicted), 1 (partial), 2 (satisfied by cited evidence).
Category score = sum of five criteria (0–10). Overall = sum of ten category scores / 10.
Use exact decimal arithmetic; show raw arithmetic alongside displayed one-decimal score.
Example boundary checks: sum 85 gives 8.5 FAIL, sum 86 gives 8.6 PASS absent critical
blockers. Runtime numerical accuracy is not applicable to a phase with no numerical
feature; score explicit numerical design/verification planning and QA arithmetic then,
and state the scope. Never infer future math accuracy from planning evidence.

For later phases weight changes are permitted only with a reason recorded before
review and weights summing to 1; show every weighted contribution. Phase 0 uses equal
weights. The reviewer may cap or fail a phase for a serious unresolved issue even if
the arithmetic passes. Neither an empty suite nor compilation alone proves feature
correctness. Cite commands/exit codes, file paths and test results; distinguish manual
inspection, structural checks, actual automated behavior and untested scenarios.

## Required report template

```text
QA REVIEW
Phase:
Overall Score: X.X / 10
Result: PASS / FAIL

Category Scores:
- Code Correctness:
- Architecture:
- Mathematical Correctness:
- Testing:
- Reliability:
- Security:
- Performance:
- Maintainability:
- UX/UI:
- Requirement Compliance:

Evidence:
[Commands, exit codes, criterion-level evidence and limitations]

TOP 5 RANKED FIXES / IMPROVEMENTS
1. Problem:
   Why it matters:
   Proposed fix:
2. Problem:
   Why it matters:
   Proposed fix:
3. Problem:
   Why it matters:
   Proposed fix:
4. Problem:
   Why it matters:
   Proposed fix:
5. Problem:
   Why it matters:
   Proposed fix:

Regression Risks:
Tests Required:
Final Verdict:
```

Top five are required even on a pass (then improvements may be nonblocking). Store
reports/revisions under `docs/qa/`; retain failed-review history. Update
DEVELOPMENT_STATUS.md with completed/pending work, decisions, debt, commands/results,
last QA score/history, known bugs and next objective. Finalize the phase in source
control after review. Do not fabricate a Git identity or a successful commit if local
policy or absent identity prevents one; document the exact available checkpoint.
