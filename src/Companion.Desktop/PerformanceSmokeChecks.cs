using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Companion.Core.Configuration;
using Companion.Infrastructure.Diagnostics;

namespace Companion.Desktop;

/// <summary>Opt-in, isolated-profile measurement harness; not a background product feature.</summary>
internal static class PerformanceSmokeChecks
{
    private static readonly TimeSpan Warmup = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    internal static async Task RunAsync(CompanionWindow window, CompanionSettings settings,
        string dataDirectory, PerformanceObserver observer, CancellationToken cancellationToken)
    {
        var wasVisible = window.IsVisible;
        var originalState = window.WindowState;
        var startedAt = DateTimeOffset.UtcNow;
        var dpi = VisualTreeHelper.GetDpi(window);
        var samples = new List<IntervalSample>();
        var content = (UIElement)window.Content;
        var inputWasEnabled = content.IsEnabled;
        var menuWasEnabled = window.ContextMenu.IsEnabled;
        void MarkMeasurement()
        {
            window.Title = "Desktop Companion · Resource measurement";
            window.NameLabel.Text = "Resource check";
            window.MovementStatus.Text = "Measuring idle behavior · Closes automatically";
        }
        try
        {
            // Keep the renderer/window enabled while excluding input from an idle measurement.
            content.IsEnabled = false;
            window.ContextMenu.IsEnabled = false;
            window.WindowState = WindowState.Normal;
            window.Apply(settings with { ReducedMotion = true });
            MarkMeasurement();
            var counterCalibration = await Task.Run(CalibrateCounters, cancellationToken);
            window.Apply(settings with { ReducedMotion = false });
            MarkMeasurement();
            window.Show();
            await Task.Delay(Warmup, cancellationToken);
            samples.Add(await MeasureAsync(window, "visible-idle", observer, cancellationToken));

            window.Apply(settings with { ReducedMotion = true });
            MarkMeasurement();
            samples.Add(await MeasureAsync(window, "visible-reduced-motion", observer, cancellationToken));

            window.Apply(settings with { ReducedMotion = false });
            MarkMeasurement();
            window.Hide();
            samples.Add(await MeasureAsync(window, "hidden", observer, cancellationToken));

            var report = new
            {
                schemaVersion = 1,
                startedAt,
                completedAt = DateTimeOffset.UtcNow,
                warmupSeconds = Warmup.TotalSeconds,
                requestedIntervalSeconds = Interval.TotalSeconds,
                logicalProcessorCount = Environment.ProcessorCount,
                os = RuntimeInformation.OSDescription,
                framework = RuntimeInformation.FrameworkDescription,
                architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                dpiX = dpi.PixelsPerInchX,
                dpiY = dpi.PixelsPerInchY,
                windowsAnimationsEnabled = SystemParameters.ClientAreaAnimation,
                highContrast = SystemParameters.HighContrast,
                avatarScale = settings.AvatarScale,
                avatarOpacity = settings.AvatarOpacity,
                trayEnabled = false,
                settingsAndMovementDisabled = true,
                observerMode = observer.ToString().ToLowerInvariant(),
                threadSnapshotCaptureAttempts = samples.Sum(sample => sample.ThreadSnapshotCaptureAttempts),
                method = "60-second intervals; one-second direct GetProcessTimes/K32GetProcessMemoryInfo samples; " +
                    (observer == PerformanceObserver.Threads ? "thread snapshots at interval boundaries; " : "no thread snapshots; ") +
                    "no forced garbage collection; CPU includes all enabled harness overhead",
                idleAnimationMethod = "Ten-second wait then 120-millisecond blink; no idle animation clocks; animation flags describe enabled motion policy, completed blink deltas record actual timed visual-property transitions",
                cpuFormula = "oneCorePercent = cpuSeconds / elapsedSeconds * 100; machinePercent = oneCorePercent / logicalProcessorCount",
                counterCalibration,
                samples
            };
            Directory.CreateDirectory(dataDirectory);
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            await File.WriteAllTextAsync(Path.Combine(dataDirectory, "performance-smoke.json"), json, cancellationToken);
        }
        finally
        {
            // A close/cancellation owns the lifetime: never resurrect its window.
            if (!cancellationToken.IsCancellationRequested && window.IsLoaded)
            {
                window.Apply(settings);
                content.IsEnabled = inputWasEnabled;
                window.ContextMenu.IsEnabled = menuWasEnabled;
                window.MovementStatus.Text = "Drag to move · Arrow keys when focused";
                window.WindowState = originalState;
                if (wasVisible) window.Show();
                else window.Hide();
            }
        }
    }

    private static async Task<IntervalSample> MeasureAsync(CompanionWindow window, string mode, PerformanceObserver observer,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var first = ProcessResourceSampler.Capture();
        var cpuStart = first.CpuTime;
        var threadSnapshotCaptureAttempts = 0;
        List<ThreadCpuReading>? CaptureThreads()
        {
            if (observer != PerformanceObserver.Threads) return null;
            threadSnapshotCaptureAttempts++;
            return ThreadCpuDiagnostic.Capture();
        }
        var threadsAtStart = CaptureThreads();
        var privateStart = first.PrivateBytes;
        var privatePeak = privateStart;
        var workingSetPeak = first.WorkingSetBytes;
        var animationAtStart = window.IsIdleAnimationRunning;
        var animationWasEnabled = animationAtStart;
        var animationWasDisabled = !animationAtStart;
        var blinksAtStart = window.AvatarRenderer.CompletedBlinkCount;
        var idleTimerObservedEnabled = window.AvatarRenderer.IsIdleBlinkTimerEnabled;
        var ownedClockPeak = window.AvatarRenderer.OwnedAnimationClockCount;
        var visibleAtStart = window.IsVisible;
        var stateAtStart = window.WindowState.ToString();
        var counterSamples = 0;
        while (stopwatch.Elapsed < Interval)
        {
            var remaining = Interval - stopwatch.Elapsed;
            if (remaining <= TimeSpan.Zero) break;
            await Task.Delay(remaining < TimeSpan.FromSeconds(1) ? remaining : TimeSpan.FromSeconds(1), cancellationToken);
            var sample = ProcessResourceSampler.Capture();
            privatePeak = Math.Max(privatePeak, sample.PrivateBytes);
            workingSetPeak = Math.Max(workingSetPeak, sample.WorkingSetBytes);
            animationWasEnabled |= window.IsIdleAnimationRunning;
            animationWasDisabled |= !window.IsIdleAnimationRunning;
            idleTimerObservedEnabled |= window.AvatarRenderer.IsIdleBlinkTimerEnabled;
            ownedClockPeak = Math.Max(ownedClockPeak, window.AvatarRenderer.OwnedAnimationClockCount);
            counterSamples++;
        }
        var threadsAtEnd = CaptureThreads();
        var last = ProcessResourceSampler.Capture();
        var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
        var cpuSeconds = (last.CpuTime - cpuStart).TotalSeconds;
        var privateEnd = last.PrivateBytes;
        privatePeak = Math.Max(privatePeak, privateEnd);
        workingSetPeak = Math.Max(workingSetPeak, last.WorkingSetBytes);
        if (elapsedSeconds <= 0 || cpuSeconds < 0)
            throw new InvalidOperationException("Process counters did not produce a valid interval.");
        var oneCorePercent = cpuSeconds / elapsedSeconds * 100;
        return new(mode, elapsedSeconds, cpuSeconds, oneCorePercent,
            oneCorePercent / Environment.ProcessorCount, privateStart,
            privateEnd, privatePeak, workingSetPeak, counterSamples,
            visibleAtStart, window.IsVisible, stateAtStart, window.WindowState.ToString(),
            animationAtStart, window.IsIdleAnimationRunning, animationWasEnabled, animationWasDisabled,
            window.AvatarRenderer.CompletedBlinkCount - blinksAtStart, idleTimerObservedEnabled, ownedClockPeak,
            threadSnapshotCaptureAttempts,
            threadsAtStart, threadsAtEnd);
    }

    private static object CalibrateCounters()
    {
        // Separate diagnostic experiment before warmup; no cost is subtracted from the intervals.
        const int reads = 60;
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        _ = process.PrivateMemorySize64;
        _ = ProcessResourceSampler.Capture();
        var first = ProcessResourceSampler.Capture();
        var timer = Stopwatch.StartNew();
        for (var index = 0; index < reads; index++)
        {
            process.Refresh();
            _ = process.PrivateMemorySize64;
            _ = process.WorkingSet64;
        }
        var managedElapsedSeconds = timer.Elapsed.TotalSeconds;
        var managedCpuSeconds = (ProcessResourceSampler.Capture().CpuTime - first.CpuTime).TotalSeconds;
        first = ProcessResourceSampler.Capture();
        timer.Restart();
        for (var index = 0; index < reads; index++) _ = ProcessResourceSampler.Capture();
        var nativeElapsedSeconds = timer.Elapsed.TotalSeconds;
        var nativeCpuSeconds = (ProcessResourceSampler.Capture().CpuTime - first.CpuTime).TotalSeconds;
        return new { reads, managedElapsedSeconds, managedCpuSeconds, nativeElapsedSeconds, nativeCpuSeconds,
            method = "Same-process back-to-back counter reads; calibration is excluded from interval timing, never subtracted from measured CPU" };
    }

    private sealed record IntervalSample(string Mode, double ElapsedSeconds, double CpuSeconds,
        double OneCorePercent, double MachinePercent, long PrivateBytesStart, long PrivateBytesEnd,
        long PrivateBytesPeak, long WorkingSetBytesPeak, int CounterSamples,
        bool VisibleAtStart, bool VisibleAtEnd, string WindowStateAtStart, string WindowStateAtEnd,
        bool AnimationAtStart, bool AnimationAtEnd, bool AnimationObservedEnabled, bool AnimationObservedDisabled,
        long CompletedIdleBlinks, bool IdleTimerObservedEnabled, int OwnedAnimationClockPeak,
        int ThreadSnapshotCaptureAttempts,
        List<ThreadCpuReading>? ThreadsAtStart, List<ThreadCpuReading>? ThreadsAtEnd);
}
