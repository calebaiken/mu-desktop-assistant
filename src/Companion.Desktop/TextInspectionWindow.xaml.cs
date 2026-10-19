using System.Windows;
using System.Windows.Threading;
using System.Diagnostics;
using Companion.Application.Tools;
using Microsoft.Win32;

namespace Companion.Desktop;

public partial class TextInspectionWindow : Window
{
    private readonly TrustedToolExecutor workflow;
    private readonly CancellationTokenSource lifetime = new();
    private readonly DispatcherTimer expiry = new() { Interval = TimeSpan.FromSeconds(1) };
    private ToolPreview? preview;
    private int generation;
    private long proposedAt;
    private bool closed, busy;
    internal Task ShutdownTask { get; private set; } = Task.CompletedTask;

    public TextInspectionWindow(TrustedToolExecutor workflow)
    {
        this.workflow = workflow;
        InitializeComponent();
        expiry.Tick += (_, _) =>
        {
            if (preview is null) return;
            if (Stopwatch.GetElapsedTime(proposedAt) >= TextInspection.ApprovalLifetime) ClearInspection("Expired. Select the file again to request a new read.");
        };
        Closed += (_, _) =>
        {
            closed = true;
            ClearInspection();
            lifetime.Cancel();
            workflow.SetEnabled(false);
            // The App owns the executor across view reopenings, including any
            // poisoned state awaiting late native cleanup.
            ShutdownTask = Task.CompletedTask;
        };
    }

    private void Enable_Changed(object sender, RoutedEventArgs e)
    {
        if (SelectFile is null) return;
        ClearInspection();
        workflow.SetEnabled(EnableInspection.IsChecked == true);
        SelectFile.IsEnabled = EnableInspection.IsChecked == true && !closed;
        InspectionStatus.Text = SelectFile.IsEnabled ? "Enabled for this window. Select a file to propose a read." : "Disabled. No file access is authorized.";
    }

    private async void Select_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Title = "Select one plain-text file", Filter = "Plain text (*.txt;*.md)|*.txt;*.md", Multiselect = false, CheckFileExists = true, DereferenceLinks = false };
        ClearInspection();
        if (picker.ShowDialog(this) == true) await PrepareSelectionAsync(picker.FileName);
    }

    // Also used by the native smoke with a public isolated fixture; the picker is
    // the sole normal UI source. No chat/model path can call this method.
    internal async Task PrepareSelectionAsync(string path)
    {
        ClearInspection();
        var current = generation;
        SetBusy(true);
        InspectionStatus.Text = "Checking selected file metadata. No text has been read.";
        try
        {
            var candidate = await workflow.PrepareAsync(TextInspection.Propose(path), lifetime.Token);
            if (closed || current != generation) { workflow.Revoke(); return; }
            preview = candidate;
            proposedAt = Stopwatch.GetTimestamp();
            TargetPath.Text = candidate.Resource.CanonicalPath;
            PreviewDetails.Text = $"Read this exact {candidate.Resource.Bytes:N0}-byte file once. No writes. Approval expires at {candidate.ExpiresAt.ToLocalTime():T}.";
            InspectionStatus.Text = "Proposed — review the exact target, then approve once or cancel.";
            expiry.Start();
        }
        catch (ToolException error) { if (!closed && current == generation) InspectionStatus.Text = FailureMessage(error.Failure); }
        catch (OperationCanceledException) { if (!closed && current == generation) InspectionStatus.Text = "Cancelled. No result retained."; }
        finally { if (!closed && current == generation) SetBusy(false); }
    }

    private async void Approve_Click(object sender, RoutedEventArgs e) => await ApproveAndInspectAsync();

    internal async Task ApproveAndInspectAsync()
    {
        if (preview is not { } selected || busy || closed) return;
        var current = generation;
        expiry.Stop();
        SetBusy(true);
        try
        {
            var grant = await workflow.ApproveAsync(selected, lifetime.Token);
            if (closed || current != generation) return;
            InspectionStatus.Text = "Authorized once — inspecting the selected file…";
            var result = await workflow.ExecuteAsync(selected, grant, lifetime.Token);
            if (closed || current != generation) return;
            FileText.Text = result.Status == ToolStatus.Completed ? result.Text : "";
            InspectionStatus.Text = result.Status == ToolStatus.Completed ? "Completed — authorization consumed. Select again for another read." : FailureMessage(result.Failure ?? ToolFailure.Internal);
        }
        catch (ToolException error) { if (!closed && current == generation) InspectionStatus.Text = FailureMessage(error.Failure); }
        catch (OperationCanceledException) { if (!closed && current == generation) InspectionStatus.Text = "Cancelled. No result retained."; }
        finally
        {
            if (!closed && current == generation) { preview = null; workflow.Revoke(); SetBusy(false); }
        }
    }

    internal void ClearInspection(string status = "Cancelled / cleared. Select a file for a fresh proposal.")
    {
        generation++;
        expiry.Stop();
        workflow.Revoke();
        preview = null;
        busy = false;
        TargetPath.Clear();
        PreviewDetails.Text = "";
        FileText.Clear();
        InspectionStatus.Text = status;
        ApproveOnce.IsEnabled = false;
        SelectFile.IsEnabled = !closed && EnableInspection.IsChecked == true;
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => ClearInspection();
    private void SetBusy(bool value)
    {
        busy = value;
        SelectFile.IsEnabled = !value && !closed && EnableInspection.IsChecked == true;
        ApproveOnce.IsEnabled = !value && !closed && preview is not null;
    }

    private static string FailureMessage(ToolFailure failure) => failure switch
    {
        ToolFailure.Denied => "Denied. Enable inspection and select a file for fresh approval.",
        ToolFailure.Expired or ToolFailure.Stale => "Expired or changed file. Select it again and review a new proposal.",
        ToolFailure.TooLarge => "Refused: the file exceeds 64 KiB or 32,768 decoded characters.",
        ToolFailure.InvalidText => "Refused: unsupported encoding, binary data, or unsafe text controls.",
        ToolFailure.InvalidRequest => "Refused: choose a regular .txt or .md file on a fixed local drive, without links or alternate streams.",
        ToolFailure.Cancelled => "Cancelled. No result retained.",
        ToolFailure.Timeout => "Timed out. Select the file again to retry with fresh approval.",
        ToolFailure.Busy => "Another read is finishing. Try selecting the file again shortly.",
        ToolFailure.AuditUnavailable => "Activity recording is unavailable. No result retained; check local profile storage.",
        _ => "Inspection unavailable. Check file access and close any writer, then select again."
    };
}
