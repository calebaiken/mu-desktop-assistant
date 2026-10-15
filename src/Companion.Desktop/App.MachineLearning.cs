using System.IO;
using System.Windows;
using Companion.Application.Conversation;
using Companion.Application.MachineLearning;
using Companion.Infrastructure.Conversation;
using Companion.Infrastructure.MachineLearning;

namespace Companion.Desktop;

public partial class App
{
    private string MlRootDirectory()
    {
        if (options is null) throw new InvalidOperationException();
        var root = options.MlRoot ?? AppContext.BaseDirectory;
        if (options.MlRoot is null)
        {
            // Source development convenience; installed layouts can place artifacts/workers beside Mu.
            for (var candidate = new DirectoryInfo(root); candidate is not null; candidate = candidate.Parent)
                if (File.Exists(Path.Combine(candidate.FullName, "Companion.slnx"))) { root = candidate.FullName; break; }
        }
        return root;
    }

    private IMlLabService CreateMlLabService()
    {
        var root = MlRootDirectory();
        return new LocalMlLabService(Path.Combine(root, "artifacts", "ml-runtime", "Scripts", "python.exe"),
            Path.Combine(root, "workers", "ml_lab", "worker.py"), Path.Combine(options!.DataDirectory, "ml-lab", "default-project"));
    }

    private ILayaDecisionService CreateLayaService()
    {
        var root = MlRootDirectory();
        return new LocalLayaDecisionService(Path.Combine(root, "artifacts", "laya-runtime", "Scripts", "python.exe"),
            Path.Combine(root, "workers", "laya_advisor", "worker.py"), Path.Combine(root, "artifacts", "laya-model"));
    }

    private void OpenMlLab()
    {
        if (stopping || companion is null || options is null || options.IsTestSession) return;
        if (mlLabWindow is not null)
        { mlLabWindow.Show(); mlLabWindow.WindowState = WindowState.Normal; mlLabWindow.Activate(); return; }
        try
        {
            mlLabService ??= CreateMlLabService();
            layaService ??= CreateLayaService();
            if (mlPlannerHost is null && options.LocalModelDirectory is { } modelDirectory)
            {
                try { mlPlannerHost = new LocalModelHost(modelDirectory); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException) { }
            }
            Func<CancellationToken, Task<IChatModel>>? planner = mlPlannerHost is null ? null : token => mlPlannerHost.GetModelAsync(token);
            mlLabWindow = new MlLabWindow(mlLabService, planner, layaService) { Owner = companion };
            mlLabWindow.Closed += (_, _) => mlLabWindow = null;
            mlLabWindow.Show();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show("The ML Lab could not open its local project. Check folder access and available storage.", "Mu Machine Learning Lab");
        }
    }
}
