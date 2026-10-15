using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Companion.Application.Conversation;
using Companion.Application.Voice;
using Companion.Infrastructure.Voice;

namespace Companion.Desktop;

/// <summary>Explicit public-sample diagnostic; normal speech never saves audio.</summary>
internal static class NeuralSpeechSmokeChecks
{
    internal static async Task RunAsync(KokoroNeuralSpeechOutput neural, ISpeechOutput speech, string profile, bool playback)
    {
        List<string> passed = [];
        List<object> observations = [];
        string? failure = null;
        var elapsed = Stopwatch.StartNew();
        var tracker = new ProbePreview(neural, speech);
        var modelCalls = 0;
        var window = new ChatWindow(_ =>
        {
            modelCalls++;
            return Task.FromException<IChatModel>(new InvalidOperationException("No language model is authorized by this voice diagnostic."));
        }, new InMemoryConversationStore(), tracker) { ShowActivated = false, ShowInTaskbar = false, Opacity = 0 };
        var ticks = 0;
        long maximumWorkerPrivateBytes = 0;
        Process? sampledWorker = null;
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(50) };
        timer.Tick += (_, _) =>
        {
            ticks++;
            try
            {
                if (neural.ActiveWorkerId is not { } id) return;
                if (sampledWorker is null || sampledWorker.Id != id)
                { sampledWorker?.Dispose(); sampledWorker = Process.GetProcessById(id); _ = sampledWorker.SafeHandle; }
                sampledWorker.Refresh();
                maximumWorkerPrivateBytes = Math.Max(maximumWorkerPrivateBytes, sampledWorker.PrivateMemorySize64);
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
        };
        void Check(string name, bool condition)
        {
            if (!condition) throw new InvalidOperationException("Neural voice check failed: " + name);
            passed.Add(name);
        }
        try
        {
            window.Show();
            await DrainAsync(window);
            Check("actual-native-chat-window", new WindowInteropHelper(window).Handle != 0);
            Check("preview-available-without-a-chat-reply", window.PreviewVoiceButton.IsEnabled && !window.ReadAloudButton.IsEnabled);
            window.SpeechOptions.IsExpanded = true;
            await UntilAsync(window, () => !window.IsSpeechBusy);
            var voices = window.SpeechVoiceSelector.Items.Cast<SpeechVoice>().Where(voice => voice.Id.StartsWith("kokoro:", StringComparison.Ordinal)).ToArray();
            Check("actual-curated-neural-catalog", voices.Select(voice => voice.Id).SequenceEqual(new[] { "kokoro:af_heart", "kokoro:af_bella", "kokoro:am_michael", "kokoro:bm_george" }) && window.SpeechVoiceSelector.SelectedItem is SpeechVoice selectedVoice && selectedVoice == voices[0]);
            Check("enumerating-voices-does-not-synthesize-or-play", tracker.Calls == 0 && !window.IsSpeaking);
            List<string> hashes = [];
            foreach (var voice in voices)
            {
                maximumWorkerPrivateBytes = 0;
                sampledWorker?.Dispose(); sampledWorker = null;
                window.SpeechVoiceSelector.SelectedItem = voice;
                window.SpeechRateSelector.SelectedValue = 0;
                var before = ticks;
                var watch = Stopwatch.StartNew();
                timer.Start();
                await window.PreviewVoiceAsync().WaitAsync(TimeSpan.FromSeconds(95));
                timer.Stop();
                var audio = tracker.LastAudio ?? throw new InvalidOperationException("Actual synthesis produced no audio: " + (tracker.LastFailure?.ToString() ?? "no provider call") + "; worker=" + (neural.LastWorkerFailure ?? "no closed failure event") + "; calls=" + tracker.Calls);
                var probe = audio.Probe;
                Check(voice.Id + "-actual-valid-audio", probe.Voice.Id == voice.Id && probe.SampleRate == 24000 && probe.Channels == 1 && probe.BitsPerSample == 16 && probe.HasNonZeroSamples && probe.DurationSeconds is > 0 and <= 30);
                Check(voice.Id + "-responsive-native-preview", ticks > before && !window.IsSpeechBusy && window.SpeechStatusLabel.Text.Contains("preview finished", StringComparison.Ordinal));
                Check(voice.Id + "-inaudible-probe-does-not-claim-speaking", !window.IsSpeaking && modelCalls == 0);
                var samplePath = Path.Combine(profile, voice.Id[7..] + ".wav");
                await File.WriteAllBytesAsync(samplePath, audio.Wave);
                hashes.Add(probe.Sha256);
                observations.Add(new { voice, probe, wallSeconds = watch.Elapsed.TotalSeconds, dispatcherTicks = ticks - before,
                    sampledMaximumWorkerPrivateBytes = maximumWorkerPrivateBytes, publicSampleFile = samplePath,
                    observation = "Actual checkpoint through production host and native preview, with playback replaced by an explicitly inaudible probe." });
                Array.Clear(audio.Wave);
                tracker.LastAudio = null;
            }
            Check("voice-preview-does-not-create-conversation-history", window.CurrentConversation is null && modelCalls == 0 && tracker.Calls == 4);
            Check("four-distinct-voice-waveforms", hashes.Distinct(StringComparer.Ordinal).Count() == 4);
            window.Width = window.MinWidth; window.Height = window.MinHeight;
            await window.SwitchHistoryModeAsync(false);
            await DrainAsync(window);
            window.UpdateLayout();
            var content = (FrameworkElement)window.Content;
            foreach (var control in new FrameworkElement[] { window.PreviewVoiceButton, window.SpeechVoiceSelector, window.SpeechRateSelector })
            {
                var bounds = control.TransformToAncestor(content).TransformBounds(new Rect(control.RenderSize));
                Check("minimum-window-reaches-" + control.Name, control.ActualWidth > 0 && control.ActualHeight > 0 && bounds.Left >= -1 && bounds.Top >= -1 && bounds.Right <= content.ActualWidth + 1 && bounds.Bottom <= content.ActualHeight + 1);
            }
            if (playback)
            {
                tracker.Playback = true;
                window.SpeechVoiceSelector.SelectedItem = voices[0];
                var operation = window.PreviewVoiceAsync();
                await UntilAsync(window, () => window.IsSpeaking || !window.IsSpeechBusy, TimeSpan.FromSeconds(95));
                Check("real-playback-starts-speaking-after-generation", window.IsSpeaking && neural.ActiveWorkerId is not null);
                using var owned = Process.GetProcessById(neural.ActiveWorkerId!.Value); _ = owned.SafeHandle;
                var stopWatch = Stopwatch.StartNew();
                window.StopSpeech();
                await operation.WaitAsync(TimeSpan.FromSeconds(4));
                Check("real-stop-exits-held-neural-worker", owned.HasExited && stopWatch.Elapsed <= TimeSpan.FromSeconds(3));
                Check("real-stop-clears-native-speech-state", !window.IsSpeechBusy && !window.IsSpeaking && window.PreviewVoiceButton.IsEnabled);
                List<SpeechOutputState> events = [];
                var statesSync = new object();
                await speech.SpeakAsync(new("Mu's local voice is ready.", voices[0].Id), new DirectProgress(state => { lock (statesSync) events.Add(state); }));
                Check("real-selected-voice-reuse-completes-after-stop", events.SequenceEqual(new[] { SpeechOutputState.Speaking, SpeechOutputState.Stopped }) && neural.ActiveWorkerId is null);
                observations.Add(new { stage = "explicit-default-output-playback", stopSeconds = stopWatch.Elapsed.TotalSeconds,
                    events, physicalHearing = "UNOBSERVED", observation = "Native playback API/event/lifetime evidence; no listening-quality score." });
            }
        }
        catch (Exception error) { failure = error is SpeechException speechError ? speechError.Failure.ToString() : error.GetType().Name + ": " + error.Message; throw; }
        finally
        {
            timer.Stop(); sampledWorker?.Dispose(); window.Close();
            Directory.CreateDirectory(profile);
            await File.WriteAllTextAsync(Path.Combine(profile, "neural-speech-smoke.json"), JsonSerializer.Serialize(new
            {
                count = passed.Count, expectedCount = playback ? 25 : 21, passed, failure,
                workerFailure = neural.LastWorkerFailure,
                elapsedSeconds = elapsed.Elapsed.TotalSeconds, playbackRequested = playback, observations,
                limitations = "Public synthetic samples only. Subjective naturalness, physical hearing, pronunciation accuracy, default-device loss and sustained resource performance are not certified. Samples are written only by this explicit isolated diagnostic; normal read-aloud saves no audio."
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static Task DrainAsync(ChatWindow window) => window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Task;
    private static async Task UntilAsync(ChatWindow window, Func<bool> condition, TimeSpan? timeout = null)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.Elapsed > (timeout ?? TimeSpan.FromSeconds(30))) throw new TimeoutException("Native voice continuation exceeded its diagnostic bound.");
            await Task.Delay(10); await DrainAsync(window);
        }
    }
    private sealed class ProbePreview(KokoroNeuralSpeechOutput neural, ISpeechOutput catalog) : ISpeechOutput
    {
        internal SpeechSynthesisAudio? LastAudio { get; set; }
        internal SpeechFailure? LastFailure { get; private set; }
        internal int Calls { get; private set; }
        internal bool Playback { get; set; }
        public Task<IReadOnlyList<SpeechVoice>> GetVoicesAsync(CancellationToken token = default) => catalog.GetVoicesAsync(token);
        public async Task SpeakAsync(SpeechRequest request, IProgress<SpeechOutputState>? progress, CancellationToken token = default)
        {
            Calls++;
            LastFailure = null;
            try
            {
                if (Playback) await catalog.SpeakAsync(request, progress, token);
                else LastAudio = await neural.SynthesizeAudioAsync(request, token);
            }
            catch (SpeechException error) { LastFailure = error.Failure; throw; }
        }
        public ValueTask DisposeAsync() => throw new InvalidOperationException("The App owns the speech providers.");
    }
    private sealed class DirectProgress(Action<SpeechOutputState> action) : IProgress<SpeechOutputState>
    { public void Report(SpeechOutputState value) => action(value); }
}
