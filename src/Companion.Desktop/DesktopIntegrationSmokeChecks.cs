using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Companion.Core.Desktop;

namespace Companion.Desktop;

/// <summary>Explicit smoke-only native adapter checks; never sends input or writes startup registration.</summary>
internal static class DesktopIntegrationSmokeChecks
{
    internal static async Task RunAsync(string dataDirectory)
    {
        List<string> passed = [];
        List<string> skipped = [];
        List<string> adapterErrors = [];
        string? failure = null;
        var firstWindow = CreateWindow();
        var secondWindow = CreateWindow();
        DesktopIntegration? first = null;
        DesktopIntegration? second = null;
        var recoveryCallbacks = 0;
        void Check(string name, bool condition)
        {
            if (!condition) throw new InvalidOperationException("Desktop integration smoke failed: " + name);
            passed.Add(name);
        }

        try
        {
            using (var disabled = new DesktopIntegration(firstWindow, () => { }, () => { }, () => { },
                adapterErrors.Add, enableSystemIntegration: false))
            {
                Check("disabled-integration-has-no-recovery-route", !disabled.RecoveryAvailable);
                Check("disabled-integration-does-not-register-shortcut",
                    !disabled.TryRegisterHotkey(SummonHotkey.CtrlAltSpace, out var disabledError) && disabledError is not null);
                var refusedClickThrough = false;
                try { disabled.SetClickThrough(true); }
                catch (InvalidOperationException) { refusedClickThrough = true; }
                Check("click-through-refused-without-recovery", refusedClickThrough && !disabled.IsClickThrough);
                var refusedHide = false;
                try { disabled.Hide(); }
                catch (InvalidOperationException) { refusedHide = true; }
                Check("hide-refused-without-recovery", refusedHide);
            }
            first = new(firstWindow, () => { }, () => { }, () => { }, adapterErrors.Add, () => recoveryCallbacks++);
            Check("tray-recovery-available-before-shortcut", first.RecoveryAvailable);
            firstWindow.Show();
            await firstWindow.Dispatcher.InvokeAsync(() => firstWindow.UpdateLayout(), DispatcherPriority.ApplicationIdle);
            var firstHandle = new WindowInteropHelper(firstWindow).Handle;
            Check("owned-window-handle-created", firstHandle != 0);
            var beforeStyle = ReadStyle(firstHandle);
            first.SetClickThrough(true);
            Check("native-click-through-bit-enabled-only", first.IsClickThrough &&
                (ReadStyle(firstHandle) ^ beforeStyle) == 0x20);
            first.Recover();
            Check("recover-clears-native-click-through", !first.IsClickThrough && (ReadStyle(firstHandle) & 0x20) == 0 &&
                firstWindow.IsVisible && recoveryCallbacks == 1);
            first.Hide();
            Check("hide-remains-recoverable", !firstWindow.IsVisible && first.RecoveryAvailable);
            first.Recover();
            Check("hidden-window-recovered", firstWindow.IsVisible && recoveryCallbacks == 2);
            first.SetSleeping(true);
            first.SetSleeping(false);
            Check("sleep-menu-state-can-be-updated", first.RecoveryAvailable);

            SummonHotkey? available = null;
            foreach (var choice in Enum.GetValues<SummonHotkey>())
                if (first.TryRegisterHotkey(choice, out _)) { available = choice; break; }
            if (available is { } selected)
            {
                Check("available-global-shortcut-registered", first.RegisteredHotkey == selected);
                second = new(secondWindow, () => { }, () => { }, () => { }, adapterErrors.Add);
                Check("second-owned-window-cannot-steal-shortcut",
                    !second.TryRegisterHotkey(selected, out var conflict) && conflict is not null && second.RegisteredHotkey is null);
                SummonHotkey? alternate = null;
                foreach (var choice in Enum.GetValues<SummonHotkey>().Where(choice => choice != selected))
                    if (second.TryRegisterHotkey(choice, out _)) { alternate = choice; break; }
                if (alternate is { } retained)
                {
                    Check("conflicting-change-retains-previous-shortcut",
                        !second.TryRegisterHotkey(selected, out var changedError) &&
                        second.RegisteredHotkey == retained && changedError is not null &&
                        changedError.Contains("remains active", StringComparison.Ordinal));
                }
                else skipped.Add("No second preset available; retaining a previous registration could not be exercised.");

                first.SetClickThrough(true);
                first.Dispose();
                Check("dispose-clears-native-click-through", (ReadStyle(firstHandle) & 0x20) == 0 && !first.RecoveryAvailable);
                Check("disposed-shortcut-is-reusable", second.TryRegisterHotkey(selected, out _) && second.RegisteredHotkey == selected);
                second.Dispose();
                Check("second-adapter-disposed", !second.RecoveryAvailable);
            }
            else
            {
                skipped.Add("No summon shortcut preset was available; native conflict, replacement, and release checks were not run.");
                first.SetClickThrough(true);
                first.Dispose();
                Check("dispose-clears-native-click-through", (ReadStyle(firstHandle) & 0x20) == 0 && !first.RecoveryAvailable);
            }
            Check("no-adapter-errors", adapterErrors.Count == 0);
        }
        catch (Exception ex)
        {
            failure = ex.GetType().Name + ": " + ex.Message;
            throw;
        }
        finally
        {
            second?.Dispose();
            first?.Dispose();
            firstWindow.Close();
            secondWindow.Close();
            Directory.CreateDirectory(dataDirectory);
            await File.WriteAllTextAsync(Path.Combine(dataDirectory, "desktop-integration-smoke.json"),
                JsonSerializer.Serialize(new { passed, count = passed.Count, skipped, adapterErrors, failure }));
        }
    }

    private static Window CreateWindow() => new()
    {
        Title = "Desktop Companion integration smoke", Width = 160, Height = 120,
        WindowStyle = WindowStyle.None, AllowsTransparency = true, ShowInTaskbar = false,
        ShowActivated = false, Opacity = 0, WindowStartupLocation = WindowStartupLocation.CenterScreen
    };

    private static long ReadStyle(nint handle) => IntPtr.Size == 8
        ? GetWindowLongPtr(handle, -20).ToInt64() : GetWindowLong(handle, -20);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint handle, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong(nint handle, int index);
}
