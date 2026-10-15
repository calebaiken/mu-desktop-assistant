using System.IO;
using Companion.Application.Voice;
using Companion.Infrastructure.Voice;

namespace Companion.Desktop;

public partial class App
{
    private KokoroNeuralSpeechOutput CreateNeuralSpeechOutput()
    {
        var root = MlRootDirectory();
        return new KokoroNeuralSpeechOutput(
            Path.Combine(root, "artifacts", "neural-speech-runtime", "Scripts", "python.exe"),
            Path.Combine(root, "workers", "neural_speech", "worker.py"),
            Path.Combine(root, "artifacts", "neural-speech-model"));
    }

    private ISpeechOutput CreateSpeechOutput()
    {
        var windows = new WindowsSpeechOutput(Path.Combine(AppContext.BaseDirectory, "Companion.Desktop.exe"));
        var root = MlRootDirectory();
        if (!File.Exists(Path.Combine(root, "artifacts", "neural-speech-runtime", "Scripts", "python.exe")) ||
            !File.Exists(Path.Combine(root, "workers", "neural_speech", "worker.py"))) return windows;
        return new CompositeSpeechOutput(windows, CreateNeuralSpeechOutput());
    }
}
