using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Companion.Application.Conversation;
using Companion.Application.MachineLearning;
using Companion.Infrastructure.Conversation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

[TestClass]
public sealed class IndependentStructuredMlTests
{
    private static readonly MlDatasetSelection Selection = new(@"C:\research\events.csv", "events", 17, 100);

    [TestMethod]
    public async Task CurrentAliasesResolveToCapturedSelectionWithoutGeneratedPaths()
    {
        foreach (var (operation, parameters) in new[]
        {
            ("inspect", "{\"selection\":\"current\"}"), ("browse", "{\"selection\":\"current\"}"),
            ("train", "{\"dataset\":\"current\",\"task\":\"reduction\",\"algorithm\":\"pca\",\"features\":[\"x\",\"z\"],\"scaling\":\"none\"}"),
            ("predict", "{\"dataset\":\"current\",\"modelId\":\"550e8400-e29b-41d4-a716-446655440000\"}")
        })
        {
            var model = new StructuredFixture($"{{\"action\":\"{operation}\",\"parameters\":{parameters},\"reason\":\"Inspect the selected data.\"}}");
            var result = await new MlConversationPlanner(model).PlanAsync("Use the selected dataset", Selection, ["x", "z"], "Research");
            var selected = result.Action switch { MlInspect a => a.Dataset, MlBrowse a => a.Dataset, MlTrain a => a.Dataset, MlPredict a => a.Dataset, _ => null };
            Assert.AreEqual(Selection, selected);
            Assert.AreEqual(1, model.StructuredCalls);
            Assert.AreEqual(0, model.OrdinaryCalls);
            if (result.Action is MlTrain train) Assert.AreEqual("none", train.Scaling);
        }
    }

    [TestMethod]
    public async Task AliasResolutionPreservesDuplicateAndUnknownFieldRejection()
    {
        string[] parameters =
        [
            "{\"dataset\":\"current\",\"dataset\":\"current\",\"task\":\"reduction\",\"algorithm\":\"pca\",\"features\":[\"x\"]}",
            "{\"dataset\":\"current\",\"task\":\"reduction\",\"algorithm\":\"pca\",\"features\":[\"x\"],\"code\":\"PRIVATE_SENTINEL\"}",
            "{\"dataset\":\"other\",\"task\":\"reduction\",\"algorithm\":\"pca\",\"features\":[\"x\"]}"
        ];
        foreach (var value in parameters)
        {
            var model = new StructuredFixture($"{{\"action\":\"train\",\"parameters\":{value},\"reason\":\"Plan\"}}");
            var error = await Assert.ThrowsExactlyAsync<MlException>(() => new MlConversationPlanner(model).PlanAsync("Plan", Selection, ["x"], "Learn"));
            Assert.IsFalse(error.Message.Contains("PRIVATE_SENTINEL", StringComparison.Ordinal));
        }
        var missing = new StructuredFixture("{\"action\":\"inspect\",\"parameters\":{\"selection\":\"current\"},\"reason\":\"Plan\"}");
        await Assert.ThrowsExactlyAsync<MlException>(() => new MlConversationPlanner(missing).PlanAsync("Plan", null, [], "Learn"));
        var extra = new StructuredFixture("{\"action\":\"inspect\",\"parameters\":{\"selection\":\"current\",\"path\":\"C:/other.csv\"},\"reason\":\"Plan\"}");
        await Assert.ThrowsExactlyAsync<MlException>(() => new MlConversationPlanner(extra).PlanAsync("Plan", Selection, [], "Learn"));
    }

    [TestMethod]
    public async Task GeneratedSchemaIsClosedAndRestrictsCurrentSelectionAndFeatureNames()
    {
        var model = new StructuredFixture("{\"action\":\"list\",\"parameters\":{},\"reason\":\"List\"}");
        await new MlConversationPlanner(model).PlanAsync("List", Selection, ["x", "z"], "Learn");
        var branches = model.Schema.GetProperty("anyOf").EnumerateArray().ToArray();
        CollectionAssert.AreEquivalent(new[] { "inspect", "browse", "train", "list", "predict", "export", "describe", "compare" },
            branches.Select(b => b.GetProperty("properties").GetProperty("action").GetProperty("const").GetString()).ToArray());
        foreach (var branch in branches) Assert.IsFalse(branch.GetProperty("additionalProperties").GetBoolean());
        var train = branches.Single(b => b.GetProperty("properties").GetProperty("action").GetProperty("const").GetString() == "train")
            .GetProperty("properties").GetProperty("parameters");
        Assert.IsFalse(train.GetProperty("additionalProperties").GetBoolean());
        Assert.AreEqual("current", train.GetProperty("properties").GetProperty("dataset").GetProperty("const").GetString());
        CollectionAssert.AreEqual(new[] { "x", "z" }, train.GetProperty("properties").GetProperty("features").GetProperty("items").GetProperty("enum").EnumerateArray().Select(v => v.GetString()).ToArray());
        Assert.IsFalse(train.GetProperty("properties").TryGetProperty("code", out _));
    }

    [TestMethod]
    public async Task UnselectedSchemaExposesExplicitBoundedPathInsteadOfCurrentAlias()
    {
        var model = new StructuredFixture("{\"action\":\"list\",\"parameters\":{},\"reason\":\"List\"}");
        await new MlConversationPlanner(model).PlanAsync("List", null, [], "Research");
        var parameters = model.Schema.GetProperty("anyOf")[0].GetProperty("properties").GetProperty("parameters");
        Assert.AreEqual(2048, parameters.GetProperty("properties").GetProperty("path").GetProperty("maxLength").GetInt32());
        Assert.IsFalse(parameters.GetProperty("additionalProperties").GetBoolean());
        Assert.IsFalse(parameters.GetProperty("properties").TryGetProperty("selection", out _));
    }

    [TestMethod]
    public async Task StructuredTransportAddsClonedSchemaAndOrdinaryChatRemainsUnconstrained()
    {
        var handler = new WireFixture();
        using var model = Model(handler);
        IAsyncEnumerable<ChatStreamEvent> stream;
        using (var schema = JsonDocument.Parse("{\"type\":\"object\",\"additionalProperties\":false}"))
            stream = model.StreamJsonAsync([new(ChatRole.User, "Plan")], schema.RootElement);
        await DrainAsync(stream);
        var constrained = handler.Completions[0];
        Assert.AreEqual("json_object", constrained.GetProperty("response_format").GetProperty("type").GetString());
        Assert.IsFalse(constrained.GetProperty("response_format").GetProperty("schema").GetProperty("additionalProperties").GetBoolean());
        Assert.AreEqual(0d, constrained.GetProperty("temperature").GetDouble());
        Assert.AreEqual(256, constrained.GetProperty("max_tokens").GetInt32());
        Assert.IsFalse(constrained.GetProperty("cache_prompt").GetBoolean());
        await DrainAsync(model.StreamAsync([new(ChatRole.User, "Hello")]));
        var ordinary = handler.Completions[1];
        Assert.IsFalse(ordinary.TryGetProperty("response_format", out _));
        Assert.AreEqual(.7, ordinary.GetProperty("temperature").GetDouble());
        Assert.AreEqual(1.5, ordinary.GetProperty("presence_penalty").GetDouble());
        Assert.AreEqual(6, handler.Requests);
    }

    [TestMethod]
    public void InvalidOrOversizedSchemasAreRejectedBeforeAnyTransport()
    {
        var handler = new WireFixture();
        using var model = Model(handler);
        foreach (var raw in new[] { "[]", "null", "{\"description\":\"" + new string('x', 16385) + "\"}" })
        {
            using var schema = JsonDocument.Parse(raw);
            Assert.ThrowsExactly<ArgumentException>(() => model.StreamJsonAsync([new(ChatRole.User, "Plan")], schema.RootElement));
        }
        Assert.AreEqual(0, handler.Requests);
    }

    [TestMethod]
    public async Task StructuredResponseStillRequiresCompletionAndReleasesAdmissionAfterFailure()
    {
        var handler = new WireFixture { Incomplete = true };
        using var model = Model(handler);
        using var schema = JsonDocument.Parse("{\"type\":\"object\"}");
        var failure = await Assert.ThrowsExactlyAsync<ChatModelException>(() => DrainAsync(model.StreamJsonAsync([new(ChatRole.User, "Plan")], schema.RootElement)));
        Assert.AreEqual(ChatFailure.Protocol, failure.Failure);
        handler.Incomplete = false;
        await DrainAsync(model.StreamJsonAsync([new(ChatRole.User, "Plan")], schema.RootElement));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var before = handler.Requests;
        await Assert.ThrowsAsync<OperationCanceledException>(() => DrainAsync(model.StreamJsonAsync([new(ChatRole.User, "Plan")], schema.RootElement, cancelled.Token)));
        Assert.AreEqual(before, handler.Requests);
    }

    private static LocalChatModel Model(WireFixture handler) => new(new HttpClient(handler), new Uri("http://127.0.0.1:12345/v1/chat/completions"), new string('a', 32));
    private static async Task DrainAsync(IAsyncEnumerable<ChatStreamEvent> stream) { await foreach (var _ in stream) { } }

    private sealed class StructuredFixture(string output) : IStructuredChatModel
    {
        internal int StructuredCalls, OrdinaryCalls;
        internal JsonElement Schema;
        public IAsyncEnumerable<ChatStreamEvent> StreamAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
        { OrdinaryCalls++; return Events(cancellationToken); }
        public IAsyncEnumerable<ChatStreamEvent> StreamJsonAsync(IReadOnlyList<ChatMessage> messages, JsonElement schema, CancellationToken cancellationToken = default)
        { StructuredCalls++; Schema = schema.Clone(); return Events(cancellationToken); }
        private async IAsyncEnumerable<ChatStreamEvent> Events([EnumeratorCancellation] CancellationToken token)
        { await Task.Yield(); token.ThrowIfCancellationRequested(); yield return new ChatTextDelta(output); yield return new ChatCompleted(ChatFinishReason.Stop); }
    }

    private sealed class WireFixture : HttpMessageHandler
    {
        internal int Requests;
        internal bool Incomplete;
        internal List<JsonElement> Completions { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            Assert.AreEqual("127.0.0.1", request.RequestUri!.Host);
            Assert.AreEqual(new string('a', 32), request.Headers.Authorization?.Parameter);
            var path = request.RequestUri.AbsolutePath;
            var media = "application/json";
            var body = path == "/apply-template" ? "{\"prompt\":\"fixture\"}" : "{\"tokens\":[1,2,3]}";
            if (path == "/v1/chat/completions")
            {
                using var parsed = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                Completions.Add(parsed.RootElement.Clone());
                media = "text/event-stream";
                body = "data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":\"{}\"}}]}\n\n" +
                    (Incomplete ? "" : "data: {\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}\n\n") + "data: [DONE]\n\n";
            }
            return new(HttpStatusCode.OK) { RequestMessage = request, Content = new StringContent(body, Encoding.UTF8, media) };
        }
    }
}
