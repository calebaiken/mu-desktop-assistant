# Mu appearance and local task advice

Mu's particle mascot is inspired by its name: a muon. The face, detector-style
halo and trail are playful illustrations. The desktop background stays
transparent and action buttons appear on hover or keyboard focus.

Open **Settings** from the character or system tray. Choose **Muon** or
**Classic**, a **Color / theme**, a friendly or focused face, and a halo/trail,
trail alone or no effects. **Galaxy** adds a dark nebula gradient, light cyan
details and static stars. Size, opacity and reduced motion remain available.
Preview your choices, then **Save** to apply them. Closing Settings discards
unsaved appearance changes. High contrast uses Windows colors and hides
decorative stars; effects do not bypass reduced motion.

Existing settings migrate in memory to schema 3 and retain the Classic
character, previous name, size, opacity and shortcut until you explicitly save.
New profiles start with the Muon character and the name Mu. No source dataset,
conversation or voice preference is changed by choosing an appearance.

## Windows icon and shortcut

`assets/brand/Mu.svg`, `Mu.png` and `Mu.ico` are the matching transparent particle
icon. The executable, native windows and system tray use it. The ICO includes
seven Windows sizes. Changing avatar themes does not change the application
icon.

Build the current desktop candidate into a stable local output folder:

```powershell
dotnet build src/Companion.Desktop/Companion.Desktop.csproj -c Release --no-restore -m:1 -nodeReuse:false -p:UseSharedCompilation=false -o artifacts/Mu
pwsh -NoProfile -File scripts/create-mu-shortcut.ps1
```

The shortcut script creates **Mu.lnk** in your Windows desktop folder. It
preserves an existing shortcut instead of replacing it. It targets the verified
executable directly, with the repository ML root and an existing local chat
model directory. There is no shell wrapper. You can also pass an absolute
`-ApplicationPath` or `-ShortcutDirectory`.

Launch Mu, then right-click its taskbar icon and choose **Pin to taskbar**.
Windows owns taskbar pinning; the script does not modify your pins. Keep the
repository/output folder in place while using this development shortcut.
Windows sign-in startup still uses the application's current location and
does not inherit this shortcut's model arguments.

## LAYA in Machine Learning Lab

[LAYA](https://github.com/NandhaKishorM/laya) is an independent open-weight typed
decision engine with a Jev-compatible interface. It is not the hosted Jev
service and does not generate conversational prose. Mu continues to use its
existing local chat model for conversation and typed experiment proposals.

In **Machine Learning Lab → Ask Mu**, enter a task and select **Analyze task
with LAYA**. It suggests regression, classification, clustering, dimensionality
reduction, anomaly detection or an unclear/other task. Its category scores,
checkpoint identity, elapsed time and caveats appear under **Local LAYA
advice**. Advice does not train a model or run another action.

For the unchanged request, a nontruncated supported category with a top score
of at least 0.65 may inform **Propose a plan** as a fallible hint. This threshold
is an admission rule, not calibrated confidence or measured accuracy. Explicit
user instructions take precedence. You still inspect the exact proposed action
and choose **Run reviewed plan**. Ambiguous, other and truncated advice is not
used as a planning hint. No model decision establishes a scientific conclusion.

LAYA is optional. `scripts/setup-laya.ps1` installs a separate pinned CPU
runtime and one hash-verified English checkpoint under `artifacts/`. Only this
explicit setup downloads dependencies and weights. Inference uses local files,
offline library settings and a Python network audit guard; private requests and
datasets are not uploaded. No API key or cloud account is needed.

Mu bounds each LAYA request to 2,000 characters, 512 tokens, two CPU threads,
one owned worker, a 3 GiB committed-memory ceiling and a two-minute deadline.
Stop planning, view closure and application shutdown cancel work. This is
resource and lifetime containment, not a sandbox for untrusted Python code.
Missing or damaged optional assets leave the existing chat and ML controls
available. Rerun setup to repair the optional installation; files failing
integrity checks are preserved rather than silently replaced.

Before loading native ML packages, LAYA requires at least 1.5 GiB available physical
RAM, 3 GiB system commit headroom and 2.5 GiB available process commit within its
job. It uses float16 parameters and CPU float16 autocast; scores may differ from
float32 execution. The resource checks refuse a request before allocating the model
when Windows is under pressure. Closing applications can help; their memory is not
reclaimed by Mu automatically.

On 2026-09-30 the pinned runtime and all checkpoint files were installed and verified.
The actual production-host/native-view diagnostic returned `memory_limit` in 1.65
seconds, with no model output. **Full LAYA inference is not yet qualified on this PC.**
The 40 controlled native UI assertions and tiny seeded precision check test different
boundaries. With adequate headroom, `scripts/verify-laya.ps1` must pass all 16 real
checks before claiming that the downloaded model works in Mu.

The checkpoint is intended for English and category scores are not calibrated
for Mu's scientific workloads. General routing accuracy, multilingual support,
fine-tuning and other LAYA decision types are separate future work. See the
[independent review](qa/MU_IDENTITY_LAYA_REVIEW.md) for the tested scope.
