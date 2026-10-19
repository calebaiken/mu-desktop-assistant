# Explicit Chat-to-Research handoff (2026-10-19)

This bounded Module B increment is stacked on PR #3, `7ce7f639c999c79631e786a7365578773e1da530`.
It reuses the existing project catalog, App-owned workspace, planner and ML worker.
Main, PR #1 and PR #2 are not merged or rewritten by this change.

## User workflow

1. In **Saved chats**, enter the research request in the **Message** box. Select
   **Send to Research Lab** instead of sending the draft to the chat model. To reuse
   an earlier user request, use the existing Edit control to put it in that box.
2. Explicitly select/create the destination in the existing research-project picker.
   This selects the active project; changing projects retires its previous view even
   if the subsequent request transfer is cancelled. It runs no ML worker.
3. Review the project name, stable ID, storage location and exact draft in a separate
   confirmation dialog. Cancel, Escape or closing the dialog transfers nothing.
   The disclosure explains that subsequent Lab operations can persist local project
   artifacts independently of chat history.
4. **Confirm transfer** opens that project's **Ask Mu** tab with the exact draft.
   Transfer does not load a model, inspect a dataset, create an experiment or admit
   a worker action. A new Lab defers its normal capabilities/registry startup.
5. If that same open Lab already has an explicitly selected dataset, its path, key
   and row range remain selected. Otherwise choose a dataset in **Data**. Text in
   the request cannot select a file. A closed/reopened view asks for selection again.
6. Select **Propose a plan**. Only this action loads the optional local model. Review
   the exact typed action, dataset, parameters and row/memory/thread budget. Model
   explanations are advisory; they are not verified scientific computation.
7. Only **Run reviewed plan** admits the proposed ML action, once. The existing
   explicit dataset/configuration buttons remain separate user-initiated operations.
   **Load Lab tools** explicitly loads capabilities and saved work in a deferred Lab.

Handoff is disabled in **Temporary Chat**, with an explanation of Lab persistence.
It never converts temporary content into a saved workflow. Pending transfer disables
Chat edits/history changes and duplicate submissions; closing Chat cancels it.

## Contracts and boundaries

- `ResearchRequestHandoff` contains at most 2,000 UTF-16 characters of one exact draft,
  with valid Unicode and no hidden format/control characters (tab/newline allowed).
  It preserves whitespace. It contains no conversation ID, history, assistant reply,
  branch, memory, file contents or clipboard data. It is process-local and disposable;
  acceptance/disposal clears its request reference. This is not secure memory erasure.
- `ResearchLabWorkspace.PrepareHandoff` captures the stable project, exact borrowed
  service lease and work-admission revision. `AcceptHandoff` consumes it once under
  the existing admission/retirement lock, checks cancellation/availability/identity,
  and invokes only a trusted synchronous UI-population callback. Worker admissions
  or project retirement invalidate pending reviews, even if the work later finishes.
  Failed delivery is not replayable. These tickets grant **no ML execution authority**.
- App serializes project selection and handoff, captures exact source/destination
  window instances, and binds cancellation callbacks to their original dialogs.
  Closed/replaced views, shutdown, uncertainty or active work require a fresh review.
- Lab proposal revisions bind the request, mode, dataset path/key/range and compute
  limits. Changes cancel planning, remove Run authority and prevent late model output
  from restoring it. Other Lab work and view closure invalidate proposals as well.
- `MlConversationPlanner` validates every file action against the explicitly captured
  `MlDatasetSelection`, including nonstructured model output. Missing, invented or
  different paths/keys/ranges cannot become file proposals. Lab refuses Propose before
  model loading until an explicit valid selection exists. Selection validation does
  not establish file existence/content: the approved worker validates actual data.
- No handoff logs or hidden persistence are added. Existing worker/project provenance
  remains authoritative for executed computations. No dependencies, remote services,
  shell commands, scientific algorithms, memory or model qualification are added.

## Verification and remaining qualification

The frozen independent protocol is [CHAT_PROJECT_HANDOFF_PROTOCOL.md](qa/CHAT_PROJECT_HANDOFF_PROTOCOL.md).
Both mandatory Windows gates pass on executable checkpoint `a33401f` (push
39307770822 and PR 39307779749): 524 executed/passing C# cases, one explicit platform
skip, and all 28 native handoff checkpoints complete. Actual run evidence and
independent verdicts are recorded in `TESTING.md` and `docs/qa/`; the gates and
15-second smoke deadline remain unchanged. Closing Chat initially exposed a real
modal-lifecycle stall; cancellation now starts at Closing and unwinds the original
modal before WPF owner teardown. Failed run history is retained. The independent [final review](qa/CHAT_PROJECT_HANDOFF_REVIEW_WINDOWS.md)
scores 96/100 = 9.6/10, PASS for this bounded development increment with zero
confirmed unresolved critical defects; physical/model/resource qualifications below
are explicitly excluded. Public native smoke uses
real App routing, WPF windows/controls, production project/service paths, an absent
runtime and explicitly declared model fixtures. Fixtures do not qualify pretrained
inference. Physical keyboard/screen-reader use, installed-model success/coexistence,
CPU/memory qualification and broader crash/low-memory matrices remain separate.

## Separate next increment: Module C proposal only

After this handoff is verified, add a focused project-scoped SQLite memory store:
explicitly preview/approve each write, show provenance and revision, inspect/edit/
delete/disable records, enforce project IDs before retrieval, keep Temporary Chat
isolated, and use versioned recoverable migrations. No embeddings or remote sync.
Design the approval/UI and recovery tests before implementing it in another PR.
