using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Threading;
using Companion.Application.Conversation;
using Companion.Application.MachineLearning;

namespace Companion.Desktop;

/// <summary>Native view checks with controlled services; no inference, training or physical-input claim.</summary>
internal static class LayaUiSmokeChecks
{
    internal static async Task RunAsync(string dataDirectory)
    {
        List<string> passed = [];
        string? failure = null;
        var windows = new List<MlLabWindow>();
        void Check(string name, bool condition)
        {
            if (!condition) throw new InvalidOperationException("LAYA UI check failed: " + name);
            passed.Add(name);
        }
        try
        {
            var service = new FixtureLab(dataDirectory);
            var laya = new FixtureLaya();
            var window = Create(service, laya);
            windows.Add(window);
            await ReadyAsync(window, service);
            Check("analyze-available-without-conversation-model", window.AnalyzeTaskButton.IsEnabled && !window.ProposeButton.IsEnabled);
            Check("analyze-button-has-accessible-name", UIElementAutomationPeer.CreatePeerForElement(window.AnalyzeTaskButton)?.GetName() == "Analyze task with LAYA");
            Check("advice-has-accessible-name", UIElementAutomationPeer.CreatePeerForElement(window.LayaAdviceText)?.GetName() == "Local LAYA task category advice");
            Check("analyze-keyboard-target", window.AnalyzeTaskButton.Focusable && window.AnalyzeTaskButton.IsTabStop && window.AnalyzeTaskButton.MinHeight >= 32);
            Check("advice-is-read-only", window.LayaAdviceText.IsReadOnly);
            window.AssistantRequest.Text = "Predict a continuous physical measurement.";
            laya.Handler = (_, _) => Task.FromResult(Advice());
            await window.AnalyzeTaskAsync().WaitAsync(TimeSpan.FromSeconds(3));
            Check("analysis-uses-exact-current-request", laya.LastPrompt == window.AssistantRequest.Text);
            Check("analysis-renders-category", window.LayaAdviceText.Text.Contains("Suggested category: regression", StringComparison.Ordinal));
            Check("scores-disclose-uncalibrated-status", window.LayaAdviceText.Text.Contains("not calibrated confidence", StringComparison.Ordinal));
            Check("analysis-discloses-no-execution", window.LayaAdviceText.Text.Contains("No action ran.", StringComparison.Ordinal) && !window.RunProposalButton.IsEnabled);
            Check("analysis-keeps-explicit-controls", window.InspectButton.IsEnabled && window.TrainButton.IsEnabled && window.AnalyzeTaskButton.IsEnabled && !window.CancelPlanButton.IsEnabled);
            Check("analysis-created-no-ml-jobs", service.Calls.SequenceEqual(new[] { "capabilities", "list" }) && service.Jobs.Count == 0);
            await CheckLayoutAsync(window, Check);

            var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            laya.Handler = async (_, token) =>
            {
                try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
                catch (OperationCanceledException) { cancelled.TrySetResult(); throw; }
                return Advice();
            };
            var pending = window.AnalyzeTaskAsync();
            Check("pending-analysis-disables-duplicate-start", !window.AnalyzeTaskButton.IsEnabled && !window.ProposeButton.IsEnabled && window.CancelPlanButton.IsEnabled);
            var calls = laya.CallCount;
            await window.AnalyzeTaskAsync();
            Check("reentrant-analysis-does-not-call-provider", laya.CallCount == calls);
            window.CancelPlanButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Task.WhenAll(pending, cancelled.Task).WaitAsync(TimeSpan.FromSeconds(3));
            Check("stop-cancels-provider", cancelled.Task.IsCompletedSuccessfully);
            Check("cancel-restores-analysis-controls", window.AnalyzeTaskButton.IsEnabled && !window.CancelPlanButton.IsEnabled && !window.RunProposalButton.IsEnabled);
            Check("cancel-shows-no-success", window.LayaAdviceText.Text.Contains("analysis stopped", StringComparison.Ordinal));
            laya.Handler = (_, _) => Task.FromException<LayaAdvice>(new LayaException("fixture", "Synthetic local advice unavailable."));
            await window.AnalyzeTaskAsync().WaitAsync(TimeSpan.FromSeconds(3));
            Check("provider-error-is-readable", window.LayaAdviceText.Text.Contains("Synthetic local advice unavailable.", StringComparison.Ordinal));
            Check("provider-error-preserves-explicit-controls", window.AnalyzeTaskButton.IsEnabled && window.InspectButton.IsEnabled && window.TrainButton.IsEnabled && !window.CancelPlanButton.IsEnabled);
            Check("failures-created-no-ml-jobs", service.Calls.Count == 2 && service.Jobs.Count == 0);

            var planner = new FixtureChat();
            var plannedService = new FixtureLab(dataDirectory);
            var plannedLaya = new FixtureLaya();
            var planned = Create(plannedService, plannedLaya, _ => Task.FromResult<IChatModel>(planner));
            windows.Add(planned);
            await ReadyAsync(planned, plannedService);
            planned.DatasetPath.Text = Path.Combine(dataDirectory, "laya-fixture-selection.csv");
            planned.RowLimit.Text = "100";
            planned.AssistantRequest.Text = "Inspect the selected data before fitting anything.";
            plannedLaya.Handler = (_, _) => Task.FromResult(Advice());
            await planned.AnalyzeTaskAsync().WaitAsync(TimeSpan.FromSeconds(3));
            await planned.ProposeAsync().WaitAsync(TimeSpan.FromSeconds(3));
            Check("unchanged-eligible-advice-is-nonbinding-hint", planner.LastSystem.Contains("A separate local LAYA model suggested task category regression", StringComparison.Ordinal) && planner.LastSystem.Contains("fallible, nonbinding hint", StringComparison.Ordinal));
            Check("proposal-still-requires-explicit-run", planned.RunProposalButton.IsEnabled && planned.ProposalText.Text.Contains("No action has run.", StringComparison.Ordinal));
            Check("proposal-used-structured-port", planner.StructuredCalls == 1 && planner.PlainCalls == 0);
            Check("proposal-created-no-ml-jobs", plannedService.Calls.Count == 2 && plannedService.Jobs.Count == 0);
            planned.AssistantRequest.Text = "A different request: inspect the selected file.";
            await planned.ProposeAsync().WaitAsync(TimeSpan.FromSeconds(3));
            Check("changed-prompt-does-not-reuse-stale-advice", !planner.LastSystem.Contains("A separate local LAYA model suggested", StringComparison.Ordinal));
            foreach (var (name, advice) in new[]
            {
                ("low-score", Advice(confidence: .64)),
                ("other-category", Advice(category: "other")),
                ("truncated", Advice(truncated: true))
            })
            {
                plannedLaya.Handler = (_, _) => Task.FromResult(advice);
                await planned.AnalyzeTaskAsync().WaitAsync(TimeSpan.FromSeconds(3));
                Check(name + "-clears-previous-runnable-proposal", !planned.RunProposalButton.IsEnabled);
                await planned.ProposeAsync().WaitAsync(TimeSpan.FromSeconds(3));
                Check(name + "-excluded-from-planner-hint", !planner.LastSystem.Contains("A separate local LAYA model suggested", StringComparison.Ordinal));
            }
            Check("all-advice-and-proposals-remain-nonexecuting", plannedService.Calls.Count == 2 && plannedService.Jobs.Count == 0);

            var late = new TaskCompletionSource<LayaAdvice>(TaskCreationOptions.RunContinuationsAsynchronously);
            CancellationToken closeToken = default;
            plannedLaya.Handler = (_, token) => { closeToken = token; return late.Task; };
            var closing = planned.AnalyzeTaskAsync();
            var beforeClose = planned.LayaAdviceText.Text;
            planned.Close();
            Check("close-cancels-pending-advice", closeToken.IsCancellationRequested);
            late.SetResult(Advice());
            await closing.WaitAsync(TimeSpan.FromSeconds(3));
            Check("late-advice-does-not-write-closed-view", planned.LayaAdviceText.Text == beforeClose && !planned.RunProposalButton.IsEnabled);
            Check("view-close-does-not-dispose-app-services", !plannedService.Disposed && !plannedLaya.Disposed);
            Check("derived-lab-window-has-app-icon", window.Icon is not null);
        }
        catch (Exception error) { failure = error.GetType().Name + ": " + error.Message; throw; }
        finally
        {
            foreach (var window in windows) window.Close();
            Directory.CreateDirectory(dataDirectory);
            await File.WriteAllTextAsync(Path.Combine(dataDirectory, "laya-ui-smoke.json"), JsonSerializer.Serialize(new
            {
                count = passed.Count, passed, failure,
                limitations = "Native WPF controls and controlled service lifecycle only. No real LAYA/Qwen inference, ML jobs, physical keyboard input or screen-reader certification."
            }));
        }
    }

    private static MlLabWindow Create(FixtureLab service, FixtureLaya laya,
        Func<CancellationToken, Task<IChatModel>>? model = null)
    {
        var window = new MlLabWindow(service, model, laya) { ShowActivated = false, ShowInTaskbar = false, Opacity = 0 };
        window.Show();
        window.LabTabs.SelectedItem = window.AssistantTab;
        return window;
    }

    private static async Task ReadyAsync(MlLabWindow window, FixtureLab service)
    {
        await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
        if (window.IsBusy || service.Calls.Count != 2) throw new InvalidOperationException("Fixture Lab initialization did not complete.");
    }

    private static async Task CheckLayoutAsync(MlLabWindow window, Action<string, bool> check)
    {
        window.Width = window.MinWidth;
        window.Height = window.MinHeight;
        window.LayaAdviceExpander.IsExpanded = true;
        await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
        var panel = (FrameworkElement)window.AssistantTab.Content;
        foreach (var control in new FrameworkElement[] { window.AssistantRequest, window.AnalyzeTaskButton, window.ProposeButton, window.CancelPlanButton, window.RunProposalButton })
        {
            control.BringIntoView();
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
            var rectangle = control.TransformToAncestor(panel).TransformBounds(new Rect(new Point(), control.RenderSize));
            var onWindow = control.TransformToAncestor(window).TransformBounds(new Rect(new Point(), control.RenderSize));
            check("expanded-minimum-window-reaches-" + control.Name, control.IsVisible && rectangle.Width > 0 && rectangle.Height >= 30 &&
                rectangle.Left >= -1 && rectangle.Top >= -1 && rectangle.Right <= panel.ActualWidth + 1 && rectangle.Bottom <= panel.ActualHeight + 1 &&
                onWindow.Top >= 0 && onWindow.Bottom < window.ActualHeight - 15);
        }
    }

    private static LayaAdvice Advice(string category = "regression", double confidence = .9, bool truncated = false) =>
        new(category, confidence, LayaAdvice.Categories.ToDictionary(value => value, value => value == category ? confidence : (1 - confidence) / 5),
            truncated, "fixture-checkpoint", .01, []);

    private sealed class FixtureLaya : ILayaDecisionService
    {
        internal Func<string, CancellationToken, Task<LayaAdvice>> Handler { get; set; } = (_, _) => Task.FromResult(Advice());
        internal string LastPrompt { get; private set; } = "";
        internal int CallCount { get; private set; }
        internal bool Disposed { get; private set; }
        public Task<LayaAdvice> AdviseAsync(string prompt, CancellationToken cancellationToken = default)
        { LastPrompt = prompt; CallCount++; return Handler(prompt, cancellationToken); }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }

    private sealed class FixtureLab(string directory) : IMlLabService
    {
        public string ProjectDirectory => directory;
        public IReadOnlyList<MlJobUpdate> Jobs => [];
        internal List<string> Calls { get; } = [];
        internal bool Disposed { get; private set; }
        public event Action<MlJobUpdate>? JobChanged { add { } remove { } }
        public void Cancel(Guid jobId) => throw new InvalidOperationException("No ML job should exist in this fixture.");
        public Task<MlResult> ExecuteAsync(MlAction action, MlLimits? limits = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add(action.Operation);
            var data = action switch
            {
                MlCapabilities => JsonSerializer.SerializeToElement(new { algorithms = Array.Empty<object>() }),
                MlList => JsonSerializer.SerializeToElement(new { experiments = Array.Empty<object>(), models = Array.Empty<object>(), warnings = Array.Empty<string>() }),
                _ => throw new InvalidOperationException("Advice must not execute ML actions.")
            };
            return Task.FromResult(new MlResult(Guid.NewGuid(), action.Operation, data));
        }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }

    private sealed class FixtureChat : IStructuredChatModel
    {
        internal string LastSystem { get; private set; } = "";
        internal int PlainCalls { get; private set; }
        internal int StructuredCalls { get; private set; }
        public IAsyncEnumerable<ChatStreamEvent> StreamAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
        { PlainCalls++; return Reply(messages, cancellationToken); }
        public IAsyncEnumerable<ChatStreamEvent> StreamJsonAsync(IReadOnlyList<ChatMessage> messages, JsonElement schema, CancellationToken cancellationToken = default)
        { StructuredCalls++; return Reply(messages, cancellationToken); }
        private async IAsyncEnumerable<ChatStreamEvent> Reply(IReadOnlyList<ChatMessage> messages, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            LastSystem = messages.First(message => message.Role == ChatRole.System).Content;
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ChatTextDelta("{\"action\":\"inspect\",\"parameters\":{\"selection\":\"current\"},\"reason\":\"Synthetic typed plan.\"}");
            yield return new ChatCompleted(ChatFinishReason.Stop);
        }
    }
}
