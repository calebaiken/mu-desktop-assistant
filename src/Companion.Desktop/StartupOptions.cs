using System.IO;

namespace Companion.Desktop;

internal enum PerformanceObserver { Threads, Process }

internal sealed record StartupOptions(string DataDirectory, bool Smoke, bool UiSmoke, bool PerformanceSmoke,
    PerformanceObserver Observer)
{
    public bool IsTestSession => Smoke || UiSmoke || PerformanceSmoke;
    public bool IsDefaultProfile => string.Equals(Path.TrimEndingDirectorySeparator(DataDirectory),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopCompanion"),
        StringComparison.OrdinalIgnoreCase);
    public static StartupOptions Parse(string[] args)
    {
        string? directory = null;
        var smoke = false;
        var uiSmoke = false;
        var performanceSmoke = false;
        var observer = PerformanceObserver.Threads;
        var observerSelected = false;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--smoke" when !smoke && !uiSmoke && !performanceSmoke: smoke = true; break;
                case "--ui-smoke" when !smoke && !uiSmoke && !performanceSmoke: uiSmoke = true; break;
                case "--performance-smoke" when !smoke && !uiSmoke && !performanceSmoke: performanceSmoke = true; break;
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
                default: throw new ArgumentException("Unsupported command line option.");
            }
        }
        if (observerSelected && !performanceSmoke)
            throw new ArgumentException("A performance observer requires performance smoke mode.");
        if (smoke || uiSmoke || performanceSmoke)
            directory ??= Path.Combine(Path.GetTempPath(), "DesktopCompanion-Smoke", Guid.NewGuid().ToString("N"));
        directory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopCompanion");
        return new(directory, smoke, uiSmoke, performanceSmoke, observer);
    }
}
