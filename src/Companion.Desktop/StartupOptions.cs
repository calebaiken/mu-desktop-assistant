using System.IO;

namespace Companion.Desktop;

internal sealed record StartupOptions(string DataDirectory, bool Smoke, bool UiSmoke)
{
    public static StartupOptions Parse(string[] args)
    {
        string? directory = null;
        var smoke = false;
        var uiSmoke = false;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--smoke" when !smoke && !uiSmoke: smoke = true; break;
                case "--ui-smoke" when !smoke && !uiSmoke: uiSmoke = true; break;
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
        if (smoke || uiSmoke)
            directory ??= Path.Combine(Path.GetTempPath(), "DesktopCompanion-Smoke", Guid.NewGuid().ToString("N"));
        directory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopCompanion");
        return new(directory, smoke, uiSmoke);
    }
}
