using System.Text.Json;
using Companion.Application.Conversation;
using Companion.Infrastructure.Conversation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

[TestClass]
public sealed class IndependentLocalModelHostTests
{
    [TestMethod]
    public async Task HashFailureReleasesEveryAcquiredFileLeaseAndNeverStartsWorker()
    {
        using var directory = new TestDirectory();
        var runtime = Directory.CreateDirectory(Path.Combine(directory.PathValue, "runtime")).FullName;
        await using var catalogStream = typeof(LocalModelHost).Assembly.GetManifestResourceStream(
            "Companion.Infrastructure.Conversation.local-runtime-hashes.json")!;
        var catalog = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(catalogStream);
        Assert.IsNotNull(catalog);
        Assert.IsTrue(catalog.Count > 0);
        foreach (var name in catalog.Keys)
            await File.WriteAllBytesAsync(Path.Combine(runtime, name), [0x51, 0x41]);

        await using var host = new LocalModelHost(directory.PathValue);
        var failure = await Assert.ThrowsExactlyAsync<ChatModelException>(() => host.GetModelAsync());
        Assert.AreEqual(ChatFailure.Unavailable, failure.Failure);
        Assert.IsNull(host.WorkerProcessId);
        Assert.IsFalse(failure.ToString().Contains(directory.PathValue, StringComparison.Ordinal));

        // Matching inventory forces admission into the hash loop. Exclusive writes
        // establish that the failed attempt released its acquired read-only leases.
        foreach (var name in catalog.Keys)
        {
            using var exclusive = new FileStream(Path.Combine(runtime, name), FileMode.Open,
                FileAccess.ReadWrite, FileShare.None);
            exclusive.WriteByte(0x00);
        }
        var retry = await Assert.ThrowsExactlyAsync<ChatModelException>(() => host.GetModelAsync());
        Assert.AreEqual(ChatFailure.Unavailable, retry.Failure);
        Assert.IsNull(host.WorkerProcessId);
    }

    [TestMethod]
    public async Task ConcurrentDisposalIsIdempotentAndRejectsLaterReadiness()
    {
        using var directory = new TestDirectory();
        var host = new LocalModelHost(directory.PathValue);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var closures = Enumerable.Range(0, 16).Select(_ => Task.Run(async () =>
        {
            await start.Task;
            await host.DisposeAsync();
        })).ToArray();
        start.SetResult();
        await Task.WhenAll(closures).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(closures.All(task => task.IsCompletedSuccessfully));
        Assert.IsNull(host.WorkerProcessId);
        await Assert.ThrowsAsync<OperationCanceledException>(() => host.GetModelAsync());
        await host.DisposeAsync();
    }

    [TestMethod]
    public async Task MissingLocalModelFailureHasNoPathOrRawExceptionAndDoesNotPoisonCancellation()
    {
        using var directory = new TestDirectory();
        await using var host = new LocalModelHost(Path.Combine(directory.PathValue, "sensitive-fixture-name"));
        var failure = await Assert.ThrowsExactlyAsync<ChatModelException>(() => host.GetModelAsync());
        Assert.AreEqual(ChatFailure.Unavailable, failure.Failure);
        Assert.IsNull(failure.InnerException);
        Assert.IsFalse(failure.ToString().Contains("sensitive-fixture-name", StringComparison.Ordinal));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => host.GetModelAsync(cancelled.Token));
        Assert.IsNull(host.WorkerProcessId);
    }
}
