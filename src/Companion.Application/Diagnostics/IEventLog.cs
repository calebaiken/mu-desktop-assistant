namespace Companion.Application.Diagnostics;

// No free-text payload or exception parameter: secrets and user content cannot be logged.
public enum EventArea { Application, Diagnostics, Error, ToolAudit }
public enum EventCode
{
    Started, Stopping, Activated, SettingsSaved, SettingsRecovered, SettingsUnsupported,
    SettingsSaveFailed, StorageUnavailable, InstanceUnavailable, UnexpectedFailure,
    ToolProposed, ToolAuthorized, ToolStarted, ToolCompleted, ToolDenied, ToolCancelled, ToolFailed
}

public sealed record AppEvent(EventArea Area, EventCode Code, Guid OperationId, int? DurationMs = null);

public interface IEventLog
{
    Task WriteAsync(AppEvent entry, CancellationToken cancellationToken);
}
