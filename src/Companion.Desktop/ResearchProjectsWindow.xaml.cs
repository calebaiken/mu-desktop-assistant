using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using Companion.Application.MachineLearning;

namespace Companion.Desktop;

public partial class ResearchProjectsWindow : Window
{
    private readonly IResearchProjectStore store;
    private readonly CancellationTokenSource lifetime = new();
    private bool closed, busy, catalogLoaded;
    private int namedProjects;
    private Task? initialization;
    internal Guid? SelectedProjectId { get; private set; }
    private sealed record ProjectRow(ResearchProject Project)
    {
        public string Identity => Project.IsLegacy ? "Original Lab location · legacy" : Project.Id.ToString("N");
        public string AccessibleLabel => $"{Project.Name}. Identity: {Identity}. Storage: {Project.Directory}";
    }

    public ResearchProjectsWindow(IResearchProjectStore store)
    {
        this.store = store;
        InitializeComponent();
        ApplyPalette();
        SystemParameters.StaticPropertyChanged += SystemPreferenceChanged;
        Loaded += async (_, _) => await InitializeAsync();
        Closed += (_, _) =>
        {
            closed = true;
            SystemParameters.StaticPropertyChanged -= SystemPreferenceChanged;
            lifetime.Cancel();
            lifetime.Dispose();
            if (DialogResult != true) SelectedProjectId = null;
            ProjectsList.ItemsSource = null;
            ProjectTarget.Clear();
            ProjectName.Clear();
        };
    }

    internal Task InitializeAsync() => initialization ??= RefreshAsync();

    private async Task RefreshAsync(Guid? selected = null)
    {
        if (closed) return;
        ClearSelection();
        catalogLoaded = false;
        ProjectsList.ItemsSource = null;
        ProjectCountLabel.Text = "Loading catalog";
        EmptyProjectsPanel.Visibility = Visibility.Collapsed;
        SetBusy(true, "Loading projects…");
        try
        {
            var projects = await store.ListAsync(lifetime.Token);
            if (closed) return;
            var rows = projects.Select(project => new ProjectRow(project)).ToArray();
            namedProjects = projects.Count(project => !project.IsLegacy);
            catalogLoaded = true;
            ProjectsList.ItemsSource = rows;
            ProjectsList.SelectedItem = rows.SingleOrDefault(row => row.Project.Id == selected);
            ProjectCountLabel.Text = $"{rows.Length} local {(rows.Length == 1 ? "project" : "projects")}";
            EmptyProjectsPanel.Visibility = rows.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            ProjectsList.Visibility = rows.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            SetStatus(selected.HasValue ? "Project created. Review its destination, then open it." :
                rows.Length == 0 ? "No projects yet. Create one to begin." : "Select a project and review its storage location.");
        }
        catch (OperationCanceledException) { if (!closed) SetStatus("Catalog loading cancelled. Refresh to try again."); }
        catch (Exception error) when (IsRecoverable(error))
        {
            if (!closed)
            {
                ProjectCountLabel.Text = "Catalog unavailable";
                SetStatus(error is MlException ? error.Message : "The project catalog could not be read. Preserve its files, check access, then refresh.", true);
            }
        }
        finally { if (!closed) SetBusy(false); }
    }

    private async void Retry_Click(object sender, RoutedEventArgs e)
    {
        if (!closed && !busy) await RefreshAsync();
    }

    private async void Create_Click(object sender, RoutedEventArgs e) => await CreateProjectAsync();
    internal async Task CreateProjectAsync()
    {
        if (closed || busy || !catalogLoaded) return;
        try { ResearchProjects.ValidateName(ProjectName.Text); }
        catch (ArgumentException error) { SetStatus(error.Message, true); return; }
        SetBusy(true, "Creating project metadata…");
        try
        {
            var project = await store.CreateAsync(ProjectName.Text, lifetime.Token);
            if (closed) return;
            ProjectName.Clear();
            await RefreshAsync(project.Id);
            if (!closed && !catalogLoaded)
                SetStatus("Project metadata was created, but the catalog could not be reloaded. Refresh before continuing; do not create a duplicate.", true);
        }
        catch (OperationCanceledException) { if (!closed) SetStatus("Creation cancelled. Refresh to check the catalog before trying again."); }
        catch (Exception error) when (IsRecoverable(error))
        { if (!closed) SetStatus(error is MlException or ArgumentException ? error.Message : "Project creation failed. Check local storage access and refresh before trying again.", true); }
        finally { if (!closed) SetBusy(false); }
    }

    private void Project_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (closed || SelectionHeading is null) return; // Selection can change during construction/teardown.
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        SelectedProjectId = null;
        if (ProjectsList.SelectedItem is ProjectRow row)
        {
            SelectionHeading.Text = row.Project.Name;
            SelectionHint.Text = row.Project.IsLegacy ? "Existing artifacts stay in their original location." : "Review the exact local destination before opening.";
            SelectionHint.Visibility = row.Project.IsLegacy ? Visibility.Visible : Visibility.Collapsed;
            ProjectTarget.Text = $"Identity: {row.Identity}\nStorage: {row.Project.Directory}";
            ProjectTarget.Visibility = Visibility.Visible;
            if (!busy) SetStatus("Review the selected destination, then open it.");
        }
        else
        {
            SelectionHeading.Text = "No project selected";
            SelectionHint.Text = "Select a project to review its identity and exact storage location.";
            SelectionHint.Visibility = Visibility.Visible;
            ProjectTarget.Clear();
            ProjectTarget.Visibility = Visibility.Collapsed;
        }
        OpenProject.IsEnabled = !closed && !busy && catalogLoaded && ProjectsList.SelectedItem is ProjectRow;
    }

    private async void Open_Click(object sender, RoutedEventArgs e) => await OpenReviewedProjectAsync();

    private async Task OpenReviewedProjectAsync()
    {
        if (closed || busy || !catalogLoaded || ProjectsList.SelectedItem is not ProjectRow row) return;
        SetBusy(true, "Checking selected destination…");
        try
        {
            var current = await store.OpenAsync(row.Project.Id, lifetime.Token);
            if (closed) return;
            // Identity, metadata and the reviewed UI snapshot must all still match.
            if (current != row.Project || !ReferenceEquals(ProjectsList.SelectedItem, row))
            {
                ClearSelection();
                catalogLoaded = false;
                SetStatus("This destination changed. Refresh and select the project again.", true);
                return;
            }
            SelectedProjectId = current.Id;
            DialogResult = true;
        }
        catch (OperationCanceledException) { if (!closed) SetStatus("Opening cancelled. Review the selection before trying again."); }
        catch (Exception error) when (IsRecoverable(error))
        {
            if (!closed)
            {
                ClearSelection();
                catalogLoaded = false;
                SetStatus(error is MlException ? error.Message : "The selected destination is unavailable. Refresh and select it again.", true);
            }
        }
        finally { if (!closed) SetBusy(false); }
    }

    // Existing native integration seam; the user-visible action additionally revalidates through OpenAsync.
    internal bool SelectProject()
    {
        if (closed || busy || !catalogLoaded || ProjectsList.SelectedItem is not ProjectRow row) return false;
        SelectedProjectId = row.Project.Id;
        return true;
    }

    private void ClearSelection()
    {
        SelectedProjectId = null;
        ProjectsList.SelectedItem = null;
        UpdateSelection();
    }

    private void SetBusy(bool value, string message = "")
    {
        busy = value;
        ProjectsList.IsEnabled = !value && catalogLoaded;
        ProjectName.IsEnabled = !value && catalogLoaded;
        RetryProjectsButton.IsEnabled = !value;
        BusyProjectsLabel.Text = message;
        BusyProjectsLabel.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        OpenProject.IsEnabled = !value && catalogLoaded && ProjectsList.SelectedItem is ProjectRow;
        UpdateNameState();
    }

    private void Name_Changed(object sender, TextChangedEventArgs e) { if (NameValidation is not null) UpdateNameState(); }
    private void UpdateNameState()
    {
        var valid = false;
        var message = "1–80 characters. Creates local metadata only.";
        if (catalogLoaded && namedProjects >= ResearchProjects.MaximumProjects) message = "The 32 named-project limit has been reached. Existing projects can still be opened.";
        else if (!string.IsNullOrEmpty(ProjectName.Text))
        {
            try { ResearchProjects.ValidateName(ProjectName.Text); valid = true; }
            catch (ArgumentException error) { message = error.Message; }
        }
        NameValidation.Text = message;
        CreateProject.IsEnabled = !closed && !busy && catalogLoaded && namedProjects < ResearchProjects.MaximumProjects && valid;
    }

    private async void Name_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        if (CreateProject.IsEnabled) await CreateProjectAsync();
    }

    private async void Projects_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        await OpenReviewedProjectAsync();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        SelectedProjectId = null;
        Close();
    }

    private void SetStatus(string text, bool error = false)
    {
        ProjectStatus.Text = text;
        ProjectStatus.SetResourceReference(ForegroundProperty, error ? "Mu.Error" : "Mu.Text");
        UIElementAutomationPeer.FromElement(ProjectStatus)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private static bool IsRecoverable(Exception error) => error is MlException or ArgumentException or
        System.IO.IOException or UnauthorizedAccessException or InvalidOperationException;

    private void Theme_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (IsInitialized && !closed) ApplyPalette();
    }

    private void ApplyPalette()
    {
        var name = SystemParameters.HighContrast ? "HighContrast" :
            ThemeModeSelector.SelectedIndex == 1 ? "Archive" : "Instrument";
        Resources.MergedDictionaries[0] = new ResourceDictionary
        { Source = new Uri($"/Companion.Desktop;component/Resources/Mu.{name}.xaml", UriKind.Relative) };
    }

    private void SystemPreferenceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.HighContrast))
            _ = Dispatcher.InvokeAsync(() => { if (!closed) ApplyPalette(); });
    }

    private void Layout_Changed(object sender, SizeChangedEventArgs e)
    {
        if (SelectionPanel is null) return;
        var wide = ActualWidth >= 800;
        ProjectLayout.ColumnDefinitions[1].Width = wide ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Grid.SetColumn(SelectionPanel, wide ? 1 : 0);
        Grid.SetRow(SelectionPanel, wide ? 0 : 1);
        SelectionPanel.Margin = wide ? new Thickness(20, 0, 0, 0) : new Thickness(0, 16, 0, 0);
    }
}
