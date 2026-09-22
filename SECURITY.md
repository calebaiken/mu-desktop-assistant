# Security and privacy design

Living threat model, not a security certification. Phase 1 adds explicit local preference
storage, content-free diagnostics and same-user activation IPC. There is still no API
client, tool execution, recording or conversation storage. Future controls below must
pass negative tests before their feature is enabled.

Implemented persistence is limited to nonsecret preferences and placement under the selected data
directory. Secrets are not supported settings fields; unknown fields are rejected.
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
and Hide require an available tray or registered shortcut; Show, the shortcut and
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
Supported Computer Use input validation remains unavailable; programmatic native
checks do not establish physical click-through delivery or accessibility behavior.
The independent follow-up scores 8.6/10 but fails phase exit; that numeric result is
not a security certification or authorization to advance past missing physical checks.

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

Local chat history will persist in normal mode and must be clearly disclosed on first
use; temporary mode is a separate repository, not a boolean applied after saving.
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

No public security contact or remote project exists yet. Report a suspected problem to
the project owner through an existing private channel, omitting live credentials and
personal documents. A public disclosure policy/contact is required before distribution.
Do not publish exploit logs or secrets in an issue. This document must evolve with each
new adapter and capability; a passing Phase 0 score is not a penetration-test result.
