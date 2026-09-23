using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Companion.Application.Conversation;
using Companion.Infrastructure.Conversation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

[TestClass]
public sealed class LocalChatModelTests
{
    private static readonly Uri Endpoint = new("http://127.0.0.1:49123/v1/chat/completions");
    private const string Token = "0123456789abcdef0123456789abcdef";
    private static readonly ChatMessage[] Messages = [new(ChatRole.System, "Be concise."), new(ChatRole.User, "Hello")];
    private const string CompletedStream = "data: {\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n";
    private const string SuccessfulStream = "data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":\"Answer\"},\"finish_reason\":null}]}\n\n" + CompletedStream;

    [TestMethod]
    public async Task StreamsFragmentedUtf8AndSendsOnlyBoundedLocalRequest()
    {
        string? requestBody = null;
        var payload = ": heartbeat\r\n\r\ndata: {\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"content\":\"Hello 🌎\"},\"finish_reason\":null}]}\r\n\r\n" + CompletedStream;
        using var client = new HttpClient(new Handler(async (request, cancellation) =>
        {
            Assert.AreEqual(Endpoint, request.RequestUri);
            Assert.AreEqual("Bearer", request.Headers.Authorization?.Scheme);
            Assert.AreEqual(Token, request.Headers.Authorization?.Parameter);
            requestBody = await request.Content!.ReadAsStringAsync(cancellation);
            return Reply(new FragmentedStream(Encoding.UTF8.GetBytes(payload), 1));
        }));
        using var model = new LocalChatModel(client, Endpoint, Token);
        var events = await CollectAsync(model);
        Assert.AreEqual(2, events.Count);
        Assert.AreEqual(new ChatTextDelta("Hello 🌎"), events[0]);
        Assert.AreEqual(new ChatCompleted(ChatFinishReason.Stop), events[1]);
        using var document = JsonDocument.Parse(requestBody!);
        var root = document.RootElement;
        Assert.IsTrue(root.GetProperty("stream").GetBoolean());
        Assert.AreEqual(256, root.GetProperty("max_tokens").GetInt32());
        Assert.IsFalse(root.GetProperty("cache_prompt").GetBoolean());
        Assert.IsFalse(root.GetProperty("chat_template_kwargs").GetProperty("enable_thinking").GetBoolean());
        Assert.AreEqual("system", root.GetProperty("messages")[0].GetProperty("role").GetString());
        Assert.AreEqual("Hello", root.GetProperty("messages")[1].GetProperty("content").GetString());
    }

    [TestMethod]
    public async Task LengthFinishAndUsageTrailerRemainAValidCompletion()
    {
        var stream = "data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":\"bounded\"},\"finish_reason\":\"length\"}]}\n\n" +
                     "data: {\"choices\":[],\"usage\":{\"completion_tokens\":256}}\n\ndata: [DONE]\n\n";
        using var model = Model(stream);
        CollectionAssert.AreEqual(new ChatStreamEvent[] { new ChatTextDelta("bounded"), new ChatCompleted(ChatFinishReason.Length) },
            await CollectAsync(model));
    }

    [TestMethod]
    public async Task TruncatedAndMalformedStreamsNeverProduceCompletion()
    {
        foreach (var stream in new[]
        {
            "", "data: [DONE]\n\n", "data: {secret prompt invalid JSON}\n\n", "data: {\"choices\":[]}\n\n",
            CompletedStream[..CompletedStream.IndexOf("data: [DONE]", StringComparison.Ordinal)],
            "data: {\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"tool_calls\"}]}\n\ndata: [DONE]\n\n"
        })
        {
            using var model = Model(stream);
            var error = await Assert.ThrowsAsync<ChatModelException>(() => CollectAsync(model));
            Assert.AreEqual(ChatFailure.Protocol, error.Failure);
            Assert.IsFalse(error.ToString().Contains("secret prompt", StringComparison.Ordinal));
            Assert.IsNull(error.InnerException);
        }
    }

    [TestMethod]
    public async Task HttpRequestFailuresContainNoResponseOrPromptDetails()
    {
        foreach (var status in new[] { HttpStatusCode.BadRequest, HttpStatusCode.RequestEntityTooLarge, HttpStatusCode.InternalServerError, HttpStatusCode.Unauthorized, HttpStatusCode.Redirect })
        {
            using var client = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(status)
            { Content = new StringContent("private text must not escape") })));
            using var model = new LocalChatModel(client, Endpoint, Token);
            var error = await Assert.ThrowsAsync<ChatModelException>(() => CollectAsync(model));
            Assert.AreEqual(status is HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge ? ChatFailure.InvalidRequest : ChatFailure.Unavailable, error.Failure);
            Assert.IsFalse(error.ToString().Contains("private text", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public async Task CallerCancellationBeforeSendDoesNotReachTransport()
    {
        var calls = 0;
        using var client = new HttpClient(new Handler((_, _) =>
        {
            calls++;
            return Task.FromResult(Reply(new MemoryStream(Encoding.UTF8.GetBytes(CompletedStream))));
        }));
        using var model = new LocalChatModel(client, Endpoint, Token);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => CollectAsync(model, cancellation.Token));
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    public async Task CancellationWhileAwaitingHeadersReleasesTheSingleTurnGuard()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var client = new HttpClient(new Handler(async (_, token) =>
        {
            if (++calls == 1)
            {
                started.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            return Reply(new MemoryStream(Encoding.UTF8.GetBytes(SuccessfulStream)));
        }));
        using var model = new LocalChatModel(client, Endpoint, Token);
        using var cancellation = new CancellationTokenSource();
        var first = CollectAsync(model, cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var busy = await Assert.ThrowsAsync<ChatModelException>(() => CollectAsync(model));
        Assert.AreEqual(ChatFailure.Busy, busy.Failure);
        await cancellation.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => first);
        Assert.AreEqual(new ChatCompleted(ChatFinishReason.Stop), (await CollectAsync(model)).Last());
    }

    [TestMethod]
    public async Task IdleTimeoutCancelsAStalledBodyAndDisposesIt()
    {
        var stalled = new StalledStream();
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Reply(stalled))));
        using var model = new LocalChatModel(client, Endpoint, Token, TimeSpan.FromMilliseconds(50), TimeSpan.FromSeconds(5));
        var error = await Assert.ThrowsAsync<ChatModelException>(() => CollectAsync(model).WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.AreEqual(ChatFailure.Timeout, error.Failure);
        Assert.IsTrue(stalled.WasDisposed);
    }

    [TestMethod]
    public async Task DisposingModelCancelsActiveBodyAsUnavailable()
    {
        var stalled = new StalledStream();
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Reply(stalled))));
        using var model = new LocalChatModel(client, Endpoint, Token);
        var collection = CollectAsync(model);
        await stalled.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        model.Dispose();
        var error = await Assert.ThrowsAsync<ChatModelException>(() => collection.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.AreEqual(ChatFailure.Unavailable, error.Failure);
        Assert.IsTrue(stalled.WasDisposed);
    }

    [TestMethod]
    public async Task InputAndOutputLimitsRejectBeforeSuccess()
    {
        var calls = 0;
        using var client = new HttpClient(new Handler((_, _) =>
        {
            calls++;
            return Task.FromResult(Reply(new MemoryStream(Encoding.UTF8.GetBytes(CompletedStream))));
        }));
        using var model = new LocalChatModel(client, Endpoint, Token);
        foreach (var input in new[]
        {
            Array.Empty<ChatMessage>(), Enumerable.Repeat(new ChatMessage(ChatRole.User, "a"), 33).ToArray(),
            Enumerable.Repeat(new ChatMessage(ChatRole.User, new string('界', 3000)), 4).ToArray(),
            new[] { new ChatMessage(ChatRole.User, "\ud800") }
        })
        {
            var error = await Assert.ThrowsAsync<ChatModelException>(() => CollectAsync(model, messages: input));
            Assert.AreEqual(ChatFailure.InvalidRequest, error.Failure);
        }
        Assert.AreEqual(0, calls);
        using var overflowing = Model("data: " + JsonSerializer.Serialize(new { choices = new[] { new { index = 0, delta = new { content = new string('a', 8193) }, finish_reason = "stop" } } }) + "\n\ndata: [DONE]\n\n");
        var overflow = await Assert.ThrowsAsync<ChatModelException>(() => CollectAsync(overflowing));
        Assert.AreEqual(ChatFailure.OutputLimit, overflow.Failure);
    }

    [TestMethod]
    public void MessagesAndEndpointsRejectInvalidLocalConfiguration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChatMessage((ChatRole)20, "text"));
        Assert.Throws<ArgumentException>(() => new ChatMessage(ChatRole.User, ""));
        Assert.Throws<ArgumentException>(() => new ChatMessage(ChatRole.User, new string('a', 8193)));
        foreach (var endpoint in new[]
        {
            "https://127.0.0.1:1234/v1/chat/completions", "http://localhost:1234/v1/chat/completions",
            "http://[::1]:1234/v1/chat/completions", "http://127.1:1234/v1/chat/completions",
            "http://127.0.0.1:1234/v1/chat/completions?private=1", "http://name@127.0.0.1:1234/v1/chat/completions",
            "http://127.0.0.1:1234/v1/chat/completions#fragment", "http://example.com:1234/v1/chat/completions"
        })
        {
            using var client = new HttpClient();
            Assert.Throws<ArgumentException>(() => new LocalChatModel(client, new Uri(endpoint), Token));
        }
        using var invalidTokenClient = new HttpClient();
        Assert.Throws<ArgumentException>(() => new LocalChatModel(invalidTokenClient, Endpoint, "secret\r\nheader"));
    }

    private static LocalChatModel Model(string payload) => new(
        new HttpClient(new Handler((_, _) => Task.FromResult(Reply(new MemoryStream(Encoding.UTF8.GetBytes(payload)))))), Endpoint, Token);

    private static HttpResponseMessage Reply(Stream stream)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
        return response;
    }

    private static async Task<List<ChatStreamEvent>> CollectAsync(IChatModel model, CancellationToken cancellation = default, IReadOnlyList<ChatMessage>? messages = null)
    {
        var result = new List<ChatStreamEvent>();
        await foreach (var item in model.StreamAsync(messages ?? Messages, cancellation)) result.Add(item);
        return result;
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            LocalChatPreflightFixture.Reply(request) is { } preflight ? Task.FromResult(preflight) : send(request, cancellationToken);
    }

    private sealed class FragmentedStream(byte[] data, int maximumRead) : MemoryStream(data)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => base.ReadAsync(buffer[..Math.Min(buffer.Length, maximumRead)], cancellationToken);
    }

    private sealed class StalledStream : Stream
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool WasDisposed { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
        protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
