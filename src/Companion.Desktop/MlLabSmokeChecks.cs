using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Companion.Application.MachineLearning;
using Companion.Application.Conversation;

namespace Companion.Desktop;

/// <summary>Reviewer-owned real WPF and local-worker diagnostics using synthetic data only.
/// Calls production controls directly; this is not physical mouse/keyboard or screen-reader qualification.</summary>
internal static class MlLabSmokeChecks
{
    internal static async Task RunAsync(IMlLabService service, string profile)
    {
        List<string> passed = [];
        string? failure = null;
        var elapsed = Stopwatch.StartNew();
        var fixtureDirectory = Path.Combine(profile, "ml-fixtures");
        Directory.CreateDirectory(fixtureDirectory);
        var csv = Path.Combine(fixtureDirectory, "deterministic-events.csv");
        var predictionCsv = Path.Combine(fixtureDirectory, "prediction.csv");
        var incompatibleCsv = Path.Combine(fixtureDirectory, "incompatible.csv");
        var data = new StringBuilder("event_id,x,z,target,event_type\n");
        var predictions = new StringBuilder("x,z\n");
        for (var index = 0; index < 80; index++)
        {
            var x = index / 10d;
            var z = (index % 7) - 3;
            data.AppendLine(FormattableString.Invariant($"{index},{x},{z},{2 * x - 3 * z + 1},{(index % 2 == 0 ? "A" : "B")}"));
            if (index < 12) predictions.AppendLine(FormattableString.Invariant($"{x},{z}"));
        }
        await File.WriteAllTextAsync(csv, data.ToString());
        await File.WriteAllTextAsync(predictionCsv, predictions.ToString());
        await File.WriteAllTextAsync(incompatibleCsv, "x\n1\n2\n3\n");
        var originalHash = SHA256.HashData(await File.ReadAllBytesAsync(csv));
        var window = new MlLabWindow(service) { ShowActivated = false, ShowInTaskbar = false, Opacity = 0 };
        void Check(string name, bool condition)
        {
            if (!condition) throw new InvalidOperationException("ML Lab smoke failed: " + name);
            passed.Add(name);
        }
        try
        {
            window.Show();
            await UntilAsync(window, () => !window.IsBusy && service.Jobs.Any(job => job.Operation == "list" && job.State == MlJobState.Completed));
            Check("real-native-window-created", new WindowInteropHelper(window).Handle != 0 && window.IsVisible);
            Check("real-capabilities-populate-models", window.AlgorithmSelector.Items.Count >= 8);
            Check("default-hyperparameters-are-valid-empty-json", window.Hyperparameters.Text == "{}");
            Check("optional-conversation-model-does-not-block-lab", !window.ProposeButton.IsEnabled && window.InspectButton.IsEnabled);
            Check("no-work-created-by-initial-capability-list", window.HistoryGrid.Items.Count == 0);
            await CheckLayoutAsync(window, Check);

            window.DatasetPath.Text = csv;
            window.RowLimit.Text = "100";
            var inspection = await window.ExecuteActionAsync(new MlInspect(new(csv, RowLimit: 100)));
            Check("real-inspection-completed", inspection is not null);
            Check("inspection-has-five-columns", window.StatisticsGrid.Items.Count == 5);
            Check("inspection-has-bounded-visible-preview", window.PreviewGrid.Items.Count is > 0 and <= 100);
            Check("identifier-not-automatically-selected", !window.SelectedFeatures.Contains("event_id"));
            var inspectedSourceBytes = await File.ReadAllBytesAsync(csv);
            Check("inspection-preserves-source", originalHash.SequenceEqual(SHA256.HashData(inspectedSourceBytes)));

            window.TaskSelector.SelectedItem = "regression";
            window.TargetSelector.SelectedItem = "target";
            window.AlgorithmSelector.SelectedItem = window.AlgorithmSelector.Items.Cast<object>().Single(item => item.ToString() == "Linear regression");
            foreach (var item in window.FeaturesGrid.Items)
            {
                var properties = TypeDescriptor.GetProperties(item);
                var name = properties["Name"]?.GetValue(item) as string;
                properties["Use"]?.SetValue(item, name is "x" or "z");
            }
            window.ScalingSelector.SelectedItem = "none";
            window.ExperimentName.Text = "Independent native regression";
            window.LabTabs.SelectedItem = window.ConfigureTab;
            var beats = new List<double>();
            var heartbeat = Stopwatch.StartNew();
            var timer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher) { Interval = TimeSpan.FromMilliseconds(50) };
            timer.Tick += (_, _) => beats.Add(heartbeat.Elapsed.TotalSeconds);
            var beforeTraining = service.Jobs.Count;
            timer.Start();
            window.TrainButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            try
            {
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                Check("train-button-enters-app-service", window.IsBusy || service.Jobs.Count > beforeTraining);
                await UntilAsync(window, () => !window.IsBusy && service.Jobs.Count > beforeTraining, 90);
            }
            finally { timer.Stop(); }
            Check("train-button-runs-real-worker", service.Jobs.Any(job => job.Operation == "train" && job.State == MlJobState.Completed));
            Check("ui-heartbeat-during-worker", beats.Count >= 2 && beats.Zip(beats.Skip(1), (a, b) => b - a).DefaultIfEmpty(0).Max() < 2);
            Check("real-metrics-present", window.MetricsGrid.Items.Count >= 4);
            Check("actual-plot-decodes", window.PlotSelector.Items.Count > 0 && window.PlotImage.Source is not null);
            Check("training-enables-controls-again", window.TrainButton.IsEnabled && !window.IsBusy);
            using var record = JsonDocument.Parse(window.ResultMetadata.Text);
            var experiment = record.RootElement.Clone();
            var experimentId = experiment.GetProperty("id").GetString()!;
            var modelId = experiment.GetProperty("modelId").GetString()!;
            Check("reproducibility-record-has-model-and-experiment-id", Guid.TryParse(experimentId, out _) && Guid.TryParse(modelId, out _));
            Check("trained-model-and-history-visible", window.HistoryGrid.Items.Count == 1 && window.ModelsGrid.Items.Count == 1);

            var prediction = await window.ExecuteActionAsync(new MlPredict(modelId, new(predictionCsv, RowLimit: 100)));
            Check("saved-model-reused-on-new-data", prediction is not null && window.PredictionGrid.Items.Count == 12);
            var bad = await window.ExecuteActionAsync(new MlPredict(modelId, new(incompatibleCsv, RowLimit: 100)));
            Check("schema-failure-is-visible-and-recoverable", bad is null && !window.IsBusy && window.PredictButton.IsEnabled);
            Check("failure-keeps-dataset-selection", window.DatasetPath.Text == csv);
            var exported = await window.ExecuteActionAsync(new MlExport(experimentId));
            Check("python-export-produces-real-artifact", exported is not null && File.Exists(exported.Data.GetProperty("path").GetString()));

            var ridge = await window.ExecuteActionAsync(new MlTrain(new(csv, RowLimit: 100), "regression", "ridge", ["x", "z"],
                "target", Scaling: "none", Name: "Independent native comparison"));
            Check("second-baseline-completes-on-same-cohort", ridge is not null);
            var ridgeId = ridge!.Data.GetProperty("id").GetString()!;
            var historical = await window.ExecuteActionAsync(new MlDescribe(experimentId));
            Check("historical-description-restores-exact-preprocessing-and-partitions", historical is not null &&
                historical.Data.GetProperty("preprocessing").GetProperty("scaling").GetString() == "none" &&
                historical.Data.GetProperty("split").GetProperty("trainRows").GetArrayLength() == 64 && window.PlotImage.Source is not null);
            var comparison = await window.ExecuteActionAsync(new MlCompare([experimentId, ridgeId]));
            Check("same-cohort-comparison-shows-two-real-models", comparison is not null && window.MetricsGrid.Items.Count == 2 &&
                comparison.Data.GetProperty("models")[0].GetProperty("modelBytes").GetInt64() > 0 && window.ResultWarnings.Text.Length > 0);

            var classification = await window.ExecuteActionAsync(new MlTrain(new(csv, RowLimit: 100), "classification", "logistic", ["x", "z"],
                "event_type", Split: "stratified", Folds: 3, Name: "Independent native classifier"));
            Check("stratified-classification-with-cv-completes", classification is not null);
            Check("classification-plot-visible", window.PlotImage.Source is not null && window.MetricsGrid.Items.Count >= 4);
            var incompatibleComparison = await window.ExecuteActionAsync(new MlCompare([experimentId, classification!.Data.GetProperty("id").GetString()!]));
            Check("different-task-comparison-rejected-with-ui-recovery", incompatibleComparison is null && !window.IsBusy && window.TrainButton.IsEnabled);
            var pca = await window.ExecuteActionAsync(new MlTrain(new(csv, RowLimit: 100), "reduction", "pca", ["x", "z"], Name: "Independent native PCA"));
            Check("pca-projection-completes-and-displays", pca is not null && window.PlotImage.Source is not null);
            var cluster = await window.ExecuteActionAsync(new MlTrain(new(csv, RowLimit: 100), "clustering", "dbscan", ["x", "z"], Name: "Independent native density"));
            Check("clustering-completes-with-statistical-caution", cluster is not null && window.ResultWarnings.Text.Length > 0);
            Check("multiple-experiments-remain-visible", window.HistoryGrid.Items.Count == 5);
            var trainedSourceBytes = await File.ReadAllBytesAsync(csv);
            Check("all-training-preserves-source", originalHash.SequenceEqual(SHA256.HashData(trainedSourceBytes)));
            await CheckCancellationAsync(service, Check);
            await CheckProposalLimitsAsync(service, csv, Check);

            var recordCount = window.HistoryGrid.Items.Count;
            window.Close();
            var reopened = new MlLabWindow(service) { ShowActivated = false, ShowInTaskbar = false, Opacity = 0 };
            window = reopened;
            reopened.Show();
            await UntilAsync(reopened, () => !reopened.IsBusy && reopened.HistoryGrid.Items.Count == recordCount);
            Check("closing-and-reopening-view-retains-persisted-work", reopened.HistoryGrid.Items.Count == recordCount);
            Check("reopened-window-uses-same-app-service", reopened.ProjectLabel.Text.Contains(service.ProjectDirectory, StringComparison.Ordinal));
            Check("no-live-jobs-remain-after-checks", service.Jobs.All(job => job.State is MlJobState.Completed or MlJobState.Cancelled or MlJobState.Failed));
        }
        catch (Exception error) { failure = error.GetType().Name + ": " + error.Message; throw; }
        finally
        {
            window.Close();
            await File.WriteAllTextAsync(Path.Combine(profile, "ml-lab-smoke.json"), JsonSerializer.Serialize(new
            {
                count = passed.Count, passed, failure, durationSeconds = elapsed.Elapsed.TotalSeconds,
                uiStatus = window.CurrentStatus,
                jobs = service.Jobs.Select(job => new { job.Id, job.Operation, state = job.State.ToString(), elapsedSeconds = job.Elapsed.TotalSeconds }).ToArray(),
                limits = new[] { "Synthetic data only; no physical input or screen-reader qualification.", "Window reopen checked; complete application restart and export-script execution use separate checks.", "Cancellation observes real owned child startup; it does not prove cancellation at a particular estimator fit instruction." }
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static async Task CheckLayoutAsync(MlLabWindow window, Action<string, bool> check)
    {
        window.Width = window.MinWidth;
        window.Height = window.MinHeight;
        foreach (TabItem tab in window.LabTabs.Items)
        {
            window.LabTabs.SelectedItem = tab;
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
            var rectangle = new Rect(tab.TranslatePoint(new Point(), window), tab.RenderSize);
            check("minimum-window-tab-reachable-" + tab.Name,
                rectangle.Width > 0 && rectangle.Height > 0 && rectangle.Left >= 0 && rectangle.Right <= window.ActualWidth && rectangle.Bottom <= window.ActualHeight);
        }
        foreach (var (tab, control, name) in new (TabItem, FrameworkElement, string)[]
        {
            (window.DataTab, window.InspectButton, "inspect"),
            (window.ConfigureTab, window.TrainButton, "train"),
            (window.ModelsTab, window.PredictButton, "predict"),
            (window.AssistantTab, window.RunProposalButton, "proposal")
        })
        {
            window.LabTabs.SelectedItem = tab;
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
            control.BringIntoView();
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
            var rectangle = new Rect(control.TranslatePoint(new Point(), window), control.RenderSize);
            check("minimum-window-action-reachable-" + name, control.IsVisible && rectangle.Height > 0 &&
                rectangle.Left >= 0 && rectangle.Right <= window.ActualWidth && rectangle.Top >= 0 && rectangle.Bottom < window.ActualHeight - 15);
        }
    }

    private static async Task CheckCancellationAsync(IMlLabService service, Action<string, bool> check)
    {
        using var active = new CancellationTokenSource();
        using var queued = new CancellationTokenSource();
        var running = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnUpdate(MlJobUpdate update) { if (update.State == MlJobState.Running) running.TrySetResult(); }
        service.JobChanged += OnUpdate;
        try
        {
            var first = service.ExecuteAsync(new MlCapabilities(), cancellationToken: active.Token);
            var second = service.ExecuteAsync(new MlList(), cancellationToken: queued.Token);
            await running.Task.WaitAsync(TimeSpan.FromSeconds(15));
            queued.Cancel(); active.Cancel();
            var stop = Stopwatch.StartNew();
            check("queued-job-cancels", await IsCancelledAsync(second));
            check("running-owned-worker-cancels", await IsCancelledAsync(first));
            check("cancel-acknowledges-within-five-seconds", stop.Elapsed < TimeSpan.FromSeconds(5));
            var result = await service.ExecuteAsync(new MlCapabilities()).WaitAsync(TimeSpan.FromSeconds(30));
            check("worker-service-reusable-after-cancel", result.Operation == "capabilities");
        }
        finally { service.JobChanged -= OnUpdate; }
    }

    private static async Task<bool> IsCancelledAsync(Task task)
    {
        try { await task.WaitAsync(TimeSpan.FromSeconds(5)); return false; }
        catch (OperationCanceledException) { return true; }
    }

    private static async Task CheckProposalLimitsAsync(IMlLabService service, string csv, Action<string, bool> check)
    {
        var model = new ProposalFixtureModel();
        var proposalWindow = new MlLabWindow(service, _ => Task.FromResult<IChatModel>(model))
            { ShowActivated = false, ShowInTaskbar = false, Opacity = 0 };
        try
        {
            var initialJobs = service.Jobs.Count;
            proposalWindow.Show();
            await UntilAsync(proposalWindow, () => !proposalWindow.IsBusy && service.Jobs.Count >= initialJobs + 2);
            proposalWindow.DatasetPath.Text = csv;
            proposalWindow.RowLimit.Text = "100";
            proposalWindow.AssistantRequest.Text = "Inspect this selection.";
            model.Output = JsonSerializer.Serialize(new
            {
                action = "inspect", parameters = new { path = csv, start = 0, rowLimit = 200 }, reason = "Synthetic over-limit proposal."
            });
            var beforePlan = service.Jobs.Count;
            await proposalWindow.ProposeAsync();
            check("proposal-cannot-raise-user-row-cap", !proposalWindow.RunProposalButton.IsEnabled && proposalWindow.RowLimit.Text == "100");
            proposalWindow.RunProposalButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await proposalWindow.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
            check("rejected-proposal-has-no-execution-authority", service.Jobs.Count == beforePlan && !proposalWindow.IsBusy);

            model.Output = JsonSerializer.Serialize(new
            {
                action = "inspect", parameters = new { path = csv, start = 0, rowLimit = 1 }, reason = "Synthetic single-record selection."
            });
            await proposalWindow.ProposeAsync();
            check("single-row-proposal-keeps-valid-larger-compute-cap", proposalWindow.RunProposalButton.IsEnabled &&
                proposalWindow.ProposalText.Text.Contains("100 rows", StringComparison.Ordinal) && service.Jobs.Count == beforePlan);
            proposalWindow.RunProposalButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await UntilAsync(proposalWindow, () => !proposalWindow.IsBusy && service.Jobs.Count > beforePlan);
            using var metadata = JsonDocument.Parse(proposalWindow.DatasetMetadata.Text);
            check("reviewed-one-row-proposal-executes-real-inspection", metadata.RootElement.GetProperty("selectedRows").GetInt32() == 1);
            var afterRun = service.Jobs.Count;
            proposalWindow.RunProposalButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await proposalWindow.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
            check("proposal-execution-is-consumed-once", !proposalWindow.RunProposalButton.IsEnabled && service.Jobs.Count == afterRun);
        }
        finally { proposalWindow.Close(); }
    }

    private sealed class ProposalFixtureModel : IChatModel
    {
        internal string Output { get; set; } = "";
        public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(IReadOnlyList<ChatMessage> messages,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ChatTextDelta(Output);
            yield return new ChatCompleted(ChatFinishReason.Stop);
        }
    }

    private static async Task UntilAsync(MlLabWindow window, Func<bool> condition, int seconds = 45)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(seconds)) throw new TimeoutException("ML native diagnostic condition did not settle.");
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            await Task.Delay(25);
        }
    }
}
