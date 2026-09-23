using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Threading;
using Companion.Application.Conversation;
using Companion.Application.Voice;

namespace Companion.Desktop;

/// <summary>Controlled speech continuations against owned WPF windows; no audio, physical input or device claim.</summary>
internal static class SpeechUiSmokeChecks
{
    internal static async Task RunAsync(string dataDirectory)
    {
        List<string> passed = [];
        string? failure = null;
        Directory.CreateDirectory(dataDirectory);
        var timing = Stopwatch.StartNew();
        var runId = Guid.NewGuid();
        var stagesPath = Path.Combine(dataDirectory, "speech-ui-stages.jsonl");
        void Checkpoint(string kind, string name) => File.AppendAllText(stagesPath,
            JsonSerializer.Serialize(new { runId, kind, name, elapsedMilliseconds = timing.ElapsedMilliseconds,
                completedChecks = passed.Count }) + Environment.NewLine);
        async Task StageAsync(string name, Func<Action<string, bool>, Task> run)
        {
            Checkpoint("begin", name);
            await run(Check);
            Checkpoint("end", name);
        }
        void Check(string name, bool condition)
        {
            if (!condition) throw new InvalidOperationException("Speech UI smoke failed: " + name);
            passed.Add(name);
            // Persist only fixture identifiers/timings. A killed run still identifies its last completed assertion.
            Checkpoint("check", name);
        }
        try
        {
            await StageAsync("explicit-playback", CheckExplicitPlaybackAsync);
            await StageAsync("eligibility", CheckEligibilityAsync);
            await StageAsync("unicode", CheckUnicodeAsync);
            await StageAsync("enumeration-cancellation", CheckEnumerationCancellationAsync);
            await StageAsync("cancellation-matrix", CheckCancellationMatrixAsync);
            await StageAsync("closed-progress", CheckClosedProgressAsync);
            await StageAsync("failure-recovery", CheckFailureRecoveryAsync);
            await StageAsync("layout", CheckLayoutAsync);
        }
        catch (Exception error)
        {
            failure = error.GetType().Name + ": " + error.Message;
            throw;
        }
        finally
        {
            Directory.CreateDirectory(dataDirectory);
            await File.WriteAllTextAsync(Path.Combine(dataDirectory, "speech-ui-smoke.json"),
                JsonSerializer.Serialize(new { passed, count = passed.Count, failure,
                    evidence = "Owned native WPF properties and controlled fake-provider lifecycle; no audio or physical-input observation." }));
        }
    }

    private static ChatWindow CreateWindow(FixtureModel model, ISpeechOutput? speech,
        IConversationStore? savedStore = null)
    {
        var window = new ChatWindow(_ => Task.FromResult<IChatModel>(model), savedStore, speech)
        { ShowActivated = false, ShowInTaskbar = false, Opacity = 0 };
        window.Show();
        return window;
    }

    private static async Task ReplyAsync(ChatWindow window, FixtureModel model, string reply, string prompt = "Public speech fixture")
    {
        model.EnqueueCompleted(reply);
        window.MessageInput.Text = prompt;
        await window.SendMessageAsync().WaitAsync(TimeSpan.FromSeconds(3));
        await DrainAsync(window);
    }

    private static async Task CheckExplicitPlaybackAsync(Action<string, bool> check)
    {
        await using var speech = new FixtureSpeech();
        var model = new FixtureModel();
        var window = CreateWindow(model, speech);
        List<bool> states = [];
        window.SpeechChanged += (_, _) => states.Add(window.IsSpeaking);
        const string literal = "<speak>Do not fetch https://example.invalid/voice.</speak> **literal** $x^2$";
        try
        {
            check("unused-speech-does-not-enumerate-or-start", speech.Enumerations == 0 && speech.Operations.Count == 0);
            check("empty-chat-cannot-read", !window.ReadAloudButton.IsEnabled && !window.IsSpeechBusy && !window.IsSpeaking);
            await window.ReadReplyAloudAsync();
            check("empty-explicit-read-does-not-touch-provider", speech.Enumerations == 0 && speech.Operations.Count == 0);
            await ReplyAsync(window, model, literal);
            check("completed-reply-never-autoplays", speech.Enumerations == 0 && speech.Operations.Count == 0);
            check("completed-reply-enables-explicit-read", window.ReadAloudButton.IsEnabled && !window.IsSpeaking);
            var read = window.ReadReplyAloudAsync();
            await UntilAsync(window, () => speech.Operations.Count == 1);
            var first = speech.Operations[0];
            check("rich-reply-passed-once-as-literal-text", first.Request.Text == literal && speech.Enumerations == 1);
            check("default-voice-and-rate-wired", first.Request.VoiceId == FixtureSpeech.Voices[0].Id && first.Request.Rate == 0);
            check("provider-start-pending-is-busy-not-speaking", window.IsSpeechBusy && !window.IsSpeaking && states.Count == 0);
            check("active-request-exposes-stop-and-blocks-options", !window.ReadAloudButton.IsEnabled &&
                window.StopSpeechButton.Visibility == Visibility.Visible && window.StopSpeechButton.IsEnabled &&
                !window.SpeechVoiceSelector.IsEnabled && !window.SpeechRateSelector.IsEnabled);
            await window.ReadReplyAloudAsync();
            check("second-read-does-not-queue", speech.Operations.Count == 1);
            first.Report(SpeechOutputState.Speaking);
            first.Report(SpeechOutputState.Speaking);
            await DrainAsync(window);
            check("speaking-requires-provider-event-and-deduplicates", window.IsSpeaking && states.SequenceEqual(new[] { true }));
            check("speaking-status-is-visible", window.SpeechStatusLabel.Visibility == Visibility.Visible &&
                !string.IsNullOrWhiteSpace(window.SpeechStatusLabel.Text));
            window.StopSpeechButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check("stop-click-signals-provider-token", first.Token.IsCancellationRequested);
            check("stop-retains-cleanup-lease", window.IsSpeechBusy && !window.ReadAloudButton.IsEnabled && !read.IsCompleted);
            await window.ReadReplyAloudAsync();
            check("read-during-cleanup-does-not-queue", speech.Operations.Count == 1);
            first.Cancel();
            await read.WaitAsync(TimeSpan.FromSeconds(3));
            await DrainAsync(window);
            check("stopped-provider-releases-speaking-once", !window.IsSpeechBusy && !window.IsSpeaking && states.SequenceEqual(new[] { true, false }));
            check("stopped-reply-remains-readable-and-reusable", window.ReadAloudButton.IsEnabled &&
                window.TranscriptText.Text.Contains(literal, StringComparison.Ordinal) && window.CurrentConversation!.Turns.Single().Response == literal);

            window.SpeechVoiceSelector.SelectedItem = FixtureSpeech.Voices[1];
            window.SpeechRateSelector.SelectedValue = 5;
            window.ReadAloudButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await UntilAsync(window, () => speech.Operations.Count == 2);
            var second = speech.Operations[1];
            check("read-button-uses-selected-voice-and-rate", second.Request.VoiceId == FixtureSpeech.Voices[1].Id && second.Request.Rate == 5);
            second.Report(SpeechOutputState.Speaking);
            await DrainAsync(window);
            first.Report(SpeechOutputState.Stopped);
            await DrainAsync(window);
            check("old-stopped-event-cannot-release-new-speech", window.IsSpeaking && window.IsSpeechBusy);
            first.Report(SpeechOutputState.Speaking);
            await DrainAsync(window);
            check("old-speaking-event-cannot-duplicate-state", states.SequenceEqual(new[] { true, false, true }));
            second.Complete();
            await UntilAsync(window, () => !window.IsSpeechBusy);
            check("completion-releases-ui-and-keeps-reply", !window.IsSpeaking && window.ReadAloudButton.IsEnabled &&
                states.SequenceEqual(new[] { true, false, true, false }) && window.CurrentConversation!.Turns.Single().Response == literal);
            check("voice-list-is-session-cached", speech.Enumerations == 1);
        }
        finally { window.Close(); speech.ReleaseAll(); }
    }

    private static async Task CheckEligibilityAsync(Action<string, bool> check)
    {
        await using var speech = new FixtureSpeech();
        var model = new FixtureModel();
        var window = CreateWindow(model, speech);
        try
        {
            var events = model.EnqueuePending();
            events.Writer.TryWrite(new ChatTextDelta("Incomplete text must never be read."));
            window.MessageInput.Text = "Pending public fixture";
            var send = window.SendMessageAsync();
            await UntilAsync(window, () => window.TranscriptText.Text.Contains("Incomplete text", StringComparison.Ordinal));
            await window.ReadReplyAloudAsync();
            check("streaming-reply-is-ineligible", !window.ReadAloudButton.IsEnabled && speech.Operations.Count == 0 && speech.Enumerations == 0);
            window.StopGeneration();
            await send.WaitAsync(TimeSpan.FromSeconds(3));
            await window.ReadReplyAloudAsync();
            check("interrupted-reply-is-ineligible", !window.ReadAloudButton.IsEnabled && speech.Operations.Count == 0 &&
                window.CurrentConversation!.Turns.Single().Outcome == TurnOutcome.Interrupted);

            var failed = model.EnqueuePending();
            failed.Writer.TryWrite(new ChatTextDelta("Failed text must never be read."));
            failed.Writer.TryComplete(new ChatModelException(ChatFailure.Protocol));
            window.MessageInput.Text = "Failed public fixture";
            await window.SendMessageAsync();
            await window.ReadReplyAloudAsync();
            check("failed-reply-is-ineligible", !window.ReadAloudButton.IsEnabled && speech.Operations.Count == 0 &&
                window.CurrentConversation!.Turns.Last().Outcome == TurnOutcome.Failed);
        }
        finally { window.Close(); speech.ReleaseAll(); }

        var noProviderModel = new FixtureModel();
        var noProvider = CreateWindow(noProviderModel, null);
        try
        {
            await ReplyAsync(noProvider, noProviderModel, "Readable without a speech provider.");
            await noProvider.ReadReplyAloudAsync();
            check("absent-provider-preserves-text-only-chat", !noProvider.ReadAloudButton.IsEnabled && !noProvider.IsSpeechBusy &&
                noProvider.TranscriptText.Text.Contains("Readable without", StringComparison.Ordinal));
        }
        finally { noProvider.Close(); }
    }

    private static async Task CheckUnicodeAsync(Action<string, bool> check)
    {
        await using var speech = new FixtureSpeech();
        var model = new FixtureModel();
        var window = CreateWindow(model, speech);
        // The surrogate pair starts at offset 1,999, exactly across the speech limit.
        var source = new string('a', 1999) + "\U0001F642" + " tail";
        try
        {
            await ReplyAsync(window, model, source);
            var read = window.ReadReplyAloudAsync();
            await UntilAsync(window, () => speech.Operations.Count == 1);
            var operation = speech.Operations[0];
            check("speech-clips-before-split-surrogate", operation.Request.Text == new string('a', 1999) && operation.Request.Text.Length <= 2000);
            check("clip-is-a-valid-unicode-source-prefix", source.StartsWith(operation.Request.Text, StringComparison.Ordinal) &&
                Encoding.UTF8.GetString(new UTF8Encoding(false, true).GetBytes(operation.Request.Text)) == operation.Request.Text);
            check("clip-is-disclosed-without-truncating-chat", window.SpeechStatusLabel.Text.Contains("2,000", StringComparison.Ordinal) &&
                window.CurrentConversation!.Turns.Single().Response == source && window.TranscriptText.Text.Contains(source, StringComparison.Ordinal));
            operation.Complete();
            await read.WaitAsync(TimeSpan.FromSeconds(3));
        }
        finally { window.Close(); speech.ReleaseAll(); }
    }

    private static async Task CheckEnumerationCancellationAsync(Action<string, bool> check)
    {
        foreach (var close in new[] { false, true })
        {
            await using var speech = new FixtureSpeech { HoldEnumeration = true };
            var model = new FixtureModel();
            var window = CreateWindow(model, speech);
            var prefix = close ? "close-enumeration-" : "stop-enumeration-";
            try
            {
                await ReplyAsync(window, model, "Public enumeration cancellation fixture.");
                var read = window.ReadReplyAloudAsync();
                await UntilAsync(window, () => speech.Enumerations == 1);
                check(prefix + "busy-before-provider-start", window.IsSpeechBusy && !window.IsSpeaking && speech.Operations.Count == 0);
                if (close) window.Close(); else window.StopSpeech();
                check(prefix + "cancels-owned-token", speech.EnumerationToken.IsCancellationRequested);
                speech.ReleaseEnumeration();
                await read.WaitAsync(TimeSpan.FromSeconds(3));
                await DrainAsync(window);
                check(prefix + "late-voices-cannot-start-playback", speech.Operations.Count == 0 && !window.IsSpeaking && !window.IsSpeechBusy);
                check(prefix + "ends-with-correct-availability", close ? !window.IsEnabled && !window.ReadAloudButton.IsEnabled : window.ReadAloudButton.IsEnabled);
            }
            finally { window.Close(); speech.ReleaseAll(); }
        }
    }

    private static async Task CheckCancellationMatrixAsync(Action<string, bool> check)
    {
        (string Name, bool Saved, Func<ChatWindow, FixtureModel, Task> Change)[] changes =
        [
            ("new-chat", false, (window, _) => { window.NewConversationButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); return Task.CompletedTask; }),
            ("new-send", false, (window, model) => ReplyAsync(window, model, "Reply after speech was stopped.", "Next question")),
            ("history-mode", false, (window, _) => window.SwitchHistoryModeAsync(false)),
            ("selected-branch", false, (window, _) => window.SelectConversationBranchAsync(window.CurrentConversation!.Turns.First().Id)),
            ("edit-turn", false, (window, _) => { window.EditSelectedTurn(); return Task.CompletedTask; }),
            ("open-history", true, (window, _) => window.OpenConversationAsync(window.CurrentConversation!.Id))
        ];
        foreach (var change in changes)
        {
            await using var speech = new FixtureSpeech();
            var model = new FixtureModel();
            var window = CreateWindow(model, speech, new InMemoryConversationStore());
            try
            {
                if (change.Saved) await window.SwitchHistoryModeAsync(false);
                await ReplyAsync(window, model, "First public reply.", "First question");
                await ReplyAsync(window, model, "Second public reply.", "Second question");
                var read = window.ReadReplyAloudAsync();
                await UntilAsync(window, () => speech.Operations.Count == 1);
                var operation = speech.Operations[0];
                operation.Report(SpeechOutputState.Speaking);
                await DrainAsync(window);
                await change.Change(window, model);
                check(change.Name + "-cancels-speech-before-cleanup", operation.Token.IsCancellationRequested && window.IsSpeechBusy);
                var transcript = window.TranscriptText.Text;
                var leaf = window.CurrentConversation?.LeafTurnId;
                operation.Report(SpeechOutputState.Speaking);
                operation.Cancel();
                await read.WaitAsync(TimeSpan.FromSeconds(3));
                await DrainAsync(window);
                check(change.Name + "-late-speech-preserves-current-view", window.TranscriptText.Text == transcript &&
                    window.CurrentConversation?.LeafTurnId == leaf && !window.IsSpeaking && !window.IsSpeechBusy);
                check(change.Name + "-never-autoplays-replacement", speech.Operations.Count == 1);
            }
            finally { window.Close(); speech.ReleaseAll(); }
        }
    }

    private static async Task CheckClosedProgressAsync(Action<string, bool> check)
    {
        await using var speech = new FixtureSpeech();
        var model = new FixtureModel();
        var window = CreateWindow(model, speech);
        List<bool> states = [];
        window.SpeechChanged += (_, _) => states.Add(window.IsSpeaking);
        try
        {
            await ReplyAsync(window, model, "Public close fixture.");
            var read = window.ReadReplyAloudAsync();
            await UntilAsync(window, () => speech.Operations.Count == 1);
            var operation = speech.Operations[0];
            operation.Report(SpeechOutputState.Speaking);
            await DrainAsync(window);
            window.Close();
            check("close-signals-active-speech-and-releases-window-state", operation.Token.IsCancellationRequested && !window.IsSpeechBusy && !window.IsSpeaking);
            var statusAfterClose = window.SpeechStatusLabel.Text;
            operation.Report(SpeechOutputState.Speaking);
            operation.Complete();
            await read.WaitAsync(TimeSpan.FromSeconds(3));
            await DrainAsync(window);
            check("closed-window-ignores-late-progress-and-completion", !window.IsSpeaking && !window.IsSpeechBusy &&
                window.SpeechStatusLabel.Text == statusAfterClose && states.SequenceEqual(new[] { true, false }));
            check("closed-window-cannot-reenable-speech", !window.IsEnabled && !window.ReadAloudButton.IsEnabled && !window.StopSpeechButton.IsEnabled);
            check("chat-does-not-dispose-app-owned-provider", speech.DisposeCalls == 0);
        }
        finally { window.Close(); speech.ReleaseAll(); }
    }

    private static async Task CheckFailureRecoveryAsync(Action<string, bool> check)
    {
        foreach (var failure in new[] { SpeechFailure.Unavailable, SpeechFailure.Timeout, SpeechFailure.ProtocolError })
        {
            await using var speech = new FixtureSpeech();
            var model = new FixtureModel();
            var window = CreateWindow(model, speech);
            var prefix = "provider-" + failure.ToString().ToLowerInvariant();
            try
            {
                await ReplyAsync(window, model, "Text remains after a speech error.");
                var read = window.ReadReplyAloudAsync();
                await UntilAsync(window, () => speech.Operations.Count == 1);
                speech.Operations[0].Report(SpeechOutputState.Speaking);
                await DrainAsync(window);
                speech.Operations[0].Fail(failure);
                await read.WaitAsync(TimeSpan.FromSeconds(3));
                await DrainAsync(window);
                check(prefix + "-clears-busy-and-preserves-reply", !window.IsSpeaking && !window.IsSpeechBusy && window.ReadAloudButton.IsEnabled &&
                    window.CurrentConversation!.Turns.Single().Response == "Text remains after a speech error.");
                check(prefix + "-shows-readable-fallback", window.SpeechStatusLabel.Visibility == Visibility.Visible &&
                    !string.IsNullOrWhiteSpace(window.SpeechStatusLabel.Text) && !window.SpeechStatusLabel.Text.Contains(nameof(SpeechException), StringComparison.Ordinal));
                var retry = window.ReadReplyAloudAsync();
                await UntilAsync(window, () => speech.Operations.Count == 2);
                speech.Operations[1].Complete();
                await retry.WaitAsync(TimeSpan.FromSeconds(3));
                check(prefix + "-permits-explicit-retry", !window.IsSpeechBusy && speech.Operations.Count == 2);
            }
            finally { window.Close(); speech.ReleaseAll(); }
        }

        await using var noVoices = new FixtureSpeech { AvailableVoices = [] };
        var noVoicesModel = new FixtureModel();
        var noVoicesWindow = CreateWindow(noVoicesModel, noVoices);
        try
        {
            await ReplyAsync(noVoicesWindow, noVoicesModel, "Text remains without an installed voice.");
            await noVoicesWindow.ReadReplyAloudAsync();
            check("empty-voice-list-never-starts-provider", noVoices.Enumerations == 1 && noVoices.Operations.Count == 0 && !noVoicesWindow.IsSpeechBusy);
            check("empty-voice-list-has-readable-text-fallback", !noVoicesWindow.IsSpeaking && !string.IsNullOrWhiteSpace(noVoicesWindow.SpeechStatusLabel.Text) &&
                noVoicesWindow.TranscriptText.Text.Contains("Text remains without", StringComparison.Ordinal));
        }
        finally { noVoicesWindow.Close(); noVoices.ReleaseAll(); }
    }

    private static async Task CheckLayoutAsync(Action<string, bool> check)
    {
        await using var speech = new FixtureSpeech();
        var model = new FixtureModel();
        var window = CreateWindow(model, speech);
        try
        {
            await ReplyAsync(window, model, "Public layout fixture.");
            window.Width = window.MinWidth;
            window.Height = window.MinHeight;
            window.SpeechOptions.IsExpanded = true;
            await UntilAsync(window, () => speech.Enumerations == 1 && !window.IsSpeechBusy);
            check("opening-options-only-enumerates-never-speaks", speech.Operations.Count == 0);
            check("session-rate-options-have-frozen-bounds", window.SpeechRateSelector.Items.Count == 11 &&
                window.SpeechRateSelector.SelectedValue is 0);
            window.SpeechRateSelector.SelectedValue = -5;
            check("session-rate-selector-accepts-negative-bound", window.SpeechRateSelector.SelectedValue is -5);
            var read = window.ReadReplyAloudAsync();
            await UntilAsync(window, () => speech.Operations.Count == 1);
            check("negative-rate-bound-reaches-provider", speech.Operations[0].Request.Rate == -5);
            speech.Operations[0].Report(SpeechOutputState.Speaking);
            await DrainAsync(window);
            window.UpdateLayout();
            var content = (FrameworkElement)window.Content;
            foreach (var control in new FrameworkElement[] { window.ReadAloudButton, window.StopSpeechButton,
                window.SpeechVoiceSelector, window.SpeechRateSelector, window.SpeechStatusLabel })
            {
                var bounds = control.TransformToAncestor(content).TransformBounds(new Rect(control.RenderSize));
                check("minimum-speech-layout-" + control.Name, control.ActualWidth > 0 && control.ActualHeight > 0 &&
                    bounds.Left >= -1 && bounds.Top >= -1 && bounds.Right <= content.ActualWidth + 1 && bounds.Bottom <= content.ActualHeight + 1);
            }
            check("expanded-speech-options-retain-readable-transcript", window.TranscriptText.ActualHeight >= 48);
            foreach (var control in new UIElement[] { window.ReadAloudButton, window.StopSpeechButton,
                window.SpeechVoiceSelector, window.SpeechRateSelector })
            {
                var peer = UIElementAutomationPeer.CreatePeerForElement(control);
                check("speech-control-has-accessible-name-" + ((FrameworkElement)control).Name,
                    peer is not null && !string.IsNullOrWhiteSpace(peer.GetName()));
            }
            speech.Operations[0].Complete();
            await read.WaitAsync(TimeSpan.FromSeconds(3));
        }
        finally { window.Close(); speech.ReleaseAll(); }
    }

    private static Task DrainAsync(ChatWindow window) => window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Task;

    private static async Task UntilAsync(ChatWindow window, Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline) throw new TimeoutException("A controlled speech UI continuation did not complete.");
            await Task.Delay(10);
            await DrainAsync(window);
        }
    }

    private sealed class FixtureModel : IChatModel
    {
        private readonly Queue<Channel<ChatStreamEvent>> responses = [];
        internal Channel<ChatStreamEvent> EnqueuePending()
        {
            var events = Channel.CreateUnbounded<ChatStreamEvent>();
            responses.Enqueue(events);
            return events;
        }
        internal void EnqueueCompleted(string text)
        {
            var events = EnqueuePending();
            events.Writer.TryWrite(new ChatTextDelta(text));
            events.Writer.TryWrite(new ChatCompleted(ChatFinishReason.Stop));
            events.Writer.TryComplete();
        }
        public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(IReadOnlyList<ChatMessage> messages,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in responses.Dequeue().Reader.ReadAllAsync(cancellationToken)) yield return item;
        }
    }

    private sealed class FixtureSpeech : ISpeechOutput
    {
        internal static IReadOnlyList<SpeechVoice> Voices { get; } =
            [new("fixture-a", "Fixture voice A", "en-US"), new("fixture-b", "Fixture voice B", "en-GB")];
        private readonly TaskCompletionSource<IReadOnlyList<SpeechVoice>> enumeration = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal IReadOnlyList<SpeechVoice> AvailableVoices { get; init; } = Voices;
        internal bool HoldEnumeration { get; init; }
        internal int Enumerations { get; private set; }
        internal int DisposeCalls { get; private set; }
        internal CancellationToken EnumerationToken { get; private set; }
        internal List<SpeechOperation> Operations { get; } = [];

        public Task<IReadOnlyList<SpeechVoice>> GetVoicesAsync(CancellationToken cancellationToken = default)
        {
            Enumerations++;
            EnumerationToken = cancellationToken;
            return HoldEnumeration ? enumeration.Task : Task.FromResult(AvailableVoices);
        }
        public Task SpeakAsync(SpeechRequest request, IProgress<SpeechOutputState>? progress, CancellationToken cancellationToken = default)
        {
            var operation = new SpeechOperation(request, progress, cancellationToken);
            Operations.Add(operation);
            // Deliberately do not complete on cancellation: UI must retain its cleanup lease.
            return operation.Completion.Task;
        }
        internal void ReleaseEnumeration() => enumeration.TrySetResult(AvailableVoices);
        internal void ReleaseAll()
        {
            ReleaseEnumeration();
            foreach (var operation in Operations) operation.Completion.TrySetResult();
        }
        public ValueTask DisposeAsync()
        {
            DisposeCalls++;
            ReleaseAll();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SpeechOperation(SpeechRequest request, IProgress<SpeechOutputState>? progress, CancellationToken token)
    {
        internal SpeechRequest Request { get; } = request;
        internal CancellationToken Token { get; } = token;
        internal TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal void Report(SpeechOutputState state) => progress?.Report(state);
        internal void Complete() { Report(SpeechOutputState.Stopped); Completion.TrySetResult(); }
        internal void Cancel() { Report(SpeechOutputState.Stopped); Completion.TrySetCanceled(Token); }
        internal void Fail(SpeechFailure failure) => Completion.TrySetException(new SpeechException(failure));
    }
}
