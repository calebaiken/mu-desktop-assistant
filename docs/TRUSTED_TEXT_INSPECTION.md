# Approved read-only text inspection

Bounded increment, 2026-10-19. Required Windows CI gates pass on source `a999ae4`,
including the production reader and native inspection-window/control smoke. Detailed
conditional symlink outcomes, physical interaction/accessibility and host resource
qualification remain separate. Testing and docs/qa record the actual evidence and
independent verdict; this does not certify a release or the larger selected modules.

## Use

Open Mu's **More** (or companion right-click) menu and select **Inspect text file**.
Enable text inspection for this window, then select a `.txt` or `.md` file. The
application checks metadata and presents the exact local target, byte length,
read-only operation and approval expiry. No file text is read during this preview.
Choose **Approve once and inspect**, or **Cancel / clear**. A new read requires a new
selection and approval. Dismissal, timeout, disable, sleep, closing and app shutdown
invalidate outstanding authority. Enablement never persists between window sessions.

Results are untrusted literal TextBox text: no Markdown/HTML navigation, command
parsing, model invocation or automatic action follows file instructions. The panel
is independent of Chat and ML Lab; it neither reads chat context nor sends file text
to Temporary/Saved chats, memory, datasets, a model, telemetry or a remote service.
Closing/clearing removes this panel's references; it cannot promise erasure from OS
paging, dumps or copies the user makes. Metadata-only local activity logs retain
operation IDs and closed event codes under the existing rotation/90-day policy.

## Exact boundaries

- One registered `mu.text.inspect` version-1 capability, disabled until the user
  enables the panel. Unknown IDs, versions, risks and absent capabilities deny.
  Arguments are a typed selected path; there is no arbitrary JSON command parser.
- Fixed local Windows drive, absolute drive-letter path, `.txt`/`.md`, at most
  1,024 UTF-16 path units and 32 components. UNC/device/mapped/removable targets,
  alternate streams, ambiguous separators/traversal/trailing dots/spaces, reserved
  DOS names and unsafe/invisible controls are refused.
- Up to **65,536 source bytes** and **32,768 decoded UTF-16 units**. Accept UTF-8
  (with/without BOM) or BOM-marked UTF-16 LE/BE, with strict malformed-Unicode
  rejection. NUL/binary/control and Unicode Format characters are refused; tab,
  CR and LF remain literal. No archive, rich document or encoding fallback.
- The Windows adapter opens/retains the drive, each directory and file with
  `FILE_FLAG_OPEN_REPARSE_POINT`; each must be an ordinary disk object with the
  exact final handle path and no reparse/offline/recall/device attributes. Directory
  and leaf handles deny write/delete sharing through the preview/read. The leaf
  also records volume/file ID, creation/write times and size, checked again around
  the read. Holding validated handles avoids reopening by an attacker-controlled
  name after approval. Existing writers cause refusal; cancel/expiry releases leases.
- A sealed process-local preview binds its immutable proposal/arguments, selected
  resource and descriptor to one executor/session. Only the trusted UI approval
  port can issue the opaque grant. Enable/disable/revocation changes invalidate old
  authority. Grants expire after **60 monotonic seconds** and consume atomically
  before any read, including failed/cancelled attempts. No durable grants or blanket
  directory access are created. The one-step action is not an autonomous task planner.
- Preparation/approval/read have **5-second operation budgets**. Noncooperative
  callbacks get **250 ms** to settle after cancellation; terminal audit has its own
  **5-second budget** (and settle allowance). A worst-case read/result response can
  therefore approach 10.5 seconds; “5-second read” is not a five-second total-UI promise.
  No queue exists. Late results are discarded and the App-owned executor is poisoned
  while retaining cleanup ownership, preventing reuse by reopening the window.
  Final disposal waits up to **3 seconds** for deferred cleanup and reports failure
  if unresolved. A Windows kernel I/O stall cannot be forcibly undone by managed
  cancellation; returning to UI does not certify the underlying I/O has exited.
- Audit intent precedes reading. Proposal/authorization/start/terminal events use
  the existing enum-only `IEventLog`; paths, file identities and text are excluded.
  Audit failure prevents reading or withholds its result. A completed audit denotes
  completion of the bounded read; revocation during terminal audit can still suppress
  delivery. These audit events are not durable recovery checkpoints for user content.

No new dependency or worker is needed for a 64 KiB read. Two bounded byte/text buffers,
one transient view and at most 33 handles avoid model startup or a persistent polling
service; the one-second expiry UI timer runs only for a pending preview. Idle host
budgets remain unchanged and have not been requalified by this increment. This is a
defense against untrusted paths/text and ordinary concurrent replacement, not against
arbitrary malicious code already executing as the same Windows user or a kernel flaw.

## Verification

Run `scripts/verify.ps1` then `scripts/verify-negative.ps1` as required by AGENTS.md on
Windows 11 x64. The ordinary composed `--smoke` now also instantiates the actual WPF
inspection controls/native HWND with the production Windows reader and an isolated public fixture:
default deny → enable → metadata-only preview → approved literal result → no UI
replay → disable/clear → released file lease. It bypasses the native file-picker
dialog only to supply that fixture; it is not physical input/accessibility evidence.

`TextInspectionTests` includes real FileStream decoding, limits, malformed text and
Windows-gated real pipeline/audit/reparse/ancestor-rename tests. `IndependentTrustedActionTests`
owns separate replay/race/expiry/failure probes. The three Windows-only tests skip on
Linux, and symbolic-link fixture creation additionally requires Developer Mode or
the Windows symlink privilege. A skip is not a successful native check.
