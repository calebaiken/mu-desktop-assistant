using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Companion.Application.MachineLearning;

namespace Companion.Desktop;

/// <summary>Explicit, isolated, real local inference diagnostic. Uses only public synthetic requests.</summary>
internal static class LayaRuntimeSmokeChecks
{
    internal static async Task RunAsync(ILayaDecisionService service, string profile)
    {
        List<string> passed = [];
        List<object> observations = [];
        string? failure = null;
        var watch = Stopwatch.StartNew();
        var tracker = new TrackingAdvice(service);
        var lab = new NonexecutingLab(profile);
        var window = new MlLabWindow(lab, laya: tracker) { ShowActivated = false, ShowInTaskbar = false, Opacity = 0 };
        var ticks = 0;
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(50) };
        timer.Tick += (_, _) => ticks++;
        void Check(string name, bool condition)
        {
            if (!condition) throw new InvalidOperationException("LAYA runtime check failed: " + name);
            passed.Add(name);
        }
        try
        {
            window.Show(); window.LabTabs.SelectedItem = window.AssistantTab;
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
            Check("native-view-initialized-without-chat-model", !window.IsBusy && lab.Calls.SequenceEqual(new[] { "capabilities", "list" }) && window.AnalyzeTaskButton.IsEnabled && !window.ProposeButton.IsEnabled);
            window.AssistantRequest.Text = "Predict the continuous amplitude of a detector event from measured features.";
            var cancelled = window.AnalyzeTaskAsync();
            window.CancelPlanButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await cancelled.WaitAsync(TimeSpan.FromSeconds(8));
            Check("real-service-cancelled-admission-is-bounded", tracker.LastCancelled && tracker.LastAdvice is null && window.LayaAdviceText.Text.Contains("analysis stopped", StringComparison.Ordinal));
            Check("cancel-created-no-actions", lab.Calls.Count == 2 && !window.RunProposalButton.IsEnabled);

            foreach (var (name, prompt, expected) in new[]
            {
                ("numerical-target", "Predict the continuous amplitude of a detector event from measured numerical features.", "regression"),
                ("unusual-records", "Find unusual detector events and rank anomalous outliers. Preserve every original record.", "anomaly")
            })
            {
                window.AssistantRequest.Text = prompt;
                var before = ticks;
                var elapsed = Stopwatch.StartNew();
                timer.Start();
                await window.AnalyzeTaskAsync().WaitAsync(TimeSpan.FromSeconds(125));
                timer.Stop();
                if (tracker.LastAdvice is not { } advice)
                    throw new InvalidOperationException("Real local inference did not produce advice: " + (tracker.LastErrorCode ?? "unknown"));
                Check(name + "-uses-reviewed-checkpoint", advice.ModelRevision == "55cf4c4ebb4ebe31b2550e8bdf3bd21b99753851");
                Check(name + "-closed-finite-distribution", LayaAdvice.Categories.Contains(advice.Category) && advice.Probabilities.Count == 6 && advice.Probabilities.Keys.All(LayaAdvice.Categories.Contains) && advice.Probabilities.Values.All(value => double.IsFinite(value) && value is >= 0 and <= 1) && Math.Abs(advice.Probabilities.Values.Sum() - 1) <= .001);
                Check(name + "-actual-time-and-numerical-warning", advice.ElapsedSeconds is >= 0 and <= 120 && advice.Warnings.Any(item => item.Contains("not calibrated", StringComparison.Ordinal)));
                Check(name + "-native-view-shows-returned-result", window.LayaAdviceText.Text.Contains("Suggested category: " + advice.Category, StringComparison.Ordinal) && window.LayaAdviceText.Text.Contains(advice.ModelRevision, StringComparison.Ordinal));
                Check(name + "-dispatcher-remains-responsive", ticks > before);
                Check(name + "-no-action-authority", lab.Calls.Count == 2 && lab.Jobs.Count == 0 && !window.RunProposalButton.IsEnabled && window.AnalyzeTaskButton.IsEnabled);
                observations.Add(new { name, prompt, expectedCategory = expected, actualCategory = advice.Category, agreesWithFixture = advice.Category == expected,
                    advice.Confidence, advice.Probabilities, advice.Truncated, advice.ElapsedSeconds, wallSeconds = elapsed.Elapsed.TotalSeconds,
                    dispatcherTicks = ticks - before, advice.ModelRevision, advice.Warnings });
            }
            Check("actual-service-reused-after-cancel-and-inference", tracker.CallCount == 3);
        }
        catch (Exception error) { failure = error.GetType().Name + ": " + error.Message; throw; }
        finally
        {
            timer.Stop(); window.Close();
            Directory.CreateDirectory(profile);
            await File.WriteAllTextAsync(Path.Combine(profile, "laya-runtime-smoke.json"), JsonSerializer.Serialize(new
            {
                count = passed.Count, passed, failure, lastErrorCode = tracker.LastErrorCode, elapsedSeconds = watch.Elapsed.TotalSeconds, observations,
                limitations = "Two public synthetic prompts exercise real checkpoint inference through the production host and native view. They are not an accuracy benchmark or confidence calibration. Cancellation covers admission; held running-process ownership is tested separately with synthetic workers. No physical input certification."
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private sealed class TrackingAdvice(ILayaDecisionService actual) : ILayaDecisionService
    {
        internal LayaAdvice? LastAdvice { get; private set; }
        internal string? LastErrorCode { get; private set; }
        internal bool LastCancelled { get; private set; }
        internal int CallCount { get; private set; }
        public async Task<LayaAdvice> AdviseAsync(string prompt, CancellationToken cancellationToken = default)
        {
            CallCount++; LastAdvice = null; LastErrorCode = null; LastCancelled = false;
            try { return LastAdvice = await actual.AdviseAsync(prompt, cancellationToken); }
            catch (OperationCanceledException) { LastCancelled = true; throw; }
            catch (LayaException error) { LastErrorCode = error.Code; throw; }
        }
        public ValueTask DisposeAsync() => throw new InvalidOperationException("The application owns the real LAYA service.");
    }

    private sealed class NonexecutingLab(string directory) : IMlLabService
    {
        public string ProjectDirectory => directory;
        public IReadOnlyList<MlJobUpdate> Jobs => [];
        internal List<string> Calls { get; } = [];
        public event Action<MlJobUpdate>? JobChanged { add { } remove { } }
        public void Cancel(Guid jobId) => throw new InvalidOperationException("No ML jobs are authorized by this diagnostic.");
        public Task<MlResult> ExecuteAsync(MlAction action, MlLimits? limits = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); Calls.Add(action.Operation);
            var data = action switch
            {
                MlCapabilities => JsonSerializer.SerializeToElement(new { algorithms = Array.Empty<object>() }),
                MlList => JsonSerializer.SerializeToElement(new { experiments = Array.Empty<object>(), models = Array.Empty<object>(), warnings = Array.Empty<string>() }),
                _ => throw new InvalidOperationException("LAYA advice cannot execute an ML action.")
            };
            return Task.FromResult(new MlResult(Guid.NewGuid(), action.Operation, data));
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
