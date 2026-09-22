namespace Companion.Core.Desktop;

/// <summary>A desktop rectangle in physical pixels, including negative monitor coordinates.</summary>
public readonly record struct DesktopRect(double Left, double Top, double Width, double Height);

/// <summary>Pure placement rules shared by desktop interaction and its tests.</summary>
public static class WindowPlacement
{
    /// <summary>
    /// Keeps the window's size while constraining each axis to the usable work area.
    /// An oversized axis is anchored at the work area's left or top edge. Snapping
    /// includes the threshold, chooses the nearer edge, and favors left/top on a tie.
    /// All arguments use physical pixels, rather than WPF device-independent pixels.
    /// </summary>
    public static DesktopRect Constrain(DesktopRect window, DesktopRect workArea, double snapDistance = 0)
    {
        ValidateRectangle(window, nameof(window));
        ValidateRectangle(workArea, nameof(workArea));
        if (!double.IsFinite(snapDistance) || snapDistance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(snapDistance), "Snap distance must be finite and nonnegative.");
        }

        var constrained = window with
        {
            Left = ConstrainAxis(window.Left, window.Width, workArea.Left, workArea.Width, snapDistance),
            Top = ConstrainAxis(window.Top, window.Height, workArea.Top, workArea.Height, snapDistance)
        };
        ValidateRectangle(constrained, nameof(window));
        return constrained;
    }

    /// <summary>Converts a nonnegative DIP distance into physical pixels at the target monitor's DPI.</summary>
    public static double DipsToPixels(double distanceInDips, double dpi)
    {
        if (!double.IsFinite(distanceInDips) || distanceInDips < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distanceInDips), "Distance must be finite and nonnegative.");
        }
        if (!double.IsFinite(dpi) || dpi <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dpi), "DPI must be finite and positive.");
        }

        double pixels = distanceInDips * (dpi / 96d);
        if (!double.IsFinite(pixels))
        {
            throw new ArgumentOutOfRangeException(nameof(distanceInDips), "The converted distance must be finite.");
        }
        return pixels;
    }

    private static double ConstrainAxis(double position, double length, double minimum, double available, double snapDistance)
    {
        if (length >= available)
        {
            return minimum;
        }

        double maximum = minimum + (available - length);
        double constrained = Math.Clamp(position, minimum, maximum);
        double minimumDistance = constrained - minimum;
        double maximumDistance = maximum - constrained;
        if (minimumDistance <= snapDistance && minimumDistance <= maximumDistance)
        {
            return minimum;
        }
        if (maximumDistance <= snapDistance)
        {
            return maximum;
        }
        return constrained;
    }

    private static void ValidateRectangle(DesktopRect rectangle, string parameterName)
    {
        if (!double.IsFinite(rectangle.Left) || !double.IsFinite(rectangle.Top) ||
            !double.IsFinite(rectangle.Width) || !double.IsFinite(rectangle.Height) ||
            rectangle.Width <= 0 || rectangle.Height <= 0 ||
            !double.IsFinite(rectangle.Left + rectangle.Width) ||
            !double.IsFinite(rectangle.Top + rectangle.Height))
        {
            throw new ArgumentOutOfRangeException(parameterName, "Rectangle coordinates and edges must be finite, with positive dimensions.");
        }
    }
}
