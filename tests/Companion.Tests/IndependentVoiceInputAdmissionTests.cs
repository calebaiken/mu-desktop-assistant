using Companion.Application.Voice;
using Companion.Infrastructure.VoiceInput;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

/// <summary>Reviewer admission/ownership checks; never starts a process, native DLL or microphone.</summary>
[TestClass]
public sealed class IndependentVoiceInputAdmissionTests
{
    [TestMethod]
    public void WorkerAdmissionAllowsOnlyTheCurrentExecutableAndThreeLiteralModes()
    {
        foreach (var role in new[] { "--capture-worker", "--recognition-worker", "--voice-fixture-worker" })
        {
            VoiceWorkerProcess.ValidateLaunch(Environment.ProcessPath!, role, 64);
            VoiceWorkerProcess.ValidateLaunch(Environment.ProcessPath!, role, 1024);
        }
        foreach (var role in new[] { "", "--speech-worker", "--capture-worker extra", "--recognition-worker\0", "--voice-fixture-worker --data-dir private" })
            AssertInputFailure(AudioInputFailure.InvalidRequest,
                () => VoiceWorkerProcess.ValidateLaunch(Environment.ProcessPath!, role, 1024));
    }

    [TestMethod]
    public void WorkerAdmissionRejectsForeignRemoteRelativeAndControlBearingPaths()
    {
        var foreign = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath!)!, "other-input-worker.exe");
        foreach (var path in new[] { "", " ", "worker.exe", @"\\server\share\worker.exe", "//server/share/worker.exe",
                     foreign, Environment.ProcessPath! + "\n", Environment.ProcessPath! + "\0" })
        {
            var error = AssertInputFailure(AudioInputFailure.InvalidRequest,
                () => VoiceWorkerProcess.ValidateLaunch(path, "--capture-worker", 1024));
            if (!string.IsNullOrWhiteSpace(path)) Assert.IsFalse(error.ToString().Contains(path, StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public void WorkerMemoryAdmissionDoesNotPermitUnboundedOrOversizedJobs()
    {
        foreach (var memory in new uint[] { 0, 1, 63, 1025, uint.MaxValue })
            AssertInputFailure(AudioInputFailure.InvalidRequest,
                () => VoiceWorkerProcess.ValidateLaunch(Environment.ProcessPath!, "--recognition-worker", memory));
    }

    [TestMethod]
    public void NativeEnvironmentHasClosedSortedNamesAndDoubleNullTermination()
    {
        var block = VoiceWorkerProcess.BuildEnvironmentBlock();
        Assert.IsTrue(block.EndsWith("\0\0", StringComparison.Ordinal));
        var entries = block[..^2].Split('\0');
        Assert.IsTrue(entries.All(entry => entry.IndexOf('=') > 0));
        var names = entries.Select(entry => entry[..entry.IndexOf('=')]).ToArray();
        string[] allowed = ["APPDATA", "LOCALAPPDATA", "PATH", "PROGRAMDATA", "SystemRoot", "TEMP", "TMP", "USERPROFILE", "WINDIR"];
        CollectionAssert.AreEquivalent(allowed, names);
        Assert.AreEqual(names.Length, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        CollectionAssert.AreEqual(names.Order(StringComparer.OrdinalIgnoreCase).ToArray(), names);
        Assert.IsFalse(names.Any(name => name.Contains("PROXY", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("KEY", StringComparison.OrdinalIgnoreCase) || name.StartsWith("DOTNET", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("COR_", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void PcmDurationUsesActualValidSamplesRatherThanBackingCapacity()
    {
        var owned = new byte[2048];
        Array.Fill(owned, (byte)0x6D);
        using var audio = Pcm16Audio.TakeOwnership(owned, 640);
        Assert.AreEqual(640, audio.ByteLength);
        Assert.AreEqual(320, audio.SampleCount);
        Assert.AreEqual(TimeSpan.FromMilliseconds(20), audio.Duration);
        Assert.AreEqual(640, audio.Bytes.Length);
        Assert.IsFalse(audio.DurationLimited);
        // Retain this alias solely as a reviewer clearing oracle, never as a production owner.
        audio.Dispose();
        Assert.IsTrue(owned.All(value => value == 0), "Unused backing capacity must also be cleared.");
    }

    [TestMethod]
    public void ExactThirtySecondAudioIsAdmittedWithoutAHiddenUnitConversion()
    {
        using var audio = Pcm16Audio.TakeOwnership(new byte[960000], 960000, durationLimited: true);
        Assert.AreEqual(480000, audio.SampleCount);
        Assert.AreEqual(TimeSpan.FromSeconds(30), audio.Duration);
        Assert.IsTrue(audio.DurationLimited);
        Assert.AreEqual(960000, audio.Bytes.Length);
    }

    [TestMethod]
    public void InvalidPcmOwnershipClearsAllTransferredBytesBeforeRejecting()
    {
        foreach (var (capacity, valid) in new[] { (8, -1), (8, 9), (8, 7), (960002, 960002), (960002, 2) })
        {
            var owned = new byte[capacity];
            Array.Fill(owned, (byte)0xC3);
            AssertInputFailure(AudioInputFailure.InvalidRequest, () => Pcm16Audio.TakeOwnership(owned, valid));
            Assert.IsTrue(owned.All(value => value == 0));
        }
    }

    [TestMethod]
    public void EmptyPcmHasZeroDurationAndCannotBeReadAfterDisposal()
    {
        var audio = Pcm16Audio.TakeOwnership([], 0);
        Assert.AreEqual(0, audio.SampleCount);
        Assert.AreEqual(TimeSpan.Zero, audio.Duration);
        Assert.AreEqual(0, audio.Bytes.Length);
        audio.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = audio.Bytes);
        audio.Dispose();
    }

    [TestMethod]
    public async Task ConcurrentPcmDisposalClearsRetainedMemoryAndIsIdempotent()
    {
        var owned = new byte[1024];
        Array.Fill(owned, (byte)0xD7);
        var audio = Pcm16Audio.TakeOwnership(owned, owned.Length);
        var previouslyReturned = audio.Bytes;
        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(audio.Dispose)));
        Assert.IsTrue(previouslyReturned.ToArray().All(value => value == 0));
        Assert.IsTrue(owned.All(value => value == 0));
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = audio.Bytes);
    }

    [TestMethod]
    public void CaptureAdmissionRejectsMissingIdentityAndMalformedEndpointText()
    {
        var id = Guid.Parse("95153365-4035-467b-8b56-319da7df21d8");
        AudioInputLimits.Validate(new AudioCaptureRequest(new string('a', 510) + "\U0001F642", id));
        foreach (var text in new[] { "", " ", "device\0", "device\r", "\uD800", "\uDC00", new string('a', 513) })
            AssertInputFailure(AudioInputFailure.InvalidRequest, () => AudioInputLimits.Validate(new AudioCaptureRequest(text, id)));
        AssertInputFailure(AudioInputFailure.InvalidRequest, () => AudioInputLimits.Validate(new AudioCaptureRequest("device", Guid.Empty)));
        AssertInputFailure(AudioInputFailure.InvalidRequest, () => AudioInputLimits.Validate((AudioCaptureRequest?)null));
    }

    [TestMethod]
    public void RecognitionAdmissionPreservesUnicodeAndRejectsAmbiguousSpeechStates()
    {
        AudioInputLimits.Validate(new SpeechRecognitionResult(new string('a', 1998) + "\U0001F642", true));
        AudioInputLimits.Validate(new SpeechRecognitionResult(string.Empty, false));
        foreach (var result in new[]
                 {
                     new SpeechRecognitionResult(new string('a', 1999) + "\U0001F642", true),
                     new SpeechRecognitionResult("\uD800", true), new SpeechRecognitionResult("\uDC00", true),
                     new SpeechRecognitionResult("private\0text", true), new SpeechRecognitionResult("", true),
                     new SpeechRecognitionResult(" ", true), new SpeechRecognitionResult("recognized words", false),
                     new SpeechRecognitionResult(null!, false)
                 })
            AssertInputFailure(AudioInputFailure.ProtocolError, () => AudioInputLimits.Validate(result));
        AssertInputFailure(AudioInputFailure.ProtocolError, () => AudioInputLimits.Validate((SpeechRecognitionResult?)null));
    }

    [TestMethod]
    public void AudioAndTranscriptStringRepresentationsDoNotExposePayload()
    {
        const string sentinel = "PRIVATE_VOICE_INPUT_TEST_CONTENT";
        var result = new SpeechRecognitionResult(sentinel, true);
        using var audio = Pcm16Audio.TakeOwnership(System.Text.Encoding.Unicode.GetBytes(sentinel), sentinel.Length * 2);
        Assert.IsFalse(result.ToString().Contains(sentinel, StringComparison.Ordinal));
        Assert.IsFalse(audio.ToString().Contains(sentinel, StringComparison.Ordinal));
        Assert.IsFalse(audio.ToString().Contains(Convert.ToBase64String(audio.Bytes.Span), StringComparison.Ordinal));
    }

    private static AudioInputException AssertInputFailure(AudioInputFailure failure, Action action)
    {
        var error = Assert.ThrowsExactly<AudioInputException>(action);
        Assert.AreEqual(failure, error.Failure);
        Assert.AreEqual($"Local voice input failed: {failure}.", error.Message);
        Assert.IsNull(error.InnerException);
        return error;
    }
}
