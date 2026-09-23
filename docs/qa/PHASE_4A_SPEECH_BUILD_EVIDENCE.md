# Phase 4A speech output evidence

Source baseline: **b11524d**, refs/checkpoints/phase-3, independently approved at
9.5/10. Phase 4A's separate fifty-criterion rubric was frozen before implementation
in PHASE_4A_SPEECH_REVIEW.md. This is explicit local speech output only; full Phase 4
still requires capture, VAD/STT, device selection, activation and feedback qualification.

## Implementation and scope

Read aloud applies only to the selected completed reply. It is manual, with session
voice/rate controls, a visible Stop voice action and plain-text fallback. New sends,
conversation/branch changes, sleep and closing cancel the operation. Engine start/
stop events own a Speaking expression lease; this is not a measured speaker clock.
Clipping at 2,000 UTF-16 units preserves a surrogate pair boundary and is disclosed;
the displayed/stored reply remains whole. Native validation rejects malformed Unicode.

System.Speech10.0.12 runs in a fresh same-executable child for each enumeration,
playback or inaudible diagnostic operation. Commands use bounded length-prefixed
private stdio frames; content is absent from argv/environment/logs. No SSML, remote
lexicon, microphone, cloud API or audio file is used. The parent admits one operation
without a queue, imposes a 30-second deadline and enforces owned worker cleanup with
a three-second bound and kill-on-close job. Cleanup failure prevents further use of
that provider instance. The create-to-job-assignment gap remains explicitly limited
by worker startup/EOF behavior, not falsely described as suspended creation.

Memory-only diagnostics use a fixed 4 MiB destination, validate actual RIFF chunks
inside the worker and return only metadata/hash. Parent arithmetic/metadata checks
are not independent access to raw native PCM. Default-output playback has text/time
bounds; it does not claim that same precomputed PCM allocation cap.

## Findings and retained execution

- First strict Release verification passed **301/301 tests**, zero skipped/failed,
  **11/11 architecture**, composed WPF smoke and zero warnings/errors. Retained TRX:
  artifacts/qa/phase4a-verification/phase4a-before-voice-selection-fix.trx, SHA256
  6AFF085C84A1A23609741B81CD06DE7CF8ECA1BE9AF62E802AFD53AD879483FE.
  This predates the voice-selection hardening below; no native speech run had occurred.
- Independent protocol/admission tests add 21 separately authored methods. Eight
  author tests check capped-buffer/RIFF arithmetic and startup environment. Native
  WPF fake-provider coverage and real engine diagnostics are reported separately.
- Review identified that a cleanup failure must poison the provider rather than
  allowing another worker; the author fixed this before the first passing build.
- Review of the shipped assembly found that SelectVoice can initialize a default
  engine first and an InstalledVoice.Enabled setter can itself initialize CurrentVoice.
  The bounded slice therefore refuses playback/probes when any enabled installed
  engine falls outside its admitted Microsoft token rules, before invoking those
  setters or selection. This uses metadata trust, not protection against forged
  same-user registrations. It does not modify the Windows voice registry.
- The new dependency's first advisory query returned exit1 with no diagnostics in
  the restricted environment. The approved normal-user query completed successfully
  across all six projects with no known vulnerable packages; output is retained at
  artifacts/qa/phase4a-package-advisories.txt. This is a configured-feed query, not a
  source/security audit. Exact provenance/licenses/lock checks are in DEPENDENCIES.md.

## Native protocol and limits

After the voice-selection fix, strict Release verification passed **305/305 tests**,
**11/11 architecture checks**, composed WPF smoke and zero warnings/errors. Four
additional pure voice-policy tests cover the Microsoft-only configuration boundary.
Retained TRX: artifacts/qa/phase4a-verification/phase4a-final-305.trx, SHA256
9CE09C3368E08916F0AC6AC5473A58B51EF7E986C07CDCE174ACF2E8CA66FD77.
A subsequent Desktop-only build also passed after adding discoverable mixed-engine
limitations to voice-control help and the unavailable-output message.

The first inaudible diagnostic passed at artifacts/qa/speech-20260922-223108-36a69151.
Five installed Microsoft voices were enumerated; Microsoft David
(MSTTS_V110_enUS_DavidM, en-US) completed both public utterances. S01 contained
221,334 PCM bytes at 22,050 Hz, mono 16-bit (5.0189115646 seconds of audio);
S02 contained 116,604 bytes (2.6440816327 seconds). Both were nonzero with fmt18/data
chunks. Those audio durations are distinct from synthesis latency.

The first expanded native UI attempt, desktop-ui-20260922-223218-e3d73fd1, **failed
its 60-second aggregate deadline**. The retained stage log reaches reply-preview
completion at 52.516 seconds and has no completed speech report. This does not
establish a speech assertion pass or a fixture deadlock. Before the next attempt,
the supervisor budget was changed to 120 seconds for the added 84-case suite;
individual fixture waits and production speech limits remain unchanged.

The revised attempt, **desktop-ui-20260922-224213-17740e63**, passed **38/38
supervisor checks** and both independently retained native runs: compact avatar51,
appearance43, desktop integration24, settings lifecycle29, placement49, chat54,
history50, rich renderer46, reply preview34 and **speech84**. No reported failures
or skips occurred. The speech fixture took19.649s and28.045s respectively; the
first run's full stages ended at62.385s. Thus the old7.5-second remaining allowance
was insufficient, with no demonstrated fixture deadlock. Production code did not
change between attempts; only diagnostic timing checkpoints were added. Final
binary hashes are retained in phase4a-verification/final-native-build.json. Its
Desktop hash differs from the playback run because of that instrumentation;
Application, Infrastructure, Core and System.Speech hashes are unchanged.

Actual Computer Use input opened Chat with Ctrl+Shift+C, expanded Voice options
without starting speech, and inspected both installed-voice and speed dropdowns.
It found that WPF's default item automation name exposed the backing record's fields.
A narrow XAML ItemContainerStyle now binds accessible item names to the displayed
voice Name and rate Label. A strict Desktop rebuild passed with zero warnings/errors;
fresh real input and refreshed automation trees show all five friendly voice names
and all eleven speed labels. Trees are retained in phase4a-verification/
actual-voice-item-names.txt and actual-rate-item-names.txt, with the executed hashes
in accessible-voice-items-build.json. Only Desktop changed; the five other binary
hashes match the native run. The 305-test/full-native results predate this label-only
edit and are not represented as rerun. A transient Computer Use geometry error was
recovered by re-observing the screenshot before clicking; no unknown outcome was
silently retried. This targeted check is not a screen-reader certification or a
physical-device/audio test, and the tool's stale focus text is not focus evidence.

The abrupt temporary-speech persistence probe passed at
artifacts/qa/private-speech-20260922-223824-cd48804a. Its readiness marker confirms
temporary mode, active fake output and one durable decoy conversation. All four
profile files were scanned in three encodings, with no sentinel match. The
independent reviewer verified the file hashes and repeated all twelve comparisons.
The exact sentinel's normal-completion scan remains unexecuted.

The declared default-output run passed at artifacts/qa/speech-20260922-223856-66dd24ca:

| Case | Observed result |
|---|---|
| S01 Stop | One Speaking/Stopped pair; cancelled; acknowledgment 0.0038051 seconds; held worker exited |
| S02 reuse | Speaking then Stopped; 3.7085101 seconds total; held worker exited |
| S04 deadline | Typed Timeout at 30.0156752 seconds for 1,980 characters at rate -5; Speaking then Stopped; held worker exited |

The supervisor exited zero without timeout; all binary identities and observations
are retained in its plan/report. Physical hearing remains **UNOBSERVED**. The native
run exercises provider/engine ownership, not actual UI button presses or App sleep.

The default speech supervisor is inaudible. `verify-speech.ps1 -Playback` explicitly
adds public default-output S01 cancel, S02 reuse and frozen S04 deadline attempts.
S01 requires one Speaking/Stopped pair and cancellation acknowledgment within3s;
S02 requires that exact event order; held process handles verify each child exited.
S04 is the public word `Maple ` repeated330 times, rate-5, expecting typed Timeout
within30–33s and confirmed child exit. Successful engine events do not establish
that a human heard the audio. All attempts are retained without selected retries.

The private-speech probe uses a controlled fake provider against the real ChatWindow
and a real saved SQLite decoy, then terminates its held process during a temporary
operation. It scans profile files in UTF-8/UTF-16LE/UTF-16BE. This is whole-app
persistence evidence; it does not inspect third-party engine internals or OS paging.
No raw audio or user speech is retained by these fixtures.

The frozen60-second idle and ten-cycle/60-second post-idle protocol are not counted
as executed by shorter checks. Physical hearing, device unplug/default-device changes,
screen-reader use and complete microphone/voice-loop behavior require their own evidence.
Phase 2 CPU qualification and other mandatory pre-release gates remain unchanged.

## Independent verdict and checkpoint

The separate fifty-criterion review scores **90/100 (9.0/10)** and approves this
bounded manual output slice, with no critical unresolved defect. Its five ranked
improvements preserve missing physical/device, App Sleep, normal-sentinel and long
idle/repeat observations. Full Phase 4 remains incomplete. The local source checkpoint
is refs/checkpoints/phase-4a-speech-output; no remote publication or release is claimed.
