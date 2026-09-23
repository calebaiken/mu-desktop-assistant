using System.IO;

namespace Companion.Desktop;

internal enum PerformanceObserver { Threads, Process }

internal sealed record StartupOptions(string DataDirectory, bool Smoke, bool UiSmoke, bool PerformanceSmoke,
    PerformanceObserver Observer, string? LocalModelDirectory = null, bool LocalModelSmoke = false)
{
    public bool IsTestSession => Smoke || UiSmoke || PerformanceSmoke || LocalModelSmoke;
    public bool IsDefaultProfile => string.Equals(Path.TrimEndingDirectorySeparator(DataDirectory),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopCompanion"),
        StringComparison.OrdinalIgnoreCase);
    public static StartupOptions Parse(string[] args)
    {
        string? directory = null;
        string? localModelDirectory = null;
        var smoke = false;
        var uiSmoke = false;
        var performanceSmoke = false;
        var localModelSmoke = false;
        var observer = PerformanceObserver.Threads;
        var observerSelected = false;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--smoke" when !smoke && !uiSmoke && !performanceSmoke && !localModelSmoke: smoke = true; break;
                case "--ui-smoke" when !smoke && !uiSmoke && !performanceSmoke && !localModelSmoke: uiSmoke = true; break;
                case "--performance-smoke" when !smoke && !uiSmoke && !performanceSmoke && !localModelSmoke: performanceSmoke = true; break;
                case "--local-model-smoke" when !smoke && !uiSmoke && !performanceSmoke && !localModelSmoke: localModelSmoke = true; break;
                case "--performance-observer" when !observerSelected && index + 1 < args.Length:
                    observer = args[++index] switch
                    {
                        "threads" => PerformanceObserver.Threads,
                        "process" => PerformanceObserver.Process,
                        _ => throw new ArgumentException("Unsupported performance observer.")
                    };
                    observerSelected = true;
                    break;
                case "--data-dir" when directory is null && index + 1 < args.Length:
                    directory = args[++index];
                    if (!Path.IsPathFullyQualified(directory))
                        throw new ArgumentException("The data directory must be an absolute local path.");
                    directory = Path.GetFullPath(directory);
                    if (directory.StartsWith(@"\\", StringComparison.Ordinal))
                        throw new ArgumentException("The data directory must be a local drive path.");
                    break;
                case "--local-model-dir" when localModelDirectory is null && index + 1 < args.Length:
                    localModelDirectory = args[++index];
                    if (!Path.IsPathFullyQualified(localModelDirectory) || localModelDirectory.StartsWith(@"\\", StringComparison.Ordinal))
                        throw new ArgumentException("The model directory must be an absolute local path.");
                    localModelDirectory = Path.GetFullPath(localModelDirectory);
                    if (localModelDirectory.StartsWith(@"\\", StringComparison.Ordinal))
                        throw new ArgumentException("The model directory must be a local drive path.");
                    break;
                default: throw new ArgumentException("Unsupported command line option.");
            }
        }
        if (observerSelected && !performanceSmoke)
            throw new ArgumentException("A performance observer requires performance smoke mode.");
        if (localModelSmoke && localModelDirectory is null)
            throw new ArgumentException("Local model smoke requires an explicit local model directory.");
        if (smoke || uiSmoke || performanceSmoke || localModelSmoke)
            directory ??= Path.Combine(Path.GetTempPath(), "DesktopCompanion-Smoke", Guid.NewGuid().ToString("N"));
        directory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopCompanion");
        return new(directory, smoke, uiSmoke, performanceSmoke, observer, localModelDirectory, localModelSmoke);
    }
}
