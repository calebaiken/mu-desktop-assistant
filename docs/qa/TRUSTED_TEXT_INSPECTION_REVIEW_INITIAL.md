# Independent QA: trusted text inspection, initial checkpoint

QA REVIEW

Phase: First Module A vertical slice, initial uncommitted implementation checkpoint,
2026-10-19; baseline `7fe3305b49f755a94d65362692e783ef0e60035a`, branch
`feature/trusted-text-inspection`.

Overall Score: **6.9 / 10**

Result: **FAIL — implementation and Windows qualification incomplete**

This historical checkpoint uses the frozen criteria in
[TRUSTED_TEXT_INSPECTION_PROTOCOL.md](TRUSTED_TEXT_INSPECTION_PROTOCOL.md).
It records the first independent test/source review before the author's subsequent
repairs. It is not a claim that later source still has every defect below. The
reviewer is a separate development QA agent; the author did not assign this score.

## Category scores and criterion-level evidence

Each number is 0/1/2 for the corresponding numbered criterion in the frozen
protocol. Missing native evidence receives no full credit. Mathematical scope is
bound/encoding/time/review arithmetic, not scientific numerical accuracy.

| Category | Criterion scores, in order | Total | Evidence and limitations |
|---|---|---:|---|
| Code Correctness | C1=1, C2=2, C3=2, C4=2, C5=1 | 8 | Actual literal fixture/policy execution; Windows real-file path unexecuted; null Execute input dereferenced before normalization in initial source. |
| Architecture | A1=1, A2=2, A3=2, A4=2, A5=1 | 8 | Filtered strict project build passed; layer source inspected; whole architecture/regression gate not independently executed at this checkpoint. |
| Mathematical Correctness | M1=2, M2=2, M3=1, M4=2, M5=2 | 9 | Real fragmented UTF-8 and exact UTF-16-unit boundary fixtures; exact monotonic expiry denied correctly under UTC rollback; native size presentation unexecuted. |
| Testing | T1=1, T2=2, T3=2, T4=0, T5=1 | 6 | 14 passed, 1 failed, zero skipped of 15 independent tests; native Windows/WPF unavailable; noncooperative deadline paths not yet behaviorally tested. |
| Reliability | R1=2, R2=1, R3=1, R4=1, R5=1 | 6 | Replay/no-queue checks passed; token cancellation alone could not bound an ignoring callback; expired lease was retained; native shutdown unexecuted. |
| Security | S1=2, S2=1, S3=2, S4=2, S5=2 | 9 | Real policy rejection, grant isolation, stale state, literal-result and redacted-audit fixtures; Windows handle security source inspected only. |
| Performance | P1=2, P2=0, P3=1, P4=1, P5=2 | 6 | Fixed byte/character bounds; hard caller deadline absent; source has no continuous idle file polling; expiry/cleanup gap; historical resource evidence unmodified. |
| Maintainability | N1=2, N2=2, N3=2, N4=0, N5=1 | 7 | Cohesive package-free contracts and native adapter, no dependencies added; independent deterministic tests; usage/security/status docs still pending. |
| UX/UI | U1=1, U2=1, U3=1, U4=1, U5=1 | 5 | WPF source offered selection, exact target, explicit approve, literal display and clear; actual native/keyboard/layout behavior not executed. |
| Requirement Compliance | Q1=1, Q2=1, Q3=2, Q4=1, Q5=0 | 5 | Narrow local read slice exists in source with isolated output; deadline/cleanup work and native verification remain; delivery docs/PR not yet complete. |

Raw arithmetic: `8+8+9+6+6+9+6+7+5+5 = 69`; `69/10 = 6.9`.
`6.9 > 8.5` is false. No rounding or weight changes apply.

## Evidence

Independently executed on Linux with .NET SDK 10.0.401, after sourcing
`/workspace/.cloud-tools/activate.sh`:

```text
dotnet test tests/Companion.Tests/Companion.Tests.csproj -c Release --no-restore
  -p:EnableWindowsTargeting=true
  --filter FullyQualifiedName~IndependentTrustedActionTests
  --logger trx;LogFileName=independent-trusted-actions-initial.trx
  --results-directory artifacts/qa/trusted-text/independent
```

Exit **1**: **14 passed, 1 failed, zero skipped, 15 total**. The filtered invocation
built Core/Application/Infrastructure/Tests successfully. The failed check was
`ExpiryAtExactMonotonicBoundaryCannotBeExtendedByUtcClockRollback`: content was
correctly denied as Expired, but the lease was not immediately disposed. Receipt:
`artifacts/qa/trusted-text/independent/independent-trusted-actions-initial.trx`
(ignored local artifact, not committed).

Independent application tests use injected owned resources/audit callbacks. Two
decoder probes use actual memory streams, including arbitrary multibyte splitting
and an independently constructed 32,768-UTF-16-unit surrogate-pair fixture. These
are actual tests of those boundaries, not Windows file-handle or WPF evidence.

Source inspected: `ToolContracts.cs`, `TrustedToolExecutor.cs`,
`TextFileInspectionTool.cs`, `BoundedTextReader.cs`, the inspector window and App
integration. No Windows host, WPF runtime, physical input device or actual resource
acceptance run was available. No native check is recorded as passing.

## TOP 5 RANKED FIXES / IMPROVEMENTS

1. **Problem:** An I/O/audit callback ignoring cancellation can retain the gate and
   caller indefinitely; shutdown has no owned late-cleanup result.
   **Why it matters:** The advertised bounded operation is not enforced by merely
   cancelling a token.
   **Proposed fix:** Bound waits, retain cleanup ownership, poison unsafe reuse and
   report unresolved cleanup explicitly; add noncooperative callback tests.
2. **Problem:** Monotonic expiry denies execution without retiring the expired lease.
   **Why it matters:** A no-longer-authorized preview can retain handles until its
   separate timer happens to run.
   **Proposed fix:** Retire pending state at expiry detection; rerun the failed test.
3. **Problem:** Execution releases admission before terminal audit and final
   revocation checks in the initial source.
   **Why it matters:** New operations can overlap incomplete publication and a
   cancelled result can surface as completed.
   **Proposed fix:** Serialize terminal recording/publication and test revocation
   while outcome auditing is blocked.
4. **Problem:** Real Windows reparse/file identity/lease and native WPF workflow
   behavior have no execution evidence.
   **Why it matters:** Source review and injected tests cannot establish OS behavior.
   **Proposed fix:** Run Windows integration tests, both supported verification
   scripts and native smoke before qualifying the end-to-end feature.
5. **Problem:** Null request handling and final usage/security/status evidence are
   incomplete.
   **Why it matters:** Malformed input should fail through a typed boundary and
   users must understand the exact supported capability and remaining limits.
   **Proposed fix:** Normalize malformed requests, complete relevant docs and retain
   the failed receipt and this review when publishing a later revision.

Regression Risks: cancellation/cleanup races, audit failure, stale file identity,
Windows path semantics, view reopen/shutdown, and privacy boundaries.

Tests Required: fix/retest the failed independent test; execute deadline/audit race,
native path/handle and WPF workflow tests; preserve existing regression and CPU/memory
thresholds and historical failed measurements.

Final Verdict: **FAIL at this initial checkpoint.** Continue repair and independent
re-review. This report is retained even if a later checkpoint passes its available
tests. Native Windows qualification remains an explicit separate evidence requirement.
