using Companion.Application.Diagnostics;

namespace Companion.Application.Tools;

/// <summary>One enabled capability, one pending operation, no execution queue.</summary>
public sealed class TrustedToolExecutor(ITool tool, IEventLog audit, TimeProvider? timeProvider = null)
    : IToolExecutor, IPermissionService, IAsyncDisposable
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly object sync = new();
    private readonly SemaphoreSlim gate = new(1);
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource revocation = new();
    private readonly List<CancellationTokenSource> retired = [];
    private readonly List<Task> cleanup = [];
    private Pending? pending;
    private Task? disposal;
    private bool enabled, disposed, poisoned, cleanupFailed;

    private sealed class Pending(ToolPreview preview, IPreparedTool resource, long timestamp) : IDisposable
    {
        public ToolPreview Preview { get; } = preview;
        public IPreparedTool Resource { get; } = resource;
        public long Timestamp { get; } = timestamp;
        public PermissionGrant? Grant { get; set; }
        public bool Running { get; set; }
        public bool DeferredDisposal { get; set; }
        public ITimer? Expiry { get; set; }
        public void Dispose() { Expiry?.Dispose(); Resource.Dispose(); }
    }

    public void SetEnabled(bool value)
    {
        lock (sync) { if (disposed) return; enabled = value && !poisoned; Invalidate(); }
    }

    public void Revoke() { lock (sync) { if (!disposed) Invalidate(); } }

    private void Invalidate()
    {
        revocation.Cancel();
        if (gate.CurrentCount == 0) retired.Add(revocation);
        else revocation.Dispose();
        revocation = new();
        if (pending is { Running: false }) { Retire(pending); pending = null; }
    }

    private void RequireEnabled()
    {
        if (disposed) throw new ToolException(ToolFailure.Denied);
        if (poisoned) throw new ToolException(ToolFailure.Unavailable);
        if (!enabled) throw new ToolException(ToolFailure.Denied);
    }

    private Pending RequirePreview(ToolPreview preview)
    {
        RequireEnabled();
        if (pending is null || !ReferenceEquals(pending.Preview, preview)) throw new ToolException(ToolFailure.Denied);
        if (clock.GetElapsedTime(pending.Timestamp) >= TextInspection.ApprovalLifetime)
        {
            var expired = pending;
            pending = null;
            Retire(expired);
            throw new ToolException(ToolFailure.Expired);
        }
        if (tool.Descriptor != preview.Descriptor || pending.Resource.Resource != preview.Resource) throw new ToolException(ToolFailure.Stale);
        return pending;
    }

    public async Task<ToolPreview> PrepareAsync(ToolProposal proposal, CancellationToken cancellationToken)
    {
        if (!await gate.WaitAsync(0, cancellationToken).ConfigureAwait(false)) throw new ToolException(ToolFailure.Busy);
        IPreparedTool? resource = null;
        try
        {
            CancellationToken revoked;
            lock (sync) { RequireEnabled(); Invalidate(); revoked = revocation.Token; }
            if (proposal is null || proposal.Id == Guid.Empty || proposal.ToolId != TextInspection.ToolId || proposal.Version != 1 ||
                tool.Descriptor.Id != TextInspection.ToolId || tool.Descriptor.Version != 1 || tool.Descriptor.Risk != ToolRisk.Confirm || proposal.Arguments is null)
                throw new ToolException(ToolFailure.InvalidRequest);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, revoked, lifetime.Token);
            deadline.CancelAfter(TextInspection.OperationTimeout);
            var preparation = Task.Run(() => tool.PrepareAsync(proposal, deadline.Token), CancellationToken.None);
            try { resource = await preparation.WaitAsync(deadline.Token).ConfigureAwait(false); }
            catch (OperationCanceledException)
            {
                if (!await SettleAsync(preparation).ConfigureAwait(false))
                {
                    Poison();
                    TrackCleanup(DisposeLatePreparationAsync(preparation));
                }
                else if (preparation.IsCompletedSuccessfully) resource = preparation.Result;
                throw;
            }
            if (resource.Resource.Bytes is < 0 or > TextInspection.MaxBytes) throw new ToolException(ToolFailure.TooLarge);
            await WriteAuditAsync(EventCode.ToolProposed, proposal.Id, deadline.Token).ConfigureAwait(false);
            lock (sync)
            {
                RequireEnabled();
                deadline.Token.ThrowIfCancellationRequested();
                var preview = new ToolPreview(proposal, tool.Descriptor, resource.Resource, clock.GetUtcNow() + TextInspection.ApprovalLifetime);
                var state = new Pending(preview, resource, clock.GetTimestamp());
                pending = state;
                resource = null;
                state.Expiry = clock.CreateTimer(_ =>
                {
                    lock (sync)
                        if (ReferenceEquals(pending, state) && !state.Running) { Retire(state); pending = null; }
                }, null, TextInspection.ApprovalLifetime, Timeout.InfiniteTimeSpan);
                return preview;
            }
        }
        catch (OperationCanceledException) { throw new ToolException(cancellationToken.IsCancellationRequested ? ToolFailure.Cancelled : ToolFailure.Timeout); }
        catch (ToolException) { throw; }
        catch (Exception) { throw new ToolException(ToolFailure.Internal); }
        finally
        {
            try { resource?.Dispose(); }
            catch (Exception) { lock (sync) { cleanupFailed = true; Poison(); } }
            finally { ReleaseGate(); }
        }
    }

    public async Task<PermissionGrant> ApproveAsync(ToolPreview preview, CancellationToken cancellationToken)
    {
        if (!await gate.WaitAsync(0, cancellationToken).ConfigureAwait(false)) throw new ToolException(ToolFailure.Busy);
        try
        {
            lock (sync)
            {
                var state = RequirePreview(preview);
                if (state.Grant is not null) throw new ToolException(ToolFailure.Denied);
            }
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
            deadline.CancelAfter(TextInspection.OperationTimeout);
            await WriteAuditAsync(EventCode.ToolAuthorized, preview.Proposal.Id, deadline.Token).ConfigureAwait(false);
            lock (sync)
            {
                deadline.Token.ThrowIfCancellationRequested();
                var state = RequirePreview(preview);
                return state.Grant = new();
            }
        }
        catch (OperationCanceledException) { throw new ToolException(cancellationToken.IsCancellationRequested ? ToolFailure.Cancelled : ToolFailure.Timeout); }
        finally { ReleaseGate(); }
    }

    public async Task<ToolResult> ExecuteAsync(ToolPreview preview, PermissionGrant? grant, CancellationToken cancellationToken)
    {
        if (preview is null) return new(Guid.Empty, ToolStatus.Denied, ToolFailure.InvalidRequest);
        if (!await gate.WaitAsync(0, CancellationToken.None).ConfigureAwait(false)) return new(preview.Proposal.Id, ToolStatus.Denied, ToolFailure.Busy);
        Pending? state = null;
        ToolResult result;
        CancellationToken revoked = default;
        try
        {
            lock (sync)
            {
                state = RequirePreview(preview);
                if (grant is null || !ReferenceEquals(state.Grant, grant) || state.Running) throw new ToolException(ToolFailure.Denied);
                state.Grant = null; // Atomic consumption, including failed/cancelled attempts.
                state.Running = true;
                state.Expiry?.Dispose();
                revoked = revocation.Token;
            }
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, revoked, lifetime.Token);
            deadline.CancelAfter(TextInspection.OperationTimeout);
            deadline.Token.ThrowIfCancellationRequested();
            if (!await AwaitOwnedAsync(Task.Run(state.Resource.IsCurrent), state, deadline.Token).ConfigureAwait(false)) throw new ToolException(ToolFailure.Stale);
            await WriteAuditAsync(EventCode.ToolStarted, preview.Proposal.Id, deadline.Token).ConfigureAwait(false);
            var text = await AwaitOwnedAsync(Task.Run(() => state.Resource.ExecuteAsync(new(preview.Proposal.Id), deadline.Token)), state, deadline.Token).ConfigureAwait(false);
            if (text is null || text.Length > TextInspection.MaxCharacters) throw new ToolException(ToolFailure.TooLarge);
            deadline.Token.ThrowIfCancellationRequested();
            if (!await AwaitOwnedAsync(Task.Run(state.Resource.IsCurrent), state, deadline.Token).ConfigureAwait(false)) throw new ToolException(ToolFailure.Stale);
            result = new(preview.Proposal.Id, ToolStatus.Completed, Text: text);
        }
        catch (OperationCanceledException)
        {
            var failure = cancellationToken.IsCancellationRequested || revoked.IsCancellationRequested || lifetime.IsCancellationRequested
                ? ToolFailure.Cancelled : ToolFailure.Timeout;
            result = new(preview.Proposal.Id, failure == ToolFailure.Cancelled ? ToolStatus.Cancelled : ToolStatus.Failed, failure);
        }
        catch (ToolException error)
        {
            result = new(preview.Proposal.Id, error.Failure is ToolFailure.Denied or ToolFailure.Expired ? ToolStatus.Denied : ToolStatus.Failed, error.Failure);
        }
        catch (Exception) { result = new(preview.Proposal.Id, ToolStatus.Failed, ToolFailure.Internal); }
        finally
        {
            lock (sync)
            {
                if (state is not null) { if (ReferenceEquals(pending, state)) pending = null; if (!state.DeferredDisposal) Retire(state); }
            }
        }
        try
        {
            using var auditDeadline = new CancellationTokenSource(TextInspection.OperationTimeout);
            await WriteAuditAsync(result.Status switch
            {
                ToolStatus.Completed => EventCode.ToolCompleted, ToolStatus.Denied => EventCode.ToolDenied,
                ToolStatus.Cancelled => EventCode.ToolCancelled, _ => EventCode.ToolFailed
            }, preview.Proposal.Id, auditDeadline.Token).ConfigureAwait(false);
        }
        catch (Exception) { result = new(preview.Proposal.Id, ToolStatus.Failed, ToolFailure.AuditUnavailable); }
        finally { ReleaseGate(); }
        if (result.Status == ToolStatus.Completed && (revoked.IsCancellationRequested || lifetime.IsCancellationRequested || cancellationToken.IsCancellationRequested))
            result = new(preview.Proposal.Id, ToolStatus.Cancelled, ToolFailure.Cancelled);
        lock (sync) if (cleanupFailed) result = new(preview.Proposal.Id, ToolStatus.Failed, ToolFailure.Unavailable);
        return result;
    }

    private async Task WriteAuditAsync(EventCode code, Guid id, CancellationToken token)
    {
        var write = Task.Run(() => audit.WriteAsync(new(EventArea.ToolAudit, code, id), token));
        try { await write.WaitAsync(token).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            if (!await SettleAsync(write).ConfigureAwait(false))
            {
                Poison();
                TrackCleanup(ObserveLateAsync(write));
            }
            throw;
        }
        catch (Exception) { throw new ToolException(ToolFailure.AuditUnavailable); }
    }

    private void Poison() { lock (sync) { poisoned = true; enabled = false; } }

    private async Task<T> AwaitOwnedAsync<T>(Task<T> work, Pending state, CancellationToken token)
    {
        try { return await work.WaitAsync(token).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            // Return promptly but retain ownership until the underlying native I/O
            // finishes. This instance cannot start another read; reopening the view
            // uses the same App-owned executor. Closing is not a cleanup success.
            if (!await SettleAsync(work).ConfigureAwait(false))
            {
                Poison();
                state.DeferredDisposal = true;
                TrackCleanup(DisposeLateOperationAsync(work, state));
            }
            throw;
        }
    }

    private static async Task<bool> SettleAsync(Task work)
    {
        try { await work.WaitAsync(TimeSpan.FromMilliseconds(250)).ConfigureAwait(false); }
        catch (TimeoutException) { return false; }
        catch (Exception) { /* A completed cancellation/failure has acknowledged cleanup. */ }
        return true;
    }

    private static async Task ObserveLateAsync(Task work) { try { await work.ConfigureAwait(false); } catch (Exception) { } }
    private async Task DisposeLatePreparationAsync(Task<IPreparedTool> work)
    {
        IPreparedTool? late = null;
        try { late = await work.ConfigureAwait(false); } catch (Exception) { }
        try { late?.Dispose(); } catch (Exception) { lock (sync) cleanupFailed = true; }
    }
    private async Task DisposeLateOperationAsync(Task work, Pending state)
    { try { await work.ConfigureAwait(false); } catch (Exception) { } finally { lock (sync) Retire(state); } }

    private void Retire(Pending state)
    {
        try { state.Dispose(); }
        catch (Exception) { cleanupFailed = true; Poison(); }
    }

    private void TrackCleanup(Task work) { lock (sync) { cleanup.RemoveAll(item => item.IsCompletedSuccessfully); cleanup.Add(work); } }

    private void ReleaseGate()
    {
        lock (sync) { foreach (var source in retired) source.Dispose(); retired.Clear(); gate.Release(); }
    }

    public ValueTask DisposeAsync()
    {
        lock (sync) return new(disposal ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        lock (sync) { disposed = true; enabled = false; Invalidate(); lifetime.Cancel(); }
        await gate.WaitAsync().ConfigureAwait(false);
        ReleaseGate();
        Task[] remaining;
        lock (sync) remaining = cleanup.ToArray();
        try { await Task.WhenAll(remaining).WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false); }
        catch (Exception) { throw new ToolException(ToolFailure.Unavailable); }
        finally { lock (sync) { revocation.Dispose(); lifetime.Dispose(); } }
        lock (sync) if (cleanupFailed) throw new ToolException(ToolFailure.Unavailable);
    }
}
