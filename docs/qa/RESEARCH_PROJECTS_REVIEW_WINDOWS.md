# QA REVIEW

Phase: B — bounded explicit research project workspace, supported-platform development exit (2026-10-19)

Overall Score: **9.6 / 10**

Result: **PASS for the bounded research-project development slice**

Independent reviewer: development QA agent, separate from the implementation author.
Reviewed code/source: **`c8da7e5664faec3f8d5430bec0ab018aa8512f42`** on
`feature/research-projects`. Base:
`376c3206a38fa86b1639e5f72c6a67cc57153cb4` (draft PR #2).
Delivery: [draft stacked PR #3](https://github.com/calebaiken/mu-desktop-assistant/pull/3),
independently checked open/draft with base `feature/trusted-text-inspection` and
this exact head. Neither PR #1 nor PR #2 was merged by this work.

The [50-criterion protocol](RESEARCH_PROJECTS_PROTOCOL.md) was frozen before review.
The [initial incomplete review and subsequent defect/native-failure addenda](RESEARCH_PROJECTS_REVIEW_INITIAL.md)
remain preserved. This final verdict does not approve a main-Chat handoff, project
memory, model profiles, new scientific algorithms, physical accessibility or release.
There are **zero confirmed unresolved critical defects** in this reviewed slice.

## Category Scores

Every criterion uses 0/1/2 and equal weights. IDs refer to the frozen protocol.

| Category | Criterion scores in protocol order | Sum |
|---|---|---:|
| Code Correctness | C1=2, C2=1, C3=2, C4=2, C5=2 | 9 |
| Architecture | A1=2, A2=2, A3=2, A4=2, A5=2 | 10 |
| Mathematical Correctness | M1=2, M2=2, M3=2, M4=2, M5=2 | 10 |
| Testing | T1=2, T2=2, T3=2, T4=2, T5=2 | 10 |
| Reliability | R1=2, R2=2, R3=2, R4=2, R5=2 | 10 |
| Security | S1=2, S2=1, S3=2, S4=2, S5=2 | 9 |
| Performance | P1=2, P2=2, P3=1, P4=2, P5=2 | 9 |
| Maintainability | N1=2, N2=2, N3=2, N4=2, N5=2 | 10 |
| UX/UI | U1=2, U2=2, U3=2, U4=2, U5=1 | 9 |
| Requirement Compliance | Q1=2, Q2=2, Q3=2, Q4=2, Q5=2 | 10 |

Exact arithmetic: `(9 + 10 + 10 + 10 + 10 + 9 + 9 + 10 + 9 + 10) / 10`
`= 96 / 10 = 9.6`, strictly greater than 8.5. No rounding is needed. The mathematical
category covers identity/path derivation, finite bounds, exact state/accounting
invariants and QA arithmetic; there is no new numerical algorithm and no credit
for unimplemented scientific analysis.

## Evidence

### Actual supported Windows gates, independently observed

Both runs identify the exact reviewed source `c8da7e5664faec3f8d5430bec0ab018aa8512f42`:

- [Push run 39301163143](https://github.com/calebaiken/mu-desktop-assistant/actions/runs/39301163143),
  job/check run `118226414685`: **success**.
- [PR run 39301178509](https://github.com/calebaiken/mu-desktop-assistant/actions/runs/39301178509),
  job/check run `118226466808`: **success**.

The reviewer independently queried the normal GitHub runs/jobs/check-run annotations
APIs. Both jobs explicitly show **Build, test and smoke: success** and
**Adversarial architecture controls: success**. The immutable workflow still runs:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-negative.ps1
```

The required scripts, assertions, resource/timeout thresholds and architecture rules
were not weakened. The additive reporting step is always-run, bounded and reports
observations; it does not determine or override gate success.

Actual TRX-derived notice annotations in **both** completed jobs report:

- **493 total; 492 executed, 492 passed, 0 failed; 1 NotExecuted**.
- The sole NotExecuted method is the deliberately Linux-only
  `TextInspectionTests.NativeCapabilityIsUnavailableOnUnsupportedPlatforms`.
- **Passed**: `IndependentResearchProjectTests.RealWindowsOwnedWorkerRoutesRequestsOnlyToSelectedProjectAndReloadsItsOwnMarker`, the real production-host/stdlib-worker routing integration method.
- **Passed**: all three exact A methods
  `WindowsRealPipelineRequiresConsentLocksIdentityAndRedactsPersistentAudit`,
  `WindowsOversizedFileIsRefusedBeforeConsentAndLeaseIsReleased`, and
  `WindowsLeafAndAncestorLinksAreRefusedWithoutReadingTarget`.
- **Research native control smoke completed: True**, with **21 passed checkpoints**.

The 21 fixed checkpoints cover actual picker/Lab HWNDs; no implicit project selection;
explicit create/select and storage preview; minimum open width/create height; all
four project list/name/create/open controls within the actual minimum-size window;
explicit missing-runtime metadata failures; a proposal adding no worker job; fresh
B selection/history/proposal controls; missing-runtime recovery; failure-versus-
completion distinction; and closing/reopening an active project's native view and
cancelling its original job through the actual reopened Cancel button.

Raw detailed blob logs/TRX remain blocked by the artifact destination policy. Unlike
the earlier source, **these counts and individual native outcomes are now actual
bounded TRX-derived observations**, retrieved through normal supported annotations,
not inferred from the overall green icon. The reporter's DTD prohibition, disabled
external XML resolution, 8 MiB XML/8 KiB smoke limits, method/checkpoint/output caps,
DataRow/error/stdout exclusion and escaped annotation delimiters were reviewed.
No private paths, project labels, dataset values or exception messages are emitted.

Independent receipts are in `artifacts/qa/research-projects/independent/`:
`windows-final-{push,pr}-status.json`, `windows-final-{push,pr}-job.json`,
`windows-final-{push,pr}-annotations.json`, `pull-request-final.json`.

### Independently executed Linux checks

With SDK 10.0.401 activated from the local tool directory:

```text
EnableWindowsTargeting=true dotnet test tests/Companion.Tests/Companion.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~IndependentResearchProjectTests --logger 'trx;LogFileName=layout-independent.trx' --results-directory artifacts/qa/research-projects/independent
```

**Exit 0: 40 passed, 0 failed,
1 Windows-only skip, 41 total** on the final native-layout source. Receipt
`layout-independent.trx`/`.log`. Earlier repaired-source and checkpoint reruns remain
as `repaired-independent.trx` and `checkpoint-independent.trx`.

- Independent architecture command:
  `dotnet run --project tests/Companion.ArchitectureChecks -c Release --no-build -- /workspace/mu-desktop-assistant`:
  **exit 0, 11/11**, `architecture-repaired.log`. Subsequent C# changes concern
  shared native initialization/layout diagnostics and do not alter the graph/pins.
- `artifacts/ml-runtime/bin/python -m unittest discover -s tests/ml_lab -p test_research_projects.py -v`:
  **exit 0, one real production-worker subprocess test passed in 14.169 seconds**;
  `production-project-worker.log`. This actually trains/list/describes/exports in A,
  denies B's access to A's experiment, and verifies source bytes are unchanged.

Independent tests use real filesystem metadata/backup/artifact files and real links,
plus explicit synthetic service barriers for concurrency/failure interleavings.
The Windows native routing test uses a real owned stdlib worker through production
`LocalMlLabService`; it is separate from the real scientific worker Linux test.
Neither is described as installed-model or physical-device qualification.

### Author receipts inspected

Under `artifacts/qa/research-projects/`:

- `build-layout-final.log`: serialized strict Release cross-build, **zero
  warnings/errors**, exit 0. The Linux build enables Windows targeting; it is not
  itself native runtime verification.
- Current project selection: **51 passed / 1 Windows-only skip / 52 total**;
  `tests-repaired-final.log`/TRX and final layout rerun. The independent subset above
  is attributed separately.
- Existing prior-A selection: **40 passed / 3 Windows-only skips / 43 total**.
- Existing actual Python ML suites pass **6 + 16 + 7 + 19 = 48 tests**;
  `ml-{author,data,pipeline,engine}.log`. With the new real production-worker case,
  **49 Python tests pass**. CI's bare Python environment does not install or qualify
  the full pinned scientific stack; the installed Linux stack supplies that evidence.
- Additive summary harness verifies actual TRX counts, method-only redaction,
  DTD rejection, missing evidence and escaped annotations. Any local synthetic
  failed-smoke receipt is a reporter fixture and is explicitly not a native result.

### Historical failed evidence and repairs

The initial working checkpoint scored 8.6 numerically but **failed phase exit** for
missing native evidence. A subsequent source review found an actual regression:
closing a running Lab made its cancellation controls unreachable because the project
switch guard also blocked view reopening. The author repaired App composition to
resume `ResearchLabWorkspace.Current` before considering a project switch. New
independent zero-Jobs admission/current-selection tests and the real native reopened
Cancel-button fixture verify that behavior. Switching remains denied while active.

Initial source `9ed4f51` failed its Windows positive gate; precise attribution remains
unknown from its generic annotation. A nonportable expected path separator in the
reviewer's test was also repaired without relaxing canonical-path equality.
Source `72206a0` then demonstrated 492 passing tests and all native integration
methods but **failed** the minimum-layout smoke checkpoint. That measurement remains
in the initial review addendum. The repair displays an invisible, nonactivated native
picker at the actual minimum size and awaits layout; it preserves the minimum button
height and adds four within-window assertions. Picker/Lab initialization now share a
Task across Loaded/explicit callers; proposal assertions distinguish initial metadata
jobs from the new proposal. All required final native checkpoints then actually pass.

Earlier Linux Windows-path failures, overlapping-build failures, malformed-surrogate
DataRow discovery failures, and the author's initial worker-exit fixture mistake remain
retained in the historical receipts. They were not retroactively marked successful.
The old application resource/model/physical/release failures remain open.

## Criterion-level assessment

- **C1/C3–C5:** actual create/open/reload returns intended stable identities and
  generated directories; duplicate labels cannot collide; saved artifact files and
  real worker history stay project scoped; legacy content is selectable without
  moving it; unknown/corrupt/missing-primary-with-recovery-evidence cases deny.
  **C2=1:** current selection survives view close/reopen, including active jobs,
  but selected identity is intentionally not remembered across App restarts. Created
  projects persist and a new session uses an explicit chooser.
- **A1–A5:** exact four-layer/package graph and worker/model isolation remain; typed
  catalog/session ports separate UI and storage; one App-owned service owns lifetime.
  Admission increments under the same lock as retirement before inner `Queued`
  publication. Retired façades permanently deny execution, and borrowed disposal
  cannot shut down the App service. Existing actions/proposal-plus-reviewed-Run stay.
- **M1–M5:** GUID identity/path derivation, reserved legacy identity, name/count/
  byte/depth limits, active-count transition barriers, unchanged scientific code and
  exact rubric arithmetic are demonstrated. These say nothing about future analyses.
- **T1–T5:** independent filesystem/adversarial/concurrency cases execute; final
  supported Windows scripts, real owned-worker routing and native controls pass;
  full current .NET regression and relevant Python computation checks are observed.
  Skips and platform differences are named above, not counted as success.
- **R1–R5:** active/queued work prevents switching; old cleanup must finish before
  another service is created; exception/timeout poisons admission and retains cleanup.
  Tests cover cancellation during retirement, shutdown during transition, concurrent
  shutdown, stale façades, synchronous failure and backup-preserving writes. Reopened
  controls can again cancel active work. Old views/planners cannot cross project identity.
- **S1/S3–S5:** trusted root plus generated IDs determines paths; metadata cannot
  redirect output; malformed/duplicate/unknown fields and invisible Unicode are
  refused; names render literally; no Chat/temporary content or approved proposal is
  transferred. Offline/credential/dependency policies stay unchanged. **S2=1:** real
  native existing-link rejection now passes, but the larger mapped/redirected-volume,
  ancestor-swap and filesystem-failure matrix remains unqualified. The documented
  trusted-profile scope is not same-user hostile-process isolation or encryption.
- **P1/P2/P4/P5:** 32-project/64 KiB bounded catalog operations avoid data scans/model
  loads, run off Dispatcher, and add no idle polling. Existing worker queue/memory/
  CPU/timeout limits are preserved, and no new resource benchmark is claimed.
  **P3=1:** native ordinary controls/reentry work and asynchronous retirement are
  reviewed, but Dispatcher latency during a deliberately delayed retirement/catalog
  storage stall has no native heartbeat measurement.
- **N1–N5:** narrow cohesive contracts, centralized admission/integrity failures,
  behavioral tests, explicit ownership/schema/manual recovery docs and honest status/
  changelog/testing/security/architecture updates match the implementation. Additive
  diagnostics are justified by genuinely inaccessible artifact evidence and preserve gates.
- **U1–U4:** existing More/Chat/tray App wiring reaches the picker; the actual native
  controls show project identity/storage/legacy status before work, create/select
  explicitly, and display denied work/runtime/catalog errors. Reopen/cancel regression
  is repaired. **U5=1:** actual minimum bounds pass, but physical keyboard/screen-reader,
  high contrast and assistive-technology qualification are not established by HWNDs.
- **Q1–Q5:** bounded Module B project selection/isolation works end-to-end; no main-
  Chat bridge, memory, model profile or advanced scientific algorithm is falsely
  credited. PR #1 implementation stays separate. Existing regression/gates and
  historical failures stay intact; dedicated stacked draft PR #3 is reviewable.

## TOP 5 RANKED FIXES / IMPROVEMENTS

These are nonblocking follow-ups for this scoped development pass.

1. **Problem:** Physical picker/keyboard/assistive-technology interaction remains
   unqualified. **Why it matters:** a transparent native test window does not establish
   real focus order or screen-reader usability. **Proposed fix:** qualify selection,
   validation, reopening active jobs, long labels/paths, high contrast and minimum
   layouts on a supported Windows desktop; retain the existing numerical thresholds.
2. **Problem:** The complete redirected-volume/reparse/swap/process-death/kernel-
   stall matrix is not measured. **Why it matters:** trusted-profile pre-use checks
   and managed deadlines are narrower than OS identity/sandbox guarantees.
   **Proposed fix:** test that matrix independently before broadening storage claims;
   retain current fail-closed admission and the stated same-user boundary.
3. **Problem:** Catalog recovery requires explicit manual preservation and restore.
   **Why it matters:** a safe integrity refusal can temporarily prevent access to
   otherwise retained experiments. **Proposed fix:** design bounded version-aware
   backup inspection/recovery UI separately, with explicit user approval and no
   silent identity rebuild, destructive migration or fallback to another project.
4. **Problem:** Native latency/resource measurements for full catalogs and delayed
   cleanup are absent. **Why it matters:** source-level async/bounds do not quantify
   UI responsiveness on target hardware. **Proposed fix:** measure a 32-project
   catalog, repeated reopen/switch cycles and delayed cleanup with a Dispatcher
   heartbeat and existing CPU/memory acceptance rules; keep failed receipts.
5. **Problem:** No last-project preference or main-Chat-to-project handoff exists.
   **Why it matters:** these are useful future conveniences, not delivered behavior.
   **Proposed fix:** keep explicit selection across restarts unless a preference is
   approved; next implement a bounded explicit Chat request handoff that reuses
   captured dataset selection and reviewed Run, then separately approved SQLite
   project memory. Award no present feature credit to those follow-ups.

## Regression Risks

Modal picker/App shutdown, late planner callbacks, catalog loss/corruption and worker
cleanup ownership remain the main integration risks. Portable race tests, real native
process/control checks and unchanged full Windows gates now cover the bounded slice.
Scientific artifact integrity/privacy still depends on the user's profile access
controls and existing worker design. CI fixture computation, native controls, real
production scientific computation and installed-model/device qualification remain
separate forms of evidence.

## Tests Required

The scoped phase-exit requirements above have actually executed. Before broad release,
complete the physical accessibility, resource/low-memory/crash/hostile-filesystem and
installed-model qualification already tracked by the repository. Do not rerun or
relabel old failures merely to obtain a score, weaken gates, or infer scientific
accuracy from this project-management pass.

## Final Verdict

**PASS at 96/100 = 9.6/10 for the bounded explicit research-project development
slice**, with zero confirmed unresolved critical defects. Both required Windows
scripts pass on the exact reviewed source, and the real native/process/control
outcomes are independently observed. Preserve the initial incomplete verdict,
reopen regression and failed native-layout receipts. Keep PR #3 reviewable as a
stacked draft; this verdict neither merges dependencies nor certifies release,
physical interaction, a main-Chat bridge or the larger selected modules.
