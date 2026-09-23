using System.Net;
using System.Text;
using System.Text.Json;

namespace Companion.Tests;

/// <summary>Keep existing SSE tests focused on their independent transport assertions.
/// Context tests provide their own formatter/tokenizer protocol, without this fixture.</summary>
internal static class LocalChatPreflightFixture
{
    internal static HttpResponseMessage? Reply(HttpRequestMessage request) => request.RequestUri?.AbsolutePath switch
    {
        "/apply-template" => Json(new { prompt = "formatted test conversation" }),
        "/tokenize" => Json(new { tokens = new[] { 1, 2, 3 } }),
        _ => null
    };

    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
    };
}
