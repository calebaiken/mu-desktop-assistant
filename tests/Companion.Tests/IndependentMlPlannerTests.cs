using System.Runtime.CompilerServices;
using System.Text.Json;
using Companion.Application.Conversation;
using Companion.Application.MachineLearning;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

/// <summary>Independent typed proposal checks with a controlled text-model port; no action executor is present.</summary>
[TestClass]
public sealed class IndependentMlPlannerTests
{
    [TestMethod]
    public async Task CompleteLocalTextBecomesAnInspectableActionWithoutExecutionAuthority()
    {
        var selection = new MlDatasetSelection(@"C:\research\events.csv", RowLimit: 100);
        var action = new MlTrain(selection, "regression", "ridge", ["x"], "y", Scaling: "none");
        var payload = JsonSerializer.Serialize(new { action = "train", parameters = MlActionCodec.Parameters(action), reason = "A regularized baseline; association does not establish causation." }, MlActionCodec.Json);
        var model = new FixtureModel(new ChatTextDelta(payload[..40]), new ChatTextDelta(payload[40..]), new ChatCompleted(ChatFinishReason.Stop));
        var proposal = await new MlConversationPlanner(model).PlanAsync("Predict y. Do not scale.", selection, ["x", "y"], "Research");
        var train = proposal.Action as MlTrain;
        Assert.IsNotNull(train);
        Assert.AreEqual("none", train.Scaling);
        Assert.AreEqual(selection, train.Dataset);
        Assert.IsTrue(proposal.ConfigurationJson.Contains("\"scaling\": \"none\"", StringComparison.Ordinal));
        Assert.AreEqual(1, model.Calls);
        Assert.AreEqual(2, model.Messages.Count);
        Assert.AreEqual(ChatRole.System, model.Messages[0].Role);
        Assert.AreEqual(ChatRole.User, model.Messages[1].Role);
    }

    [TestMethod]
    public async Task IncompleteLengthLimitedAndLateStreamsCannotBecomeRunnablePlans()
    {
        const string json = "{\"action\":\"list\",\"parameters\":{},\"reason\":\"Inspect saved work.\"}";
        ChatStreamEvent[][] cases =
        [
            [new ChatTextDelta(json)],
            [new ChatTextDelta(json), new ChatCompleted(ChatFinishReason.Length)],
            [new ChatTextDelta(json), new ChatCompleted(ChatFinishReason.Stop), new ChatTextDelta("late")],
            [new ChatCompleted(ChatFinishReason.Stop)]
        ];
        foreach (var events in cases)
            await Assert.ThrowsExactlyAsync<MlException>(() => new MlConversationPlanner(new FixtureModel(events)).PlanAsync("List experiments", null, [], "Learn"));
    }

    [TestMethod]
    public async Task ArbitraryCodeUnknownActionsAndExtraFieldsAreRejectedWithoutEcho()
    {
        string[] outputs =
        [
            "import os; os.remove('PRIVATE_PLAN_SENTINEL')",
            "{\"action\":\"exec\",\"parameters\":{},\"reason\":\"PRIVATE_PLAN_SENTINEL\"}",
            "{\"action\":\"list\",\"parameters\":{\"code\":\"PRIVATE_PLAN_SENTINEL\"},\"reason\":\"List\"}",
            "{\"action\":\"list\",\"parameters\":{},\"reason\":\"List\",\"execute\":true}",
            "{\"action\":\"list\",\"action\":\"export\",\"parameters\":{},\"reason\":\"List\"}",
            "```json\n{\"action\":\"list\",\"parameters\":{},\"reason\":\"List\"}\n```"
        ];
        foreach (var output in outputs)
        {
            var error = await Assert.ThrowsExactlyAsync<MlException>(() => new MlConversationPlanner(new FixtureModel(
                new ChatTextDelta(output), new ChatCompleted(ChatFinishReason.Stop))).PlanAsync("Propose a plan", null, [], "Auto"));
            Assert.AreEqual("proposal", error.Code);
            Assert.IsFalse(error.ToString().Contains("PRIVATE_PLAN_SENTINEL", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public async Task OutputBoundRejectsBeforeParsingOversizedModelText()
    {
        var model = new FixtureModel(new ChatTextDelta(new string('x', 12001)), new ChatCompleted(ChatFinishReason.Stop));
        await Assert.ThrowsExactlyAsync<MlException>(() => new MlConversationPlanner(model).PlanAsync("List", null, [], "Learn"));
    }

    [TestMethod]
    public async Task InvalidUserRequestDoesNotCallLocalModel()
    {
        foreach (var prompt in new[] { "", " ", new string('x', 2001) })
        {
            var model = new FixtureModel();
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => new MlConversationPlanner(model).PlanAsync(prompt, null, [], "Learn"));
            Assert.AreEqual(0, model.Calls);
        }
    }

    [TestMethod]
    public async Task CancellationCannotYieldAProposalEvenFromNoncooperativeModel()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var model = new FixtureModel(new ChatTextDelta("{\"action\":\"list\",\"parameters\":{},\"reason\":\"List\"}"), new ChatCompleted(ChatFinishReason.Stop));
        await Assert.ThrowsAsync<OperationCanceledException>(() => new MlConversationPlanner(model).PlanAsync("List", null, [], "Learn", cancellation.Token));
        Assert.AreEqual(0, model.Calls);
    }

    private sealed class FixtureModel(params ChatStreamEvent[] events) : IChatModel
    {
        public int Calls { get; private set; }
        public IReadOnlyList<ChatMessage> Messages { get; private set; } = [];
        public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(IReadOnlyList<ChatMessage> messages,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Calls++;
            Messages = messages.ToArray();
            await Task.Yield();
            // Deliberately ignores cancellation, exercising the planner's own admission guard.
            foreach (var item in events) yield return item;
        }
    }
}
