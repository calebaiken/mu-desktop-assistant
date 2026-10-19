using Companion.Application.Tools;
using Companion.Infrastructure.Tools;

namespace Companion.Desktop;

public partial class App
{
    private TextInspectionWindow? textInspectionWindow;
    private TrustedToolExecutor? textInspectionWorkflow;

    private void OpenTextInspection()
    {
        if (stopping || companion is null || runtime is null || options is null || options.IsTestSession) return;
        if (textInspectionWindow is null)
        {
            textInspectionWorkflow ??= new(new TextFileInspectionTool(), runtime.Log);
            textInspectionWindow = new(textInspectionWorkflow) { Owner = companion };
            textInspectionWindow.Closed += (_, _) => textInspectionWindow = null;
        }
        textInspectionWindow.Show();
        textInspectionWindow.WindowState = System.Windows.WindowState.Normal;
        textInspectionWindow.Activate();
    }
}
