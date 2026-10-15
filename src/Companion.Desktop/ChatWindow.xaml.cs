using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Companion.Application.Conversation;
using Companion.Application.Voice;
using Companion.Infrastructure.Desktop;

namespace Companion.Desktop;

/// <summary>Bounded local chat with explicitly separate saved and temporary sessions.</summary>
public partial class ChatWindow : Window
{
    private const int MaximumInputCharacters = 2000;
    private const int MaximumReplyCharacters = 8192;
    private const int MaximumContextBytes = 24 * 1024;
    private const int MaximumContextMessages = 24;
    private readonly Func<CancellationToken, Task<IChatModel>> modelFactory;
    private readonly List<ChatMessage> history = [];
    private readonly List<TranscriptEntry> transcript = [];
    private CancellationTokenSource? activeTurn;
    private string? retryPrompt;
    private bool stopping;

    public ChatWindow(Func<CancellationToken, Task<IChatModel>> modelFactory, IConversationStore? savedStore = null,
        ISpeechOutput? speechOutput = null)
    {
        ArgumentNullException.ThrowIfNull(modelFactory);
        this.modelFactory = modelFactory;
        InitializeComponent();
        InitializeHistory(savedStore);
        InitializeSpeech(speechOutput);
        Loaded += (_, _) =>
        {
            try { DesktopWindowPlacement.Constrain(new WindowInteropHelper(this).Handle, false); }
            catch (Win32Exception) { StatusLabel.Text = "The display changed. You can move this chat window using its title bar."; }
            MessageInput.Focus();
        };
        Closed += (_, _) => BeginShutdown();
    }

    public bool IsGenerating { get; private set; }
    public event EventHandler? GenerationChanged;
    public event Action<string>? ReplyCompleted;
    public event EventHandler? MlLabRequested;
    private void MlLab_Click(object sender, RoutedEventArgs e) => MlLabRequested?.Invoke(this, EventArgs.Empty);
    internal IReadOnlyList<ChatMessage> ConversationContext => history.AsReadOnly();

    internal async Task SendMessageAsync()
    {
        Dispatcher.VerifyAccess();
        if (!CanSend) return;
        var prompt = MessageInput.Text.Trim();
        if (prompt.Length == 0) return;
        if (prompt.Length > MaximumInputCharacters)
        {
            StatusLabel.Text = "Shorten your message to 2,000 characters, then send it again.";
            return;
        }
        StopSpeech();

        var request = new List<ChatMessage>(history) { new(ChatRole.User, prompt) };
        while (request.Count > 1 && (request.Count > MaximumContextMessages || ContextBytes(request) > MaximumContextBytes))
            request.RemoveRange(0, 2);
        using var cancellation = new CancellationTokenSource();
        activeTurn = cancellation;
        SetGenerating(true);
        SetHistoryInteraction(false);
        MessageInput.Clear();
        retryPrompt = null;
        RetryButton.Visibility = Visibility.Collapsed;
        MessageInput.IsReadOnly = true;
        NewConversationButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        SendButton.IsEnabled = false;
        StatusLabel.Text = "Loading the local model… You can stop at any time.";
        transcript.Add(new("You", prompt));
        var reply = new TranscriptEntry("Companion · responding", string.Empty);
        transcript.Add(reply);
        RenderTranscript();
        var output = new StringBuilder();
        var completed = false;
        var lengthLimited = false;
        var publishReply = false;
        try
        {
            request = (await PrepareHistoryTurnAsync(prompt, cancellation.Token)).ToList();
            if (!MayUpdate(cancellation)) return;
            RenderPendingBranch(prompt, reply);
            var model = await modelFactory(cancellation.Token);
            if (!MayUpdate(cancellation)) return;
            cancellation.Token.ThrowIfCancellationRequested();
            StatusLabel.Text = "Replying on this PC…";
            await foreach (var item in model.StreamAsync(request.AsReadOnly(), cancellation.Token))
            {
                if (!MayUpdate(cancellation)) return;
                cancellation.Token.ThrowIfCancellationRequested();
                if (completed) throw new ChatModelException(ChatFailure.Protocol);
                switch (item)
                {
                    case ChatTextDelta delta:
                        if (delta.Text.Length > MaximumReplyCharacters - output.Length)
                            throw new ChatModelException(ChatFailure.OutputLimit);
                        output.Append(delta.Text);
                        reply.Content = output.ToString();
                        RenderTranscript();
                        break;
                    case ChatCompleted done:
                        completed = true;
                        lengthLimited = done.Reason == ChatFinishReason.Length;
                        break;
                    default:
                        throw new ChatModelException(ChatFailure.Protocol);
                }
            }
            if (!MayUpdate(cancellation)) return;
            cancellation.Token.ThrowIfCancellationRequested();
            if (!completed || string.IsNullOrWhiteSpace(output.ToString()))
                throw new ChatModelException(ChatFailure.Protocol);
            if (!await CompleteHistoryTurnAsync(output.ToString(), TurnOutcome.Completed))
            {
                if (MayUpdate(cancellation)) { reply.Label = "Companion · not saved"; RenderTranscript(); }
                return;
            }
            if (!MayUpdate(cancellation)) return;
            history.Clear();
            history.AddRange(request);
            history.Add(new(ChatRole.Assistant, output.ToString()));
            while (history.Count > MaximumContextMessages || ContextBytes(history) > MaximumContextBytes)
                history.RemoveRange(0, 2);
            reply.Label = lengthLimited ? "Companion · response limit reached" : "Companion";
            StatusLabel.Text = lengthLimited ? "The reply reached its response limit. You can ask a follow-up." : IsTemporary ? "Ready. This conversation is not saved." : "Ready. Saved on this PC.";
            RenderTranscript();
            publishReply = true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (MayUpdate(cancellation))
            {
                await CompleteHistoryTurnAsync(output.ToString(), TurnOutcome.Interrupted);
                if (MayUpdate(cancellation)) MarkIncomplete(reply, prompt, "stopped", "Stopped. This interrupted turn will not be included in the next message.");
            }
        }
        catch (ChatModelException error)
        {
            if (MayUpdate(cancellation))
            {
                await CompleteHistoryTurnAsync(output.ToString(), TurnOutcome.Failed);
                if (MayUpdate(cancellation)) MarkIncomplete(reply, prompt, "incomplete", FailureMessage(error.Failure));
            }
        }
        catch (ConversationStoreException error)
        {
            if (MayUpdate(cancellation)) MarkIncomplete(reply, prompt, "not saved", StoreFailureMessage(error));
        }
        catch (Exception)
        {
            if (MayUpdate(cancellation))
            {
                await CompleteHistoryTurnAsync(output.ToString(), TurnOutcome.Failed);
                if (MayUpdate(cancellation)) MarkIncomplete(reply, prompt, "incomplete", "The local reply could not finish. Edit and retry your message.");
            }
        }
        finally
        {
            if (ReferenceEquals(activeTurn, cancellation))
            {
                activeTurn = null;
                if (!stopping)
                {
                    SetGenerating(false);
                    SetHistoryInteraction(true);
                    MessageInput.IsReadOnly = false;
                    NewConversationButton.IsEnabled = CanSend;
                    StopButton.IsEnabled = false;
                    SendButton.IsEnabled = CanSend && !string.IsNullOrWhiteSpace(MessageInput.Text);
                    MessageInput.Focus();
                }
            }
        }
        if (publishReply && !stopping) ReplyCompleted?.Invoke(output.ToString());
    }

    public void BeginShutdown()
    {
        Dispatcher.VerifyAccess();
        if (stopping) return;
        stopping = true;
        ShutdownSpeech();
        IsEnabled = false;
        SendButton.IsEnabled = false;
        StopButton.IsEnabled = false;
        activeTurn?.Cancel();
        SetGenerating(false);
        history.Clear();
        transcript.Clear();
        retryPrompt = null;
        ShutdownHistory();
    }

    internal void StopGeneration()
    {
        Dispatcher.VerifyAccess();
        if (stopping || activeTurn is null) return;
        StopButton.IsEnabled = false;
        StatusLabel.Text = "Stopping the local reply…";
        activeTurn.Cancel();
    }

    private bool MayUpdate(CancellationTokenSource turn) => !stopping && ReferenceEquals(activeTurn, turn);

    private void SetGenerating(bool value)
    {
        if (IsGenerating == value) return;
        IsGenerating = value;
        UpdateSpeechControls();
        UpdateTranscriptView();
        GenerationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void MarkIncomplete(TranscriptEntry reply, string prompt, string label, string status)
    {
        reply.Label = "Companion · " + label;
        if (reply.Content.Length == 0) reply.Content = "(No reply received.)";
        retryPrompt = prompt;
        RetryButton.Visibility = Visibility.Visible;
        StatusLabel.Text = HasPendingSave ? "This reply is still here, but could not be saved. Choose Retry saving before continuing." : status;
        RenderTranscript();
    }

    private void RenderTranscript()
    {
        while (transcript.Count > 16 || transcript.Sum(entry => entry.Content.Length) > 48 * 1024)
            transcript.RemoveRange(0, 2);
        var followEnd = TranscriptText.VerticalOffset + TranscriptText.ViewportHeight >= TranscriptText.ExtentHeight - 24;
        TranscriptText.Text = string.Join("\n\n", transcript.Select(entry => entry.Label + "\n" + entry.Content));
        if (followEnd) TranscriptText.ScrollToEnd();
        UpdateTranscriptView();
    }

    private void TranscriptView_Changed(object sender, RoutedEventArgs e) => UpdateTranscriptView();

    private void UpdateTranscriptView()
    {
        if (TranscriptText is null || FormattedTranscript is null || stopping) return;
        var plain = IsGenerating || PlainTextToggle.IsChecked == true;
        // Streaming remains cheap and selectable; rich parsing runs only on settled views.
        TranscriptText.Visibility = plain ? Visibility.Visible : Visibility.Hidden;
        FormattedTranscript.Visibility = plain ? Visibility.Hidden : Visibility.Visible;
        if (!plain)
            FormattedTranscript.Document = SafeChatDocumentRenderer.RenderConversation(transcript.Select(entry => (entry.Label, entry.Content)).ToArray());
    }

    private static int ContextBytes(IEnumerable<ChatMessage> messages) => messages.Sum(message => Encoding.UTF8.GetByteCount(message.Content));

    private static string FailureMessage(ChatFailure failure) => failure switch
    {
        ChatFailure.InvalidRequest => "The model could not accept this conversation. Start a new conversation or shorten your message.",
        ChatFailure.Unavailable => "The local model is unavailable. Check the local model setup, then retry.",
        ChatFailure.Timeout => "The local model took too long to respond. You can retry with a shorter message.",
        ChatFailure.Protocol => "The local model returned an incomplete reply. Edit and retry your message.",
        ChatFailure.OutputLimit => "The reply exceeded the local response limit. Try asking for a shorter answer.",
        ChatFailure.Busy => "The local model is still finishing another request. Try again shortly.",
        _ => "The local reply could not finish. Edit and retry your message."
    };

    private async void Send_Click(object sender, RoutedEventArgs e) => await SendMessageAsync();
    private void Stop_Click(object sender, RoutedEventArgs e) => StopGeneration();

    private async void MessageInput_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None) return;
        e.Handled = true;
        await SendMessageAsync();
    }

    private void MessageInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (SendButton is not null)
            SendButton.IsEnabled = CanSend && !string.IsNullOrWhiteSpace(MessageInput.Text);
    }

    private void NewConversation_Click(object sender, RoutedEventArgs e)
    {
        if (!CanSend) return;
        ResetConversation();
    }

    private void Retry_Click(object sender, RoutedEventArgs e)
    {
        if (!CanSend || retryPrompt is null) return;
        MessageInput.Text = retryPrompt;
        MessageInput.CaretIndex = MessageInput.Text.Length;
        MessageInput.Focus();
        StatusLabel.Text = "Edit your message if needed, then send it again.";
    }

    private sealed class TranscriptEntry(string label, string content)
    {
        public string Label { get; set; } = label;
        public string Content { get; set; } = content;
    }
}
