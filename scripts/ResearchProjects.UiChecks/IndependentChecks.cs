using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Companion.Application.MachineLearning;
using Companion.Desktop;
using ResearchProjectLimits = Companion.Application.MachineLearning.ResearchProjects;

namespace ResearchProjects.UiChecks;

/// <summary>
/// Independently authored native behavior checks. The stores are declared metadata
/// fixtures; no model, dataset, worker, physical keyboard or screen reader is qualified.
/// </summary>
internal static class IndependentChecks
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);
    private static string? selectionStep;
    private static string? waitStep, keyboardStep;
    private static SelectionObservation? selectionObservation;
    private static readonly List<object> keyboardTrace = [], inputCalls = [], keyboardSnapshots = [];
    private static readonly HashSet<string> WaitLabels = new(StringComparer.Ordinal)
    {
        "failed refresh returned", "repaired refresh returned", "deferred catalog began", "deferred create began",
        "explicit modal Open completed", "explicit modal Cancel completed", "native Escape completed",
        "cancelled pending destination", "changed destination revalidation returned", "stale destination dismissed",
        "access key dropdown dismissed", "native theme dropdown opened", "native theme choice applied", "catalog ready"
    };

    private sealed record SelectionObservation(int Index, int ItemCount, bool ListEnabled)
    {
        public string? OwnerPeerType { get; set; }
        public int? ItemPeerCount { get; set; }
        public string? ItemPeerType { get; set; }
        public bool? CurrentItemMatched { get; set; }
        public bool? ItemEnabled { get; set; }
        public bool? SelectionPatternAvailable { get; set; }
    }

    internal static async Task<IReadOnlyList<string>> RunAsync(string outputDirectory)
    {
        var passed = new List<string>();
        var completed = false;
        string? failedCheck = null, failureType = null, failureDetail = null;
        selectionStep = null;
        waitStep = keyboardStep = null;
        selectionObservation = null;
        keyboardTrace.Clear(); inputCalls.Clear(); keyboardSnapshots.Clear();
        var stage = "late catalog";
        void Check(bool condition, string label)
        {
            if (!condition) { failedCheck = label; throw new InvalidOperationException(label); }
            passed.Add(label);
        }
        try
        {
            await LateCatalogAfterCloseAsync(Check);
            stage = "duplicate and late create";
            await DuplicateAndLateCreateAsync(Check);
            stage = "retry and stale selection";
            await RetryClearsStaleSelectionAsync(Check);
            stage = "legacy and capacity";
            await LegacyCapacityAsync(Check);
            stage = "identity and accessibility";
            await IdentityAndAccessibilityAsync(Check);
            stage = "modal actions";
            await ExplicitModalActionsAsync(Check);
            stage = "late and changed open";
            await LateAndChangedOpenAsync(Check);
            stage = "viewport and footer";
            await ViewportRegressionAsync(outputDirectory, Check);
            stage = "native keyboard";
            await KeyboardScenarioAsync(outputDirectory, Check);
            completed = true;
            return passed;
        }
        catch (Exception error) { failureType = error.GetType().Name; failureDetail = selectionStep ?? waitStep ?? keyboardStep; throw; }
        finally
        {
            Directory.CreateDirectory(outputDirectory);
            await File.WriteAllTextAsync(Path.Combine(outputDirectory, "independent-native-checks.json"),
                JsonSerializer.Serialize(new { completed, stage, failedCheck, failureType, failureDetail, waitStep, selectionObservation, passed }));
        }
    }

    private static async Task ViewportRegressionAsync(string outputDirectory, Action<bool, string> check)
    {
        var first = Project(20, "Detector study") with { Directory = @"C:\Mu-UI-fixtures\projects\00000000000000000000000000000020" };
        var second = Project(21, "Calibration notes") with { Directory = @"C:\Mu-UI-fixtures\projects\00000000000000000000000000000021" };
        var legacy = Project(0, "Existing Lab project") with { Directory = @"C:\Mu-UI-fixtures\default-project", IsLegacy = true };
        var observations = new List<object>();
        var completed = false;
        try
        {
            foreach (var (width, height) in new[] { (800, 600), (540, 460) })
            {
                var store = new FixtureStore { Rows = [first, second, legacy] };
                var window = NewWindow(store);
                window.Width = width;
                window.Height = height;
                try
                {
                    window.Show();
                    await ReadyAsync(window, store);
                    SelectRow(Find<ListBox>(window, "ProjectsList"), 0);
                    SetValue(Find<TextBox>(window, "ProjectName"), "Viewport creation candidate");
                    var scroll = Find<ScrollViewer>(window, "ProjectBodyScroll");
                    scroll.ScrollToTop();
                    await Program.SettleAsync(window);
                    var viewport = scroll.Template.FindName("PART_ScrollContentPresenter", scroll) as ScrollContentPresenter
                        ?? throw new InvalidOperationException("Actual body scroll viewport unavailable.");
                    var visible = new Rect(new Point(), viewport.RenderSize);
                    var target = Find<TextBox>(window, "ProjectTarget");
                    var name = Find<TextBox>(window, "ProjectName");
                    var create = Find<Button>(window, "CreateProject");
                    Record("initial");
                    check(visible.Width > 0 && visible.Height > 0 && scroll.VerticalOffset == 0,
                        "initial body viewport measured at top " + width);
                    check(target.Text.Contains(first.Id.ToString("N"), StringComparison.Ordinal)
                        && target.Text.Contains(first.Directory, StringComparison.Ordinal),
                        "viewport reviews exact UIA selected destination " + width);
                    CheckFooter("initial");
                    if (width == 800)
                    {
                        CheckInside(target, "selected destination");
                        CheckInside(name, "new project input");
                        CheckInside(create, "Create action");
                        check(create.IsEnabled, "visible Create action accepts valid name 800");
                    }
                    else
                    {
                        target.BringIntoView(); // Narrow scrolling is a real, legitimate recovery path.
                        await Program.SettleAsync(window);
                        Record("destination scrolled into view");
                        CheckInside(target, "reachable selected destination");
                        CheckFooter("scrolled");
                    }
                    check(store.OpenCalls == 0 && store.CreateCalls == 0 && SelectedId(window) is null,
                        "viewport selection and scrolling grant no action " + width);

                    void CheckInside(FrameworkElement control, string label) => check(control.IsVisible
                        && FullyInside(control.TransformToAncestor(viewport).TransformBounds(new Rect(new Point(), control.RenderSize)), visible),
                        "body viewport fully contains " + label + " " + width);

                    void CheckFooter(string phase)
                    {
                        var client = ClientBounds(window);
                        foreach (var controlName in new[] { "OpenProject", "CancelProjectsButton" })
                        {
                            var control = Find<Button>(window, controlName);
                            check(control.IsVisible && control.IsEnabled && !control.IsDescendantOf(scroll)
                                && FullyInside(control.TransformToAncestor(window).TransformBounds(new Rect(new Point(), control.RenderSize)), client),
                                "footer remains visible outside scroll " + controlName + " " + phase + " " + width);
                        }
                    }

                    void Record(string phase) => observations.Add(new
                    {
                        width, height, phase, scroll.VerticalOffset,
                        viewport = Box(visible), nativeClient = Box(ClientBounds(window)),
                        target = Box(target.TransformToAncestor(viewport).TransformBounds(new Rect(new Point(), target.RenderSize))),
                        name = Box(name.TransformToAncestor(viewport).TransformBounds(new Rect(new Point(), name.RenderSize))),
                        create = Box(create.TransformToAncestor(viewport).TransformBounds(new Rect(new Point(), create.RenderSize)))
                    });
                }
                finally { window.Close(); }
            }
            completed = true;
        }
        finally
        {
            Directory.CreateDirectory(outputDirectory);
            await File.WriteAllTextAsync(Path.Combine(outputDirectory, "independent-viewport-observation.json"),
                JsonSerializer.Serialize(new { completed, scope = "Actual native scroll viewport with three fixed metadata rows; no physical display qualification", observations }));
        }

        static object Box(Rect rectangle) => new { rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height };
        static bool FullyInside(Rect control, Rect viewport)
        {
            const double rounding = 0.01; // Only floating-point layout noise, far below a physical pixel.
            return control.Width > 0 && control.Height > 0 && control.Left >= viewport.Left - rounding
                && control.Top >= viewport.Top - rounding && control.Right <= viewport.Right + rounding
                && control.Bottom <= viewport.Bottom + rounding;
        }
    }

    private static Rect ClientBounds(Window window)
    {
        if (!GetClientRect(new WindowInteropHelper(window).Handle, out var client))
            throw new InvalidOperationException("Native client bounds unavailable.");
        var dpi = VisualTreeHelper.GetDpi(window);
        return new Rect(0, 0, (client.Right - client.Left) / dpi.DpiScaleX, (client.Bottom - client.Top) / dpi.DpiScaleY);
    }

    private static async Task LegacyCapacityAsync(Action<bool, string> check)
    {
        var legacy = Project(0, "Legacy kept in place") with { IsLegacy = true };
        foreach (var namedCount in new[] { ResearchProjectLimits.MaximumProjects - 1, ResearchProjectLimits.MaximumProjects })
        {
            var rows = Enumerable.Range(1, namedCount).Select(i => Project(100 + i, "Project " + i)).Prepend(legacy).ToArray();
            var store = new FixtureStore { Rows = rows };
            var window = NewWindow(store);
            try
            {
                window.Show();
                await ReadyAsync(window, store);
                var name = Find<TextBox>(window, "ProjectName");
                if (name.IsEnabled) SetValue(name, "Capacity candidate");
                else name.Text = "Capacity candidate"; // Deliberate disabled-state fixture injection checks the independent capacity guard.
                await TurnAsync();
                check(Find<ListBox>(window, "ProjectsList").Items.Count == namedCount + 1,
                    "legacy is visible beside named catalog count " + namedCount);
                check(Find<Button>(window, "CreateProject").IsEnabled == (namedCount < ResearchProjectLimits.MaximumProjects),
                    "legacy does not consume named catalog capacity " + namedCount);
                SelectRow(Find<ListBox>(window, "ProjectsList"), 0);
                await TurnAsync();
                check(Find<TextBox>(window, "ProjectTarget").Text.Contains(legacy.Directory, StringComparison.Ordinal)
                    && SelectedId(window) is null && store.OpenCalls == 0,
                    "legacy exact path is reviewed without activation " + namedCount);
            }
            finally { window.Close(); }
        }
    }

    private static async Task RetryClearsStaleSelectionAsync(Action<bool, string> check)
    {
        var first = Project(5, "Same refreshed label");
        var second = Project(6, first.Name);
        var calls = 0;
        var store = new FixtureStore
        {
            List = _ => (++calls) switch
            {
                1 => Task.FromResult<IReadOnlyList<ResearchProject>>([first]),
                2 => Task.FromException<IReadOnlyList<ResearchProject>>(new MlException("project_catalog", "Public fixture catalog unavailable. Retry after repair.")),
                _ => Task.FromResult<IReadOnlyList<ResearchProject>>([second])
            }
        };
        var window = NewWindow(store);
        try
        {
            window.Show();
            await ReadyAsync(window, store);
            SelectRow(Find<ListBox>(window, "ProjectsList"), 0);
            await TurnAsync();
            Invoke(Find<Button>(window, "RetryProjectsButton"));
            await WaitAsync(() => calls == 2 && Find<Button>(window, "RetryProjectsButton").IsEnabled, "failed refresh returned");
            check(!Find<Button>(window, "OpenProject").IsEnabled && SelectedId(window) is null,
                "failed refresh refuses stale activation");
            check(Find<TextBox>(window, "ProjectTarget").Text.Length == 0,
                "failed refresh clears stale identity preview");
            Invoke(Find<Button>(window, "RetryProjectsButton"));
            await WaitAsync(() => calls == 3 && Find<ListBox>(window, "ProjectsList").Items.Count == 1,
                "repaired refresh returned");
            await TurnAsync();
            var list = Find<ListBox>(window, "ProjectsList");
            check(list.SelectedIndex == -1 && !Find<Button>(window, "OpenProject").IsEnabled,
                "repaired catalog does not substitute same-name project");
            SelectRow(list, 0);
            await TurnAsync();
            check(Find<TextBox>(window, "ProjectTarget").Text.Contains(second.Directory, StringComparison.Ordinal)
                && !Find<TextBox>(window, "ProjectTarget").Text.Contains(first.Directory, StringComparison.Ordinal),
                "explicit repaired selection previews only current identity");
        }
        finally { window.Close(); }
    }

    private static async Task LateCatalogAfterCloseAsync(Action<bool, string> check)
    {
        var gate = new TaskCompletionSource<IReadOnlyList<ResearchProject>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new FixtureStore { List = _ => gate.Task };
        var window = NewWindow(store);
        try
        {
            window.Show();
            await WaitAsync(() => store.ListCalls == 1, "deferred catalog began");
            var pendingInitialization = Initialization(window);
            check(!Find<Button>(window, "OpenProject").IsEnabled, "pending catalog denies Open");
            check(!Find<Button>(window, "CreateProject").IsEnabled, "pending catalog denies Create");
            window.Close();
            check(store.LastListToken.IsCancellationRequested, "closing cancels catalog lifetime");
            gate.SetResult([Project(1, "Late catalog")]); // Intentionally noncooperative fixture.
            await pendingInitialization.WaitAsync(Deadline);
            check(Find<ListBox>(window, "ProjectsList").Items.Count == 0, "late catalog cannot repopulate closed picker");
            check(SelectedId(window) is null && store.OpenCalls == 0, "late catalog cannot activate destination");
        }
        finally { if (window.IsVisible) window.Close(); }
    }

    private static async Task DuplicateAndLateCreateAsync(Action<bool, string> check)
    {
        var gate = new TaskCompletionSource<ResearchProject>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new FixtureStore { Create = (_, _) => gate.Task };
        var window = NewWindow(store);
        try
        {
            window.Show();
            await ReadyAsync(window, store);
            SetValue(Find<TextBox>(window, "ProjectName"), "One exact creation");
            var create = Find<Button>(window, "CreateProject");
            Invoke(create);
            await WaitAsync(() => store.CreateCalls == 1, "deferred create began");
            try { Invoke(create); }
            catch (ElementNotEnabledException) { }
            await TurnAsync();
            check(store.CreateCalls == 1, "duplicate UIA Create admits one metadata request");
            check(!Find<Button>(window, "OpenProject").IsEnabled, "pending Create denies Open");
            window.Close();
            check(store.LastCreateToken.IsCancellationRequested, "closing cancels Create lifetime");
            gate.SetResult(Project(2, "One exact creation"));
            await TurnAsync();
            await TurnAsync();
            check(store.ListCalls == 1, "late Create cannot reload closed picker");
            check(SelectedId(window) is null && store.OpenCalls == 0, "late Create cannot activate destination");
        }
        finally { if (window.IsVisible) window.Close(); }
    }

    private static async Task IdentityAndAccessibilityAsync(Action<bool, string> check)
    {
        var first = Project(3, "Same visible name");
        var second = Project(4, first.Name);
        var store = new FixtureStore { Rows = [first, second] };
        var window = NewWindow(store);
        try
        {
            window.Show();
            await ReadyAsync(window, store);
            var list = Find<ListBox>(window, "ProjectsList");
            check(list.SelectedIndex == -1 && !Find<Button>(window, "OpenProject").IsEnabled,
                "loaded catalog requires explicit selection");
            window.UpdateLayout();
            SelectRow(list, 1);
            await TurnAsync();
            var target = Find<TextBox>(window, "ProjectTarget");
            check(target.Text.Contains(second.Id.ToString("N"), StringComparison.Ordinal)
                && target.Text.Contains(second.Directory, StringComparison.Ordinal)
                && !target.Text.Contains(first.Directory, StringComparison.Ordinal),
                "duplicate labels preserve exact selected identity and path");
            check(SelectedId(window) is null && store.OpenCalls == 0 && store.CreateCalls == 0,
                "UIA selection performs no open or creation");
            foreach (var name in new[] { "ProjectsList", "ProjectTarget", "ProjectName", "CreateProject", "OpenProject" })
            {
                var control = Find<FrameworkElement>(window, name);
                var controlPeer = UIElementAutomationPeer.CreatePeerForElement((UIElement)control);
                check(controlPeer is not null && !string.IsNullOrWhiteSpace(controlPeer.GetName()),
                    "UIA accessible name " + name);
            }
            check(AutomationProperties.GetLiveSetting(Find<TextBlock>(window, "ProjectStatus")) != AutomationLiveSetting.Off,
                "status declares relevant live region");
            SetValue(Find<TextBox>(window, "ProjectName"), "Keyboard creation candidate");
            await TurnAsync();
            await ThemeContrastAsync(window, check);
        }
        finally { window.Close(); }
    }

    private static async Task ThemeContrastAsync(Window window, Action<bool, string> check)
    {
        var selector = Find<ComboBox>(window, "ThemeModeSelector");
        foreach (var mode in new[] { 0, 1 })
        {
            selector.SelectedIndex = mode;
            await TurnAsync();
            foreach (var name in new[] { "ProjectName", "ProjectTarget", "ProjectsList", "CreateProject", "OpenProject" })
            {
                var control = Find<Control>(window, name);
                check(control.Foreground is SolidColorBrush && control.Background is SolidColorBrush,
                    "rendered opaque brush roles " + mode + " " + name);
                var foreground = ((SolidColorBrush)control.Foreground).Color;
                var background = ((SolidColorBrush)control.Background).Color;
                check(foreground.A == 255 && background.A == 255 && Contrast(foreground, background) >= 4.5,
                    "rendered text contrast at least 4.5 " + mode + " " + name);
                if (control is TextBox or ListBox)
                    check(control.BorderBrush is SolidColorBrush stroke && stroke.Color.A == 255
                        && Contrast(stroke.Color, background) >= 3,
                        "essential control boundary contrast at least 3 " + mode + " " + name);
            }
        }
        selector.SelectedIndex = 0;
        await TurnAsync();
    }

    private static double Contrast(Color first, Color second)
    {
        static double Linear(byte channel)
        {
            var value = channel / 255d;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
        static double Luminance(Color color) => 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
        var a = Luminance(first);
        var b = Luminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static async Task ExplicitModalActionsAsync(Action<bool, string> check)
    {
        var first = Project(7, "First explicit destination");
        var second = Project(8, "Second explicit destination");
        var store = new FixtureStore { Rows = [first, second] };
        var (opened, openedId) = await ModalAsync(store, async window =>
        {
            await ReadyAsync(window, store);
            SelectRow(Find<ListBox>(window, "ProjectsList"), 1);
            await TurnAsync();
            check(SelectedId(window) is null, "selection before modal Open grants no destination");
            Invoke(Find<Button>(window, "OpenProject"));
            await WaitAsync(() => !window.IsVisible, "explicit modal Open completed");
        });
        check(opened == true && openedId == second.Id, "UIA Open confirms exactly reviewed project");
        var (cancelled, cancelledId) = await ModalAsync(store, async window =>
        {
            await ReadyAsync(window, store);
            SelectRow(Find<ListBox>(window, "ProjectsList"), 0);
            await TurnAsync();
            Invoke(Find<Button>(window, "CancelProjectsButton"));
            await WaitAsync(() => !window.IsVisible, "explicit modal Cancel completed");
        });
        check(cancelled != true && cancelledId is null, "UIA Cancel leaves no accepted destination");
    }

    private static async Task NativeEscapeAsync(Action<bool, string> check)
    {
        var store = new FixtureStore { Rows = [Project(7, "First explicit destination"), Project(8, "Second explicit destination")] };
        var (escaped, escapedId) = await ModalAsync(store, async window =>
        {
            await ReadyAsync(window, store);
            using var trace = new KeyboardTraceScope(window);
            var handle = new WindowInteropHelper(window).Handle;
            window.Activate();
            _ = SetForegroundWindow(handle);
            await TurnAsync();
            check(GetForegroundWindow() == handle, "modal picker owns injected Escape foreground");
            keyboardStep = "native Escape injection";
            SnapshotKeyboard(window, "before Escape");
            SendKey(0x1B);
            await WaitAsync(() => !window.IsVisible, "native Escape completed");
        });
        check(escaped != true && escapedId is null, "native Escape leaves no accepted destination");
    }

    private static async Task<(bool? Result, Guid? Id)> ModalAsync(FixtureStore store, Func<ResearchProjectsWindow, Task> actions)
    {
        var window = NewWindow(store);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Exception? failure = null;
        window.Loaded += async (_, _) =>
        {
            try { await actions(window); }
            catch (Exception error) { failure = error; }
            finally { if (window.IsVisible) window.Close(); finished.TrySetResult(); }
        };
        var result = window.ShowDialog();
        await finished.Task.WaitAsync(Deadline);
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        return (result, SelectedId(window));
    }

    private static async Task LateAndChangedOpenAsync(Action<bool, string> check)
    {
        var project = Project(9, "Reviewed snapshot");
        var gate = new TaskCompletionSource<ResearchProject>(TaskCreationOptions.RunContinuationsAsynchronously);
        var deferred = new FixtureStore { Rows = [project], Open = (_, _) => gate.Task };
        var (cancelled, cancelledId) = await ModalAsync(deferred, async window =>
        {
            await ReadyAsync(window, deferred);
            SelectRow(Find<ListBox>(window, "ProjectsList"), 0);
            // Directly invoke the production async boundary to retain its completion
            // task. Separate modal tests above exercise the real UIA Open action.
            var pendingOpen = (Task)window.GetType().GetMethod("OpenReviewedProjectAsync",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(window, null)!;
            check(deferred.OpenCalls == 1 && !Find<Button>(window, "OpenProject").IsEnabled,
                "pending destination revalidation denies duplicate Open");
            Invoke(Find<Button>(window, "CancelProjectsButton"));
            await WaitAsync(() => !window.IsVisible, "cancelled pending destination");
            gate.SetResult(project); // Intentionally ignores the cancelled lifetime.
            await pendingOpen.WaitAsync(Deadline);
            check(SelectedId(window) is null, "completed late Open cannot accept closed destination");
        });
        check(cancelled != true && cancelledId is null, "late Open preserves cancelled modal result");

        var changed = new FixtureStore
        {
            Rows = [project],
            Open = (_, _) => Task.FromResult(project with { Name = "Changed after review" })
        };
        var (stale, staleId) = await ModalAsync(changed, async window =>
        {
            await ReadyAsync(window, changed);
            SelectRow(Find<ListBox>(window, "ProjectsList"), 0);
            Invoke(Find<Button>(window, "OpenProject"));
            await WaitAsync(() => changed.OpenCalls == 1 && Find<Button>(window, "RetryProjectsButton").IsEnabled,
                "changed destination revalidation returned");
            check(window.IsVisible && SelectedId(window) is null && !Find<Button>(window, "OpenProject").IsEnabled,
                "changed reviewed snapshot refuses modal activation");
            check(Find<TextBox>(window, "ProjectTarget").Text.Length == 0,
                "changed destination clears obsolete preview");
            Invoke(Find<Button>(window, "CancelProjectsButton"));
            await WaitAsync(() => !window.IsVisible, "stale destination dismissed");
        });
        check(stale != true && staleId is null, "stale Open leaves no accepted identity");
    }

    private static void SelectRow(ListBox list, int index)
    {
        selectionStep = "selection layout";
        list.UpdateLayout();
        selectionObservation = new(index, list.Items.Count, list.IsEnabled);
        selectionStep = "selection index bounds";
        if (index < 0 || index >= list.Items.Count) throw new ArgumentOutOfRangeException(nameof(index));
        selectionStep = "selection owner peer";
        var peer = UIElementAutomationPeer.CreatePeerForElement(list) as ListBoxAutomationPeer
            ?? throw new InvalidOperationException("Native list automation peer unavailable.");
        selectionObservation.OwnerPeerType = peer.GetType().Name;
        // Public WPF API synchronously rebuilds item children after ItemsSource
        // replacement. Container wrappers do not own the SelectionItem pattern.
        selectionStep = "selection reset children";
        peer.ResetChildrenCache();
        selectionStep = "selection current children";
        var items = peer.GetChildren()?.OfType<ListBoxItemAutomationPeer>().ToArray() ?? [];
        selectionObservation.ItemPeerCount = items.Length;
        var item = items.ElementAtOrDefault(index)
            ?? throw new InvalidOperationException("Current native item peer unavailable.");
        selectionObservation.ItemPeerType = item.GetType().Name;
        selectionStep = "selection current peer identity";
        selectionObservation.CurrentItemMatched = ReferenceEquals(item.Item, list.Items[index]);
        if (selectionObservation.CurrentItemMatched != true)
            throw new InvalidOperationException("Native item peer did not identify the current catalog item.");
        selectionStep = "selection enabled observation";
        selectionObservation.ItemEnabled = item.IsEnabled();
        selectionStep = "selection pattern";
        var selection = item.GetPattern(PatternInterface.SelectionItem) as ISelectionItemProvider;
        selectionObservation.SelectionPatternAvailable = selection is not null;
        if (selection is null) throw new InvalidOperationException("Native selection pattern unavailable.");
        selectionStep = "selection invoke";
        selection.Select();
        selectionStep = "selection accepted current identity";
        if (!ReferenceEquals(list.SelectedItem, list.Items[index]))
            throw new InvalidOperationException("Native selection did not identify the current catalog item.");
        selectionStep = null;
    }

    private static async Task KeyboardScenarioAsync(string outputDirectory, Action<bool, string> check)
    {
        var environmentBefore = DesktopDiagnostics();
        var completed = false;
        try
        {
            var store = new FixtureStore { Rows = [Project(3, "Same visible name"), Project(4, "Same visible name")] };
            var window = NewWindow(store);
            try
            {
                window.Show();
                await ReadyAsync(window, store);
                SelectRow(Find<ListBox>(window, "ProjectsList"), 1);
                SetValue(Find<TextBox>(window, "ProjectName"), "Keyboard creation candidate");
                await TurnAsync();
                using var trace = new KeyboardTraceScope(window);
                try { await NativeKeyboardAsync(window, check); }
                finally { SnapshotKeyboard(window, "keyboard scenario exit"); }
            }
            finally { window.Close(); }
            await NativeEscapeAsync(check); // Native input remains last; earlier UIA modal checks still run.
            keyboardStep = null;
            completed = true;
        }
        finally
        {
            Directory.CreateDirectory(outputDirectory);
            await File.WriteAllTextAsync(Path.Combine(outputDirectory, "independent-keyboard-observation.json"),
                JsonSerializer.Serialize(new
                {
                    completed, keyboardStep, waitStep, environmentBefore, environmentAfter = DesktopDiagnostics(),
                    injection = "Real Win32 SendInput with checked queued count, mapped scan codes and extended navigation flags",
                    scope = "Fixed synthetic keys and public control labels only; no physical input or session reconfiguration",
                    inputCalls, keyboardTrace, keyboardSnapshots
                }));
        }
    }

    private sealed class KeyboardTraceScope : IDisposable
    {
        private readonly Window window;
        private readonly KeyEventHandler handler;
        internal KeyboardTraceScope(Window window)
        {
            this.window = window;
            handler = (_, e) =>
            {
                if (keyboardTrace.Count < 64) keyboardTrace.Add(new
                {
                    key = PublicKey(e.Key), systemKey = PublicKey(e.SystemKey),
                    alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0,
                    focused = FocusLabel(), windowActive = window.IsActive
                });
            };
            window.AddHandler(Keyboard.PreviewKeyDownEvent, handler, true);
        }
        public void Dispose() => window.RemoveHandler(Keyboard.PreviewKeyDownEvent, handler);
    }

    private static string PublicKey(Key key) => key switch
    {
        Key.System or Key.LeftAlt or Key.RightAlt or Key.Tab or Key.M or Key.N or Key.Down or Key.End or Key.Enter or Key.Escape or Key.Home => key.ToString(),
        _ => "Other"
    };

    private static string FocusLabel()
    {
        var value = Keyboard.FocusedElement as DependencyObject;
        for (var depth = 0; value is not null && depth < 24; depth++, value = Parent(value))
            if (value is FrameworkElement element && element.Name is "ProjectsList" or "ProjectTarget" or "ProjectName" or
                "CreateProject" or "OpenProject" or "ThemeModeSelector" or "RetryProjectsButton" or "CancelProjectsButton") return element.Name;
        return Keyboard.FocusedElement is null ? "None" : "Other element";
    }

    private static void SnapshotKeyboard(Window window, string label)
    {
        if (keyboardSnapshots.Count >= 48) return;
        var handle = new WindowInteropHelper(window).Handle;
        keyboardSnapshots.Add(new
        {
            label, focused = FocusLabel(), windowActive = window.IsActive, visible = window.IsVisible,
            foregroundMatches = GetForegroundWindow() == handle, nativeFocusMatches = GetFocus() == handle,
            themeFocused = Find<ComboBox>(window, "ThemeModeSelector").IsKeyboardFocusWithin,
            themeOpen = Find<ComboBox>(window, "ThemeModeSelector").IsDropDownOpen
        });
    }

    private static object DesktopDiagnostics()
    {
        var threadName = DesktopName(GetThreadDesktop(GetCurrentThreadId()), out var threadError);
        var input = OpenInputDesktop(0, false, 0x0001); // DESKTOP_READOBJECTS only; never switch or reconfigure a desktop.
        var inputError = input == IntPtr.Zero ? Marshal.GetLastWin32Error() : 0;
        string? inputName = null;
        try { if (input != IntPtr.Zero) inputName = DesktopName(input, out inputError); }
        finally { if (input != IntPtr.Zero) _ = CloseDesktop(input); }
        static string PublicDesktop(string? name) => name switch
        { null => "Unavailable", "Default" => "Default", "Winlogon" => "Winlogon", "Screen-saver" => "Screen saver", _ => "Other desktop" };
        return new
        {
            Environment.UserInteractive, sessionId = Process.GetCurrentProcess().SessionId,
            threadDesktop = PublicDesktop(threadName), inputDesktop = PublicDesktop(inputName), threadError, inputError,
            inputMatchesThread = threadName is not null && inputName is not null ? (bool?)(threadName == inputName) : null
        };
    }

    private static string? DesktopName(IntPtr handle, out int error)
    {
        error = 0;
        if (handle == IntPtr.Zero) { error = Marshal.GetLastWin32Error(); return null; }
        var name = new StringBuilder(256);
        if (GetUserObjectInformation(handle, 2, name, name.Capacity * sizeof(char), out _)) return name.ToString();
        error = Marshal.GetLastWin32Error();
        return null;
    }

    private static async Task NativeKeyboardAsync(Window window, Action<bool, string> check)
    {
        keyboardStep = "foreground acquisition";
        var handle = new WindowInteropHelper(window).Handle;
        window.Activate();
        _ = SetForegroundWindow(handle);
        await TurnAsync();
        SnapshotKeyboard(window, "foreground acquired");
        check(GetForegroundWindow() == handle, "shown picker owns injected keyboard foreground");
        var theme = Find<ComboBox>(window, "ThemeModeSelector");
        var name = Find<TextBox>(window, "ProjectName");
        var originalName = name.Text;
        keyboardStep = "access key M";
        Keyboard.Focus(name); // Start on a different target so the access key must move focus.
        await TurnAsync();
        SnapshotKeyboard(window, "before Alt M");
        SendAltKey(0x4D); // Alt+M: Reading mode.
        await Task.Delay(50);
        await TurnAsync();
        SnapshotKeyboard(window, "after Alt M");
        check(theme.IsKeyboardFocusWithin && GetForegroundWindow() == handle
            && window.IsVisible && SelectedId(window) is null,
            "native Alt M focuses reading mode without project activation");
        if (theme.IsDropDownOpen)
        {
            SendKey(0x1B);
            await WaitAsync(() => !theme.IsDropDownOpen, "access key dropdown dismissed");
        }
        keyboardStep = "access key N";
        SendAltKey(0x4E); // Alt+N: New project name.
        await Task.Delay(50);
        await TurnAsync();
        SnapshotKeyboard(window, "after Alt N");
        check(name.IsKeyboardFocusWithin && GetForegroundWindow() == handle && name.Text == originalName
            && window.IsVisible && SelectedId(window) is null,
            "native Alt N focuses project name without project activation");
        Keyboard.Focus(theme);
        await TurnAsync();
        keyboardStep = "theme Alt Down";
        SnapshotKeyboard(window, "before Alt Down");
        SendAltKey(0x28);
        await WaitAsync(() => theme.IsDropDownOpen, "native theme dropdown opened");
        keyboardStep = "theme choice";
        SendKey(0x23); // End.
        SendKey(0x0D); // Enter consumes only the local theme choice.
        await WaitAsync(() => !theme.IsDropDownOpen && theme.SelectedIndex == 1, "native theme choice applied");
        check(window.IsVisible && SelectedId(window) is null, "theme keyboard Enter cannot activate project");
        theme.SelectedIndex = 0;
        await TurnAsync();
        Keyboard.Focus(name);
        await TurnAsync();
        check(name.IsKeyboardFocusWithin, "project name accepts native keyboard focus");
        var visited = new HashSet<DependencyObject>();
        keyboardStep = "native Tab traversal";
        for (var i = 0; i < 18; i++)
        {
            SendKey(0x09); // Actual Win32-injected Tab, not a fabricated routed event.
            await Task.Delay(25);
            SnapshotKeyboard(window, "after Tab");
            if (Keyboard.FocusedElement is DependencyObject focused)
                for (var current = focused; current is not null; current = Parent(current)) visited.Add(current);
            check(window.IsKeyboardFocusWithin, "native Tab remains within picker " + i);
        }
        foreach (var controlName in new[] { "ProjectsList", "ProjectTarget", "ProjectName", "CreateProject", "OpenProject" })
            check(visited.Contains(Find<FrameworkElement>(window, controlName)), "native Tab reaches " + controlName);
        var list = Find<ListBox>(window, "ProjectsList");
        keyboardStep = "native Home";
        Keyboard.Focus(list);
        SendKey(0x24); // Home selects the first row using actual keyboard input.
        await Task.Delay(50);
        check(list.SelectedIndex == 0, "native Home selects intended first project");
        keyboardStep = null;
    }

    private static DependencyObject? Parent(DependencyObject value) => value switch
    {
        Visual or System.Windows.Media.Media3D.Visual3D => VisualTreeHelper.GetParent(value),
        FrameworkContentElement content => content.Parent,
        _ => LogicalTreeHelper.GetParent(value)
    };

    private static ResearchProjectsWindow NewWindow(IResearchProjectStore store) => new(store)
    { Width = 800, Height = 680, WindowStartupLocation = WindowStartupLocation.CenterScreen };

    private static async Task ReadyAsync(Window window, FixtureStore store)
    {
        await WaitAsync(() => store.ListCalls >= 1 && Find<Button>(window, "RetryProjectsButton").IsEnabled, "catalog ready");
        await TurnAsync();
        window.UpdateLayout();
    }

    private static T Find<T>(Window window, string name) where T : FrameworkElement =>
        window.FindName(name) as T ?? throw new InvalidOperationException("Named native control missing: " + name);

    private static Guid? SelectedId(Window window) => (Guid?)window.GetType()
        .GetProperty("SelectedProjectId", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
        .GetValue(window);

    private static Task Initialization(Window window) => (Task)window.GetType()
        .GetMethod("InitializeAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
        .Invoke(window, null)!;

    private static void SetValue(TextBox text, string value)
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(text)!;
        ((IValueProvider)peer.GetPattern(PatternInterface.Value)!).SetValue(value);
    }

    private static void Invoke(Button button)
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(button)!;
        ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)!).Invoke();
    }

    private static async Task WaitAsync(Func<bool> condition, string label)
    {
        if (!WaitLabels.Contains(label)) throw new ArgumentException("Wait diagnostics require an owned fixed label.", nameof(label));
        waitStep = label;
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            if (clock.Elapsed > Deadline) throw new TimeoutException(label);
            await Task.Delay(10);
        }
        waitStep = null;
    }

    private static async Task TurnAsync() => await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

    private static ResearchProject Project(int suffix, string name) => new(
        Guid.Parse($"00000000-0000-0000-0000-{suffix:000000000000}"), name,
        Path.Combine(Path.GetTempPath(), "mu-public-independent-picker", suffix.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        DateTimeOffset.UnixEpoch);

    private static void SendKey(byte key)
    {
        SendNativeKey(key, false);
        SendNativeKey(key, true);
    }

    private static void SendAltKey(byte key)
    {
        SendNativeKey(0x12, false);
        try { SendKey(key); }
        finally { SendNativeKey(0x12, true); }
    }

    private static void SendNativeKey(byte key, bool released)
    {
        var scan = MapVirtualKey(key, 4); // MAPVK_VK_TO_VSC_EX retains the E0 navigation-key prefix.
        var extended = (scan & 0xff00) is 0xe000 or 0xe100 || key is >= 0x21 and <= 0x28;
        var flags = (released ? 0x0002u : 0u) | (extended ? 0x0001u : 0u);
        var inputs = new[] { new NativeInput { Type = 1, Data = new InputUnion
            { Keyboard = new KeyboardInput { VirtualKey = key, Scan = (ushort)(scan & 0xff), Flags = flags } } } };
        var queued = SendInput(1, inputs, Marshal.SizeOf<NativeInput>());
        var error = queued == 1 ? 0 : Marshal.GetLastWin32Error();
        if (inputCalls.Count < 96) inputCalls.Add(new
        {
            key = key switch { 0x12 => "Alt", 0x4D => "M", 0x4E => "N", 0x28 => "Down", 0x23 => "End", 0x24 => "Home", 0x09 => "Tab", 0x0D => "Enter", 0x1B => "Escape", _ => "Other" },
            released, scan = scan & 0xff, extended, queued, error
        });
        if (queued != 1) throw new InvalidOperationException("Native keyboard input was not queued.");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInput { internal uint Type; internal InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] internal KeyboardInput Keyboard;
        [FieldOffset(0)] internal MouseInput Mouse; // Preserves the native INPUT union size/alignment on x86 and x64.
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput { internal ushort VirtualKey, Scan; internal uint Flags, Time; internal UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput { internal int X, Y; internal uint Data, Flags, Time; internal UIntPtr Extra; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { internal int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr window, out NativeRect rectangle);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, [In] NativeInput[] inputs, int size);
    [DllImport("user32.dll", EntryPoint = "MapVirtualKeyW")]
    private static extern uint MapVirtualKey(uint code, uint type);
    [DllImport("user32.dll")]
    private static extern IntPtr GetFocus();
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetThreadDesktop(uint thread);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr OpenInputDesktop(uint flags, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint access);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("user32.dll", EntryPoint = "GetUserObjectInformationW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetUserObjectInformation(IntPtr value, int index, StringBuilder output, int bytes, out int needed);

    private sealed class FixtureStore : IResearchProjectStore
    {
        internal IReadOnlyList<ResearchProject> Rows { get; set; } = [];
        internal Func<CancellationToken, Task<IReadOnlyList<ResearchProject>>>? List { get; init; }
        internal Func<string, CancellationToken, Task<ResearchProject>>? Create { get; init; }
        internal Func<Guid, CancellationToken, Task<ResearchProject>>? Open { get; init; }
        internal int ListCalls { get; private set; }
        internal int CreateCalls { get; private set; }
        internal int OpenCalls { get; private set; }
        internal CancellationToken LastListToken { get; private set; }
        internal CancellationToken LastCreateToken { get; private set; }
        public Task<IReadOnlyList<ResearchProject>> ListAsync(CancellationToken cancellationToken = default)
        { ListCalls++; LastListToken = cancellationToken; return List?.Invoke(cancellationToken) ?? Task.FromResult(Rows); }
        public Task<ResearchProject> CreateAsync(string name, CancellationToken cancellationToken = default)
        { CreateCalls++; LastCreateToken = cancellationToken; return Create?.Invoke(name, cancellationToken) ?? Task.FromException<ResearchProject>(new MlException("fixture_create", "Fixture creation unavailable.")); }
        public Task<ResearchProject> OpenAsync(Guid id, CancellationToken cancellationToken = default)
        { OpenCalls++; return Open?.Invoke(id, cancellationToken) ?? Task.FromResult(Rows.Single(project => project.Id == id)); }
    }
}
