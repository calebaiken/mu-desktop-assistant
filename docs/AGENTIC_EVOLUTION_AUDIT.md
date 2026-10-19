# Agentic evolution audit — 2026-10-19

Selected modules: **A, B, C, G, I**. First increment: A's approved, read-only
plain-text inspection, with I's reliability/security checks. This is a development
candidate; native Windows qualification and the independent phase exit remain gates.

The audit below records the starting state. Subsequent implementation and Windows
CI at `a999ae4` are recorded in [Testing](../TESTING.md) and docs/qa; the first bounded
workflow is now wired and passes both required remote verification gates. Remaining
physical/resource and detailed conditional-path qualification are still separate.

The follow-up `feature/research-projects` candidate adds explicit project creation/
selection and serialized existing-Lab ownership on top of the first slice; see
[its bounded workflow and verification scope](RESEARCH_PROJECTS.md). The inventory
below deliberately retains the original main audit rather than claiming Chat
handoff, memory or model-profile follow-ups have been implemented.

## Source and PR inspection

- Repository: `https://github.com/calebaiken/mu-desktop-assistant`; Git's default
  branch is `main`, at `7fe3305b49f755a94d65362692e783ef0e60035a` when inspected.
  The cloud checkout started clean on local branch `work` at that same commit.
  Work continues on `feature/trusted-text-inspection`; main was not replaced.
- GitHub's repository page labels the repository **Public**, contradicting README's
  previous private label. Read-only Git fetch succeeds using existing authentication.
  The initial GraphQL request was denied; a subsequent REST check confirmed public
  visibility, default branch and push permissions with existing authentication.
  Both the paginated REST open-PR listing and public page showed only #1 at audit
  time; Git advertised only `refs/pull/1/head`. No new credential is required.
- [PR #1](https://github.com/calebaiken/mu-desktop-assistant/pull/1) remains open,
  `feat/ml-lab-hdf5-waveform` → `main`, head
  `8c0e1190b2c7b1ca4090f8285393f62052abf13c`. Its 17 changed files include bounded
  HDF5 attribute previews, NPZ header metadata, numeric 3D channel selectors,
  15 time/frequency waveform features, standalone export parity, WPF selector
  autofill, independent Python fixtures and expanded Windows CI.
- Its current checks page and commit status partial reported **2/2 checks OK** at
  that exact head, including Windows runs
  [38906604006](https://github.com/calebaiken/mu-desktop-assistant/actions/runs/38906604006)
  and [38906611324](https://github.com/calebaiken/mu-desktop-assistant/actions/runs/38906611324).
  This observation is distinct from the older run IDs/test counts in its PR body.
  Review comments and the final corrective commits were examined: variable-length
  attribute admission, selector escaping, untruncated selector data, numeric-only
  recommendations, zero crossings, NPZ headers, fixture timeouts and artifact paths.
  This task neither merges that PR nor copies its implementation.

## Actual capability inventory

| Area | Implemented in the inspected main source | Partial, absent or unqualified |
|---|---|---|
| Foundation | Four C# layers, .NET 10.0.401, strict locked restore, WPF composition, settings/placement recovery, bounded enum-only logs, single instance | Final CPU qualification, physical display/input/accessibility/sign-in, installer/release remain open |
| Chat | Owned local Qwen/llama.cpp worker, streaming/cancel, separate in-memory Temporary and SQLite Saved stores, branches/search, bounded native rendering | General reasoning/scientific accuracy not established by narrow fixtures; no tool-call authority |
| A | `IToolExecutor`, `ITool`, `IPermissionService`, proposals and task plans existed **only in docs/CONTRACTS.md**, not C# interfaces | This candidate implements one action/one preview/one grant; autonomous planning, shell, mutations and plugins stay unavailable |
| B | App-owned integrated ML Lab; bounded CSV/JSON/HDF5/NPY/NPZ readers; training/plots/prediction; exact-cohort comparison; standalone reproduction; current-selection alias and reviewed single-use Run | Project UI is hardcoded to `ml-lab/default-project`; 3D/waveform/attribute additions belong to PR #1; general units, uncertainty and advanced waveform analyses are not main capabilities |
| C | Conversation history and settings, not approved long-term memory | No `IMemoryProvider` implementation, project memory approval/editor/retrieval/migrations; no embeddings needed for first increment |
| G | Pinned lazy local chat model, verified runtime/assets, Windows jobs and cleanup; separate optional LAYA/Kokoro resource admission | No model picker/profiles or cross-workload scheduler. Full LAYA inference, final neural reuse and model coexistence remain memory-gated/unqualified |
| D/E/F/H | Existing speech output and avatar behavior remain | Voice-input WIP is preserved/excluded from Desktop compilation. Context capture and Git actions are unselected. No new capture, dev assistant or avatar feature is introduced |

Application owns package-free contracts/workflows; Infrastructure owns native file,
SQLite and worker adapters; Desktop composes controls/services. Python scientific
workers remain separate, bounded and offline. No dependencies, lockfiles, layer
references, model settings or resource acceptance thresholds are changed here.

## Documentation inconsistencies

- README called the public repository private; SECURITY claimed no remote project
  existed. Those descriptions are corrected. A public security contact is still absent.
- DEVELOPMENT_STATUS called the entire checkpoint uncommitted and based on `22f3dae`;
  it is now committed in the inspected `7fe3305` tree. The old receipt is retained
  and explicitly labeled historical rather than reattributed to this candidate.
- TESTING claimed CI had never run although later passages and real GitHub checks
  describe executions. Its historical statement is clarified.
- AGENTS said NuGet had two sources: the enforced XML actually contains two child
  elements (`clear` plus one NuGet source). Its wording is corrected, not the check.
- SDK `latestPatch` allows patches in the same feature band; the architecture check
  verifies the pinned configuration values rather than rejecting every possible
  later-patch executable. Setup uses the exact pinned SDK here.
- The AGENTS 32-skip heading describes historical optional-interpreter cases; actual
  Linux baseline has 31 skips and 23 failures (one native job test fails before the
  optional interpreter check). Historical Windows receipts are not Linux guarantees.
- “Installed on this PC” and `artifacts/Mu` receipts describe the original Windows
  machine. Runtime/model assets and historical generated receipts are not in Git and
  were not present in this fresh Linux task. TESTING's “no scientific calculation”
  statement refers to the planned general science engine; existing ML computations
  and PR #1's waveform work have their own narrower scopes.

## First milestone and follow-up dependencies

The first A action is deliberately outside Chat: **More → Inspect text file**,
enable for this window, choose `.txt`/`.md`, inspect the trusted target/limits,
approve once, see literal text, then clear/disable. It does not ingest a file into
any conversation or silently persist it. A one-step proposal/result supplies actual
authority boundaries before a general task DAG is worth implementing.

Threats are model/file-content authority confusion, grant replay, changed arguments
or identity, Windows links/devices/streams, unbounded/invalid text, callback races,
failed audit and leaked handles. Controls/tests are described in
[the action guide](TRUSTED_TEXT_INSPECTION.md) and the independent
[frozen QA protocol](qa/TRUSTED_TEXT_INSPECTION_PROTOCOL.md).

Next reviewable increments, each gated independently:

1. **A/I qualification:** run the complete Windows positive/negative gates, native
   file/reparse/ancestor-swap fixtures and actual picker/keyboard/contrast/sleep/close
   checks; repair any failure before declaring end-to-end delivery. Preserve CPU
   failures and the strict independent score >8.5/zero-critical gate.
2. **B:** review/integrate PR #1 through its own PR process; add explicit project
   identity/selection around the existing Lab service. Reuse its captured dataset
   alias and approved Run for a Chat→Lab bridge; do not route generated paths through
   the text-file capability. Add method/units/provenance display for verified results,
   separate from model explanations. Peak detection, baseline/coincidence/uncertainty
   and notebook export need later implemented numerical oracles and budgets.
3. **C:** build approved SQLite memory against that explicit project identity, with
   per-write provenance, optimistic revisions, inspect/edit/delete/disable, versioned
   recovery and temporary-mode sentinel tests; no automatic chat promotion/embeddings.
4. **G/I:** independently qualify existing Qwen, LAYA and Kokoro load/unload/low-memory
   behavior before model profiles and a shared workload admission policy. Distinguish
   configured/loaded/qualified states and retain each workload's present limits.

Linux supports cross-compilation, pure policy/decoder tests and existing portable ML
tests. It cannot execute WPF or kernel32 file-handle checks. Missing native evidence
limits qualification; it is not grounds to replace Windows or weaken tests.
