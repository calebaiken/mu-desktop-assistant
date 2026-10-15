using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Companion.Application.Voice;
using Companion.Infrastructure.MachineLearning;
using Companion.Infrastructure.Voice;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

/// <summary>Independent protocol/routing and held-process checks, with no model or audible playback.</summary>
[TestClass]
public sealed class IndependentNeuralSpeechTests
{
    private const string Id = "00000000-0000-4000-8000-000000000001";
    private static readonly SpeechRequest Request = new("Public speech fixture", "kokoro:af_heart");
    private static readonly SpeechVoice Heart = new("kokoro:af_heart", "Heart — Neural (US)", "en-US");
    private static readonly SpeechVoice Windows = new("windows-fixture", "Windows fixture", "en-US");

    [TestMethod]
    public void EventEnvelopeIdentityAndClosedFieldsAreStrict()
    {
        var valid = "{\"protocol\":1,\"id\":\"" + Id + "\",\"kind\":\"completed\"}";
        Assert.AreEqual("completed", Parse(valid).Kind);
        var cases = new[]
        {
            "{}", valid.Replace("\"protocol\":1", "\"protocol\":true"),
            valid.Replace("\"protocol\":1", "\"protocol\":1.0"),
            valid.Replace("\"protocol\":1", "\"protocol\":2147483648"),
            valid.Replace("\"protocol\":1", "\"protocol\":2"),
            valid.Replace("\"protocol\":1", "\"protocol\":1,\"protocol\":1"),
            valid.Replace("\"kind\":\"completed\"", "\"kind\":\"execute\""),
            valid.Replace("\"kind\":\"completed\"", "\"kind\":\"completed\",\"extra\":\"private marker\""),
            valid.Replace(Id, "different"), valid.Replace("completed", "\\ud800"),
            valid + "{}", "[" + valid + "]"
        };
        foreach (var raw in cases)
        {
            var error = Assert.ThrowsExactly<SpeechException>(() => Parse(raw), raw);
            Assert.AreEqual(SpeechFailure.ProtocolError, error.Failure);
            Assert.IsNull(error.InnerException);
            Assert.IsFalse(error.ToString().Contains("private marker", StringComparison.Ordinal));
        }
        foreach (var identity in new[] { Guid.Empty.ToString(), Id.ToUpperInvariant().Replace("4000", "ABCD"), "{" + Id + "}", "not-guid" })
            Assert.ThrowsExactly<SpeechException>(() => KokoroNeuralSpeechOutput.ParseEvent(Encoding.UTF8.GetBytes(valid), identity, Request));
        Assert.ThrowsExactly<SpeechException>(() => KokoroNeuralSpeechOutput.ParseEvent([0xFF], Id, Request));
    }

    [TestMethod]
    public void CatalogContainsOnlyPinnedNamesCulturesAndUniqueIds()
    {
        var voices = new[] { Heart, new SpeechVoice("kokoro:af_bella", "Bella — Neural (US)", "en-US"),
            new SpeechVoice("kokoro:am_michael", "Michael — Neural (US)", "en-US"),
            new SpeechVoice("kokoro:bm_george", "George — Neural (UK)", "en-GB") };
        string Event(IEnumerable<SpeechVoice> selected) => JsonSerializer.Serialize(new
        {
            protocol = 1, id = Id, kind = "voices",
            voices = selected.Select(voice => new { id = voice.Id, name = voice.Name, culture = voice.Culture })
        });
        var result = Parse(Event(voices)).Voices!;
        CollectionAssert.AreEqual(voices, result.ToArray());
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<SpeechVoice>)result).Add(Heart));
        foreach (var invalid in new[] { voices[..3], voices.Append(Heart).ToArray(),
            new[] { Heart, Heart, voices[2], voices[3] },
            new[] { Heart with { Name = "Fake natural voice" }, voices[1], voices[2], voices[3] },
            new[] { Heart with { Culture = "en-GB" }, voices[1], voices[2], voices[3] } })
            Assert.ThrowsExactly<SpeechException>(() => Parse(Event(invalid)));
    }

    [TestMethod]
    public void ProbeUsesIndependentExactPcmAndDurationOracle()
    {
        var wave = Wave([1, short.MinValue, short.MaxValue, 0]);
        var result = Parse(AudioEvent(wave)).Audio!;
        CollectionAssert.AreEqual(wave, result.Wave);
        Assert.AreEqual(8, result.Probe.DataBytes);
        Assert.AreEqual(24000, result.Probe.SampleRate);
        Assert.AreEqual(1, result.Probe.Channels);
        Assert.AreEqual(16, result.Probe.BitsPerSample);
        Assert.AreEqual(1d / 6000, result.Probe.DurationSeconds);
        Assert.IsTrue(result.Probe.HasNonZeroSamples);
        Assert.AreEqual(Heart, result.Probe.Voice);
        var exact = Wave(Enumerable.Repeat((short)1, 720000).ToArray());
        Assert.AreEqual(30d, Parse(AudioEvent(exact)).Audio!.Probe.DurationSeconds);
        Assert.ThrowsExactly<SpeechException>(() => Parse(AudioEvent(Wave(Enumerable.Repeat((short)1, 720001).ToArray()))));
    }

    [TestMethod]
    public void CorruptAudioBase64AndMismatchedFormatAreRejected()
    {
        var valid = Wave([1, 2]);
        var badSize = valid.ToArray(); BinaryPrimitives.WriteUInt32LittleEndian(badSize.AsSpan(4), uint.MaxValue);
        var badRate = valid.ToArray(); BinaryPrimitives.WriteUInt32LittleEndian(badRate.AsSpan(24), 16000);
        var badData = valid.ToArray(); BinaryPrimitives.WriteUInt32LittleEndian(badData.AsSpan(40), uint.MaxValue);
        foreach (var raw in new[] { valid[..43], badSize, badRate, badData, Wave([0, 0]), valid.Append((byte)0).ToArray() })
            Assert.ThrowsExactly<SpeechException>(() => Parse(AudioEvent(raw)));
        foreach (var encoded in new[] { "!" + new string('A', 60), Convert.ToBase64String(valid).Insert(4, "\n"),
            new string('A', KokoroNeuralSpeechOutput.MaximumAudioEventBytes), "" })
            Assert.ThrowsExactly<SpeechException>(() => Parse(JsonSerializer.Serialize(new { protocol = 1, id = Id, kind = "probe", waveBase64 = encoded })));
        Assert.ThrowsExactly<SpeechException>(() => KokoroNeuralSpeechOutput.ParseEvent(Encoding.UTF8.GetBytes(AudioEvent(valid)), Id, null));
    }

    [TestMethod]
    public void WorkerFailuresAreFixedContentFreeCategories()
    {
        foreach (var code in new[] { "missing_assets", "asset_mismatch", "memory_limit", "runtime_missing", "inference_failed", "audio_limit", "unsupported_platform" })
        {
            var parsed = Parse(JsonSerializer.Serialize(new { protocol = 1, id = Id, kind = "failed", failure = code }));
            Assert.AreEqual(SpeechFailure.Unavailable, parsed.Failure);
            Assert.AreEqual(code, parsed.WorkerFailure);
        }
        Assert.AreEqual(SpeechFailure.InvalidRequest, Parse(JsonSerializer.Serialize(new { protocol = 1, id = Id, kind = "failed", failure = "invalid_request" })).Failure);
        Assert.ThrowsExactly<SpeechException>(() => Parse(JsonSerializer.Serialize(new { protocol = 1, id = Id, kind = "failed", failure = "private text" })));
    }

    [TestMethod]
    public async Task InvalidUnicodeVoiceRateAndCancelledAdmissionNeverStartWorker()
    {
        using var files = new TestDirectory();
        await using var output = new KokoroNeuralSpeechOutput(files.FilePath("python.exe"), files.FilePath("worker.py"), files.PathValue);
        foreach (var request in new[] { Request with { Text = "" }, Request with { Text = new string('x', 2001) },
            Request with { Text = "\ud800" }, Request with { Text = "private\0marker" }, Request with { Rate = -6 },
            Request with { Rate = 6 }, Request with { VoiceId = "kokoro:unknown" }, Request with { VoiceId = Windows.Id } })
        {
            var error = await Assert.ThrowsExactlyAsync<SpeechException>(() => output.SpeakAsync(request, null));
            Assert.AreEqual(SpeechFailure.InvalidRequest, error.Failure);
            Assert.IsNull(output.ActiveWorkerId);
        }
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => output.GetVoicesAsync(cancelled.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => output.SynthesizeAudioAsync(Request, cancelled.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => output.SpeakAsync(Request, null, cancelled.Token));
        Assert.IsNull(output.ActiveWorkerId);
        Assert.AreEqual(SpeechFailure.Unavailable, (await Assert.ThrowsExactlyAsync<SpeechException>(() => output.GetVoicesAsync())).Failure);
    }

    [TestMethod]
    public void StartEnvironmentHasFixedWorkerNoTextAndNoCredentials()
    {
        using var files = new TestDirectory();
        var python = Python(); var worker = files.FilePath("worker.py");
        var start = KokoroNeuralSpeechOutput.BuildStartInfo(python, worker);
        CollectionAssert.AreEqual(new[] { "-I", "-u", worker }, start.ArgumentList.ToArray());
        Assert.AreEqual(string.Empty, start.Arguments);
        Assert.IsFalse(start.UseShellExecute);
        Assert.IsTrue(start.CreateNoWindow && start.RedirectStandardInput && start.RedirectStandardOutput && start.RedirectStandardError);
        Assert.AreEqual(files.PathValue, start.WorkingDirectory);
        Assert.AreEqual(Environment.SystemDirectory, start.Environment["PATH"]);
        foreach (var name in new[] { "HF_HUB_OFFLINE", "TRANSFORMERS_OFFLINE", "HF_HUB_DISABLE_TELEMETRY", "DO_NOT_TRACK" })
            Assert.AreEqual("1", start.Environment[name]);
        foreach (var name in new[] { "OMP_NUM_THREADS", "OPENBLAS_NUM_THREADS", "MKL_NUM_THREADS", "NUMEXPR_NUM_THREADS" })
            Assert.AreEqual("2", start.Environment[name]);
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SystemRoot", "WINDIR", "TEMP", "TMP", "USERPROFILE", "LOCALAPPDATA", "APPDATA", "PATH",
            "OMP_NUM_THREADS", "OPENBLAS_NUM_THREADS", "MKL_NUM_THREADS", "NUMEXPR_NUM_THREADS", "HF_HUB_OFFLINE", "TRANSFORMERS_OFFLINE", "HF_HUB_DISABLE_TELEMETRY", "DO_NOT_TRACK", "PYTHONUTF8", "__PYVENV_LAUNCHER__" };
        Assert.IsTrue(start.Environment.Keys.All(allowed.Contains));
    }

    [TestMethod]
    [DataRow("wrong-id")]
    [DataRow("tail")]
    [DataRow("duplicate")]
    [DataRow("stderr")]
    [DataRow("stdout")]
    [DataRow("nonzero")]
    public async Task RealMalformedOrFloodingWorkerFailsAndAllowsSafeReuse(string fault)
    {
        using var files = new TestDirectory();
        await WriteWorker(files, $$"""
            if request["request"]["text"] == "fault":
                fault = "{{fault}}"
                if fault == "stderr":
                    sys.stderr.write("x" * 40000); sys.stderr.flush(); time.sleep(60)
                if fault == "stdout":
                    sys.stdout.write("x" * (6 * 1024 * 1024 + 1)); sys.stdout.flush(); time.sleep(60)
                if fault == "wrong-id": response["id"] = "different"
                raw = json.dumps(response)
                if fault == "duplicate": raw = raw.replace('"protocol": 1', '"protocol": 1, "protocol": 1')
                print(raw, flush=True)
                if fault == "tail": print("tail", flush=True)
                if fault == "nonzero": sys.exit(3)
            else: print(json.dumps(response), flush=True)
            """);
        await using var output = new KokoroNeuralSpeechOutput(Python(), files.FilePath("worker.py"), files.PathValue);
        var error = await Assert.ThrowsExactlyAsync<SpeechException>(() => output.SynthesizeAudioAsync(Request with { Text = "fault" }).WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.IsTrue(error.Failure is SpeechFailure.ProtocolError or SpeechFailure.Unavailable);
        Assert.IsNull(output.ActiveWorkerId);
        Assert.IsFalse(Directory.Exists(await File.ReadAllTextAsync(files.FilePath("owned-temp-path"))));
        Assert.AreEqual(24000, (await output.SynthesizeAudioAsync(Request).WaitAsync(TimeSpan.FromSeconds(10))).Probe.SampleRate);
        Assert.IsFalse(Directory.Exists(await File.ReadAllTextAsync(files.FilePath("owned-temp-path"))));
    }

    [TestMethod]
    public async Task HeldSynthesisCancellationTerminatesOnlyOwnedWorkerAndNeverSpeaks()
    {
        using var files = new TestDirectory(); using var otherFiles = new TestDirectory();
        const string barrier = """
            (project / "ready.tmp").write_text(str(os.getpid()), encoding="utf-8")
            os.replace(project / "ready.tmp", project / "ready")
            while not (project / "release").exists(): time.sleep(.01)
            print(json.dumps(response), flush=True)
            """;
        await WriteWorker(files, barrier); await WriteWorker(otherFiles, barrier);
        await using var output = new KokoroNeuralSpeechOutput(Python(), files.FilePath("worker.py"), files.PathValue);
        await using var other = new KokoroNeuralSpeechOutput(Python(), otherFiles.FilePath("worker.py"), otherFiles.PathValue);
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var states = new ConcurrentQueue<SpeechOutputState>();
        var active = output.SpeakAsync(Request, new DirectProgress(states.Enqueue), cancel.Token);
        var otherActive = other.SynthesizeAudioAsync(Request);
        using var worker = await HoldIdentity(files, active);
        using var otherWorker = await HoldIdentity(otherFiles, otherActive);
        var ownedTemp = output.ActiveWorkerTemporaryDirectory!;
        var otherTemp = other.ActiveWorkerTemporaryDirectory!;
        Assert.IsTrue(File.Exists(Path.Combine(ownedTemp, "espeak-copy.dll")));
        var busy = await Assert.ThrowsExactlyAsync<SpeechException>(() => output.GetVoicesAsync());
        Assert.AreEqual(SpeechFailure.Busy, busy.Failure);
        Assert.IsEmpty(states);
        var watch = Stopwatch.StartNew(); cancel.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => active.WaitAsync(TimeSpan.FromSeconds(6)));
        Assert.IsTrue(watch.Elapsed < TimeSpan.FromSeconds(6));
        Assert.IsTrue(worker.HasExited);
        Assert.IsFalse(otherWorker.HasExited);
        Assert.IsFalse(Directory.Exists(ownedTemp));
        Assert.IsTrue(Directory.Exists(otherTemp));
        CollectionAssert.AreEqual(new[] { SpeechOutputState.Stopped }, states.ToArray());
        await File.WriteAllTextAsync(otherFiles.FilePath("release"), "go");
        Assert.IsNotNull(await otherActive.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.IsFalse(Directory.Exists(otherTemp));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task HeldPlaybackDisposalIsSharedTerminatesWorkerAndReportsStopped(bool throwingObserver)
    {
        using var files = new TestDirectory();
        await WriteWorker(files, """
            print(json.dumps({"protocol":1,"id":request["id"],"kind":"speaking"}), flush=True)
            (project / "ready.tmp").write_text(str(os.getpid()), encoding="utf-8")
            os.replace(project / "ready.tmp", project / "ready")
            time.sleep(60)
            """);
        var output = new KokoroNeuralSpeechOutput(Python(), files.FilePath("worker.py"), files.PathValue);
        var states = new ConcurrentQueue<SpeechOutputState>();
        var active = output.SpeakAsync(Request, new DirectProgress(state =>
        {
            states.Enqueue(state);
            if (throwingObserver) throw new InvalidOperationException("Public observer fault");
        }));
        using var worker = await HoldIdentity(files, active);
        var ownedTemp = output.ActiveWorkerTemporaryDirectory!;
        Assert.IsTrue(SpinWait.SpinUntil(() => states.Contains(SpeechOutputState.Speaking), TimeSpan.FromSeconds(2)));
        var first = output.DisposeAsync().AsTask(); var second = output.DisposeAsync().AsTask();
        Assert.AreSame(first, second);
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(6));
        await Assert.ThrowsAsync<OperationCanceledException>(() => active);
        Assert.IsTrue(worker.HasExited);
        Assert.IsFalse(Directory.Exists(ownedTemp));
        CollectionAssert.AreEqual(new[] { SpeechOutputState.Speaking, SpeechOutputState.Stopped }, states.ToArray());
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => output.GetVoicesAsync());
        await output.DisposeAsync();
    }

    [TestMethod]
    public async Task CompositeRoutesSelectedVoiceWithoutFallbackAndSharesBusyState()
    {
        await using var windows = new FakeOutput([Windows]); await using var neural = new FakeOutput([Heart]);
        await using var output = new CompositeSpeechOutput(windows, neural);
        CollectionAssert.AreEqual(new[] { Heart, Windows }, (await output.GetVoicesAsync()).ToArray());
        neural.Hold = true;
        using var cancellation = new CancellationTokenSource();
        var active = output.SpeakAsync(Request, null, cancellation.Token);
        await neural.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.AreEqual(SpeechFailure.Busy, (await Assert.ThrowsExactlyAsync<SpeechException>(() => output.SpeakAsync(Request with { VoiceId = Windows.Id }, null))).Failure);
        cancellation.Cancel(); await Assert.ThrowsAsync<OperationCanceledException>(() => active);
        Assert.AreEqual(0, windows.Speaks);
        neural.Hold = false; neural.SpeakFailure = SpeechFailure.Unavailable;
        await Assert.ThrowsExactlyAsync<SpeechException>(() => output.SpeakAsync(Request, null));
        Assert.AreEqual(0, windows.Speaks);
        await output.SpeakAsync(Request with { VoiceId = Windows.Id }, null);
        Assert.AreEqual(1, windows.Speaks);
        await Assert.ThrowsExactlyAsync<SpeechException>(() => output.SpeakAsync(Request with { VoiceId = "kokoro:unknown" }, null));
        Assert.AreEqual(2, neural.Speaks);
    }

    [TestMethod]
    public async Task CompositePreservesOptionalProviderFailureAndRejectsSpoofedCatalogs()
    {
        await using var windows = new FakeOutput([Windows]); await using var neural = new FakeOutput([Heart]);
        await using var output = new CompositeSpeechOutput(windows, neural);
        neural.ListFailure = SpeechFailure.Unavailable;
        CollectionAssert.AreEqual(new[] { Windows }, (await output.GetVoicesAsync()).ToArray());
        Assert.AreEqual(SpeechFailure.Unavailable, output.NeuralListingFailure);
        Assert.IsNull(output.WindowsListingFailure);
        neural.ListFailure = null; windows.ListFailure = SpeechFailure.Unavailable;
        CollectionAssert.AreEqual(new[] { Heart }, (await output.GetVoicesAsync()).ToArray());
        Assert.AreEqual(SpeechFailure.Unavailable, output.WindowsListingFailure);
        windows.ListFailure = null; windows.Voices = [Heart];
        Assert.AreEqual(SpeechFailure.ProtocolError, (await Assert.ThrowsExactlyAsync<SpeechException>(() => output.GetVoicesAsync())).Failure);
        windows.Voices = [Windows]; neural.Voices = [new("kokoro:fake", "Fake voice", "en-US")];
        await Assert.ThrowsExactlyAsync<SpeechException>(() => output.GetVoicesAsync());
    }

    [TestMethod]
    public async Task CompositeConcurrentDisposalStillDisposesBothWhenOneThrowsSynchronously()
    {
        var windows = new FakeOutput([Windows]) { DisposeFailure = true }; var neural = new FakeOutput([Heart]);
        var output = new CompositeSpeechOutput(windows, neural);
        var first = output.DisposeAsync().AsTask(); var second = output.DisposeAsync().AsTask();
        Assert.AreSame(first, second);
        await Assert.ThrowsExactlyAsync<SpeechException>(() => first);
        Assert.AreEqual(1, windows.Disposals); Assert.AreEqual(1, neural.Disposals);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => output.GetVoicesAsync());
    }

    [TestMethod]
    public void OwnedTemporaryCleanupRejectsForeignPathsAndRemovesNestedOwnedFiles()
    {
        using var unrelated = new TestDirectory();
        var sentinel = unrelated.FilePath("preserve.txt"); File.WriteAllText(sentinel, "public unrelated fixture");
        Assert.AreEqual(SpeechFailure.CleanupFailed, Assert.ThrowsExactly<SpeechException>(() => KokoroNeuralSpeechOutput.DeleteOwnedTemporaryDirectory(unrelated.PathValue)).Failure);
        Assert.AreEqual("public unrelated fixture", File.ReadAllText(sentinel));
        var owned = KokoroNeuralSpeechOutput.CreateOwnedTemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(owned, "loader"));
        File.WriteAllText(Path.Combine(owned, "loader", "espeak-copy.dll"), "public runtime fixture");
        KokoroNeuralSpeechOutput.DeleteOwnedTemporaryDirectory(owned);
        Assert.IsFalse(Directory.Exists(owned));
        Assert.AreEqual(SpeechFailure.CleanupFailed, Assert.ThrowsExactly<SpeechException>(() => KokoroNeuralSpeechOutput.DeleteOwnedTemporaryDirectory(owned)).Failure);
        Assert.IsTrue(File.Exists(sentinel));
    }

    [TestMethod]
    public async Task LastWorkerFailureIsClosedRetainedAcrossRejectedAdmissionAndResetOnReuse()
    {
        using var files = new TestDirectory();
        await WriteWorker(files, """
            if request["request"]["text"] == "failure":
                response = {"protocol":1,"id":request["id"],"kind":"failed","failure":"memory_limit"}
            print(json.dumps(response), flush=True)
            """);
        await using var output = new KokoroNeuralSpeechOutput(Python(), files.FilePath("worker.py"), files.PathValue);
        Assert.IsNull(output.LastWorkerFailure);
        Assert.AreEqual(SpeechFailure.Unavailable, (await Assert.ThrowsExactlyAsync<SpeechException>(() => output.SynthesizeAudioAsync(Request with { Text = "failure" }))).Failure);
        Assert.AreEqual("memory_limit", output.LastWorkerFailure);
        await Assert.ThrowsExactlyAsync<SpeechException>(() => output.SpeakAsync(Request with { Text = "" }, null));
        Assert.AreEqual("memory_limit", output.LastWorkerFailure);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => output.GetVoicesAsync(cancelled.Token));
        Assert.AreEqual("memory_limit", output.LastWorkerFailure);
        Assert.AreEqual(24000, (await output.SynthesizeAudioAsync(Request)).Probe.SampleRate);
        Assert.IsNull(output.LastWorkerFailure);
    }

    [TestMethod]
    public async Task RepeatedRealCancellationAndErrorDrainTerminationKeepThreeSecondCleanupAndReuse()
    {
        using var files = new TestDirectory();
        await WriteWorker(files, """
            if request["request"]["text"] in ("cancel", "stderr"):
                (project / "ready.tmp").write_text(str(os.getpid()), encoding="utf-8")
                os.replace(project / "ready.tmp", project / "ready")
                while not (project / "release").exists(): time.sleep(.001)
                if request["request"]["text"] == "stderr":
                    sys.stderr.write("x" * 40000); sys.stderr.flush()
                time.sleep(60)
            else: print(json.dumps(response), flush=True)
            """);
        await using var output = new KokoroNeuralSpeechOutput(Python(), files.FilePath("worker.py"), files.PathValue);
        for (var iteration = 0; iteration < 24; iteration++)
        {
            File.Delete(files.FilePath("ready")); File.Delete(files.FilePath("release"));
            var cancelled = iteration % 2 == 0;
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var states = new ConcurrentQueue<SpeechOutputState>();
            var active = output.SpeakAsync(Request with { Text = cancelled ? "cancel" : "stderr" },
                new DirectProgress(states.Enqueue), cancellation.Token);
            using var held = await HoldIdentity(files, active);
            var temporary = output.ActiveWorkerTemporaryDirectory!;
            Assert.IsTrue(File.Exists(Path.Combine(temporary, "espeak-copy.dll")));
            var watch = Stopwatch.StartNew();
            if (cancelled)
            {
                cancellation.Cancel();
                await Assert.ThrowsAsync<OperationCanceledException>(() => active.WaitAsync(TimeSpan.FromSeconds(6)));
            }
            else
            {
                await File.WriteAllTextAsync(files.FilePath("release"), "go");
                var failure = await Assert.ThrowsExactlyAsync<SpeechException>(() => active.WaitAsync(TimeSpan.FromSeconds(6)));
                Assert.IsTrue(failure.Failure is SpeechFailure.ProtocolError or SpeechFailure.Unavailable);
            }
            Assert.IsTrue(watch.Elapsed < SpeechLimits.CleanupDeadline, $"Actual held termination exceeded 3 seconds at iteration {iteration}.");
            Assert.IsTrue(held.HasExited);
            Assert.IsNull(output.ActiveWorkerId);
            Assert.IsFalse(Directory.Exists(temporary));
            CollectionAssert.AreEqual(new[] { SpeechOutputState.Stopped }, states.ToArray());
            Assert.AreEqual(24000, (await output.SynthesizeAudioAsync(Request).WaitAsync(TimeSpan.FromSeconds(10))).Probe.SampleRate);
            Assert.IsFalse(Directory.Exists(await File.ReadAllTextAsync(files.FilePath("owned-temp-path"))));
        }
    }

    [TestMethod]
    public async Task StopConfirmsHeldExitWhenRealJobTerminationRacesProcessKill()
    {
        using var files = new TestDirectory();
        await File.WriteAllTextAsync(files.FilePath("worker.py"),
            "import os, sys, time\nsys.stdin.readline()\nprint(os.getpid(),flush=True)\ntime.sleep(60)\n", new UTF8Encoding(false));
        for (var iteration = 0; iteration < 32; iteration++)
        {
            using var job = MlWorkerJob.Create(1536);
            var start = KokoroNeuralSpeechOutput.BuildStartInfo(Python(), files.FilePath("worker.py"));
            // This direct termination oracle needs no writable working directory. Keep Windows'
            // asynchronous release of a killed process's cwd out of unrelated fixture disposal;
            // the public-host case separately checks its actual worker cwd and owned TEMP cleanup.
            start.WorkingDirectory = Environment.SystemDirectory;
            using var held = Process.Start(start)!;
            _ = held.SafeHandle;
            try
            {
                job.Assign(held);
                await held.StandardInput.WriteLineAsync("public fixture"); held.StandardInput.Close();
                Assert.AreEqual(held.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    await held.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)));
                var watch = Stopwatch.StartNew();
                // Actual Windows job termination starts asynchronously. Immediate and yielded calls
                // cover both a still-exiting child and an already-signaled held handle, without
                // assuming or faking which native Process.Kill race the scheduler will produce.
                job.Terminate();
                if (iteration % 2 != 0) await Task.Yield();
                await KokoroNeuralSpeechOutput.StopOwnedWorkerAsync(held, job).WaitAsync(TimeSpan.FromSeconds(6));
                Assert.IsTrue(watch.Elapsed < SpeechLimits.CleanupDeadline);
                Assert.IsTrue(held.HasExited);
                Assert.AreNotEqual(0, held.ExitCode);
            }
            finally
            {
                job.Terminate();
                await held.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));
            }
        }
    }

    private static KokoroNeuralSpeechOutput.NeuralEvent Parse(string raw) => KokoroNeuralSpeechOutput.ParseEvent(Encoding.UTF8.GetBytes(raw), Id, Request);
    private static string AudioEvent(byte[] wave) => JsonSerializer.Serialize(new { protocol = 1, id = Id, kind = "probe", waveBase64 = Convert.ToBase64String(wave) });
    private static byte[] Wave(short[] values)
    {
        var bytes = new byte[44 + values.Length * 2];
        "RIFF"u8.CopyTo(bytes); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), (uint)bytes.Length - 8);
        "WAVEfmt "u8.CopyTo(bytes.AsSpan(8)); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(20), 1); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(22), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(24), 24000); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(28), 48000);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(32), 2); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(34), 16);
        "data"u8.CopyTo(bytes.AsSpan(36)); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(40), (uint)values.Length * 2);
        for (var index = 0; index < values.Length; index++) BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(44 + index * 2), values[index]);
        return bytes;
    }
    private static string Python()
    {
        for (var root = new DirectoryInfo(AppContext.BaseDirectory); root is not null; root = root.Parent)
            if (File.Exists(Path.Combine(root.FullName, "Companion.slnx")))
            {
                var python = Path.Combine(root.FullName, "artifacts", "ml-runtime", "Scripts", "python.exe");
                if (!File.Exists(python)) Assert.Inconclusive("Optional ML Python runtime is absent. Run scripts/setup-ml-lab.ps1 to execute real process-boundary review.");
                return python;
            }
        throw new AssertFailedException("The reviewed local test runtime was not found.");
    }
    private static async Task WriteWorker(TestDirectory files, string body) => await File.WriteAllTextAsync(files.FilePath("worker.py"),
        "import base64, json, os, sys, time\nfrom pathlib import Path\nrequest=json.loads(sys.stdin.readline())\nassert sys.stdin.read()==''\nproject=Path(request['modelDir'])\ntemporary=Path(os.environ['TEMP'])\nassert temporary==Path(os.environ['TMP'])\n(project/'owned-temp-path').write_text(str(temporary),encoding='utf-8')\n(temporary/'espeak-copy.dll').write_bytes(b'public runtime fixture')\nresponse={'protocol':1,'id':request['id'],'kind':'probe','waveBase64':'" + Convert.ToBase64String(Wave([1, -1, 2])) + "'}\n" + body + "\n", new UTF8Encoding(false));
    private static async Task<Process> HoldIdentity(TestDirectory files, Task active)
    {
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!File.Exists(files.FilePath("ready")))
        {
            if (active.IsCompleted) { await active; Assert.Fail("Worker did not reach its held barrier."); }
            await Task.Delay(20, limit.Token);
        }
        var process = Process.GetProcessById(int.Parse(await File.ReadAllTextAsync(files.FilePath("ready"), limit.Token), System.Globalization.CultureInfo.InvariantCulture));
        _ = process.Handle; return process;
    }
    private sealed class DirectProgress(Action<SpeechOutputState> report) : IProgress<SpeechOutputState>
    { public void Report(SpeechOutputState value) => report(value); }
    private sealed class FakeOutput(IReadOnlyList<SpeechVoice> voices) : ISpeechOutput
    {
        public IReadOnlyList<SpeechVoice> Voices { get; set; } = voices;
        public SpeechFailure? ListFailure { get; set; }
        public SpeechFailure? SpeakFailure { get; set; }
        public bool Hold { get; set; }
        public bool DisposeFailure { get; set; }
        public int Speaks { get; private set; }
        public int Disposals { get; private set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IReadOnlyList<SpeechVoice>> GetVoicesAsync(CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); return ListFailure is { } failure ? Task.FromException<IReadOnlyList<SpeechVoice>>(new SpeechException(failure)) : Task.FromResult(Voices); }
        public async Task SpeakAsync(SpeechRequest request, IProgress<SpeechOutputState>? progress, CancellationToken cancellationToken = default)
        {
            Speaks++; Started.TrySetResult();
            if (SpeakFailure is { } failure) throw new SpeechException(failure);
            if (Hold) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        public ValueTask DisposeAsync()
        { Disposals++; if (DisposeFailure) throw new SpeechException(SpeechFailure.CleanupFailed); return ValueTask.CompletedTask; }
    }
}
