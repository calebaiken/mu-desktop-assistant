using System.Runtime.CompilerServices;
using Microsoft.Win32;

[assembly: InternalsVisibleTo("Companion.Tests")]

namespace Companion.Infrastructure.Desktop;

/// <summary>Explicit, per-user Windows startup registration. Never called by construction.</summary>
public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DesktopCompanion";
    private const string TestKeyPrefix = @"Software\DesktopCompanion.Tests\";
    private static readonly IRegistrationStore ProductionStore = new RegistryRegistrationStore(RunKey, writable: true);

    /// <summary>Builds a quoted, argument-free executable command without reading or writing the registry.</summary>
    public static string BuildCommand(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (!Path.IsPathFullyQualified(executablePath) || executablePath.IndexOfAny(['"', '<', '>', '|', '*', '?']) >= 0 ||
            executablePath.Any(char.IsControl) ||
            !string.Equals(Path.GetExtension(executablePath), ".exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Startup requires the full path to the installed companion executable.", nameof(executablePath));
        var fullPath = Path.GetFullPath(executablePath);
        if (string.Equals(Path.GetFileName(fullPath), "dotnet.exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Startup requires the companion executable, not the dotnet host.", nameof(executablePath));
        return '"' + fullPath + '"';
    }

    public static string? GetCommand() => GetCommand(ProductionStore);

    public static bool MatchesCommand(string? registeredCommand, string executablePath) =>
        string.Equals(registeredCommand, BuildCommand(executablePath), StringComparison.OrdinalIgnoreCase);

    public static bool IsEnabled(string executablePath) => MatchesCommand(GetCommand(), executablePath);

    /// <summary>Changes only this app's HKCU Run value after an explicit user setting change.</summary>
    public static void SetEnabled(bool enabled, string executablePath) => SetEnabled(enabled, executablePath, ProductionStore);

    internal static string? GetCommand(IRegistrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        return store.GetCommand();
    }

    internal static void SetEnabled(bool enabled, string executablePath, IRegistrationStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        var command = BuildCommand(executablePath);
        if (enabled)
        {
            if (!File.Exists(executablePath)) throw new FileNotFoundException("The companion executable could not be found.", executablePath);
            store.WriteCommand(command);
        }
        else store.DeleteCommand();
    }

    // The test factory cannot target Run or any arbitrary key, including another user's hive.
    internal static IRegistrationStore CreateTestStore(string keyPath, bool writable = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);
        if (!keyPath.StartsWith(TestKeyPrefix, StringComparison.Ordinal) || keyPath.Length != TestKeyPrefix.Length + 32 ||
            !Guid.TryParseExact(keyPath[TestKeyPrefix.Length..], "N", out var testId) || testId == Guid.Empty)
            throw new ArgumentException("Registry tests require a unique key in the companion test namespace.", nameof(keyPath));
        return new RegistryRegistrationStore(keyPath, writable);
    }

    internal interface IRegistrationStore
    {
        string? GetCommand();
        void WriteCommand(string command);
        void DeleteCommand();
    }

    private sealed class RegistryRegistrationStore(string keyPath, bool writable) : IRegistrationStore
    {
        public string? GetCommand()
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: false);
            return key?.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
        }

        public void WriteCommand(string command)
        {
            using var key = writable
                ? Registry.CurrentUser.CreateSubKey(keyPath, writable: true)
                : Registry.CurrentUser.OpenSubKey(keyPath, writable: false);
            if (key is null) throw new IOException("The current user's startup registry key is unavailable.");
            key.SetValue(ValueName, command, RegistryValueKind.String);
        }

        public void DeleteCommand()
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
