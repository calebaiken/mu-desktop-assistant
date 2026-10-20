# Independent QA protocol — Research Projects UI v3

Frozen by the independent QA agent on 2026-10-19, before production UI changes.
Implementation baseline: `6e4ba5d` on `feature/chat-project-handoff`.
Scope: reusable, opt-in WPF theme resources and one complete Research Projects
picker. No new application shell, navigation, Chat, ML Lab, scientific algorithm,
project storage migration, approval policy, or avatar redesign is authorized by
this increment. The Muon geometry remains 136×132 DIPs.

## Scoring and evidence

Ten equally weighted categories each contain five frozen criteria. Each criterion
scores 0 (missing/contradicted), 1 (partial), or 2 (satisfied by cited evidence).
Category score is its five raw scores summed (0–10). Overall is the sum of all
50 raw scores divided by 10; show exact arithmetic and a one-decimal display.
Passing requires an unrounded score strictly greater than 8.5/10, zero unresolved
critical correctness/security/accessibility defects, and sufficient end-to-end
evidence. A missing required check is not a pass. No score substitutes for actual
native visual, accessibility, functional, or comparative evidence.

No scientific computation is changed. Mathematical Correctness evaluates the
picker's identity/count/size/contrast bounds and review arithmetic only; it grants
no new scientific, pretrained model, physical display, or hardware qualification.

## Frozen criteria

### 1. Code Correctness

1. The picker loads the authoritative project catalog and clearly presents each actual name, stable ID, legacy status, and selected exact storage path without fabricated metadata.
2. Selection alone causes no open, create, dataset scan, model call, worker start, or scientific execution; explicit Open activates exactly the selected identity.
3. Create validates the existing name/catalog rules, creates at most one project for one accepted action, and reports success only after the authoritative result.
4. Stale, missing, busy, denied, and corrupt states prevent invalid activation and provide a truthful recoverable outcome without selecting another project silently.
5. Search/filter, reload, keyboard activation, cancel, dismissal, and double-click preserve selection/activation invariants and never open an unintended project.

### 2. Architecture

1. The four-layer reference graph, pinned packages/lockfiles, warning policy, security manifest, and architecture checks remain intact without new dependencies.
2. Existing application-owned project catalog, selection/lifecycle, storage rules, and ML admission boundaries remain authoritative; no duplicate catalog or storage format is added.
3. Shared theme dictionaries use explicit opt-in style keys and documented resource roles so existing Mu windows are not globally restyled by this slice.
4. Picker behavior and asynchronous state have a testable authoritative boundary rather than relying only on button enablement or mutable labels.
5. High-contrast and theme resources reuse platform mechanisms and remain scoped, maintainable, and compatible with the native WPF rendering stack.

### 3. Mathematical Correctness

1. The existing maximum of 32 catalog-created projects (plus an optional pre-existing legacy row) and name/ID constraints are preserved and independently checked at relevant boundaries.
2. Displayed project counts, filtered counts, selected identity, and exact path describe actual catalog/selection state and cannot drift after refresh or creation.
3. Canonical token values, native DIP sizing, and fallback fonts are mapped explicitly; CSS pixel/DIP and viewport/DPI evidence are not conflated.
4. Actual rendered foreground/background pairs meet the stated 4.5:1 normal-text and 3:1 essential-indicator targets, with reproducible contrast calculations and documented exceptions.
5. Review includes all 50 raw criterion scores, exact sum/division, strict threshold decision, and honest limits on numerical and scientific qualification.

### 4. Testing

1. Independent behavioral tests cover load/select/explicit open/create/cancel and meaningful name/count boundaries, using the existing authoritative project APIs.
2. Adversarial tests cover stale selection, catalog change, duplicate activation, active jobs, asynchronous errors, close/cancel, and late completion where relevant.
3. Supported Windows positive and negative repository gates run with current named receipts, exact counts/skips, source checkpoint, and exit codes.
4. Actual native tests exercise the complete primary-action paths, empty/full/corrupt/busy/denied states, keyboard-only traversal, and UI Automation names/status/focus.
5. Native layout evidence covers meaningful 800px-class and larger windows, 100–200% scaling where feasible, high contrast and reduced motion; unexercised combinations remain explicitly unverified.

### 5. Reliability

1. Asynchronous catalog/open/create operations leave no blocking UI waits and map failures into safe, actionable status while retaining editable input/selection when valid.
2. Closing or canceling invalidates pending UI completion, releases transient subscriptions/state, and cannot activate a project or mutate a replacement window later.
3. Reload/filter/create changes cannot silently substitute another selected project, lose stable identity, or execute stale Open intent.
4. Existing queued/running-job transition protection and reopened-Lab cancellation behavior remain intact across picker use.
5. Repeated open/close, double activation, initialization failures, and missing optional runtimes do not leak windows/tasks or trigger implicit retries/execution.

### 6. Security

1. Local-first behavior remains intact: no telemetry, hosted calls, credential access, added shell execution, or automatic dataset discovery/inspection is introduced.
2. Existing exact trusted approvals, Run reviewed plan, project ownership, and chat-handoff isolation remain enforced; UI styling creates no authority.
3. User project names and paths remain untrusted display data, preserving text safely without markup/command interpretation or path inference from model output.
4. New status/errors/logs do not expose dataset contents, hidden chat content, credentials, or unnecessary private paths; exact storage path is intentionally shown only in the picker review context.
5. Invalid catalog/storage state and busy/stale lifecycle races fail closed without silent repair, cross-project substitution, or catalog migration.

### 7. Performance

1. Selection/filter/rendering remains bounded by the real 32-project catalog and adds no polling, ambient animation, always-on rendering, or unbounded retained history.
2. Picker load/open/create adds no worker installation/start, model inference, dataset scan, or scientific computation outside explicit existing actions.
3. UI operations avoid synchronous blocking, redundant reload loops, and event-handler accumulation; cancellation/closing releases owned transient work.
4. Existing resource/timeout/admission limits are unchanged and supported regression checks retain their original thresholds.
5. Real native performance observations or measurements document startup/idle/repeated-use behavior and limitations, with no invented timings or hardware claims.

### 8. Maintainability

1. Production changes remain cohesive to shared opt-in resources and the picker, preserving unrelated windows and current user workflows.
2. Resource names, token mappings, style opt-in, theme/high-contrast fallback, and necessary exceptions are documented for later bounded slices.
3. State transitions and errors use clear typed or otherwise explicit boundaries, avoid duplicated permission/selection logic, and are independently testable.
4. Tests assert meaningful behavior and native evidence rather than merely mirroring markup; fixtures, platform prerequisites, and evidence provenance are clear.
5. CHANGELOG, DEVELOPMENT_STATUS, TESTING, and relevant usage/design docs describe actual delivered behavior and remaining gaps without rewriting historical receipts.

### 9. UX/UI

1. Actual before/after native screenshots demonstrate clear hierarchy, readable project list, selected identity/exact path, explicit Create/Open, and preserved Muon identity.
2. Enabled controls perform their stated action; empty/loading/validation/busy/stale/corrupt/error/success states provide truthful copy and practical recovery without invented timestamps/counts/progress.
3. Keyboard navigation reaches all controls with visible focus and sensible focus return; UI Automation exposes useful names, selected state, and relevant status announcements without focus stealing.
4. Narrow/high-DPI layouts preserve the full selected path and reachable primary/cancel actions without clipped controls, unusable horizontal scrolling, or hover-only functions; high contrast remains legible.
5. A separate fresh critic compares actual same-viewport native Mu evidence with fetched named comparable screens using the requested blind binary method; outcome and largest deficit are preserved, or unavailable evidence is explicitly blocking/unverified.

### 10. Requirement Compliance

1. A focused feature branch derives from verified Chat handoff head without merging/rebasing unrelated PRs, preserves ancestry, and is prepared as a draft PR against the intended base.
2. Actual native baseline precedes design decisions, authoritative v3 manual/tokens/skill and Mu blueprint are inspected, and browser prototypes remain labeled illustrative.
3. Existing legacy project behavior, project IDs/paths, 32-project limit, job transitions, explicit selection, resource/privacy/scientific boundaries, and 136×132-DIP Muon remain preserved.
4. No inert navigation, fake telemetry, automatic analysis, storage migration, unlicensed bundled fonts, new web stack, or unrelated feature/approval change enters the increment.
5. Handoff includes exact source/evidence checkpoints, actual native before/after captures, independent QA and critic history, measured checks, unresolved blockers, and the next separately reviewable UI slice.

## Review procedure

The independent engineering QA agent owns this protocol, separate tests, and
criterion-level reports, and makes no production edits. The implementation agent
cannot substitute self-scores. Builds sharing bin/obj are serialized. Actual
Windows/WPF behavior is distinguished from Linux cross-compilation/source checks;
UI Automation screenshots alone do not prove keyboard input, screen-reader use,
physical-monitor DPI, reduced-motion preferences, or real-user task timing.

Native results must identify the source SHA, executable/configuration, window/work
area dimensions, effective DPI, fixture, actions, output artifacts, and failures.
Preserve initial failed reviews and earlier benchmark receipts. Each review uses
the repository QA report template, names the top five ranked fixes/improvements,
and lists regression risks and remaining tests, including on a passing review.
If available evidence is insufficient, retain an incomplete/failed verdict and
continue safe repairs; do not qualify hardware or invent a blind visual win.
