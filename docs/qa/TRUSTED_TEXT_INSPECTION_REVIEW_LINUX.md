# Independent QA: trusted text inspection, Linux checkpoint

QA REVIEW

Phase: First Module A development candidate, 2026-10-19. Source base
`7fe3305b49f755a94d65362692e783ef0e60035a`; branch
`feature/trusted-text-inspection`; reviewed changes were not yet committed.

Overall Score: **8.2 / 10**

Result: **FAIL — native qualification evidence incomplete**

Reviewer: separate independent development QA agent. Criteria and weights remain
those frozen in [the protocol](TRUSTED_TEXT_INSPECTION_PROTOCOL.md). This report
supersedes the initial checkpoint for available Linux evidence, while preserving
[the initial 6.9 failed review](TRUSTED_TEXT_INSPECTION_REVIEW_INITIAL.md). No
remaining critical application-policy defect was confirmed in this review. That
finding does not establish that unexecuted Windows behavior is correct.

## Category scores and criterion-level evidence

Each criterion is scored 0/1/2 under the frozen protocol. Evidence references are
defined below. Native paths/handles and WPF execution receive partial/no credit
because they did not execute in this environment.

| Category | Criterion scores, in order | Total | Criterion-level evidence and limitations |
|---|---|---:|---|
| Code Correctness | C1=1, C2=2, C3=2, C4=2, C5=2 | 9 | C1: real FileStream decoding/source preservation, but production Windows adapter unexecuted. C2–C5: NET43 typed denial/malformed/null, immutable preview, replay/revocation/expiry/stale-state and content-free terminal results. |
| Architecture | A1=2, A2=2, A3=2, A4=2, A5=2 | 10 | A1: ARCH11 and strict cross-build. A2–A4: reviewed package-free policy/authority port, Infrastructure-only native adapter and App-owned lifetime. A5: isolated inspector has no Chat/model/history/memory dependency; BASE/CAND failure-name sets match. |
| Mathematical Correctness | M1=2, M2=2, M3=1, M4=2, M5=2 | 9 | M1/M4: independent exact byte/UTF-16 bounds, fragmented Unicode and supplementary/malformed path/text fixtures in NET43. M2: exact monotonic expiry with UTC rollback. M3: size/units agree in source, native display unexecuted. M5: exact arithmetic below. No scientific numerical accuracy claimed. |
| Testing | T1=2, T2=2, T3=2, T4=0, T5=1 | 7 | T1: strict cross-build/locked restore/ARCH11 evidence. T2/T3: NET43, including 20 reviewer-authored tests and actual streams. T4: three Windows tests skipped and WPF unavailable. T5: cancellation/deadline/audit/cleanup tests execute; Windows process/teardown and complete supported gate remain missing. |
| Reliability | R1=2, R2=2, R3=2, R4=1, R5=1 | 8 | R1/R2: parallel replay/no queue, blocked terminal audit, noncooperative callback budget and cancellation tests. R3: stale/failure/corrupt text/unsupported Linux capability and fresh-use recovery. R4/R5: owned cleanup/disposal policy tested; real Windows lease release, hard process failure and native shutdown remain unexecuted. |
| Security | S1=2, S2=1, S3=2, S4=2, S5=2 | 9 | S1/S3: default deny, separate trusted approval, opaque reference-bound one-use authority and adversarial policy tests. S2: canonical no-follow ancestor/file lease/identity implementation inspected, not executed on Windows. S4/S5: literal decoder/result fixtures, enum-only audit contract, no text/path in audited DTOs and no chat/model/persistence call for selected text. |
| Performance | P1=2, P2=2, P3=1, P4=1, P5=2 | 8 | P1/P2: bounded allocation/decoding, no queue and actual ignoring-cancellation deadline test. P3/P4: source moves I/O off dispatcher and retains bounded owned handles/cleanup; native measurements and UI responsiveness unexecuted. P5: unchanged resource thresholds and historical failed receipts retained; no new CPU/memory qualification claimed. |
| Maintainability | N1=2, N2=2, N3=2, N4=2, N5=2 | 10 | N1: cohesive contracts/executor/decoder/native adapter/window. N2: no dependency/lockfile/project-reference changes. N3: deterministic independent fixtures with explicit native requirements. N4/N5: action/security/architecture/testing/status/audit docs explain exact budgets, completed candidate and qualification gaps; historical reviews preserved. |
| UX/UI | U1=1, U2=1, U3=1, U4=1, U5=1 | 5 | U1–U5: actual WPF source provides menu access, picker, enable, exact metadata preview/expiry, approval once, literal result, clear/disable, accurate state labels and native system colors/names. The smoke is implemented but has not executed here; physical picker, keyboard, minimum geometry and accessibility remain unqualified. |
| Requirement Compliance | Q1=1, Q2=2, Q3=2, Q4=1, Q5=1 | 7 | Q1: coherent source slice, native execution incomplete. Q2/Q3: NET43 plus source establish application permission/limits/audit/offline and temporary-chat separation. Q4: Linux failures/skips/unavailable Windows gates honestly reported. Q5: branch, source audit, docs and follow-up sequence exist; PR/Windows receipts pending at this checkpoint. |

Raw arithmetic: `9+10+9+7+8+9+8+10+5+7 = 82`; `82/10 = 8.2`.
`8.2 > 8.5` is false. No rounding or weight adjustment applies.

## Evidence

**NET43, independently executed:** after sourcing
`/workspace/.cloud-tools/activate.sh`, SDK 10.0.401 on Debian Linux:

```text
dotnet test tests/Companion.Tests/Companion.Tests.csproj -c Release --no-restore
  -p:EnableWindowsTargeting=true
  --filter FullyQualifiedName~IndependentTrustedActionTests|FullyQualifiedName~TextInspectionTests
  --logger trx;LogFileName=independent-trusted-actions-unicode-final.trx
  --results-directory artifacts/qa/trusted-text/independent
```

The actual filter/logger arguments were individually shell-quoted. Exit **0**:
**40 passed, zero failed, three explicitly Windows-only skipped, 43 total**.
Includes **20 independently authored tests**. Strict Core/Application/Infrastructure/
Tests build completed in this invocation. Receipt:
`artifacts/qa/trusted-text/independent/independent-trusted-actions-unicode-final.trx`.
An earlier independent 42-case run also passed 39/42 with the same three native
skips; its receipt remains `independent-trusted-actions-final.trx`.

**ARCH11, independently executed and retained:**

```text
dotnet run --project tests/Companion.ArchitectureChecks -c Release --no-build -- /workspace/mu-desktop-assistant
```

Exit **0**, **11/11**. Receipt:
`artifacts/qa/trusted-text/independent/architecture-final.log`.

**BASE/CAND, author-executed receipts independently inspected:**
`artifacts/qa/trusted-text-20261019/baseline.trx` has 344 passed, 23 failed,
31 skipped, 398 total. The earlier candidate full gate in `final-full-gate.trx`
has 383 passed, 23 failed, 34 skipped, 440 total. This predates the final 20th
independent path-Unicode test. The reviewer independently parsed both TRX files:
**introduced failed test names: none; removed failed test names: none**.
The positive script exits unsuccessfully at Windows-dependent tests on Linux,
before WPF smoke. It is not a passing full gate. The author's full Release
cross-build receipt reports zero warnings/errors.

**PY, author-executed receipts independently inspected:** reviewed
`ml-author.log`, `ml-data.log`, `ml-engine.log`, `ml-pipeline.log` under the same
candidate artifact directory: **6+16+19+7 = 48 actual Python tests pass**.
No new Python behavior is introduced by this slice. This is inspected execution
evidence, not a claim the reviewer separately reran those commands.

**NATIVE, unavailable:** the retained direct native launch reports missing
`Microsoft.WindowsDesktop.App` on Linux. Three Windows handle/reparse/persistence
tests call Inconclusive here. The new real WPF smoke, Windows positive/negative
scripts, actual file-picker interaction, physical accessibility and native
adversarial ancestor swapping did not execute. Source review is not execution.

**Repair history:** the original independent 14/15 failure is preserved; a later
17-case checkpoint found three failures while lifecycle fixes were underway.
The current available tests verify immediate expired lease retirement, bounded
noncooperative waits, revocation during terminal audit, post-disposal controls,
deferred cleanup, malformed/null requests and Unicode Format rejection across
supplementary runes and paths. The reviewer corrected a test to accept an explicitly
poisoned/unavailable instance as safe denial and awaited successful disposal before
asserting late cleanup; neither change allows content delivery or replay.

All artifacts above remain ignored local outputs; none are committed binaries or
private data. Historical resource failures and QA receipts were not rewritten.

## TOP 5 RANKED FIXES / IMPROVEMENTS

1. **Problem:** Production Windows handles and actual WPF controls are unexecuted.
   **Why it matters:** Those are essential to the promised end-to-end security and
   observable workflow; passing Linux application tests cannot qualify them.
   **Proposed fix:** Run supported Windows verification and negative scripts, inspect
   real native test/smoke results, then perform independent re-review. If PR CI is
   available, use its actual logs/artifacts rather than a workflow definition.
2. **Problem:** Adversarial Windows ancestor replacement/junction swaps and some
   real resource identity failures lack executed fixtures.
   **Why it matters:** Retained handles/final-path checks are the primary defense
   against file redirection after trusted preview.
   **Proposed fix:** Run deterministic native swap/replacement/lease fixtures,
   including mapped/device/ADS rejection and release after cancel/expiry/shutdown.
3. **Problem:** Physical picker, keyboard, contrast, minimum layout, sleep and close
   behavior have source review only.
   **Why it matters:** Approval is useful only when users can inspect the exact
   target and reliably revoke/clear the operation through the real controls.
   **Proposed fix:** Execute a recorded native interaction/accessibility matrix;
   preserve any defects and recheck affected smoke behavior after repairs.
4. **Problem:** Native low-memory/process-termination/resource qualification is open.
   **Why it matters:** A bounded byte buffer does not establish desktop idle CPU,
   kernel cleanup after abrupt exit or model-workload coexistence.
   **Proposed fix:** Keep all thresholds/history unchanged; add measured incremental
   workload/crash qualification in the appropriate selected A/I/G milestone.
5. **Problem:** The development candidate has no reviewed Windows receipt/PR delivery
   identity yet, and the public repository still lacks a documented security contact.
   **Why it matters:** Reviewers need the precise source and qualification status;
   users need a private route for sensitive vulnerability reports before release.
   **Proposed fix:** Attach the focused draft PR and inspect its exact-head CI if
   permissions permit; record remaining blockers. Establish a disclosure contact
   before distribution rather than claiming this review is certification.

Regression Risks: Windows sharing/reparse semantics, stalled kernel I/O, late
cleanup, terminal-audit/result state, view reopen and existing resource budgets.

Tests Required: Windows positive/negative gates and native fixtures; physical
picker/keyboard/accessibility; process failure/low-memory/resource qualification.
Skipped cases must remain visible. No scientific analysis or other selected module
is qualified by this review.

Final Verdict: **FAIL/incomplete phase exit at 8.2/10.** The available Linux checks
support a focused reviewable candidate and the observed application-policy repairs.
They do not qualify an end-to-end Windows capability. Keep the strict >8.5 and
zero-critical gate; append a separate review when meaningful native evidence arrives.
