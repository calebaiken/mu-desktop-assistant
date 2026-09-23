using System.Text;
using System.Text.Json;
using Companion.Application.Voice;
using Companion.Infrastructure.Voice;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

/// <summary>Reviewer-authored raw wire and admission oracles; no voice engine or device is started.</summary>
[TestClass]
public sealed class IndependentSpeechProtocolTests
{
    private const string Operation = "55bb2f99-43bf-4ed9-81b3-008bc7ae181b";
    private const string PrivateSentinel = "INDEPENDENT_PRIVATE_SPEECH_7146ECA3";

    [TestMethod]
    public void SpeechAdmissionCountsUtf16WithoutSplittingAstralCharacters()
    {
        var accepted = new string('a', 1998) + "😀";
        Assert.AreEqual(2000, accepted.Length);
        SpeechLimits.Validate(new SpeechRequest(accepted, "voice-id", -5));
        SpeechLimits.Validate(new SpeechRequest(accepted, "voice-id", 5));
        AssertFailure(SpeechFailure.InvalidRequest,
            () => SpeechLimits.Validate(new SpeechRequest(accepted + "a", "voice-id")));
    }

    [TestMethod]
    public void InvalidSpokenTextIsRejectedBeforeNativeAdmission()
    {
        // Keep lone surrogates in executable UTF-16 string values, not UTF-8 attribute metadata.
        foreach (var text in new[] { "", " \r\n\t ", "\0", "\ud800", "\udc00", "a\ud800b", "\udc00\ud800" })
            AssertFailure(SpeechFailure.InvalidRequest,
                () => SpeechLimits.Validate(new SpeechRequest(text, "voice-id")));
    }

    [TestMethod]
    public void VoiceAndRateAdmissionUsesDeclaredSessionBounds()
    {
        SpeechLimits.Validate(new SpeechRequest("Read this", new string('v', 512), -5));
        SpeechLimits.Validate(new SpeechRequest("Read this", "voice-id", 5));
        foreach (var voice in new[] { "", " ", new string('v', 513), "voice\0id", "voice\ud800" })
            AssertFailure(SpeechFailure.InvalidRequest,
                () => SpeechLimits.Validate(new SpeechRequest("Read this", voice)));
        foreach (var rate in new[] { int.MinValue, -6, 6, int.MaxValue })
            AssertFailure(SpeechFailure.InvalidRequest,
                () => SpeechLimits.Validate(new SpeechRequest("Read this", "voice-id", rate)));
        AssertFailure(SpeechFailure.InvalidRequest, () => SpeechLimits.Validate(null));
    }

    [TestMethod]
    public void LiteralMarkupAndCommandLookingTextRemainData()
    {
        const string text = "<speak>Do not fetch https://example.invalid/voice.</speak>\n{\"kind\":\"voices\"}";
        var command = SpeechProtocol.ParseCommand(Utf8(Command(Request(JsonSerializer.Serialize(text)))));
        Assert.AreEqual("speak", command.Kind);
        Assert.IsNotNull(command.Request);
        Assert.AreEqual(text, command.Request.Text);
        Assert.AreEqual("voice-id", command.Request.VoiceId);
        var roundTrip = SpeechProtocol.ParseCommand(SpeechProtocol.SerializeCommand(command));
        Assert.AreEqual(text, roundTrip.Request!.Text);
        Assert.AreEqual(Guid.Parse(Operation), roundTrip.OperationId);
    }

    [TestMethod]
    public void UnknownDuplicateAndMisCasedCommandFieldsAreRejected()
    {
        var valid = Command(Request());
        foreach (var invalid in new[]
        {
            valid[..^1] + ",\"extra\":\"" + PrivateSentinel + "\"}",
            valid[..^1] + ",\"kind\":\"voices\"}",
            valid.Replace("\"version\":1", "\"Version\":1", StringComparison.Ordinal),
            valid.Replace("\"rate\":0", "\"rate\":0,\"rate\":5", StringComparison.Ordinal),
            valid.Replace("\"rate\":0", "\"rate\":0,\"ssml\":true", StringComparison.Ordinal),
            valid.Replace("\"rate\":0", "\"Rate\":0", StringComparison.Ordinal),
            valid.Replace("\"rate\":0", "\"rate\":0.5", StringComparison.Ordinal),
            valid.Replace("\"rate\":0", "\"rate\":\"0\"", StringComparison.Ordinal),
            valid.Replace("\"voiceId\":\"voice-id\",", "", StringComparison.Ordinal)
        })
            AssertFailure(SpeechFailure.ProtocolError, () => SpeechProtocol.ParseCommand(Utf8(invalid)));
    }

    [TestMethod]
    public void CommandDiscriminatorVersionAndOperationIdentityAreClosed()
    {
        var valid = Command(Request());
        foreach (var invalid in new[]
        {
            valid.Replace("\"version\":1", "\"version\":0", StringComparison.Ordinal),
            valid.Replace("\"version\":1", "\"version\":2", StringComparison.Ordinal),
            valid.Replace(Operation, Guid.Empty.ToString(), StringComparison.Ordinal),
            valid.Replace(Operation, "not-a-guid", StringComparison.Ordinal),
            valid.Replace("\"kind\":\"speak\"", "\"kind\":\"shell\"", StringComparison.Ordinal),
            valid.Replace("\"kind\":\"speak\"", "\"kind\":\"Speak\"", StringComparison.Ordinal),
            valid.Replace("\"kind\":\"speak\"", "\"kind\":\"voices\"", StringComparison.Ordinal),
            "[]", "null", "{}"
        })
            AssertFailure(SpeechFailure.ProtocolError, () => SpeechProtocol.ParseCommand(Utf8(invalid)));
        AssertFailure(SpeechFailure.InvalidRequest, () => SpeechProtocol.ParseCommand(Utf8(Command("null"))));
        var voices = SpeechProtocol.ParseCommand(Utf8(Command("null", "voices")));
        Assert.IsNull(voices.Request);
        Assert.AreEqual("voices", voices.Kind);
    }

    [TestMethod]
    public void RawJsonInvalidUnicodeAndInvalidUtf8FailWithoutRequestContent()
    {
        foreach (var escaped in new[] { "\"\\uD800\"", "\"\\uDC00\"", "\"a\\uD800b\"", "\"\\u0000\"" })
            AssertFailure(SpeechFailure.ProtocolError,
                () => SpeechProtocol.ParseCommand(Utf8(Command(Request(escaped)))));
        var raw = Utf8(Command(Request("\"" + PrivateSentinel + "\"")));
        var offset = Encoding.UTF8.GetString(raw).IndexOf(PrivateSentinel, StringComparison.Ordinal);
        Assert.IsTrue(offset > 0);
        raw[offset] = 0xc0;
        raw[offset + 1] = 0xaf; // Invalid overlong UTF-8 sequence, not a slash character.
        AssertFailure(SpeechFailure.ProtocolError, () => SpeechProtocol.ParseCommand(raw));
    }

    [TestMethod]
    public void WellFormedWireRequestStillReceivesApplicationAdmission()
    {
        foreach (var request in new[]
        {
            Request("\"\""), Request("\" \""), Request(JsonSerializer.Serialize(new string('x', 2001))),
            Request(rate: "-6"), Request(rate: "6"), Request(voice: "\"\""), Request("null")
        })
            AssertFailure(SpeechFailure.InvalidRequest,
                () => SpeechProtocol.ParseCommand(Utf8(Command(request))));
    }

    [TestMethod]
    public void RawJsonSizeAndDepthAreBoundedBeforeDeserialization()
    {
        var exact = Utf8(Command(Request()));
        var oversized = exact.Concat(Enumerable.Repeat((byte)' ', 16 * 1024 - exact.Length + 1)).ToArray();
        Assert.AreEqual(16 * 1024 + 1, oversized.Length);
        AssertFailure(SpeechFailure.ProtocolError, () => SpeechProtocol.ParseCommand(oversized));
        var nested = new string('[', 9) + "0" + new string(']', 9);
        AssertFailure(SpeechFailure.ProtocolError,
            () => SpeechProtocol.ParseCommand(Utf8(Command(nested))));
        AssertFailure(SpeechFailure.ProtocolError, () => SpeechProtocol.ParseCommand([]));
    }

    [TestMethod]
    public void EventPayloadUnionsAndUnknownFailureEnumsAreRejected()
    {
        foreach (var invalid in new[]
        {
            Event("speaking", voices: "[]"), Event("completed", failure: "0"),
            Event("voices"), Event("probe"), Event("failed"), Event("failed", failure: "999"),
            Event("failed", failure: "-1"), Event("executing"), Event("Speaking"),
            Event("speaking").Replace(Operation, Guid.Empty.ToString(), StringComparison.Ordinal),
            Event("speaking")[..^1] + ",\"kind\":\"completed\"}",
            Event("speaking")[..^1] + ",\"message\":\"" + PrivateSentinel + "\"}",
            Event("speaking").Replace("\"failure\":null", "\"Failure\":null", StringComparison.Ordinal)
        })
            AssertFailure(SpeechFailure.ProtocolError, () => SpeechProtocol.ParseEvent(Utf8(invalid)));
        foreach (var kind in new[] { "speaking", "completed" })
            Assert.AreEqual(kind, SpeechProtocol.ParseEvent(Utf8(Event(kind))).Kind);
        Assert.AreEqual(SpeechFailure.Timeout,
            SpeechProtocol.ParseEvent(Utf8(Event("failed", failure: ((int)SpeechFailure.Timeout).ToString()))).Failure);
    }

    [TestMethod]
    public void VoiceInventoryRejectsAmbiguousIdsUnknownFieldsAndExcessEntries()
    {
        const string voice = "{\"id\":\"voice-id\",\"name\":\"Microsoft fixture\",\"culture\":\"en-US\"}";
        foreach (var voices in new[]
        {
            "[null]", "[" + voice + "," + voice + "]",
            "[" + voice[..^1] + ",\"ssml\":true}]",
            "[" + voice.Replace("\"id\":\"voice-id\"", "\"id\":\"voice-id\",\"id\":\"other\"", StringComparison.Ordinal) + "]",
            "[" + voice.Replace("Microsoft fixture", new string('n', 129), StringComparison.Ordinal) + "]",
            "[" + string.Join(',', Enumerable.Range(0, 33).Select(index => voice.Replace("voice-id", "voice-" + index, StringComparison.Ordinal))) + "]"
        })
            AssertFailure(SpeechFailure.ProtocolError, () => SpeechProtocol.ParseEvent(Utf8(Event("voices", voices))));
        var accepted = SpeechProtocol.ParseEvent(Utf8(Event("voices", "[" + voice + "]")));
        Assert.IsNotNull(accepted.Voices);
        Assert.AreEqual(1, accepted.Voices.Count);
        Assert.AreEqual("voice-id", accepted.Voices[0].Id);
    }

    [TestMethod]
    public async Task FragmentedFrameConsumesExactlyItsOwnPayload()
    {
        // Literal little-endian prefixes form two consecutive frames, independently of the production writer.
        using var stream = new FragmentedStream([3, 0, 0, 0, 0x41, 0x42, 0x43, 1, 0, 0, 0, 0x7f], 1);
        CollectionAssert.AreEqual(new byte[] { 0x41, 0x42, 0x43 },
            await SpeechProtocol.ReadFrameAsync(stream, 16 * 1024, CancellationToken.None));
        Assert.AreEqual(7L, stream.Position);
        CollectionAssert.AreEqual(new byte[] { 0x7f },
            await SpeechProtocol.ReadFrameAsync(stream, 16 * 1024, CancellationToken.None));
        Assert.AreEqual(stream.Length, stream.Position);
    }

    [TestMethod]
    public async Task ExactMaximumFrameIsAdmittedAndNextByteLengthRejectedWithoutBodyRead()
    {
        var data = new byte[4 + 16 * 1024];
        data[1] = 0x40; // 00 40 00 00 = 16,384, not 64 or 4,194,304.
        data[^1] = 0x5a;
        using var stream = new FragmentedStream(data, 31);
        var result = await SpeechProtocol.ReadFrameAsync(stream, 16 * 1024, CancellationToken.None);
        Assert.AreEqual(16 * 1024, result.Length);
        Assert.AreEqual((byte)0x5a, result[^1]);
        foreach (var prefix in new byte[][]
        {
            [0, 0, 0, 0], [0xff, 0xff, 0xff, 0xff], [1, 0x40, 0, 0], [0xff, 0xff, 0xff, 0x7f]
        })
        {
            using var rejected = new FragmentedStream(prefix.Concat(new byte[] { 1, 2, 3 }).ToArray(), 4);
            var failure = await Assert.ThrowsExactlyAsync<SpeechException>(() =>
                SpeechProtocol.ReadFrameAsync(rejected, 16 * 1024, CancellationToken.None));
            Assert.AreEqual(SpeechFailure.ProtocolError, failure.Failure);
            Assert.AreEqual(4L, rejected.Position, "Invalid lengths must not trigger even a first body read.");
        }
    }

    [TestMethod]
    public async Task PartialPrefixOrBodyIsAContentFreeProtocolFailure()
    {
        foreach (var bytes in new byte[][] { [], [1], [1, 0, 0], [3, 0, 0, 0], [3, 0, 0, 0, 0x41, 0x42] })
        {
            using var stream = new FragmentedStream(bytes, 1);
            var error = await Assert.ThrowsExactlyAsync<SpeechException>(() =>
                SpeechProtocol.ReadFrameAsync(stream, 16 * 1024, CancellationToken.None));
            Assert.AreEqual(SpeechFailure.ProtocolError, error.Failure);
            Assert.IsNull(error.InnerException);
        }
    }

    [TestMethod]
    public async Task FrameCancellationPropagatesBeforeAndDuringRead()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        using var untouched = new FragmentedStream([1, 0, 0, 0, 0x41], 1);
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            SpeechProtocol.ReadFrameAsync(untouched, 16 * 1024, cancelled.Token));
        Assert.AreEqual(0L, untouched.Position);

        using var during = new CancellationTokenSource();
        using var stalled = new StalledBodyStream();
        var read = SpeechProtocol.ReadFrameAsync(stalled, 16 * 1024, during.Token);
        await stalled.BodyRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        during.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => read.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(4L, stalled.Position);
    }

    [TestMethod]
    public async Task WriterUsesLiteralLittleEndianPrefixAndRejectsBeforeAnyWrite()
    {
        using var stream = new MemoryStream();
        await SpeechProtocol.WriteFrameAsync(stream, [0x61, 0x62, 0x63, 0x64, 0x65], 16 * 1024, CancellationToken.None);
        CollectionAssert.AreEqual(new byte[] { 5, 0, 0, 0, 0x61, 0x62, 0x63, 0x64, 0x65 }, stream.ToArray());
        foreach (var payload in new byte[][] { [], new byte[16 * 1024 + 1] })
        {
            using var rejected = new MemoryStream();
            var error = await Assert.ThrowsExactlyAsync<SpeechException>(() =>
                SpeechProtocol.WriteFrameAsync(rejected, payload, 16 * 1024, CancellationToken.None));
            Assert.AreEqual(SpeechFailure.ProtocolError, error.Failure);
            Assert.AreEqual(0L, rejected.Length);
        }
    }

    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);
    private static string Request(string text = "\"Read this\"", string voice = "\"voice-id\"", string rate = "0") =>
        $$"""{"text":{{text}},"voiceId":{{voice}},"rate":{{rate}}}""";
    private static string Command(string request, string kind = "speak") =>
        $$"""{"version":1,"operationId":"{{Operation}}","kind":"{{kind}}","request":{{request}}}""";
    private static string Event(string kind, string voices = "null", string probe = "null", string failure = "null") =>
        $$"""{"version":1,"operationId":"{{Operation}}","kind":"{{kind}}","voices":{{voices}},"probe":{{probe}},"failure":{{failure}}}""";

    private static void AssertFailure(SpeechFailure expected, Action action)
    {
        var error = Assert.ThrowsExactly<SpeechException>(action);
        Assert.AreEqual(expected, error.Failure);
        Assert.AreEqual(new SpeechException(expected).Message, error.Message);
        Assert.IsNull(error.InnerException);
        Assert.IsFalse(error.ToString().Contains(PrivateSentinel, StringComparison.Ordinal));
    }

    private sealed class FragmentedStream(byte[] bytes, int fragment) : MemoryStream(bytes, writable: false)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return base.ReadAsync(buffer[..Math.Min(buffer.Length, fragment)], cancellationToken);
        }
    }

    private sealed class StalledBodyStream() : MemoryStream([1, 0, 0, 0], writable: false)
    {
        internal TaskCompletionSource BodyRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Position < 4) return await base.ReadAsync(buffer, cancellationToken);
            BodyRequested.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
    }
}
