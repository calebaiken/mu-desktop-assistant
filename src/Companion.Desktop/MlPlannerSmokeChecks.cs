using System.IO;
using System.Text.Json;
using System.Text;
using System.Runtime.CompilerServices;
using Companion.Application.Conversation;
using Companion.Application.MachineLearning;

namespace Companion.Desktop;

/// <summary>Live local-model proposals on synthetic column names; never executes a proposal.</summary>
internal static class MlPlannerSmokeChecks
{
    internal static async Task RunAsync(IChatModel model, string profile, CancellationToken cancellationToken)
    {
        var results = new List<object>();
        var failed = false;
        var selected = new MlDatasetSelection(Path.Combine(profile, "ml-fixtures", "deterministic-events.csv"), RowLimit: 100);
        var cases = new[]
        {
            (Prompt: "Inspect the currently selected dataset.", Kind: "inspect"),
            (Prompt: "Run PCA on x and z in the selected dataset. Do not scale these features.", Kind: "train")
        };
        foreach (var item in cases)
        {
            var recording = new SyntheticRecordingModel(model);
            try
            {
                var proposal = await new MlConversationPlanner(recording).PlanAsync(item.Prompt, selected,
                    ["event_id", "x", "z", "target", "event_type"], "Research", cancellationToken);
                var valid = item.Kind == "inspect" ? proposal.Action is MlInspect inspect && inspect.Dataset == selected
                    : proposal.Action is MlTrain { Task: "reduction", Algorithm: "pca", Scaling: "none", Target: null } train &&
                      train.Dataset == selected && train.Features.SequenceEqual(new[] { "x", "z" });
                failed |= !valid;
                results.Add(new { prompt = item.Prompt, passed = valid, proposal = proposal.ConfigurationJson, explanation = proposal.Explanation });
            }
            catch (Exception error) when (error is MlException or ChatModelException or ArgumentException)
            {
                failed = true;
                results.Add(new { prompt = item.Prompt, passed = false, error = error.Message, syntheticModelOutput = recording.Output.ToString() });
            }
        }
        await File.WriteAllTextAsync(Path.Combine(profile, "ml-planner-smoke.json"), JsonSerializer.Serialize(new
        {
            passed = !failed, results, limitations = "Two live synthetic prompts; not a general language-understanding benchmark. No proposal was executed."
        }, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        if (failed) throw new InvalidOperationException("Live local ML planning diagnostic failed; inspect its retained receipt.");
    }

    // This recorder exists only in the explicitly isolated, synthetic diagnostic.
    private sealed class SyntheticRecordingModel(IChatModel inner) : IStructuredChatModel
    {
        internal StringBuilder Output { get; } = new();
        public IAsyncEnumerable<ChatStreamEvent> StreamAsync(IReadOnlyList<ChatMessage> messages,
            CancellationToken cancellationToken = default) => RecordAsync(inner.StreamAsync(messages, cancellationToken), cancellationToken);
        public IAsyncEnumerable<ChatStreamEvent> StreamJsonAsync(IReadOnlyList<ChatMessage> messages,
            JsonElement schema, CancellationToken cancellationToken = default) => RecordAsync(
                inner is IStructuredChatModel structured ? structured.StreamJsonAsync(messages, schema, cancellationToken) : inner.StreamAsync(messages, cancellationToken), cancellationToken);
        private async IAsyncEnumerable<ChatStreamEvent> RecordAsync(IAsyncEnumerable<ChatStreamEvent> stream,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var item in stream.WithCancellation(cancellationToken))
            {
                if (item is ChatTextDelta delta && Output.Length < 12000) Output.Append(delta.Text);
                yield return item;
            }
        }
    }
}
