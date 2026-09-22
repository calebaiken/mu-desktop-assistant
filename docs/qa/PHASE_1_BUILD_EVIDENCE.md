# Phase 1 implementation evidence

Date: 2026-09-21. This record separates implementation-agent observations from the
independent review at `PHASE_1_REVIEW.md`. Phase 0 source is preserved at
`refs/checkpoints/phase-0`; no pre-existing source was discarded.

## Executed verification and failure history

- Initial parallel package restore failed with a filesystem collision. Serialized
  restore completed. The maintained verification script uses `--disable-parallel`.
- A locked restore failed NU1900 when the sandbox could not reach NuGet vulnerability
  data. Approved network execution succeeded; vulnerability auditing stayed enabled.
- The first full restricted-token run built with zero warnings/errors and passed
  11 architecture checks, but 10 of 38 MSTest cases failed with Windows access denial
  on atomic File.Replace or current-user named pipes. No test or control was removed.
- Identical full Release verification outside the restricted token passed: exit 0,
  zero warnings/errors, 38/38 MSTest cases, 0 skipped, 11/11 architecture checks and
  composed settings/logging/instance/XAML/startup/shutdown smoke. This does not require
  application UAC elevation; the manifest remains asInvoker/uiAccess=false.
- Independent QA separately ran the Release test suite and full Debug verification and both nine-case
  negative/control and real-HWND process suites. See its report for exact commands.

Actual command: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1`.
TRX output is retained under ignored `artifacts/test-results/`. Simulated disk-full
testing uses a narrow injected atomic writer; it is not physical disk exhaustion.

## Root-agent interactive Windows evidence

Used the installed computer-use skill and supported `@oai/sky` APIs. No direct
PowerShell UIAutomation or custom computer-use protocol was used. Process launch and
read-only inspection used ordinary shell APIs, not terminal UI automation.

Isolated data profile:
`artifacts/qa/interactive-1408d25f186b436597dc9a68a71d420e`.

1. Launched Release executable with `--data-dir` pointing to that profile (PID 208868).
   The companion and settings windows were discovered from returned window objects.
2. Observed the static companion, unclipped Settings/Exit buttons, and settings with
   name field, checkboxes, Save, Close and status text. Actual screenshots were viewed.
   Accessible names/IDs exposed the real controls. This is not full screen-reader or
   keyboard-only conformance testing.
3. Set name to `Pip QA` through the observed editor, invoked Save, observed `Saved.`,
   then closed settings. The companion title and visible label both changed to `Pip QA`.
4. Launched a second process against the same profile. It exited 0; a fresh window
   inventory showed one existing companion. No duplicate companion was created.
5. Invoked the first companion's Exit button. PID 208868 disappeared and its log
   contained a Stopping event. The immediate window-list snapshot briefly still showed
   the closing window; process termination was checked separately afterward.
6. Relaunched against the same profile (PID 209288). The real window title/label and
   reopened editor both displayed the saved name. A read-only check confirmed the test
   name was absent from the application event log.
7. Concurrent user input occurred during cleanup/restoration of the pre-check name.
   The refreshed title became `Mu`, but some input calls returned unavailable geometry
   and the helper explicitly detected user input. Automation stopped to avoid contention.
   The final second Exit was **not** completed/claimed; the user-interacted test window
   was left alone. The first complete Exit/restart sequence above is the tested evidence.

Additional save events existed from concurrent interaction and are not attributed to
agent tests. No microphone, credential, security/privacy UI or OS setting was changed.
The selected profile is test data under ignored artifacts, not the normal app profile.

## Corrections made after independent review

Wrong-type schema recovery, cached backup validity, save state after failed load,
unbounded corruption copying, postcommit-cancellation reporting, root/UNC normalization,
oldest-event retention, disabled-family sweeping and malformed-log startup recovery
were corrected. Independent tests retain regression cases. Log integrity claims are
limited to a parsed first record and a newline-terminated tail; arbitrary interior JSON
corruption is not scanned. Both protection limits and future work remain documented.

No actual provider, voice, conversation memory, tool, science, animation pack, installer,
remote CI execution or measured performance claim follows from this phase.
