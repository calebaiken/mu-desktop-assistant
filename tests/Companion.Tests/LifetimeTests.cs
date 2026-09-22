using Companion.Infrastructure.Lifetime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

[TestClass]
public sealed class LifetimeTests
{
    [TestMethod]
    public async Task SecondInstanceActivatesFirstAndLockCanBeReacquiredAfterShutdown()
    {
        using var directory = new TestDirectory();
        var activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using (var first = SingleInstanceCoordinator.TryAcquire(directory.PathValue))
        {
            Assert.IsNotNull(first);
            first.StartListening(() => activated.TrySetResult());
            Assert.IsNull(SingleInstanceCoordinator.TryAcquire(directory.PathValue));
            Assert.IsTrue(await SingleInstanceCoordinator.ActivateExistingAsync(directory.PathValue, CancellationToken.None));
            await activated.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsFalse(first.Completion.IsCompleted);
        }
        await using var restarted = SingleInstanceCoordinator.TryAcquire(directory.PathValue);
        Assert.IsNotNull(restarted);
    }

    [TestMethod]
    public async Task CancelledActivationExitsPromptly()
    {
        using var directory = new TestDirectory();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            SingleInstanceCoordinator.ActivateExistingAsync(directory.PathValue, cancelled.Token));
    }

    [TestMethod]
    public async Task SeparateDataDirectoriesDoNotShareInstanceLock()
    {
        using var firstDirectory = new TestDirectory();
        using var secondDirectory = new TestDirectory();
        await using var first = SingleInstanceCoordinator.TryAcquire(firstDirectory.PathValue);
        await using var second = SingleInstanceCoordinator.TryAcquire(secondDirectory.PathValue);
        Assert.IsNotNull(first);
        Assert.IsNotNull(second);
    }
}
