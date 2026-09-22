using Companion.Core.Desktop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

[TestClass]
public sealed class IndependentAvatarStateTests
{
    private static readonly AvatarState[] IncreasingPriority =
    [AvatarState.Notifying, AvatarState.Thinking, AvatarState.Working,
        AvatarState.Speaking, AvatarState.Listening, AvatarState.Error];

    [TestMethod]
    public void EveryPriorityPairResolvesHigherAndReturnsToRemainingOwner()
    {
        for (var lower = 0; lower < IncreasingPriority.Length; lower++)
        for (var higher = lower + 1; higher < IncreasingPriority.Length; higher++)
        {
            var machine = new AvatarStateMachine();
            using var low = machine.Begin(IncreasingPriority[lower]);
            using var high = machine.Begin(IncreasingPriority[higher]);
            Assert.AreEqual(IncreasingPriority[higher], machine.Current);

            high.Dispose();
            Assert.AreEqual(IncreasingPriority[lower], machine.Current);
            low.Dispose();
            Assert.AreEqual(AvatarState.Idle, machine.Current);
        }
    }

    [TestMethod]
    public void LateOrRepeatedCompletionCannotClearANewerOwnerOfTheSameState()
    {
        var machine = new AvatarStateMachine();
        using var old = machine.Begin(AvatarState.Working);
        using var current = machine.Begin(AvatarState.Working);
        old.Dispose();
        old.Dispose();
        Assert.AreEqual(AvatarState.Working, machine.Current);

        current.Dispose();
        Assert.AreEqual(AvatarState.Idle, machine.Current);
    }

    [TestMethod]
    public void SleepInvalidatesOldLeasesAndWakeDoesNotReviveTheirActivities()
    {
        var machine = new AvatarStateMachine();
        using var oldError = machine.Begin(AvatarState.Error);
        using var oldSpeech = machine.Begin(AvatarState.Speaking);
        machine.SetSleeping(true);
        Assert.AreEqual(AvatarState.Sleeping, machine.Current);
        Assert.ThrowsExactly<InvalidOperationException>(() => machine.Begin(AvatarState.Listening));

        machine.SetSleeping(false);
        Assert.AreEqual(AvatarState.Idle, machine.Current);
        using var newSpeech = machine.Begin(AvatarState.Speaking);
        oldSpeech.Dispose();
        oldError.Dispose();
        Assert.AreEqual(AvatarState.Speaking, machine.Current);
        newSpeech.Dispose();
        Assert.AreEqual(AvatarState.Idle, machine.Current);
    }

    [TestMethod]
    [DataRow(AvatarState.Idle)]
    [DataRow(AvatarState.Sleeping)]
    [DataRow((AvatarState)(-1))]
    [DataRow((AvatarState)999)]
    public void InvalidActivityCannotAlterAnExistingOwner(AvatarState invalid)
    {
        var machine = new AvatarStateMachine();
        using var working = machine.Begin(AvatarState.Working);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => machine.Begin(invalid));
        Assert.AreEqual(AvatarState.Working, machine.Current);
    }

    [TestMethod]
    public async Task ConcurrentOwnersAndConcurrentDisposalsDoNotLeakState()
    {
        var machine = new AvatarStateMachine();
        var leases = await Task.WhenAll(Enumerable.Range(0, 120).Select(index =>
            Task.Run(() => machine.Begin(IncreasingPriority[index % IncreasingPriority.Length]))));
        Assert.AreEqual(AvatarState.Error, machine.Current);

        await Task.WhenAll(leases.Select(lease => Task.Run(lease.Dispose)));

        Assert.AreEqual(AvatarState.Idle, machine.Current);
    }

    [TestMethod]
    public void ChangedCallbackAllowsAnotherThreadToReadCurrentWithoutHoldingTheGate()
    {
        var machine = new AvatarStateMachine();
        var observations = 0;
        machine.Changed += (_, _) =>
        {
            var reader = Task.Run(() => machine.Current);
            Assert.IsTrue(reader.Wait(TimeSpan.FromSeconds(3)),
                "A Changed callback must not hold a lock needed by another reader.");
            Assert.IsTrue(Enum.IsDefined(reader.Result));
            observations++;
        };

        using (machine.Begin(AvatarState.Thinking)) { Assert.AreEqual(AvatarState.Thinking, machine.Current); }
        Assert.AreEqual(2, observations);
        Assert.AreEqual(AvatarState.Idle, machine.Current);
    }
}
