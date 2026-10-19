using System.Text;
using Companion.Application.Diagnostics;
using Companion.Application.Tools;
using Companion.Infrastructure.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

/// <summary>Independent policy/real-stream probes; these do not qualify Windows handles or WPF.</summary>
[TestClass]
public sealed class IndependentTrustedActionTests
{
    private const string Literal = "PUBLIC-FIXTURE <script>run()</script> $(whoami) [link](file:///C:/secret)";
    private const string Selected = @"C:\PublicFixture\reviewed.txt";

    [TestMethod]
    public async Task DisabledAndUnknownCapabilitiesNeverPrepareOrRead()
    {
        var tool = new FixtureTool();
        await using var executor = new TrustedToolExecutor(tool, new FixtureAudit());
        Assert.AreEqual(ToolFailure.Denied, (await Assert.ThrowsExactlyAsync<ToolException>(() => executor.PrepareAsync(Proposal(), default))).Failure);
        executor.SetEnabled(true);
        foreach (var proposal in new[] { Proposal() with { Id = Guid.Empty }, Proposal() with { ToolId = "mu.shell" },
                     Proposal() with { Version = 2 }, Proposal() with { Arguments = null! } })
            Assert.AreEqual(ToolFailure.InvalidRequest, (await Assert.ThrowsExactlyAsync<ToolException>(() => executor.PrepareAsync(proposal, default))).Failure);
        Assert.AreEqual(0, tool.Preparations);
        Assert.AreEqual(0, tool.Leases.Sum(item => item.Reads));
    }

    [TestMethod]
    public async Task DisabledByDefaultAndUndefinedDescriptorRiskCannotGainAuthority()
    {
        foreach (var risk in new[] { ToolRisk.DisabledByDefault, (ToolRisk)1234 })
        {
            var tool = new FixtureTool { Descriptor = new(TextInspection.ToolId, 1, "Fixture", risk) };
            await using var executor = Enabled(tool);
            Assert.AreEqual(ToolFailure.InvalidRequest, (await Assert.ThrowsExactlyAsync<ToolException>(() => executor.PrepareAsync(Proposal(), default))).Failure);
            Assert.AreEqual(0, tool.Preparations);
        }
    }

    [TestMethod]
    public async Task ProposalApprovalAndReadAreDistinctAndOnlyOneUseReturnsLiteralText()
    {
        var audit = new FixtureAudit();
        var tool = new FixtureTool();
        await using var executor = Enabled(tool, audit);
        var proposal = Proposal();
        var preview = await executor.PrepareAsync(proposal, default);
        Assert.AreSame(proposal, preview.Proposal);
        Assert.AreEqual(Selected, preview.Resource.CanonicalPath);
        Assert.AreEqual(0, tool.Leases.Single().Reads);
        var grant = await executor.ApproveAsync(preview, default);
        Assert.AreEqual(0, tool.Leases.Single().Reads);
        var result = await executor.ExecuteAsync(preview, grant, default);
        Assert.AreEqual(ToolStatus.Completed, result.Status);
        Assert.AreEqual(proposal.Id, result.OperationId);
        Assert.AreEqual(Literal, result.Text);
        Assert.IsNull(result.Failure);
        Assert.IsTrue(tool.Leases.Single().Disposed);
        var replay = await executor.ExecuteAsync(preview, grant, default);
        Assert.AreEqual(ToolStatus.Denied, replay.Status);
        Assert.IsNull(replay.Text);
        Assert.AreEqual(1, tool.Leases.Single().Reads);
        CollectionAssert.AreEqual(new[] { EventCode.ToolProposed, EventCode.ToolAuthorized, EventCode.ToolStarted,
            EventCode.ToolCompleted, EventCode.ToolDenied }, audit.Events.Select(item => item.Code).ToArray());
        foreach (var representation in new[] { proposal.ToString(), preview.ToString(), preview.Resource.ToString(),
                     proposal.Arguments.ToString(), grant.ToString(), result.ToString(), string.Join("\n", audit.Events) })
        {
            Assert.IsFalse(representation.Contains(Selected, StringComparison.Ordinal));
            Assert.IsFalse(representation.Contains(Literal, StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public async Task MissingGrantAndForeignGrantNeverReadAndReleaseOwnedResource()
    {
        var firstTool = new FixtureTool();
        var otherTool = new FixtureTool();
        await using var first = Enabled(firstTool);
        await using var other = Enabled(otherTool);
        var preview = await first.PrepareAsync(Proposal(), default);
        var foreignPreview = await other.PrepareAsync(Proposal(), default);
        var foreignGrant = await other.ApproveAsync(foreignPreview, default);
        Assert.AreEqual(ToolStatus.Denied, (await first.ExecuteAsync(preview, foreignGrant, default)).Status);
        Assert.AreEqual(0, firstTool.Leases.Single().Reads);
        Assert.IsTrue(firstTool.Leases.Single().Disposed);
        var next = await first.PrepareAsync(Proposal(), default);
        Assert.AreEqual(ToolStatus.Denied, (await first.ExecuteAsync(next, null, default)).Status);
        Assert.IsTrue(firstTool.Leases.All(item => item.Disposed && item.Reads == 0));
        Assert.AreEqual(ToolStatus.Completed, (await other.ExecuteAsync(foreignPreview, foreignGrant, default)).Status);
    }

    [TestMethod]
    public async Task ReselectionRevocationAndDisableInvalidateOldPreviewsAndGrants()
    {
        var tool = new FixtureTool();
        await using var executor = Enabled(tool);
        var first = await executor.PrepareAsync(Proposal(), default);
        var oldGrant = await executor.ApproveAsync(first, default);
        var second = await executor.PrepareAsync(Proposal(), default);
        Assert.IsTrue(tool.Leases[0].Disposed);
        Assert.AreEqual(ToolStatus.Denied, (await executor.ExecuteAsync(first, oldGrant, default)).Status);
        var secondGrant = await executor.ApproveAsync(second, default);
        executor.Revoke();
        Assert.IsTrue(tool.Leases[1].Disposed);
        Assert.AreEqual(ToolStatus.Denied, (await executor.ExecuteAsync(second, secondGrant, default)).Status);
        var third = await executor.PrepareAsync(Proposal(), default);
        var thirdGrant = await executor.ApproveAsync(third, default);
        executor.SetEnabled(false);
        executor.SetEnabled(true);
        Assert.AreEqual(ToolStatus.Denied, (await executor.ExecuteAsync(third, thirdGrant, default)).Status);
        Assert.IsTrue(tool.Leases.All(item => item.Disposed && item.Reads == 0));
    }

    [TestMethod]
    public async Task ExpiryAtExactMonotonicBoundaryCannotBeExtendedByUtcClockRollback()
    {
        var clock = new FixtureClock();
        var tool = new FixtureTool();
        await using var executor = new TrustedToolExecutor(tool, new FixtureAudit(), clock);
        executor.SetEnabled(true);
        var preview = await executor.PrepareAsync(Proposal(), default);
        var grant = await executor.ApproveAsync(preview, default);
        clock.AdvanceMonotonic(TextInspection.ApprovalLifetime);
        clock.Utc = clock.Utc.AddYears(-1);
        var result = await executor.ExecuteAsync(preview, grant, default);
        Assert.AreEqual(ToolStatus.Denied, result.Status);
        Assert.AreEqual(ToolFailure.Expired, result.Failure);
        Assert.IsNull(result.Text);
        Assert.AreEqual(0, tool.Leases.Single().Reads);
        Assert.IsTrue(tool.Leases.Single().Disposed);
    }

    [TestMethod]
    public async Task ParallelReplayAndNewPreparationHaveNoQueueAndOnlyOneRead()
    {
        var entered = Signal();
        var release = Signal();
        var tool = new FixtureTool { Read = async token => { entered.SetResult(); await release.Task.WaitAsync(token); return Literal; } };
        await using var executor = Enabled(tool);
        var preview = await executor.PrepareAsync(Proposal(), default);
        var grant = await executor.ApproveAsync(preview, default);
        var running = executor.ExecuteAsync(preview, grant, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var replay = await executor.ExecuteAsync(preview, grant, default);
        Assert.AreEqual(ToolFailure.Busy, replay.Failure);
        Assert.IsNull(replay.Text);
        Assert.AreEqual(ToolFailure.Busy, (await Assert.ThrowsExactlyAsync<ToolException>(() => executor.PrepareAsync(Proposal(), default))).Failure);
        release.SetResult();
        Assert.AreEqual(ToolStatus.Completed, (await running).Status);
        Assert.AreEqual(1, tool.Leases.Single().Reads);
    }

    [TestMethod]
    public async Task RevocationDuringReadWithholdsLateContentAndReleasesLease()
    {
        var entered = Signal();
        var release = Signal();
        var tool = new FixtureTool { Read = async _ => { entered.SetResult(); await release.Task; return Literal; } };
        await using var executor = Enabled(tool);
        var preview = await executor.PrepareAsync(Proposal(), default);
        var grant = await executor.ApproveAsync(preview, default);
        var running = executor.ExecuteAsync(preview, grant, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        executor.Revoke();
        release.SetResult();
        var result = await running.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.AreEqual(ToolStatus.Cancelled, result.Status);
        Assert.IsNull(result.Text);
        await executor.DisposeAsync();
        Assert.IsTrue(tool.Leases.Single().Disposed);
        var replay = await executor.ExecuteAsync(preview, grant, default);
        Assert.IsNull(replay.Text);
        Assert.IsTrue(replay.Status == ToolStatus.Denied || replay is { Status: ToolStatus.Failed, Failure: ToolFailure.Unavailable });
    }

    [TestMethod]
    public async Task PreCancelledExecutionConsumesGrantWithoutCallingRead()
    {
        var tool = new FixtureTool();
        await using var executor = Enabled(tool);
        var preview = await executor.PrepareAsync(Proposal(), default);
        var grant = await executor.ApproveAsync(preview, default);
        var result = await executor.ExecuteAsync(preview, grant, new CancellationToken(true));
        Assert.AreEqual(ToolStatus.Cancelled, result.Status);
        Assert.IsNull(result.Text);
        Assert.AreEqual(0, tool.Leases.Single().Reads);
        Assert.AreEqual(ToolStatus.Denied, (await executor.ExecuteAsync(preview, grant, default)).Status);
    }

    [TestMethod]
    public async Task StaleBeforeOrAfterReadNeverReturnsSuccessfulContent()
    {
        foreach (var staleAfterRead in new[] { false, true })
        {
            var checks = 0;
            var tool = new FixtureTool { Current = () => ++checks == 1 && staleAfterRead };
            await using var executor = Enabled(tool);
            var preview = await executor.PrepareAsync(Proposal(), default);
            var grant = await executor.ApproveAsync(preview, default);
            var result = await executor.ExecuteAsync(preview, grant, default);
            Assert.AreEqual(ToolFailure.Stale, result.Failure);
            Assert.IsNull(result.Text);
            Assert.AreEqual(staleAfterRead ? 1 : 0, tool.Leases.Single().Reads);
            Assert.IsTrue(tool.Leases.Single().Disposed);
        }
    }

    [TestMethod]
    public async Task AuditFailuresBeforeReadOrBeforePublicationWithholdContent()
    {
        foreach (var failingCode in new[] { EventCode.ToolStarted, EventCode.ToolCompleted })
        {
            var tool = new FixtureTool();
            var audit = new FixtureAudit { Write = entry => entry.Code == failingCode ? Task.FromException(new IOException(Selected + Literal)) : Task.CompletedTask };
            await using var executor = Enabled(tool, audit);
            var preview = await executor.PrepareAsync(Proposal(), default);
            var grant = await executor.ApproveAsync(preview, default);
            var result = await executor.ExecuteAsync(preview, grant, default);
            Assert.AreEqual(ToolFailure.AuditUnavailable, result.Failure);
            Assert.AreEqual(ToolStatus.Failed, result.Status);
            Assert.IsNull(result.Text);
            Assert.AreEqual(failingCode == EventCode.ToolStarted ? 0 : 1, tool.Leases.Single().Reads);
            Assert.IsFalse(result.ToString().Contains(Selected, StringComparison.Ordinal));
            Assert.IsTrue(tool.Leases.Single().Disposed);
        }
    }

    [TestMethod]
    public async Task RevocationDuringTerminalAuditWithholdsContentAndKeepsAdmissionClosed()
    {
        var entered = Signal();
        var release = Signal();
        var tool = new FixtureTool();
        var audit = new FixtureAudit { Write = async entry =>
        {
            if (entry.Code == EventCode.ToolCompleted) { entered.SetResult(); await release.Task; }
        } };
        await using var executor = Enabled(tool, audit);
        var preview = await executor.PrepareAsync(Proposal(), default);
        var grant = await executor.ApproveAsync(preview, default);
        var running = executor.ExecuteAsync(preview, grant, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var newPreparation = executor.PrepareAsync(Proposal(), default);
        var rejected = false;
        try { await newPreparation; }
        catch (ToolException error) { rejected = error.Failure == ToolFailure.Busy; }
        executor.Revoke();
        release.SetResult();
        var result = await running.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.IsTrue(rejected, "Admission must remain closed until terminal auditing and result publication settle.");
        Assert.AreEqual(ToolStatus.Cancelled, result.Status);
        Assert.IsNull(result.Text);
    }

    [TestMethod]
    public async Task CancellationIgnoringReadCannotHoldCallerBeyondOperationBudget()
    {
        var entered = Signal();
        var release = Signal();
        var tool = new FixtureTool { Read = async _ => { entered.SetResult(); await release.Task; return Literal; } };
        await using var executor = Enabled(tool);
        var preview = await executor.PrepareAsync(Proposal(), default);
        var grant = await executor.ApproveAsync(preview, default);
        var running = executor.ExecuteAsync(preview, grant, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var deadline = Task.Delay(TextInspection.OperationTimeout + TimeSpan.FromSeconds(1));
        var completedWithinBudget = await Task.WhenAny(running, deadline) == running;
        release.SetResult();
        var result = await running.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.IsTrue(completedWithinBudget, "A callback ignoring cancellation retained the caller beyond the bounded execution budget.");
        Assert.AreEqual(ToolFailure.Timeout, result.Failure);
        Assert.IsNull(result.Text);
    }

    [TestMethod]
    public async Task CorruptPreparationAndPrivateExceptionMessagesDoNotEscape()
    {
        foreach (var bytes in new long[] { -1, TextInspection.MaxBytes + 1L })
        {
            var tool = new FixtureTool { Bytes = bytes };
            await using var executor = Enabled(tool);
            Assert.AreEqual(ToolFailure.TooLarge, (await Assert.ThrowsExactlyAsync<ToolException>(() => executor.PrepareAsync(Proposal(), default))).Failure);
            Assert.IsTrue(tool.Leases.Single().Disposed);
        }
        var throwing = new FixtureTool { Read = _ => Task.FromException<string>(new IOException(Selected + Literal)) };
        await using var failing = Enabled(throwing);
        var preview = await failing.PrepareAsync(Proposal(), default);
        var grant = await failing.ApproveAsync(preview, default);
        var result = await failing.ExecuteAsync(preview, grant, default);
        Assert.AreEqual(ToolFailure.Internal, result.Failure);
        Assert.IsNull(result.Text);
        Assert.IsFalse(result.ToString().Contains(Literal, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task DisposeCancelsOwnedWorkAndPreventsFutureAdmission()
    {
        var entered = Signal();
        var tool = new FixtureTool { Read = async token => { entered.SetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, token); return Literal; } };
        var executor = Enabled(tool);
        var preview = await executor.PrepareAsync(Proposal(), default);
        var grant = await executor.ApproveAsync(preview, default);
        var running = executor.ExecuteAsync(preview, grant, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await executor.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        Assert.AreEqual(ToolStatus.Cancelled, (await running).Status);
        Assert.IsTrue(tool.Leases.Single().Disposed);
        executor.SetEnabled(true);
        Assert.AreEqual(ToolFailure.Denied, (await Assert.ThrowsExactlyAsync<ToolException>(() => executor.PrepareAsync(Proposal(), default))).Failure);
    }

    [TestMethod]
    public async Task RealStreamChunkingDoesNotCorruptMultibyteLiteralText()
    {
        const string expected = "A😀 €漢字\r\n<script>literal</script>\tB";
        foreach (var size in new[] { 1, 2, 3, 7 })
        {
            using var stream = new FragmentedStream(Encoding.UTF8.GetBytes(expected), size);
            Assert.AreEqual(expected, await BoundedTextReader.ReadAsync(stream, default));
        }
    }

    [TestMethod]
    public async Task IndependentUtf16BoundCountsSurrogatePairsAndAcceptsExactLimit()
    {
        var exact = string.Concat(Enumerable.Repeat("😀", TextInspection.MaxCharacters / 2));
        Assert.AreEqual(TextInspection.MaxCharacters, exact.Length);
        using var accepted = new MemoryStream(Encoding.UTF8.GetBytes(exact));
        Assert.AreEqual(exact, await BoundedTextReader.ReadAsync(accepted, default));
        using var over = new MemoryStream(Encoding.UTF8.GetBytes(new string('x', TextInspection.MaxCharacters + 1)));
        Assert.AreEqual(ToolFailure.TooLarge, (await Assert.ThrowsExactlyAsync<ToolException>(() => BoundedTextReader.ReadAsync(over, default))).Failure);
    }

    [TestMethod]
    public async Task SupplementaryInvisibleFormatRunesAreRejectedAlongsideBmpControls()
    {
        foreach (var control in new[] { "\u202E", "\U000E0001", "\U000E0020" })
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("visible" + control + "text"));
            Assert.AreEqual(ToolFailure.InvalidText, (await Assert.ThrowsExactlyAsync<ToolException>(() => BoundedTextReader.ReadAsync(stream, default))).Failure);
        }
    }

    [TestMethod]
    public async Task NullExecutionIsTypedDenialAndCannotReadAValidPendingFile()
    {
        var tool = new FixtureTool();
        await using var executor = Enabled(tool);
        var preview = await executor.PrepareAsync(Proposal(), default);
        var grant = await executor.ApproveAsync(preview, default);
        var malformed = await executor.ExecuteAsync(null!, grant, default);
        Assert.AreEqual(ToolFailure.InvalidRequest, malformed.Failure);
        Assert.IsNull(malformed.Text);
        Assert.AreEqual(0, tool.Leases.Single().Reads);
        Assert.AreEqual(ToolStatus.Completed, (await executor.ExecuteAsync(preview, grant, default)).Status);
    }

    [TestMethod]
    public void ExactPathPreviewRejectsSupplementaryInvisibleAndMalformedUnicode()
    {
        foreach (var path in new[] { "C:\\public\\hidden\U000E0001.txt", "C:\\public\\hidden\U000E0020.txt", "C:\\public\\broken\uD800.txt" })
            Assert.AreEqual(ToolFailure.InvalidRequest, Assert.ThrowsExactly<ToolException>(() => TextFileInspectionTool.ValidatePath(path)).Failure);
        Assert.AreEqual("C:\\public\\visible😀.txt", TextFileInspectionTool.ValidatePath("C:\\public\\visible😀.txt"));
    }

    private static ToolProposal Proposal() => TextInspection.Propose(Selected);
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static TrustedToolExecutor Enabled(FixtureTool tool, FixtureAudit? audit = null)
    {
        var executor = new TrustedToolExecutor(tool, audit ?? new FixtureAudit());
        executor.SetEnabled(true);
        return executor;
    }

    private sealed class FixtureTool : ITool
    {
        public ToolDescriptor Descriptor { get; init; } = new(TextInspection.ToolId, 1, "Independent public fixture", ToolRisk.Confirm);
        public Func<CancellationToken, Task<string>> Read { get; init; } = _ => Task.FromResult(Literal);
        public Func<bool> Current { get; init; } = () => true;
        public long Bytes { get; init; } = Encoding.UTF8.GetByteCount(Literal);
        public int Preparations { get; private set; }
        public List<FixtureLease> Leases { get; } = [];
        public Task<IPreparedTool> PrepareAsync(ToolProposal proposal, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Preparations++;
            var lease = new FixtureLease(this, proposal.Id);
            Leases.Add(lease);
            return Task.FromResult<IPreparedTool>(lease);
        }
    }

    private sealed class FixtureLease(FixtureTool owner, Guid operation) : IPreparedTool
    {
        public ToolResource Resource { get; } = new(Selected, "independent-file-identity", owner.Bytes);
        public bool Disposed { get; private set; }
        public int Reads { get; private set; }
        public bool IsCurrent() => owner.Current();
        public async Task<string> ExecuteAsync(AuthorizedContext authorization, CancellationToken cancellationToken)
        {
            Assert.IsFalse(Disposed);
            Assert.AreEqual(operation, authorization.ProposalId);
            Reads++;
            return await owner.Read(cancellationToken);
        }
        public void Dispose() => Disposed = true;
    }

    private sealed class FixtureAudit : IEventLog
    {
        public List<AppEvent> Events { get; } = [];
        public Func<AppEvent, Task> Write { get; init; } = _ => Task.CompletedTask;
        public async Task WriteAsync(AppEvent entry, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Events.Add(entry);
            await Write(entry);
        }
    }

    private sealed class FixtureClock : TimeProvider
    {
        private long timestamp;
        public DateTimeOffset Utc { get; set; } = new(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Utc;
        public override long GetTimestamp() => timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public void AdvanceMonotonic(TimeSpan duration) => timestamp += duration.Ticks;
    }

    private sealed class FragmentedStream(byte[] bytes, int chunkSize) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => base.ReadAsync(buffer[..Math.Min(buffer.Length, chunkSize)], cancellationToken);
    }
}
