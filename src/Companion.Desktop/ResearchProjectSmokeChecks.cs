using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;
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
        var profile = Path.Combine(directory, "public-research-project-smoke");
        var store = new JsonResearchProjectStore(profile);
        var picker = new ResearchProjectsWindow(store) { ShowActivated = false, ShowInTaskbar = false };
        Guid first;
        try
        {
            Require(new WindowInteropHelper(picker).EnsureHandle() != IntPtr.Zero, "native picker HWND");
            await picker.InitializeAsync();
            Require(!picker.OpenProject.IsEnabled && !picker.SelectProject(), "no implicit project selection");
            picker.ProjectName.Text = "Public project A";
            await picker.CreateProjectAsync();
            Require(picker.OpenProject.IsEnabled && picker.SelectProject(), "explicit create and selection");
            first = picker.SelectedProjectId!.Value;
            var selected = await store.OpenAsync(first);
            Require(picker.ProjectTarget.Text.Contains(selected.Directory, StringComparison.Ordinal), "trusted storage preview");
            picker.Measure(new Size(picker.MinWidth, picker.MinHeight));
            picker.Arrange(new Rect(0, 0, picker.MinWidth, picker.MinHeight));
            Require(picker.OpenProject.ActualWidth > 0 && picker.CreateProject.ActualHeight >= 32, "minimum layout actions");
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
            Require(new WindowInteropHelper(oldView).EnsureHandle() != IntPtr.Zero, "native project Lab HWND");
            oldView.DatasetPath.Text = Path.Combine(profile, "explicit-public-selection.csv");
            oldView.AssistantRequest.Text = "List saved work.";
            await oldView.ProposeAsync();
            Require(oldView.RunProposalButton.IsEnabled && a.Service.Jobs.Count == 0, "proposal does not execute");
        }
        finally { oldView.BeginShutdown(); oldView.Close(); }
        var b = await workspace.OpenAsync(second.Id);
        var view = new MlLabWindow(b.Service, project: b.Project) { ShowActivated = false, ShowInTaskbar = false };
        try
        {
            Require(new WindowInteropHelper(view).EnsureHandle() != IntPtr.Zero, "native second project HWND");
            Require(view.ProjectLabel.Text.Contains(second.Id.ToString("N"), StringComparison.Ordinal), "selected project identity");
            Require(view.DatasetPath.Text.Length == 0 && view.HistoryGrid.Items.Count == 0 && !view.RunProposalButton.IsEnabled, "fresh project has no prior selection history or plan");
            var result = await view.ExecuteActionAsync(new MlList());
            Require(result is null && !view.IsBusy && view.TrainButton.IsEnabled, "missing runtime recovers controls");
            Require(b.Service.Jobs.All(job => job.State == MlJobState.Failed), "failure is not completion");
            try { await a.Service.ExecuteAsync(new MlList()); throw new InvalidOperationException("Stale project admitted work."); }
            catch (MlException error) when (error.Code == "stale_project") { }
        }
        finally { view.Close(); }
    }

    private static void Require(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("Research project smoke failed: " + name);
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
