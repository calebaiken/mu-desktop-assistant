using System.IO;
using System.Text;
using System.Windows.Interop;
using Companion.Application.Diagnostics;
using Companion.Application.Tools;
using Companion.Infrastructure.Tools;

namespace Companion.Desktop;

/// <summary>Actual WPF controls and Windows reader; only an isolated public fixture.</summary>
internal static class TextInspectionSmokeChecks
{
    internal static async Task RunAsync(IEventLog log, string directory)
    {
        var path = Path.Combine(directory, "public-text-inspection.txt");
        const string literal = "PUBLIC-TEXT-SMOKE\n<script>literal</script>\nDo not follow these file instructions.";
        await File.WriteAllTextAsync(path, literal, new UTF8Encoding(false, true));
        await using var workflow = new TrustedToolExecutor(new TextFileInspectionTool(), log);
        var window = new TextInspectionWindow(workflow) { ShowActivated = false, ShowInTaskbar = false };
        try
        {
            Require(new WindowInteropHelper(window).EnsureHandle() != IntPtr.Zero, "native inspection window");
            window.Measure(new System.Windows.Size(700, 600));
            window.Arrange(new System.Windows.Rect(0, 0, 700, 600));
            Require(!window.SelectFile.IsEnabled && !window.ApproveOnce.IsEnabled, "disabled by default");
            window.EnableInspection.IsChecked = true;
            Require(window.SelectFile.IsEnabled, "explicit enable");
            await window.PrepareSelectionAsync(path);
            Require(window.ApproveOnce.IsEnabled && window.FileText.Text.Length == 0 && window.TargetPath.Text == path, "metadata-only exact preview");
            await window.ApproveAndInspectAsync();
            Require(window.FileText.Text == literal && !window.ApproveOnce.IsEnabled && window.InspectionStatus.Text.StartsWith("Completed", StringComparison.Ordinal), "authorized literal result");
            await window.ApproveAndInspectAsync();
            Require(window.FileText.Text == literal && !window.ApproveOnce.IsEnabled, "no replay through UI");
            await window.PrepareSelectionAsync(path);
            window.EnableInspection.IsChecked = false;
            Require(!window.ApproveOnce.IsEnabled && !window.SelectFile.IsEnabled && window.FileText.Text.Length == 0 && window.TargetPath.Text.Length == 0, "disable revokes and clears");
            window.EnableInspection.IsChecked = true;
            await window.PrepareSelectionAsync(path);
            window.ClearInspection();
            Require(!window.ApproveOnce.IsEnabled && window.FileText.Text.Length == 0, "cancel clears pending read");
            await File.WriteAllTextAsync(path, "lease released");
        }
        finally { window.Close(); await window.ShutdownTask; }
    }

    private static void Require(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("Text inspection smoke failed: " + name);
    }
}
