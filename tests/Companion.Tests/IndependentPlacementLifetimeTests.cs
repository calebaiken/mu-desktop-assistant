using System.Text.Json;
using Companion.Core.Desktop;
using Companion.Infrastructure.Desktop;
using Companion.Infrastructure.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

[TestClass]
public sealed class IndependentPlacementLifetimeTests
{
    private static SavedPlacement Initial => new()
    {
        MonitorDeviceId = "independent-lifetime-monitor", HorizontalFraction = .2, VerticalFraction = .3
    };

    [TestMethod]
    public async Task CancellationQueuedBehindCommitDoesNotCancelOwnerOrPoisonNextSave()
    {
        using var directory = new TestDirectory();
        await File.WriteAllTextAsync(directory.FilePath("placement.json"), JsonSerializer.Serialize(Initial));
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writer = new CallbackWriter((path, backup, bytes) =>
        {
            entered.TrySetResult();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Test writer was not released.");
            new AtomicFileWriter().Write(path, backup, bytes);
        });
        using var store = new JsonPlacementStore(directory.PathValue, writer);
        await store.LoadAsync(CancellationToken.None);
        var first = Initial with { HorizontalFraction = .4 };
        var owner = store.SaveAsync(first, CancellationToken.None);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var cancellation = new CancellationTokenSource();
            var waiter = store.SaveAsync(Initial with { HorizontalFraction = .6 }, cancellation.Token);
            Assert.IsFalse(waiter.IsCompleted);
            await cancellation.CancelAsync();
            await Assert.ThrowsAsync<OperationCanceledException>(() => waiter);
            Assert.IsFalse(owner.IsCompleted);
            Assert.AreEqual(Initial, JsonSerializer.Deserialize<SavedPlacement>(
                await File.ReadAllTextAsync(directory.FilePath("placement.json"))));
        }
        finally
        {
            release.Set();
            await owner.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.AreEqual(first, JsonSerializer.Deserialize<SavedPlacement>(
            await File.ReadAllTextAsync(directory.FilePath("placement.json"))));
        var last = Initial with { HorizontalFraction = .8 };
        await store.SaveAsync(last, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(last, JsonSerializer.Deserialize<SavedPlacement>(
            await File.ReadAllTextAsync(directory.FilePath("placement.json"))));
    }

    [TestMethod]
    public async Task CancellationAfterAtomicCommitReportsSuccessAndPublishesCommittedBaseline()
    {
        using var directory = new TestDirectory();
        using var cancellation = new CancellationTokenSource();
        var writer = new CallbackWriter((path, backup, bytes) =>
        {
            new AtomicFileWriter().Write(path, backup, bytes);
            cancellation.Cancel();
        });
        using var store = new JsonPlacementStore(directory.PathValue, writer);
        await store.LoadAsync(CancellationToken.None);
        await store.SaveAsync(Initial, cancellation.Token);
        Assert.IsTrue(cancellation.IsCancellationRequested);
        var next = Initial with { VerticalFraction = .9 };
        await store.SaveAsync(next, CancellationToken.None);
        using var restart = new JsonPlacementStore(directory.PathValue);
        Assert.AreEqual(next, (await restart.LoadAsync(CancellationToken.None)).Placement);
        Assert.IsEmpty(Directory.GetFiles(directory.PathValue, "*.tmp"));
    }

    [TestMethod]
    public async Task FailedWriterPreservesKnownGoodAndAllowsAWholeRetry()
    {
        using var directory = new TestDirectory();
        var before = JsonSerializer.Serialize(Initial);
        await File.WriteAllTextAsync(directory.FilePath("placement.json"), before);
        var attempts = 0;
        var writer = new CallbackWriter((path, backup, bytes) =>
        {
            if (Interlocked.Increment(ref attempts) == 1) throw new IOException("Injected precommit write failure.");
            new AtomicFileWriter().Write(path, backup, bytes);
        });
        using var store = new JsonPlacementStore(directory.PathValue, writer);
        await store.LoadAsync(CancellationToken.None);
        await Assert.ThrowsExactlyAsync<IOException>(() =>
            store.SaveAsync(Initial with { HorizontalFraction = .5 }, CancellationToken.None));
        Assert.AreEqual(before, await File.ReadAllTextAsync(directory.FilePath("placement.json")));
        var retry = Initial with { VerticalFraction = .7 };
        await store.SaveAsync(retry, CancellationToken.None);
        using var restart = new JsonPlacementStore(directory.PathValue);
        Assert.AreEqual(retry, (await restart.LoadAsync(CancellationToken.None)).Placement);
        Assert.AreEqual(2, attempts);
        Assert.IsEmpty(Directory.GetFiles(directory.PathValue, "*.tmp"));
    }

    private sealed class CallbackWriter(Action<string, string?, byte[]> callback) : IAtomicFileWriter
    {
        public void Write(string targetPath, string? backupPath, ReadOnlySpan<byte> contents) =>
            callback(targetPath, backupPath, contents.ToArray());
    }
}
