using System.IO;

namespace Companion.Desktop;

internal enum PerformanceObserver { Threads, Process }

internal sealed record StartupOptions(string DataDirectory, bool Smoke, bool UiSmoke, bool PerformanceSmoke,
    PerformanceObserver Observer, string? LocalModelDirectory = null, bool LocalModelSmoke = false, bool PrivateHistorySmoke = false,
    bool SpeechSmoke = false, bool SpeechPlayback = false, bool PrivateSpeechSmoke = false,
    string? MlRoot = null, bool MlSmoke = false, bool LayaSmoke = false,
    bool NeuralSpeechSmoke = false, bool NeuralSpeechPlayback = false)
{
    public bool IsTestSession => Smoke || UiSmoke || PerformanceSmoke || LocalModelSmoke || PrivateHistorySmoke || SpeechSmoke || PrivateSpeechSmoke || MlSmoke || LayaSmoke || NeuralSpeechSmoke;
    public bool IsDefaultProfile => string.Equals(Path.TrimEndingDirectorySeparator(DataDirectory),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopCompanion"),
        StringComparison.OrdinalIgnoreCase);
    public static StartupOptions Parse(string[] args)
    {
        string? directory = null;
        string? localModelDirectory = null;
        string? mlRoot = null;
        var mlSmoke = false;
        var layaSmoke = false;
        var neuralSpeechSmoke = false;
        var neuralSpeechPlayback = false;
        var smoke = false;
        var uiSmoke = false;
        var performanceSmoke = false;
        var localModelSmoke = false;
        var privateHistorySmoke = false;
        var speechSmoke = false;
        var speechPlayback = false;
        var privateSpeechSmoke = false;
        var observer = PerformanceObserver.Threads;
        var observerSelected = false;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--ml-root" when mlRoot is null && index + 1 < args.Length:
                    mlRoot = args[++index];
                    if (!Path.IsPathFullyQualified(mlRoot) || mlRoot.StartsWith("\\\\", StringComparison.Ordinal) || mlRoot.StartsWith("//", StringComparison.Ordinal))
                        throw new ArgumentException("The ML root must be an absolute local path.");
                    mlRoot = Path.GetFullPath(mlRoot);
                    break;
                case "--ml-smoke" when !mlSmoke: mlSmoke = true; break;
                case "--laya-smoke" when !layaSmoke: layaSmoke = true; break;
                case "--neural-speech-smoke" when !neuralSpeechSmoke: neuralSpeechSmoke = true; break;
                case "--neural-speech-playback" when !neuralSpeechPlayback: neuralSpeechPlayback = true; break;
                case "--smoke" when !smoke && !uiSmoke && !performanceSmoke && !localModelSmoke && !privateHistorySmoke && !speechSmoke && !privateSpeechSmoke: smoke = true; break;
                case "--ui-smoke" when !smoke && !uiSmoke && !performanceSmoke && !localModelSmoke && !privateHistorySmoke && !speechSmoke && !privateSpeechSmoke: uiSmoke = true; break;
                case "--performance-smoke" when !smoke && !uiSmoke && !performanceSmoke && !localModelSmoke && !privateHistorySmoke && !speechSmoke && !privateSpeechSmoke: performanceSmoke = true; break;
                case "--local-model-smoke" when !smoke && !uiSmoke && !performanceSmoke && !localModelSmoke && !privateHistorySmoke && !speechSmoke && !privateSpeechSmoke: localModelSmoke = true; break;
                case "--private-history-smoke" when !smoke && !uiSmoke && !performanceSmoke && !localModelSmoke && !privateHistorySmoke && !speechSmoke && !privateSpeechSmoke: privateHistorySmoke = true; break;
                case "--speech-smoke" when !smoke && !uiSmoke && !performanceSmoke && !localModelSmoke && !privateHistorySmoke && !speechSmoke && !privateSpeechSmoke: speechSmoke = true; break;
                case "--private-speech-smoke" when !smoke && !uiSmoke && !performanceSmoke && !localModelSmoke && !privateHistorySmoke && !speechSmoke && !privateSpeechSmoke: privateSpeechSmoke = true; break;
                case "--speech-playback" when !speechPlayback: speechPlayback = true; break;
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
        if (mlSmoke && (mlRoot is null || directory is null || smoke || uiSmoke || performanceSmoke || localModelSmoke || privateHistorySmoke || speechSmoke || privateSpeechSmoke))
            throw new ArgumentException("ML diagnostics require an explicit ML root, isolated profile and no other diagnostic mode.");
        if (layaSmoke && (mlRoot is null || directory is null || localModelDirectory is not null || mlSmoke || smoke || uiSmoke || performanceSmoke || localModelSmoke || privateHistorySmoke || speechSmoke || privateSpeechSmoke))
            throw new ArgumentException("LAYA diagnostics require an explicit ML root, isolated profile and no chat model or other diagnostic mode.");
        if (neuralSpeechSmoke && (mlRoot is null || directory is null || localModelDirectory is not null || mlSmoke || layaSmoke || smoke || uiSmoke || performanceSmoke || localModelSmoke || privateHistorySmoke || speechSmoke || privateSpeechSmoke || speechPlayback))
            throw new ArgumentException("Neural speech diagnostics require an explicit root, isolated profile and no other diagnostic mode or chat model.");
        if (neuralSpeechPlayback && !neuralSpeechSmoke)
            throw new ArgumentException("Neural playback requires its explicit diagnostic mode.");
        if (observerSelected && !performanceSmoke)
            throw new ArgumentException("A performance observer requires performance smoke mode.");
        if (localModelSmoke && localModelDirectory is null)
            throw new ArgumentException("Local model smoke requires an explicit local model directory.");
        if (privateHistorySmoke && (directory is null || localModelDirectory is not null))
            throw new ArgumentException("Private history smoke requires an explicit isolated profile and no model directory.");
        if ((speechSmoke || privateSpeechSmoke) && (directory is null || localModelDirectory is not null))
            throw new ArgumentException("Speech diagnostics require an explicit isolated profile and no model directory.");
        if (speechPlayback && !speechSmoke) throw new ArgumentException("Diagnostic playback requires speech smoke mode.");
        if (smoke || uiSmoke || performanceSmoke || localModelSmoke || privateHistorySmoke || speechSmoke || privateSpeechSmoke)
            directory ??= Path.Combine(Path.GetTempPath(), "DesktopCompanion-Smoke", Guid.NewGuid().ToString("N"));
        directory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopCompanion");
        if ((privateHistorySmoke || speechSmoke || privateSpeechSmoke || mlSmoke || layaSmoke || neuralSpeechSmoke) && string.Equals(Path.TrimEndingDirectorySeparator(directory),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopCompanion"), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Content diagnostics cannot use the normal application profile.");
        return new(directory, smoke, uiSmoke, performanceSmoke, observer, localModelDirectory, localModelSmoke, privateHistorySmoke,
            speechSmoke, speechPlayback, privateSpeechSmoke, mlRoot, mlSmoke, layaSmoke, neuralSpeechSmoke, neuralSpeechPlayback);
    }
}
