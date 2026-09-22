using Companion.Infrastructure.Desktop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

[TestClass]
public sealed class StartupRegistrationTests
{
    [TestMethod]
    public void ExecutableWithSpacesIsFullyQuotedWithoutArguments()
    {
        const string executable = @"C:\Program Files\Desktop Companion\Companion.Desktop.exe";
        Assert.AreEqual('"' + executable + '"', StartupRegistration.BuildCommand(executable));
    }

    [TestMethod]
    public void DotSegmentsNormalizeBeforeCommandComparison()
    {
        Assert.AreEqual(@"""C:\Apps\Companion.Desktop.exe""",
            StartupRegistration.BuildCommand(@"C:\Apps\Old\..\Companion.Desktop.exe"));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("Companion.Desktop.exe")]
    [DataRow(@"C:Companion.Desktop.exe")]
    [DataRow(@"\Companion.Desktop.exe")]
    [DataRow(@"C:\Apps\Companion.Desktop.cmd")]
    [DataRow(@"C:\Apps\dotnet.exe")]
    [DataRow(@"C:\Apps\DOTNET.EXE")]
    [DataRow(@"C:\Apps\Companion.Desktop.exe --hidden")]
    [DataRow("C:\\Apps\\Companion.Desktop.exe\" --something \".exe")]
    [DataRow("C:\\Apps\\Companion\nDesktop.exe")]
    [DataRow("C:\\Apps\\Companion\0Desktop.exe")]
    [DataRow(@"C:\Apps\*.exe")]
    [DataRow(@"C:\Apps\bad|Companion.exe")]
    public void InvalidOrArgumentBearingPathsAreRejectedBeforeRegistryAccess(string executable)
    {
        Assert.ThrowsExactly<ArgumentException>(() => StartupRegistration.BuildCommand(executable));
        Assert.ThrowsExactly<ArgumentException>(() => StartupRegistration.SetEnabled(false, executable));
    }

    [TestMethod]
    public void MissingOrDifferentRegistrationIsDisabled()
    {
        const string executable = @"C:\Apps\Companion.Desktop.exe";
        Assert.IsFalse(StartupRegistration.MatchesCommand(null, executable));
        Assert.IsFalse(StartupRegistration.MatchesCommand("", executable));
        Assert.IsFalse(StartupRegistration.MatchesCommand(executable, executable));
        Assert.IsFalse(StartupRegistration.MatchesCommand('"' + executable + "\" --hidden", executable));
        Assert.IsFalse(StartupRegistration.MatchesCommand(@"""C:\Other\Companion.Desktop.exe""", executable));
        Assert.IsTrue(StartupRegistration.MatchesCommand(@"""c:\apps\COMPANION.DESKTOP.EXE""", executable));
    }

    [TestMethod]
    public void MissingExecutableCannotEnableStartupAndDoesNotChangeExistingRegistration()
    {
        using var directory = new TestDirectory();
        var before = StartupRegistration.GetCommand();
        Assert.ThrowsExactly<FileNotFoundException>(() =>
            StartupRegistration.SetEnabled(true, directory.FilePath("Companion.Desktop.exe")));
        Assert.AreEqual(before, StartupRegistration.GetCommand());
    }
}
