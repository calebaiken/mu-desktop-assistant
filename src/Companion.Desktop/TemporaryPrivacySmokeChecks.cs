using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Companion.Application.Conversation;
using Companion.Infrastructure.Conversation;

namespace Companion.Desktop;

/// <summary>Public sentinel fixture that remains alive for the supervisor's owned-process termination.</summary>
internal static class TemporaryPrivacySmokeChecks
{
    internal const string PromptSentinel = "PHASE3_TEMP_PROMPT_7D2A95B4A13E4D87A41F29F5E904C6B1";
    internal const string ReplySentinel = "PHASE3_TEMP_REPLY_86913C08B64F4ED696CF8AA497629C15";

    internal static async Task WaitForPrivacyTerminationAsync(string dataDirectory)
    {
        if (File.Exists(Path.Combine(dataDirectory, "conversations.db")))
            throw new InvalidOperationException("The privacy fixture requires a fresh conversation profile.");
        var saved = new SqliteConversationStore(dataDirectory);
        var window = new ChatWindow(_ => Task.FromResult<IChatModel>(new SentinelModel()), saved)
        { ShowActivated = false, ShowInTaskbar = false, Opacity = 0 };
        window.Show();
        try
        {
            // Establish a real durable repository and saved transcript before switching mode.
            await window.SwitchHistoryModeAsync(false);
            window.MessageInput.Text = "Public saved decoy question";
            await window.SendMessageAsync();
            if (window.IsGenerating || window.CurrentConversation?.Turns.Single().Outcome != TurnOutcome.Completed)
                throw new InvalidOperationException("The durable privacy-fixture decoy did not complete.");
            var decoyId = window.CurrentConversation.Id;
            await window.SwitchHistoryModeAsync(true);
            if (!window.IsTemporary || window.CurrentConversation is not null || window.ConversationContext.Count != 0)
                throw new InvalidOperationException("The privacy fixture did not switch to an empty temporary session.");
            window.MessageInput.Text = PromptSentinel;
            await window.SendMessageAsync();
            if (window.IsGenerating || !window.IsTemporary || window.CurrentConversation?.Turns.Single().Outcome != TurnOutcome.Completed ||
                window.ConversationContext.Count != 2 || window.ConversationContext[0].Content != PromptSentinel ||
                window.ConversationContext[1].Content != ReplySentinel || !window.TranscriptText.Text.Contains(ReplySentinel, StringComparison.Ordinal))
                throw new InvalidOperationException("The temporary privacy-fixture reply did not complete in memory.");
            var listed = await saved.ListAsync();
            if (listed.Count != 1 || listed[0].Id != decoyId)
                throw new InvalidOperationException("The privacy fixture changed the durable conversation inventory.");
            // This marker deliberately contains no transcript or sentinel value.
            var readyPath = Path.Combine(dataDirectory, "private-history-ready.json");
            await File.WriteAllTextAsync(readyPath + ".tmp",
                JsonSerializer.Serialize(new { ready = true, temporary = true, durableConversations = listed.Count,
                    processId = Environment.ProcessId }));
            File.Move(readyPath + ".tmp", readyPath);
            await Task.Delay(Timeout.InfiniteTimeSpan);
        }
        finally { window.Close(); }
    }

    private sealed class SentinelModel : IChatModel
    {
        public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(IReadOnlyList<ChatMessage> messages,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var temporary = messages.Count == 1 && messages[0].Content == PromptSentinel;
            yield return new ChatTextDelta(temporary ? ReplySentinel : "Public saved decoy answer");
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ChatCompleted(ChatFinishReason.Stop);
        }
    }
}
