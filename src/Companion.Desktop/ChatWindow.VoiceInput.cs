using System.Windows;
using System.Windows.Input;
using Companion.Application.Voice;

namespace Companion.Desktop;

public partial class ChatWindow
{
    private IAudioCapture? audioCapture;
    private ISpeechToTextProvider? speechRecognizer;
    private CancellationTokenSource? voiceInputCancellation;
    private CancellationTokenSource? voiceHoldRelease;
    private Task? voiceInputTask;
    private bool holdIntent, releasingMouse, applyingVoiceDraft;
    private long draftRevision, voiceConversationRevision;
    internal bool IsVoiceInputBusy => voiceInputCancellation is not null;
    internal bool IsCapturing => VoiceInputState == VoiceInputActivity.Recording;
    internal bool IsTranscribing => VoiceInputState == VoiceInputActivity.Transcribing;
    internal VoiceInputActivity VoiceInputState { get; private set; }
    public event EventHandler? VoiceInputChanged;
    internal enum VoiceInputActivity { None, Preparing, Recording, Transcribing }

    private void InitializeVoiceInput(IAudioCapture? capture, ISpeechToTextProvider? recognizer,
        IVoiceInputPreferencesStore? preferences)
    {
        audioCapture = capture; speechRecognizer = recognizer; voicePreferencesStore = preferences;
        VoiceInputPanel.Visibility = capture is null || recognizer is null || preferences is null
            ? Visibility.Collapsed : Visibility.Visible;
        Deactivated += (_, _) => CancelVoiceInput();
        IsVisibleChanged += (_, _) => { if (!IsVisible) CancelVoiceInput(); };
        UpdateVoiceInputControls();
    }

    internal Task BeginVoiceInputAsync()
    {
        Dispatcher.VerifyAccess();
        if (!CanBeginVoiceInput) return Task.CompletedTask;
        holdIntent = true;
        var cancellation = new CancellationTokenSource();
        var release = new CancellationTokenSource();
        voiceInputCancellation = cancellation;
        voiceHoldRelease = release;
        var device = (AudioInputDevice)MicrophoneSelector.SelectedItem;
        var draft = draftRevision;
        var conversationRevision = voiceConversationRevision;
        SetVoiceInputState(VoiceInputActivity.Preparing);
        SetVoiceInputStatus("Preparing the microphone. Keep holding to record.");
        UpdateVoiceInputControls();
        return voiceInputTask = RunVoiceInputAsync(device.Id, draft, conversationRevision, cancellation, release);
    }

    private async Task RunVoiceInputAsync(string deviceId, long draft, long conversationRevision,
        CancellationTokenSource cancellation, CancellationTokenSource release)
    {
        var captureFinished = false;
        try
        {
            await StopSpeechAndWaitAsync(cancellation.Token);
            await Task.Delay(AudioInputLimits.OutputTail, cancellation.Token);
            if (!holdIntent || release.IsCancellationRequested || !MayApplyVoiceDraft(cancellation, draft, conversationRevision)) return;
            var progress = new Progress<AudioCaptureState>(state =>
            {
                if (stopping || !ReferenceEquals(voiceInputCancellation, cancellation)) return;
                if (state == AudioCaptureState.Started && !captureFinished && !cancellation.IsCancellationRequested && !release.IsCancellationRequested)
                {
                    SetVoiceInputState(VoiceInputActivity.Recording);
                    SetVoiceInputStatus("Recording · Release to transcribe. Nothing is sent automatically.");
                }
                else if (state == AudioCaptureState.Stopped && IsCapturing)
                    SetVoiceInputState(VoiceInputActivity.Preparing);
            });
            using var audio = await audioCapture!.CaptureAsync(new(deviceId, Guid.NewGuid()), progress,
                release.Token, cancellation.Token);
            captureFinished = true;
            if (!MayApplyVoiceDraft(cancellation, draft, conversationRevision)) return;
            SetVoiceInputState(VoiceInputActivity.Transcribing);
            SetVoiceInputStatus(audio.DurationLimited
                ? "The 30-second recording limit was reached. Transcribing locally…"
                : "Transcribing locally… You can cancel or type instead.");
            var result = audio.ByteLength == 0 ? new SpeechRecognitionResult(string.Empty, false)
                : await speechRecognizer!.TranscribeAsync(audio, cancellation.Token);
            AudioInputLimits.Validate(result);
            if (!MayApplyVoiceDraft(cancellation, draft, conversationRevision)) return;
            if (!result.HasSpeech) { SetVoiceInputStatus("No speech detected. Hold to talk again, or type your message."); return; }
            applyingVoiceDraft = true;
            try { MessageInput.Text = result.Text; MessageInput.CaretIndex = result.Text.Length; }
            finally { applyingVoiceDraft = false; }
            SetVoiceInputStatus("Transcript ready. Check or edit it, then choose Send.");
            MessageInput.Focus();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (!stopping && ReferenceEquals(voiceInputCancellation, cancellation))
                SetVoiceInputStatus("Voice input cancelled. Your typed message is unchanged.");
        }
        catch (Exception error)
        {
            if (!stopping && ReferenceEquals(voiceInputCancellation, cancellation))
                SetVoiceInputStatus(VoiceInputFailureMessage(error));
        }
        finally
        {
            captureFinished = true;
            if (ReferenceEquals(voiceInputCancellation, cancellation))
            {
                voiceInputCancellation = null;
                voiceHoldRelease = null;
                holdIntent = false;
                SetVoiceInputState(VoiceInputActivity.None);
                if (!stopping) { ReleaseHoldMouse(); UpdateVoiceInputControls(); UpdateSpeechControls(); }
            }
            release.Dispose(); cancellation.Dispose();
        }
    }

    private bool CanBeginVoiceInput => !stopping && !IsVoiceInputBusy && !IsGenerating && !historyBusy &&
        !HasPendingSave && audioCapture is not null && speechRecognizer is not null &&
        voicePreferencesReady && !voicePreferencesSaving && !voicePreferences.MicrophoneMuted &&
        MicrophoneSelector.SelectedItem is AudioInputDevice && MessageInput.Text.Length == 0;

    private bool MayApplyVoiceDraft(CancellationTokenSource operation, long draft, long conversationRevision) =>
        !stopping && !operation.IsCancellationRequested && ReferenceEquals(voiceInputCancellation, operation) &&
        draftRevision == draft && voiceConversationRevision == conversationRevision && MessageInput.Text.Length == 0;

    internal void ReleaseVoiceInput()
    {
        Dispatcher.VerifyAccess();
        holdIntent = false;
        voiceHoldRelease?.Cancel();
        ReleaseHoldMouse();
    }

    internal void CancelVoiceInput()
    {
        Dispatcher.VerifyAccess();
        voiceConversationRevision++;
        holdIntent = false;
        voiceInputCancellation?.Cancel();
        ReleaseHoldMouse();
        if (!stopping && IsVoiceInputBusy) SetVoiceInputStatus(IsCapturing ? "Stopping the microphone…" : "Cancelling voice input…");
    }

    private async Task CancelVoiceInputAndWaitAsync()
    {
        CancelVoiceInput();
        if (voiceInputTask is not null) await voiceInputTask.WaitAsync(AudioInputLimits.CleanupDeadline);
    }

    private void VoiceDraftChanged()
    {
        draftRevision++;
        if (!applyingVoiceDraft && IsVoiceInputBusy) CancelVoiceInput();
        UpdateVoiceInputControls();
    }

    private void SetVoiceInputState(VoiceInputActivity state)
    {
        if (VoiceInputState == state) return;
        VoiceInputState = state;
        if (!stopping) UpdateVoiceInputControls();
        VoiceInputChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetVoiceInputStatus(string text)
    {
        VoiceInputStatus.Text = text;
        VoiceInputStatus.Visibility = Visibility.Visible;
    }

    private static string VoiceInputFailureMessage(Exception error) => error is AudioInputException input ? input.Failure switch
    {
        AudioInputFailure.PermissionDenied => "Windows has not allowed this microphone. Typed chat is still available.",
        AudioInputFailure.DeviceLost => "The selected microphone disconnected. Select a microphone and hold again.",
        AudioInputFailure.Overflow => "Recording could not keep up. Please try a shorter recording.",
        AudioInputFailure.Timeout => "Voice input reached its time limit. Try a shorter recording or type instead.",
        AudioInputFailure.CleanupFailed => "Voice input could not stop cleanly. Restart the companion before recording again.",
        _ => "Local voice input is unavailable. Check the microphone and local voice setup, or type instead."
    } : "Voice input could not finish. Your typed message is unchanged.";

    private void ReleaseHoldMouse()
    {
        if (HoldToTalkButton is null || !HoldToTalkButton.IsMouseCaptured) return;
        releasingMouse = true;
        try { HoldToTalkButton.ReleaseMouseCapture(); }
        finally { releasingMouse = false; }
    }

    private async void HoldToTalk_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!CanBeginVoiceInput || e.ChangedButton != MouseButton.Left) return;
        e.Handled = true;
        HoldToTalkButton.Focus();
        if (!HoldToTalkButton.CaptureMouse()) return;
        await BeginVoiceInputAsync();
    }
    private void HoldToTalk_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || !holdIntent) return;
        e.Handled = true; ReleaseVoiceInput();
    }
    private void HoldToTalk_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (!releasingMouse && IsVoiceInputBusy) CancelVoiceInput();
    }
    private async void HoldToTalk_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space || Keyboard.Modifiers != ModifierKeys.None) return;
        e.Handled = true;
        if (!e.IsRepeat && CanBeginVoiceInput) await BeginVoiceInputAsync();
    }
    private void HoldToTalk_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space) return;
        e.Handled = true; ReleaseVoiceInput();
    }
    private void HoldToTalk_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (holdIntent) CancelVoiceInput();
    }
    private void CancelVoiceInput_Click(object sender, RoutedEventArgs e) => CancelVoiceInput();
}
