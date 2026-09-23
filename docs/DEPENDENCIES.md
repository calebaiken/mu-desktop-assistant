# Dependency inventory

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
model footprint. The implementation uses Windows speech synthesis only. The package
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

The active application uses no hosted AI provider SDK, user API key, Python training
environment, WebView2, KaTeX or CDN assets. The paused from-scratch training archive
is excluded from the runnable application's dependency graph.
