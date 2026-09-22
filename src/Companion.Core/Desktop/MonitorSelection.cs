namespace Companion.Core.Desktop;

/// <summary>An enumerated display's identity and usable area in physical desktop pixels.</summary>
public readonly record struct DesktopMonitor(string DeviceId, DesktopRect WorkArea);

/// <summary>Pure, enumeration-order-independent rules for cycling through connected displays.</summary>
public static class MonitorSelection
{
    /// <summary>
    /// Cycles by work-area top, left, then case-insensitive ordinal device identity. Zero or one
    /// display is a no-op. If the current display disappeared, choose the first surviving display.
    /// Duplicate identities are rejected because they cannot identify an unambiguous restore target.
    /// </summary>
    public static DesktopMonitor? Next(IReadOnlyList<DesktopMonitor> monitors, string currentMonitorDeviceId)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        ValidateIdentity(currentMonitorDeviceId, nameof(currentMonitorDeviceId));
        var snapshot = monitors.ToArray();
        var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var monitor in snapshot)
        {
            ValidateIdentity(monitor.DeviceId, nameof(monitors));
            _ = WindowPlacement.Constrain(monitor.WorkArea, monitor.WorkArea);
            if (!identities.Add(monitor.DeviceId))
                throw new ArgumentException("Monitor identities must be unique.", nameof(monitors));
        }
        if (snapshot.Length < 2) return null;
        var ordered = snapshot.OrderBy(monitor => monitor.WorkArea.Top)
            .ThenBy(monitor => monitor.WorkArea.Left)
            .ThenBy(monitor => monitor.DeviceId, StringComparer.OrdinalIgnoreCase).ToArray();
        var current = Array.FindIndex(ordered, monitor =>
            string.Equals(monitor.DeviceId, currentMonitorDeviceId, StringComparison.OrdinalIgnoreCase));
        return ordered[(current + 1) % ordered.Length];
    }

    /// <summary>
    /// Retargets the current monitor-relative fractions to the next display without resizing or
    /// moving a window. Restore later uses the actual size after the target display's DPI change.
    /// </summary>
    public static SavedPlacement? NextPlacement(IReadOnlyList<DesktopMonitor> monitors, string currentMonitorDeviceId,
        DesktopRect window, DesktopRect currentWorkArea)
    {
        var current = SavedPlacement.Capture(currentMonitorDeviceId, window, currentWorkArea);
        var next = Next(monitors, currentMonitorDeviceId);
        return next is { } target ? current with { MonitorDeviceId = target.DeviceId } : null;
    }

    private static void ValidateIdentity(string identity, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(identity) || identity.Length > 128 || identity.Any(char.IsControl))
            throw new ArgumentException("Invalid monitor identity.", parameterName);
    }
}
