# Testing

## Research projects — final development evidence, 2026-10-19

Reviewed executable source **`c8da7e5664faec3f8d5430bec0ab018aa8512f42`**;
[stacked draft PR #3](https://github.com/calebaiken/mu-desktop-assistant/pull/3),
base `376c320`. Independent [final review](docs/qa/RESEARCH_PROJECTS_REVIEW_WINDOWS.md):
**96/100 = 9.6/10 PASS**, zero confirmed unresolved critical defects in this bounded
B development slice. Physical/resource/model/release qualification remains separate.

| Executed check | Actual result | Receipt under `artifacts/qa/research-projects/` |
|---|---|---|
| Windows push 39301163143 / PR 39301178509 | Both jobs successful; each positive and negative verification step successful | `windows-final-code-{push,pr}-jobs.json`; independent `windows-final-{push,pr}-job.json` |
| Actual Windows TRX-derived annotations | 493 total; 492 executed/passed; zero failed; one NotExecuted (unsupported-platform assertion) | `windows-final-code-push-annotations.json`, `layout-annotations-pr.json`; independent final annotations |
| Named native integration methods | Real project worker routing plus all three A file/lease/link cases Passed | Same annotations; exact method identities, not inferred counters |
| Actual WPF project control smoke | Completed True; all 21 checkpoints passed, including minimum-size control bounds and closing/reopening/cancelling the original job | Same annotations; JSON/proposal and active-job service fixtures explicitly identified in the guide |
| Final Release cross-build, Linux SDK10.0.401 | Zero warnings/errors, exit0 | `build-layout-final.log` |
| New Linux project cases | 51 passed, zero failed, one Windows-only skip, 52 total | `research-projects-layout-fix.trx`, `tests-layout-fix.log` |
| Independent Linux project cases | 40 passed, zero failed, one Windows-only skip, 41 total | `independent/layout-independent.trx`, `.log` |
| Architecture | 11/11, root and independent execution | `architecture.log`, `independent/architecture-repaired.log` |
| Prior-A Linux regression | 40 passed, three native skips, 43 total | `trusted-action-regression.trx` |
| Production Python ML | 6+16+7+19 existing cases plus one real two-project subprocess case =49 passed | `ml-{author,data,pipeline,engine}.log`, `python-projects-final.log`; independent production worker log |

Detailed blob logs/TRX still cannot be downloaded under the cloud destination policy.
The counts, selected native method outcomes and 21 UI checkpoint outcomes above are
actual bounded observations published by the tested, additive CI reporter and fetched
through supported check annotations. Required gate status is separately confirmed
from run/job metadata. No DataRow values, error messages, stack/stdout, private paths
or dataset contents are published. The reporter does not override gate conclusions.
Native worker routing uses a real owned stdlib fixture; scientific isolation uses the
actual Python worker and installed pinned Linux stack. Neither qualifies physical
input/accessibility, model inference/coexistence or broader crash/resource behavior.

### Earlier research-project development receipts (preserved)

The following records describe their earlier checkpoints; pending/failure statements
here are historical. The final evidence above supersedes their qualification status
without changing or deleting any original measurement or failed review.



Base `376c3206a38fa86b1639e5f72c6a67cc57153cb4`; branch `feature/research-projects`.
The [project guide](docs/RESEARCH_PROJECTS.md) documents the observable workflow and
the independently [frozen criteria](docs/qa/RESEARCH_PROJECTS_PROTOCOL.md) cover only
this bounded increment. Candidate verification is in progress, not yet a phase exit.
The [initial independent review](docs/qa/RESEARCH_PROJECTS_REVIEW_INITIAL.md) is
**8.6/10, FAIL / incomplete phase exit** for missing actual native evidence;
the numerical threshold alone does not establish phase approval.

Receipts under `artifacts/qa/research-projects/`:

- `build-candidate.log`: serialized Release cross-build, SDK10.0.401/Linux,
  `EnableWindowsTargeting=true`, zero warnings/errors. A prior overlapping developer/
  reviewer build failed because intermediate DLLs were being regenerated; its
  `build-native-smoke.log` is retained and final builds are serialized.
- `python-projects-first.log`: initial test fixture incorrectly expected exit1 for
  a declared worker error; main's protocol exits0 with `ok:false`. Corrected the
  assertion without changing the worker or adopting PR #1's transport changes.
- `python-projects-final.log`: real production-worker subprocess test passes actual
  train/list/describe/export isolation, cross-project retrieval denial and unchanged
  source data. No mocked model computation is substituted for that check.
- `research-projects-author.trx`, `tests-author.log`: 51 new project cases,
  **50 passed / 0 failed / 1 Windows-only process skip** on Linux. Independent
  reproduction is 39 passed / 1 native skip of its 40 cases; receipt
  `independent/checkpoint-independent.trx`. Name/surrogate fixture corrections and
  earlier discovery/build overlap receipts remain retained there.
- `architecture.log`: 11/11; `trusted-action-regression.trx`: 40 passed / 3 explicit
  native skips of 43 prior-A cases. Existing worker suites (`ml-author.log`,
  `ml-data.log`, `ml-pipeline.log`, `ml-engine.log`) pass 6+16+7+19=48 tests;
  with the new production-worker case, 49 actual Python tests pass.

Subsequent repair receipts: `build-diagnostics.log` is zero warnings/errors;
`research-projects-reopen.trx` is **51 passed / 1 native skip of 52**. Independent
review found and documented the close-view/reopen-active-job regression after the
initial checkpoint; `Current`/App resume and native cancel-control diagnostics now
cover the repaired invariant. A nonportable expected test path was also canonicalized.
The old `9ed4f51` push run 39298675100 failed the positive step; negative was skipped.
Its aggregate exit1 annotation is preserved in `first-windows-annotations.json`;
precise test failure attribution remains unconfirmed because blob downloads fail.

`scripts/write-verification-summary.ps1` is an additive always-run diagnostic step,
without changing the required gates. A real-TRX local harness verified actual counts,
class/method-only output (synthetic private DataRow sentinel absent), fixed smoke
labels, DTD rejection and explicit missing evidence; `summary-real-trx.md` is retained.
XML is limited to 8 MiB, smoke receipts to 8 KiB, individual method/checkpoint lists
and total summary characters are bounded. No errors/stack/stdout or private fixtures
are published. Native evidence/re-review of the repaired source is still pending.

Actual `72206a0` Windows annotation (`diagnostic-annotations-push.json`, run
39300426711, job118223866106) reports **493 total / 492 passed / 0 failed /
1 NotExecuted**. The sole skip is the Linux-only unsupported-platform assertion.
The real Windows research-worker routing test and all three text file/lease/link
integration methods explicitly **Passed**. Overall positive verification still
**failed**, and negative verification was skipped, because the native research smoke
failed `minimum layout actions` after its first four checkpoints. Preserve this
failed measurement; unit success is not a gate pass.

The smoke now shows a nonactivated transparent picker at its actual minimum size
and awaits dispatcher layout rather than measuring an unshown Window. Picker/Lab
initialization share a Task so Loaded and explicit awaits cannot race. Proposal
checks await the two initial metadata jobs and prove no additional job is admitted
by planning. Minimum button size and within-window assertions and the 15s gate are
retained. A single escaped bounded notice annotation exposes the same redacted
facts through the supported checks API when summary pages/downloads are unavailable.
The local summary harness's failure receipt is synthetic; it claims no native run.

The ordinary Windows `--smoke` path now also creates project picker/Lab HWNDs and
exercises real catalog/session controls, exact identity/storage preview, proposal
without execution, fresh view state, stale-project denial and production missing-
runtime recovery. Its model JSON is an explicit fixture, not installed-model
inference qualification. A Windows-only real owned-worker routing test reports an
honest skip when its native/interpreter prerequisites are missing. Native execution,
physical interaction/accessibility and detailed conditional outcomes must be reported
from actual evidence; existing Linux failures/history below remain unchanged.

## Approved text inspection — 2026-10-19

The new panel, contracts and native adapter are described in
[the action guide](docs/TRUSTED_TEXT_INSPECTION.md). Local evidence below is from
Debian Linux with SDK 10.0.401 and `EnableWindowsTargeting=true`. Windows CI evidence
is attributed separately to its exact source/run. Initial failures remain retained.

| Check | Actual current result | Receipt under `artifacts/qa/` |
|---|---|---|
| Baseline `pwsh -File scripts/verify.ps1` | Restore/build/11 architecture pass; tests 344 passed, 23 Windows failures, 31 skipped; WPF not reached | `trusted-text-20261019/baseline-verify.log`, `baseline.trx` |
| Candidate Release cross-build | Zero warnings/errors | `trusted-text-20261019/build-checked.log` |
| New policy/decoder/real-stream/Windows integration selection | 40 passed, 0 failed, 3 Windows-only skipped, 43 total | `trusted-text-20261019/targeted-final.trx` |
| Independent rerun of same 43 cases | 40 passed, 0 failed, 3 skipped | `trusted-text/independent/independent-trusted-actions-unicode-final.trx` |
| Independent architecture check | 11/11, exit 0 | `trusted-text/independent/architecture-final.log` |
| Candidate full Linux gate before final additional Unicode-path case | 383 passed, same 23 baseline failing names, 34 skipped, 440 total; WPF not reached | `trusted-text-20261019/final-full-gate.log`, `final-full-gate.trx` |
| Portable existing ML suites | 48 passed: author 6, independent data 16, pipeline 7, engine 19 | `trusted-text-20261019/ml-{author,data,pipeline,engine}.log` |
| Direct WPF launch on Linux | Exit 150: Microsoft.WindowsDesktop.App unavailable | `trusted-text-20261019/native-unavailable.log` |
| Final stronger native checks, local selection | Release zero warnings/errors; 40 passed, 0 failed, 3 Windows-only skipped, 43 total | `trusted-text-20261019/build-native-evidence.log`, `targeted-native-evidence.trx` |

Windows CI on `a999ae4f59b16a9e435185726a647668a5aac51f` passes **2/2 checks**:
[push run 39293630026](https://github.com/calebaiken/mu-desktop-assistant/actions/runs/39293630026)
and [PR run 39293637612](https://github.com/calebaiken/mu-desktop-assistant/actions/runs/39293637612).
The exact-head public checks payload identifies both `verify` jobs as succeeded;
the immutable unchanged workflow runs `scripts/verify.ps1` followed by
`scripts/verify-negative.ps1` without continue-on-error. This executes the actual
production-reader/native-HWND/control smoke and the normal Windows file identity/
sharing/ancestor-rename assertions. This is real remote Windows evidence, not a
Linux simulation. The preceding `d2607b6` Windows runs also succeeded, and their
individual positive/negative step success was independently read through REST.

Detailed log/TRX downloads redirect to a storage host denied by the runtime network
policy. Exact Windows case counters and the conditional symlink fixture's actual
pass/skip outcome are therefore **not reported**. Public exact-head status receipt:
`trusted-text-20261019/windows-final-public-status.json`. Full gates succeeding does
not substitute for physical picker/keyboard/screen-reader or resource measurements.

The full required Windows gate is `scripts/verify.ps1` then
`scripts/verify-negative.ps1`. The composed `--smoke` now includes the actual WPF
inspection panel and production file reader with an isolated public fixture.
Windows tests exercise locked target identity, post-read lease release, redacted
real log storage, oversized files and leaf/ancestor links. Link fixture creation
requires Developer Mode or the Windows symlink privilege. Unsupported-platform
denial has a separate Linux test. Skips do not count as native success.

Independent tests include exact monotonic expiry under UTC rollback, same/foreign
grant replay, stale resources, cancellation/disposal, audit errors and audit races,
noncooperative late callbacks, redacted/literal results, malformed inputs and Unicode
boundaries. Physical picker/keyboard/high-contrast/accessibility, adversarial native
ancestor swaps and full workload resource qualification remain required. Future
source changes retain the complete Windows gates.
The independent protocol and initial failed review are under `docs/qa/`; a passing
Linux suite alone cannot advance the phase. Historical receipts below stay unchanged.
The independent Linux checkpoint in `docs/qa/TRUSTED_TEXT_INSPECTION_REVIEW_LINUX.md`
scores **8.2/10, FAIL / incomplete qualification** under the unchanged >8.5 gate.
The subsequent exact-code [Windows review](docs/qa/TRUSTED_TEXT_INSPECTION_REVIEW_WINDOWS.md)
scores **94/100 = 9.4/10, PASS** for the bounded A development slice, with zero
confirmed unresolved critical defects. Its detailed evidence provenance and remaining
deductions are explicit; no precise Windows counters or conditional symlink result
were invented from aggregate CI success. This does not certify release or other modules.

## Optional neural voices — 2026-09-30

This unit extends explicit local speech, without opening a microphone or completing
the full voice loop. Receipts live under `artifacts/qa/neural-voice-review` unless a
full path below specifies another isolated run. See the
[independent review](docs/qa/MU_NEURAL_VOICE_REVIEW.md) for final scores and repair loops.

Receipts below record real runs on the provisioned local host, where the optional
ML runtimes under `artifacts/` exist and the full suite executes. GitHub Actions
provisions a bare interpreter at the same path and also reaches 398/398; it does
not install the pinned ML packages, since these checks import none of them. See
[AGENTS.md](AGENTS.md).

| Check | Executed result | Receipt |
|---|---|---|
| Strict final shortcut-targeted production build | 0 warnings/errors; includes Stop repair | `desktop-stable-final-build.txt` |
| Independent affected neural + preserved SAPI | 56/56; 23 new neural, 33 preserved | `termination-race-fixture-repaired.trx` |
| Full final .NET regression | 398/398; zero skipped; 29 s | `full-suite-termination-repaired.trx` |
| Architecture | 11/11; required root argument supplied on repaired invocation | `architecture-root-repaired.txt` |
| Independent Python | 21/21; real NumPy conversion, no model inference | `independent-python-quantization.txt` |
| Four real voices in native Preview | 21/21, actual production host/model; 61.257 s | `artifacts/qa/neural-speech-20260930-021140-e9038281/neural-speech-smoke.json` |
| Independent real audio oracle | Four bounded PCM16 mono 24 kHz WAVs, exact durations/hashes | `actual-public-wave-oracle.txt` and `actual-public-wave-reviewed.json` |
| Final shortcut-targeted native UI supervisor | 50/50; two retained runs, each with 94 speech checks | `native-ui-stable-final.txt`; `artifacts/qa/desktop-ui-20260930-114805-9e0138dc/first-run` and `profile` |
| Existing Desktop shortcut readback | Rebuilt executable/local model arguments verified; shortcut preserved | `shortcut-final-readback.json` |

The independent .NET cases exercise closed commands/catalogs, exact audio/time
arithmetic, invalid/tampered/missing input, true held-process cancellation/disposal,
output floods, unrelated worker preservation and operation-owned TEMP cleanup.
New preview checks preserve all 84 original native speech assertions. They execute
real WPF windows and controlled providers, not physical input or human listening.

The actual four-voice Preview run synthesizes the same fixed public introduction
with each curated voice at rate zero. An explicit inaudible production probe
replaces playback for that run, so its valid audio/dispatcher evidence does not
alone prove output-device playback. The exported public WAV files are retained
only by this isolated diagnostic. Normal speech saves no WAVs. The independent
oracle verifies 269,887 / 277,679 / 305,695 / 300,847 frames and durations of
11.245292 / 11.569958 / 12.737292 / 12.535292 seconds. Later actual speech may yield
different PCM hashes; these are attempt-specific receipts, not a bit-identical
inference promise or a human-naturalness benchmark.

Retained native failures: `015934-0439993c` stopped after four preliminary checks;
`021327-407a5987` and `021546-3d22f3e6` failed between inaudible probes;
`022828-a14d8eb2` reached 24/25, including actual Speaking, held-worker Stop and
native idle recovery, then failed the short reuse reading. None is a 25-case pass.
Their full directory names begin `artifacts/qa/neural-speech-20260930-`.
The separate `artifacts/qa/neural-speech-lifecycle-20260930-023218-bc651ea8`
normal-Speak harness exposed CleanupFailed after six checks. The ensuing narrow
termination-race repair confirms held-child exit within the original remaining
three seconds before accepting a racing Kill error. The original failed receipts
and their temporary-directory evidence are preserved.

After that repair, the final 398-test suite and affected 56-test suite pass,
including 24 real held-process cancellation/error-drain/reuse iterations and
32 actual job-termination scheduling races under the original three-second bound.
The real model reruns at `024351-86635676` and `114316-407af385` each stopped
at five of 33 checks with the exact content-free `memory_limit` code. The final
directory is `artifacts/qa/neural-speech-lifecycle-20260930-114316-407af385`.
This proves safe refusal, not successful generation/cancellation/playback reuse.
The final numerical review does not waive this **INCOMPLETE** functional gate.
User applications were not closed by Mu; unproven background-process ownership
did not authorize terminating them. Qwen/neural co-residency was not attempted
under the measured commit shortage.

The reviewer initially used a 66,000-byte probe flood, below the six-MiB probe cap.
That test's own guard expired; the fixture was repaired to cap plus one byte,
without weakening the production cap or timeout. Setup's initial PS5 quoting
verification failure and its repaired idempotent run remain under
`artifacts/neural-voice-research`. Final normal playback/privacy, termination-race
regression and source/binary hashes are recorded in the review's final ledger.
Missing physical hearing, subjective preference, pronunciation, device-loss,
co-resident model resources and release qualifications are not silently passed.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/setup-neural-speech.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/verify-neural-speech.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/verify-neural-speech.ps1 -Playback
```

`-Playback` deliberately uses the default output for a public sample and checks
Stop/reuse. Its aggregate supervisor permits 650 seconds; individual production
budgets remain 90 seconds generation, 30 seconds playback and three seconds cleanup.
Run model diagnostics serially on this low-memory PC. Do not attribute a new
successful run to an older failed attempt or erase failures from the ledger.

## Mu identity and optional LAYA evidence — 2026-09-30

Current build: `artifacts/Mu/Companion.Desktop.exe`. These receipts qualify avatar/
icon/UI and the advisory boundary; **full pretrained LAYA inference is incomplete**.
All paths below are relative to `artifacts/qa` unless stated otherwise.

| Check | Executed result | Receipt |
|---|---|---|
| Strict production build | 0 warnings/errors | `identity-laya-review/desktop-icon-repair-build.txt` |
| Full .NET regression | 375/375, 0 skipped | `identity-laya-review/full-suite-reviewed.trx` |
| Architecture | 11/11 | `identity-laya-review/architecture-final.txt` |
| Independent LAYA Python | 12/12; no pretrained inference | `identity-laya-review/python-independent-admission-final.txt` |
| Native UI supervisor | 44/44; two retained runs | `identity-laya-review/native-ui-icon-repaired.txt` |
| Avatar customization | 315/315 in each native run | `desktop-ui-20260930-005702-9eb5cf24/first-run/avatar-customization-smoke.json` and `profile/avatar-customization-smoke.json` |
| Controlled LAYA UI | 40/40 in each native run | Same `first-run/` and `profile/` directories, `laya-ui-smoke.json` |
| ICO decoding / PE extraction | All seven frames decoded; actual executable icon inspected | `identity-laya-review/icon-decoded.json`, `executable-icon.json` |
| Shortcut | Scratch and actual Desktop creation/property verification passed | `identity-laya-review/shortcut-fixture-create.txt`, `desktop-shortcut-create.txt` |
| Actual LAYA production host/native view | **FAIL: memory_limit**, 3 preliminary checks; no model output; 1.65 s | `laya-runtime-20260930-010310-74c36871/laya-runtime-smoke.json` |

The tiny seeded real ModernBERT/LAYA-head float16 CPU check passed in 15.026 seconds
(`laya/tiny-precision-runtime-seeded.txt`). It verifies numerical execution of that
policy on a tiny synthetic model, not the downloaded checkpoint or domain accuracy.
The full-model diagnostic must pass its 16 checks before inference is qualified:

```powershell
.\scripts\verify-ui.ps1 -ApplicationPath .\artifacts\Mu\Companion.Desktop.exe
.\scripts\verify-laya.ps1 -ApplicationPath .\artifacts\Mu\Companion.Desktop.exe
```

The initial `identity-laya-review/real-host-native-laya.txt` text capture is empty
and is retained as a logging failure, not evidence. The actual native JSON above
records the run's failure and elapsed time. The independent review scores this
bounded work 88/100 (8.8/10), **overall INCOMPLETE** because successful full-checkpoint
inference remains a functional gate; the identity/customization scope is delivered.

Native fixtures use actual WPF windows/dispatchers and isolate profiles. They do not
certify physical mouse/keyboard travel, screen readers, multiple displays or an idle
CPU budget. Synthetic held Python workers separately test actual cancellation and
disposal ownership. No test opens the microphone or uses user dataset content.

Retained failures include the initial hover gap (`desktop-ui-20260930-004141-3ed7cbed`),
expanded minimum-window action clipping (`004749-16bd2937`) and missing derived-window
icon (`005356-6d7b0e51`); repairs reran the original assertions. Prior full-model
float32/commit failures and setup disk exhaustion are preserved under `laya/`.
The repaired legacy-name test follows a retained 374/375 run. These failures were
not recategorized as passes. See the [independent review](docs/qa/MU_IDENTITY_LAYA_REVIEW.md).

## ML foundation evidence — 2026-09-29

This is the integrated bounded ML foundation, not full requested ML Phase 1 or a
release approval. Final receipts preserve the scope of each run:

| Check | Executed result | Retained receipt under `artifacts/qa` |
|---|---|---|
| Strict candidate production build | 0 warnings, 0 errors | `ml-foundation/build-candidate.txt` |
| .NET unit/integration/adversarial | 353/353; 0 skipped/inconclusive | `ml-foundation/ml-reviewed-final.trx`, `final-reviewed-dotnet-tests.txt` |
| Architecture | 11/11 | `ml-foundation/final-architecture-repaired.txt` |
| Independent Python | 46/46 full suite plus 19/19 expanded engine run; 48 distinct covered cases, not 65 unique tests | `ml-foundation-review/independent-final-run.txt`, `engine-final-negative-run.txt` |
| Author Python | 6/6; loops cover 22 task/algorithm pairs; 20.209 s | `artifacts/ml-author-tests/author-suite-foundation.txt` (outside `artifacts/qa`) |
| Native Lab | 57 assertions; failure=null; 137.904 s | `ml-native-20260929-230409-e1b0e71e/ml-lab-smoke.json` |
| Live local planner | 2/2 public prompts through unchanged production transport; no proposal executed | `ml-native-20260929-230409-e1b0e71e/ml-planner-smoke.json` |
| Existing desktop UI regression | 38/38 supervisor checks | `desktop-ui-20260929-230137-f73c8c5b/profile`, `ml-foundation/existing-ui-regression.txt` |

The current author receipt is `artifacts/ml-author-tests/author-suite-foundation.txt`:
6/6 in 20.209 seconds. The older `author-suite-final.txt` contains the preceding
five-case run and does not qualify the later comparison test.

The native suite exercises a real WPF HWND and local workers: minimum geometry,
dispatcher heartbeat, inspection, regression/classification/CV/PCA/clustering,
prediction failure/recovery, full historical detail, exact-cohort comparison,
plots, queue/running cancellation, reuse and reopening. It is not physical input,
screen-reader, multi-monitor or general scientific-validity qualification. The two
live prompts check current-selection inspection and PCA without scaling; they are
not a general language-understanding benchmark. The earlier 48- and 52-check native
passes, failed planning attempts and diagnostic tee-assisted probe remain historical
artifacts; the final planner receipt uses the unchanged production transport.

Host tests execute synthetic workers through the real job/stdio adapter, covering
output floods followed by a deliberate sleep, malformed replies, missing runtime,
already-cancelled admission, cancelled-middle FIFO ordering, memory denial and held-
worker concurrent disposal. Only missing optional runtime makes applicable tests
inconclusive; the final 353-test run has none. The earlier 346-test receipt is retained
as its own checkpoint. `final-structured-tests-build.txt` also records zero warnings
and errors; `verified-source-manifest.json` identifies the final validated source.
No test opens the microphone.

```powershell
.\scripts\setup-ml-lab.ps1 -Python C:\path\to\python.exe
& .\artifacts\ml-runtime\Scripts\python.exe -m unittest discover -s tests/ml_lab -p 'test_independent_*.py' -v
& .\artifacts\ml-runtime\Scripts\python.exe workers/ml_lab/test_author.py
dotnet test tests/Companion.Tests -c Release --no-build --filter FullyQualifiedName~MlWorkerHostTests
.\scripts\verify-ml-lab.ps1 -SkipBuild -LocalModelDirectory .\artifacts\local-ai
```

Use the matching freshly built executable. The normal output was locked by an older
open Mu, so final validation used `artifacts/qa/ml-candidate/Companion.Desktop.exe`.
The ML and existing-UI scripts accept `-ApplicationPath` for this candidate. Do not
attribute a candidate receipt to the older normal binary. Close the older app before
launching the candidate or rebuilding the usual output. Full pre-existing release
gates and voice-input nonqualification below are unchanged. The independent
[ML review](docs/qa/ML_LAB_FOUNDATION_REVIEW.md) records repairs, remaining scope and score.

Phase 3 verification covers bounded local SSE/context admission, owned worker lifetime,
SQLite history and branch recovery, separate temporary sessions, native Markdown/math
rendering and the compact reply preview. The first history regression passed 267/267
tests; that retained artifact predates later rendering/recovery/preview edits and is
not a pass claim for the final source. The [Phase 3 review](docs/qa/PHASE_3_HISTORY_REVIEW.md)
tracks executed artifacts, failed attempts and its final **9.5/10 development verdict**.
That result belongs to Phase 3; the Phase 4A speech increment independently passes at
**9.0/10** for manual output only. Earlier
224-test local-chat and compact-surface evidence keeps its original, narrower scope.

`scripts/verify-local-model.ps1` separately runs the
[frozen real-model protocol](docs/qa/PHASE_3_LOCAL_MODEL_PROTOCOL.md), retaining binary
identities, public fixture responses, errors, actual token-admission observations,
cancellation and worker cleanup/restart outcomes in a unique directory. It requires
`scripts/setup-local-ai.ps1` first. The expanded recorded run in
`artifacts/qa/local-model-20260922-214337-9c4ffcd3` reports protocol PASS; its small public
prompt set does not establish broad assistant quality, and manual content criteria
remain distinct from structural protocol checks.

`scripts/verify-ui.ps1` requires two native runs with 54 chat lifecycle, 50 history,
46 document and 34 reply-preview assertions alongside earlier desktop suites. These
are expected counts enforced by the current supervisor, not proof that a particular
binary completed them. Native method/property checks do not establish actual
keyboard/mouse or screen-reader usability. Renderer notation is not numerical validation.

Phase 4A passes **84 native fake-provider UI checks in each of two retained runs**
and **38/38 UI supervisor cases** in `desktop-ui-20260922-224213-17740e63`.
Its first expanded attempt exhausted the former 60-second
aggregate deadline after preceding suites consumed 52.516 seconds; that failed
attempt is retained. The passing attempt declared 120 seconds overall, added content-free
substage timing and retained all individual three-second waits. The UI checks cover
explicit start/stop, completed-reply eligibility, no autoplay, literal/Unicode-bounded
text, voice/rate selection, cancellation/late callbacks, native layout and accessible
names. They neither send audio nor establish physical input, hearing or device behavior.
The [frozen Phase 4A review protocol](docs/qa/PHASE_4A_SPEECH_REVIEW.md) governs required
native-engine, privacy, fault and lifecycle evidence; its **90/100** verdict approves
only this output slice. Full Phase 4 and release qualification remain unfinished.

Strict speech regression passes **305/305 tests**, **11/11 architecture checks**,
composed WPF smoke and zero warnings/errors. Native memory synthesis passed in
`speech-20260922-223108-36a69151`; explicit default-output Stop/reuse/deadline passed
in `speech-20260922-223856-66dd24ca`. Stop acknowledged in 0.0038051 seconds; the
30-second deadline ended at 30.0156752 seconds, with held worker exits confirmed.
The abrupt temporary-speech sentinel scan passed in
`private-speech-20260922-223824-cd48804a`. All paths are under `artifacts/qa`.
Physical hearing, full App Sleep/input integration, the exact sentinel's normal
exit scan and the frozen 60-second idle/ten-cycle protocol remain unexecuted.

`scripts/verify-speech.ps1` creates a unique isolated profile and retains binary/protocol
hashes and all outcomes. Its default diagnostic is **inaudible**, synthesizing public
fixtures into bounded memory and retaining metadata only. Explicitly adding
`-Playback` enables default-device audio for the public playback/Stop cases. Successful
engine events or PCM metadata do not prove a listener heard a start or stop; retain
actual observer notes separately. The supervisor's process deadline is 120 seconds;
the production per-operation and cleanup limits remain 30 and 3 seconds.

`scripts/verify-history-privacy.ps1 -Speech` starts a fresh profile, writes a real saved
SQLite decoy, then holds a fake explicit speech operation on a temporary public sentinel.
The app allows 10 seconds for readiness; the external supervisor allows 15 seconds for
the marker, terminates only its owned process and scans every bounded profile file
and sidecar for UTF-8/UTF-16LE/UTF-16BE sentinel bytes. This is whole-app voice UI
persistence evidence with no audio, not evidence about a third-party speech engine,
OS paging or forensic erasure. The existing history-only mode remains available.

Use Windows 11 x64 and .NET SDK 10.0.401. All builds enable nullable checks, built-in
analyzers, deterministic output and warnings as errors. Core and Application remain
package-free. Infrastructure owns Microsoft.Data.Sqlite and System.Speech 10.0.12; Desktop owns Markdig
1.3.2 and WpfMath 2.1.0. Their exact resolved transitives and content hashes are locked.
MSTest TestFramework/TestAdapter 4.4.1 and Microsoft.NET.Test.Sdk 18.10.0 remain pinned
test-only dependencies. See the [complete dependency inventory](docs/DEPENDENCIES.md).
NuGet vulnerability auditing remains on.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1 -Configuration Debug
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-negative.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-ui.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-speech.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-history-privacy.ps1 -Speech
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/measure-desktop.ps1
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

Phase 2 adds independent integer-lattice placement and rational-coordinate oracles,
state priority/lease/sleep/concurrency checks, appearance migration/ranges, placement
schema/recovery/conflict/cancellation tests and startup command validation. The latest
13 registry integration cases use only validated unique HKCU test keys and all pass.
Startup tests never write the user's Run value or simulate a Windows sign-in.
`verify-ui.ps1` uses a 60-second per-process deadline for expanded real-HWND checks,
including eight explicitly labeled previews, native capture/restore/fallback, pending
move–Hide–Exit persistence and native click-through/tray/hotkey conflict/release.
It retains stage timings and named assertions. Shortcut subchecks report a limitation
if the user's applications already occupy all presets; a skipped case is not a pass.
Monitor-cycle policy is independently tested; Ctrl+Shift+M and the next-display action
share two-step native placement. The latest native run observed one display at 120 DPI,
so its next-display assertion verifies a no-op, not physical cross-display movement.

`measure-desktop.ps1` uses an isolated profile and opt-in `--performance-smoke`: 10 s
warmup, then 60 s each of visible animation, reduced motion and hidden state. It
records actual animation flags, process CPU and private/working-set bytes through native
counters, OS/runtime, DPI and reference hardware. The supervisor independently recomputes
CPU percentages. Limits are strictly below 1% of one logical processor over 60 seconds
and 150,000,000 private bytes; machine-normalized CPU is informational. Calibration is
never subtracted. The supervisor retains raw artifacts and fails on a breached budget.
No forced GC, OS preference change or tray is involved. This is a process-counter
baseline, not frame-time, compositor/GPU, startup-percentile or long-run leak evidence.

The retained final Phase 2 Release verification has **196/196 tests**, 11 architecture checks,
composed smoke and zero warnings/errors, now including the final-source readiness
fixes. The matching TRX is archived at
`artifacts/qa/desktop-ui-20260922-185538-0c91126a/release-196-before-final-build-confirmation.trx`.
Three added independent placement tests verify a
cancelled queued waiter does not cancel its owner or poison later saves, cancellation
after atomic commit reports success, and a failed writer preserves the known-good
baseline for retry. The earlier pre-observer 196-case TRX is preserved at
`artifacts/qa/desktop-ui-20260922-183217-1d198cb8/release-196-before-observer-abba.trx`;
the historical 193-case artifact remains retained with its original scope.
The corresponding Phase 2 native artifact, `artifacts/qa/desktop-ui-20260922-185538-0c91126a/profile`,
has **18/18 process controls, 43 appearance, 24 integration, 29 settings lifecycle
and 49 placement-initialization assertions**, with no skips or reported failures. Controlled pending
saves cover committed, cancelled and I/O-failed completion during shutdown: committed
data remains durable, the companion is not reapplied, native effects/status changes
do not run late, and controls remain disabled. Hidden/minimized settings reopening
also passes in both application runs. Timed
eyelid property transitions prove completed idle blinks; interrupted blinks do not
count. Idle owns no animation clocks, active expressions own at most two controlled
clocks capped at 20 FPS, and stopping releases timers/clocks. These behavior checks
cover the current ten-second blink cadence and 120 ms closure, using bounded 13-second
blink waits. That Release build has zero warnings/errors. The single clean
resource run for this cadence completed with the unchanged limits and failed visible CPU.

Both controlled resource runs of the earlier renderer, `performance-20260922-172039-f052a9fb` and
`performance-20260922-172825-fc26bc57`, pass memory and fail all CPU intervals. The latter
records 5.260228% visible, 2.708053% reduced-motion and 2.707961% hidden one-core CPU.
Peak private bytes are 65,101,824 / 65,290,240 / 62,021,632 respectively. The original
`performance-20260922-164711-eccda553` remains rejected because interactive settings
changes contaminated its intervals. Diagnostic traces include profiling overhead and
cannot replace a clean budget run. See the [independent follow-up review](docs/qa/PHASE_2_FOLLOWUP_REVIEW.md).

The six-second-blink run, `performance-20260922-175003-37870412`, is a valid **failure**:
visible CPU is 1.093335%, reduced-motion 0.494627% and hidden 0.416623%. Reduced/hidden
CPU and all memory intervals pass; peak private bytes are 63,504,384 / 65,265,664 /
63,492,096. Actual completed blinks are 10/0/0 with zero owned animation clocks.
Combined blink/lifetime changes improved the reduced/hidden floor, but a single cause
is not established. No overhead is subtracted. The ten-second production decision was
recorded before its single measurement, preserving this result and earlier failures.

The pre-experiment ten-second run, `performance-20260922-175657-e7f2743d`, **fails**:
visible CPU is 1.041400%, reduced-motion 0.520719% and hidden 0.390566%. Reduced/hidden
CPU and all memory intervals pass; peak private bytes are 62,914,560 / 64,225,280 /
66,539,520, with 6/0/0 completed blinks and zero owned clocks. The supervisor exits 1
and preserves `budget-evaluation.json`. The cadence is frozen; no unchanged rerun,
threshold relaxation or overhead subtraction is used to manufacture a passing result.
The resulting historical review scored **8.6/10** with a failed phase exit. That
report and its resource failure remain unchanged by subsequent experiments.

The [observer protocol](docs/qa/PHASE_2_OBSERVER_PROTOCOL.md) declares exactly **A/B/B/A**
before execution: A is `measure-desktop.ps1 -Observer threads` (the legacy default),
with two nonempty boundary thread snapshots per interval; B is `-Observer process`,
which omits only those snapshots. Both use the same ten-second blink cadence, warmup,
sixty-second intervals, native one-second counters, input freeze and calibration.
No calibration or observer cost is subtracted. `measure-observer-comparison.ps1`
requires the explicitly reviewed `-ExpectedProtocolSha256`, freezes five binary and
protocol/supervisor hashes, uses four fresh profiles, and retains complete reports
and budget results. Technical failures stop without substitution; budget failures
remain valid results and never trigger retries. Acceptance requires both B runs to
pass all intervals after all four valid runs complete. All A/B results must be
reported, without claiming statistical significance or a product optimization.
The completed comparison is
`artifacts/qa/observer-d217a835e8bc4ec9b6f27b98637024fd/comparison.json`: all four runs
are valid and all CPU/memory intervals pass. Visible one-core CPU is 0.885254% / 0.989278%
for A and 0.755076% / 0.703079% for B. These results belong to the recorded earlier binary;
they do not establish an isolated product optimization or statistical significance.

The [final-build protocol](docs/qa/PHASE_2_FINAL_BUILD_PROTOCOL.md) declares exactly
two B runs after startup-placement and confirmed-shortcut recovery fixes. Both must
pass every unchanged limit with frozen binary/protocol/script hashes and no overhead
subtraction. Use the comparison supervisor's `-FinalBuild` mode with the reviewed
protocol SHA256. Technical failures stop without substitution; valid budget failures
remain retained. The final-source strict build, 196-test suite and 18/43/24/29/49 native
checks pass. Final measurements are retained at
`artifacts/qa/final-build-1ca3041a363849aabc6b2c4e2170ce6f/comparison.json` and the aggregate
result is **FAIL**: both valid B runs had to pass, but only the second does.

| Final-build B run | Visible CPU | Reduced-motion CPU | Hidden CPU | Peak private bytes, corresponding modes | Result |
|---|---:|---:|---:|---|---|
| `performance-20260922-185649-ab4dbf46` | 1.978694% | 1.015400% | 0.364563% | 64,974,848 / 65,224,704 / 62,337,024 | Visible/reduced CPU FAIL; hidden CPU and all memory PASS |
| `performance-20260922-190004-0f75069d` | 0.755208% | 0.598859% | 0.364527% | 63,119,360 / 64,102,400 / 63,520,768 | All intervals PASS |

Each records 6/0/0 completed blinks and zero owned clocks. The first failure, exact
source/binary attribution and aggregate rule remain unchanged; no substitute run,
threshold relaxation or overhead subtraction follows. The independent development
review passes at **8.7/10** under the explicitly post-result
[resource sequence decision](docs/qa/PHASE_2_RESOURCE_SEQUENCE.md). Performance stays
**7/10 with zero points for the failed budget criterion**; this experiment remains FAIL.

The first new fixture build had a compile error, now corrected. Native attempt
`desktop-ui-20260922-185315-2646a9cb` completed 43/24/29 checks, then failed placement
assertion 42 because it compared size after warning text resized the window. The
fixture now compares Left/Top; that correction changed no production code. The failed
artifact remains retained; the subsequent full native run passes. No concurrent builds,
app tests, traces or companion input are allowed during frozen resource sequences.

The [validation sequence decision](docs/qa/PHASE_2_VALIDATION_SEQUENCE.md) moves the
unavailable physical display/input/accessibility and actual sign-in matrix to mandatory
Phase 11/12 pre-release qualification. The user has no second monitor and the supported
native helper remains unavailable. These cases remain unexecuted, receive no passing
credit, and must be completed before release. Phase 2 development completion does not
approve release. Phase 12 must use a separately predeclared repeat protocol with actual
presentation/occlusion, warmup, observer cost and scenario state recorded. The unchanged
limits are strictly below 1% of one logical processor and 150,000,000 sampled private
bytes; retain every valid failure and exact binary identity without overhead subtraction.

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
reopen/relaunch, activate an existing instance and Exit. Phase 1's completed input
evidence remains in `docs/qa/PHASE_1_REVIEW.md`. During resumed Phase 2 validation, the
supported native helper pipe remained unavailable (OS error 2) after updated skill
guidance and a fresh session reset; no actual
mouse/keyboard actions were attempted. The process smoke alone does not prove user input, screen
reader behavior, keyboard coverage, mixed DPI or compositor quality.

CI configuration in `.github/workflows/verify.yml` runs the positive and negative
scripts on Windows and uploads TRX. Remote execution was absent at that historical
baseline; actual later CI executions are recorded above/in the current source audit.
Visible interactive QA is local, separate from
headless CI. Do not equate a configuration file with a successful CI run.

## Dependency review

The runtime package allowlist is limited to SQLite and System.Speech in Infrastructure
and native rich rendering in Desktop. Transitive package versions, package-supplied repository commits,
declared licenses and WpfMath font notices are recorded in
[docs/DEPENDENCIES.md](docs/DEPENDENCIES.md). Production has no browser-rendering or
hosted-provider package. Core/Application dependency rules still prohibit package references.

The test stack uses Microsoft's MIT-licensed projects, compatible with this .NET target,
with test-only PrivateAssets and a dedicated source configuration.
Version sources: [MSTest framework](https://www.nuget.org/packages/MSTest.TestFramework/4.4.1),
[adapter](https://www.nuget.org/packages/MSTest.TestAdapter/4.4.1),
[test SDK](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/18.10.0).
Transitive versions/content hashes are recorded in packages.lock.json; review lock diffs.
The 2026-09-22 `dotnet list Companion.slnx package --vulnerable --include-transitive`
check completed across all six projects without a reported known-vulnerable package.
The post-speech-package approved run is retained in
`artifacts/qa/phase4a-package-advisories.txt`; the first restricted invocation exited 1
without output. It was not a passing check, and the advisory scan was not rerun for
these documentation edits.
This is a query of the configured NuGet advisory feed, not a source audit, exploitability
assessment or scan of the separately downloaded model/native worker. Keep normal
restore audit enabled; report network audit failures rather than suppressing NU1900.
It does not establish absence of unknown vulnerabilities.

## Future gates

Each owning phase adds unit, integration, UI, security, recovery and performance cases.
Extend the existing mock HTTP/SSE and real SQLite integration cases with each worker/storage change; no live
paid API tests without explicit configuration. Voice requires PCM plus physical-device
fixtures. Tools require schema/permission/replay/TOCTOU/cancellation/audit tests. Temporary
mode requires sentinel scans of DB/WAL/log/cache after crash and restart. See ROADMAP.md.

Scientific code must use independent oracles, stated equations/domains/units, near-zero
absolute and relative tolerances, nonfinite and boundary tests. First-order uncertainty
uses variance(f) approximately J Σ Jᵀ, with covariance, differentiability and small-error
assumptions documented; compare nonlinear cases independently. Affine temperatures
must distinguish values from intervals. Model text never supplies the numerical oracle.
This planned general-purpose science engine is not implemented or verified; the
separate ML Lab computations and PR #1's proposed waveform work have narrower scopes.
QA arithmetic uses exact
decimals and the strict score >8.5 condition; 8.5 fails.

Tests use isolated temporary directories with guarded cleanup, never the user's actual
profile. Keep failed-review history and specific commands/results. Repository-authored
native C++ additions require sanitizer/resource checks; there are none yet. The external
llama.cpp worker has its separately pinned installation and runtime protocol checks.
Repeat relevant tests after fixes,
not to manufacture evidence by rerunning unchanged successful suites.
