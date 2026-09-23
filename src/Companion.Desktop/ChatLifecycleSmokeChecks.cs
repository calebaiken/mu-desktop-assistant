using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Companion.Application.Conversation;
using Companion.Core.Desktop;
using Companion.Infrastructure.Desktop;

namespace Companion.Desktop;

/// <summary>Controlled model continuations against real WPF windows; does not simulate physical input.</summary>
internal static class ChatLifecycleSmokeChecks
{
    internal static async Task RunAsync(string dataDirectory)
    {
        List<string> passed = [];
        string? failure = null;
        void Check(string name, bool condition)
        {
            if (!condition) throw new InvalidOperationException("Chat lifecycle smoke failed: " + name);
            passed.Add(name);
        }
        try
        {
            await CheckLateFactoryAsync(Check);
            await CheckLateStreamAsync(Check);
            await CheckConversationAsync(Check);
            await CheckLayoutAsync(Check);
        }
        catch (Exception error)
        {
            failure = error.GetType().Name + ": " + error.Message;
            throw;
        }
        finally
        {
            Directory.CreateDirectory(dataDirectory);
            await File.WriteAllTextAsync(Path.Combine(dataDirectory, "chat-lifecycle-smoke.json"),
                JsonSerializer.Serialize(new { passed, count = passed.Count, failure }));
        }
    }

    private static ChatWindow CreateWindow(Func<CancellationToken, Task<IChatModel>> factory)
    {
        var window = new ChatWindow(factory) { ShowActivated = false, ShowInTaskbar = false, Opacity = 0 };
        window.Show();
        return window;
    }

    private static async Task CheckLateFactoryAsync(Action<string, bool> check)
    {
        var ready = new TaskCompletionSource<IChatModel>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        CancellationToken receivedToken = default;
        var model = new FixtureModel();
        var window = CreateWindow(token => { calls++; receivedToken = token; return ready.Task; });
        List<bool> busy = [];
        window.GenerationChanged += (_, _) => busy.Add(window.IsGenerating);
        try
        {
            window.MessageInput.Text = "This text must stay in memory only.";
            var send = window.SendMessageAsync();
            await window.SendMessageAsync();
            check("factory-one-active-turn", calls == 1 && !send.IsCompleted && window.IsGenerating);
            check("loading-can-stop", window.StopButton.IsEnabled && !window.SendButton.IsEnabled && window.MessageInput.IsReadOnly);
            var statusBeforeClose = window.StatusLabel.Text;
            var transcriptBeforeClose = window.TranscriptText.Text;
            window.BeginShutdown();
            window.Close();
            check("close-cancels-factory-token", receivedToken.IsCancellationRequested);
            ready.SetResult(model);
            await send.WaitAsync(TimeSpan.FromSeconds(3));
            check("late-factory-does-not-start-model", model.Requests.Count == 0);
            check("late-factory-does-not-update-ui", window.StatusLabel.Text == statusBeforeClose && window.TranscriptText.Text == transcriptBeforeClose);
            check("late-factory-cannot-enable-controls", !window.IsEnabled && !window.SendButton.IsEnabled && !window.StopButton.IsEnabled);
            check("close-releases-generation-state", busy.SequenceEqual(new[] { true, false }) && !window.IsGenerating);
            check("close-clears-context", window.ConversationContext.Count == 0);
        }
        finally { window.Close(); }
    }

    private static async Task CheckLateStreamAsync(Action<string, bool> check)
    {
        var model = new FixtureModel();
        var window = CreateWindow(_ => Task.FromResult<IChatModel>(model));
        List<bool> busy = [];
        window.GenerationChanged += (_, _) => busy.Add(window.IsGenerating);
        try
        {
            window.MessageInput.Text = "Stop this turn.";
            var send = window.SendMessageAsync();
            model.Events.Writer.TryWrite(new ChatTextDelta("Partial reply"));
            await WaitUntilAsync(window, () => window.TranscriptText.Text.Contains("Partial reply", StringComparison.Ordinal));
            window.StopGeneration();
            check("stop-signals-model-and-disables-stop", model.Token.IsCancellationRequested && !window.StopButton.IsEnabled);
            // Deliberately ignore cancellation inside this fixture, then deliver a late event.
            model.Events.Writer.TryWrite(new ChatTextDelta("LATE CONTENT"));
            model.Events.Writer.TryComplete();
            await send.WaitAsync(TimeSpan.FromSeconds(3));
            check("cancelled-partial-is-labelled", window.TranscriptText.Text.Contains("Companion · stopped", StringComparison.Ordinal));
            check("late-delta-after-stop-is-ignored", !window.TranscriptText.Text.Contains("LATE CONTENT", StringComparison.Ordinal));
            check("cancelled-turn-is-not-context", window.ConversationContext.Count == 0);
            check("cancelled-turn-can-be-edited-and-retried", window.RetryButton.Visibility == Visibility.Visible && !window.MessageInput.IsReadOnly);
            check("stop-releases-generation-state", busy.SequenceEqual(new[] { true, false }) && !window.IsGenerating);
        }
        finally { window.Close(); }

        var lateModel = new FixtureModel();
        var closedWindow = CreateWindow(_ => Task.FromResult<IChatModel>(lateModel));
        try
        {
            closedWindow.MessageInput.Text = "Close during streaming.";
            var send = closedWindow.SendMessageAsync();
            lateModel.Events.Writer.TryWrite(new ChatTextDelta("Before close"));
            await WaitUntilAsync(closedWindow, () => closedWindow.TranscriptText.Text.Contains("Before close", StringComparison.Ordinal));
            var priorText = closedWindow.TranscriptText.Text;
            var priorStatus = closedWindow.StatusLabel.Text;
            closedWindow.Close();
            lateModel.Events.Writer.TryWrite(new ChatCompleted(ChatFinishReason.Stop));
            lateModel.Events.Writer.TryComplete();
            await send.WaitAsync(TimeSpan.FromSeconds(3));
            check("closed-stream-cancels-token", lateModel.Token.IsCancellationRequested);
            check("closed-stream-does-not-complete-ui", priorText == closedWindow.TranscriptText.Text && priorStatus == closedWindow.StatusLabel.Text);
            check("closed-stream-does-not-commit-history", closedWindow.ConversationContext.Count == 0 && !closedWindow.IsGenerating);
        }
        finally { closedWindow.Close(); }
    }

    private static async Task CheckConversationAsync(Action<string, bool> check)
    {
        var models = new Queue<FixtureModel>();
        var window = CreateWindow(_ => Task.FromResult<IChatModel>(models.Dequeue()));
        var literalReply = "<script>alert('plain text')</script> **not rendered**";
        try
        {
            var first = CompletedModel(literalReply);
            models.Enqueue(first);
            window.MessageInput.Text = "First message";
            await window.SendMessageAsync();
            check("completed-pair-enters-context", window.ConversationContext.Count == 2 && window.ConversationContext[0].Role == ChatRole.User && window.ConversationContext[1].Content == literalReply);
            check("reply-remains-literal-plain-text", window.TranscriptText.Text.Contains(literalReply, StringComparison.Ordinal) && window.TranscriptText.IsReadOnly);

            var failed = new FixtureModel();
            failed.Events.Writer.TryWrite(new ChatTextDelta("Interrupted fragment"));
            failed.Events.Writer.TryComplete(new ChatModelException(ChatFailure.Protocol));
            models.Enqueue(failed);
            window.MessageInput.Text = "Failed message";
            await window.SendMessageAsync();
            check("failed-turn-keeps-prior-context", window.ConversationContext.Count == 2 && window.ConversationContext[0].Content == "First message");
            check("failed-partial-has-incomplete-label", window.TranscriptText.Text.Contains("Companion · incomplete", StringComparison.Ordinal));
            check("next-request-receives-completed-history", failed.Requests.Single().Count == 3 && failed.Requests[0][1].Content == literalReply);

            var missingCompletion = new FixtureModel();
            missingCompletion.Events.Writer.TryWrite(new ChatTextDelta("No finish event"));
            missingCompletion.Events.Writer.TryComplete();
            models.Enqueue(missingCompletion);
            window.MessageInput.Text = "Missing completion";
            await window.SendMessageAsync();
            check("missing-completion-cannot-enter-context", window.ConversationContext.Count == 2);

            var oversized = CompletedModel(new string('x', 8193));
            models.Enqueue(oversized);
            window.MessageInput.Text = "Oversized output";
            await window.SendMessageAsync();
            check("oversized-output-is-bounded", window.ConversationContext.Count == 2 && !window.TranscriptText.Text.Contains(new string('x', 8193), StringComparison.Ordinal));

            for (var index = 0; index < 18; index++)
            {
                var bounded = CompletedModel(new string('\u754c', 1100));
                models.Enqueue(bounded);
                window.MessageInput.Text = "Bounded turn " + index + new string('\u754c', 900);
                await window.SendMessageAsync();
                var request = bounded.Requests.Single();
                check("request-bounds-" + index, request.Count <= 24 && request.Sum(message => Encoding.UTF8.GetByteCount(message.Content)) <= 24 * 1024);
            }
            check("history-stays-paired-and-bounded", window.ConversationContext.Count % 2 == 0 && window.ConversationContext.Count <= 24 && window.ConversationContext.Sum(message => Encoding.UTF8.GetByteCount(message.Content)) <= 24 * 1024);
            check("transcript-stays-bounded", window.TranscriptText.Text.Length <= 50 * 1024 && !window.TranscriptText.Text.Contains("First message", StringComparison.Ordinal));
            check("context-drops-oldest-completed-pairs", window.ConversationContext[0].Content.StartsWith("Bounded turn", StringComparison.Ordinal));
        }
        finally { window.Close(); }
    }

    private static FixtureModel CompletedModel(string output)
    {
        var model = new FixtureModel();
        model.Events.Writer.TryWrite(new ChatTextDelta(output));
        model.Events.Writer.TryWrite(new ChatCompleted(ChatFinishReason.Stop));
        model.Events.Writer.TryComplete();
        return model;
    }

    private static async Task CheckLayoutAsync(Action<string, bool> check)
    {
        var owner = new CompanionWindow { ShowActivated = false, ShowInTaskbar = false, Opacity = 0 };
        owner.Show();
        owner.Apply(new() { ReducedMotion = true });
        var ownerHandle = new WindowInteropHelper(owner).Handle;
        DesktopWindowPlacement.MoveBy(ownerHandle, int.MaxValue, int.MaxValue);
        var window = new ChatWindow(_ => Task.FromResult<IChatModel>(new FixtureModel()))
        {
            Owner = owner, ShowActivated = false, ShowInTaskbar = false, Opacity = 0
        };
        try
        {
            window.Show();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            var bounds = DesktopWindowPlacement.ReadBounds(new WindowInteropHelper(window).Handle);
            check("chat-owner-edge-placement-contained", WindowPlacement.Constrain(bounds.Window, bounds.WorkArea) == bounds.Window);
            window.Width = window.MinWidth;
            window.Height = window.MinHeight;
            window.RetryButton.Visibility = Visibility.Visible;
            window.StatusLabel.Text = "The model could not accept this conversation. Start a new conversation or shorten your message.";
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();
            var content = (FrameworkElement)window.Content;
            foreach (var element in new FrameworkElement[] { window.TranscriptText, window.MessageInput,
                window.SendButton, window.StopButton, window.NewConversationButton, window.RetryButton, window.StatusLabel })
            {
                var rectangle = element.TransformToAncestor(content).TransformBounds(new Rect(element.RenderSize));
                check("minimum-layout-" + element.Name, element.ActualWidth > 0 && element.ActualHeight > 0 &&
                    rectangle.Left >= -1 && rectangle.Top >= -1 && rectangle.Right <= content.ActualWidth + 1 && rectangle.Bottom <= content.ActualHeight + 1);
            }
            check("minimum-layout-keeps-readable-transcript", window.TranscriptText.ActualHeight >= 48);
        }
        finally { window.Close(); owner.Close(); }
    }

    private static async Task WaitUntilAsync(Window window, Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (!condition())
        {
            timeout.Token.ThrowIfCancellationRequested();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle, timeout.Token);
        }
    }

    private sealed class FixtureModel : IChatModel
    {
        internal Channel<ChatStreamEvent> Events { get; } = Channel.CreateBounded<ChatStreamEvent>(8);
        internal List<IReadOnlyList<ChatMessage>> Requests { get; } = [];
        internal CancellationToken Token { get; private set; }

        public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(IReadOnlyList<ChatMessage> messages,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Token = cancellationToken;
            Requests.Add(messages.ToArray());
            await foreach (var item in Events.Reader.ReadAllAsync()) yield return item;
        }
    }
}
