using System.Text.Json;
using Companion.Application.Diagnostics;
using Companion.Infrastructure.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

[TestClass]
public sealed class LoggingTests
{
    [TestMethod]
    public async Task StructuredChannelsExcludeContentAndDiagnosticsDefaultOff()
    {
        using var directory = new TestDirectory();
        using var log = new JsonEventLog(directory.PathValue);
        foreach (var area in Enum.GetValues<EventArea>())
            await log.WriteAsync(new(area, EventCode.Started, Guid.NewGuid()), CancellationToken.None);
        Assert.IsFalse(File.Exists(directory.FilePath("diagnostics.jsonl")));
        foreach (var area in new[] { "application", "error", "toolaudit" })
        {
            using var json = JsonDocument.Parse((await File.ReadAllLinesAsync(directory.FilePath(area + ".jsonl")))[0]);
            CollectionAssert.AreEquivalent(new[] { "schemaVersion", "timestamp", "area", "code", "operationId", "durationMs" },
                json.RootElement.EnumerateObject().Select(property => property.Name).ToArray());
        }
        log.DiagnosticsEnabled = true;
        await log.WriteAsync(new(EventArea.Diagnostics, EventCode.Started, Guid.NewGuid()), CancellationToken.None);
        Assert.IsTrue(File.Exists(directory.FilePath("diagnostics.jsonl")));
    }

    [TestMethod]
    public async Task RotationIsBoundedAndEveryRecordIsValidJson()
    {
        using var directory = new TestDirectory();
        using var log = new JsonEventLog(directory.PathValue, maximumBytes: 1024, retainedFiles: 3);
        await Task.WhenAll(Enumerable.Range(0, 60).Select(_ => log.WriteAsync(
            new(EventArea.Application, EventCode.Activated, Guid.NewGuid()), CancellationToken.None)));
        var files = Directory.GetFiles(directory.PathValue);
        Assert.HasCount(3, files);
        foreach (var file in files)
        {
            Assert.IsTrue(new FileInfo(file).Length <= 1024);
            foreach (var line in await File.ReadAllLinesAsync(file))
            {
                using var json = JsonDocument.Parse(line);
                Assert.AreEqual("Activated", json.RootElement.GetProperty("code").GetString());
            }
        }
    }

    [TestMethod]
    public async Task OldestEventAgeBoundsRetentionEvenWhenFileWasRecentlyWritten()
    {
        using var directory = new TestDirectory();
        var clock = new ManualClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        using var log = new JsonEventLog(directory.PathValue, timeProvider: clock);
        await log.WriteAsync(new(EventArea.Application, EventCode.Started, Guid.NewGuid()), CancellationToken.None);
        clock.Now = clock.Now.AddDays(13);
        await log.WriteAsync(new(EventArea.Application, EventCode.SettingsSaved, Guid.NewGuid()), CancellationToken.None);
        clock.Now = clock.Now.AddDays(2);
        await log.WriteAsync(new(EventArea.Application, EventCode.Stopping, Guid.NewGuid()), CancellationToken.None);
        var lines = await File.ReadAllLinesAsync(directory.FilePath("application.jsonl"));
        Assert.HasCount(1, lines);
        StringAssert.Contains(lines[0], "Stopping");
    }

    [TestMethod]
    public async Task InvalidCodesAndStorageFailureAreNotSilentlyAccepted()
    {
        using var directory = new TestDirectory();
        using var log = new JsonEventLog(directory.PathValue);
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => log.WriteAsync(
            new((EventArea)999, EventCode.Started, Guid.NewGuid()), CancellationToken.None));
        using var denied = new JsonEventLog(directory.FilePath("not-a-directory"));
        await File.WriteAllTextAsync(directory.FilePath("not-a-directory"), "block");
        await Assert.ThrowsAsync<IOException>(() => denied.WriteAsync(new(EventArea.Error, EventCode.Started, Guid.NewGuid()), CancellationToken.None));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("{\"timestamp\":")]
    [DataRow("{}\n")]
    [DataRow("{\"timestamp\":123}\n")]
    [DataRow("garbage\n")]
    [DataRow("{\"timestamp\":\"2026-09-21T00:00:00Z\"}\n{incomplete")]
    public async Task DamagedLogsRecoverWithoutBlockingStartup(string contents)
    {
        using var directory = new TestDirectory();
        await File.WriteAllTextAsync(directory.FilePath("application.jsonl"), contents);
        using var log = new JsonEventLog(directory.PathValue);
        await log.WriteAsync(new(EventArea.Application, EventCode.Started, Guid.NewGuid()), CancellationToken.None);
        Assert.AreEqual(1, log.RecoveredCorruptLogs);
        Assert.AreEqual(contents, await File.ReadAllTextAsync(directory.FilePath("application.jsonl.corrupt")));
        using var json = JsonDocument.Parse((await File.ReadAllLinesAsync(directory.FilePath("application.jsonl")))[0]);
        Assert.AreEqual("Started", json.RootElement.GetProperty("code").GetString());
    }

    [TestMethod]
    public async Task StartupSweepExpiresDisabledDiagnosticFamily()
    {
        using var directory = new TestDirectory();
        var clock = new ManualClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        using var log = new JsonEventLog(directory.PathValue, timeProvider: clock) { DiagnosticsEnabled = true };
        await log.WriteAsync(new(EventArea.Diagnostics, EventCode.Started, Guid.NewGuid()), CancellationToken.None);
        log.DiagnosticsEnabled = false;
        clock.Now = clock.Now.AddDays(15);
        await log.WriteAsync(new(EventArea.Application, EventCode.Started, Guid.NewGuid()), CancellationToken.None);
        Assert.IsFalse(File.Exists(directory.FilePath("diagnostics.jsonl")));
    }

    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
