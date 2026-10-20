# Dependency inventory

2026-10-19 UI v3: no new direct packages, runtime dependencies, bundled fonts,
images or logos. The standalone dev-only `scripts/ResearchProjects.UiChecks`
project references Desktop and reuses its reviewed graph in a committed
[lockfile](../scripts/ResearchProjects.UiChecks/packages.lock.json). It is outside
the unchanged production solution and uses platform WPF/GDI for fixture-only UI
checks. [Brand-source provenance](UI_DESIGN_V3.md); the kit is not redistributed.

Recorded 2026-09-22 from the restored `.packages/<id>/<version>/*.nuspec` files,
package notices and committed lock files. Repository URLs, commit IDs and license
expressions below are package-supplied metadata; an absent commit is explicitly
identified. This inventory does not substitute for a source or license compliance audit.

## Resolution and verification

The reviewed feed is NuGet.org, configured in [NuGet.config](../NuGet.config), with
the workspace-local `.packages` cache. `scripts/verify.ps1` restores in locked mode.
Exact resolved versions and NuGet content hashes are recorded in the
[Desktop lock](../src/Companion.Desktop/packages.lock.json),
[Infrastructure lock](../src/Companion.Infrastructure/packages.lock.json) and
[test lock](../tests/Companion.Tests/packages.lock.json). Core and Application have
no package dependencies. Desktop owns rendering packages; Infrastructure owns SQLite
and the Windows speech adapter.

For all **22 distinct restored package IDs** in the runtime/test graph, this inventory
check matched the archive SHA512 against its cache `.nupkg.sha512`, and independently
matched `.nupkg.metadata` source/contentHash against the NuGet.org source and lock file.
NuGet's content hash excludes signing material, so it must not be compared directly
with the raw signed ZIP hash. See [NuGet's metadata format](https://github.com/NuGet/Home/wiki/Nupkg-Metadata-File).
The original Phase 3 check covered 21 IDs; the same two checks were also completed
for the added System.Speech package. These local consistency checks are not a fresh
signature/certificate-chain audit.

The command `dotnet list Companion.slnx package --vulnerable --include-transitive`
completed after the Phase 4A package addition across all six projects and reported
no known vulnerable package. The approved retry is retained at
`artifacts/qa/phase4a-package-advisories.txt`; the initial restricted invocation exited
1 without output and is not a successful check. This records a query of the configured
NuGet advisory feed on that date. It does not establish absence of unknown vulnerabilities, assess
exploitability, or scan the separately downloaded native inference runtime/model.
Normal restore auditing remains enabled; unavailable advisory data must be reported,
not suppressed. No redundant advisory rerun was performed for this document.

## Runtime NuGet packages

| Package | Locked version | Owning dependency | Declared license | Package-supplied provenance |
|---|---|---|---|---|
| Markdig | 1.3.2 | Direct, Desktop Markdown parser | BSD-2-Clause | [xoofx/markdig](https://github.com/xoofx/markdig), commit `fc705234fa211d179ee1d5e7656b51ab99f70ca9` |
| WpfMath | 2.1.0 | Direct, Desktop local mathematical geometry | MIT AND OFL-1.1 | [ForNeVeR/xaml-math](https://github.com/ForNeVeR/xaml-math), commit `d6f576345998777dcf7ad112da1add9bd7ad9674` |
| XamlMath.Shared | 2.1.0 | Transitive through WpfMath | MIT | [ForNeVeR/xaml-math](https://github.com/ForNeVeR/xaml-math); nuspec does not declare a commit |
| Microsoft.Data.Sqlite | 10.0.12 | Direct, Infrastructure conversation persistence | MIT | [dotnet/dotnet](https://github.com/dotnet/dotnet), commit `95017c711e6afc1085133d440e42b4bd78155701` |
| Microsoft.Data.Sqlite.Core | 10.0.12 | Transitive through Microsoft.Data.Sqlite | MIT | [dotnet/dotnet](https://github.com/dotnet/dotnet), commit `95017c711e6afc1085133d440e42b4bd78155701` |
| System.Speech | 10.0.12 | Direct, Infrastructure installed Windows voice adapter | MIT | [dotnet/dotnet](https://github.com/dotnet/dotnet), commit `95017c711e6afc1085133d440e42b4bd78155701` |
| SQLitePCLRaw.bundle_e_sqlite3 | 2.1.12 | Transitive SQLite bundle initialization | Apache-2.0 | [ericsink/SQLitePCL.raw](https://github.com/ericsink/SQLitePCL.raw); nuspec does not declare a commit |
| SQLitePCLRaw.core | 2.1.12 | Transitive SQLite interop abstraction | Apache-2.0 | [ericsink/SQLitePCL.raw](https://github.com/ericsink/SQLitePCL.raw); nuspec does not declare a commit |
| SQLitePCLRaw.lib.e_sqlite3 | 2.1.12 | Transitive platform-specific native SQLite binaries | Apache-2.0 | [ericsink/SQLitePCL.raw](https://github.com/ericsink/SQLitePCL.raw); nuspec does not declare a commit |
| SQLitePCLRaw.provider.e_sqlite3 | 2.1.12 | Transitive SQLite native provider | Apache-2.0 | [ericsink/SQLitePCL.raw](https://github.com/ericsink/SQLitePCL.raw); nuspec does not declare a commit |

The SQLite native package version above is the package identity, not a claim about
the embedded SQLite engine's version. The restored package contains several platform
assets; the current application qualification target is Windows x64. The dependency
graph does not imply other platforms have been built or tested.

System.Speech 10.0.12's restored archive is **1,334,368 bytes**. Its `net10.0`
dependency group has no additional NuGet dependencies; the package therefore adds
one runtime package ID. This is archive size, not an installed application or voice
model footprint. The WindowsSpeechOutput adapter uses this package for Windows speech synthesis only. The package
also contains recognition APIs, but adding the package does not enable microphone
capture or recognition in this application. Installed Windows voice engines remain
separate platform dependencies; eligibility checks do not prove synthesis succeeds.

WpfMath's declared `MIT AND OFL-1.1` expression is not the whole font-notice inventory.
Its restored `FONT-LICENSES.md` also lists Computer Modern font files under the Knuth
License, plus `jlm_msam10.ttf` under SIL OFL 1.1 with reserved font names. Retain that
file, the package's license/copyright notices and applicable font notices in the
eventual release distribution. The application does not modify those fonts. The
restored README explains the upstream historical licensing and links the relevant
notices; the exact shipping artifact still needs the Phase 12 redistribution review.

Rendering uses Markdig's AST and application-owned WPF nodes, not its HTML output.
WpfMath is used only for bounded, allowlisted local geometry. Neither package is a
browser, tool executor, network renderer or mathematical correctness checker.

## Test-only packages

The three direct test references use `PrivateAssets="all"`. Runtime SQLite and speech
packages also appear in the test graph through the Infrastructure project reference; they are
already listed above. The following twelve packages belong to the test toolchain.
Their presence does not introduce application telemetry; the verification script sets
both .NET CLI and testing-platform telemetry opt-out variables.

| Package | Locked version | Relation | Declared license | Package-supplied source |
|---|---|---|---|---|
| Microsoft.NET.Test.Sdk | 18.10.0 | Direct | MIT | VSTest source below |
| MSTest.TestAdapter | 4.4.1 | Direct | MIT | TestFX source below |
| MSTest.TestFramework | 4.4.1 | Direct | MIT | TestFX source below |
| Microsoft.ApplicationInsights | 2.23.0 | Transitive | MIT | ApplicationInsights source below |
| Microsoft.CodeCoverage | 18.10.0 | Transitive | MIT | VSTest source below |
| Microsoft.Testing.Extensions.Telemetry | 2.4.1 | Transitive | MIT | TestFX source below |
| Microsoft.Testing.Extensions.TrxReport.Abstractions | 2.4.1 | Transitive | MIT | TestFX source below |
| Microsoft.Testing.Platform | 2.4.1 | Transitive | MIT | TestFX source below |
| Microsoft.Testing.Platform.MSBuild | 2.4.1 | Transitive | MIT | TestFX source below |
| Microsoft.TestPlatform.ObjectModel | 18.10.0 | Transitive | MIT | VSTest source below |
| Microsoft.TestPlatform.TestHost | 18.10.0 | Transitive | MIT | VSTest source below |
| MSTest.Analyzers | 4.4.1 | Transitive | MIT | TestFX source below |

- VSTest: [microsoft/vstest](https://github.com/microsoft/vstest), commit
  `5b0c2fc69de12eb4383a7b6b38acab164a2f68fb`.
- TestFX: [microsoft/testfx](https://github.com/microsoft/testfx), commit
  `9a14d19cb8ef1ec6a2aae81d4223a09190f96593`.
- ApplicationInsights: [Microsoft/ApplicationInsights-dotnet](https://github.com/Microsoft/ApplicationInsights-dotnet),
  commit `2faa7e8b157a431daa2e71785d68abd5fa817b53`.

## Dependencies outside NuGet

Windows and the installed .NET/WindowsDesktop runtime are platform prerequisites.
The inspected development SDK is 10.0.401 and WindowsDesktop runtime is 10.0.12;
the installer/runtime redistribution choice remains Phase 12 work.

Local inference uses the separately installed **llama.cpp v0.4.1 / b10964**, MIT,
and official **Qwen3-0.6B Q8_0 GGUF**, Apache-2.0. Runtime/model revisions, SHA256
pins and original publisher links are recorded in [LOCAL_AI.md](LOCAL_AI.md), with
executable/DLL pins embedded in the Infrastructure runtime catalog. The CPU runtime
package also includes LLVM OpenMP and its own license notice. Preserve those
notices if packaging the worker. NuGet advisory results do not cover these artifacts.

The active application uses no hosted AI provider SDK, user API key, WebView2, KaTeX
or CDN assets. The paused language-model-from-scratch archive remains excluded.
The separate optional ML Lab environment below trains local scientific/tabular models;
it does not replace or self-train the pretrained conversational model.

## Optional ML Lab Python environment — 2026-09-29

`scripts/setup-ml-lab.ps1` uses local Python 3.12/3.13 and `uv` to create
`artifacts/ml-runtime`, then runs `uv pip sync --require-hashes` against
[`workers/requirements-ml.lock`](../workers/requirements-ml.lock). The current
development environment records CPython 3.12.14 and uv 0.12.17; these are observed
tools, not a claim that CPython is distributed or hash-pinned by this repository.
The 24 exact Python distributions below match the installed `.dist-info/METADATA`
and generated lock. Setup verification succeeded. Download hashes establish matching
artifacts, not absence of vulnerabilities or malicious code. The historical NuGet
advisory scan above does not audit Python packages; no Python vulnerability audit
or completed redistribution-license review is claimed here.

Eight direct requirements are specified in `workers/requirements-ml.in`:

| Distribution | Version | Installed declared license | Upstream source from metadata |
|---|---|---|---|
| NumPy | 2.3.3 | BSD; bundled notices also apply | [numpy/numpy](https://github.com/numpy/numpy) |
| pandas | 2.3.3 | BSD-3-Clause | [pandas-dev/pandas](https://github.com/pandas-dev/pandas) |
| scikit-learn | 1.7.2 | BSD-3-Clause | [scikit-learn/scikit-learn](https://github.com/scikit-learn/scikit-learn) |
| SciPy | 1.16.2 | BSD; bundled notices also apply | [scipy/scipy](https://github.com/scipy/scipy) |
| Matplotlib | 3.10.6 | Matplotlib license, PSF-style metadata | [matplotlib/matplotlib](https://github.com/matplotlib/matplotlib) |
| h5py | 3.14.0 | BSD-3-Clause; HDF5/LZF and bundled notices also apply | [h5py/h5py](https://github.com/h5py/h5py) |
| skops | 0.13.0 | MIT | [skops-dev/skops](https://github.com/skops-dev/skops) |
| psutil | 7.1.0 | BSD-3-Clause | [giampaolo/psutil](https://github.com/giampaolo/psutil) |

The remaining locked distributions are transitive dependencies:

| Distribution | Version | Installed declared license |
|---|---|---|
| cloudpickle | 3.1.2 | BSD-3-Clause |
| contourpy | 1.4.0 | BSD-3-Clause |
| cycler | 0.12.1 | BSD; packaged LICENSE |
| fonttools | 4.66.1 | MIT; LICENSE.external also applies |
| joblib | 1.6.0 | BSD-3-Clause |
| kiwisolver | 1.5.1 | BSD; packaged LICENSE |
| packaging | 26.3 | Apache-2.0 OR BSD-2-Clause |
| pillow | 12.3.0 | MIT-CMU |
| prettytable | 3.18.0 | BSD-3-Clause |
| pyparsing | 3.3.3 | MIT |
| python-dateutil | 2.9.0.post0 | Dual Apache/BSD declaration; packaged LICENSE |
| pytz | 2026.4 | MIT |
| six | 1.17.0 | MIT |
| threadpoolctl | 3.7.0 | BSD-3-Clause |
| tzdata | 2026.4 | Apache-2.0; packaged timezone notices |
| wcwidth | 0.9.1 | MIT classifier; packaged LICENSE |

The inventory records upstream metadata, not a legal opinion or complete bundled
native-library/font notice analysis. Preserve all wheel license files and review the
actual shipping artifact before redistribution. `cloudpickle`/`joblib` transitives
are installed by the scientific stack; the Lab's model-admission path does not import
arbitrary pickle/joblib artifacts. It admits only its bounded, version-checked `.skops`
artifacts through a fixed type policy.

Parquet/Excel readers and advanced backends are optional capability checks, not part
of these 24 packages. Package discovery does not enable SHAP/UMAP, GPU, deep-learning
or advanced model adapters. The local worker uses CPU computation and Matplotlib Agg
PNG output; no notebook server, cloud training or external inference service is added.

## Optional LAYA advisory environment — 2026-09-30

[`scripts/setup-laya.ps1`](../scripts/setup-laya.ps1) installs a separate
`artifacts/laya-runtime` with local CPython 3.12.14 and uv 0.12.17 observed in this
checkout. It synchronizes **28 exact top-level Python distributions** using
`--require-hashes --only-binary :all:` and the committed
[`workers/requirements-laya.lock`](../workers/requirements-laya.lock). The eight
direct requirements come from [`workers/requirements-laya.in`](../workers/requirements-laya.in).
The versions and license declarations below were read from the installed
`.dist-info/METADATA` and, where stated, packaged license files. This optional
environment does not alter the ML Lab environment or replace Qwen conversation.

| Direct distribution | Installed locked version | Installed declared license | Source |
|---|---|---|---|
| laya | 0.3.21 | Apache-2.0 | [NandhaKishorM/laya](https://github.com/NandhaKishorM/laya/tree/9d955671415fc19f069b9cc998928075c1f255ec) |
| torch | 2.6.0+cpu | BSD-3-Clause; bundled LICENSE/NOTICE also apply | [pytorch/pytorch](https://github.com/pytorch/pytorch) |
| transformers | 4.51.3 | Apache 2.0 | [huggingface/transformers](https://github.com/huggingface/transformers) |
| numpy | 2.3.3 | BSD; bundled notices also apply | [numpy/numpy](https://github.com/numpy/numpy) |
| safetensors | 0.6.2 | Apache classifier; packaged Apache-2.0 LICENSE | [huggingface/safetensors](https://github.com/huggingface/safetensors) |
| huggingface-hub | 0.35.3 | Apache declaration; packaged Apache-2.0 LICENSE | [huggingface/huggingface_hub](https://github.com/huggingface/huggingface_hub) |
| tokenizers | 0.21.4 | Apache Software License classifier; no license file found in installed dist-info | [huggingface/tokenizers](https://github.com/huggingface/tokenizers) |
| psutil | 7.1.0 | BSD-3-Clause | [giampaolo/psutil](https://github.com/giampaolo/psutil) |

| Transitive distribution | Installed locked version | Installed declared license |
|---|---|---|
| certifi | 2026.7.22 | MPL-2.0 |
| charset-normalizer | 3.5.1 | MIT |
| colorama | 0.4.6 | BSD classifier; packaged three-clause BSD LICENSE.txt |
| filelock | 4.0.7 | MIT |
| fsspec | 2026.9.0 | BSD-3-Clause |
| hf-xet | 1.6.0 | Apache-2.0 |
| idna | 3.20 | BSD-3-Clause |
| Jinja2 | 3.1.6 | BSD classifier; packaged three-clause BSD LICENSE.txt |
| MarkupSafe | 3.0.3 | BSD-3-Clause |
| mpmath | 1.3.0 | BSD declaration; packaged three-clause BSD LICENSE |
| networkx | 3.7 | BSD-3-Clause |
| packaging | 26.3 | Apache-2.0 OR BSD-2-Clause |
| PyYAML | 6.0.3 | MIT |
| regex | 2026.9.29 | Apache-2.0 AND CNRI-Python |
| requests | 2.34.2 | Apache-2.0 |
| setuptools | 84.0.0 | MIT; vendored notices also apply |
| sympy | 1.13.1 | BSD declaration; packaged three-clause BSD LICENSE |
| tqdm | 4.70.1 | MPL-2.0 AND MIT |
| typing_extensions | 4.16.0 | PSF-2.0 |
| urllib3 | 2.8.0 | MIT |

The count describes independently locked top-level distributions, not every component
inside their wheels. For example, installed setuptools vendors `autocommand` 2.2.2
with an LGPLv3 metadata declaration and other internal packages; PyTorch contains
additional third-party notices, and NumPy contains bundled native-library notices.
Preserve and review these when preparing an actual redistributable artifact. This
metadata inventory is not a completed license-compliance or vulnerability audit.
The earlier NuGet advisory result does not cover this runtime, and no Python
vulnerability-scan result is claimed.

The installed English checkpoint is
[`convaiinnovations/laya` at `55cf4c4ebb4ebe31b2550e8bdf3bd21b99753851`](https://huggingface.co/convaiinnovations/laya/tree/55cf4c4ebb4ebe31b2550e8bdf3bd21b99753851).
Its publisher model metadata declares Apache-2.0. The reviewed LAYA source revision
is `9d955671415fc19f069b9cc998928075c1f255ec`; its license is retained in
[`workers/laya_advisor/LICENSE.laya`](../workers/laya_advisor/LICENSE.laya).
The [`model manifest`](../workers/laya_advisor/model-manifest.json) records the
source/checkpoint revisions and exact sizes/SHA256 values for all five admitted
model files: 846,195,574 bytes total, including 842,609,210 bytes of safetensors.
Setup verified these files; these are asset sizes, not process memory or installed
Python-runtime size. A modified existing asset is preserved and refused.

The current worker uses CPU-only Torch 2.6.0, explicitly constructs float16 parameters,
and uses CPU float16 autocast. Transformers 4.51.3 preserves that scoped dtype policy
for this checkpoint; the worker verifies parameter dtypes after loading. A smaller
CPU wheel replaced the initial Torch 2.8 attempt that exhausted disk during copying.
This precision policy avoids a full float32 parameter copy and may change scores
relative to float32; it does not establish calibration or scientific accuracy.
Inference uses local safetensors and verified configuration files, with offline
environment flags and a network audit guard. Installed Hub/HTTP/Xet packages are
dependencies, not permission to transmit prompts or dataset contents.

**Qualification remains limited:** setup/import checks and a seeded tiny native
ModernBERT/LAYA-head precision test passed, and protocol/security tests passed.
Successful full-checkpoint inference inside Mu's 3 GiB Windows job is still
unqualified because the available system memory/commit failed admission. The worker
requires 1.5 GiB available physical RAM, 3 GiB system commit headroom and 2.5 GiB
process commit headroom before native imports, and returns a clear resource error
when these are unavailable. Installed assets and a successful tiny-model test must
not be presented as a successful pretrained-model run. See the
[`worker documentation`](../workers/laya_advisor/README.md) for protocol and limits.

## Optional Kokoro neural speech environment — 2026-09-30

[`scripts/setup-neural-speech.ps1`](../scripts/setup-neural-speech.ps1) creates
`artifacts/neural-speech-runtime` using local Python 3.12 and `uv`, then synchronizes
**18 exact Python distributions** with `--require-hashes --only-binary :all:` against
[`workers/requirements-neural-speech.lock`](../workers/requirements-neural-speech.lock).
CPython 3.12.14 and uv 0.12.17 were observed in this checkout; they are local tools,
not repository-pinned redistributed runtimes. This environment is separate from
`artifacts/ml-runtime` and `artifacts/laya-runtime`. It does not install PyTorch, a GPU
runtime, a web server, an audio-device package, or a hosted voice SDK. Windows
in-memory playback uses Python's standard-library `winsound` in the owned worker.
The existing System.Speech adapter remains available as a separate voice choice.

The five direct requirements are in
[`workers/requirements-neural-speech.in`](../workers/requirements-neural-speech.in).
Versions below match the installed distribution metadata and lock. License entries
identify metadata, packaged notices, or the inspected upstream license explicitly.

| Direct distribution | Installed locked version | License evidence | Source |
|---|---|---|---|
| kokoro-onnx | 0.6.1 | Packaged MIT LICENSE | [thewh1teagle/kokoro-onnx](https://github.com/thewh1teagle/kokoro-onnx) |
| onnxruntime | 1.23.2 | MIT classifier and packaged LICENSE | [microsoft/onnxruntime](https://github.com/microsoft/onnxruntime) |
| numpy | 2.3.3 | BSD classifier; packaged and bundled notices also apply | [numpy/numpy](https://github.com/numpy/numpy) |
| phonemizer | 3.4.0 | GPLv3-or-later classifier and packaged GPL-3.0 LICENSE | [bootphon/phonemizer](https://github.com/bootphon/phonemizer) |
| espeakng-loader | 0.2.4 | No license declaration/file in installed dist-info; inspected upstream loader is MIT, bundled eSpeak NG is GPL-3.0 | [thewh1teagle/espeakng-loader](https://github.com/thewh1teagle/espeakng-loader), [eSpeak NG](https://github.com/espeak-ng/espeak-ng) |

The thirteen remaining distributions are transitive dependencies:

| Transitive distribution | Installed locked version | Installed license evidence |
|---|---|---|
| attrs | 26.1.0 | MIT expression and packaged LICENSE |
| cloudpickle | 3.1.2 | BSD classifier; packaged three-clause BSD LICENSE |
| coloredlogs | 15.0.1 | MIT classifier and packaged LICENSE.txt |
| dlinfo | 2.0.0 | MIT declaration and packaged LICENSE |
| flatbuffers | 25.12.19 | Apache Software License classifier; no license file found in installed dist-info |
| humanfriendly | 10.0 | MIT classifier and packaged LICENSE.txt |
| joblib | 1.6.0 | BSD-3-Clause expression and packaged LICENSE.txt |
| mpmath | 1.3.0 | BSD classifier; packaged three-clause BSD LICENSE |
| packaging | 26.3 | Apache-2.0 OR BSD-2-Clause expression; packaged notices |
| protobuf | 7.36.2 | Three-clause BSD declaration and packaged LICENSE |
| pyreadline3 | 3.5.6 | BSD classifier; packaged LICENSE.md also identifies file-specific exceptions |
| sympy | 1.14.0 | BSD classifier; packaged three-clause BSD LICENSE and parser notices |
| typing_extensions | 4.16.0 | PSF-2.0 expression and packaged LICENSE |

The count covers installed Python distributions, not every embedded native library
or component. Major upstream notices, including GPL phonemizer/eSpeak and the
Apache model license, are retained in
[`workers/neural_speech/licenses`](../workers/neural_speech/licenses/). The full
installed metadata inventory is retained at
`artifacts/neural-voice-research/runtime-license-inventory.json`. This optional stack
must not be described as wholly permissive: packaging and redistributing the GPL
components needs a corresponding-source and license-compliance review. Source links
and notice copies are not a completed redistribution offer. NumPy and other wheels
have additional bundled notices. No Python vulnerability audit or complete license
audit is claimed, and the earlier NuGet advisory result does not cover this runtime.

The model is **Kokoro-82M v1.0 FP32**, obtained from the upstream
[`model-files-v1.1` ONNX release](https://github.com/thewh1teagle/kokoro-onnx/releases/tag/model-files-v1.1).
The release tag is a provenance label; the exact sizes and SHA256 pins in
[`workers/neural_speech/model-manifest.json`](../workers/neural_speech/model-manifest.json),
the worker, and setup script are the admission authority. The installed assets
total **353,719,767 bytes**, separately stored once in `artifacts/neural-speech-model`:

| Asset | Bytes | SHA256 |
|---|---:|---|
| kokoro-v1.0.onnx | 325,505,369 | `beb0d1848dee9a49da392cc3df26958d46cfa35d321edf434f52949153f0df3a` |
| voices-v1.0.bin | 28,214,398 | `bca610b8308e8d99f32e6fe4197e7ec01679264efed0cac9140fe9c29f1fbf7d` |

The [publisher model metadata](https://huggingface.co/hexgrad/Kokoro-82M) and ONNX
project identify the model as Apache-2.0. The inspected Kokoro ONNX source commit was
`3596b26764286a7de9d90c363e988d50578918e5`; the installed 0.6.1 wheel is independently
pinned by SHA256
`50c8de4950d601df41428ee5462a48c8a78bef441bf671f2492e070ef44d8a32`.
The closed voice catalog exposes Heart, Bella and Michael in US English, and George
in UK English. These are pretrained general voices, without voice cloning. Model
asset size does not describe installed runtime size, RAM use, or a naturalness score.

Runtime speech is local and CPU-only. It uses the pinned files with read-only
Windows leases, explicit CPUExecutionProvider, two inference threads, one
inter-operation thread, and CPU memory arena disabled. Before native imports,
the worker requires **512 MiB available physical RAM, 1,536 MiB system commit
headroom, and 1,024 MiB process commit headroom**; queries fail closed. The host owns
one worker in a **1,536 MiB** Windows job with a **90-second synthesis deadline**,
**30-second playback deadline**, and **3-second cleanup bound**. Speech input is
limited to 2,000 UTF-16 units; phoneme batches are at most 160 characters and output
is shortened to at most 29.5 seconds to leave terminal-event overhead inside the
playback deadline. Normal speech text and audio stay in memory and are not saved.

One temporary-file exception is the upstream phonemizer's copies of its packaged
eSpeak DLL. Each operation has an owned temporary directory; the host removes that
directory only after its held worker exits, including cancellation where Python's
atexit cleanup cannot run. Those files contain library code, without speech text or
audio. The worker preloads the packaged DLL and does not search for a system-wide
replacement. Offline flags and Python audit guards refuse DNS, connections,
datagrams and child processes; they are not a general native-code sandbox.

Setup, idempotent asset reuse and the four-voice catalog have been executed.
One fixed public Heart sample also completed through the production memory-limited
host: 15.804 seconds of validated PCM16 mono 24 kHz audio, 758,636 WAV bytes.
Its receipt is `artifacts/neural-voice-research/host-probe-native.txt`. This is a
bounded synthesis observation, not a naturalness benchmark or a certification of
every UI/playback/device workflow. The 21-case four-voice native inaudible diagnostic
now passes and its exported public audio passes independent PCM inspection.
Actual native Speaking/Stop reached 24 of 25 checks before short reuse failed.
A separate normal-Speak check exposed a held-termination race; the narrow repair
passes 398 regression tests, but its real 33-case rerun safely returned
`memory_limit` at five checks. Normal completed reuse/disposal and Qwen co-residency
remain unqualified, alongside physical listening, pronunciation quality,
device-loss behavior and release redistribution. The unfinished LAYA/full-ML qualifications above
are unchanged by this optional voice installation. See the
[`neural worker documentation`](../workers/neural_speech/README.md) for the protocol.
