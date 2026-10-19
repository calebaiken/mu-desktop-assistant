using System.IO;
using System.Text.Json;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Controls;
using Companion.Application.Conversation;
using Companion.Application.MachineLearning;
using Companion.Infrastructure.MachineLearning;

namespace Companion.Desktop;

/// <summary>Native project controls plus production metadata/session/missing-runtime paths.
/// Proposal JSON is a declared fixture, not local-model inference qualification.</summary>
internal static class ResearchProjectSmokeChecks
{
    internal static async Task RunAsync(string directory)
    {
        var passed = new List<string>();
        string? failedCheck = null, failureType = null;
        var completed = false;
        void Check(bool condition, string name)
        {
            if (!condition) { failedCheck = name; throw new InvalidOperationException("Research project smoke checkpoint failed."); }
            passed.Add(name);
        }
        try { await RunCoreAsync(directory, Check); completed = true; }
        catch (Exception error) { failureType = error.GetType().Name; throw; }
        finally
        {
            // Public fixture only: fixed checkpoint labels/types; no paths, project
            // names, dataset values, exception messages, prompts or stack traces.
            await File.WriteAllTextAsync(Path.Combine(directory, "research-project-smoke.json"),
                JsonSerializer.Serialize(new { completed, failedCheck, failureType, passed }));
        }
    }

    private static async Task RunCoreAsync(string directory, Action<bool, string> check)
    {
        var profile = Path.Combine(directory, "public-research-project-smoke");
        var store = new JsonResearchProjectStore(profile);
        var picker = new ResearchProjectsWindow(store) { ShowActivated = false, ShowInTaskbar = false };
        Guid first;
        try
        {
            check(new WindowInteropHelper(picker).EnsureHandle() != IntPtr.Zero, "native picker HWND");
            await picker.InitializeAsync();
            check(!picker.OpenProject.IsEnabled && !picker.SelectProject(), "no implicit project selection");
            picker.ProjectName.Text = "Public project A";
            await picker.CreateProjectAsync();
            check(picker.OpenProject.IsEnabled && picker.SelectProject(), "explicit create and selection");
            first = picker.SelectedProjectId!.Value;
            var selected = await store.OpenAsync(first);
            check(picker.ProjectTarget.Text.Contains(selected.Directory, StringComparison.Ordinal), "trusted storage preview");
            picker.Measure(new Size(picker.MinWidth, picker.MinHeight));
            picker.Arrange(new Rect(0, 0, picker.MinWidth, picker.MinHeight));
            check(picker.OpenProject.ActualWidth > 0 && picker.CreateProject.ActualHeight >= 32, "minimum layout actions");
        }
        finally { picker.Close(); }
        var second = await store.CreateAsync("Public project B");
        await using var workspace = new ResearchLabWorkspace(store, project => new LocalMlLabService(
            Path.Combine(profile, "uninstalled-runtime", "python.exe"), Path.Combine(profile, "uninstalled-runtime", "worker.py"), project.Directory));
        var a = await workspace.OpenAsync(first);
        var oldView = new MlLabWindow(a.Service, _ => Task.FromResult<IChatModel>(new ListProposalFixture()), project: a.Project)
            { ShowActivated = false, ShowInTaskbar = false };
        try
        {
            check(new WindowInteropHelper(oldView).EnsureHandle() != IntPtr.Zero, "native project Lab HWND");
            oldView.DatasetPath.Text = Path.Combine(profile, "explicit-public-selection.csv");
            oldView.AssistantRequest.Text = "List saved work.";
            await oldView.ProposeAsync();
            check(oldView.RunProposalButton.IsEnabled && a.Service.Jobs.Count == 0, "proposal does not execute");
        }
        finally { oldView.BeginShutdown(); oldView.Close(); }
        var b = await workspace.OpenAsync(second.Id);
        var view = new MlLabWindow(b.Service, project: b.Project) { ShowActivated = false, ShowInTaskbar = false };
        try
        {
            check(new WindowInteropHelper(view).EnsureHandle() != IntPtr.Zero, "native second project HWND");
            check(view.ProjectLabel.Text.Contains(second.Id.ToString("N"), StringComparison.Ordinal), "selected project identity");
            check(view.DatasetPath.Text.Length == 0 && view.HistoryGrid.Items.Count == 0 && !view.RunProposalButton.IsEnabled, "fresh project has no prior selection history or plan");
            var result = await view.ExecuteActionAsync(new MlList());
            check(result is null && !view.IsBusy && view.TrainButton.IsEnabled, "missing runtime recovers controls");
            check(b.Service.Jobs.All(job => job.State == MlJobState.Failed), "failure is not completion");
            try { await a.Service.ExecuteAsync(new MlList()); throw new InvalidOperationException("Stale project admitted work."); }
            catch (MlException error) when (error.Code == "stale_project") { }
        }
        finally { view.Close(); }
        await CheckActiveReopenAsync(store, first, check);
    }

    private static async Task CheckActiveReopenAsync(IResearchProjectStore store, Guid id, Action<bool, string> check)
    {
        var project = await store.OpenAsync(id);
        var fixture = new ActiveViewFixture(project.Directory);
        await using var workspace = new ResearchLabWorkspace(store, _ => fixture);
        var selected = await workspace.OpenAsync(id);
        var work = selected.Service.ExecuteAsync(new MlList());
        var original = new MlLabWindow(selected.Service, project: project) { ShowActivated = false, ShowInTaskbar = false };
        new WindowInteropHelper(original).EnsureHandle();
        original.Close();
        var active = workspace.Current ?? throw new InvalidOperationException("Active project cannot reopen.");
        var reopened = new MlLabWindow(active.Service, project: active.Project) { ShowActivated = false, ShowInTaskbar = false };
        try
        {
            check(new WindowInteropHelper(reopened).EnsureHandle() != IntPtr.Zero, "reopened native active project HWND");
            check(workspace.IsBusy && reopened.JobsGrid.Items.Count == 1 && reopened.CancelJobButton.IsEnabled, "active job visible after view close");
            reopened.CancelJobButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            try { await work; throw new InvalidOperationException("Active fixture did not cancel."); }
            catch (OperationCanceledException) { }
            check(!workspace.IsBusy && ReferenceEquals(active.Service, selected.Service), "reopened view cancels the original owned job");
        }
        finally { reopened.Close(); fixture.Cancel(fixture.Id); }
    }

    // Declared view/admission fixture; real Windows process routing is tested
    // independently. It performs no computation, filesystem access or inference.
    private sealed class ActiveViewFixture(string directory) : IMlLabService
    {
        private readonly CancellationTokenSource cancellation = new();
        internal Guid Id { get; } = Guid.NewGuid();
        private MlJobState state = MlJobState.Running;
        public string ProjectDirectory => directory;
        public IReadOnlyList<MlJobUpdate> Jobs => [new(Id, "list", state, TimeSpan.Zero)];
        public event Action<MlJobUpdate>? JobChanged;
        public void Cancel(Guid jobId) { if (jobId == Id) cancellation.Cancel(); }
        public async Task<MlResult> ExecuteAsync(MlAction action, MlLimits? limits = null, CancellationToken cancellationToken = default)
        {
            try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellation.Token); throw new InvalidOperationException(); }
            finally { state = MlJobState.Cancelled; JobChanged?.Invoke(new(Id, "list", state, TimeSpan.Zero)); }
        }
        public ValueTask DisposeAsync() { cancellation.Cancel(); cancellation.Dispose(); return ValueTask.CompletedTask; }
    }

    private sealed class ListProposalFixture : IChatModel
    {
        public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(IReadOnlyList<ChatMessage> messages,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield(); cancellationToken.ThrowIfCancellationRequested();
            yield return new ChatTextDelta("{\"action\":\"list\",\"parameters\":{},\"reason\":\"Public proposal boundary fixture; no inference qualification.\"}");
            yield return new ChatCompleted(ChatFinishReason.Stop);
        }
    }
}
