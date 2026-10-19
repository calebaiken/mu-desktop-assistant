# Security and privacy design

Living threat model, not a security certification. Phase 1 adds explicit local preference
storage, content-free diagnostics and same-user activation IPC. Phase 3 adds an owned
local model process, bounded loopback chat client and optional saved conversations.
There is no cloud inference, arbitrary shell execution or recording. Future controls below must
pass negative tests before their feature is enabled.

The 2026-10-19 text inspection candidate adds one user-enabled, read-only `.txt`/`.md`
capability. A metadata-only exact preview precedes fresh, process-local, single-use
approval; monotonic expiry, revocation, no queue and retained validated Windows
handles bind the read to the selected file. Reparse points, devices, remote/alternate
streams and ambiguous paths deny; strict byte/character/encoding limits precede
literal display. Prompt/file text cannot issue grants or call the executor. The panel
is independent of all chat stores and memory; diagnostic events omit target and text.
Cancelled/late callbacks cannot repopulate a cleared/closed panel. Noncooperative I/O
poisons the App-owned executor, retains cleanup ownership and can fail shutdown; a
managed timeout is not proof a blocked kernel operation exited. See
[the precise budgets/threat boundary](docs/TRUSTED_TEXT_INSPECTION.md). Native Windows
file identity/reparse and actual picker/accessibility checks still require execution;
no new OS sandbox or security certification is claimed.

The optional 2026-09-30 neural output backend uses pinned Kokoro ONNX assets and a
separate hash-locked CPU runtime. Explicit setup downloads assets; speech itself
uses no API key, network service or automatic download. Python audit hooks refuse
DNS, connections, datagrams, shell execution and child processes; pinned native
libraries are trusted code, so this is not a general native-code sandbox. The
host supplies a closed environment, bounded private pipes and an owned 1,536 MiB
one-process job before sending text. Asset hashes and Windows read leases prevent
ordinary concurrent file replacement. No arbitrary model/provider/voice URL is
accepted, and the selected voice is never silently replaced on a speech failure.

Normal neural speech creates no audio/transcript file and puts no utterance in
argv, environment or logs. Only explicit isolated public-sample diagnostics export
WAVs. Packaged phonemizer DLL copies use a unique owned TEMP directory, cleaned
after confirmed worker exit, including cancellation. Cleanup checks ownership,
canonical paths, links, descendant counts and a time bound before deleting leaves;
it never sweeps generic TEMP. Stop/new context/close invalidate old callbacks and
terminate only the held worker. Memory admission, text/chunk/audio caps and separate
90-second generation/30-second playback/three-second cleanup budgets remain enforced.
OS paging/dumps, same-user malicious code and device behavior remain outside this
application persistence guarantee. See [neural voice help](docs/MU_NEURAL_VOICES.md).

The 2026-09-30 optional LAYA advisor classifies only the explicit problem description;
it does not read datasets, execute actions or replace the local chat model. A fallible
hint may inform a reviewed proposal only while its exact request remains unchanged.
The App owns one bounded CPU worker: 3 GiB committed-memory job cap, one-process
limit, two-minute deadline, bounded UTF-8 protocol and minimal offline environment.
The worker admits input before native imports and refuses inadequate physical,
system-commit or process-commit headroom. The native real-model diagnostic currently
returns `memory_limit`; successful full-model inference is not a certified behavior.

Explicit setup downloads hash-pinned optional wheels and five checkpoint files;
runtime uses safetensors, refuses links/unexpected files/remote custom code, verifies
hashes and holds Windows read leases throughout loading/inference. Offline library
flags and a Python audit hook deny network operations. These controls contain a
trusted packaged worker; they are not an OS sandbox against hostile Python/native
code or same-user malware. Prompts/scores are not logged or persisted by the worker.
No API credential is needed. Mu does not close other applications to reclaim memory.

Avatar customization stores closed enums, not arbitrary executable themes or remote
assets. SVG/PNG/ICO assets are local and generated from the native vector design.
The shortcut creator preserves an existing link, targets a verified local executable,
and reads back its target, arguments, working directory and icon; Windows taskbar
pinning remains an explicit user operation. The Desktop folder on this PC is inside
OneDrive, so Windows may sync the shortcut according to the user's existing settings;
Mu itself does not upload datasets or perform a cloud sync.

The 2026-09-29 ML foundation adds explicitly requested local computation and durable
Lab artifacts, independent of temporary Chat. Dataset references include local paths;
experiment metadata, selected predictions, model files and plots can contain sensitive
scientific data. These are plaintext project artifacts, not content-free diagnostics
or encrypted storage. Closing temporary Chat does not erase this separate project.
The worker's bounded operation journal records IDs/states/error codes, not raw rows
or prompts. Source files are referenced and preserved, not rewritten or deleted.

ML execution uses closed typed actions and one owned CPU worker with a hard Windows
job-memory cap, one-process limit, deadlines, bounded stdio and a minimal environment.
Request admission precedes scientific imports and follows job assignment. The brief
process-create/assignment gap remains; this trusted packaged worker is not an OS
sandbox for arbitrary Python. No generated code, pickle/joblib import, subprocess
tool or external upload action is exposed. Export writes a reproduction script;
export does not run that script automatically.

Readers refuse path links/junctions/remote paths, unsafe NumPy objects and HDF5
external/soft links or external/virtual storage. File/array/ZIP/selection limits
precede materialization where supported. Large-source fingerprints are explicitly
sampled and are not complete content proofs. A changed source prevents silent
completed registration. Saved `.skops` models require bounded size/expansion, recorded
hash and exact library versions plus a fixed allowlist of pinned serialization types.
The application never trusts all types discovered in a file; a matching hash alone
is not proof of trusted origin. Same-user malicious replacement remains outside the
protection boundary. Arbitrary external serialized models are not imported.

Ask Mu returns schema-constrained data through the existing local model. The current
selection is resolved by the application, not invented by model text. The UI captures
and displays a single typed action and limits, then requires explicit Run. Dataset
contents and model prose cannot grant shell or broader file authority. Plots are
bounded local PNG files beneath the project, rendered through native WPF, with no
remote URI or active web surface. ML evidence does not qualify voice-input WIP,
physical accessibility, unresolved desktop CPU budgets or a signed release.

Phase 4A adds manually requested local speech output. The adapter admits installed
Microsoft voice metadata and refuses mixed enabled engines before APIs that could
initialize another default voice. This is trusted OS registration data, not a defense
against same-user forgery. Each operation owns a same-executable child with private,
bounded stdio, a 30-second deadline and three-second cleanup bound. It sends literal
text, never SSML, resource URLs, lexicons or commands. No text enters argv/environment,
no audio file is created, and there is no automatic playback or microphone capture.
The UI cancels on context changes, sleep and close; a cleanup failure prevents reuse.
The short process-create/job-assignment gap remains a documented limitation. Native
engine events and bounded-memory synthesis do not certify physical audibility or
device removal. Temporary speech privacy probes cover app-controlled files, not OS
paging, dumps or arbitrary external engine behavior.

Local inference admits only the pinned official runtime/model, verifies every bundled
executable/DLL and holds read leases while running. The host validates loopback socket
ownership before authentication, ignores inherited provider/runtime configuration,
disables remote fetching/UI/slot APIs/logging and uses an internal per-launch secret.
This is not a user API key or a defense against malicious code running as the same
Windows user. Runtime output has no authority to execute tools. Native Markdown/code/
math rendering creates only fixed WPF document elements; HTML, images and links are
inert text, with no browser, remote asset requests or model-supplied XAML. Windows
job cleanup begins after assignment; the brief create-to-assign crash gap remains.
Chat starts in a separate in-memory Temporary repository. Saved chats requires an
explicit mode change and stores plaintext in the profile's conversations.db. Temporary
text never enters that repository or content-free logs; switching modes does not copy
it. OS paging/dumps and copies outside the app are not erased on close. Saved messages
remain after continuing a failed saved turn temporarily, as the UI discloses.
[Local AI boundaries](docs/LOCAL_AI.md) apply.

Implemented persistence includes nonsecret preferences, placement and explicitly saved
conversation history under the selected data directory. Deleting a conversation is a
logical database deletion, not forensic erasure of free pages, backups or OS copies.
Secrets are not supported settings fields; unknown fields are rejected.
Hot-journal recovery first validates a bounded SQLite copy named
`conversations.recovery.db` with its sidecars and `conversations.recovery.owner`.
Normal completion removes these files. A hard kill during validation can leave
plaintext saved-history copies until a later recovery cleanup; they are not a backup.
Unknown scratch files without a valid ownership marker are preserved and refused.
One validated `.bak` and one latest `.invalid` settings file are retained until user
removal; they are not forensic erasure mechanisms. Do not manually put keys in settings.
Logs use closed typed payloads, 10 MiB × 5 files per channel plus one corrupt-log
quarantine per family. On each actual log write (including startup), all known families
are swept by oldest-event timestamp: 14 days, or 90 days for future tool-audit metadata.
Whole old files can be discarded even if they contain newer entries. Corrupt-log
retention uses its filesystem modification time. No cleanup occurs while stopped.
Diagnostic channels default off; a damaged log is quarantined and recovery is shown.
Atomic file operations prevent ordinary partial writes, not malicious same-user tampering.
Current-user named pipes are not a defense against malware running as the same user.

Phase 2 adds native integration limited to the application's own window. Click-through
and Hide now require a successfully registered summon shortcut; requested tray
visibility alone does not prove that Windows displayed a usable icon. Show, the shortcut and
same-user activation restore interaction. Registration conflicts are visible, and
shortcuts/menu/icon resources are released on close. Placement JSON has a 4 KiB limit,
a closed versioned schema and finite normalized coordinates. Monitor identifiers are
matched against enumerated displays, never used as file paths. Future or oversized
placement files remain read-only; malformed bounded originals retain one quarantine.

Sign-in startup changes only `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`'s
`DesktopCompanion` value after an explicit changed preference is saved. It uses a
validated quoted executable path without arguments or shell execution. Test/custom
profiles cannot change startup through the settings UI. This development run tests
command validation and the missing-executable failure without changing the user's
startup registration. All thirteen native registry cases now pass against validated
GUID keys under `HKCU\Software\DesktopCompanion.Tests`. They share the production read/write/delete implementation and
remove only their own nonrecursive leaf. Actual sign-in behavior still needs a
disposable-user validation run. Expression previews never activate capture or tools.
Monitor cycling uses enumerated displays and the companion's owned HWND. Shortcut
registration success clears its own status message without clearing storage failures.
Hidden/minimized settings recovery now passes native checks. Closing disposes renderer
resources; stopping clears timers and controlled clocks so late callbacks cannot render.
Beginning shutdown now disables settings before disposing native adapters. A pending
Save may finish its atomic file commit, but its continuation cannot reapply the avatar,
register a shortcut, change click-through/startup or re-enable controls afterward.
Twenty-nine native lifecycle assertions cover controlled committed, cancelled and
I/O-failed saves. The test profiles do not change the user's startup value.
Initial placement now disables interaction until loading/restoration settles, without
re-enabling a stopped or closed window or changing a previously disabled state. Final
native regression passes 49 placement-initialization and 24 integration assertions,
including the confirmed-shortcut requirement.
Supported Computer Use input validation remains unavailable; programmatic native
checks do not establish physical click-through delivery or accessibility behavior.
The final independent development review passes at 8.7/10; the historical 8.6 failed
phase exit remains preserved. The
[validation sequence decision](docs/qa/PHASE_2_VALIDATION_SEQUENCE.md) keeps unavailable
physical/input/accessibility and actual sign-in checks mandatory before release in
Phases 11/12. Neither a development score nor programmatic tests certify those checks.
The observer experiment changes only optional diagnostic thread snapshots, preserves
fixed resource limits; all four original runs pass on their recorded binary. Final
readiness fixes pass regression. Final resource confirmation fails because the first
of two valid runs exceeds CPU limits, despite the second run and all memory intervals
passing. The aggregate result is preserved. The explicit post-result
[resource sequence decision](docs/qa/PHASE_2_RESOURCE_SEQUENCE.md) requires unchanged
CPU/memory qualification in Phase 12 before release. Performance remains 7/10 with
zero budget-criterion points; development approval is not security or release certification.

## Assets, actors and trust boundaries

Protect API credentials, user documents, conversation/memory data, microphone/screen
privacy, OS integrity and action consent. Trust user commands and installed application
code within their scopes. Treat model output, retrieved text, remote servers, provider
errors, clipboard contents, asset packs, document metadata and plugins as untrusted.
Boundaries: user → UI; remote provider → adapter; web renderer → host; application →
OS/files/credentials; worker → broker; storage → deserializer. Local malware with the
same user privileges and a compromised OS are outside complete protection guarantees.

Run `asInvoker` with `uiAccess=false` (present in baseline manifest), no admin service.
Windows Credential Manager generic credentials are the planned desktop secret store,
accessed through `CredReadW`/`CredWriteW`/`CredDeleteW` with checked Win32 errors and
SafeHandle/native allocation cleanup. `CredWriteW` writes into the current user's
credential set ([Microsoft API](https://learn.microsoft.com/en-us/windows/win32/api/wincred/nf-wincred-credwritew)).
Secrets never enter settings, SQLite, Git, model prompts, diagnostic exports or workers.
Memory zeroing is best effort; managed strings cannot promise perfect erasure. Credential
Manager does not protect against arbitrary malicious software running as that user.

## Threat/control/verification matrix

| Threat | Required control | Owning phase and adversarial test |
|---|---|---|
| Prompt injection asks for OS actions | Model only proposes; independent deny-by-default policy and tool registry | 7: malicious document cannot execute/register/approve a tool |
| Consent replay / argument change | Single-use operation-bound grant, identity/policy revalidation | 7: parallel replay, expired grant, renamed/replaced file, altered args all denied |
| Traversal / link escape / TOCTOU | Restricted roots, canonical final handle path, reparse/device/UNC/ADS policy | 7–8: junction swap, sibling-prefix path, symlink chain, ADS and device path cases |
| Arbitrary execution | No shell by default; allowlist structured tool parameters; no `eval` or loaded plugin DLLs | 7–9: shell metacharacters treated as invalid data, unsupported command rejected |
| Malicious document / asset | Bounded plain-text first; no macros; decode limits; sandbox before richer parsers | 8/10: oversized/malformed/archive bomb and path escape cases |
| XSS / renderer-to-host escalation | Sanitized Markdown, raw HTML off, CSP, bundled assets, origin checks, no generic host proxies | 3: scripts, event handlers, SVG, `javascript:`, iframe and forged web messages |
| Credential exfiltration through endpoint | Explicit endpoint configuration UI; HTTPS; loopback HTTP opt-in only; no credential forwarding on redirect | 3/11: redirect host swap, malformed/embedded-userinfo URI, secret redaction |
| SSRF / unintended network | Tool URLs and provider endpoints separate; disallow private/link-local network targets unless narrow local-provider consent; redirect/DNS revalidation | 3/7: loopback confusion, DNS rebinding, metadata IP and redirect chain |
| Memory poisoning / cross-project leak | User-issued approval, provenance, scope filtering before retrieval | 6: injection cannot approve, project A never returns B's memory |
| Temporary-mode persistence | In-memory repository; no durable tools/cache/summary/embedding/content logs | 3/6/11: sentinel search across DB/WAL/log/cache after crash and restart |
| Accidental mic/screen capture | Off by default, push-to-talk, visible activity, immediate mute/revocation | 4/11: cancel/device-loss/playback echo/permission-denied tests |
| Corrupt configuration/database | Strict version/size validation, atomic config replacement, migrations + backups, visible recovery | 1/3/6: truncated files, unknown schemas, disk-full and interrupted migration |
| Runaway requests/memory | Bounded channels, token/byte limits, cancellation, request budgets, deadlines | 3/4/7: endless stream, oversized frame, repeated tool loop, stalled network |
| Missing audit / partial side effects | Intent audit before consequential action; fail closed if unavailable; record unknown outcomes | 7/11: full disk and crash between intent/commit/outcome |
| Supply-chain / malicious update | Pinned reviewed packages, advisories, licensed assets, signed update/install artifacts | 10/12: invalid signature, downgrade/replay and interrupted update |

## Consent and retention defaults

No proactive desktop context, screen capture, continuous mic capture, startup registration
or telemetry upload by default. Long-term memory requires explicit enablement and each
stored fact requires user approval. File inspection uses user-selected grants; a file
mention in model text is not a grant. Consequential mutations always receive the exact
trusted application preview required by their policy. Optional shell, email or calendar
integrations remain absent until separately implemented/reviewed.

Local chat history persists only after choosing Saved chats, with a persistent mode
label; temporary mode is a separate repository, not a boolean applied after saving.
Remote processing requires provider disclosure and sends only selected context. Local
data is not encrypted by SQLite by default. Rely on OS user access controls and
recommend OS disk encryption for at-rest protection; application-level DB encryption
is an explicit later threat-model decision. Never advertise private mode as encryption.

Default application log design: content-free structured events, 10 MiB/file × 5 rotations,
14-day retention; diagnostics opt-in with redaction; audit metadata 90 days, configurable
with a disclosed deletion policy. Audit metadata avoids full file paths and tool payloads.
Backups are local, user-managed and subject to explicit retention. Clear history/memory
removes application records/indexes but cannot guarantee physical erasure from SSDs,
backups, OS dumps or provider retention. Crash reporting is local and opt-in to export.

## Reporting

The source repository is public on GitHub (checked 2026-10-19), but no dedicated
public security contact is documented yet. Report a suspected problem to
the project owner through an existing private channel, omitting live credentials and
personal documents. A public disclosure policy/contact is required before distribution.
Do not publish exploit logs or secrets in an issue. This document must evolve with each
new adapter and capability; a passing Phase 0 score is not a penetration-test result.
