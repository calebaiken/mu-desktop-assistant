# Phase 3 history, rendering and context evidence

Date: 2026-09-22 (local); later artifacts use UTC 2026-09-23. Source started from
994a43a / refs/checkpoints/compact-avatar-increment. This report attributes executed
evidence; independent scoring lives in PHASE_3_HISTORY_REVIEW.md. Neither report
changes historical Phase 2 CPU failures or unexecuted release qualifications.

## Implemented scope

Explicit SQLite saved history; default separate in-memory Temporary mode; titles,
literal search, deletion, immutable edited/regenerated branches, pending-prompt
commits and interrupted-turn recovery. Write failures retain output and offer retry
or explicit temporary continuation. Earlier saved messages remain, as the UI states.
Exact worker-template/tokenizer admission reserves 256 response tokens within 2,048.
Native Markdown/code/math uses fixed WPF elements and shared parsing/render bounds,
literal role labels, inert HTML/links/images and plain-text fallback. A brief reply
preview appears beside the character only when chat is inactive; it does not resize
the companion and dismisses on activation, closing, hiding, sleep or expiry.

## Retained execution sequence

1. First strict build found seven analyzer issues in new tests (six obsolete data
   attributes and one constant-identity assertion). These were corrected without
   loosening analyzers; the subsequent suite passed **267/267**, zero skipped.
   artifacts/qa/phase3-history-verification/phase3-history-first.trx, SHA256
   AE50903D0A118A8D4E4ECF617F6C7BECDDAA2C2144B0607FE2E3E0ABEBFBE4B0.
2. Native desktop-ui-20260922-213916-c60d9fbc passed the existing 20 supervisor
   controls and both 50-case history / 42-case renderer suites. Four additional
   renderer boundary cases were not yet in that binary; this run is not a 46-case pass.
3. Strict Release verification again passed **267/267**, **11/11 architecture**,
   composed WPF smoke, zero compiler warnings/errors. Retained TRX:
   artifacts/qa/phase3-history-verification/phase3-history-pre-recovery.trx.
   Native desktop-ui-20260922-214203-090a483a passed **22/22 supervisor controls**,
   with independently retained first-run and profile reports: compact51,
   appearance43, integration24, settings29, placement49, chat54, history50, renderer46.
4. Abrupt-exit Temporary privacy probe **PASS**:
   artifacts/qa/private-history-20260922-214333-88549cc1/result.json. The supervisor
   terminated only its held child process after readiness, then scanned every bounded
   profile file (including a real saved decoy database) for public temporary prompt/
   reply sentinels in UTF-8, UTF-16LE and UTF-16BE. No sentinel matched. This does not
   inspect OS paging/dumps or prove forensic erasure. Executable/assembly hashes and
   per-file hashes are retained.
5. Frozen context/model protocol **PASS**:
   artifacts/qa/local-model-20260922-214337-9c4ffcd3. A/B/C, cancellation/reuse,
   oversized-latest rejection with zero deltas/completions, long-history completion,
   owned idle-worker death/restart, disposal and cancellation after startup all pass.
   All public inputs/outputs retained, no selected retries. Independent reviewer
   manually assessed C's three relevant desk tips as passing its narrow criterion.
   Ready3.764s, total36.695s, sampled maximum worker private bytes360,431,616.
   This excludes mapped physical memory and is not a lifetime/whole-product peak.
   Exact live token counts and number of pruned pairs were not exposed/measured;
   unit fixtures test the 1,792/1,793 boundary and pair-removal behavior.
6. Independent review found read-only schema admission prevented SQLite hot-journal
   rollback (extended error776). A guarded recovered-copy validation path and five
   independently authored genuine SQLite spilled-journal snapshot tests were added.
   Tests distinguish recoverable committed history, unsupported/corrupt original
   preservation, bounded header metadata and super-journal rejection. These are
   SQLite-generated interrupted-write snapshots, not an actual process-kill claim.
7. The reply-preview build first failed on an incorrect AutomationLiveSetting
   namespace. That compile error was fixed; final verification is recorded below.
8. A restricted-context run built cleanly but returned 240 passed / 32 failed from
   272 tests. Twenty-seven established atomic-file/registry fixtures hit sandbox
   UnauthorizedAccess; five new journal fixtures could not read an open writer's
   journal with File.ReadAllBytes. The failed TRX remains at
   artifacts/qa/phase3-history-verification/phase3-history-restricted-failure.trx.
   The fixture now uses explicitly shared, bounded reads while its SQLite connection
   is idle. No production recovery assertion had run in those five failed cases.
9. Approved normal-user final strict verification: **272/272 passed, zero failed/
   skipped**, **11/11 architecture**, zero warnings/errors and composed WPF smoke.
   TRX: artifacts/qa/phase3-history-verification/phase3-history-final-272.trx,
   SHA256 C8C33B7272B93BF579FEB4FD404D0FC1A1CC4433BC773F4A30675EA511D0A06A.
   All five independent journal cases passed. Final native run
   desktop-ui-20260922-215621-2d737c27 passed **28/28 supervisor controls**, with
   both retained runs passing prior suites plus history50, renderer46 and reply34.
   Five negative CLI controls prevent the private-history diagnostic from using an
   implicit/default profile, conflicting/duplicate modes or a model directory.
10. Final abrupt-exit privacy **PASS**, retained at
    artifacts/qa/private-history-20260922-215831-6eee7933/result.json. Its Desktop
    and Infrastructure hashes match the final built files; independent review also
    rescanned the raw files. No additional live-model rerun was performed after
    storage/preview-only edits; item5 retains its earlier exact binary identity.

## Actual native input sample

Using the supported computer-use skill against the isolated profile
artifacts/qa/history-input-20260922, root opened Chat with Ctrl+Shift+C, selected
Saved chats, entered a public desk-organization prompt using the accessibility value
setter, clicked Send and minimized chat while loading. On completion the avatar
returned to Ready and a compact three-line preview appeared above it. It expired
before the attempted Open chat click, so that click failed with a stale-element
message and is not counted as a passing input check. A fresh observation confirmed
the preview gone; Ctrl+Shift+C restored chat. The saved title, branch control, rendered
list and Plain text toggle were visibly usable. Closing chat then cleared its window.
The helper's focused-element field did not reliably track pointer focus, so the
message was set through its explicit observed edit element rather than unverified
typing. This does not qualify screen-reader focus behavior.

This exploratory model prompt asked for a numbered list; the model returned three
bullets instead. Preserve that instruction-following miss: the exercise establishes
UI integration, not a new model-quality pass. The public output remains in the
isolated saved database. Independent review approved Phase 3 development at
**9.5/10 (95/100)**; mandatory release and broad-capability qualifications remain.

Items 1–5 predate the recovery fix and reply preview. Final-source regression must
be reported separately rather than relabeling their binary identity. Model inference
implementation is unchanged by the recovery/preview edits.

## Boundaries

Saved data is local plaintext. A pending prompt survives a completed pre-inference
commit; stream fragments not finalized before a crash can be lost. Deleted rows or
temporary managed strings are not secure erasure. Recovery validates bounded copies
before mutating an original; arbitrary same-user replacement after lease release
remains outside the application profile-lock guarantee. Future/corrupt original
files are preserved. No schema upgrade has yet been introduced or claimed tested.
Recovery uses conversations.recovery.db, its sidecars and an ownership marker.
A hard kill during validation can retain bounded plaintext saved-history copies
until a later recovery cleanup; these are not automatic backups. Unknown scratch
files without a valid ownership marker are refused unchanged (source-reviewed).

Native smoke checks exercise owned real WPF/HWND layout and controlled event paths;
they do not substitute for physical input, assistive technology, mixed-DPI or device
qualification. The reply timer helper shortens its diagnostic interval to test the
same Tick handler and separately checks the production20-second interval. Model
quality remains limited to narrow public cases. Worker create-to-job-assignment
crash hardening, actual midstream process death and broader model evaluation remain
explicit limitations. Voice, tools and approved long-term memory are later phases.
