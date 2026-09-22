namespace Companion.Core.Desktop;

/// <summary>A monitor-relative position, independent of display resolution and avatar size.</summary>
public sealed record SavedPlacement
{
    public const int CurrentVersion = 1;
    public int SchemaVersion { get; init; } = CurrentVersion;
    public string MonitorDeviceId { get; init; } = string.Empty;
    public double HorizontalFraction { get; init; }
    public double VerticalFraction { get; init; }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (SchemaVersion != CurrentVersion) errors.Add("Unsupported placement version.");
        // Device names identify enumerated monitors only; they are never interpreted as file paths.
        if (string.IsNullOrWhiteSpace(MonitorDeviceId) || MonitorDeviceId.Length > 128 ||
            MonitorDeviceId.Any(char.IsControl)) errors.Add("Invalid monitor identity.");
        if (!double.IsFinite(HorizontalFraction) || HorizontalFraction is < 0 or > 1)
            errors.Add("Invalid horizontal position.");
        if (!double.IsFinite(VerticalFraction) || VerticalFraction is < 0 or > 1)
            errors.Add("Invalid vertical position.");
        return errors;
    }

    public static SavedPlacement Capture(string monitorDeviceId, DesktopRect window, DesktopRect workArea)
    {
        var constrained = WindowPlacement.Constrain(window, workArea);
        var result = new SavedPlacement
        {
            MonitorDeviceId = monitorDeviceId,
            HorizontalFraction = Fraction(constrained.Left, constrained.Width, workArea.Left, workArea.Width),
            VerticalFraction = Fraction(constrained.Top, constrained.Height, workArea.Top, workArea.Height)
        };
        if (result.Validate().Count != 0) throw new ArgumentException("Invalid monitor identity.", nameof(monitorDeviceId));
        return result;
    }

    /// <summary>Uses the actual current physical size, including any completed WPF DPI resize.</summary>
    public DesktopRect Restore(DesktopRect window, DesktopRect workArea)
    {
        if (Validate().Count != 0) throw new InvalidOperationException("Cannot restore an invalid saved placement.");
        // Validate both rectangles before calculating available travel on each axis.
        _ = WindowPlacement.Constrain(window, workArea);
        return WindowPlacement.Constrain(window with
        {
            Left = workArea.Left + Math.Max(0, workArea.Width - window.Width) * HorizontalFraction,
            Top = workArea.Top + Math.Max(0, workArea.Height - window.Height) * VerticalFraction
        }, workArea);
    }

    private static double Fraction(double position, double length, double start, double available) =>
        length >= available ? 0 : Math.Clamp((position - start) / (available - length), 0, 1);
}
