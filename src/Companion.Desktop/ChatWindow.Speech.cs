using System.Windows;
using Companion.Application.Conversation;
using Companion.Application.Voice;

namespace Companion.Desktop;

public partial class ChatWindow
{
    private ISpeechOutput? speechOutput;
    private CancellationTokenSource? speechCancellation;
    private bool speechVoicesLoaded;
    internal bool IsSpeechBusy => speechCancellation is not null;
    internal bool IsSpeaking { get; private set; }
    public event EventHandler? SpeechChanged;

    private void InitializeSpeech(ISpeechOutput? output)
    {
        speechOutput = output;
        SpeechOptions.Visibility = ReadAloudButton.Visibility = output is null ? Visibility.Collapsed : Visibility.Visible;
        SpeechRateSelector.ItemsSource = Enumerable.Range(-5, 11)
            .Select(rate => new SpeechRateChoice(rate, rate == 0 ? "Normal speed" : rate < 0 ? $"Slower ({rate})" : $"Faster (+{rate})")).ToArray();
        SpeechRateSelector.SelectedValue = 0;
        UpdateSpeechControls();
    }

    internal async Task ReadReplyAloudAsync()
    {
        Dispatcher.VerifyAccess();
        if (stopping || IsSpeechBusy || IsGenerating || historyBusy || speechOutput is null || SpeechReply() is not { } reply) return;
        await SpeakTextAsync(reply, preview: false);
    }

    internal async Task PreviewVoiceAsync()
    {
        Dispatcher.VerifyAccess();
        if (stopping || IsSpeechBusy || IsGenerating || historyBusy || speechOutput is null) return;
        await SpeakTextAsync("Hello, I'm Mu, your local assistant. We can explore an idea together, take a closer look at your data, or work through a physics problem. Choose the voice that feels most comfortable to you.", preview: true);
    }

    private async Task SpeakTextAsync(string text, bool preview)
    {
        var content = text;
        var clipped = content.Length > 2000;
        if (clipped)
        {
            var length = char.IsHighSurrogate(content[1999]) ? 1999 : 2000;
            content = content[..length];
        }
        using var cancellation = new CancellationTokenSource();
        speechCancellation = cancellation;
        SetSpeechStatus("Preparing the selected local voice…");
        UpdateSpeechControls();
        try
        {
            await LoadSpeechVoicesAsync(cancellation);
            if (!MayUpdateSpeech(cancellation) || cancellation.IsCancellationRequested) return;
            if (SpeechVoiceSelector.SelectedItem is not SpeechVoice voice)
                throw new SpeechException(SpeechFailure.Unavailable);
            var rate = SpeechRateSelector.SelectedValue is int selected ? selected : 0;
            var neural = voice.Id.StartsWith("kokoro:", StringComparison.Ordinal);
            var playbackStatus = neural
                ? $"{(preview ? "Previewing" : "Reading up to 30 seconds using")} {voice.Name}. Longer replies are shortened; the full text stays here."
                : clipped
                ? $"Reading the first 2,000 characters using {voice.Name}. Playback stops after 30 seconds."
                : $"Reading aloud using {voice.Name}. Playback stops after 30 seconds.";
            SetSpeechStatus(neural ? $"Preparing {voice.Name} locally… You can stop at any time." : playbackStatus);
            var progress = new Progress<SpeechOutputState>(state =>
            {
                if (!MayUpdateSpeech(cancellation)) return;
                // A queued start callback cannot revive speech after a stop request.
                if (state == SpeechOutputState.Speaking && cancellation.IsCancellationRequested) return;
                if (state == SpeechOutputState.Speaking) SetSpeechStatus(playbackStatus);
                SetSpeaking(state == SpeechOutputState.Speaking);
            });
            await speechOutput!.SpeakAsync(new(content, voice.Id, rate), progress, cancellation.Token);
            if (MayUpdateSpeech(cancellation)) SetSpeechStatus(cancellation.IsCancellationRequested ? "Voice stopped. Your reply is still here."
                : preview ? "Voice preview finished. Choose another voice to compare."
                : neural ? "Finished voice playback, up to 30 seconds. The full reply remains available as text." : "Finished reading aloud.");
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (MayUpdateSpeech(cancellation)) SetSpeechStatus("Voice stopped. Your reply is still here.");
        }
        catch (SpeechException error)
        {
            if (MayUpdateSpeech(cancellation)) SetSpeechStatus(SpeechFailureMessage(error.Failure));
        }
        catch (Exception)
        {
            if (MayUpdateSpeech(cancellation)) SetSpeechStatus("The selected local voice could not play. You can choose another voice, or read and copy the text.");
        }
        finally
        {
            if (ReferenceEquals(speechCancellation, cancellation))
            {
                speechCancellation = null;
                SetSpeaking(false);
                if (!stopping) UpdateSpeechControls();
            }
        }
    }

    internal void StopSpeech()
    {
        Dispatcher.VerifyAccess();
        if (speechCancellation is null || speechCancellation.IsCancellationRequested) return;
        speechCancellation.Cancel();
        if (!stopping)
        {
            SetSpeechStatus("Stopping voice…");
            UpdateSpeechControls();
        }
    }

    private void ShutdownSpeech()
    {
        StopSpeech();
        speechCancellation = null;
        SetSpeaking(false);
        // App owns provider disposal. Pending callbacks must not touch the closed controls.
    }

    private string? SpeechReply()
    {
        if (conversation is null || conversation.LeafTurnId is not { } leaf) return null;
        var turn = conversation.Turns.FirstOrDefault(item => item.Id == leaf);
        return turn is { Outcome: TurnOutcome.Completed } && !string.IsNullOrWhiteSpace(turn.Response) ? turn.Response : null;
    }

    private async Task LoadSpeechVoicesAsync(CancellationTokenSource cancellation)
    {
        if (speechVoicesLoaded || speechOutput is null) return;
        var voices = await speechOutput.GetVoicesAsync(cancellation.Token);
        if (!MayUpdateSpeech(cancellation) || cancellation.IsCancellationRequested) return;
        SpeechVoiceSelector.ItemsSource = voices;
        SpeechVoiceSelector.SelectedIndex = voices.Count > 0 ? 0 : -1;
        speechVoicesLoaded = voices.Count > 0;
        if (!speechVoicesLoaded) throw new SpeechException(SpeechFailure.Unavailable);
    }

    private async void SpeechOptions_Expanded(object sender, RoutedEventArgs e)
    {
        if (stopping || IsSpeechBusy || speechOutput is null || speechVoicesLoaded) return;
        using var cancellation = new CancellationTokenSource();
        speechCancellation = cancellation;
        UpdateSpeechControls();
        try { await LoadSpeechVoicesAsync(cancellation); }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception) { if (MayUpdateSpeech(cancellation)) SetSpeechStatus("No usable local voice was found. Chat remains available."); }
        finally
        {
            if (ReferenceEquals(speechCancellation, cancellation))
            {
                speechCancellation = null;
                if (!stopping) UpdateSpeechControls();
            }
        }
    }

    private bool MayUpdateSpeech(CancellationTokenSource operation) => !stopping && ReferenceEquals(speechCancellation, operation);

    private void SetSpeaking(bool value)
    {
        if (IsSpeaking == value) return;
        IsSpeaking = value;
        SpeechChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetSpeechStatus(string message)
    {
        SpeechStatusLabel.Text = message;
        SpeechStatusLabel.Visibility = Visibility.Visible;
    }

    private void UpdateSpeechControls()
    {
        if (ReadAloudButton is null || stopping) return;
        ReadAloudButton.IsEnabled = speechOutput is not null && !IsSpeechBusy && !IsGenerating && !historyBusy && SpeechReply() is not null;
        StopSpeechButton.Visibility = IsSpeechBusy ? Visibility.Visible : Visibility.Collapsed;
        StopSpeechButton.IsEnabled = IsSpeechBusy && speechCancellation?.IsCancellationRequested == false;
        SpeechVoiceSelector.IsEnabled = SpeechRateSelector.IsEnabled = !IsSpeechBusy;
        PreviewVoiceButton.IsEnabled = speechOutput is not null && !IsSpeechBusy && !IsGenerating && !historyBusy;
    }

    private static string SpeechFailureMessage(SpeechFailure failure) => failure switch
    {
        SpeechFailure.Timeout => "Voice stopped at its time limit. Your reply is still here; you can read or copy it.",
        SpeechFailure.Busy => "The voice is still stopping. Try Read aloud again when it finishes.",
        SpeechFailure.InvalidRequest => "This reply or voice selection could not be read aloud. Choose a local voice and try again.",
        SpeechFailure.CleanupFailed => "The voice could not stop cleanly. Restart the companion before trying again.",
        _ => "The selected voice, its local assets, available memory or output device is unavailable. Try another voice; chat remains usable. Windows voices require supported Microsoft engines."
    };

    private async void ReadAloud_Click(object sender, RoutedEventArgs e) => await ReadReplyAloudAsync();
    private void StopSpeech_Click(object sender, RoutedEventArgs e) => StopSpeech();
    private async void PreviewVoice_Click(object sender, RoutedEventArgs e) => await PreviewVoiceAsync();
    private sealed record SpeechRateChoice(int Rate, string Label);
}
