# QA REVIEW

Phase: UX-0/UX-1 — supported Windows engineering checkpoint, 2026-10-20 UTC

Overall Score: **9.4 / 10**

Result: **FAIL / INCOMPLETE — required matched current-competitor comparison remains unavailable**

The independently observed current Windows functional, architecture and resource gates pass. Current native pixels and detailed input/viewport receipts are now retrieved and inspected, including the repaired error state. The numerical threshold is met, with **zero confirmed unresolved critical correctness/security/accessibility defects in the tested scope**. This does **not** pass the requested UI phase: a matched current-competitor blind comparison is unavailable. Physical accessibility/DPI and broader resource/scientific qualification remain unverified. No visual win against a current competitor is claimed.

Reviewed production source: **`cc163d6ea4afeceb34301114d0b2714119e0fabb`**. The feature branch derives from Chat handoff **`6e4ba5d5f0edd5af23eabf3493e41c07dd6b592e`**; [PR #5](https://github.com/calebaiken/mu-desktop-assistant/pull/5) is a focused draft against `feature/chat-project-handoff`. This report follows the [unchanged 50-criterion protocol](RESEARCH_PROJECTS_UI_V3_PROTOCOL.md). The [initial 65/100 review and every addendum](RESEARCH_PROJECTS_UI_V3_REVIEW_INITIAL.md), deficient captures and failed native runs are preserved. The reviewer authored separate tests/reports and made no production edits.

## Category Scores

Each raw criterion uses 0/1/2 in frozen order; ten categories retain equal weight.

| Category | Five raw criterion scores | Sum |
|---|---|---:|
| Code Correctness | C1=2, C2=2, C3=2, C4=2, C5=2 | 10 |
| Architecture | A1=2, A2=2, A3=2, A4=2, A5=2 | 10 |
| Mathematical Correctness | M1=2, M2=2, M3=2, M4=2, M5=2 | 10 |
| Testing | T1=2, T2=2, T3=2, T4=2, T5=1 | 9 |
| Reliability | R1=2, R2=2, R3=2, R4=2, R5=2 | 10 |
| Security | S1=2, S2=2, S3=2, S4=2, S5=2 | 10 |
| Performance | P1=2, P2=2, P3=2, P4=2, P5=2 | 10 |
| Maintainability | N1=2, N2=2, N3=2, N4=2, N5=2 | 10 |
| UX/UI | U1=2, U2=2, U3=1, U4=1, U5=0 | 6 |
| Requirement Compliance | Q1=2, Q2=1, Q3=2, Q4=2, Q5=2 | 9 |

Exact arithmetic: `(10 + 10 + 10 + 9 + 10 + 10 + 10 + 10 + 6 + 9) / 10 = 94 / 10 = 9.4`. This is strictly greater than 8.5. The frozen sufficient-evidence rule still leaves the phase **FAIL / INCOMPLETE**. Mathematical credit concerns identity/count/contrast bounds and review arithmetic; no scientific algorithm or model accuracy is qualified.

## Evidence

### Current supported Windows results

Independent ordinary GitHub job/annotation requests establish:

| Execution | Source/provenance | Actual result |
|---|---|---|
| Push [39344178105](https://github.com/calebaiken/mu-desktop-assistant/actions/runs/39344178105), job `118372678654` | Direct `cc163d6` source | **Success**, every step successful |
| PR [39344183029](https://github.com/calebaiken/mu-desktop-assistant/actions/runs/39344183029), job `118372695457` | PR head `cc163d6`; current merge ref `0b373ac370319cd3e2454dd2740d26265913f26f` | **Success**, every step successful |

Both executions pass the unchanged positive and negative scripts, frozen-baseline capture, new native picker verification, and companion resource measurement. Each reports **525 total, 524 executed/passed, zero failed, one NotExecuted**. The sole skip is `TextInspectionTests.NativeCapabilityIsUnavailableOnUnsupportedPlatforms`. The four named actual Windows worker/file identity/consent/link methods pass. Both retain **21 project** and **28 Chat-handoff** native checkpoints and report **112 completed independent UI checks**, no failed label/type/detail/wait, with OS/WPF high contrast true. Current push detailed keyboard/viewport/capture receipts are independently retrieved and inspected; current PR results remain API observations.

The commands remain:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-negative.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-projects-ui.ps1 -Mode candidate
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/measure-desktop.ps1
```

The PR merge object's parents are `6e4ba5d` and `cc163d6`; its tree **`24037cf32b5b587062dc016df93e679cd4249c8f`** exactly equals the direct branch head's tree. This is independently verified Git API/ref provenance, not a claim of reading the inaccessible PR capture manifest. The retrieved push candidate manifest directly records `cc163d6`; its frozen baseline records `6e4ba5d`. Receipts: `list-surface-{push,pr}-{job,annotations,artifacts}.json`, `list-surface-pr-merge-{ref,provenance}.json` under ignored `artifacts/qa/ui-v3/independent/`.

### Current visual access and last actually inspected pixels

Current push native artifact **12230441547**, **499,443 bytes**, initially fails at `productionresultssa4.blob.core.windows.net`. A later distinct current resource artifact downloads normally at that same host, providing new observed access evidence. **One ordinary retry of the original native artifact then succeeds**, using the unchanged GitHub API and returned redirect. Both initial failure and later success receipts are retained. Current PR native artifact **12230865960**, **503,598 bytes**, remains denied at its normal sa16 redirect. No redirect rewrite, forced rerun, reupload, screenshot annotation transport or other bypass is used. No environment publication is inferred from this observed access change.

The latest actually inspected native images belong to **current push run 39344178105**, artifact **12230441547**, direct source **`cc163d6`**. Extraction is `list-surface-push-native-retry/{baseline,candidate}/`; receipt `list-surface-push-native-retry-download.json`. Earlier distinct PR run 39343072663/artifact12229109729, actual merge `ae22dbb` with the exact earlier `d5d1a8c` tree, remains separately retained and is not relabeled as current evidence.

The reviewer opens the current baseline/candidate selected 800×600 pair, candidate Archive, high-contrast, 1024×680, narrow initial/review, empty and error PNGs. All 16 files are independently checked opaque; sizes are **784×561**, **1008×641**, and **524×421** pixels at scale 1, within the recorded **1024×720-DIP** work area. The compact 800 image shows three complete fixture rows, exact destination, name/Create and footer, without an outer-body scrollbar. Minimum-size scrolling actually reveals the complete destination. The current error image visibly removes the earlier bright disabled-list rectangle while retaining clear unavailable status and Refresh/Cancel. The scoped ListBox template and unavailable-list collapse preserve source selection/disabled/scroll semantics and current native tests pass. Instrument/Archive/empty/error/ordinary selected images record high contrast false; the separate real high-contrast capture records true and displays a legible Windows white/black/blue palette.

The current detailed receipt establishes all 112 named checks, including real SelectionItem selection after catalog replacement, duplicate-name exact identity, 31/32 named projects plus legacy, UIA Open/Cancel, noncooperative late results, changed destination, contrast and strict viewport bounds. At 800, body viewport height is **333.6633333333333 DIPs**, with complete name/Create bottom **321.19**, approximately **12.47 DIPs** clear. At 540, actual BringIntoView scrolls **223.01 DIPs**, bringing the target fully into the **182.42-DIP** viewport while footer Open/Cancel remain outside the scroll body. The 0.01-DIP rounding tolerance is unchanged.

The current keyboard receipt records **56 checked SendInput calls**, each queued once/error zero, and actual WPF System/Alt+M/N/Down, dropdown End/Enter, 18 Tabs, Home and modal Escape. Focus reaches the intended targets without accepting another project; UserInteractive is true and input/thread Default desktops match. No fabricated routed key event substitutes for native input. The current high-contrast receipt confirms original false, native/WPF enabled true, restored true and restored WPF false. This does not explain the later separate resource process's highContrast true observation; both actual receipts are preserved without assigning an unmeasured cause.

Native TextBox edit templates retain a platform focus stroke, visibly blue rather than the canonical gold role. One inspected earlier narrow-review pair is **RGB(86,157,229)** against **RGB(30,27,35)**, independently calculated **5.945336402765718:1**. This is one real pixel-pair observation, not complete hover/focus/disabled template or physical WCAG qualification. Control-brush contrast tests likewise have a stated scope.

Fresh blind [round 1](RESEARCH_PROJECTS_UI_V3_CRITIC_ROUND1.md) prefers baseline; [round 2](RESEARCH_PROJECTS_UI_V3_CRITIC_ROUND2.md) prefers the repaired candidate but flags competing scroll regions; [round 3](RESEARCH_PROJECTS_UI_V3_CRITIC_ROUND3.md) prefers the earlier `d5d1a8c` compact primary screen. Its main remaining deficit is small/wrapped exact storage metadata. Round 3 excludes the error-state repair and does not establish a current competitor win. The genuine published VS Code reference is not a matched live application capture; obsolete/undated references are excluded as current evidence.

Fresh blind [round 4](RESEARCH_PROJECTS_UI_V3_CRITIC_ROUND4.md) uses the **current push run39344178105/artifact12230441547** selected **and error-state** pairs at the same 800×600-DIP/784×561-pixel viewport. It chooses **B=current `cc163d6` over A=frozen `6e4ba5d`**. The critic finds healthy selection/review, visible complete identity/path, deliberate Open, and recoverable error via Refresh/Cancel, with no visible blocking UX/accessibility issue established. Its largest remaining deficit is scattered unavailable-state guidance: the review still says Select while the catalog is unavailable. Consolidated failure/retry instructions and more readable exact metadata are bounded follow-ups. This is a source-specific native before/after win, **not** a matched current-competitor benchmark or full Gauntlet pass; **U5 remains 0**.

### Current resource evidence

Current push resource artifact **12231040817**, **4,093 bytes**, downloads through its ordinary sa4 path and is independently inspected. Profile `performance-20261019-235458-bbf9e99f` retains binary hashes, raw CPU/elapsed counters, budget evaluation, and six nonempty boundary thread snapshots. Recomputed CPU/machine percentages match within 0.000001; all intervals last at least 60 seconds and private peaks cover endpoints. No calibration/observer cost is subtracted.

| Current execution / interval | One-core CPU | Peak private bytes | Unchanged budgets |
|---|---:|---:|---|
| Push visible idle | 0.31248986334964934% | 27,869,184 | Both pass; raw recomputed |
| Push visible reduced motion | 0.026040451358801748% | 27,811,840 | Both pass; raw recomputed |
| Push hidden | 0.07811972066927717% | 27,246,592 | Both pass; raw recomputed |
| PR visible idle | 0.3385323282523918% | 26,968,064 | Both pass; API observation |
| PR visible reduced motion | 0.15624425333636227% | 26,603,520 | Both pass; API observation |
| PR hidden | 0.1562453165466365% | 26,685,440 | Both pass; API observation |

Limits remain strictly **<1% of one core** and **<150,000,000 private bytes**. Current PR resource artifact **12230211701** remains inaccessible at its normal sa16 redirect; its raw values are not independently recomputed. Current push raw scope is Windows version `10.0.26100`, x64, .NET `10.0.13`, 96 DPI, four logical processors, default `threads` observer. **Windows animations are false and high contrast is true**; no blink/timer/owned animation occurs in any interval. This is neither normal-color nor animated-idle qualification. The earlier current capture process confirms restoration to false, but no cause is established for the later resource process reporting true. Historical resource failures remain unchanged. Receipts: `list-surface-{push,pr}-resource-download.json`, `list-surface-push-resource-independent-arithmetic.json` and extracted raw profile.

The separate earlier 1,500-ms picker-plus-harness observation **1.041607225614325% / 47,374,336 private bytes** has a different scope and is not substituted for these companion budget intervals. None of these measurements qualifies a physical reference PC, long-term percentile, installed-model coexistence, scientific workload or release.

## Criterion-level assessment

- **C1=2:** authoritative returned names/IDs/legacy/exact paths remain literal data; catalog and identity checks complete. **C2=2:** selection performs no create/open/worker action; real explicit modal Open confirms exactly the reviewed project. **C3=2:** existing name/count validation, busy admission and authoritative create/reload remain intact; successful create smoke and duplicate/late-create checks pass. **C4=2:** missing/changed/error/busy states clear or refuse obsolete acceptance and retain recovery. **C5=2:** current complete native suite preserves reload, keyboard, modal cancel and dismissal invariants; selection/double-click has no implicit Open handler. No search is required for this bounded catalog.
- **A1=2:** mandatory architecture/security gates pass; the six-project production solution, pinned graph, locks and manifest remain unchanged. The separately inventoried dev helper adds no direct packages. **A2=2:** existing catalog/workspace/store rules remain authoritative. **A3=2:** shared `Mu.*` styles require opt-in; the scrollbar implicit override is local to this window. **A4=2:** Open revalidates the entire typed project record and reviewed item reference; behavior guards do not rely on enablement alone. **A5=2:** scoped native resources/SystemColors and cleaned-up preference subscriptions use WPF mechanisms, supported by actual earlier high-contrast pixels and current native/WPF observations.
- **M1=2:** UI capacity checks at 31/32 named projects plus legacy and existing name/ID bounds pass. **M2=2:** counts and exact selection derive from current catalog data; failed refresh and same-name replacement cannot substitute identity. **M3=2:** canonical colors/DIPs/font fallbacks and the decorative/functional-border exception are mapped explicitly; native edit-template focus is separately identified. **M4=2:** actual native control brush-pair tests meet 4.5:1 text/3:1 essential boundaries in both modes; the inspected focus pixel pair also exceeds 3:1. These do not certify every template state or physical rendering. **M5=2:** all 50 raw scores and exact arithmetic are present without scientific claims.
- **T1=2:** meaningful independent load/create/select/modal/cancel/capacity checks and existing boundary regressions execute. **T2=2:** actual stale/repaired catalog, duplicate, noncooperative close/create/open and changed snapshot cases pass; existing active-job guards pass. **T3=2:** both current Windows executions complete the required unchanged gates with exact named counts/skip. **T4=2:** both complete the known 112-check native suite, including real injected keyboard and UIA paths; current push detailed receipts are independently inspected. **T5=1:** actual current ordinary/narrow/large/high-contrast native evidence exists, but 125–200% physical DPI and the full accessibility/motion matrix remain unqualified.
- **R1=2:** async operations retain responsive cancellation and truthful recoverable outcomes; current checks pass. **R2=2:** close invalidates noncooperative late catalog/create/open results and clears transient state. **R3=2:** reload clears selection and Open refuses changed snapshots. **R4=2:** existing queued/running-job switching protection and reopened original-job cancellation retain passing checkpoints. **R5=2:** repeated shown/modal fixtures, duplicate admission, initialization errors, optional-runtime recovery and preference unsubscription retain explicit cleanup without automatic retries.
- **S1=2:** no new hosted call, telemetry, credential, shell or dataset-discovery production behavior. **S2=2:** exact approvals, project ownership and explicit reviewed Run remain unchanged; handoff zero-worker/no-artifact checks pass. **S3=2:** names/paths remain display text, preserving existing validation and storage rules. **S4=2:** reporter/diagnostics expose fixed public labels/counters, not private text; exact paths are intentionally available in picker review/accessibility. **S5=2:** invalid storage/catalog/lifecycle races fail closed; no migration, silent repair or project substitution is added.
- **P1=2:** real catalog bounds and static resources add no polling/render loop/history. **P2=2:** picker metadata actions add no worker/model/dataset/scientific work. **P3=2:** async admission/cleanup and actual repeated/late-result checks avoid redundant callbacks and static-handler retention. **P4=2:** original timeout/resource/admission thresholds and required verification commands are retained. **P5=2:** genuine current native resource observations, independently recomputed push counters, distinct PR values and exact scenario limits are recorded. This criterion rewards truthful measured evidence, not general performance certification.
- **N1=2:** production changes are limited to shared opt-in resources and this picker, preserving unrelated views. **N2=2:** durable design/resource maps, fallback/border/focus exceptions and scope are explicit. **N3=2:** busy/catalog/selection/accepted-ID transitions and recoverable errors are clear and independently exercised. **N4=2:** tests verify real authority/lifecycle/UIA/input/viewport behavior and preserve helper mistakes rather than weakening assertions. **N5=2:** CHANGELOG, DEVELOPMENT_STATUS, TESTING, usage/design and QA history describe delivered behavior and remaining gaps; final handoff distinguishes retrieved push results from inaccessible PR details.
- **U1=2:** actual current matched native images show improved hierarchy/full path/Create/Open, three complete rows, repaired error surface and unchanged Muon source. **U2=2:** controls perform their stated actions, states/counts are truthful and recovery remains practical; current native pixels and passing tests support the bounded disabled-surface repair. **U3=1:** actual injected traversal/access keys/UIA names pass and visible native focus exists; screen-reader announcements and complete state-specific focus rendering remain unverified. **U4=1:** current strict reachable-target/footer geometry and actual narrow/contrast images support scale-1 behavior; higher-DPI physical combinations remain open. **U5=0:** no matched current-competitor blind binary benchmark is available. Baseline wins do not replace it; this remains blocking.
- **Q1=2:** draft PR/base/ancestry are verified, with no unrelated merge/rebase. **Q2=1:** authoritative v3 references/protocol preceded implementation, but the first pre-design native captures were defective; faithful later recaptures cannot retroactively change that history. **Q3=2:** legacy/IDs/paths/32-project bounds, job ownership, privacy/approval/resource/scientific boundaries and 136×132-DIP Muon remain preserved. **Q4=2:** no unrelated shell, memory, migration, fake telemetry/navigation, web stack or bundled font/dependency is added. **Q5=2:** exact current sources/APIs, actual current before/after images, independent review/critic history, measurements, unresolved blockers and next bounded slice are handed off. Remaining comparator/physical limits are explicitly retained.

## TOP 5 RANKED FIXES / IMPROVEMENTS

1. **Problem:** the required matched current-competitor blind comparison is absent. **Why it matters:** a before/after baseline preference cannot establish the requested Gauntlet outcome. **Proposed fix:** obtain genuine named current same-task captures at a matched viewport, then use a fresh independent critic and preserve its binary decision/deficit. Until then, retain incomplete acceptance and no competitor-win claim.
2. **Problem:** physical screen-reader, keyboard/display and 125–200% DPI combinations are unverified. **Why it matters:** property/automation tests do not prove announcement quality, real-world focus or mixed-DPI overflow. **Proposed fix:** complete the supported physical accessibility/DPI qualification before release; retain current CI injection and scale-1 results as their own narrower evidence.
3. **Problem:** round 4 finds scattered catalog-unavailable guidance, including a Select instruction when no catalog is available. **Why it matters:** the count, review panel and error require users to combine separate messages even though Refresh/Cancel safely recover. **Proposed fix:** consolidate preservation/failure/retry instructions and make the review hint reflect catalog availability in a separate bounded follow-up; retain the current fail-closed guards.
4. **Problem:** small wrapped IDs/paths remain the primary-screen readability deficit. **Why it matters:** exact destination comparison becomes laborious for long paths and duplicate names. **Proposed fix:** use a separately reviewed readability increment with more metadata space or labeled selectable fields; retain every character and explicit Open semantics. Avoid unrelated changes in this final checkpoint.
5. **Problem:** resource results cover no animated idle, normal-color current push measurement or co-resident scientific/model workload. **Why it matters:** broad performance/release claims would exceed the actual evidence; current capture restoration and later resource contrast observations also differ. **Proposed fix:** preserve both actual receipts and limits, investigate only under a separately justified diagnostic protocol, and qualify required resource combinations before release. Do not subtract overhead, infer a cause or rerun unchanged cases to increase this score.

## Regression Risks

Native ListBox/edit/scroll templates can vary by Windows theme/runtime; pointer/paging behavior and every hover/disabled/focus pixel remain less exercised than selection and keyboard. Long paths/names and narrow scrolling need continued exact-content preservation. Async metadata may commit before cancellation, so Refresh remains the recovery authority. Current source maintains reviewed snapshot and workspace admission boundaries; passing fixture tests do not qualify model inference or other scientific execution.

## Tests Required

Current primary/error/contrast images and detailed input/viewport receipts are inspected. Complete the matched current-competitor critique. Keep physical DPI/accessibility, remaining template/pointer states and animated/model resource qualification open for their supported separate checks. No new source change, unchanged rerun, assertion relaxation or extra test breadth is requested to manufacture acceptance.

## Final Verdict

**94/100 = 9.4/10; FAIL / INCOMPLETE for the requested UI phase.** Current supported native engineering checks and inspected visual repairs satisfy the bounded functional checkpoint, with zero confirmed unresolved critical correctness/security/accessibility defects in the tested scope. The required matched current-competitor comparison remains missing, and broader accessibility/resource qualification remains limited. This report does not pass the Gauntlet, qualify physical accessibility/hardware/scientific workloads, or rewrite the original 6.5 review and failed runs. Draft review may continue while the explicit evidence blockers remain visible.
