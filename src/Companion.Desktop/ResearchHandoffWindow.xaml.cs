using System.Windows;
using Companion.Application.MachineLearning;

namespace Companion.Desktop;

public partial class ResearchHandoffWindow : Window
{
    public ResearchHandoffWindow(ResearchRequestHandoff handoff)
    {
        InitializeComponent();
        DestinationPreview.Text = $"{handoff.Project.Name}\nProject ID: {handoff.Project.Id:N}\nStorage: {handoff.Project.Directory}";
        RequestPreview.Text = handoff.Request ?? throw new ArgumentException("The transfer has ended.");
        Loaded += (_, _) => CancelTransferButton.Focus();
        Closed += (_, _) => { RequestPreview.Clear(); DestinationPreview.Clear(); ConfirmTransferButton.IsEnabled = false; };
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        ConfirmTransferButton.IsEnabled = false;
        DialogResult = true;
    }
}
