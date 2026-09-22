using System.Security;
using Companion.Infrastructure.Desktop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;

namespace Companion.Tests;

/// <summary>Real registry I/O restricted to a unique test key; never touches the user's Run key.</summary>
[TestClass]
public sealed class StartupRegistrationRegistryTests
{
    [TestMethod]
    public void EnableReadAndDisableUseNativeRegistryAndAreIdempotent()
    {
        using var fixture = new RegistryFixture();
        using var files = new TestDirectory();
        var executable = files.FilePath("Companion.Desktop.exe");
        File.WriteAllBytes(executable, [0x4d, 0x5a]); // Registration never executes the file.
        using (var key = fixture.Open(writable: true)) key.SetValue("UnrelatedValue", "preserved", RegistryValueKind.String);
        var store = StartupRegistration.CreateTestStore(fixture.KeyPath);
        Assert.IsNull(StartupRegistration.GetCommand(store));
        StartupRegistration.SetEnabled(false, executable, store);
        StartupRegistration.SetEnabled(true, executable, store);
        StartupRegistration.SetEnabled(true, executable, store);

        var command = StartupRegistration.BuildCommand(executable);
        Assert.AreEqual(command, StartupRegistration.GetCommand(store));
        Assert.IsTrue(StartupRegistration.MatchesCommand(StartupRegistration.GetCommand(store), executable));
        using (var key = fixture.Open(writable: false))
        {
            Assert.AreEqual(command, key.GetValue("DesktopCompanion", null, RegistryValueOptions.DoNotExpandEnvironmentNames));
            Assert.AreEqual(RegistryValueKind.String, key.GetValueKind("DesktopCompanion"));
            Assert.AreEqual("preserved", key.GetValue("UnrelatedValue"));
        }

        StartupRegistration.SetEnabled(false, executable, store);
        StartupRegistration.SetEnabled(false, executable, store);
        Assert.IsNull(StartupRegistration.GetCommand(store));
        using var after = fixture.Open(writable: false);
        Assert.IsNull(after.GetValue("DesktopCompanion"));
        Assert.AreEqual("preserved", after.GetValue("UnrelatedValue"));
    }

    [TestMethod]
    public void MissingExecutableAndInvalidCommandPreserveRegisteredValue()
    {
        using var fixture = new RegistryFixture();
        using var files = new TestDirectory();
        const string previousCommand = @"""C:\Previous App\Companion.Desktop.exe""";
        using (var key = fixture.Open(writable: true)) key.SetValue("DesktopCompanion", previousCommand, RegistryValueKind.String);
        var store = StartupRegistration.CreateTestStore(fixture.KeyPath);

        Assert.ThrowsExactly<FileNotFoundException>(() =>
            StartupRegistration.SetEnabled(true, files.FilePath("missing.exe"), store));
        Assert.ThrowsExactly<ArgumentException>(() =>
            StartupRegistration.SetEnabled(true, @"C:\Bad App\Companion.Desktop.exe --option", store));
        Assert.ThrowsExactly<ArgumentException>(() => StartupRegistration.SetEnabled(false, "relative.exe", store));
        Assert.AreEqual(previousCommand, StartupRegistration.GetCommand(store));
    }

    [TestMethod]
    public void NativeReadOnlyKeyWriteAndDeleteFailuresPreserveRegisteredValue()
    {
        using var fixture = new RegistryFixture();
        using var files = new TestDirectory();
        var executable = files.FilePath("Companion.Desktop.exe");
        File.WriteAllBytes(executable, [0x4d, 0x5a]);
        const string previousCommand = @"""C:\Previous App\Companion.Desktop.exe""";
        using (var key = fixture.Open(writable: true)) key.SetValue("DesktopCompanion", previousCommand, RegistryValueKind.String);
        var readOnlyStore = StartupRegistration.CreateTestStore(fixture.KeyPath, writable: false);

        AssertWriteDenied(() => StartupRegistration.SetEnabled(true, executable, readOnlyStore));
        Assert.AreEqual(previousCommand, StartupRegistration.GetCommand(readOnlyStore));
        AssertWriteDenied(() => StartupRegistration.SetEnabled(false, executable, readOnlyStore));
        Assert.AreEqual(previousCommand, StartupRegistration.GetCommand(readOnlyStore));
    }

    [TestMethod]
    public void RegistryReadsDoNotExpandEnvironmentStringsOrAcceptDifferentValueKinds()
    {
        using var fixture = new RegistryFixture();
        var store = StartupRegistration.CreateTestStore(fixture.KeyPath);
        using (var key = fixture.Open(writable: true))
            key.SetValue("DesktopCompanion", @"""%TEMP%\Companion.Desktop.exe""", RegistryValueKind.ExpandString);
        Assert.AreEqual(@"""%TEMP%\Companion.Desktop.exe""", StartupRegistration.GetCommand(store));
        using (var key = fixture.Open(writable: true)) key.SetValue("DesktopCompanion", 1, RegistryValueKind.DWord);
        Assert.IsNull(StartupRegistration.GetCommand(store));
    }

    [TestMethod]
    public void MissingTestKeyReadsAsDisabledAndDisableDoesNotCreateIt()
    {
        using var fixture = new RegistryFixture(create: false);
        var store = StartupRegistration.CreateTestStore(fixture.KeyPath);
        Assert.IsNull(StartupRegistration.GetCommand(store));
        StartupRegistration.SetEnabled(false, @"C:\Apps\Companion.Desktop.exe", store);
        using var missing = Registry.CurrentUser.OpenSubKey(fixture.KeyPath, writable: false);
        Assert.IsNull(missing);
    }

    [TestMethod]
    [DataRow(@"Software\Microsoft\Windows\CurrentVersion\Run")]
    [DataRow(@"Software\DesktopCompanion.Tests")]
    [DataRow(@"Software\DesktopCompanion.Tests\not-a-guid")]
    [DataRow(@"Software\DesktopCompanion.Tests\00000000000000000000000000000000")]
    [DataRow(@"Software\DesktopCompanion.Tests\ 12345678901234567890123456789012 ")]
    [DataRow(@"Software\DesktopCompanion.Tests\00000000000000000000000000000000\Child")]
    [DataRow(@"Software\DesktopCompanion.Tests\..\Other")]
    [DataRow(@"HKEY_CURRENT_USER\Software\DesktopCompanion.Tests\00000000000000000000000000000000")]
    public void TestStoreCannotTargetProductionOrUnvalidatedKeyPaths(string path)
    {
        Assert.ThrowsExactly<ArgumentException>(() => StartupRegistration.CreateTestStore(path));
    }

    private static void AssertWriteDenied(Action action)
    {
        try
        {
            action();
            Assert.Fail("A read-only registry key must reject mutation.");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException) { }
    }

    private sealed class RegistryFixture : IDisposable
    {
        private const string Prefix = @"Software\DesktopCompanion.Tests\";
        private readonly string id = Guid.NewGuid().ToString("N");
        public string KeyPath { get; }

        public RegistryFixture(bool create = true)
        {
            KeyPath = Prefix + id;
            ValidatePath();
            if (create)
            {
                using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
                if (key is null) throw new IOException("The isolated registry fixture could not be created.");
            }
        }

        public RegistryKey Open(bool writable)
        {
            ValidatePath();
            return Registry.CurrentUser.OpenSubKey(KeyPath, writable)
                ?? throw new IOException("The isolated registry fixture is unavailable.");
        }

        private void ValidatePath()
        {
            if (!Guid.TryParseExact(id, "N", out _) || KeyPath != Prefix + id ||
                KeyPath[Prefix.Length..].Contains('\\'))
                throw new InvalidOperationException("Unsafe registry fixture path.");
        }

        public void Dispose()
        {
            ValidatePath();
            // No recursive delete: a fixture may remove only its exact GUID leaf, never the test parent.
            Registry.CurrentUser.DeleteSubKey(KeyPath, throwOnMissingSubKey: false);
        }
    }
}
