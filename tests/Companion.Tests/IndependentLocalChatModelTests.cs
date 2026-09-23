using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Companion.Application.Conversation;
using Companion.Infrastructure.Conversation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

[TestClass]
public sealed class IndependentLocalChatModelTests
{
    private const string Secret = "independent-test-session-token-0001";
    private static readonly Uri Endpoint = new("http://127.0.0.1:49159/v1/chat/completions");
    private static readonly ChatMessage[] Request = [new(ChatRole.User, "public fixture")];
    private static readonly JsonSerializerOptions LiteralUnicode = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    [TestMethod]
    public async Task EveryUtf8ByteAndCrLfBoundaryCanBeFragmentedWithoutCorruptingText()
    {
        const string expected = "Cafe\u0301 € 😀\n漢字";
        var wire = Chunk(expected) + Finish() + "data: [DONE]\r\n\r\n";
        using var model = Model(new FragmentedStream(Encoding.UTF8.GetBytes(wire), 1));
        var events = await CollectAsync(model);
        Assert.AreEqual(expected, string.Concat(events.OfType<ChatTextDelta>().Select(item => item.Text)));
        Assert.AreEqual(1, events.OfType<ChatCompleted>().Count());
        Assert.AreEqual(ChatFinishReason.Stop, ((ChatCompleted)events[^1]).Reason);
    }

    [TestMethod]
    public async Task RequestUtf8BudgetAcceptsExactly32768BytesAndRejects32769BeforeSend()
    {
        var exact = new[] { new ChatMessage(ChatRole.User, new string('€', 8192)),
            new ChatMessage(ChatRole.Assistant, new string('é', 4095)), new ChatMessage(ChatRole.User, "AB") };
        Assert.AreEqual(32768, exact.Sum(message => Encoding.UTF8.GetByteCount(message.Content)));
        var sends = 0;
        using var handler = new CallbackHandler((_, _) =>
        {
            Interlocked.Increment(ref sends);
            return Task.FromResult(Response(new MemoryStream(Encoding.UTF8.GetBytes(Valid("accepted")))));
        });
        using var client = new HttpClient(handler);
        using var model = new LocalChatModel(client, Endpoint, Secret);
        Assert.IsInstanceOfType<ChatCompleted>((await CollectAsync(model, exact))[^1]);
        var over = exact.ToArray();
        over[^1] = new(ChatRole.User, "ABC");
        var error = await Assert.ThrowsExactlyAsync<ChatModelException>(() => CollectAsync(model, over));
        Assert.AreEqual(ChatFailure.InvalidRequest, error.Failure);
        Assert.AreEqual(1, sends);
    }

    [TestMethod]
    public async Task Output8192CharactersSucceedsAnd8193FailsWithoutPublishingExcess()
    {
        using (var model = Model(new MemoryStream(Encoding.UTF8.GetBytes(Valid(new string('€', 8192))))))
        {
            var events = await CollectAsync(model);
            Assert.AreEqual(8192, events.OfType<ChatTextDelta>().Sum(item => item.Text.Length));
            Assert.IsInstanceOfType<ChatCompleted>(events[^1]);
        }
        using var over = Model(new MemoryStream(Encoding.UTF8.GetBytes(Chunk(new string('€', 8192)) + Chunk("A") + Finish() + Done)));
        var observed = new List<ChatStreamEvent>();
        var error = await Assert.ThrowsExactlyAsync<ChatModelException>(async () =>
        {
            await foreach (var item in over.StreamAsync(Request)) observed.Add(item);
        });
        Assert.AreEqual(ChatFailure.OutputLimit, error.Failure);
        Assert.AreEqual(8192, observed.OfType<ChatTextDelta>().Sum(item => item.Text.Length));
        Assert.AreEqual(0, observed.OfType<ChatCompleted>().Count());
    }

    [TestMethod]
    public async Task EmptyCleanTerminalIsNotMisrepresentedAsSuccessfulAnswer()
    {
        using var model = Model(new MemoryStream(Encoding.UTF8.GetBytes(Finish() + Done)));
        var failure = await Assert.ThrowsExactlyAsync<ChatModelException>(() => CollectAsync(model));
        Assert.AreEqual(ChatFailure.Protocol, failure.Failure);
    }

    [TestMethod]
    public async Task AmbiguousDuplicateFinishPropertiesCannotHideUnsupportedTermination()
    {
        var forged = "data: {\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"tool_calls\",\"finish_reason\":\"stop\"}]}\r\n\r\n";
        using var model = Model(new MemoryStream(Encoding.UTF8.GetBytes(Chunk("partial") + forged + Done)));
        var failure = await Assert.ThrowsExactlyAsync<ChatModelException>(() => CollectAsync(model));
        Assert.AreEqual(ChatFailure.Protocol, failure.Failure);
    }

    [TestMethod]
    public async Task TextAfterFinishCannotBecomeSuccessEvenWhenDoneFollows()
    {
        using var model = Model(new MemoryStream(Encoding.UTF8.GetBytes(Chunk("first") + Finish() + Chunk("late") + Done)));
        var events = new List<ChatStreamEvent>();
        var failure = await Assert.ThrowsExactlyAsync<ChatModelException>(async () =>
        {
            await foreach (var item in model.StreamAsync(Request)) events.Add(item);
        });
        Assert.AreEqual(ChatFailure.Protocol, failure.Failure);
        Assert.AreEqual("first", string.Concat(events.OfType<ChatTextDelta>().Select(item => item.Text)));
        Assert.AreEqual(0, events.OfType<ChatCompleted>().Count());
    }

    [TestMethod]
    public async Task RequestSnapshotSurvivesCallerListMutationDuringAwaitedSend()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string? captured = null;
        using var handler = new CallbackHandler(async (request, token) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            captured = await request.Content!.ReadAsStringAsync(token);
            return Response(new MemoryStream(Encoding.UTF8.GetBytes(Valid("ok"))));
        });
        using var client = new HttpClient(handler);
        using var model = new LocalChatModel(client, Endpoint, Secret);
        var messages = new List<ChatMessage> { new(ChatRole.User, "before mutation") };
        var run = CollectAsync(model, messages);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        messages[0] = new(ChatRole.User, "after mutation");
        messages.Add(new(ChatRole.System, "untrusted late instruction"));
        release.TrySetResult();
        await run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsNotNull(captured);
        using var json = JsonDocument.Parse(captured);
        var sent = json.RootElement.GetProperty("messages");
        Assert.AreEqual(1, sent.GetArrayLength());
        Assert.AreEqual("before mutation", sent[0].GetProperty("content").GetString());
    }

    [TestMethod]
    public async Task DisposingPartialEnumeratorReleasesBusyGuardForNextRequest()
    {
        using var handler = new CallbackHandler((_, _) => Task.FromResult(
            Response(new MemoryStream(Encoding.UTF8.GetBytes(Valid("first delta"))))));
        using var client = new HttpClient(handler);
        using var model = new LocalChatModel(client, Endpoint, Secret);
        await using (var partial = model.StreamAsync(Request).GetAsyncEnumerator())
        {
            Assert.IsTrue(await partial.MoveNextAsync());
            Assert.IsInstanceOfType<ChatTextDelta>(partial.Current);
        }
        var next = await CollectAsync(model);
        Assert.IsInstanceOfType<ChatCompleted>(next[^1]);
    }

    [TestMethod]
    public async Task InvalidUtf8FailsWithoutLeakingRawBytesOrCompleting()
    {
        var bytes = Encoding.UTF8.GetBytes("data: ").Concat(new byte[] { 0xc0, 0xaf, 13, 10, 13, 10 }).ToArray();
        using var model = Model(new FragmentedStream(bytes, 1));
        var failure = await Assert.ThrowsExactlyAsync<ChatModelException>(() => CollectAsync(model));
        Assert.AreEqual(ChatFailure.Protocol, failure.Failure);
        Assert.IsNull(failure.InnerException);
    }

    private const string Done = "data: [DONE]\r\n\r\n";
    private static string Valid(string text) => Chunk(text) + Finish() + Done;
    private static string Chunk(string text) => "data: " + JsonSerializer.Serialize(new
    {
        choices = new[] { new { index = 0, delta = new { content = text }, finish_reason = (string?)null } }
    }, LiteralUnicode) + "\r\n\r\n";
    private static string Finish() => "data: {\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}\r\n\r\n";
    private static LocalChatModel Model(Stream stream)
    {
        var client = new HttpClient(new CallbackHandler((_, _) => Task.FromResult(Response(stream))));
        return new(client, Endpoint, Secret);
    }
    private static HttpResponseMessage Response(Stream stream)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
        return response;
    }
    private static async Task<List<ChatStreamEvent>> CollectAsync(IChatModel model, IReadOnlyList<ChatMessage>? messages = null)
    {
        var result = new List<ChatStreamEvent>();
        await foreach (var item in model.StreamAsync(messages ?? Request)) result.Add(item);
        return result;
    }
    private sealed class CallbackHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            LocalChatPreflightFixture.Reply(request) is { } preflight ? Task.FromResult(preflight) : send(request, cancellationToken);
    }
    private sealed class FragmentedStream(byte[] bytes, int fragmentSize) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, fragmentSize)], cancellationToken);
    }
}
