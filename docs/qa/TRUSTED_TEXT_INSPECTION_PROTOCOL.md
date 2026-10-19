# Trusted text inspection: independent QA protocol

Frozen 2026-10-19 before implementation review. Scope: the first Module A
vertical slice, from explicit native file selection through reviewed single-use
approval to bounded literal text display. Reviewer: a separate development QA
agent. Modules B/C/G and general tool automation are outside this increment.

Each criterion below receives 0 (missing or contradicted), 1 (partial evidence),
or 2 (satisfied by cited evidence). Category score is the sum of its five criteria.
Overall score is the sum of all 50 criterion scores divided by 10, using exact
decimal arithmetic. More than 8.5/10 and no critical unresolved defect is required
to pass. Missing Windows/runtime/physical evidence is not a passing check. Native
Windows qualification and release resource acceptance remain separate from Linux
source/build/application-policy evidence. No scientific numerical accuracy is
claimed; mathematical criteria cover bound arithmetic and review arithmetic.

Critical blockers regardless of arithmetic: file access without fresh approval;
grant replay or retargeting; executable interpretation of file/model text;
unbounded source materialization; unintended source writes; path redirection
outside the reviewed target; text/path leakage into saved chat, model input,
logs or persistent artifacts; or misrepresenting absent native execution as passed.

| ID | Category | Criterion |
|---|---|---|
| C1 | Code Correctness | A valid selected text file produces the exact expected literal text and leaves source bytes unchanged. |
| C2 | Code Correctness | Invalid/null/malformed typed requests and unsupported capability/version combinations fail closed. |
| C3 | Code Correctness | Preview, authorization and execution refer to the same immutable operation and resource. |
| C4 | Code Correctness | Denied, expired, revoked, stale and replayed operations cannot return file content. |
| C5 | Code Correctness | Exactly one terminal result distinguishes completed, cancelled, denied and failed operations; failures do not expose content. |
| A1 | Architecture | Four-layer project/package/build/manifest boundaries remain enforced by the existing architecture checks. |
| A2 | Architecture | Tool execution has one application entry point; raw infrastructure execution cannot be invoked by model text. |
| A3 | Architecture | Native file identity/path handling is confined to Infrastructure; policy and descriptors remain package-free. |
| A4 | Architecture | UI owns selection/consent only, while execution state, cancellation and permission policy have explicit ownership. |
| A5 | Architecture | Chat/model/history/ML boundaries remain intact; the slice introduces no memory, cloud or unselected module behavior. |
| M1 | Mathematical Correctness | File-byte and decoded-character limits are positive, exact and independently tested at boundary values. |
| M2 | Mathematical Correctness | Grant expiry/deadline comparisons handle equality, clock advancement and cancellation without rounding or extension. |
| M3 | Mathematical Correctness | Resource estimates and displayed sizes/units agree with the enforced bounds; no fabricated measurements are presented. |
| M4 | Mathematical Correctness | Encoding, Unicode length and canonical identity/argument comparisons are explicit and tested, without ambiguous truncation. |
| M5 | Mathematical Correctness | QA criterion/category arithmetic and strict >8.5 decision are shown exactly; no numerical-analysis capability is inferred. |
| T1 | Testing | Strict locked restore/build and all existing architecture checks execute successfully in the stated configuration. |
| T2 | Testing | Deterministic application-policy tests execute normal, denied, malformed, expired, revocation and replay cases. |
| T3 | Testing | Independent adversarial tests or probes exercise real relevant behavior beyond implementation-mirroring assertions. |
| T4 | Testing | Real Windows file-handle/reparse/sharing/identity integration and WPF workflow smoke checks execute, or remain explicitly incomplete. |
| T5 | Testing | Cancellation, resource exhaustion, teardown and affected regression suites execute; skipped/missing checks are individually disclosed. |
| R1 | Reliability | Concurrent execution and approval races cannot duplicate access, exceed admission or reuse consumed authority. |
| R2 | Reliability | Cancellation and timeout bound the operation and prevent late callbacks from reviving closed or replaced UI state. |
| R3 | Reliability | Failure, replacement, deletion, corrupted text and unavailable platform permit safe subsequent use or explicit fail-closed recovery. |
| R4 | Reliability | Every acquired file/native handle, grant and cancellation source is disposed/revoked on completion, expiry, reselection and close. |
| R5 | Reliability | Shutdown and process failure cannot persist file content or leave background activity/locks beyond the owning process. |
| S1 | Security | Explicit trusted UI approval is the sole source of authority, scoped to one exact operation, with default deny. |
| S2 | Security | Windows canonical paths, final-handle identity, reparse points, devices, UNC and ADS are guarded without check-then-open escape. |
| S3 | Security | Opaque expiring grants resist altered arguments, wrong session/operation/policy, replay and concurrent reuse. |
| S4 | Security | Plain-text decoding/output never executes commands, XAML, Markdown links, shell syntax or model instructions. |
| S5 | Security | Audit/failure records exclude content/full paths; no model/history/persistent write or remote call receives selected text. |
| P1 | Performance | Byte/character/concurrency limits precede expensive allocation or decoding; oversized sources fail before full read. |
| P2 | Performance | Time budgets and cancellation have executable evidence, with no unbounded retry, queue or polling loop. |
| P3 | Performance | Native/UI I/O keeps dispatcher work bounded and does not introduce continuous idle work. |
| P4 | Performance | Handles, buffers and retained proposals/results have explicit bounded lifetimes and resource admission. |
| P5 | Performance | Actual resource evidence is distinguished from source inspection; previous failed CPU/memory receipts and thresholds are retained. |
| N1 | Maintainability | Small cohesive contracts, policy, native adapter and UI modules have explicit failure/state/ownership semantics. |
| N2 | Maintainability | Dependencies and SDK pins stay unchanged unless a reviewed inventory/lockfile update justifies them. |
| N3 | Maintainability | Tests use deterministic fixtures, independent expectations and observable behavior; platform requirements are explicit. |
| N4 | Maintainability | Usage/security/architecture/testing docs explain implemented behavior and supported limits. |
| N5 | Maintainability | Status/changelog/report preserve historical evidence and distinguish implemented, unqualified and deferred modules. |
| U1 | UX/UI | The action is reachable through the actual native application with an explicit file picker and usable controls. |
| U2 | UX/UI | Preview shows the exact canonical target, read-only effect, enforced limits and fresh approval requirement before access. |
| U3 | UX/UI | Proposed, approved, running, completed, denied, cancelled and failed states have accurate user feedback. |
| U4 | UX/UI | Output is literal and can be cleared; revocation/expiry/reselection/close prevent stale access and stale displayed success. |
| U5 | UX/UI | Keyboard names/navigation, high contrast and minimum layout are reviewable; physical accessibility is qualified separately. |
| Q1 | Requirement Compliance | Module A's first bounded read-only action is a coherent slice without arbitrary shell/filesystem/app control. |
| Q2 | Requirement Compliance | User control, expiry, exact preview, single-use/replay protection, timeout/cancellation and redacted audit are implemented. |
| Q3 | Requirement Compliance | Temporary chat isolation and offline/private-data constraints are preserved; there is no silent ingestion or persistence. |
| Q4 | Requirement Compliance | Supported Windows gate commands/native tests are run where available; absent prerequisites and qualification limits are reported honestly. |
| Q5 | Requirement Compliance | Focused branch/PR, source identity, appropriate docs and a concrete selected-module follow-up sequence are provided or exact blockers documented. |

Review reports and any failed-review revisions are stored separately under
`docs/qa/`; this frozen protocol does not claim a passing implementation.
