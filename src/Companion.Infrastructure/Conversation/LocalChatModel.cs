using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Companion.Application.Conversation;

namespace Companion.Infrastructure.Conversation;

/// <summary>Streams only from the companion's authenticated, loopback-only model worker.</summary>
public sealed class LocalChatModel : IStructuredChatModel, IDisposable
{
    internal const int MaximumMessageBytes = 32 * 1024;
    internal const int MaximumEventBytes = 64 * 1024;
    internal const int ContextTokens = 2048;
    internal const int MaximumOutputTokens = 256;
    internal const int MaximumPromptTokens = ContextTokens - MaximumOutputTokens;
    internal const int MaximumPreflightBytes = 256 * 1024;
    internal const int MaximumFormattedPromptBytes = 64 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly HttpClient client;
    private readonly Uri endpoint;
    private readonly string sessionToken;
    private readonly TimeSpan idleTimeout;
    private readonly TimeSpan turnTimeout;
    private readonly CancellationTokenSource lifetime = new();
    private int active;
    private int disposed;

    public LocalChatModel(Uri endpoint, string sessionToken)
        : this(CreateClient(), endpoint, sessionToken) { }

    // Injection is restricted to the test assembly; production always creates its own handler.
    internal LocalChatModel(HttpClient client, Uri endpoint, string sessionToken,
        TimeSpan? idleTimeout = null, TimeSpan? turnTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme != "http" ||
            endpoint.Host != "127.0.0.1" || endpoint.Port is < 1 or > 65535 ||
            endpoint.AbsolutePath != "/v1/chat/completions" ||
            endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 ||
            !endpoint.OriginalString.StartsWith($"http://127.0.0.1:{endpoint.Port}/", StringComparison.Ordinal))
            throw new ArgumentException("A literal IPv4 loopback chat endpoint is required.", nameof(endpoint));
        ArgumentNullException.ThrowIfNull(sessionToken);
        if (sessionToken.Length is < 32 or > 128 ||
            sessionToken.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-'))
            throw new ArgumentException("A private local worker session token is required.", nameof(sessionToken));
        this.client = client;
        this.endpoint = endpoint;
        this.sessionToken = sessionToken;
        this.idleTimeout = idleTimeout ?? TimeSpan.FromSeconds(30);
        this.turnTimeout = turnTimeout ?? TimeSpan.FromSeconds(120);
        if (this.idleTimeout <= TimeSpan.Zero || this.turnTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(idleTimeout));
    }

    public IAsyncEnumerable<ChatStreamEvent> StreamAsync(IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken = default) => StreamCoreAsync(messages, null, cancellationToken);

    public IAsyncEnumerable<ChatStreamEvent> StreamJsonAsync(IReadOnlyList<ChatMessage> messages,
        JsonElement schema, CancellationToken cancellationToken = default)
    {
        if (schema.ValueKind != JsonValueKind.Object || schema.GetRawText().Length > 16384)
            throw new ArgumentException("A bounded JSON object schema is required.", nameof(schema));
        return StreamCoreAsync(messages, schema.Clone(), cancellationToken);
    }

    private async IAsyncEnumerable<ChatStreamEvent> StreamCoreAsync(
        IReadOnlyList<ChatMessage> messages,
        JsonElement? schema,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Volatile.Read(ref disposed) != 0) throw new ChatModelException(ChatFailure.Unavailable);
        if (Interlocked.CompareExchange(ref active, 1, 0) != 0) throw new ChatModelException(ChatFailure.Busy);
        try
        {
            var snapshot = SnapshotMessages(messages);
            using var turn = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
            turn.CancelAfter(turnTimeout);
            using var idle = CancellationTokenSource.CreateLinkedTokenSource(turn.Token);
            var requestMessages = await AdmitContextAsync(snapshot, idle, cancellationToken).ConfigureAwait(false);
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            var requestBody = new Dictionary<string, object?>
            {
                ["model"] = "local", ["messages"] = requestMessages, ["stream"] = true, ["max_tokens"] = MaximumOutputTokens,
                ["cache_prompt"] = false, ["temperature"] = schema is null ? 0.7 : 0.0,
                ["top_k"] = 20, ["top_p"] = 0.8, ["min_p"] = 0.0,
                ["presence_penalty"] = schema is null ? 1.5 : 0.0, ["chat_template_kwargs"] = new { enable_thinking = false }
            };
            if (schema is { } constrained) requestBody["response_format"] = new { type = "json_object", schema = constrained };
            request.Content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(requestBody));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            using var response = await SendAsync(request, idle, cancellationToken).ConfigureAwait(false);
            ValidateResponse(response, endpoint, "text/event-stream");
            using var stream = await OpenStreamAsync(response, idle, cancellationToken).ConfigureAwait(false);
            var reader = new BoundedLineReader(stream, this, idle, cancellationToken);
            var data = new StringBuilder();
            var eventBytes = 0;
            var outputBytes = 0;
            var outputCharacters = 0;
            ChatFinishReason? finished = null;
            while (true)
            {
                var line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (line is null) throw new ChatModelException(ChatFailure.Protocol);
                // Reserve CRLF even for LF/CR input, including the event's blank terminator.
                eventBytes += StrictByteCount(line) + 2;
                if (eventBytes > MaximumEventBytes) throw new ChatModelException(ChatFailure.Protocol);
                if (line.Length != 0)
                {
                    if (line.StartsWith(':')) continue;
                    var colon = line.IndexOf(':');
                    var field = colon < 0 ? line : line[..colon];
                    var value = colon < 0 ? "" : line[(colon + 1)..];
                    if (value.StartsWith(' ')) value = value[1..];
                    if (field == "data")
                    {
                        if (data.Length != 0) data.Append('\n');
                        data.Append(value);
                    }
                    continue;
                }
                eventBytes = 0;
                if (data.Length == 0) continue;
                var payload = data.ToString();
                data.Clear();
                if (payload == "[DONE]")
                {
                    if (finished is null || outputCharacters == 0) throw new ChatModelException(ChatFailure.Protocol);
                    yield return new ChatCompleted(finished.Value);
                    yield break;
                }
                var parsed = ParseChunk(payload);
                if (parsed.Text.Length != 0)
                {
                    if (finished is not null) throw new ChatModelException(ChatFailure.Protocol);
                    outputBytes += StrictByteCount(parsed.Text);
                    outputCharacters += parsed.Text.Length;
                    if (outputBytes > MaximumMessageBytes || outputCharacters > ChatMessage.MaximumCharacters)
                        throw new ChatModelException(ChatFailure.OutputLimit);
                    yield return new ChatTextDelta(parsed.Text);
                }
                if (parsed.Finish is not null)
                {
                    if (finished is not null) throw new ChatModelException(ChatFailure.Protocol);
                    finished = parsed.Finish;
                }
            }
        }
        finally { Interlocked.Exchange(ref active, 0); }
    }

    private sealed record WireMessage(string role, string content);

    private static WireMessage[] SnapshotMessages(IReadOnlyList<ChatMessage> messages)
    {
        if (messages is null || messages.Count is < 1 or > 32)
            throw new ChatModelException(ChatFailure.InvalidRequest);
        var result = new WireMessage[messages.Count];
        var bytes = 0;
        var expected = ChatRole.User;
        for (var index = 0; index < result.Length; index++)
        {
            var message = messages[index];
            if (message is null) throw new ChatModelException(ChatFailure.InvalidRequest);
            if (!(index == 0 && message.Role == ChatRole.System))
            {
                if (message.Role != expected) throw new ChatModelException(ChatFailure.InvalidRequest);
                expected = expected == ChatRole.User ? ChatRole.Assistant : ChatRole.User;
            }
            try { bytes += StrictUtf8.GetByteCount(message.Content); }
            catch (EncoderFallbackException) { throw new ChatModelException(ChatFailure.InvalidRequest); }
            if (bytes > MaximumMessageBytes) throw new ChatModelException(ChatFailure.InvalidRequest);
            result[index] = new(message.Role.ToString().ToLowerInvariant(), message.Content);
        }
        if (result[^1].role != "user") throw new ChatModelException(ChatFailure.InvalidRequest);
        return result;
    }

    private async Task<WireMessage[]> AdmitContextAsync(WireMessage[] messages,
        CancellationTokenSource idle, CancellationToken caller)
    {
        // b10964 /apply-template and /v1/chat/completions both call
        // oaicompat_chat_params_parse. Completion tokenization uses add_special=true,
        // parse_special=true; use the same flags, template kwargs and actual worker vocabulary.
        // https://github.com/ggml-org/llama.cpp/blob/b10964/tools/server/server-context.cpp
        var systemCount = messages[0].role == "system" ? 1 : 0;
        while (true)
        {
            using var formatted = await PostJsonAsync("/apply-template", new
            {
                model = "local", messages, chat_template_kwargs = new { enable_thinking = false }
            }, idle, caller).ConfigureAwait(false);
            string prompt;
            try
            {
                prompt = formatted.RootElement.GetProperty("prompt").GetString()
                    ?? throw new ChatModelException(ChatFailure.Protocol);
                if (prompt.Length == 0 || StrictByteCount(prompt) > MaximumFormattedPromptBytes)
                    throw new ChatModelException(ChatFailure.Protocol);
            }
            catch (Exception error) when (error is InvalidOperationException or KeyNotFoundException)
            { throw new ChatModelException(ChatFailure.Protocol); }

            using var tokenized = await PostJsonAsync("/tokenize", new
            {
                content = prompt, add_special = true, parse_special = true, with_pieces = false
            }, idle, caller).ConfigureAwait(false);
            var count = CountTokens(tokenized.RootElement);
            if (count <= MaximumPromptTokens) return messages;
            // Preserve the optional system instruction and newest user prompt. A historical
            // turn is evicted only as a complete user/assistant pair, never partial text.
            if (messages.Length - systemCount <= 1)
                throw new ChatModelException(ChatFailure.InvalidRequest);
            messages = messages.Take(systemCount).Concat(messages.Skip(systemCount + 2)).ToArray();
        }
    }

    private static int CountTokens(JsonElement root)
    {
        try
        {
            var tokens = root.GetProperty("tokens");
            if (tokens.ValueKind != JsonValueKind.Array || tokens.GetArrayLength() is < 1 or > MaximumFormattedPromptBytes)
                throw new ChatModelException(ChatFailure.Protocol);
            foreach (var token in tokens.EnumerateArray())
                if (!token.TryGetInt32(out var id) || id < 0)
                    throw new ChatModelException(ChatFailure.Protocol);
            return tokens.GetArrayLength();
        }
        catch (Exception error) when (error is InvalidOperationException or KeyNotFoundException)
        { throw new ChatModelException(ChatFailure.Protocol); }
    }

    private async Task<JsonDocument> PostJsonAsync(string path, object payload,
        CancellationTokenSource idle, CancellationToken caller)
    {
        var target = new Uri(endpoint, path);
        using var request = new HttpRequestMessage(HttpMethod.Post, target);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var body = JsonSerializer.SerializeToUtf8Bytes(payload);
        // JSON escapes can expand a bounded UTF8 prompt; still impose a wire-size ceiling.
        if (body.Length > 512 * 1024) throw new ChatModelException(ChatFailure.InvalidRequest);
        request.Content = new ByteArrayContent(body);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var response = await SendAsync(request, idle, caller).ConfigureAwait(false);
        ValidateResponse(response, target, "application/json");
        if (response.Content.Headers.ContentLength is > MaximumPreflightBytes)
            throw new ChatModelException(ChatFailure.Protocol);
        using var stream = await OpenStreamAsync(response, idle, caller).ConfigureAwait(false);
        using var data = new MemoryStream();
        var buffer = new byte[4096];
        while (true)
        {
            int count;
            try
            {
                idle.CancelAfter(idleTimeout);
                idle.Token.ThrowIfCancellationRequested();
                count = await stream.ReadAsync(buffer, idle.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw CancellationFailure(caller); }
            catch (Exception error) when (error is HttpRequestException or IOException or ObjectDisposedException)
            { throw new ChatModelException(ChatFailure.Unavailable); }
            if (count == 0) break;
            if (data.Length + count > MaximumPreflightBytes) throw new ChatModelException(ChatFailure.Protocol);
            data.Write(buffer, 0, count);
        }
        JsonDocument? document = null;
        try
        {
            document = JsonDocument.Parse(data.GetBuffer().AsMemory(0, (int)data.Length), new JsonDocumentOptions { MaxDepth = 16 });
            RejectDuplicateProperties(document.RootElement);
            if (document.RootElement.ValueKind != JsonValueKind.Object || document.RootElement.TryGetProperty("error", out _))
                throw new ChatModelException(ChatFailure.Protocol);
            return document;
        }
        catch (JsonException) { document?.Dispose(); throw new ChatModelException(ChatFailure.Protocol); }
        catch { document?.Dispose(); throw; }
    }

    private static void ValidateResponse(HttpResponseMessage response, Uri expected, string mediaType)
    {
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge or HttpStatusCode.UnprocessableEntity)
            throw new ChatModelException(ChatFailure.InvalidRequest);
        if (!response.IsSuccessStatusCode) throw new ChatModelException(ChatFailure.Unavailable);
        if (response.RequestMessage?.RequestUri is { } actual && actual != expected)
            throw new ChatModelException(ChatFailure.Protocol);
        if (response.Content.Headers.ContentType?.MediaType != mediaType)
            throw new ChatModelException(ChatFailure.Protocol);
    }

    private static (string Text, ChatFinishReason? Finish) ParseChunk(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 16 });
            var root = document.RootElement;
            RejectDuplicateProperties(root);
            if (root.ValueKind != JsonValueKind.Object || root.TryGetProperty("error", out _))
                throw new ChatModelException(ChatFailure.Protocol);
            var choices = root.GetProperty("choices");
            if (choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() > 1)
                throw new ChatModelException(ChatFailure.Protocol);
            if (choices.GetArrayLength() == 0)
            {
                if (!root.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
                    throw new ChatModelException(ChatFailure.Protocol);
                return ("", null);
            }
            var choice = choices[0];
            if (choice.GetProperty("index").GetInt32() != 0) throw new ChatModelException(ChatFailure.Protocol);
            var delta = choice.GetProperty("delta");
            if (delta.ValueKind != JsonValueKind.Object || delta.TryGetProperty("tool_calls", out _) || delta.TryGetProperty("function_call", out _))
                throw new ChatModelException(ChatFailure.Protocol);
            if (delta.TryGetProperty("role", out var role) && role.GetString() != "assistant")
                throw new ChatModelException(ChatFailure.Protocol);
            var text = delta.TryGetProperty("content", out var content) && content.ValueKind != JsonValueKind.Null
                ? content.GetString() ?? "" : "";
            ChatFinishReason? finish = null;
            if (choice.TryGetProperty("finish_reason", out var reason) && reason.ValueKind != JsonValueKind.Null)
                finish = reason.GetString() switch
                {
                    "stop" => ChatFinishReason.Stop,
                    "length" => ChatFinishReason.Length,
                    _ => throw new ChatModelException(ChatFailure.Protocol)
                };
            return (text, finish);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        { throw new ChatModelException(ChatFailure.Protocol); }
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new ChatModelException(ChatFailure.Protocol);
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) RejectDuplicateProperties(item);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationTokenSource idle, CancellationToken caller)
    {
        try
        {
            idle.CancelAfter(idleTimeout);
            return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, idle.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw CancellationFailure(caller); }
        catch (Exception error) when (error is HttpRequestException or IOException or ObjectDisposedException)
        { throw new ChatModelException(ChatFailure.Unavailable); }
    }

    private async Task<Stream> OpenStreamAsync(HttpResponseMessage response,
        CancellationTokenSource idle, CancellationToken caller)
    {
        try
        {
            idle.CancelAfter(idleTimeout);
            return await response.Content.ReadAsStreamAsync(idle.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw CancellationFailure(caller); }
        catch (Exception error) when (error is HttpRequestException or IOException or ObjectDisposedException)
        { throw new ChatModelException(ChatFailure.Unavailable); }
    }

    private Exception CancellationFailure(CancellationToken caller)
    {
        caller.ThrowIfCancellationRequested();
        return new ChatModelException(Volatile.Read(ref disposed) != 0 ? ChatFailure.Unavailable : ChatFailure.Timeout);
    }

    private static int StrictByteCount(string value)
    {
        try { return StrictUtf8.GetByteCount(value); }
        catch (EncoderFallbackException) { throw new ChatModelException(ChatFailure.Protocol); }
    }

    private static HttpClient CreateClient() => new(new SocketsHttpHandler
    {
        UseProxy = false, AllowAutoRedirect = false, UseCookies = false,
        AutomaticDecompression = DecompressionMethods.None,
        ConnectTimeout = TimeSpan.FromSeconds(10), MaxConnectionsPerServer = 1,
        MaxResponseHeadersLength = 8
    }) { Timeout = Timeout.InfiniteTimeSpan };

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        lifetime.Cancel();
        client.Dispose();
        // Keep the tiny CTS alive: an enumerator can race Dispose before linking its token.
    }

    private sealed class BoundedLineReader(Stream stream, LocalChatModel owner,
        CancellationTokenSource idle, CancellationToken caller)
    {
        private readonly byte[] buffer = new byte[4096];
        private readonly byte[] lineBuffer = new byte[MaximumEventBytes];
        private int position;
        private int length;
        private bool skipLf;

        public async Task<string?> ReadLineAsync()
        {
            var count = 0;
            while (true)
            {
                if (position == length)
                {
                    try
                    {
                        idle.CancelAfter(owner.idleTimeout);
                        length = await stream.ReadAsync(buffer, idle.Token).ConfigureAwait(false);
                        position = 0;
                    }
                    catch (OperationCanceledException) { throw owner.CancellationFailure(caller); }
                    catch (Exception error) when (error is HttpRequestException or IOException or ObjectDisposedException)
                    { throw new ChatModelException(ChatFailure.Unavailable); }
                    if (length == 0) return null; // Unterminated final lines never constitute success.
                }
                if (idle.IsCancellationRequested) throw owner.CancellationFailure(caller);
                var value = buffer[position++];
                if (skipLf)
                {
                    skipLf = false;
                    if (value == '\n') continue;
                }
                if (value is (byte)'\r' or (byte)'\n')
                {
                    skipLf = value == '\r';
                    try { return StrictUtf8.GetString(lineBuffer, 0, count); }
                    catch (DecoderFallbackException) { throw new ChatModelException(ChatFailure.Protocol); }
                }
                if (count == lineBuffer.Length) throw new ChatModelException(ChatFailure.Protocol);
                lineBuffer[count++] = value;
            }
        }
    }
}
