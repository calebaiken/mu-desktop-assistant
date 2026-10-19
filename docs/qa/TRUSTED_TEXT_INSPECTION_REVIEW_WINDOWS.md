# Independent QA: approved text inspection, Windows development checkpoint

QA REVIEW

Phase: First bounded Module A development vertical slice, 2026-10-19.

Reviewed code commit: **`a999ae4f59b16a9e435185726a647668a5aac51f`**, on
`feature/trusted-text-inspection`, based on
`main` commit `7fe3305b49f755a94d65362692e783ef0e60035a`.
Draft [PR #2](https://github.com/calebaiken/mu-desktop-assistant/pull/2).

Overall Score: **9.4 / 10**

Result: **PASS — bounded development slice only; release qualification remains open**

Reviewer: a separate independent development QA agent. This is a re-review using
new actual Windows execution evidence against the unchanged
[frozen protocol](TRUSTED_TEXT_INSPECTION_PROTOCOL.md). The author did not assign
the verdict. Preserve the [initial 6.9 FAIL](TRUSTED_TEXT_INSPECTION_REVIEW_INITIAL.md)
and [Linux-only 8.2 incomplete review](TRUSTED_TEXT_INSPECTION_REVIEW_LINUX.md).
No unresolved critical correctness/security defect was confirmed in this scope.
This approves neither the larger A/B/C/G/I modules nor release, physical
accessibility, arbitrary automation or general scientific correctness.

## Category scores and criterion-level evidence

Each criterion receives 0/1/2 under the frozen protocol. References WIN, NET, ARCH,
REG and SRC are defined below. Missing individual native outcomes, adversarial
reparse execution, process-death, resource and physical evidence retain deductions.

| Category | Criterion scores, in order | Total | Criterion-level evidence and limits |
|---|---|---:|---|
| Code Correctness | C1=2, C2=2, C3=2, C4=2, C5=2 | 10 | C1: WIN unconditionally executes actual production reader/literal result smoke; NET real streams preserve source. C2: NET malformed/null/unsupported requests deny. C3: immutable exact preview and SRC handle identity. C4: NET denial/expiry/revoke/replay. C5: NET typed terminal outcomes, late-content suppression and audit failure. |
| Architecture | A1=2, A2=2, A3=2, A4=2, A5=2 | 10 | A1: ARCH11 and WIN required strict gate. A2/A3: package-free application authority and Infrastructure-only native adapter. A4: App retains executor across window reopen; explicit cleanup ownership. A5: isolated inspector has no Chat/model/history/memory dependency; REG regression evidence retained. |
| Mathematical Correctness | M1=2, M2=2, M3=2, M4=2, M5=2 | 10 | M1/M4: NET exact byte/UTF-16, fragmented Unicode and invalid supplementary/path fixtures. M2: exact monotonic expiry/UTC rollback. M3: displayed source bytes and documented KiB/UTF-16 bounds match code, exercised through native preview/read. M5: exact arithmetic below. Scope is bounds/encoding/time/review arithmetic, not scientific accuracy. |
| Testing | T1=2, T2=2, T3=2, T4=1, T5=2 | 9 | T1: WIN positive/negative script success plus ARCH. T2/T3: NET deterministic normal/adversarial tests, including 20 reviewer-authored cases and actual streams. T4: native HWND/production-reader WPF smoke has real gate success, but individual reparse/privilege-dependent outcomes are inaccessible. T5: cancellation/deadline/limit/cleanup tests and actual required regression gate; missing qualifications explicitly disclosed. |
| Reliability | R1=2, R2=2, R3=2, R4=2, R5=1 | 9 | R1/R2: NET parallel replay/no queue, terminal-audit revocation and bounded noncooperative callbacks. R3: stale state/corrupt text/platform/audit failure recovery. R4: native clear/disable verifies lease release; pending expiry/disposal ownership tested independently. R5: normal native shutdown/gates pass, but forced process-death/kernel-stall qualification is absent. |
| Security | S1=2, S2=1, S3=2, S4=2, S5=2 | 9 | S1/S3: separate trusted approval, opaque exact-preview single-use authority and policy tests. S2: final-path/file-ID/no-follow/ancestor-lease implementation reviewed; native read and additional ancestor-rename fixture exist, but reparse/swapping qualification is incomplete and individual outcomes cannot be inspected. S4/S5: literal native result, strict decoder, enum-only audit and no text/model/history/persistent-content bridge. |
| Performance | P1=2, P2=2, P3=1, P4=2, P5=2 | 9 | P1/P2: fixed buffers/decoding bounds, no queue and real ignoring-cancellation deadline probe. P3: I/O is off dispatcher and native smoke completes, but dedicated heartbeat/CPU measurements are absent. P4: bounded pending handle/resource ownership, native lease release and poisoned late cleanup. P5: actual measurements distinguished from source checks; unchanged historical resource failures/thresholds retained. |
| Maintainability | N1=2, N2=2, N3=2, N4=2, N5=2 | 10 | N1: cohesive contracts/executor/decoder/native/window modules. N2: no dependency/lockfile/reference changes. N3: independent deterministic fixtures with explicit platform requirements. N4/N5: usage/security/architecture/testing/source-audit docs and this preserved review history distinguish candidate, verification provenance and remaining work. |
| UX/UI | U1=2, U2=2, U3=2, U4=2, U5=1 | 9 | U1: compiled existing menu event path and actual native inspector HWND in WIN smoke. U2: exact metadata-only target precedes approval. U3/U4: native smoke exercises completed literal output, no UI replay, disable and clear. U5: names/access keys/system colors reviewed; physical picker/keyboard/contrast/minimum layout/accessibility remain unqualified. |
| Requirement Compliance | Q1=2, Q2=2, Q3=2, Q4=2, Q5=2 | 10 | Q1: coherent reachable read-only slice, no shell/general filesystem/app control. Q2/Q3: approved bounded single-use local read, redacted audit and strict chat isolation. Q4: actual exact-code Windows required CI gates, portable evidence and access limits honestly distinguished. Q5: focused branch/draft PR/source identity, docs and selected-module follow-up sequence; no merge or PR #1 duplication. |

Raw arithmetic: `10+10+10+9+9+9+9+10+9+10 = 94`; `94/10 = 9.4`.
`9.4 > 8.5` is true; zero critical unresolved defects were confirmed in this bounded
review. No rounding, weight changes or future capability credit apply.

## Evidence

### WIN — independently observed actual final-code CI

The reviewer fetched the normal public GitHub sources through the configured proxy:

- [Exact-code status partial](https://github.com/calebaiken/mu-desktop-assistant/pull/2/partials/commit_status_icon?oid=a999ae4f59b16a9e435185726a647668a5aac51f):
  **`2 / 2 checks OK`**.
- [PR checks page](https://github.com/calebaiken/mu-desktop-assistant/pull/2/checks)
  simultaneously selected commit **`a999ae4`**, contained two **`This job succeeded`**
  labels, and identified final-code Windows runs
  [39293630026](https://github.com/calebaiken/mu-desktop-assistant/actions/runs/39293630026)
  and [39293637612](https://github.com/calebaiken/mu-desktop-assistant/actions/runs/39293637612).
- Their job URLs are respectively
  [118200398980](https://github.com/calebaiken/mu-desktop-assistant/actions/runs/39293630026/job/118200398980)
  and [118200425424](https://github.com/calebaiken/mu-desktop-assistant/actions/runs/39293637612/job/118200425424).

Independent fetch/parse commands returned **exit 0**. Saved public responses:
`artifacts/qa/trusted-text/independent/final-commit-status.html` and
`final-pr-checks.html`. The observed commit selector and status were checked
together, rather than attributing an older green run to new source.

At that immutable code commit, `.github/workflows/verify.yml` runs both commands
on `windows-latest` without `continue-on-error`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-negative.ps1
```

The positive script requires locked restore, strict Release build, architecture,
nonempty unfiltered tests and native WPF smoke exit 0 within 15 seconds. The App's
ordinary `--smoke` path unconditionally awaits `TextInspectionSmokeChecks.RunAsync`.
That smoke creates the inspector's native HWND and uses the production Windows
reader: disabled → enable → exact metadata-only preview → approved literal result →
no UI replay → disable/clear → source write succeeds after lease release. Failure
throws and fails the positive script. The negative script is a separate required
step. Thus actual successful workflow conclusions provide bounded native gate/smoke
evidence; merely having a workflow file would not.

For earlier code `d2607b60ae27494cf449adc845ac74dec0acf73a`, the reviewer also directly
observed REST run/job responses for
[39292932838](https://github.com/calebaiken/mu-desktop-assistant/actions/runs/39292932838):
Build/test/smoke, Adversarial architecture controls and artifact upload all succeeded.
This historical confirmation is not substituted for the final-code observation.

**Access limitation:** direct job-log and TRX artifact downloads redirect to
`productionresultssa7.blob.core.windows.net`, blocked by the cloud destination
policy. Later `api.github.com` reads also became blocked. No proxy bypass or trust
disablement was attempted. Public allowed GitHub status sources remained usable.
The reviewer has **not inspected exact Windows counters, individual TRX outcomes,
symlink privilege skips or detailed final step stdout**. Do not infer/quote precise
Windows case totals or claim the symlink case executed from this report. Physical
interaction, adversarial swaps and resource acceptance are also not established.

### NET/ARCH/REG — portable execution and source review

The reviewer independently executed the 43-case text inspection selection on Linux,
SDK 10.0.401: **40 passed, zero failed, three Windows-only skipped**, exit **0**.
Receipt `artifacts/qa/trusted-text/independent/independent-trusted-actions-unicode-final.trx`.
The invocation and cases are recorded in the Linux review. That independent run
preceded the final native-only HWND/ancestor-rename fixture changes. The reviewer
inspected the complete `d2607b6..a999ae4` delta: those two native evidence changes
and guide text only. The author rebuilt final source and reran the same selection,
again **40/0/3 of 43**, with zero build warnings/errors; receipts
`trusted-text-20261019/build-native-evidence.log` and `targeted-native-evidence.trx`.
No portable policy/decoder behavior changed between those runs.

Independent architecture execution: **11/11, exit 0**;
`artifacts/qa/trusted-text/independent/architecture-final.log`. Earlier candidate
Linux full-gate TRX: **383 passed, 23 failed, 34 skipped, 440 total**, before the
final 20th independent test. Independent comparison with baseline TRX proves the
same 23 failed test names, zero introduced failures. This Linux gate remains
failed and is not relabeled as passing because Windows CI succeeded.

The reviewer inspected the author's actual Python receipts: **48 tests passed**
across 6/16/19/7 suites. Python source and PR #1's waveform/HDF5/NPZ functionality
were not changed. No installed-model inference/resource qualification was performed.

SRC inspection covered all changed tool contracts/executor/native/decoder/window,
App/menu integration, native smoke, tests and relevant docs. Historical 6.9/8.2
reports and failed test receipts remain intact. No generated binaries, credentials,
datasets, model weights or runtime environments are committed by this feature.

## TOP 5 RANKED FIXES / IMPROVEMENTS

These are remaining qualification/follow-up improvements; none is a confirmed
critical blocker to this bounded development slice.

1. **Problem:** Detailed Windows logs/TRX and privilege-dependent link outcomes
   cannot be inspected under the current cloud destination policy.
   **Why it matters:** Aggregate success does not identify every skipped native
   case or fully qualify hostile reparse scenarios.
   **Proposed fix:** Obtain detailed retained CI evidence through supported network
   configuration/access, record exact outcomes, and run missing adversarial
   junction/link/swap/identity fixtures without treating skips as passes.
2. **Problem:** Actual picker, keyboard, minimum geometry, contrast, screen reader,
   sleep and close interaction are not physically qualified.
   **Why it matters:** User control depends on usable exact previews and reliable
   revocation in the real desktop flow.
   **Proposed fix:** Run and retain a native interaction/accessibility matrix before
   distribution; repair any observed defects and retest affected paths.
3. **Problem:** Native hard termination, low-memory/kernel-stall and desktop/worker
   resource acceptance remain open.
   **Why it matters:** Managed cancellation and fixed buffers cannot prove native
   cleanup after every fault or qualify idle CPU/model coexistence.
   **Proposed fix:** Keep original thresholds/failures intact and perform measured
   A/I/G qualification with owned crash/low-memory fixtures before broader automation.
4. **Problem:** Current authority is intentionally one registered action and one
   process-local preview; broader task/schema/project contracts remain design.
   **Why it matters:** Extending it casually could turn narrow selected-file consent
   into blanket authority or couple durable research work to temporary chat.
   **Proposed fix:** Keep the one-action boundary; make the next B increment explicit
   project identity and reuse ML Lab's captured dataset/reviewed Run, respecting PR #1.
5. **Problem:** Public-source release readiness still lacks a dedicated documented
   private security-reporting contact and physical/resource receipts.
   **Why it matters:** A development QA pass is not release/security certification.
   **Proposed fix:** Establish the reporting route and complete existing release
   gates; keep the PR draft while those independently scoped qualifications remain.

Regression Risks: Windows path/sharing/filter-driver semantics, unacknowledged native
I/O, retained late cleanup, audit/result ordering, view reopen and resource budgets.

Tests Required: inspect exact native case outcomes, run missing adversarial swaps,
physical interaction/accessibility and measured crash/low-memory/resource acceptance.
Keep successful gates and all historical failures; do not weaken the threshold.

Final Verdict: **PASS at 9.4/10 for the first bounded Module A development slice**
on reviewed code `a999ae4`. The actual native gate/smoke evidence supports this
decision. Larger modules, advanced scientific features, model qualification,
physical accessibility and release remain incomplete and receive no capability claim.
