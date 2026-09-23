# Local pretrained chat execution evidence

Root executed these checks on Windows 11 x64 with .NET SDK 10.0.401. The independent
reviewer owns [the review and verdict](PHASE_3_LOCAL_INCREMENT_REVIEW.md). This file
records execution; it does not assign a score or complete Phase 3.

## Dependencies and setup

`scripts/setup-local-ai.ps1` downloaded the official pinned Qwen3-0.6B Q8_0 model and
llama.cpp Windows CPU archive. Both lengths and SHA256 hashes matched their publisher
metadata; extracted executable/DLL hashes are embedded in the application catalog.
The runtime reports `0.4.1-dev (build 10964, commit b29c606e2)`, the build referenced
by stable release v0.4.1. Source URLs, revisions and licenses are in
[LOCAL_AI.md](../LOCAL_AI.md). A subsequent setup run validated existing files and
completed successfully. No provider credentials or pretrained Python loaders are used.

The earlier from-scratch prototype was interrupted by the user's pretrained choice.
Its unexecuted draft was preserved in ignored `artifacts/paused-from-scratch-20260922`.
The unused isolated training environment was removed. No training or learning claim
was produced by that work.

## Build and scripted regression

`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1`:

- Strict Release build: **0 warnings, 0 errors**.
- **224/224 tests**, **0 failed, 0 skipped**; **11/11 architecture checks**.
- Composed WPF settings/logging/instance/XAML/startup/shutdown smoke passed.
- Twelve of the new test methods are authored by the independent reviewer.

Retained TRX files in `artifacts/qa/local-increment-verification`:
`initial-224.trx`, `final-224.trx`, and
`final-224-concurrent-disposal.trx`. The last includes the reviewer's refined test
using genuinely concurrent disposal callers and the final setup-validation changes.
Two early compile attempts failed before execution: target-typed `new` in a `params`
fixture and an avatar integration method not yet written. Both were corrected before
the passing strict runs. Neither is a model inference result.

## Native UI/lifetime checks

`scripts/verify-ui.ps1` exited 0 with **19/19 process/control checks** and two complete
native runs. Evidence: `artifacts/qa/desktop-ui-20260922-195825-69e2c07d/profile`.
Each final report has distinct named passing assertions and no failures/skips:

- 43 appearance/state/placement/animation.
- 24 recovery/tray/shortcut.
- 29 settings Save/shutdown.
- 49 placement initialization.
- **54 chat lifecycle/history/layout**.

Chat fixtures use real WPF windows and controlled model continuations. They verify
late completion after close, stop/single flight, successful vs incomplete history,
plain text, bounded multibyte conversations and minimum-size/edge placement. They do
not simulate or prove actual user input, screen-reader operation or physical DPI
behavior. Those release qualifications remain outstanding.

## Real model

The first predeclared run of `scripts/verify-local-model.ps1` exited **0**, no timeout,
with integration **PASS**. Evidence directory:
`artifacts/qa/local-model-20260922-195744-9143334a`.
Its plan records exact binary/model/protocol hashes before launch. Protocol SHA256:
`96C29636EA67F985DC38429C9494C5AA6617C368D78242545DAE59C1B4BE1B45`.

All public fixture outputs are retained. A returned exactly `LOCAL_READY`; B1 and B2
returned `Maple`; C returned three distinct short desk-organization tips. Every full
reply emitted text and exactly one Stop completion. Cancellation occurred after the
first nonempty delta (`Here`), followed by successful fresh Case A reuse. The owned
worker exited on dispose. A second worker actually spawned, was cancelled while
startup remained pending, and exited. Content observations are separate from the
transport result; these few prompts do not establish broad assistant capability.

Observed readiness: **4.8037317 seconds**. Entire run: **14.9589429 seconds**.
Per-delta sampled maximum worker private bytes: **360,083,456**. This is not a
lifetime peak or total physical-memory figure; mapped model pages are additional.
No token throughput claim is made. CPU inference ran with the documented offline
flag and local socket ownership/authentication checks; physical network disconnection
and OS network-trace qualification were not performed.

The final source later changed only desktop optional-configuration recovery/CLI
validation plus reviewer tests and documentation. The inference Infrastructure DLL
hash remains `47B7DA5EBE469EF00EAEEEA12D9C0854343B7386136086AA05C3A661F7DD8BE5`,
identical to the live plan; the model and adapter were not changed or rerun for a
preferred output. Final Desktop DLL SHA256 is
`28761CDFB4E06720A58B323956FDF1FCC36DF49C6F525E8E1F26E85943276F71`.

## New command-line controls

`scripts/verify-local-model-options.ps1` exited 0: **8/8** malformed/missing/duplicate/
conflicting/network-path options were refused with exit 2. Evidence:
`artifacts/qa/local-options-f7d63cfb39d645b6b43523c1ae7f92bf/results.json`.
The final strict verification above covers this desktop build. The optional model
constructor error containment is source-reviewed; no new reparse-path UI action is
claimed as physically exercised.

No companion or model worker was left running by these checks. Phase 2's failed CPU
qualification remains unchanged. Persistent history, rich rendering, exact token
preflight, broader model evaluation and release validation remain outstanding.
