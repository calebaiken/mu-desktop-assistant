using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Companion.Application.Conversation;
using Companion.Infrastructure.Conversation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

[TestClass]
public sealed class LocalChatContextTests
{
    private const string Token = "context-test-private-session-0001";
    private static readonly Uri Endpoint = new("http://127.0.0.1:49161/v1/chat/completions");
    private static readonly ChatMessage[] Request = [new(ChatRole.User, "Hello")];
    private const string Success = "data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":\"ok\"},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n";

    [TestMethod]
    public async Task Exact1792TokenPromptReserves256AndUsesAuthenticatedMatchingUnicodeTemplate()
    {
        const string unicode = "Cafe\u0301 € 😀 漢字";
        const string formatted = "<|im_start|>user\nCafe\u0301 € 😀 漢字<|im_end|>\n<|im_start|>assistant\n<think>\n\n</think>\n\n";
        var paths = new List<string>();
        using var model = Model(async (request, cancellation) =>
        {
            Assert.AreEqual("127.0.0.1", request.RequestUri!.Host);
            Assert.AreEqual(Endpoint.Port, request.RequestUri.Port);
            Assert.AreEqual("Bearer", request.Headers.Authorization?.Scheme);
            Assert.AreEqual(Token, request.Headers.Authorization?.Parameter);
            var path = request.RequestUri.AbsolutePath;
            paths.Add(path);
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellation));
            if (path == "/tokenize")
            {
                Assert.AreEqual(formatted, json.RootElement.GetProperty("content").GetString());
                Assert.IsTrue(json.RootElement.GetProperty("add_special").GetBoolean());
                Assert.IsTrue(json.RootElement.GetProperty("parse_special").GetBoolean());
                Assert.IsFalse(json.RootElement.GetProperty("with_pieces").GetBoolean());
                return Tokens(1792);
            }
            Assert.AreEqual(unicode, json.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
            Assert.IsFalse(json.RootElement.GetProperty("chat_template_kwargs").GetProperty("enable_thinking").GetBoolean());
            if (path == "/apply-template")
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(new { prompt = formatted });
                return Response(new FragmentedStream(bytes), "application/json");
            }
            Assert.AreEqual(256, json.RootElement.GetProperty("max_tokens").GetInt32());
            return Sse();
        });
        Assert.IsInstanceOfType<ChatCompleted>((await CollectAsync(model, [new(ChatRole.User, unicode)]))[^1]);
        CollectionAssert.AreEqual(new[] { "/apply-template", "/tokenize", "/v1/chat/completions" }, paths);
    }

    [TestMethod]
    public async Task OverBudgetContextDropsOnlyOldestCompletePairsAndNeverMutatesCaller()
    {
        ChatMessage[] input = [new(ChatRole.System, "system"), new(ChatRole.User, "old user"), new(ChatRole.Assistant, "old answer"),
            new(ChatRole.User, "middle user"), new(ChatRole.Assistant, "middle answer"), new(ChatRole.User, "recent user"),
            new(ChatRole.Assistant, "recent answer"), new(ChatRole.User, "current user")];
        var formatted = new List<string[]>();
        string[]? generated = null;
        var counts = new Queue<int>([2600, 1800, 1792]);
        using var model = Model(async (request, cancellation) =>
        {
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellation));
            if (request.RequestUri!.AbsolutePath == "/tokenize") return Tokens(counts.Dequeue());
            var contents = json.RootElement.GetProperty("messages").EnumerateArray().Select(item => item.GetProperty("content").GetString()!).ToArray();
            if (request.RequestUri.AbsolutePath == "/apply-template")
            {
                formatted.Add(contents);
                return Json(new { prompt = string.Join("\n", contents) });
            }
            generated = contents;
            return Sse();
        });
        await CollectAsync(model, input);
        Assert.AreEqual(8, input.Length);
        Assert.AreEqual("old user", input[1].Content);
        CollectionAssert.AreEqual(new[] { 8, 6, 4 }, formatted.Select(item => item.Length).ToArray());
        Assert.IsNotNull(generated);
        CollectionAssert.AreEqual(new[] { "system", "recent user", "recent answer", "current user" }, generated);
        foreach (var snapshot in formatted)
        {
            Assert.AreEqual("system", snapshot[0]);
            Assert.AreEqual("current user", snapshot[^1]);
        }
        Assert.AreEqual(0, counts.Count);
    }

    [TestMethod]
    public async Task SinglePromptAt1793FailsWithoutInferenceAndGuardAllowsNextTurn()
    {
        var count = 1793;
        var generated = 0;
        using var model = Model((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath switch
        {
            "/apply-template" => Json(new { prompt = "same short text, vocabulary decides the budget" }),
            "/tokenize" => Tokens(count),
            _ => Generate()
        }));
        var error = await Assert.ThrowsExactlyAsync<ChatModelException>(() => CollectAsync(model));
        Assert.AreEqual(ChatFailure.InvalidRequest, error.Failure);
        Assert.AreEqual(0, generated);
        count = 1792;
        Assert.IsInstanceOfType<ChatCompleted>((await CollectAsync(model))[^1]);
        Assert.AreEqual(1, generated);
        HttpResponseMessage Generate() { generated++; return Sse(); }
    }

    [TestMethod]
    public async Task InvalidTurnStructuresFailBeforeAnyTransport()
    {
        var calls = 0;
        using var model = Model((_, _) => { calls++; return Task.FromResult(Sse()); });
        ChatMessage[][] cases = [[new(ChatRole.System, "system only")], [new(ChatRole.Assistant, "orphan")],
            [new(ChatRole.User, "first"), new(ChatRole.User, "second")],
            [new(ChatRole.User, "first"), new(ChatRole.Assistant, "answer")],
            [new(ChatRole.System, "one"), new(ChatRole.System, "two"), new(ChatRole.User, "prompt")]];
        foreach (var messages in cases)
            Assert.AreEqual(ChatFailure.InvalidRequest, (await Assert.ThrowsExactlyAsync<ChatModelException>(() => CollectAsync(model, messages))).Failure);
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    public async Task InvalidTemplateAndTokenizerResponsesFailClosedWithoutInference()
    {
        (string Path, string Json)[] cases = [("/apply-template", "{\"prompt\":\"one\",\"prompt\":\"two\"}"),
            ("/apply-template", "{\"prompt\":null}"), ("/apply-template", "{\"prompt\":\"\"}"),
            ("/apply-template", JsonSerializer.Serialize(new { prompt = new string('a', LocalChatModel.MaximumFormattedPromptBytes + 1) })),
            ("/tokenize", "{\"tokens\":[]}"), ("/tokenize", "{\"tokens\":[-1]}"),
            ("/tokenize", "{\"tokens\":[\"1\"]}"), ("/tokenize", "{\"tokens\":[1],\"tokens\":[2]}"),
            ("/tokenize", "{\"tokens\":[2147483648]}"), ("/tokenize", "{\"error\":\"secret raw text\"}")];
        foreach (var item in cases)
        {
            var generated = 0;
            using var model = Model((request, _) =>
            {
                var path = request.RequestUri!.AbsolutePath;
                if (path == item.Path) return Task.FromResult(RawJson(item.Json));
                if (path == "/apply-template") return Task.FromResult(Json(new { prompt = "fixture" }));
                if (path == "/tokenize") return Task.FromResult(Tokens(10));
                generated++;
                return Task.FromResult(Sse());
            });
            var error = await Assert.ThrowsExactlyAsync<ChatModelException>(() => CollectAsync(model));
            Assert.AreEqual(ChatFailure.Protocol, error.Failure, item.Json[..Math.Min(60, item.Json.Length)]);
            Assert.IsFalse(error.ToString().Contains("secret raw text", StringComparison.Ordinal));
            Assert.IsNull(error.InnerException);
            Assert.AreEqual(0, generated);
        }
    }

    [TestMethod]
    public async Task PreflightRejectsOversizedBodyWithOrWithoutContentLength()
    {
        foreach (var knownLength in new[] { true, false })
        {
            var oversized = Encoding.UTF8.GetBytes(new string(' ', LocalChatModel.MaximumPreflightBytes + 1));
            using var model = Model((_, _) => Task.FromResult(Response(
                knownLength ? new MemoryStream(oversized) : new ForwardStream(oversized), "application/json")));
            var error = await Assert.ThrowsExactlyAsync<ChatModelException>(() => CollectAsync(model));
            Assert.AreEqual(ChatFailure.Protocol, error.Failure);
        }
    }

    [TestMethod]
    public async Task PreflightRejectsRedirectedUriAndUnexpectedMediaType()
    {
        foreach (var redirected in new[] { true, false })
        {
            using var model = Model((_, _) =>
            {
                var response = Json(new { prompt = "fixture" });
                if (redirected) response.RequestMessage = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:49162/apply-template");
                else response.Content.Headers.ContentType = new("text/plain");
                return Task.FromResult(response);
            });
            Assert.AreEqual(ChatFailure.Protocol, (await Assert.ThrowsExactlyAsync<ChatModelException>(() => CollectAsync(model))).Failure);
        }
    }

    [TestMethod]
    public async Task CancellationDuringTokenizerBodyDisposesItAndAllowsNextTurn()
    {
        using var cancellation = new CancellationTokenSource();
        var body = new DelayedStream(heartbeat: false);
        var blocked = true;
        using var model = Model((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath switch
        {
            "/apply-template" => Json(new { prompt = "fixture" }),
            "/tokenize" when blocked => Response(body, "application/json"),
            "/tokenize" => Tokens(10),
            _ => Sse()
        }));
        var run = CollectAsync(model, cancellation: cancellation.Token);
        await body.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.AreEqual(ChatFailure.Busy, (await Assert.ThrowsExactlyAsync<ChatModelException>(() => CollectAsync(model))).Failure);
        await cancellation.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.IsTrue(body.WasDisposed);
        blocked = false;
        Assert.IsInstanceOfType<ChatCompleted>((await CollectAsync(model))[^1]);
    }

    [TestMethod]
    public async Task PreflightBodyIdleDeadlineDoesNotWaitForGeneration()
    {
        var body = new DelayedStream(heartbeat: false);
        using var model = Model((_, _) => Task.FromResult(Response(body, "application/json")),
            TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5));
        Assert.AreEqual(ChatFailure.Timeout, (await Assert.ThrowsExactlyAsync<ChatModelException>(() => CollectAsync(model).WaitAsync(TimeSpan.FromSeconds(3)))).Failure);
        Assert.IsTrue(body.WasDisposed);
    }

    [TestMethod]
    public async Task FrequentSseHeartbeatsCannotExtendAbsoluteTurnDeadline()
    {
        var body = new DelayedStream(heartbeat: true);
        using var model = Model((request, _) => Task.FromResult(Preflight(request) ?? Response(body, "text/event-stream")),
            TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(800));
        var elapsed = Stopwatch.StartNew();
        var error = await Assert.ThrowsExactlyAsync<ChatModelException>(() => CollectAsync(model).WaitAsync(TimeSpan.FromSeconds(4)));
        Assert.AreEqual(ChatFailure.Timeout, error.Failure);
        Assert.IsTrue(body.Reads >= 3, "Heartbeats must actually keep the connection active before the total deadline.");
        Assert.IsTrue(elapsed.Elapsed < TimeSpan.FromSeconds(3));
        Assert.IsTrue(body.WasDisposed);
    }

    [TestMethod]
    public async Task WorkerTransportFailureIsSanitizedAndNeverCompletes()
    {
        foreach (var duringPreflight in new[] { true, false })
        {
            using var model = Model((request, _) =>
            {
                if (duringPreflight) throw new HttpRequestException("private worker detail");
                return Task.FromResult(Preflight(request) ?? Response(new FailedStream(), "text/event-stream"));
            });
            var error = await Assert.ThrowsExactlyAsync<ChatModelException>(() => CollectAsync(model));
            Assert.AreEqual(ChatFailure.Unavailable, error.Failure);
            Assert.IsFalse(error.ToString().Contains("private worker detail", StringComparison.Ordinal));
            Assert.IsNull(error.InnerException);
        }
    }

    private static LocalChatModel Model(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send,
        TimeSpan? idle = null, TimeSpan? turn = null) => new(new HttpClient(new Handler(send)), Endpoint, Token, idle, turn);
    private static HttpResponseMessage? Preflight(HttpRequestMessage request) => request.RequestUri!.AbsolutePath switch
    {
        "/apply-template" => Json(new { prompt = "fixture" }), "/tokenize" => Tokens(10), _ => null
    };
    private static HttpResponseMessage Tokens(int count) => Json(new { tokens = Enumerable.Repeat(1, count).ToArray() });
    private static HttpResponseMessage Json(object value) => RawJson(JsonSerializer.Serialize(value));
    private static HttpResponseMessage RawJson(string value) => Response(new MemoryStream(Encoding.UTF8.GetBytes(value)), "application/json");
    private static HttpResponseMessage Sse() => Response(new MemoryStream(Encoding.UTF8.GetBytes(Success)), "text/event-stream");
    private static HttpResponseMessage Response(Stream stream, string type) => new(HttpStatusCode.OK)
    { Content = new StreamContent(stream) { Headers = { ContentType = new(type) } } };
    private static async Task<List<ChatStreamEvent>> CollectAsync(IChatModel model, IReadOnlyList<ChatMessage>? messages = null, CancellationToken cancellation = default)
    {
        var events = new List<ChatStreamEvent>();
        await foreach (var item in model.StreamAsync(messages ?? Request, cancellation)) events.Add(item);
        return events;
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
    private sealed class FragmentedStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => base.ReadAsync(buffer[..1], cancellationToken);
    }
    private sealed class ForwardStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }
    private class DelayedStream(bool heartbeat) : Stream
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Reads { get; private set; }
        public bool WasDisposed { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await Task.Delay(heartbeat ? TimeSpan.FromMilliseconds(10) : Timeout.InfiniteTimeSpan, cancellationToken);
            Reads++;
            ": active\n\n"u8.CopyTo(buffer.Span);
            return 10;
        }
        protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
    private sealed class FailedStream() : DelayedStream(false)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => throw new IOException("private worker detail");
    }
}
