namespace Companion.Application.Tools;

public enum ToolFailure { InvalidRequest, Unavailable, Denied, Expired, Stale, Busy, Cancelled, Timeout, TooLarge, InvalidText, AuditUnavailable, Internal }
public enum ToolStatus { Completed, Denied, Cancelled, Failed }

public sealed class ToolException(ToolFailure failure) : Exception($"Text inspection: {failure}.")
{
    public ToolFailure Failure { get; } = failure;
}

public enum ToolRisk { DisabledByDefault, Confirm }
public sealed record ToolDescriptor(string Id, int Version, string Description, ToolRisk Risk = ToolRisk.DisabledByDefault);
public sealed record TextInspectionArguments(string Path)
{
    public override string ToString() => "Selected text file arguments";
}
public sealed record ToolProposal(Guid Id, string ToolId, int Version, TextInspectionArguments Arguments)
{
    public override string ToString() => $"Tool proposal {Id}";
}

public sealed record ToolResource(string CanonicalPath, string Identity, long Bytes)
{
    public override string ToString() => "Selected text file resource";
}

// Preview/grant/context cannot be constructed from model JSON. The executor owns
// the actual preview instance and retained resource, not a caller-supplied hash.
public sealed class ToolPreview
{
    internal ToolPreview(ToolProposal proposal, ToolDescriptor descriptor, ToolResource resource, DateTimeOffset expiresAt)
        => (Proposal, Descriptor, Resource, ExpiresAt) = (proposal, descriptor, resource, expiresAt);
    public ToolProposal Proposal { get; }
    public ToolDescriptor Descriptor { get; }
    public ToolResource Resource { get; }
    public DateTimeOffset ExpiresAt { get; }
    public override string ToString() => $"Tool preview {Proposal.Id}";
}

public sealed class PermissionGrant
{
    internal PermissionGrant() { }
    public override string ToString() => "Single-use tool authorization";
}

public sealed class AuthorizedContext
{
    internal AuthorizedContext(Guid proposalId) => ProposalId = proposalId;
    public Guid ProposalId { get; }
}

public sealed record ToolResult(Guid OperationId, ToolStatus Status, ToolFailure? Failure = null, string? Text = null)
{
    public override string ToString() => $"Tool result {OperationId}: {Status}, {Failure}";
}

public interface IToolExecutor
{
    Task<ToolPreview> PrepareAsync(ToolProposal proposal, CancellationToken cancellationToken);
    Task<ToolResult> ExecuteAsync(ToolPreview preview, PermissionGrant? grant, CancellationToken cancellationToken);
}

// Only trusted application controls receive this port; it is never a model tool.
public interface IPermissionService
{
    void SetEnabled(bool enabled);
    void Revoke();
    Task<PermissionGrant> ApproveAsync(ToolPreview preview, CancellationToken cancellationToken);
}

public interface ITool
{
    ToolDescriptor Descriptor { get; }
    Task<IPreparedTool> PrepareAsync(ToolProposal proposal, CancellationToken cancellationToken);
}

public interface IPreparedTool : IDisposable
{
    ToolResource Resource { get; }
    bool IsCurrent();
    Task<string> ExecuteAsync(AuthorizedContext authorization, CancellationToken cancellationToken);
}

public static class TextInspection
{
    public const string ToolId = "mu.text.inspect";
    public const int MaxBytes = 64 * 1024;
    public const int MaxCharacters = 32 * 1024;
    public static readonly TimeSpan ApprovalLifetime = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(5);
    public static ToolProposal Propose(string selectedPath) => new(Guid.NewGuid(), ToolId, 1, new(selectedPath));
}
