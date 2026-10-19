using System.Windows;
using System.Windows.Controls;
using Companion.Application.MachineLearning;

namespace Companion.Desktop;

public partial class ResearchProjectsWindow : Window
{
    private readonly IResearchProjectStore store;
    private readonly CancellationTokenSource lifetime = new();
    private bool closed, busy, initialized;
    internal Guid? SelectedProjectId { get; private set; }
    private sealed record ProjectRow(ResearchProject Project)
    {
        public string Label => Project.IsLegacy ? Project.Name + " (original location)" : $"{Project.Name} · {Project.Id:N}";
    }

    public ResearchProjectsWindow(IResearchProjectStore store)
    {
        this.store = store;
        InitializeComponent();
        Loaded += async (_, _) => await InitializeAsync();
        Closed += (_, _) => { closed = true; lifetime.Cancel(); ProjectsList.ItemsSource = null; ProjectTarget.Clear(); ProjectName.Clear(); };
    }

    internal async Task InitializeAsync()
    {
        if (initialized) return;
        initialized = true;
        await RefreshAsync();
    }

    private async Task RefreshAsync(Guid? selected = null)
    {
        SetBusy(true);
        try
        {
            var projects = await store.ListAsync(lifetime.Token);
            if (closed) return;
            var rows = projects.Select(project => new ProjectRow(project)).ToArray();
            ProjectsList.ItemsSource = rows;
            ProjectsList.SelectedItem = rows.SingleOrDefault(row => row.Project.Id == selected);
            ProjectStatus.Text = rows.Length == 0 ? "Create a project to begin. No dataset has been opened." : "Select a project and review its storage location.";
        }
        catch (OperationCanceledException) { }
        catch (MlException error) { if (!closed) ProjectStatus.Text = error.Message; }
        finally { if (!closed) SetBusy(false); }
    }

    private async void Create_Click(object sender, RoutedEventArgs e) => await CreateProjectAsync();
    internal async Task CreateProjectAsync()
    {
        if (closed || busy) return;
        SetBusy(true);
        try
        {
            var project = await store.CreateAsync(ProjectName.Text, lifetime.Token);
            if (closed) return;
            ProjectName.Clear();
            await RefreshAsync(project.Id);
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is MlException or ArgumentException)
        { if (!closed) ProjectStatus.Text = error.Message; }
        finally { if (!closed) SetBusy(false); }
    }

    private void Project_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ProjectsList.SelectedItem is ProjectRow row)
            ProjectTarget.Text = $"{row.Project.Name}\nIdentity: {(row.Project.IsLegacy ? "legacy default-project" : row.Project.Id.ToString("N"))}\nStorage: {row.Project.Directory}";
        else ProjectTarget.Clear();
        OpenProject.IsEnabled = !busy && ProjectsList.SelectedItem is ProjectRow;
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (SelectProject()) DialogResult = true;
    }

    internal bool SelectProject()
    {
        if (closed || busy || ProjectsList.SelectedItem is not ProjectRow row) return false;
        SelectedProjectId = row.Project.Id;
        return true;
    }

    private void SetBusy(bool value)
    {
        busy = value;
        ProjectsList.IsEnabled = ProjectName.IsEnabled = CreateProject.IsEnabled = !value;
        OpenProject.IsEnabled = !value && ProjectsList.SelectedItem is ProjectRow;
    }
}
