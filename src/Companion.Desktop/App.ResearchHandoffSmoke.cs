using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Companion.Application.Conversation;
using Companion.Application.MachineLearning;
using Companion.Infrastructure.MachineLearning;

namespace Companion.Desktop;

public partial class App
{
    // Runs only in the existing --smoke path. Real App routing and native windows;
    // public fixture text, unavailable runtime, and declared model ports (no inference claim).
    private Action<string>? recordHandoffSmokeFailure;

    private async Task RunResearchHandoffSmokeAsync(string directory)
    {
        var smokeWatch = Stopwatch.StartNew();
        var passed = new List<string>();
        string? failedCheck = null, failureType = null;
        var completed = false;
        void Receipt() => File.WriteAllText(Path.Combine(directory, "chat-project-handoff-smoke.json"),
            JsonSerializer.Serialize(new { completed, failedCheck, failureType, elapsedMilliseconds = smokeWatch.ElapsedMilliseconds, passed }));
        recordHandoffSmokeFailure = type => { failureType = type; Receipt(); };
        Receipt(); // Progress survives a hard smoke timeout; fixed public labels only.
        void Check(bool condition, string name)
        {
            if (!condition) { failedCheck = name; Receipt(); throw new InvalidOperationException("Research handoff smoke checkpoint failed."); }
            if (!passed.Contains(name)) passed.Add(name);
            Receipt();
        }
        var profile = Path.Combine(directory, "public-chat-handoff-smoke");
        ChatWindow? chat = null;
        ResearchProject? a = null, b = null;
        try
        {
            Check(companion is not null && new WindowInteropHelper(companion).EnsureHandle() != IntPtr.Zero, "native companion owner HWND");
            researchProjects = new JsonResearchProjectStore(profile);
            a = await researchProjects.CreateAsync("Public handoff A");
            b = await researchProjects.CreateAsync("Public handoff B");
            researchWorkspace = new(researchProjects, project => new LocalMlLabService(
                Path.Combine(profile, "missing-runtime", "python.exe"), Path.Combine(profile, "missing-runtime", "worker.py"), project.Directory));
            chat = new ChatWindow(_ => throw new InvalidOperationException("Chat model must not start during transfer."), new InMemoryConversationStore())
                { Owner = companion, ShowActivated = false, ShowInTaskbar = false, Opacity = 0 };
            chatWindow = chat;
            chat.ResearchHandoff = (request, token) => TransferChatRequestAsync(chat, request, token);
            const string exact = "  Inspect the explicitly selected public dataset.\r\nDo not train.  ";
            chat.Show();
            chat.MessageInput.Text = exact;
            await chat.SendToResearchAsync();
            Check(!chat.ResearchHandoffButton.IsEnabled && mlLabWindow is null && researchWorkspace.Current is null, "temporary chat refuses UI and direct transfer");
            await chat.SwitchHistoryModeAsync(false);
            chat.MessageInput.Text = exact;
            Check(chat.ResearchHandoffButton.IsEnabled, "saved draft enables explicit transfer");
            chat.Width = chat.MinWidth; chat.Height = chat.MinHeight;
            await Dispatcher.InvokeAsync(chat.UpdateLayout, DispatcherPriority.ApplicationIdle);
            Check(new FrameworkElement[] { chat.MessageInput, chat.ResearchHandoffButton, chat.SendButton, chat.TranscriptText }
                .All(control => Within(control, chat)), "minimum chat layout keeps draft transfer and transcript visible");
            await DriveAsync(a.Id, preview =>
            {
                Check(preview.RequestPreview.Text == exact && preview.DestinationPreview.Text.Contains(a.Id.ToString("N"), StringComparison.Ordinal), "real preview preserves exact request and destination");
                preview.Width = preview.MinWidth; preview.Height = preview.MinHeight; preview.UpdateLayout();
                Check(new FrameworkElement[] { preview.RequestPreview, preview.ConfirmTransferButton, preview.CancelTransferButton }.All(c => Within(c, preview)), "minimum transfer layout keeps review and controls visible");
                preview.ConfirmTransferButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            });
            var lab = mlLabWindow ?? throw new InvalidOperationException("Confirmed handoff did not open Lab.");
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Check(lab.AssistantRequest.Text == exact && lab.LabTabs.SelectedItem == lab.AssistantTab && !lab.RunProposalButton.IsEnabled, "real App confirmation populates request without runnable proposal");
            Check(researchWorkspace.Current?.Service.Jobs.Count == 0 && !Directory.Exists(a.Directory) && !Directory.Exists(b.Directory), "handoff and deferred Lab Loaded run zero workers and save no artifacts");
            Check(lab.DatasetPath.Text.Length == 0, "handoff never infers dataset from request");
            lab.DatasetPath.Text = Path.Combine(profile, "explicit-public-selection.csv");
            lab.DatasetKey.Text = "events"; lab.StartRow.Text = "17"; lab.RowLimit.Text = "100";
            chat.MessageInput.Text = "A second public request.";
            await DriveAsync(a.Id, preview => preview.ConfirmTransferButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            Check(ReferenceEquals(lab, mlLabWindow) && lab.DatasetKey.Text == "events" && lab.StartRow.Text == "17" && lab.RowLimit.Text == "100", "same project handoff retains only its explicit dataset snapshot");
            chat.MessageInput.Text = "Cancelled public request.";
            await DriveAsync(a.Id, preview => preview.CancelTransferButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            Check(lab.AssistantRequest.Text == "A second public request." && researchWorkspace.Current?.Service.Jobs.Count == 0, "cancel control leaves previous Lab request unchanged");
            await DriveAsync(a.Id, preview => { lab.Close(); preview.ConfirmTransferButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); });
            Check(mlLabWindow is null, "closed destination cannot receive late confirmation");
            await DriveAsync(b.Id, preview => preview.Close());
            Check(mlLabWindow is null && researchWorkspace.Current?.Project.Id == b.Id && !Directory.Exists(b.Directory), "dismissed transfer never borrows another project dataset");
            await DriveAsync(b.Id, preview =>
            {
                Check(true, "closing Chat from real preview started");
                chat.Close();
                Check(true, "closing Chat from real preview returned");
            });
            Check(mlLabWindow is null && researchWorkspace.Current?.Service.Jobs.Count == 0, "closing Chat during real preview cancels without delivery");
            chatWindow = null;
            chat = new ChatWindow(_ => throw new InvalidOperationException(), new InMemoryConversationStore())
                { Owner = companion, ShowActivated = false, ShowInTaskbar = false, Opacity = 0 };
            chatWindow = chat;
            chat.Show();
            await chat.SwitchHistoryModeAsync(false);
            chat.MessageInput.Text = "Pending public transfer.";
            var callback = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            chat.ResearchHandoff = async (_, token) => { callback.SetResult(true); await pending.Task; token.ThrowIfCancellationRequested(); return true; };
            var transfer = chat.SendToResearchAsync();
            await callback.Task;
            await chat.SendToResearchAsync(); // Duplicate must not call callback a second time.
            chat.Close(); pending.SetResult(true); await transfer;
            Check(!chat.ResearchHandoffButton.IsEnabled, "closing Chat cancels pending transfer and duplicate submission");
            await CheckProposalLifecycleAsync(profile, a, Check);
            completed = true;
        }
        catch (Exception error) { failureType = error.GetType().Name; Receipt(); throw; }
        finally
        {
            chat?.Close(); chatWindow = null;
            mlLabWindow?.Close(); mlLabWindow = null;
            if (researchWorkspace is not null) await researchWorkspace.DisposeAsync();
            researchWorkspace = null; researchProjects = null;
            Receipt();
            recordHandoffSmokeFailure = null;
        }

        async Task DriveAsync(Guid id, Action<ResearchHandoffWindow> finish)
        {
            Exception? failure = null;
            var stage = 0;
            var watch = Stopwatch.StartNew();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
            timer.Tick += (_, _) =>
            {
                try
                {
                    if (watch.Elapsed > TimeSpan.FromSeconds(3)) throw new TimeoutException();
                    if (stage == 0 && researchProjectsWindow is { IsLoaded: true } picker && picker.ProjectsList.Items.Count >= 2)
                    {
                        stage++;
                        picker.ProjectsList.SelectedIndex = id == a?.Id ? 0 : 1;
                        // Catalog display order can change. Select by trusted identity preview.
                        if (!picker.ProjectTarget.Text.Contains(id.ToString("N"), StringComparison.Ordinal))
                            picker.ProjectsList.SelectedIndex = picker.ProjectsList.SelectedIndex == 0 ? 1 : 0;
                        if (!picker.ProjectTarget.Text.Contains(id.ToString("N"), StringComparison.Ordinal)) throw new InvalidOperationException();
                        picker.OpenProject.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }
                    else if (stage == 1 && researchHandoffWindow is { IsLoaded: true } preview)
                    { stage++; finish(preview); }
                }
                catch (Exception error) { failure = error; timer.Stop(); researchProjectsWindow?.Close(); researchHandoffWindow?.Close(); }
            };
            timer.Start();
            try { await chat!.SendToResearchAsync(); }
            finally { timer.Stop(); }
            if (failure is not null) throw failure;
            Check(stage == 2, "explicit picker and preview reached");
        }
    }

    private static bool Within(FrameworkElement control, Window window)
    {
        var rect = new Rect(control.TranslatePoint(new Point(), window), control.RenderSize);
        return rect.Width > 0 && rect.Height > 0 && rect.Left >= 0 && rect.Top >= 0 && rect.Right <= window.ActualWidth && rect.Bottom <= window.ActualHeight;
    }

    private static async Task CheckProposalLifecycleAsync(string profile, ResearchProject project, Action<bool, string> check)
    {
        await using var service = new LocalMlLabService(Path.Combine(profile, "missing", "python.exe"), Path.Combine(profile, "missing", "worker.py"), project.Directory);
        var model = new HandoffProposalFixture();
        var view = new MlLabWindow(service, _ => Task.FromResult<IChatModel>(model), project: project, deferInitialization: true)
            { ShowActivated = false, ShowInTaskbar = false, Opacity = 0 };
        try
        {
            view.Show(); view.AcceptResearchRequest(project.Id, "Inspect the selected dataset.");
            await view.ProposeAsync();
            check(model.Calls == 0 && service.Jobs.Count == 0 && !view.RunProposalButton.IsEnabled, "missing dataset refuses proposal before loading a model");
            view.DatasetPath.Text = Path.Combine(profile, "explicit-public-selection.csv");
            view.DatasetKey.Text = "events"; view.StartRow.Text = "17"; view.RowLimit.Text = "100";
            await view.ProposeAsync();
            check(view.RunProposalButton.IsEnabled && service.Jobs.Count == 0 && view.ProposalText.Text.Contains("events", StringComparison.Ordinal), "captured dataset becomes typed proposal with zero execution");
            var json = view.ProposalText.Text;
            using (var parsed = JsonDocument.Parse(json[json.IndexOf('{')..(json.LastIndexOf('}') + 1)]))
            {
                var selected = parsed.RootElement.GetProperty("parameters");
                check(selected.GetProperty("path").GetString() == view.DatasetPath.Text && selected.GetProperty("key").GetString() == "events" &&
                    selected.GetProperty("start").GetInt32() == 17 && selected.GetProperty("rowLimit").GetInt32() == 100 &&
                    json.Contains("100 rows; 1,024 MiB worker memory; 2 CPU threads", StringComparison.Ordinal), "review displays exact captured path key range and limits");
            }
            foreach (var input in new[] { view.DatasetPath, view.DatasetKey, view.StartRow, view.RowLimit, view.MemoryLimit, view.ThreadLimit, view.AssistantRequest })
            {
                check(view.RunProposalButton.IsEnabled, "fresh reviewed proposal runnable before each mutation");
                var original = input.Text;
                input.Text += " ";
                check(!view.RunProposalButton.IsEnabled, "input edit invalidates reviewed plan");
                input.Text = original;
                await view.ProposeAsync();
            }
            view.AssistantMode.SelectedIndex = 1;
            check(!view.RunProposalButton.IsEnabled, "mode change invalidates reviewed plan");
            model.Delay = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var planning = view.ProposeAsync();
            view.DatasetKey.Text = "changed";
            model.Delay.SetResult(true); await planning;
            check(!view.RunProposalButton.IsEnabled && service.Jobs.Count == 0, "late noncooperative model cannot restore invalidated proposal");
            model.Delay = null;
            await view.ProposeAsync();
            view.RunProposalButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            // Explicit Run is the first production ML admission; missing runtime fails honestly.
            while (view.IsBusy) await Task.Delay(1);
            check(service.Jobs.Count == 1 && service.Jobs[0].State == MlJobState.Failed && !view.RunProposalButton.IsEnabled, "only Run admits worker and missing runtime reports failure");
            model.Delay = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var closingPlan = view.ProposeAsync();
            view.Close(); model.Delay.SetResult(true); await closingPlan;
            check(!view.RunProposalButton.IsEnabled && service.Jobs.Count == 1 && view.AssistantRequest.Text.Length == 0, "closed Lab rejects late planning callback and clears request");
        }
        finally { view.Close(); }
        var unavailable = new MlLabWindow(service, _ => Task.FromException<IChatModel>(new ChatModelException(ChatFailure.Unavailable)), project: project, deferInitialization: true);
        try
        {
            unavailable.AcceptResearchRequest(project.Id, "Inspect.");
            unavailable.DatasetPath.Text = Path.Combine(profile, "explicit-public-selection.csv");
            var before = service.Jobs.Count;
            await unavailable.ProposeAsync();
            check(!unavailable.RunProposalButton.IsEnabled && service.Jobs.Count == before, "missing model fails proposal without admitting ML work");
        }
        finally { unavailable.Close(); }
    }

    private sealed class HandoffProposalFixture : IChatModel
    {
        internal int Calls { get; private set; }
        internal TaskCompletionSource<bool>? Delay { get; set; }
        public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(IReadOnlyList<ChatMessage> messages, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Delay is { } pending) await pending.Task; // Deliberately ignores cancellation until the planner checks.
            yield return new ChatTextDelta("{\"action\":\"inspect\",\"parameters\":{\"selection\":\"current\"},\"reason\":\"Inspect only the captured selection.\"}");
            yield return new ChatCompleted(ChatFinishReason.Stop);
        }
    }
}
