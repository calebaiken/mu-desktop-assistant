using System.Text.Json;
using System.Xml.Linq;

if (args.Length != 1 || !File.Exists(Path.Combine(args[0], "Companion.slnx")))
{
    Console.Error.WriteLine("Usage: architecture-checks <repository-root>");
    return 2;
}

var root = Path.GetFullPath(args[0]);
var failures = 0;
var count = 0;
void Check(string name, Action assertion)
{
    count++;
    try
    {
        assertion();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception error)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {name}: {error.Message}");
    }
}

void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

XDocument Xml(string path) => XDocument.Load(Path.Combine(root, path));

var expected = new Dictionary<string, string[]>
{
    ["Core"] = [],
    ["Application"] = ["Core"],
    ["Infrastructure"] = ["Application", "Core"],
    ["Desktop"] = ["Application", "Core", "Infrastructure"]
};
foreach (var (layer, dependencies) in expected)
{
    Check($"{layer} project dependency boundary", () =>
    {
        var doc = Xml($"src/Companion.{layer}/Companion.{layer}.csproj");
        var actual = doc.Descendants("ProjectReference")
            .Select(node => (string?)node.Attribute("Include") ?? "")
            .Order(StringComparer.Ordinal).ToArray();
        var allowed = dependencies.Select(name => $"../Companion.{name}/Companion.{name}.csproj")
            .Order(StringComparer.Ordinal).ToArray();
        Require(actual.SequenceEqual(allowed), "Unexpected reference direction or path.");
        var packages = doc.Descendants("PackageReference")
            .Select(node => ((string?)node.Attribute("Include"), (string?)node.Attribute("Version"))).ToArray();
        var reviewedPackages = layer == "Infrastructure"
            ? new[] { ((string?)"Microsoft.Data.Sqlite", (string?)"10.0.12"), ((string?)"System.Speech", (string?)"10.0.12") }
            : layer == "Desktop"
                ? new[] { ((string?)"Markdig", (string?)"1.3.2"), ((string?)"WpfMath", (string?)"2.1.0") }
                : [];
        Require(packages.SequenceEqual(reviewedPackages), "Production package inventory differs from reviewed pins.");
    });
}

Check("solution includes all six projects", () =>
{
    var projects = Xml("Companion.slnx").Descendants("Project")
        .Select(node => (string?)node.Attribute("Path") ?? "").ToArray();
    var required = expected.Keys.Select(layer => $"src/Companion.{layer}/Companion.{layer}.csproj")
        .Append("tests/Companion.ArchitectureChecks/Companion.ArchitectureChecks.csproj")
        .Append("tests/Companion.Tests/Companion.Tests.csproj");
    Require(projects.Order().SequenceEqual(required.Order()), "Solution project inventory differs.");
    Require(projects.All(path => File.Exists(Path.Combine(root, path))), "Missing project file.");
});

Check("strict build settings", () =>
{
    var doc = Xml("Directory.Build.props");
    foreach (var setting in new[] { "TreatWarningsAsErrors", "EnableNETAnalyzers", "Deterministic", "RestorePackagesWithLockFile" })
        Require(doc.Descendants(setting).Single().Value == "true", $"{setting} disabled.");
    Require(doc.Descendants("Nullable").Single().Value == "enable", "Nullable analysis disabled.");
    Require(doc.Descendants("AnalysisLevel").Single().Value == "latest", "Analyzer baseline changed.");
});

Check("SDK pin excludes previews", () =>
{
    using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "global.json")));
    var sdk = json.RootElement.GetProperty("sdk");
    Require(sdk.GetProperty("version").GetString() == "10.0.401", "SDK baseline changed; review required.");
    Require(sdk.GetProperty("rollForward").GetString() == "latestPatch", "SDK rolls across feature bands.");
    Require(!sdk.GetProperty("allowPrerelease").GetBoolean(), "Preview SDK allowed.");
});

Check("least privilege and DPI manifest", () =>
{
    var doc = Xml("src/Companion.Desktop/app.manifest");
    var privileges = doc.Descendants().Single(node => node.Name.LocalName == "requestedExecutionLevel");
    Require((string?)privileges.Attribute("level") == "asInvoker", "Elevation requested.");
    Require((string?)privileges.Attribute("uiAccess") == "false", "UIAccess requested.");
    Require(doc.Descendants().Single(node => node.Name.LocalName == "dpiAwareness").Value == "PerMonitorV2", "Missing DPI declaration.");
});

Check("required planning documents", () =>
{
    foreach (var path in new[] { "README.md", "ARCHITECTURE.md", "SECURITY.md", "TESTING.md", "ROADMAP.md", "CHANGELOG.md", "DEVELOPMENT_STATUS.md", "docs/CONTRACTS.md", "docs/REQUIREMENTS.md", "docs/QA_PROCESS.md" })
        Require(new FileInfo(Path.Combine(root, path)).Length > 100, $"Missing or empty {path}.");
});

Check("reviewed package source", () =>
{
    var sources = Xml("NuGet.Config").Root!.Element("packageSources")!;
    Require(sources.Elements().Count() == 2 && sources.Element("clear") is not null, "Unexpected package sources.");
    Require((string?)sources.Element("add")?.Attribute("value") == "https://api.nuget.org/v3/index.json", "Unreviewed package feed.");
});

Check("sensitive local artifacts ignored", () =>
{
    var lines = File.ReadAllLines(Path.Combine(root, ".gitignore"));
    foreach (var rule in new[] { ".env", ".env.*", "*.db", "*.db-*", "*.log", "*.dmp", "*.pfx", "*.key", "secrets/" })
        Require(lines.Contains(rule), $"Missing ignore rule {rule}.");
});

Console.WriteLine($"{count - failures}/{count} architecture checks passed.");
return failures == 0 ? 0 : 1;
