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
        if (stopping || companion is null || options is null || options.IsTestSession || selectingResearchProject) return;
        if (mlLabWindow is not null)
        { mlLabWindow.Show(); mlLabWindow.WindowState = WindowState.Normal; mlLabWindow.Activate(); return; }
        if (researchWorkspace?.Current is { } active)
        {
            try { ShowResearchLab(active); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            { MessageBox.Show("The active Lab view could not reopen. Its jobs still belong to Mu; restart Mu if the view remains unavailable.", "Mu Research projects"); }
            return;
        }
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
            ShowResearchLab(selection);
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

    private ResearchHandoffWindow? researchHandoffWindow;

    private async Task<bool> TransferChatRequestAsync(ChatWindow source, string request, CancellationToken cancellationToken)
    {
        if (stopping || options is null || !ReferenceEquals(chatWindow, source) || source.IsTemporary || selectingResearchProject)
            return false;
        ResearchRequestHandoff.ValidateRequest(request);
        if (researchWorkspace?.IsBusy == true || mlLabWindow is { CanAcceptResearchRequest: false })
            throw new MlException("project_busy", "Wait for Lab work or planning to finish before transferring a request.");
        selectingResearchProject = true;
        try
        {
            researchProjects ??= new JsonResearchProjectStore(options.DataDirectory);
            researchWorkspace ??= new(researchProjects, project => CreateMlLabService(project.Directory));
            var picker = new ResearchProjectsWindow(researchProjects) { Owner = source };
            researchProjectsWindow = picker;
            ResearchHandoffWindow? preview = null;
            void CancelDialogs()
            {
                // On Chat.Closing, unwind the modal before WPF tears down its owner.
                // References belong only to this transfer; late cancellation cannot close a later dialog.
                if (preview is { IsVisible: true }) preview.DialogResult = false;
                if (picker.IsVisible) picker.DialogResult = false;
            }
            using var cancelled = cancellationToken.Register(() =>
            {
                if (Dispatcher.CheckAccess()) CancelDialogs();
                else if (!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(new Action(CancelDialogs));
            });
            cancellationToken.ThrowIfCancellationRequested();
            if (picker.ShowDialog() != true || picker.SelectedProjectId is not { } id) return false;
            cancellationToken.ThrowIfCancellationRequested();
            var selection = await researchWorkspace.OpenAsync(id, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (stopping || !ReferenceEquals(chatWindow, source)) return false;
            // The existing picker explicitly changes the active project. Old views
            // are retired even if the subsequent request preview is cancelled.
            if (mlLabWindow is { } previous && !previous.IsBoundTo(selection)) previous.Close();
            var intendedView = mlLabWindow;
            if (intendedView is { CanAcceptResearchRequest: false }) return false;
            using var handoff = researchWorkspace.PrepareHandoff(id, request, source.IsTemporary);
            preview = new(handoff) { Owner = source };
            researchHandoffWindow = preview;
            cancellationToken.ThrowIfCancellationRequested();
            if (preview.ShowDialog() != true) return false;
            cancellationToken.ThrowIfCancellationRequested();
            if (stopping || !ReferenceEquals(chatWindow, source) || !ReferenceEquals(mlLabWindow, intendedView)) return false;
            var view = intendedView ?? CreateResearchLab(selection, deferInitialization: true);
            try { researchWorkspace.AcceptHandoff(handoff, exact => view.AcceptResearchRequest(id, exact), cancellationToken); }
            catch { if (intendedView is null) view.Close(); throw; }
            view.Show();
            view.WindowState = WindowState.Normal;
            view.Activate();
            return true;
        }
        finally
        {
            researchHandoffWindow = null;
            researchProjectsWindow = null;
            selectingResearchProject = false;
        }
    }

    private void ShowResearchLab(ResearchLabSelection selection) => CreateResearchLab(selection).Show();

    private MlLabWindow CreateResearchLab(ResearchLabSelection selection, bool deferInitialization = false)
    {
        if (stopping || options is null || companion is null) throw new OperationCanceledException();
        layaService ??= CreateLayaService();
        if (mlPlannerHost is null && options.LocalModelDirectory is { } modelDirectory)
        {
            try { mlPlannerHost = new LocalModelHost(modelDirectory); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException) { }
        }
        Func<CancellationToken, Task<IChatModel>>? planner = mlPlannerHost is null ? null : token => mlPlannerHost.GetModelAsync(token);
        mlLabWindow = new MlLabWindow(selection.Service, planner, layaService, selection.Project, deferInitialization) { Owner = companion };
        var window = mlLabWindow;
        window.Closed += (_, _) => { if (ReferenceEquals(mlLabWindow, window)) mlLabWindow = null; };
        window.ChangeProjectRequested += (_, _) => _ = SelectResearchProjectAsync();
        return window;
    }
}
