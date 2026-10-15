# AGENTS.md

Operating contract for coding agents and contributors working on Mu. It records
what the build actually enforces, so you do not have to rediscover it by reading
a failed CI run. It does not restate the product design: that lives in
[ARCHITECTURE.md](ARCHITECTURE.md), [docs/CONTRACTS.md](docs/CONTRACTS.md) and
[docs/REQUIREMENTS.md](docs/REQUIREMENTS.md).

## The gate

`scripts/verify.ps1` is the only supported verification path. It runs, in order:

1. `dotnet restore --locked-mode`
2. `dotnet build -c Release`
3. `tests/Companion.ArchitectureChecks`
4. `dotnet test tests/Companion.Tests`
5. a headless WPF smoke launch of `Companion.Desktop.exe` (must exit 0 within 15 s)

`scripts/verify-negative.ps1` runs afterwards as a separate step and expects the
binaries from step 2 to exist, so run `verify.ps1` first.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-negative.ps1
```

Requires .NET SDK **10.0.401** (`global.json` pins the version exactly and sets
`rollForward: latestPatch`, `allowPrerelease: false`). Anything else fails the
SDK-pin check, not just the build.

## Architecture checks are exact-match, not advisory

`tests/Companion.ArchitectureChecks/Program.cs` compares values for equality, so
a reasonable-looking addition usually breaks CI rather than being tolerated.

- **Project references** must be exactly these, no more:
  `Core` → none · `Application` → Core · `Infrastructure` → Application, Core ·
  `Desktop` → Application, Core, Infrastructure. Never invert a layer.
- **Package references** are pinned to an exact reviewed set. `Infrastructure` may
  only carry `Microsoft.Data.Sqlite` `10.0.12` and `System.Speech` `10.0.12`;
  `Desktop` may only carry `Markdig` `1.3.2` and `WpfMath` `2.1.0`; `Core` and
  `Application` must carry **no** packages at all. Adding one requires updating
  this check and `docs/DEPENDENCIES.md` in the same change.
- **`Directory.Build.props`** must keep `TreatWarningsAsErrors`,
  `EnableNETAnalyzers`, `Deterministic` and `RestorePackagesWithLockFile` true,
  `Nullable` enabled and `AnalysisLevel` latest. Do not soften these to get a
  build through.
- **`src/Companion.Desktop/app.manifest`** must stay `asInvoker` with
  `uiAccess="false"` and `dpiAwareness` `PerMonitorV2`.
- **`NuGet.Config`** must keep exactly two sources with a `<clear />`, the first
  being `https://api.nuget.org/v3/index.json`. Adding a feed breaks the check.
- **`.gitignore`** must keep these nine literal lines: `.env`, `.env.*`, `*.db`,
  `*.db-*`, `*.log`, `*.dmp`, `*.pfx`, `*.key`, `secrets/`.

## Locked restore

Restore runs with `--locked-mode`, and `packages.lock.json` is committed for all
six projects. After any dependency change you must regenerate the lock files or
restore fails:

```powershell
dotnet restore Companion.slnx --force-evaluate
```

Commit the updated `packages.lock.json` alongside the `.csproj`.

## Optional local runtimes and the 32 CI skips

The ML Lab, LAYA and neural-speech workers need Python runtimes that live under
`artifacts/` and are **gitignored**. They are provisioned locally by
`scripts/setup-ml-lab.ps1`, `scripts/setup-laya.ps1` and
`scripts/setup-neural-speech.ps1`.

Consequently the two environments differ, and this is expected, not a bug:

| | Failed | Passed | Skipped | Total |
|---|---|---|---|---|
| GitHub Actions (no runtime provisioned) | 0 | 366 | 32 | 398 |
| Local, after the setup scripts | 0 | 398 | 0 | 398 |

Tests that need the reviewed runtime resolve it from
`artifacts/ml-runtime/Scripts/python.exe`. **When that file is absent such a test
must call `Assert.Inconclusive(...)`, never `Assert.IsTrue(File.Exists(...))` or
`Assert.Fail`.** A missing optional runtime is a reported skip, never a failure
and never a silent pass. Three helpers already follow this rule —
`MlWorkerHostTests`, `IndependentLayaTests.MakeService` and
`IndependentNeuralSpeechTests.Python`. Treat them as the pattern.

Never "fix" the CI skips by asserting the runtime exists. That produced 23 false
failures on 2026-10-15 before it was corrected.

## What must never be committed

`artifacts/`, `.vs/`, `.dotnet-home/` and `.packages/` are ignored and total
roughly 4.6 GB locally. They hold compiled binaries, WPF build output and
model weights (Kokoro ONNX ~325 MB). Kokoro and other weights are downloaded at
run time into `%LOCALAPPDATA%` and SHA-256 verified, never vendored.

This is a local-first application. Do not add cloud calls, telemetry, or any
code that reads an API key or environment credential. Tests assert that worker
environments are offline and stripped of credentials; several use sentinel
values such as `"HF_TOKEN": "not-a-token"` inside `patch.dict` to prove workers
discard them. Those sentinels are intentional.

## Licensing

The project is MIT (`LICENSE`). Dependency licences are inventoried in
`docs/DEPENDENCIES.md` with verbatim texts under `workers/*/licenses/`.

One boundary matters: `workers/neural_speech/licenses/` contains **GPL-3.0**
texts (`espeak-ng`, `phonemizer`). Those packages are installed into an isolated
venv and never vendored or redistributed, which keeps the project MIT. Do not
add a vendored binary or a bundled installer that embeds them — that would make
the distribution GPL-3.0.

## Reporting honestly

The repository consistently records unfinished work as unfinished. When you change
behaviour:

- Update `DEVELOPMENT_STATUS.md`, `CHANGELOG.md` and the relevant doc, with a date.
- Do not record a check as passed unless it actually ran in that configuration.
  Note which environment a result came from; a local run and a CI run are not
  interchangeable (see the table above).
- A missing check is not a passing check. If a boundary is unverified, say so.
- Receipt rows in `TESTING.md` reference named artifacts from real past runs.
  Leave historical rows alone and add a new one; never edit a receipt to match a
  later run.