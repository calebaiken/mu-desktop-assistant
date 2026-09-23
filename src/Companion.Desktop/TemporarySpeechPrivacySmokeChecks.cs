using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Threading;
using Companion.Application.Conversation;
using Companion.Application.Voice;
using Companion.Infrastructure.Conversation;

namespace Companion.Desktop;

/// <summary>Whole-app temporary speech UI persistence fixture; fake output is not third-party engine evidence.</summary>
internal static class TemporarySpeechPrivacySmokeChecks
{
    internal const string ReplySentinel = "PHASE4A_TEMP_SPEECH_6E31D790B7A94F54B2A937AD55C00812";
    private const string TemporaryPrompt = "Public temporary speech privacy question";
    private const string SavedReply = "This is the local voice playback check. Press Stop to end it.";

    internal static async Task WaitForPrivacyTerminationAsync(string dataDirectory)
    {
        if (File.Exists(Path.Combine(dataDirectory, "conversations.db")))
            throw new InvalidOperationException("The speech privacy fixture requires a fresh conversation profile.");
        using var readinessDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var readyToken = readinessDeadline.Token;
        var saved = new SqliteConversationStore(dataDirectory);
        await using var speech = new HoldingSpeech();
        var window = new ChatWindow(_ => Task.FromResult<IChatModel>(new SentinelModel()), saved, speech)
        { ShowActivated = false, ShowInTaskbar = false, Opacity = 0 };
        Task? read = null;
        window.Show();
        try
        {
            await window.SwitchHistoryModeAsync(false).WaitAsync(readyToken);
            window.MessageInput.Text = "Public saved speech privacy decoy";
            await window.SendMessageAsync().WaitAsync(readyToken);
            if (window.IsGenerating || window.CurrentConversation?.Turns.Single().Outcome != TurnOutcome.Completed ||
                window.CurrentConversation.Turns.Single().Response != SavedReply || speech.SpeakCalls != 0)
                throw new InvalidOperationException("The durable speech privacy decoy did not complete without playback.");
            var decoyId = window.CurrentConversation.Id;
            await window.SwitchHistoryModeAsync(true).WaitAsync(readyToken);
            if (!window.IsTemporary || window.CurrentConversation is not null || window.ConversationContext.Count != 0)
                throw new InvalidOperationException("The speech privacy fixture did not create an empty temporary session.");
            window.MessageInput.Text = TemporaryPrompt;
            await window.SendMessageAsync().WaitAsync(readyToken);
            if (window.IsGenerating || !window.IsTemporary || window.CurrentConversation?.Turns.Single().Outcome != TurnOutcome.Completed ||
                window.CurrentConversation.Turns.Single().Response != ReplySentinel || speech.SpeakCalls != 0 ||
                !window.TranscriptText.Text.Contains(ReplySentinel, StringComparison.Ordinal))
                throw new InvalidOperationException("The temporary speech privacy reply did not complete in memory without playback.");

            // Deliberately invoke the same explicit UI action; this fixture has no audio output.
            read = window.ReadReplyAloudAsync();
            while (!window.IsSpeaking)
            {
                readyToken.ThrowIfCancellationRequested();
                if (read.IsCompleted) throw new InvalidOperationException("The controlled speech privacy operation ended before readiness.");
                await Task.Delay(10, readyToken);
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle, readyToken);
            }
            if (!window.IsSpeechBusy || speech.SpeakCalls != 1 || !speech.ReceivedSentinel)
                throw new InvalidOperationException("The speech privacy fixture did not retain one active explicit operation.");
            var listed = await saved.ListAsync(cancellationToken: readyToken);
            var storedDecoy = await saved.LoadAsync(decoyId, readyToken);
            if (listed.Count != 1 || listed[0].Id != decoyId || storedDecoy?.Turns.Single().Response != SavedReply)
                throw new InvalidOperationException("The speech privacy fixture changed the durable decoy.");

            var readyPath = Path.Combine(dataDirectory, "private-speech-ready.json");
            await File.WriteAllTextAsync(readyPath + ".tmp", JsonSerializer.Serialize(new
            {
                ready = true, temporary = window.IsTemporary, processId = Environment.ProcessId,
                speechBusy = window.IsSpeechBusy, durableConversations = listed.Count
            }), readyToken);
            File.Move(readyPath + ".tmp", readyPath);
            // Setup is bounded above. The ready process remains owned by the external termination supervisor.
            await Task.Delay(Timeout.InfiniteTimeSpan);
        }
        finally
        {
            window.Close();
            if (read is not null) await read.WaitAsync(TimeSpan.FromSeconds(3));
        }
    }

    private sealed class SentinelModel : IChatModel
    {
        public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(IReadOnlyList<ChatMessage> messages,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var temporary = messages.Count == 1 && messages[0].Content == TemporaryPrompt;
            yield return new ChatTextDelta(temporary ? ReplySentinel : SavedReply);
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ChatCompleted(ChatFinishReason.Stop);
        }
    }

    private sealed class HoldingSpeech : ISpeechOutput
    {
        internal int SpeakCalls { get; private set; }
        internal bool ReceivedSentinel { get; private set; }
        public Task<IReadOnlyList<SpeechVoice>> GetVoicesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<SpeechVoice>>([new("fixture-privacy", "Silent privacy fixture", "en-US")]);
        }
        public async Task SpeakAsync(SpeechRequest request, IProgress<SpeechOutputState>? progress,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SpeakCalls++;
            ReceivedSentinel = request.Text == ReplySentinel;
            if (SpeakCalls != 1 || !ReceivedSentinel)
                throw new InvalidOperationException("The speech privacy fixture received an unexpected operation.");
            progress?.Report(SpeechOutputState.Speaking);
            try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
            finally { progress?.Report(SpeechOutputState.Stopped); }
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
