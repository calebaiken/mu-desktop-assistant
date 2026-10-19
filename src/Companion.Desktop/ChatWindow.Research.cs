using System.Windows;
using Companion.Application.MachineLearning;

namespace Companion.Desktop;

public partial class ChatWindow
{
    private CancellationTokenSource? researchTransfer;
    internal Func<string, CancellationToken, Task<bool>>? ResearchHandoff { get; set; }
    private void UpdateResearchControls()
    {
        if (ResearchHandoffButton is null) return;
        ResearchHandoffButton.IsEnabled = !IsTemporary && CanSend && !string.IsNullOrWhiteSpace(MessageInput.Text);
        ResearchHandoffHelp.Text = IsTemporary
            ? "Research handoff is disabled in Temporary Chat: the ML Lab can persist project artifacts."
            : "Send to Research Lab transfers only this message-box draft after project selection and confirmation. Chat history is never included.";
    }
    private async void ResearchHandoff_Click(object sender, RoutedEventArgs e) => await SendToResearchAsync();
    internal async Task SendToResearchAsync()
    {
        if (IsTemporary || !CanSend || ResearchHandoff is null) return;
        var request = MessageInput.Text; // Exact visible draft; no trimming, transcript, clipboard or history.
        try { ResearchRequestHandoff.ValidateRequest(request); }
        catch (ArgumentException error) { StatusLabel.Text = error.Message; return; }
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(historyLifetime.Token);
        researchTransfer = cancellation;
        SetHistoryInteraction(false);
        MessageInput.IsReadOnly = true;
        UpdateResearchControls();
        try
        {
            var transferred = await ResearchHandoff(request, cancellation.Token);
            if (!stopping && !cancellation.IsCancellationRequested)
                StatusLabel.Text = transferred ? "Request transferred. Review a new plan in the selected Lab; no ML action has run." : "Research transfer cancelled. No request was transferred.";
        }
        catch (OperationCanceledException) { if (!stopping) StatusLabel.Text = "Research transfer cancelled."; }
        catch (Exception) { if (!stopping) StatusLabel.Text = "The research destination is unavailable. Select the project and review a new transfer."; }
        finally
        {
            if (ReferenceEquals(researchTransfer, cancellation)) researchTransfer = null;
            if (!stopping)
            {
                MessageInput.IsReadOnly = false;
                SetHistoryInteraction(true);
                UpdateResearchControls();
            }
        }
    }
}
