using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Companion.Application.MachineLearning;
using Companion.Desktop;

namespace ResearchProjects.UiChecks;

/// <summary>Real native WPF client-area renders. All project data are declared public fixtures.</summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindows() || args.Length != 3 || args[1] is not ("baseline" or "candidate")) return 2;
        var directory = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(directory);
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) =>
        {
            try
            {
                await CaptureAsync(directory, args[1], args[2]);
                if (args[1] == "candidate")
                {
                    var checks = await IndependentChecks.RunAsync(directory);
                    Console.WriteLine($"PASS {checks.Count} independent native project UI checks");
                }
                app.Shutdown(0);
            }
            catch (Exception error)
            {
                // Only public fixtures are used; diagnostics still omit exception messages and paths.
                File.WriteAllText(Path.Combine(directory, "failure.json"), JsonSerializer.Serialize(new { type = error.GetType().Name }));
                Console.Error.WriteLine("FAIL native project UI checks: " + error.GetType().Name);
                app.Shutdown(1);
            }
        };
        return app.Run();
    }

    private static async Task CaptureAsync(string directory, string mode, string source)
    {
        List<object> images = [];
        var store = new PublicProjects();
        foreach (var (width, height) in new[] { (800, 600), (1024, 768), (540, 460) })
        {
            var view = new ResearchProjectsWindow(store) { Width = width, Height = height, ShowInTaskbar = false, ShowActivated = false };
            try
            {
                view.Show();
                await SettleAsync(view);
                var list = (ListBox)view.FindName("ProjectsList");
                if (list.Items.Count != store.Projects.Count) throw new InvalidOperationException("Fixture catalog did not load.");
                list.SelectedIndex = 0;
                await SettleAsync(view);
                Save(view, $"{mode}-{width}x{height}-selected.png");
                if (mode == "candidate")
                {
                    var theme = (ComboBox)view.FindName("ThemeModeSelector");
                    theme.SelectedIndex = 1;
                    await SettleAsync(view);
                    Save(view, $"{mode}-{width}x{height}-selected-archive.png");
                    theme.SelectedIndex = 0;
                    await SettleAsync(view);
                }
                if (width == 800)
                {
                    var process = Process.GetCurrentProcess();
                    var cpu = process.TotalProcessorTime;
                    var watch = Stopwatch.StartNew();
                    await Task.Delay(1500);
                    process.Refresh();
                    File.WriteAllText(Path.Combine(directory, "picker-idle-observation.json"), JsonSerializer.Serialize(new
                    {
                        scope = "Shown standalone picker and verification harness; not companion release qualification",
                        durationMilliseconds = watch.ElapsedMilliseconds,
                        singleCoreCpuPercent = (process.TotalProcessorTime - cpu).TotalMilliseconds / watch.Elapsed.TotalMilliseconds * 100,
                        privateBytes = process.PrivateMemorySize64, workingSetBytes = process.WorkingSet64,
                        hasAnimation = view.HasAnimatedProperties, renderingTier = RenderCapability.Tier >> 16
                    }));
                }
            }
            finally { view.Close(); }
        }
        var empty = new ResearchProjectsWindow(new PublicProjects(empty: true)) { Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        try { empty.Show(); await SettleAsync(empty); Save(empty, mode + "-800x600-empty.png"); }
        finally { empty.Close(); }
        var errorView = new ResearchProjectsWindow(new PublicProjects(fail: true)) { Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        try { errorView.Show(); await SettleAsync(errorView); Save(errorView, mode + "-800x600-error.png"); }
        finally { errorView.Close(); }
        if (mode == "candidate")
        {
            using var contrast = new WindowsContrastScope();
            await Task.Delay(250); // Allow Windows' setting-change broadcast to reach the dispatcher.
            var observed = SystemParameters.HighContrast;
            File.WriteAllText(Path.Combine(directory, "high-contrast-observation.json"), JsonSerializer.Serialize(new
            {
                requested = true, nativeEnabled = contrast.Enabled, wpfEnabled = observed,
                nativeError = contrast.NativeError, scope = "Disposable CI desktop; no physical monitor or screen reader qualification"
            }));
            if (contrast.Enabled && observed)
            {
                var highContrast = new ResearchProjectsWindow(store) { Width = 800, Height = 600, ShowInTaskbar = false };
                try
                {
                    highContrast.Show();
                    await SettleAsync(highContrast);
                    ((ListBox)highContrast.FindName("ProjectsList")).SelectedIndex = 0;
                    await SettleAsync(highContrast);
                    if (((SolidColorBrush)highContrast.FindResource("Mu.Text")).Color != SystemColors.WindowTextColor)
                        throw new InvalidOperationException("Windows contrast palette did not apply.");
                    Save(highContrast, mode + "-800x600-high-contrast.png");
                }
                finally { highContrast.Close(); }
            }
            // An unavailable SPI setting is recorded as unverified, never a passing contrast check.
        }
        File.WriteAllText(Path.Combine(directory, "captures.json"), JsonSerializer.Serialize(new
        {
            source, mode, fixture = "Fixed synthetic metadata only; no dataset, model or worker",
            capture = "Actual shown native window client pixels via Win32 GetDC/BitBlt; no OS chrome; not an HTML mockup",
            images
        }, new JsonSerializerOptions { WriteIndented = true }));

        void Save(ResearchProjectsWindow window, string name)
        {
            var dpi = VisualTreeHelper.GetDpi(window);
            var bitmap = NativeClientCapture.Capture(window);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var output = File.Create(Path.Combine(directory, name));
            encoder.Save(output);
            images.Add(new { name, windowWidthDip = window.ActualWidth, windowHeightDip = window.ActualHeight,
                bitmap.PixelWidth, bitmap.PixelHeight, dpi.DpiScaleX, dpi.DpiScaleY, highContrast = SystemParameters.HighContrast });
        }
    }

    internal static async Task SettleAsync(Window window)
    {
        await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
        await Task.Delay(40);
        await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
    }

    private sealed class PublicProjects(bool empty = false, bool fail = false) : IResearchProjectStore
    {
        public IReadOnlyList<ResearchProject> Projects { get; } = empty ? [] :
        [
            new(new("11111111-1111-4111-8111-111111111111"), "Detector study", @"C:\Mu-UI-fixtures\projects\11111111111141118111111111111111", DateTimeOffset.UnixEpoch),
            new(new("22222222-2222-4222-8222-222222222222"), "Calibration notes", @"C:\Mu-UI-fixtures\projects\22222222222242228222222222222222", DateTimeOffset.UnixEpoch),
            new(Guid.Empty, "Existing Lab project", @"C:\Mu-UI-fixtures\default-project", DateTimeOffset.UnixEpoch, true)
        ];
        public Task<IReadOnlyList<ResearchProject>> ListAsync(CancellationToken cancellationToken = default)
            => fail ? Task.FromException<IReadOnlyList<ResearchProject>>(new MlException("project_catalog", "The project catalog could not be read. Its files were preserved.")) : Task.FromResult(Projects);
        public Task<ResearchProject> CreateAsync(string name, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<ResearchProject> OpenAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Projects.Single(p => p.Id == id));
    }
}
