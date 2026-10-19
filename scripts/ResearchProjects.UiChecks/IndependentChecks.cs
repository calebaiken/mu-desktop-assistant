using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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

namespace ResearchProjects.UiChecks;

/// <summary>
/// Independently authored native behavior checks. The stores are declared metadata
/// fixtures; no model, dataset, worker, physical keyboard or screen reader is qualified.
/// </summary>
internal static class IndependentChecks
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);

    internal static async Task<IReadOnlyList<string>> RunAsync(string outputDirectory)
    {
        var passed = new List<string>();
        var completed = false;
        string? failedCheck = null, failureType = null;
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
            stage = "identity and accessibility";
            await IdentityAndAccessibilityAsync(Check);
            stage = "modal actions";
            await ExplicitModalActionsAsync(Check);
            completed = true;
            return passed;
        }
        catch (Exception error) { failureType = error.GetType().Name; throw; }
        finally
        {
            Directory.CreateDirectory(outputDirectory);
            await File.WriteAllTextAsync(Path.Combine(outputDirectory, "independent-native-checks.json"),
                JsonSerializer.Serialize(new { completed, stage, failedCheck, failureType, passed }));
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
            check(!Find<Button>(window, "OpenProject").IsEnabled, "pending catalog denies Open");
            check(!Find<Button>(window, "CreateProject").IsEnabled, "pending catalog denies Create");
            window.Close();
            check(store.LastListToken.IsCancellationRequested, "closing cancels catalog lifetime");
            gate.SetResult([Project(1, "Late catalog")]); // Intentionally noncooperative fixture.
            await TurnAsync();
            await TurnAsync();
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
            await NativeKeyboardAsync(window, check);
        }
        finally { window.Close(); }
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

    private static void SelectRow(ListBox list, int index)
    {
        list.UpdateLayout();
        var row = (ListBoxItem?)list.ItemContainerGenerator.ContainerFromIndex(index)
            ?? throw new InvalidOperationException("Native list container unavailable.");
        var peer = UIElementAutomationPeer.CreatePeerForElement(row)
            ?? throw new InvalidOperationException("Native selection peer unavailable.");
        ((ISelectionItemProvider)(peer.GetPattern(PatternInterface.SelectionItem)
            ?? throw new InvalidOperationException("Native selection pattern unavailable."))).Select();
    }

    private static async Task NativeKeyboardAsync(Window window, Action<bool, string> check)
    {
        var handle = new WindowInteropHelper(window).Handle;
        window.Activate();
        _ = SetForegroundWindow(handle);
        await TurnAsync();
        check(GetForegroundWindow() == handle, "shown picker owns injected keyboard foreground");
        var name = Find<TextBox>(window, "ProjectName");
        Keyboard.Focus(name);
        await TurnAsync();
        check(name.IsKeyboardFocusWithin, "project name accepts native keyboard focus");
        var visited = new HashSet<DependencyObject>();
        for (var i = 0; i < 18; i++)
        {
            SendKey(0x09); // Actual Win32-injected Tab, not a fabricated routed event.
            await Task.Delay(25);
            if (Keyboard.FocusedElement is DependencyObject focused)
                for (var current = focused; current is not null; current = Parent(current)) visited.Add(current);
            check(window.IsKeyboardFocusWithin, "native Tab remains within picker " + i);
        }
        foreach (var controlName in new[] { "ProjectsList", "ProjectTarget", "ProjectName", "CreateProject", "OpenProject" })
            check(visited.Contains(Find<FrameworkElement>(window, controlName)), "native Tab reaches " + controlName);
        var list = Find<ListBox>(window, "ProjectsList");
        Keyboard.Focus(list);
        SendKey(0x24); // Home selects the first row using actual keyboard input.
        await Task.Delay(50);
        check(list.SelectedIndex == 0, "native Home selects intended first project");
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
        await WaitAsync(() => store.ListCalls >= 1 && Find<TextBox>(window, "ProjectName").IsEnabled, "catalog ready");
        await TurnAsync();
        window.UpdateLayout();
    }

    private static T Find<T>(Window window, string name) where T : FrameworkElement =>
        window.FindName(name) as T ?? throw new InvalidOperationException("Named native control missing: " + name);

    private static Guid? SelectedId(Window window) => (Guid?)window.GetType()
        .GetProperty("SelectedProjectId", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
        .GetValue(window);

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
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            if (clock.Elapsed > Deadline) throw new TimeoutException(label);
            await Task.Delay(10);
        }
    }

    private static async Task TurnAsync() => await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

    private static ResearchProject Project(int suffix, string name) => new(
        Guid.Parse($"00000000-0000-0000-0000-{suffix:000000000000}"), name,
        Path.Combine(Path.GetTempPath(), "mu-public-independent-picker", suffix.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        DateTimeOffset.UnixEpoch);

    private static void SendKey(byte key)
    {
        keybd_event(key, 0, 0, UIntPtr.Zero);
        keybd_event(key, 0, 0x0002, UIntPtr.Zero);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);

    private sealed class FixtureStore : IResearchProjectStore
    {
        internal IReadOnlyList<ResearchProject> Rows { get; set; } = [];
        internal Func<CancellationToken, Task<IReadOnlyList<ResearchProject>>>? List { get; init; }
        internal Func<string, CancellationToken, Task<ResearchProject>>? Create { get; init; }
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
        { OpenCalls++; return Task.FromResult(Rows.Single(project => project.Id == id)); }
    }
}
