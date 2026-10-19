# QA REVIEW

Phase: UX-0/UX-1 — scoped WPF resources and Research Projects picker, initial candidate (2026-10-19)

Overall Score: **6.5 / 10**

Result: **FAIL / INCOMPLETE**

Independent reviewer: development engineering QA agent, separate from the implementation author. This review uses the [50 frozen criteria](RESEARCH_PROJECTS_UI_V3_PROTOCOL.md), unchanged weights and strict exit rule. Reviewed source is **`1d09dc88faf130dc04e02eae0bf4e7a4d07f2ac2`**, on `feature/research-projects-ui-v3`, based on Chat handoff `6e4ba5d5f0edd5af23eabf3493e41c07dd6b592e`. Baseline harness source is `7744f562f27f7c8bce62dfe40171543c654f07c7`.

The mandatory repository gates pass, but the new native UI verification step **fails**. Its exact failing checkpoint was not yet retrievable at this review. The baseline PNGs were retrieved and actually inspected; the capture has transparent-background and cropping defects. These are evidence-harness defects, not proof that the production window itself is clipped. Candidate images and the detailed independent native receipt remain unavailable after an ordinary artifact download was denied. No blind visual comparison, native accessibility completion, or visual win is claimed.

There is **no confirmed critical production correctness/security defect from source inspection**, but acceptance is blocked by the unresolved native UI failure and inadequate visual evidence. This is not a zero-risk or successful-feature qualification. Uncommitted later high-contrast, resource-measurement, reporting and documentation work in the shared checkout is **excluded** from this checkpoint review.

## Category Scores

Each criterion scores 0/1/2 in frozen protocol order. Categories are equally weighted.

| Category | Raw criterion scores | Sum |
|---|---|---:|
| Code Correctness | C1=2, C2=2, C3=1, C4=1, C5=1 | 7 |
| Architecture | A1=2, A2=2, A3=2, A4=2, A5=1 | 9 |
| Mathematical Correctness | M1=1, M2=1, M3=2, M4=1, M5=2 | 7 |
| Testing | T1=1, T2=1, T3=2, T4=0, T5=0 | 4 |
| Reliability | R1=1, R2=1, R3=1, R4=2, R5=1 | 6 |
| Security | S1=2, S2=2, S3=2, S4=2, S5=1 | 9 |
| Performance | P1=2, P2=2, P3=1, P4=2, P5=1 | 8 |
| Maintainability | N1=2, N2=1, N3=2, N4=1, N5=0 | 6 |
| UX/UI | U1=0, U2=1, U3=1, U4=1, U5=0 | 3 |
| Requirement Compliance | Q1=1, Q2=1, Q3=2, Q4=2, Q5=0 | 6 |

Exact arithmetic: `(7 + 9 + 7 + 4 + 6 + 9 + 8 + 6 + 3 + 6) / 10 = 65 / 10 = 6.5`. The strict greater-than-8.5 threshold fails. Even a higher numerical score would not override the unresolved native UI and visual-evidence blockers. No scientific algorithm changes; mathematical scoring covers bounds, roles/contrast, identity/accounting and this exact arithmetic only.

## Evidence

### Independently observed supported Windows results

Normal GitHub run/job/check-annotation APIs were queried through the inherited proxy with TLS verification retained. JSON receipts are under ignored `artifacts/qa/ui-v3/independent/`.

- [Baseline run 39338407912](https://github.com/calebaiken/mu-desktop-assistant/actions/runs/39338407912), job `118353597991`, exact harness SHA `7744f562f27f7c8bce62dfe40171543c654f07c7`: **success**. Both required verification scripts and the unchanged-picker capture step succeeded. Receipts: `baseline-run.json`, `baseline-job.json`, `baseline-annotations.json`.
- [Candidate run 39339073773](https://github.com/calebaiken/mu-desktop-assistant/actions/runs/39339073773), job `118355817904`, exact SHA `1d09dc88faf130dc04e02eae0bf4e7a4d07f2ac2`: **failure**. `Build, test and smoke` and `Adversarial architecture controls` both **success**; `Verify native Research Projects UI` **failure**. Evidence upload and bounded summarization succeeded. Receipts: `candidate-job.json`, `candidate-annotations.json`, `candidate-artifacts.json`.
- Both runs' TRX-derived notices report **525 total, 524 executed/passed, zero failed, one NotExecuted**. The sole NotExecuted method is the Linux-only `TextInspectionTests.NativeCapabilityIsUnavailableOnUnsupportedPlatforms`.
- All four named native process/file methods report Passed, including real project-worker routing and trusted file identity/link/refusal/audit checks.
- Both report **21 completed existing project checkpoints** and **28 completed existing Chat-handoff checkpoints**. Candidate handoff smoke elapsed **1,490 ms**. These preserve existing project admission/cancel/reopen and exact-approval behavior; they do not prove the new independent picker suite completed.
- Candidate notice records **eight native client images generated**. The API notice is exactly **4,096 characters** and truncates during the second image line, before the new independent-suite receipt. No passed-checkpoint count or failure attribution for that suite is invented.

The commands remain:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-negative.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-projects-ui.ps1 -Mode candidate
```

The native fixture uses actual WPF windows, UI Automation providers, Win32-injected keys and declared synthetic project metadata. Injected keyboard checks do not qualify physical keyboard use or a screen reader. An attempted or partially completed suite is not a passing suite.

### Actual baseline artifact retrieval and image inspection

Standard download of baseline artifact **12227546936** succeeded, **74,961 bytes**, through its ordinary redirect to `productionresultssa2.blob.core.windows.net`. No alternate transport or proxy change was used. Receipt: `baseline-artifact-download.json`; archive: `baseline-native.zip`; extracted files: `baseline-native/baseline/`.

The manifest identifies unchanged production source **`6e4ba5d5f0edd5af23eabf3493e41c07dd6b592e`**, synthetic metadata, **100% / scale 1×1**, high contrast false, and these actual dimensions:

| Requested/actual window DIPs | Captured root-Grid pixels |
|---|---|
| 800×600 | 748×525 |
| 1024×768 | 972×693 |
| 540×460 | 488×385 |

Five files exist: selected at those three sizes, plus 800×600 empty and error. The reviewer opened `baseline-800x600-selected.png` using the image-viewing tool. Its background is transparent where the Window supplies the real background; black header/copy disappear against a black preview. The right edge crops Create/footer content. Independent PNG inspection found **123,260 fully transparent pixels of 392,700**; samples `(0,0)` and `(300,40)` are RGBA `(0,0,0,0)`.

Source attribution is precise: `Program.CaptureAsync.Save` renders `window.Content`, a root Grid with nonzero Margin, into only that Grid's ActualWidth/ActualHeight. It omits the Window background and the root's translated bounds. The output is a real native visual-tree render, but it is an incomplete representation of the shown window. Do not treat this harness artifact as an actual production overflow finding or use it to claim a visual win.

Candidate artifact **12227064445** is listed as **295,757 bytes**. Its standard archive request received proxy CONNECT **403 Forbidden**. A redirect-metadata-only query identifies `productionresultssa3.blob.core.windows.net`; receipt `candidate-artifact-download.json`. The failure is a destination-policy/access blocker, not evidence that the artifact or test receipt does not exist. Only supported settings and ordinary artifact retrieval may resolve it.

### Available local checks and limitations

Independent helper cross-build before the candidate's production changes passed **exit 0, zero warnings/errors**, with SDK 10.0.401: `helper-build-contrast.log` (3.03 s). Its earlier namespace-shadowing compile failure is retained as `helper-build-initial-namespace-failure.log`; the alias repair is not hidden. This earlier helper build is **not** relabeled as the final candidate build. Candidate compilation and mandatory regressions are established by the actual Windows gates above.

Author-provided current production Python regressions were inspected under `artifacts/qa/ui-v3/`: author 6, data 16, engine 19, pipeline 7, project isolation 1, all OK (**49 total**). The production worker source is unchanged by the UI candidate. These are worker regression receipts, not a successful scientific-runtime Run through the new picker UI.

The retrieved baseline idle observation records **1,501 ms, 0% one-core CPU, 36,757,504 private bytes, 96,161,792 working-set bytes, no animated properties, rendering tier 2**. Its scope is the shown standalone baseline picker and harness, explicitly not companion release qualification. No candidate performance result or unchanged-companion budget pass is inferred from that baseline.

## Criterion-level assessment

Scores here correspond exactly to the table and frozen protocol.

- **C1=2:** production `RefreshAsync` uses the existing store, actual returned rows, full stable IDs/legacy labels and exact selected path; existing native create/identity preview regression passes. **C2=2:** selection updates display only; explicit Open revalidates through `store.OpenAsync`, and no model/dataset/worker API is introduced. **C3=1:** source validates names and guards duplicate admission before awaiting; existing create smoke passes, but the new duplicate/completion checks have no completed receipt. **C4=1:** corrupt/stale/missing paths clear selection and disable catalog-dependent operations; new native denial/recovery completion is unknown. **C5=1:** refresh/cancel/key handlers are explicit, but the new interaction suite fails. Search is deliberately absent for the bounded 32-entry catalog; this score concerns the present controls, not an invented search requirement.
- **A1=2:** actual mandatory architecture controls pass; the production graph/packages/locks/manifest remain unchanged. The dev helper is outside the six-project solution and reuses a Desktop reference without direct packages. **A2=2:** existing catalog/store/workspace ownership remains authoritative. **A3=2:** all shared styles have explicit `Mu.*` keys and merge only in the picker. **A4=2:** Open compares the complete authoritative project record and reviewed item reference before assigning the accepted ID; App workspace admission remains final authority. **A5=1:** OS-color resources and high-contrast preference subscription/unsubscription are present, but actual high-contrast behavior has not run at this checkpoint.
- **M1=1:** source correctly excludes the legacy row from the 32 named-project capacity, and existing backend bounds regressions pass; new UI capacity checks are not observed complete. **M2=1:** count/identity derive from current catalog data and refresh clears stale data, but actual new refresh/duplicate-name native outcomes are unavailable. **M3=2:** Instrument/Archive role values match the supplied canonical JSON, with explicit DIP/font fallbacks and documented code comments distinguishing decorative versus functional borders. **M4=1:** independent sRGB calculations show canonical quiet borders below 3:1; the candidate uses secondary text for functional boundaries and contains actual-control contrast checks, but their native results and representative rendered images are not available. **M5=2:** all 50 scores and exact arithmetic are shown with no scientific qualification implied.
- **T1=1:** independent behavior tests exist for UIA selection/Open/Cancel, capacity, duplicate Create, and asynchronous completion, but the new suite has not completed successfully. **T2=1:** independent late-load/create/open, changed snapshot and failed-refresh fixtures are meaningful but have no completed native evidence. **T3=2:** the candidate's required Windows positive/negative gates actually pass with named results. **T4=0:** the newly required native primary-action/UIA/keyboard suite fails; its missing detailed receipt cannot be treated as completion. **T5=0:** initial images do not faithfully capture the full native appearance; no 125–200%, actual high-contrast or reduced-motion matrix is executed by this checkpoint.
- **R1=1:** asynchronous operations and recoverable failures are explicit and do not synchronously block; full new native evidence remains missing. **R2=1:** Closed cancels/disposes the lifetime and suppresses late callbacks, with independent noncooperative fixtures authored but not observed complete. **R3=1:** refresh invalidates selection and Open validates snapshots; new refresh/race completion remains unknown. **R4=2:** existing actual project/handoff native checkpoints retain active-job switch refusal and reopened cancellation. **R5=1:** static preference events unsubscribe and transient contents clear, but repeated/interleaved new native checks are incomplete.
- **S1=2:** the production diff contains no hosted/credential/telemetry/shell/dataset-discovery additions. **S2=2:** application and approval boundaries are unchanged; existing handoff zero-worker/no-artifact and reviewed-run checks pass. **S3=2:** names/paths remain literal TextBlock/TextBox data, existing validation is reused, and no executable markup/path inference is introduced. **S4=2:** no new content logging is added; paths are intentionally exposed in the picker review/accessible labels, while the reporter emits bounded fixed labels and dimensions. **S5=1:** source fails closed for catalog/storage/snapshot changes and existing storage regressions pass, but the new lifecycle race tests lack a completed native result.
- **P1=2:** the catalog is bounded and no polling, animation loop or retained history is introduced. **P2=2:** picker operations only list/create/open metadata; unchanged native handoff zero-worker boundaries pass. **P3=1:** async work and event cleanup are explicit, but actual new repeated-use/late-callback native completion is unavailable. **P4=2:** no resource/admission/timeout threshold is weakened; mandatory gates retain the original positive/negative commands. **P5=1:** a genuine short baseline observation exists, but no retrieved candidate observation or current companion budget result supports broader performance acceptance.
- **N1=2:** production changes remain focused to the picker and opt-in resources; the small smoke adjustment calls BringIntoView before retaining full-bounds assertions for the intentionally scrollable body. **N2=1:** role names and functional-border exception are clear in source, but the durable design mapping is not committed at this checkpoint. **N3=2:** busy/catalog-loaded/selection/accepted-ID transitions are explicit; backend authority is not duplicated. **N4=1:** tests exercise meaningful boundaries and declare synthetic/native scope, but the first new native run fails and the capture helper is defective. **N5=0:** required status/changelog/testing/usage updates for this candidate are not yet part of the reviewed commit; later uncommitted drafts receive no credit.
- **U1=0:** no faithful before/after native screenshot pair is available for hierarchy/readability review. The Muon source remains unchanged, but that alone does not satisfy the complete criterion. **U2=1:** source contains truthful empty/loading/error/busy/stale/success copy and working handlers, while the native new-control suite is incomplete. **U3=1:** explicit labels, focus styles, key routes, polite region and `LiveRegionChanged` raising exist; actual native focus/keyboard completion and a screen reader are unproven. **U4=1:** existing native minimum-size bounds checks pass after actual scrolling; broader high-DPI, high-contrast and faithful screenshot overflow review remain missing. **U5=0:** no fresh blind binary comparison has occurred and no winner is claimed.
- **Q1=1:** branch ancestry is focused and unchanged; a reviewed draft PR is not yet delivered. **Q2=1:** authoritative kit/protocol and native baseline generation preceded the candidate, but the generated baseline was not yet faithfully captured/inspected before UI preparation; browser prototypes were not misrepresented as native. **Q3=2:** existing project/storage/approval/resource behavior and 136×132-DIP Muon source are preserved, supported by existing native regressions. **Q4=2:** no unrelated shell, memory, telemetry, migration, bundled font, web stack or dependency is added. **Q5=0:** final source/evidence handoff, reliable images, critic history and completed independent verdict are pending.

## TOP 5 RANKED FIXES / IMPROVEMENTS

1. **Problem:** The new native UI step fails without an inspectable failing checkpoint.
   **Why it matters:** passing older smokes cannot establish the new primary-action, duplicate/late-callback, focus or contrast behavior.
   **Proposed fix:** retrieve the actual receipt through the standard permitted artifact route, diagnose the precise failure, repair its cause and rerun the unchanged meaningful suite. Preserve this first failed run; do not drop assertions or infer success from image generation.
2. **Problem:** The capture helper omits the native Window background and crops translated content.
   **Why it matters:** incomplete native renders hide text and can manufacture overflow findings or a misleading comparison.
   **Proposed fix:** capture the full native visual/window client with its background and correct bounds, then recapture the frozen old production UI and current candidate at identical actual viewports/DPI. Retain the initial deficient files and provenance.
3. **Problem:** Actual high-contrast, higher-DPI, reduced-motion and completed keyboard/UIA evidence are absent.
   **Why it matters:** custom control templates and narrow scrolling can fail despite successful compilation and ordinary light/dark source checks.
   **Proposed fix:** run supported native checks, explicitly restore any OS preferences changed by the dev harness, record actual OS/WPF state and viewport/DPI, and leave physical display/screen-reader gaps unqualified.
4. **Problem:** The requested independent visual gauntlet has not run.
   **Why it matters:** self-scores or defective screenshot pairs do not establish comparable-task quality.
   **Proposed fix:** after repairing captures, give a fresh critic actual matched native Mu and fetched named comparable task screens with labels hidden; retain the binary choice, largest deficit and every iteration. Make no win claim while evidence is unavailable.
5. **Problem:** Durable candidate documentation, final performance evidence and draft-PR handoff are pending.
   **Why it matters:** reviewers need precise scope, resource observations, remaining qualification and token exceptions without treating old resource failures as fixed.
   **Proposed fix:** commit dated status/testing/changelog/design mappings and focused draft-PR details after actual results; retain the unchanged resource thresholds and failed receipts, distinguish a short picker observation from companion/release qualification, and name the next bounded slice separately.

## Regression Risks

Modal dismissal during noncooperative metadata operations, Enter routing between create/list/theme controls, selection substitution after refresh, and custom native templates remain the main new risks. Existing project isolation, busy-job ownership, temporary Chat refusal, approval semantics and worker boundaries have passing mandatory regression evidence and are unchanged in production source. The current defect in capture provenance/rendering must not be confused with a production layout defect.

## Tests Required

Complete the new native UI suite with a detailed passing receipt; recapture and inspect faithful baseline/candidate native images; verify contrast and reachable controls at actual narrow/DPI/high-contrast conditions; perform the separate critic comparison; and complete supported resource observation without weakening existing limits. A simulated key, system-API contrast toggle, synthetic project metadata or short harness timing must remain labeled as such. No installed-model inference, physical accessibility, scientific algorithm, or release qualification is granted.

## Final Verdict

**FAIL / INCOMPLETE at 65/100 = 6.5/10.** Both mandatory repository gates pass, but the new native UI step fails and required visual/accessibility/comparison evidence is incomplete. Preserve this initial scored review, the failed Windows run and deficient first captures. Continue diagnosis and bounded repair; this checkpoint does not authorize a passing UI phase exit or claim a blind visual win.

## Addendum — corrected native capture and first detailed new-UI receipt (2026-10-19)

This addendum preserves the original **65/100 = 6.5 FAIL/INCOMPLETE** score and all earlier observations. It records new evidence; it does not retroactively pass either earlier run.

[Run 39339769599](https://github.com/calebaiken/mu-desktop-assistant/actions/runs/39339769599), job `118358133026`, exact source **`91a464572449f1fa0ce4698ad6faef3a72e9cb8a`**, completes with **failure**. Both mandatory repository gates again pass, and the frozen-old-UI capture step succeeds. The new native UI step fails; the subsequent unchanged companion resource-measurement step is **skipped**, not passed. Compact annotations now expose the actual new-suite result: **completed false, 10 passed checkpoints, stage `retry and stale selection`, failure type `InvalidOperationException`, no failed assertion label**. Independently fetched receipts: `repaired-job.json`, `repaired-annotations.json`, `repaired-artifacts.json`.

The ten passed checkpoints actually establish pending-catalog Open/Create refusal; catalog lifetime cancellation; late catalog suppression; duplicate UIA Create admission of one metadata request; pending-Create Open refusal; Create lifetime cancellation; and no closed-window reload/activation from late Create. Later identity, capacity, contrast, keyboard and modal Open checks did not complete in this run and receive no passing credit.

The independent test's `SelectRow` helper used `CreatePeerForElement(ListBoxItem)`. Official WPF source shows that this returns a `ListBoxItemWrapperAutomationPeer`; the SelectionItem pattern belongs to the owning ListBox's `ListBoxItemAutomationPeer` children. This is a mistake in the independent test helper, not established production failure. The observed stage/type matches that defect at the first selection attempt, although the deliberately content-free receipt omits the exception message. The repair uses `ListBoxAutomationPeer.GetChildren().OfType<ListBoxItemAutomationPeer>()` and invokes the actual `ISelectionItemProvider.Select()`; it retains the UIA path and every selection assertion. No programmatic SelectedIndex fallback replaces the tested action. Its local cross-build passes **exit 0, zero warnings/errors, 2.86 s**, receipt `helper-build-item-peer-fix.log`; native proof of the repair remains pending. Source-inspection evidence is retained as the three official WPF source files under the independent artifact directory. The first `1d09dc8` run's exact failure attribution remains provisional rather than being rewritten from the later run.

Standard artifact download **12228542403** now succeeds, **426,264 bytes**, through its normal redirect to `productionresultssa7.blob.core.windows.net`. No alternative transport or proxy alteration is used. Receipt: `capture-repaired-artifact-download.json`; archive: `capture-repaired-native.zip`; extraction: `capture-repaired-native/{baseline,candidate}/`.

The repaired helper copies actual shown native client pixels via GetClientRect/GetDC/BitBlt and cleans up its GDI handles. The same helper captures the frozen production source `6e4ba5d5f0edd5af23eabf3493e41c07dd6b592e` from a separate CI checkout and the candidate `91a4645`. Matching client sizes are now **784×561**, **1008×729**, and **524×421** pixels for **800×600**, **1024×768**, and **540×460** DIP windows, all scale **1×1**, high contrast false. Both inspected 800×600 selected PNGs have alpha extrema **255–255**, with the correct visible window background and full margins. These repair the earlier capture artifact; the deficient first files remain retained.

Actual visual inspection of the corrected selected captures finds a **new production UX acceptance blocker**: at **800×600**, the enabled Open button is visible while the entire selected destination review/exact storage path lies below the scroll viewport; at **540×460**, even the selected row's ID is partially clipped and its destination review is hidden below the fold. At **1024×768**, the side-by-side review displays the full selected ID/path. This finding is supported by faithful native images rather than inferred from the initial cropped renders. The compact layout needs repair so a user can see/review the selected identity and exact storage destination while the primary action is available. Styling must not sacrifice this existing workflow clarity.

The OS contrast receipt reports **native enabled true, WPF enabled false, native error 0**. No high-contrast image is generated; actual WPF high-contrast behavior remains **unverified**. Later uncommitted native/WPF restoration read-back work is not part of this run. No physical-monitor, high-DPI or screen-reader claim follows from the SPI request.

The corrected short idle observations are also retained: baseline **1,500 ms, 1.041261546507639% one-core CPU, 38,948,864 private bytes**; candidate **1,499 ms, 1.041765912232572% one-core CPU, 47,001,600 private bytes**. Both declare a shown standalone picker plus verification harness, no animated properties, rendering tier 2. These genuine observations are not the separate companion's three-interval resource gate; neither a companion budget pass nor a production idle regression is inferred from mismatched scope and such a short observation. The full current companion sampler was skipped after the native UI failure.

The engineering verdict remains **FAIL/INCOMPLETE**. Required next evidence is the native item-peer test repair, corrected compact identity/path layout, faithful subsequent captures, actual supported accessibility/resource results, and the separate fresh visual critic. A future passing review must cite its own source and receipts and retain this history.

## Addendum — item-peer repair reaches the catalog reload (2026-10-19)

[Run 39340102901](https://github.com/calebaiken/mu-desktop-assistant/actions/runs/39340102901), job `118359234502`, source **`4d1c4f82cf9998011312bf1cfc9ccf29e83d82ff`**, again fails the separate native UI step after passing the mandatory repository gates. The new actual receipt reports **13 passed**, stage `retry and stale selection`, `ElementNotEnabledException`, and no failed assertion label. Three newly passed assertions establish stale activation refusal after failed refresh, clearing the obsolete identity preview, and no automatic substitution of a same-name project after catalog repair. The next current-row selection does not complete; later assertions remain unexecuted.

Normal download of artifact **12228935495** succeeds, **426,381 bytes**, via `productionresultssa8.blob.core.windows.net`. Independent copies are `item-peer-firstfix-{job,annotations,artifacts}.json`, `item-peer-firstfix-artifact-download.json`, and `item-peer-firstfix-native.zip`/extraction. The source's restoration read-back now actually reports **original OS contrast false, restored true, restored WPF contrast false**. The enabled contrast attempt still reports **native true, WPF false** and therefore does not qualify WPF high-contrast rendering.

Further official WPF source inspection shows `IItemContainerProvider.FindItemByProperty` explicitly resets the automation children cache and enumerates the current ItemCollection. The independent helper's cached GetChildren item peers can retain the retired item across ItemsSource replacement. This supports a stale-peer diagnosis; the exception is not by itself proof of application disablement. The next repair uses the actual ItemContainer provider, realizes a virtualized item when required, invokes its actual SelectionItem provider, and additionally asserts reference equality with the requested current catalog item. The existing exact-current-ID/path assertion remains intact. No programmatic selection fallback or production change is introduced. Its local strict cross-build passes **exit 0, zero warnings/errors, 2.71 s**, `helper-build-current-item-container.log`; native verification of this repair remains pending.

The original score and failed verdict remain unchanged. These preserved failures show successive test-harness corrections and actual partial results, not a completed new UI suite or an accepted compact layout.

## Addendum — compact capture and current-provider attempt (2026-10-19)

[Run 39340726888](https://github.com/calebaiken/mu-desktop-assistant/actions/runs/39340726888), job `118361309982`, source **`61afa6b9c65367d62135c5cce51a7a70c8394508`**, fails the separate new native UI step after **10 passed** checkpoints, at `retry and stale selection`, with `InvalidOperationException` and no assertion label. The first selection therefore did not complete with the attempted ItemContainer approach. The exact missing precondition is not recorded by that version, so it is **not** retrospectively attributed to a particular optional pattern or provider cast. Parent-retrieved full artifact **12228916363** is retained at `artifacts/ui-v3/compact-first/`; the reviewer directly inspected its receipt, manifest, and actual 800×600 selected image.

That image shows the compact repair has made the selected path visible, but the New project input and Create button remain clipped at the body viewport's bottom. This is still an acceptance blocker. Its manifest now reports **1024×720 DIP work area**. The 800×600 and 540×460 windows fit that area; the separate 1024×768 client capture exceeds its work area and must not be represented as proof that the whole window fits the available desktop.

The installed .NET 10.0.12 `PresentationCore.xml` documents public `AutomationPeer.ResetChildrenCache` as synchronously rebuilding child elements. The next independent helper revision uses this standard API before obtaining actual ListBox item peers, retains `SelectionItem.Select`, and checks both the peer's current data item and accepted current selection. It adds content-free fixed `failureDetail` stages and a selection observation containing only index/count, enabled flags, public peer types, current-item match and pattern availability. A further failure can identify its exact native precondition without exposing project names, paths, payloads or stack traces. No direct selection fallback replaces the tested UIA route.

An independent viewport regression is also added after the existing flows: with three fixed public rows, 800×600 must show the entire destination, name input and Create button inside the actual ScrollContentPresenter viewport; at 540×460 the target may be revealed using actual BringIntoView, while Open/Cancel remain outside the scroll body and fully inside the Win32 client bounds. The tolerance is only 0.01 DIP for floating-point layout noise. Geometry survives failures in a separate receipt. It does not widen the tested viewport or excuse the observed clipping. The original score/verdict and all prior failed runs remain unchanged; these revisions still require their own successful native evidence.
