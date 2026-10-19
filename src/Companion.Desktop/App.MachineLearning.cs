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

    private IMlLabService CreateMlLabService(string? projectDirectory = null)
    {
        var root = MlRootDirectory();
        return new LocalMlLabService(Path.Combine(root, "artifacts", "ml-runtime", "Scripts", "python.exe"),
            Path.Combine(root, "workers", "ml_lab", "worker.py"), projectDirectory ?? Path.Combine(options!.DataDirectory, "ml-lab", "default-project"));
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
        _ = SelectResearchProjectAsync();
    }

    private IResearchProjectStore? researchProjects;
    private ResearchLabWorkspace? researchWorkspace;
    private ResearchProjectsWindow? researchProjectsWindow;
    private bool selectingResearchProject;

    private async Task SelectResearchProjectAsync()
    {
        if (stopping || companion is null || options is null || selectingResearchProject) return;
        if (researchWorkspace?.IsBusy == true || mlLabWindow?.IsBusy == true)
        {
            MessageBox.Show("Wait for Lab work to finish or cancel its jobs before changing projects.", "Mu Research projects");
            return;
        }
        selectingResearchProject = true;
        var previous = mlLabWindow;
        try
        {
            researchProjects ??= new JsonResearchProjectStore(options.DataDirectory);
            researchWorkspace ??= new(researchProjects, project => CreateMlLabService(project.Directory));
            previous?.BeginShutdown();
            researchProjectsWindow = new(researchProjects) { Owner = previous ?? (Window)companion };
            if (researchProjectsWindow.ShowDialog() != true || researchProjectsWindow.SelectedProjectId is not { } id || stopping) return;
            if (previous is not null) previous.IsEnabled = false;
            var selection = await researchWorkspace.OpenAsync(id, runtime!.Lifetime.Token);
            if (stopping) return;
            previous?.Close();
            layaService ??= CreateLayaService();
            if (mlPlannerHost is null && options.LocalModelDirectory is { } modelDirectory)
            {
                try { mlPlannerHost = new LocalModelHost(modelDirectory); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException) { }
            }
            Func<CancellationToken, Task<IChatModel>>? planner = mlPlannerHost is null ? null : token => mlPlannerHost.GetModelAsync(token);
            mlLabWindow = new MlLabWindow(selection.Service, planner, layaService, selection.Project) { Owner = companion };
            var window = mlLabWindow;
            window.Closed += (_, _) => { if (ReferenceEquals(mlLabWindow, window)) mlLabWindow = null; };
            window.ChangeProjectRequested += (_, _) => _ = SelectResearchProjectAsync();
            mlLabWindow.Show();
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or MlException or ObjectDisposedException)
        {
            if (!stopping) MessageBox.Show(error is MlException ? error.Message : "The ML Lab could not open its local project. Check folder access and available storage.", "Mu Research projects");
        }
        finally
        {
            researchProjectsWindow = null;
            selectingResearchProject = false;
            if (!stopping && previous is not null && ReferenceEquals(mlLabWindow, previous)) previous.IsEnabled = true;
        }
    }
}
