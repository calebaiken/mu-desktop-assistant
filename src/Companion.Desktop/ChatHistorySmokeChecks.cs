using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Companion.Application.Conversation;
using Companion.Infrastructure.Conversation;

namespace Companion.Desktop;

/// <summary>Independent reviewer fixtures using real owned WPF windows and SQLite; no physical input claim.</summary>
internal static class ChatHistorySmokeChecks
{
    internal static async Task RunAsync(string dataDirectory)
    {
        var passed = new List<string>();
        string? failure = null;
        void Check(string name, bool condition)
        {
            if (!condition) throw new InvalidOperationException("Chat history smoke failed: " + name);
            passed.Add(name);
        }
        var fixture = Path.Combine(dataDirectory, "history-review-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        try
        {
            await CheckTemporaryAsync(Path.Combine(fixture, "temporary"), Check);
            await CheckSavedNavigationAsync(Path.Combine(fixture, "saved"), Check);
            await CheckLateLoadAsync(Path.Combine(fixture, "late-load"), Check);
            await CheckLateCompletionAsync(Path.Combine(fixture, "late-completion"), Check);
            await CheckSaveFailureRecoveryAsync(Path.Combine(fixture, "failed-save"), Check);
        }
        catch (Exception error)
        {
            failure = error.GetType().Name + ": " + error.Message;
            throw;
        }
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(dataDirectory, "chat-history-smoke.json"),
                JsonSerializer.Serialize(new { passed, count = passed.Count, failure }));
        }
    }

    private static ChatWindow Window(QueueModel model, IConversationStore store)
    {
        var window = new ChatWindow(_ => Task.FromResult<IChatModel>(model), store)
        { ShowActivated = false, ShowInTaskbar = false, Opacity = 0 };
        window.Show();
        return window;
    }

    private static async Task CheckTemporaryAsync(string directory, Action<string, bool> check)
    {
        var sentinel = "temporary-native-" + Guid.NewGuid().ToString("N");
        var store = new ControlledStore(new SqliteConversationStore(directory));
        var model = new QueueModel(sentinel);
        var window = Window(model, store);
        try
        {
            check("default-temporary-mode-visible", window.IsTemporary && window.PrivacyLabel.Text.Contains("Temporary", StringComparison.Ordinal));
            check("default-temporary-hides-saved-history", window.HistoryPane.Visibility == Visibility.Collapsed);
            window.MessageInput.Text = sentinel;
            await window.SendMessageAsync();
            check("temporary-chat-completes", window.CurrentConversation?.Turns.Single().Outcome == TurnOutcome.Completed);
            check("temporary-does-not-call-durable-store", store.Calls == 0);
            check("temporary-does-not-create-profile", !Directory.Exists(directory));
            window.Close();
            check("temporary-close-clears-context-and-snapshot", window.CurrentConversation is null && window.ConversationContext.Count == 0);
            check("temporary-close-does-not-call-durable-store", store.Calls == 0 && !Directory.Exists(directory));
        }
        finally { window.Close(); }
    }

    private static async Task CheckSavedNavigationAsync(string directory, Action<string, bool> check)
    {
        var model = new QueueModel("root answer", "original answer", "edited answer", "regenerated answer");
        var store = new SqliteConversationStore(directory);
        var window = Window(model, store);
        Guid conversationId;
        Guid originalLeaf;
        try
        {
            await window.SwitchHistoryModeAsync(false);
            check("saved-mode-discloses-local-unencrypted-storage", !window.IsTemporary && window.PrivacyLabel.Text.Contains("without encryption", StringComparison.Ordinal));
            check("saved-history-pane-visible", window.HistoryPane.Visibility == Visibility.Visible);
            window.MessageInput.Text = "root question";
            await window.SendMessageAsync();
            conversationId = window.CurrentConversation!.Id;
            var root = window.CurrentConversation.LeafTurnId!.Value;
            window.MessageInput.Text = "original followup";
            await window.SendMessageAsync();
            originalLeaf = window.CurrentConversation!.LeafTurnId!.Value;
            check("saved-completed-turns-retained", window.CurrentConversation.Turns.Count == 2 && window.CurrentConversation.Turns.All(turn => turn.Outcome == TurnOutcome.Completed));
            check("saved-followup-context-contains-completed-root", model.Requests[1].Select(message => message.Content).SequenceEqual(new[] { "root question", "root answer", "original followup" }));
            window.EditSelectedTurn();
            check("edit-loads-selected-prompt", window.MessageInput.Text == "original followup");
            window.MessageInput.Text = "edited followup";
            await window.SendMessageAsync();
            check("edit-forks-without-rewriting-original", window.CurrentConversation!.Turns.Count == 3 &&
                window.CurrentConversation.Turns.Single(turn => turn.Id == originalLeaf).Response == "original answer");
            check("edited-request-excludes-replaced-turn", model.Requests[2].Select(message => message.Content).SequenceEqual(new[] { "root question", "root answer", "edited followup" }));
            check("edited-display-shows-selected-branch-only", window.TranscriptText.Text.Contains("edited answer", StringComparison.Ordinal) &&
                !window.TranscriptText.Text.Contains("original answer", StringComparison.Ordinal));
            await window.SelectConversationBranchAsync(originalLeaf);
            check("branch-navigation-restores-original-response", window.TranscriptText.Text.Contains("original answer", StringComparison.Ordinal) &&
                !window.TranscriptText.Text.Contains("edited answer", StringComparison.Ordinal));
            window.RegenerateButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await UntilAsync(window, () => !window.IsGenerating && window.CurrentConversation?.Turns.Count == 4);
            check("regenerate-forks-original-prompt", model.Requests[3].Last().Content == "original followup" &&
                window.CurrentConversation!.Turns.Single(turn => turn.Id == originalLeaf).Response == "original answer");
            check("regenerated-branch-retains-root-parent", window.CurrentConversation!.Turns.Single(turn => turn.Id == window.CurrentConversation.LeafTurnId).ParentTurnId == root);
            window.TitleInput.Text = "Renamed history fixture";
            window.RenameButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await UntilAsync(window, () => window.ConversationHeading.Text == "Renamed history fixture" && window.HistoryPane.IsEnabled);
            check("rename-updates-window-and-list", window.HistoryList.Items.Cast<ConversationSummary>().Any(item => item.Id == conversationId && item.Title == "Renamed history fixture"));
            window.SearchInput.Text = "edited followup";
            Search(window);
            await UntilAsync(window, () => window.HistoryPane.IsEnabled);
            check("search-finds-unselected-branch-content", window.HistoryList.Items.Cast<ConversationSummary>().Single().Id == conversationId);
            window.SearchInput.Text = "absent-history-fixture";
            Search(window);
            await UntilAsync(window, () => window.HistoryPane.IsEnabled);
            check("search-miss-clears-list", window.HistoryList.Items.Count == 0);
            window.SearchInput.Clear();
            Search(window);
            await UntilAsync(window, () => window.HistoryPane.IsEnabled);
            window.Width = window.MinWidth;
            window.Height = window.MinHeight;
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
            var content = (FrameworkElement)window.Content;
            foreach (var control in new FrameworkElement[] { window.HistoryPane, window.BranchSelector, window.MessageInput, window.SendButton, window.TranscriptText })
            {
                var bounds = control.TransformToAncestor(content).TransformBounds(new Rect(control.RenderSize));
                check("saved-minimum-layout-" + control.Name, control.ActualHeight > 0 && control.ActualWidth > 0 &&
                    bounds.Left >= -1 && bounds.Top >= -1 && bounds.Right <= content.ActualWidth + 1 && bounds.Bottom <= content.ActualHeight + 1);
            }
            check("saved-minimum-readable-transcript", window.TranscriptText.ActualHeight >= 48);
        }
        finally { window.Close(); }

        var reopened = Window(new QueueModel(), new SqliteConversationStore(directory));
        try
        {
            await reopened.SwitchHistoryModeAsync(false);
            check("new-window-lists-saved-conversation", reopened.HistoryList.Items.Cast<ConversationSummary>().Any(item => item.Id == conversationId));
            await reopened.OpenConversationAsync(conversationId);
            check("reopen-restores-title-and-four-branches", reopened.CurrentConversation?.Title == "Renamed history fixture" && reopened.CurrentConversation.Turns.Count == 4);
            await reopened.SelectConversationBranchAsync(originalLeaf);
            check("reopen-can-select-original-branch", reopened.TranscriptText.Text.Contains("original answer", StringComparison.Ordinal));
            await reopened.SwitchHistoryModeAsync(true);
            check("switch-to-temporary-starts-empty-without-import", reopened.IsTemporary && reopened.CurrentConversation is null && reopened.TranscriptText.Text.Length == 0 && reopened.ConversationContext.Count == 0);
            check("switch-to-temporary-preserves-saved-history", (await store.LoadAsync(conversationId))?.Turns.Count == 4);
        }
        finally { reopened.Close(); }
    }

    private static async Task CheckLateLoadAsync(string directory, Action<string, bool> check)
    {
        var inner = new SqliteConversationStore(directory);
        var conversation = await inner.CreateAsync("delayed load fixture");
        await inner.AppendTurnAsync(conversation.Id, null, "saved question", "saved answer", TurnOutcome.Completed);
        var store = new ControlledStore(inner) { PauseLoad = true };
        var window = Window(new QueueModel(), store);
        try
        {
            await window.SwitchHistoryModeAsync(false);
            var open = window.OpenConversationAsync(conversation.Id);
            await store.Paused.Task.WaitAsync(TimeSpan.FromSeconds(5));
            check("pending-load-disables-history-controls", !window.HistoryPane.IsEnabled && !window.SavedModeButton.IsEnabled);
            var status = window.StatusLabel.Text;
            var text = window.TranscriptText.Text;
            window.Close();
            store.Release.TrySetResult();
            await open.WaitAsync(TimeSpan.FromSeconds(5));
            check("late-load-after-close-does-not-restore-private-view", window.CurrentConversation is null && window.TranscriptText.Text == text && window.StatusLabel.Text == status);
            check("late-load-after-close-cannot-enable-controls", !window.IsEnabled && !window.SendButton.IsEnabled);
        }
        finally { store.Release.TrySetResult(); window.Close(); }
    }

    private static async Task CheckLateCompletionAsync(string directory, Action<string, bool> check)
    {
        var inner = new SqliteConversationStore(directory);
        var store = new ControlledStore(inner) { PauseCompletion = true };
        var window = Window(new QueueModel("committed answer"), store);
        try
        {
            await window.SwitchHistoryModeAsync(false);
            window.MessageInput.Text = "commit before close";
            var send = window.SendMessageAsync();
            await store.Paused.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var id = window.CurrentConversation!.Id;
            var text = window.TranscriptText.Text;
            var status = window.StatusLabel.Text;
            window.Close();
            store.Release.TrySetResult();
            await send.WaitAsync(TimeSpan.FromSeconds(5));
            var saved = await inner.LoadAsync(id);
            check("close-after-database-commit-retains-completed-answer", saved?.Turns.Single().Outcome == TurnOutcome.Completed && saved.Turns.Single().Response == "committed answer");
            check("late-save-continuation-does-not-update-closed-window", window.TranscriptText.Text == text && window.StatusLabel.Text == status && window.CurrentConversation is null);
            check("late-save-continuation-keeps-controls-disabled", !window.IsEnabled && !window.SendButton.IsEnabled && !window.IsGenerating);
        }
        finally { store.Release.TrySetResult(); window.Close(); }
    }

    private static async Task CheckSaveFailureRecoveryAsync(string directory, Action<string, bool> check)
    {
        var inner = new SqliteConversationStore(directory);
        var store = new ControlledStore(inner) { FailCompletion = true };
        var sentinel = "unsaved-native-" + Guid.NewGuid().ToString("N");
        var laterSentinel = "temporary-followup-" + Guid.NewGuid().ToString("N");
        var model = new QueueModel(sentinel, laterSentinel);
        var window = Window(model, store);
        try
        {
            await window.SwitchHistoryModeAsync(false);
            window.MessageInput.Text = "saved question whose reply fails to save";
            await window.SendMessageAsync();
            var savedId = window.CurrentConversation!.Id;
            check("failed-save-keeps-output-visible", window.TranscriptText.Text.Contains(sentinel, StringComparison.Ordinal));
            check("failed-save-offers-explicit-recovery", window.SaveRetryButton.Visibility == Visibility.Visible &&
                window.ContinueTemporaryButton.Visibility == Visibility.Visible && window.ContinueTemporaryButton.IsEnabled);
            check("failed-save-cannot-silently-start-another-saved-turn", !window.SendButton.IsEnabled && !window.NewConversationButton.IsEnabled);
            var calls = store.Calls;
            await window.ContinueWithoutSavingAsync();
            check("continue-temporarily-keeps-unsaved-reply-in-memory", window.IsTemporary && window.TranscriptText.Text.Contains(sentinel, StringComparison.Ordinal) &&
                window.ConversationContext.Last().Content == sentinel);
            check("explicit-temporary-recovery-does-not-write-durable-store", store.Calls == calls);
            check("temporary-recovery-clearly-retains-earlier-saved-messages", window.StatusLabel.Text.Contains("Earlier saved messages remain", StringComparison.Ordinal));
            window.MessageInput.Text = laterSentinel;
            await window.SendMessageAsync();
            check("temporary-recovery-can-continue-with-unsaved-context", model.Requests[1].Any(message => message.Content == sentinel) && !window.IsGenerating);
            check("temporary-recovery-persistently-discloses-earlier-saved-history", window.PrivacyLabel.Text.Contains("Earlier saved messages remain", StringComparison.Ordinal));
            check("temporary-followup-never-reaches-saved-store", store.Calls == calls);
            window.Close();
            var saved = await inner.LoadAsync(savedId);
            check("failed-saved-completion-recovers-original-prompt-as-interrupted", saved?.Turns.Single().Outcome == TurnOutcome.Interrupted && saved.Turns.Single().Response.Length == 0);
            check("unsaved-reply-absent-from-database-and-sidecars", !ContainsSentinel(directory, sentinel));
            check("temporary-followup-absent-from-database-and-sidecars", !ContainsSentinel(directory, laterSentinel));
        }
        finally { window.Close(); }
    }

    private static bool ContainsSentinel(string directory, string sentinel)
    {
        var encodings = new[] { Encoding.UTF8, Encoding.Unicode, Encoding.BigEndianUnicode };
        foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
        {
            var bytes = File.ReadAllBytes(file);
            if (encodings.Any(encoding => bytes.AsSpan().IndexOf(encoding.GetBytes(sentinel)) >= 0)) return true;
        }
        return false;
    }

    private static void Search(ChatWindow window) => ((Panel)window.SearchInput.Parent).Children.OfType<Button>().Single()
        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static async Task UntilAsync(Window window, Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            timeout.Token.ThrowIfCancellationRequested();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle, timeout.Token);
        }
    }

    private sealed class QueueModel(params string[] responses) : IChatModel
    {
        private readonly Queue<string> responses = new(responses);
        internal List<IReadOnlyList<ChatMessage>> Requests { get; } = [];
        public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(IReadOnlyList<ChatMessage> messages,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(messages.ToArray());
            var answer = responses.Dequeue();
            yield return new ChatTextDelta(answer);
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ChatCompleted(ChatFinishReason.Stop);
        }
    }

    private sealed class ControlledStore(IConversationStore inner) : IConversationStore
    {
        internal int Calls { get; private set; }
        internal bool PauseLoad { get; init; }
        internal bool PauseCompletion { get; init; }
        internal bool FailCompletion { get; init; }
        internal TaskCompletionSource Paused { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IReadOnlyList<ConversationSummary>> ListAsync(string? search = null, CancellationToken cancellationToken = default)
        { Calls++; return inner.ListAsync(search, cancellationToken); }
        public Task<ConversationSnapshot> CreateAsync(string title, CancellationToken cancellationToken = default)
        { Calls++; return inner.CreateAsync(title, cancellationToken); }
        public async Task<ConversationSnapshot?> LoadAsync(Guid conversationId, CancellationToken cancellationToken = default)
        {
            Calls++;
            var loaded = await inner.LoadAsync(conversationId, cancellationToken);
            if (PauseLoad) { Paused.TrySetResult(); await Release.Task; }
            return loaded;
        }
        public Task<ConversationSnapshot> AppendTurnAsync(Guid conversationId, Guid? parentTurnId, string prompt, string response,
            TurnOutcome outcome, CancellationToken cancellationToken = default)
        { Calls++; return inner.AppendTurnAsync(conversationId, parentTurnId, prompt, response, outcome, cancellationToken); }
        public async Task<ConversationSnapshot> CompleteTurnAsync(Guid conversationId, Guid turnId, string response,
            TurnOutcome outcome, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (FailCompletion) throw new ConversationStoreException(ConversationStoreFailure.LimitReached);
            var completed = await inner.CompleteTurnAsync(conversationId, turnId, response, outcome, cancellationToken);
            if (PauseCompletion) { Paused.TrySetResult(); await Release.Task; }
            return completed;
        }
        public Task<ConversationSnapshot> SelectBranchAsync(Guid conversationId, Guid? leafTurnId, CancellationToken cancellationToken = default)
        { Calls++; return inner.SelectBranchAsync(conversationId, leafTurnId, cancellationToken); }
        public Task RenameAsync(Guid conversationId, string title, CancellationToken cancellationToken = default)
        { Calls++; return inner.RenameAsync(conversationId, title, cancellationToken); }
        public Task DeleteAsync(Guid conversationId, CancellationToken cancellationToken = default)
        { Calls++; return inner.DeleteAsync(conversationId, cancellationToken); }
    }
}
