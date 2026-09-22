# QA REVIEW

Phase: 0 — architecture, requirements, threat model, repository and minimal build

Overall Score: 9.4 / 10

Result: PASS

Reviewer: independent `phase0_qa` development agent, 2026-09-21. Review began against
the empty workspace and continued at scaffold, architecture and executable checkpoints.
The reviewer did not implement production code; it authored the independent negative
verification script and this report.

Category Scores:
- Code Correctness: 10 / 10
- Architecture: 10 / 10
- Mathematical Correctness: 10 / 10
- Testing: 9 / 10
- Reliability: 9 / 10
- Security: 10 / 10
- Performance: 9 / 10
- Maintainability: 9 / 10
- UX/UI: 9 / 10
- Requirement Compliance: 9 / 10

## Evidence:

### Scope and scoring

This score applies only to the requested Phase 0 foundation. It is not a score for a
finished assistant, a production-readiness certification or evidence that deferred
capabilities work. The three empty library projects explicitly reserve boundaries.
There is no implemented AI provider, avatar, audio pipeline, storage, mathematical
engine or tool executor to validate in this phase.

Per `docs/QA_PROCESS.md`, each category contains five equally valued criteria: 0 means
absent/contradicted, 1 means partial evidence, and 2 means satisfied within Phase 0.
All categories have equal weight. The arithmetic is:

`(10 + 10 + 10 + 9 + 9 + 10 + 9 + 9 + 9 + 9) / 10 = 94 / 10 = 9.4`.

The reviewer independently executed PowerShell decimal score and threshold assertions:
94 / 10 equals 9.4; 85 / 10 does **not** exceed 8.5; 86 / 10 **does** exceed 8.5.
All assertions passed, exit 0. No critical unresolved Phase 0 defect overrides the score.

| Category | Five scored criteria and supporting evidence |
|---|---|
| Code Correctness | **2** Release strict compilation succeeds; **2** Debug strict compilation succeeds; **2** real XAML content constructs/layouts and exits on its STA dispatcher; **2** invalid/extra application arguments exit 2; **2** inspected all production source (`App.xaml.cs`, `FoundationWindow.xaml.cs`, XAML and manifest), with no fake services, ignored exceptions or hidden background workload. |
| Architecture | **2** documented stack tradeoffs and inspected installed SDK; **2** all four dependency boundaries pass executable checks; **2** provider/conversation/tool/memory/audio/rendering ports specify ownership, cancellation and failures in `docs/CONTRACTS.md`; **2** whole-system concurrency, privacy, storage, UI and scientific responsibilities are assigned in `ARCHITECTURE.md`; **2** future IPC versioning, resource ownership and worker isolation are explicit without unnecessary present dependencies. |
| Mathematical Correctness | **2** absence of runtime numerical implementation is accurately disclosed; **2** deterministic computation and independent numerical oracles are mandatory; **2** units, affine temperature distinctions, finite/domain boundaries and absolute/relative tolerances are specified; **2** first-order covariance formula `J Σ Jᵀ` and small-error/differentiability limitations are correctly described in `TESTING.md`; **2** reviewer independently computed score and exact strict-threshold boundaries. No runtime scientific accuracy claim follows from these design checks. |
| Testing | **2** eleven architecture assertions genuinely execute; **2** smoke exercises actual compiled XAML/STA/layout/lifetime; **2** adversarial fixture mutations and invalid invocations fail with asserted diagnostics and exit codes; **2** repeatable scripts, bounded processes and retained negative evidence permit reruns; **1** standard test discovery, machine-readable runner output and CI remain explicitly deferred before Phase 1 production logic. |
| Reliability | **2** locked restore succeeds without external feeds; **2** Release and Debug smoke terminate successfully under a deadline; **2** invalid invocations have tested failure codes and malformed XML is contained as a check failure; **2** recovery/cancellation/migration/unknown-side-effect design has named later acceptance tests; **1** visible-window interaction, user-driven close/restart and actual OS presentation lifecycle remain untested. |
| Security | **2** manifest explicitly requests `asInvoker` and no UIAccess; **2** reviewed executable has no provider, shell, capture, document parser or persistence attack surface; **2** threat matrix covers prompt injection, path/URL/credential/renderer/worker/consent boundaries and adversarial owners; **2** independent authorization, single-use operation-bound grants and temporary-mode exclusions are specified; **2** offline source configuration, project-only lock files and sensitive artifact ignore rules were inspected and checked. These are foundation and design checks, not tested future controls. |
| Performance | **2** quantified targets are marked unmeasured; **2** bounded queues/streams/token budgets, cancellation and nonblocking UI ownership are designed; **2** hidden/idle animation reduction and on-demand WebView resource costs are addressed; **2** current tiny executable has no polling, network retry loop or third-party runtime dependency; **1** actual idle/startup/rendering/resource measurements and trace artifacts remain deferred. |
| Maintainability | **2** small coherent layered projects and clear dependency direction; **2** pinned SDK, locked restore, analyzers, warnings as errors and editor settings; **2** repeatable positive/negative developer commands and explicitly documented harness limitations; **2** architecture, threat model, contracts, roadmap and status explain deferred work; **1** Git was initialized and staged, but an author-attributed commit cannot be made without configured identity; the implementation agent is finalizing an explicitly identity-free tree checkpoint. |
| UX/UI | **2** build window truthfully labels its foundation status and has a named Close action; **2** XAML uses system color resources, wrapping text, native controls and minimum dimensions; **2** `docs/UX_SPEC.md` defines summon/hide/tray/hotkey/focus and progressive disclosure; **2** accessible states, capture indicators, private-mode disclosure and useful errors have named design requirements; **1** no actual visible rendering, screen reader, keyboard or mixed-DPI session was exercised. |
| Requirement Compliance | **2** initial empty-workspace preservation/toolchain inspection was performed; **2** full brief is mapped through 24 requirement groups and phases; **2** all seven required root documents plus contracts/QA/UX/requirements exist and were read; **2** minimally runnable build, independent review, required categories/top five and strict score gate are delivered; **1** source checkpoint finalization and the final status synchronization follow this report, and a conventional commit awaits legitimate local Git identity. |

### Commands actually executed by the independent reviewer

Environment: Windows build 10.0.26200, x64, .NET SDK 10.0.401 and WindowsDesktop runtime
10.0.12. `dotnet --info` was inspected independently. No installed .NET workloads were
reported. No existing workspace files or `AGENTS.md` were found at the initial check.

| Command / activity | Observed result |
|---|---|
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1 -Configuration Release` | Exit 0; locked restore; five projects built; 0 warnings, 0 errors; 11/11 checks; WPF XAML/content-layout/STA shutdown smoke passed. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1 -Configuration Debug` | Exit 0; locked restore; five projects built; 0 warnings, 0 errors; 11/11 checks; same actual-content smoke passed. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-negative.ps1 -Configuration Release` | Exit 0; 9/9 negative/control checks passed. Requires a prior build; modifies only a new ignored fixture under `artifacts/qa/`. |
| Manual source/configuration/document inspection | All production C#/XAML, project/build/manifest/lock configuration, both verification scripts and planning documents reviewed. Structural checks do not replace this source inspection. |
| Independent arithmetic and threshold assertions | Exit 0; 9.4 score and exact 8.5-fails / 8.6-passes boundary confirmed. |

After the final process-ownership correction to `verify.ps1`, the reviewer inspected
the revised script and independently reran the Release command: exit 0, 0 warnings,
0 errors, 11/11 architecture checks and WPF smoke PASS. The Debug build was verified
before that script-only correction; no C#/XAML/project inputs changed afterward.

The negative suite independently observed:

1. Valid fixture: exit 0, exactly 11/11 architecture checks.
2. Checker missing argument: exit 2 and usage diagnostic.
3. Checker invalid repository root: exit 2 and usage diagnostic.
4. Forbidden Core → Desktop reference: exit 1 and the expected Core-boundary failure.
5. Empty required README: exit 1 and the expected required-document failure.
6. Malformed project XML: exit 1 and the expected Core-boundary failure.
7. Invalid application flag: exit 2.
8. Extra application flag: exit 2.
9. Restored fixture: exit 0, exactly 11/11 architecture checks.

Successful negative evidence is retained locally at
`artifacts/qa/negative-20260921-030449-8a2f527a/`. Future runs create new directories.
Generated evidence is ignored by Git; the script and this durable report are source files.

### Defects found and addressed during development

- The implementation agent's first smoke run measured a never-shown `Window`, whose
  desired size was zero. It returned exit 1 rather than silently passing. The fix measures
  the real XAML content directly, and the reviewer independently verified both builds.
  This remains a content-layout check, not an HWND/compositor rendering check.
- Reviewer feedback corrected wording that implied the user had forbidden later phases;
  the documentation now describes the initial delivery's Phase 0 scope accurately.
- Review of the initial negative fixture caught that a string replacement would not
  change a self-closing project file. The fixture now writes an explicit prohibited
  reference, and the expected failure is observed.
- The first negative-harness execution exposed a PowerShell `Start-Process` fast-process
  result with a null exit code despite successful checker output. The harness now uses
  a directly owned `Process`, hidden execution, bounded wait and asynchronous output
  draining; actual expected exit codes 0, 1 and 2 were verified. The implementation
  agent applied the same process-ownership fix to the main verification script.

Early inspections and failed verification attempts were preliminary checkpoints, not
completed scored reviews. This is the first complete Phase 0 review; failures have not
been erased or reclassified as passing tests.

## TOP 5 RANKED FIXES / IMPROVEMENTS

1. **Establish standard test discovery and CI before production logic (Phase 1).**
   Problem: The dependency-free harness is explicit and useful, but `dotnet test` does
   not execute it and there is no CI/discovery/reporting baseline.
   Why it matters: Future regression coverage must be reliably discovered and enforced.
   Proposed fix: Review/pin a maintained test framework; integrate unit/integration
   discovery, machine-readable output and these existing invariant checks into CI.

2. **Validate real Windows presentation and accessibility (Phases 1–2).**
   Problem: Hidden content layout does not verify visible rendering, Close behavior,
   keyboard navigation, screen readers, tray recovery or mixed-DPI/monitor behavior.
   Why it matters: The desktop companion's core experience depends on those OS paths.
   Proposed fix: Add actual visible launch/close UI automation and the planned manual
   mixed-DPI, negative-coordinate, monitor-removal and click-through recovery matrix.

3. **Finalize source history with a legitimate author identity.**
   Problem: There is no configured local Git author identity and hence no conventional
   author-attributed commit yet; an identity-free tree checkpoint is explicitly planned.
   Why it matters: Normal commit history improves review, rollback and future phase links.
   Proposed fix: Preserve the tree checkpoint now; when a real author identity is
   available, create the normal phase commit without fabricating identity or rewriting
   historical claims. Keep checkpoint type and status truthful in development status.

4. **Turn security contracts into adversarial tests before enabling capabilities.**
   Problem: Grants, private repositories, endpoint restrictions and renderer isolation
   are thoroughly designed but not implemented or tested as real protections.
   Why it matters: Documentation cannot enforce authority or prevent private data leaks.
   Proposed fix: Introduce executable redaction/config tests in Phase 1, provider/private
   sentinel/renderer tests in Phase 3, and approval replay/TOCTOU/audit-failure cases
   before any consequential tool is enabled. Keep model text outside the authority path.

5. **Capture performance baselines when the actual avatar exists (Phase 2).**
   Problem: CPU, memory, startup and frame targets have no measured workload yet.
   Why it matters: Transparency, animation and the eventual WebView can change resource use.
   Proposed fix: Record reference hardware and Release traces for hidden/idle/visible
   states, startup/cancellation latency and resource growth; retain separate chat-process
   budgets. Do not present this foundation's compilation time as an application benchmark.

## Regression Risks:

- SDK or project changes may invalidate exact dependency/lock/source assertions.
- Windows presentation and monitor behavior can fail while hidden layout still passes.
- Introducing packages changes the offline baseline and requires explicit source review.
- New adapters can violate design-only privacy/permission contracts unless enforced by tests.
- Process-launch changes must preserve bounded waits and actual exit-code propagation.

## Tests Required:

The executed build, structural, real-XAML and negative tests satisfy the current Phase 0
scope. Repeat relevant checks if executable/build/script inputs change. Source checkpoint
and status finalization need a final consistency check. The five improvements above and
`ROADMAP.md` define the next owning phases' required tests; none is falsely counted here
as already passed. No API, microphone, database, provider, memory, mathematical engine,
multi-monitor, screen-reader, compositor or installer validation was performed.

## Final Verdict:

**PASS for Phase 0 only: 9.4 > 8.5.** The architecture, threat model, traceable roadmap,
minimal runnable foundation and independently exercised verification are sufficient
for the Phase 0 gate. Top-five improvements and documented evidence gaps remain visible.
Future phases require their own implementation evidence and independent score above 8.5.
