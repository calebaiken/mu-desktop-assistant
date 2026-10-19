using System.Windows;
using System.Windows.Controls;
using Companion.Application.Conversation;

namespace Companion.Desktop;

public partial class ChatWindow
{
    private IConversationStore? savedConversations;
    private IConversationStore sessionStore = new InMemoryConversationStore();
    private ConversationSnapshot? conversation;
    private readonly CancellationTokenSource historyLifetime = new();
    private Guid? currentTurnId;
    private Guid? forkParentId;
    private bool forking;
    private bool historyBusy;
    private bool bindingHistory;
    private bool temporaryFromSaved;
    private PendingCompletion? pendingCompletion;
    internal bool IsTemporary { get; private set; } = true;
    internal ConversationSnapshot? CurrentConversation => conversation;
    private bool HasPendingSave => pendingCompletion is not null;
    private bool CanSend => !stopping && researchTransfer is null && activeTurn is null && !historyBusy && !HasPendingSave;

    private void InitializeHistory(IConversationStore? savedStore)
    {
        savedConversations = savedStore;
        SavedModeButton.IsEnabled = savedStore is not null;
        UpdateHistoryPresentation();
        UpdateResearchControls();
    }

    private async Task<IReadOnlyList<ChatMessage>> PrepareHistoryTurnAsync(string prompt, CancellationToken token)
    {
        var store = sessionStore;
        var current = conversation ?? await store.CreateAsync(ConversationHistory.TitleFromPrompt(prompt), token);
        if (stopping) throw new OperationCanceledException(token);
        var parent = forking ? forkParentId : current.LeafTurnId;
        var branch = new ConversationSnapshot(current.Id, current.Title, current.UpdatedAt, parent, current.Turns);
        var context = ConversationHistory.BuildContext(branch, prompt);
        var started = await store.AppendTurnAsync(current.Id, parent, prompt, string.Empty, TurnOutcome.InProgress, token);
        if (stopping) throw new OperationCanceledException(token);
        conversation = started;
        currentTurnId = started.LeafTurnId;
        forking = false;
        forkParentId = null;
        UpdateHistoryPresentation();
        return context;
    }

    private async Task<bool> CompleteHistoryTurnAsync(string response, TurnOutcome outcome)
    {
        if (currentTurnId is not { } turn || conversation is not { } current || stopping) return !stopping;
        var completion = new PendingCompletion(current.Id, turn, response, outcome);
        try
        {
            var updated = await sessionStore.CompleteTurnAsync(current.Id, turn, response, outcome, historyLifetime.Token);
            if (stopping) return false;
            conversation = updated;
            currentTurnId = null;
            pendingCompletion = null;
            UpdateHistoryPresentation();
            // A search/list failure does not undo a successfully committed turn.
            if (!IsTemporary)
            {
                try { await RefreshHistoryListAsync(); }
                catch (ConversationStoreException) { StatusLabel.Text = "Reply saved. The conversation list could not refresh; try Search again."; }
            }
            return !stopping;
        }
        catch (OperationCanceledException) when (stopping) { return false; }
        catch (Exception error) when (error is ConversationStoreException or System.IO.IOException or UnauthorizedAccessException)
        {
            if (stopping) return false;
            pendingCompletion = completion;
            SaveRetryButton.Visibility = Visibility.Visible;
            ContinueTemporaryButton.Visibility = Visibility.Visible;
            StatusLabel.Text = "This reply is still here, but could not be saved. Choose Retry saving before continuing.";
            return false;
        }
    }

    private async void SaveRetry_Click(object sender, RoutedEventArgs e)
    {
        if (stopping || activeTurn is not null || historyBusy || pendingCompletion is not { } pending) return;
        historyBusy = true;
        SetHistoryInteraction(false);
        SaveRetryButton.IsEnabled = false;
        try
        {
            if (await CompleteHistoryTurnAsync(pending.Response, pending.Outcome) && !stopping)
            {
                RestoreConversationView();
                StatusLabel.Text = IsTemporary ? "Ready. Temporary conversation." : "Reply saved. You can continue.";
            }
        }
        finally
        {
            historyBusy = false;
            if (!stopping) { SetHistoryInteraction(true); SaveRetryButton.IsEnabled = true; }
        }
    }

    internal async Task SwitchHistoryModeAsync(bool temporary)
    {
        if (!CanSend || (!temporary && savedConversations is null)) return;
        historyBusy = true;
        SetHistoryInteraction(false);
        try
        {
            // Switching always begins a new session. No transcript is copied across the privacy boundary.
            IsTemporary = temporary;
            sessionStore = temporary ? new InMemoryConversationStore() : savedConversations!;
            ResetConversation();
            HistoryList.ItemsSource = null;
            SearchInput.Clear();
            UpdateHistoryPresentation();
            if (!temporary) await RefreshHistoryListAsync();
        }
        catch (OperationCanceledException) when (stopping) { }
        catch (ConversationStoreException error) { if (!stopping) StatusLabel.Text = StoreFailureMessage(error); }
        finally { historyBusy = false; if (!stopping) SetHistoryInteraction(true); }
    }

    internal async Task ContinueWithoutSavingAsync()
    {
        if (stopping || activeTurn is not null || historyBusy || pendingCompletion is not { } pending || conversation is not { } current) return;
        StopSpeech();
        historyBusy = true;
        SaveRetryButton.IsEnabled = false;
        ContinueTemporaryButton.IsEnabled = false;
        try
        {
            // An explicit recovery action copies only the displayed branch into memory.
            // Nothing is written back to the saved repository, including the unsaved reply.
            var temporaryStore = new InMemoryConversationStore();
            var copy = await temporaryStore.CreateAsync(current.Title, historyLifetime.Token);
            foreach (var turn in ConversationHistory.GetActiveBranch(current))
                copy = await temporaryStore.AppendTurnAsync(copy.Id, copy.LeafTurnId, turn.Prompt,
                    turn.Id == pending.TurnId ? pending.Response : turn.Response,
                    turn.Id == pending.TurnId ? pending.Outcome : turn.Outcome, historyLifetime.Token);
            if (stopping) return;
            sessionStore = temporaryStore;
            conversation = copy;
            IsTemporary = true;
            temporaryFromSaved = true;
            pendingCompletion = null;
            currentTurnId = null;
            HistoryList.ItemsSource = null;
            RestoreConversationView();
            StatusLabel.Text = "Continuing temporarily with this reply. Earlier saved messages remain on this PC.";
        }
        catch (OperationCanceledException) when (stopping) { }
        catch (ConversationStoreException error) { if (!stopping) StatusLabel.Text = StoreFailureMessage(error); }
        finally
        {
            historyBusy = false;
            if (!stopping)
            {
                SaveRetryButton.IsEnabled = true;
                ContinueTemporaryButton.IsEnabled = true;
                SetHistoryInteraction(true);
            }
        }
    }

    private async Task RefreshHistoryListAsync()
    {
        if (IsTemporary || stopping) return;
        var items = await sessionStore.ListAsync(SearchInput.Text, historyLifetime.Token);
        if (stopping || IsTemporary) return;
        bindingHistory = true;
        try
        {
            HistoryList.ItemsSource = items;
            HistoryList.SelectedItem = items.FirstOrDefault(item => item.Id == conversation?.Id);
        }
        finally { bindingHistory = false; }
    }

    internal Task OpenConversationAsync(Guid id) => RunHistoryActionAsync(async () =>
    {
        var loaded = await sessionStore.LoadAsync(id, historyLifetime.Token);
        if (stopping) return;
        if (loaded is null) throw new ConversationStoreException(ConversationStoreFailure.NotFound);
        conversation = loaded;
        forking = false;
        forkParentId = null;
        retryPrompt = null;
        MessageInput.Clear();
        RestoreConversationView();
        StatusLabel.Text = "Conversation opened. Choose a turn to continue from, edit or regenerate.";
    });

    internal Task SelectConversationBranchAsync(Guid turnId) => RunHistoryActionAsync(async () =>
    {
        if (conversation is null) return;
        var updated = await sessionStore.SelectBranchAsync(conversation.Id, turnId, historyLifetime.Token);
        if (stopping) return;
        conversation = updated;
        forking = false;
        forkParentId = null;
        RestoreConversationView();
        StatusLabel.Text = "Continuing from the selected turn. Other branches are preserved.";
    });

    private async Task RunHistoryActionAsync(Func<Task> action)
    {
        if (!CanSend) return;
        StopSpeech();
        historyBusy = true;
        SetHistoryInteraction(false);
        try { await action(); }
        catch (OperationCanceledException) when (stopping) { }
        catch (ConversationStoreException error) { if (!stopping) StatusLabel.Text = StoreFailureMessage(error); }
        catch (ArgumentException) { if (!stopping) StatusLabel.Text = "Check the title or search text and try again."; }
        finally { historyBusy = false; if (!stopping) SetHistoryInteraction(true); }
    }

    private void ResetConversation()
    {
        StopSpeech();
        conversation = null;
        temporaryFromSaved = false;
        currentTurnId = null;
        forking = false;
        forkParentId = null;
        history.Clear();
        transcript.Clear();
        TranscriptText.Clear();
        UpdateTranscriptView();
        MessageInput.Clear();
        retryPrompt = null;
        RetryButton.Visibility = Visibility.Collapsed;
        if (IsTemporary) sessionStore = new InMemoryConversationStore();
        UpdateHistoryPresentation();
        StatusLabel.Text = IsTemporary ? "New temporary conversation. History is not saved." : "New conversation. Messages will be saved on this PC.";
        MessageInput.Focus();
    }

    private void RestoreConversationView()
    {
        if (conversation is null) return;
        transcript.Clear();
        foreach (var turn in ConversationHistory.GetActiveBranch(conversation))
        {
            transcript.Add(new("You", turn.Prompt));
            transcript.Add(new(turn.Outcome switch
            {
                TurnOutcome.Completed => "Companion",
                TurnOutcome.Interrupted => "Companion · stopped",
                _ => "Companion · incomplete"
            }, string.IsNullOrEmpty(turn.Response) ? "(No reply received.)" : turn.Response));
        }
        history.Clear();
        // Use the shared admission policy, then remove its placeholder next prompt.
        history.AddRange(ConversationHistory.BuildContext(conversation, "next").SkipLast(1));
        retryPrompt = null;
        RetryButton.Visibility = Visibility.Collapsed;
        RenderTranscript();
        UpdateHistoryPresentation();
    }

    private void RenderPendingBranch(string prompt, TranscriptEntry reply)
    {
        if (conversation is null) return;
        transcript.Clear();
        foreach (var turn in ConversationHistory.GetActiveBranch(conversation).Where(turn => turn.Id != currentTurnId))
        {
            transcript.Add(new("You", turn.Prompt));
            transcript.Add(new(turn.Outcome == TurnOutcome.Completed ? "Companion" : "Companion · incomplete",
                string.IsNullOrEmpty(turn.Response) ? "(No reply received.)" : turn.Response));
        }
        transcript.Add(new("You", prompt));
        transcript.Add(reply);
        RenderTranscript();
    }

    private void UpdateHistoryPresentation()
    {
        ConversationHeading.Text = conversation?.Title ?? "Your local companion";
        PrivacyLabel.Text = IsTemporary
            ? temporaryFromSaved ? "Temporary · New messages are not saved. Earlier saved messages remain in history."
                : "Temporary · Nothing from this chat is saved. Closing it clears the conversation."
            : "Saved on this PC · History is stored locally without encryption. Partial replies may be lost if the app closes.";
        HistoryPane.Visibility = IsTemporary ? Visibility.Collapsed : Visibility.Visible;
        TitleInput.Text = conversation?.Title ?? string.Empty;
        SaveRetryButton.Visibility = HasPendingSave ? Visibility.Visible : Visibility.Collapsed;
        ContinueTemporaryButton.Visibility = HasPendingSave ? Visibility.Visible : Visibility.Collapsed;
        bindingHistory = true;
        try
        {
            var choices = conversation?.Turns.Select((turn, index) => new TurnChoice(turn.Id,
                $"{index + 1} · {ConversationHistory.TitleFromPrompt(turn.Prompt)}")).ToArray() ?? [];
            BranchSelector.ItemsSource = choices;
            BranchSelector.SelectedItem = choices.FirstOrDefault(choice => choice.Id == conversation?.LeafTurnId);
            BranchPanel.Visibility = choices.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        }
        finally { bindingHistory = false; }
        UpdateSpeechControls();
    }

    private void SetHistoryInteraction(bool enabled)
    {
        var available = enabled && !stopping && !HasPendingSave;
        HistoryPane.IsEnabled = available;
        BranchPanel.IsEnabled = available;
        TemporaryModeButton.IsEnabled = available;
        SavedModeButton.IsEnabled = available && savedConversations is not null;
        NewConversationButton.IsEnabled = available;
        RetryButton.IsEnabled = available;
        SendButton.IsEnabled = available && CanSend && !string.IsNullOrWhiteSpace(MessageInput.Text);
        UpdateSpeechControls();
        UpdateResearchControls();
    }

    private void ShutdownHistory()
    {
        historyLifetime.Cancel();
        conversation = null;
        currentTurnId = null;
        pendingCompletion = null;
        sessionStore = new InMemoryConversationStore();
        // Do not touch controls after close: pending callbacks cannot repopulate them.
    }

    internal void EditSelectedTurn()
    {
        if (!CanSend || conversation is null || conversation.LeafTurnId is not { } leaf) return;
        StopSpeech();
        var turn = conversation.Turns.Single(item => item.Id == leaf);
        forkParentId = turn.ParentTurnId;
        forking = true;
        MessageInput.Text = turn.Prompt;
        MessageInput.CaretIndex = MessageInput.Text.Length;
        MessageInput.Focus();
        StatusLabel.Text = "Edit your message and send. A new branch will preserve the original reply.";
    }

    private bool ConfirmDiscardTemporary() => !IsTemporary || conversation is null || conversation.Turns.Count == 0 ||
        MessageBox.Show(this, "Start a new chat and discard this temporary conversation?", "Temporary conversation",
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    private async void SavedMode_Click(object sender, RoutedEventArgs e)
    {
        if (CanSend && ConfirmDiscardTemporary()) await SwitchHistoryModeAsync(false);
    }
    private async void TemporaryMode_Click(object sender, RoutedEventArgs e)
    {
        if (CanSend && ConfirmDiscardTemporary()) await SwitchHistoryModeAsync(true);
    }
    private async void Search_Click(object sender, RoutedEventArgs e) => await RunHistoryActionAsync(RefreshHistoryListAsync);
    private async void ContinueTemporary_Click(object sender, RoutedEventArgs e) => await ContinueWithoutSavingAsync();
    private async void HistoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!bindingHistory && !IsTemporary && HistoryList.SelectedItem is ConversationSummary selected)
            await OpenConversationAsync(selected.Id);
    }
    private async void BranchSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!bindingHistory && BranchSelector.SelectedItem is TurnChoice choice) await SelectConversationBranchAsync(choice.Id);
    }
    private void EditTurn_Click(object sender, RoutedEventArgs e) => EditSelectedTurn();
    private async void Regenerate_Click(object sender, RoutedEventArgs e)
    {
        if (!CanSend || conversation?.LeafTurnId is null) return;
        EditSelectedTurn();
        await SendMessageAsync();
    }
    private async void Rename_Click(object sender, RoutedEventArgs e) => await RunHistoryActionAsync(async () =>
    {
        if (conversation is not { } current) return;
        await sessionStore.RenameAsync(current.Id, TitleInput.Text.Trim(), historyLifetime.Token);
        if (stopping) return;
        conversation = await sessionStore.LoadAsync(current.Id, historyLifetime.Token);
        if (stopping) return;
        UpdateHistoryPresentation();
        await RefreshHistoryListAsync();
        if (!stopping) StatusLabel.Text = "Conversation renamed.";
    });
    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (!CanSend || conversation is not { } current) return;
        if (MessageBox.Show(this, "Delete this saved conversation and all its branches? This cannot be undone.",
            "Delete conversation", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await RunHistoryActionAsync(async () =>
        {
            await sessionStore.DeleteAsync(current.Id, historyLifetime.Token);
            if (stopping) return;
            ResetConversation();
            await RefreshHistoryListAsync();
            if (!stopping) StatusLabel.Text = "Conversation deleted.";
        });
    }
    private void CopyReply_Click(object sender, RoutedEventArgs e)
    {
        var reply = transcript.LastOrDefault(entry => entry.Label.StartsWith("Companion", StringComparison.Ordinal));
        if (reply is null) return;
        try { Clipboard.SetText(reply.Content); StatusLabel.Text = "Reply copied."; }
        catch (System.Runtime.InteropServices.ExternalException) { StatusLabel.Text = "The clipboard is busy. Try copying again."; }
    }

    private static string StoreFailureMessage(ConversationStoreException error) => error.Failure switch
    {
        ConversationStoreFailure.Corrupt => "Saved history could not be read. Its files were preserved. You can start a temporary chat.",
        ConversationStoreFailure.UnsupportedSchema => "This history was saved by a different app version. Its files were preserved. You can start a temporary chat.",
        ConversationStoreFailure.LimitReached => "History is full. Start a new conversation or delete an old saved one.",
        ConversationStoreFailure.NotFound => "That conversation is no longer available. Refresh the saved list.",
        _ => "History could not be saved or opened. Check available disk space and access, then try again."
    };

    private sealed record TurnChoice(Guid Id, string Label);
    private sealed record PendingCompletion(Guid ConversationId, Guid TurnId, string Response, TurnOutcome Outcome);
}
