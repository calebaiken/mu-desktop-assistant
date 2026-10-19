using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Companion.Application.Conversation;
using Companion.Application.MachineLearning;
using Microsoft.Win32;

namespace Companion.Desktop;

/// <summary>A native view of the App-owned Lab. Closing this view never disposes its service.</summary>
public partial class MlLabWindow : Window
{
    private readonly IMlLabService service;
    private readonly Func<CancellationToken, Task<IChatModel>>? modelFactory;
    private readonly ILayaDecisionService? laya;
    private readonly ObservableCollection<FeatureChoice> features = [];
    private readonly ObservableCollection<JobRow> jobs = [];
    private readonly List<JsonElement> experiments = [];
    private readonly List<JsonElement> models = [];
    private readonly List<AlgorithmChoice> algorithms = [];
    private MlDatasetSelection? inspectedSelection;
    private JsonElement? selectedExperiment;
    private JsonElement? selectedModel;
    private MlAction? proposedAction;
    private MlLimits? proposedLimits;
    private CancellationTokenSource? planning;
    private bool closed;
    private bool busy;
    private Task? initialization;

    public MlLabWindow(IMlLabService service, Func<CancellationToken, Task<IChatModel>>? modelFactory = null,
        ILayaDecisionService? laya = null, ResearchProject? project = null)
    {
        ArgumentNullException.ThrowIfNull(service);
        this.service = service;
        this.modelFactory = modelFactory;
        this.laya = laya;
        InitializeComponent();
        ProjectLabel.Text = project is null ? $"Project: {service.ProjectDirectory}" : $"Project: {project.Name} · {(project.IsLegacy ? "legacy" : project.Id.ToString("N"))}\n{service.ProjectDirectory}";
        ChangeProjectButton.Visibility = project is null ? Visibility.Collapsed : Visibility.Visible;
        FeaturesGrid.ItemsSource = features;
        JobsGrid.ItemsSource = jobs;
        TaskSelector.ItemsSource = new[] { "regression", "classification", "clustering", "reduction", "anomaly" };
        TaskSelector.SelectedIndex = 0;
        ScalingSelector.ItemsSource = new[] { "standard", "robust", "minmax", "none" };
        ScalingSelector.SelectedIndex = 0;
        SplitSelector.ItemsSource = new[] { "random", "stratified", "group", "time" };
        SplitSelector.SelectedIndex = 0;
        AssistantMode.ItemsSource = new[] { "Learn", "Research", "Auto" };
        AssistantMode.SelectedIndex = 0;
        ProposeButton.IsEnabled = modelFactory is not null;
        AnalyzeTaskButton.IsEnabled = laya is not null;
        if (laya is null) LayaAdviceText.Text = "Optional local LAYA task advice is unavailable in this session.";
        if (modelFactory is null) ProposalText.Text = "Local conversational planning is unavailable. The dataset and experiment controls remain available.";
        service.JobChanged += OnJobChanged;
        foreach (var job in service.Jobs) UpdateJob(job);
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    internal bool IsBusy => busy;
    public event EventHandler? ChangeProjectRequested;
    private void ChangeProject_Click(object sender, RoutedEventArgs e) => ChangeProjectRequested?.Invoke(this, EventArgs.Empty);
    internal string CurrentStatus => StatusLabel.Text;
    internal IReadOnlyList<string> SelectedFeatures => features.Where(feature => feature.Use).Select(feature => feature.Name).ToArray();

    private async void OnLoaded(object sender, RoutedEventArgs e)
        => await InitializeAsync();

    internal Task InitializeAsync() => initialization ??= InitializeCoreAsync();

    private async Task InitializeCoreAsync()
    {
        await ExecuteActionAsync(new MlCapabilities());
        if (!closed) await ExecuteActionAsync(new MlList());
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        closed = true;
        planning?.Cancel();
        service.JobChanged -= OnJobChanged;
        PlotImage.Source = null;
    }

    public void BeginShutdown() => planning?.Cancel();

    private void OnJobChanged(MlJobUpdate update)
    {
        if (closed || Dispatcher.HasShutdownStarted) return;
        _ = Dispatcher.BeginInvoke(new Action(() => { if (!closed) UpdateJob(update); }));
    }

    private void UpdateJob(MlJobUpdate update)
    {
        var index = jobs.ToList().FindIndex(job => job.Id == update.Id);
        var row = new JobRow(update.Id, update.Operation, update.State.ToString(),
            update.Elapsed.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture),
            $"{update.WorkerMemoryBytes / (1024d * 1024):F1} MiB",
            update.CpuPercent is { } cpu ? $"{cpu:F1}%" : "—", update.State);
        if (index >= 0) jobs[index] = row;
        else jobs.Insert(0, row);
        // A view is bounded even when the App-owned service has a longer session history.
        while (jobs.Count > 100) jobs.RemoveAt(jobs.Count - 1);
        CancelJobButton.IsEnabled = jobs.Any(job => IsJobActive(job.JobState));
    }

    private static bool IsJobActive(MlJobState state) => state is MlJobState.Queued or MlJobState.Preparing or MlJobState.Running;

    private async Task HandleAsync(Func<MlAction> createAction)
    {
        try { await ExecuteActionAsync(createAction()); }
        catch (Exception exception) when (exception is ArgumentException or JsonException or FormatException or OverflowException)
        { if (!closed) StatusLabel.Text = exception.Message; }
    }

    internal async Task<MlResult?> ExecuteActionAsync(MlAction action, MlLimits? explicitLimits = null)
    {
        if (closed || busy) return null;
        MlLimits limits;
        try
        {
            limits = explicitLimits ?? ReadLimits() with { MaxRows = ActionRows(action) ?? ReadInt(RowLimit.Text, 10, 100000, "Maximum rows") };
            limits.Validate();
        }
        catch (ArgumentException exception) { StatusLabel.Text = exception.Message; return null; }
        SetBusy(true);
        StatusLabel.Text = $"{action.Operation}: waiting for the local worker…";
        try
        {
            // This task belongs to the App-owned service. Closing the view does not cancel it.
            var result = await service.ExecuteAsync(action, limits);
            if (closed) return result;
            ApplyResult(result);
            if (action is MlInspect inspect)
            {
                DatasetPath.Text = inspect.Dataset.Path;
                DatasetKey.Text = inspect.Dataset.Key ?? "";
                StartRow.Text = inspect.Dataset.Start.ToString(CultureInfo.InvariantCulture);
                RowLimit.Text = inspect.Dataset.RowLimit.ToString(CultureInfo.InvariantCulture);
                inspectedSelection = inspect.Dataset;
            }
            StatusLabel.Text = $"{action.Operation} completed locally.";
            if (action is MlTrain)
            {
                // Refresh saved records without losing the completed result selection.
                try
                {
                    var saved = await service.ExecuteAsync(new MlList(), limits);
                    if (!closed) ApplyResult(saved);
                }
                catch (Exception exception) when (exception is MlException or OperationCanceledException or IOException or InvalidOperationException or ArgumentException or JsonException)
                { if (!closed) StatusLabel.Text = "Training completed and its results are shown. Refresh saved work to retry loading the registry."; }
            }
            return result;
        }
        catch (OperationCanceledException) { if (!closed) StatusLabel.Text = "The local job was cancelled. No completed result is being reported."; }
        catch (MlException exception) { if (!closed) StatusLabel.Text = exception.Message; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or JsonException)
        { if (!closed) StatusLabel.Text = "The Lab could not complete this request. Check the local runtime, dataset and selected configuration."; }
        finally { if (!closed) SetBusy(false); }
        return null;
    }

    private void SetBusy(bool value)
    {
        busy = value;
        BrowseButton.IsEnabled = InspectButton.IsEnabled = TrainButton.IsEnabled = ExportButton.IsEnabled = PredictButton.IsEnabled = !value;
        RunProposalButton.IsEnabled = !value && proposedAction is not null && planning is null;
    }

    private MlLimits ReadLimits()
    {
        var limits = new MlLimits(ReadInt(RowLimit.Text, 10, 100000, "Maximum rows"),
            ReadInt(MemoryLimit.Text, 256, 4096, "Worker memory"), ReadInt(ThreadLimit.Text, 1, 8, "CPU threads"));
        limits.Validate();
        return limits;
    }

    private static int? ActionRows(MlAction action) => action switch
    {
        MlInspect inspect => inspect.Dataset.RowLimit,
        MlBrowse browse => browse.Dataset.RowLimit,
        MlTrain train => train.Dataset.RowLimit,
        MlPredict predict => predict.Dataset.RowLimit,
        _ => null
    };

    private MlDatasetSelection ReadDataset(bool prediction = false)
    {
        var path = (prediction ? PredictionPath.Text : DatasetPath.Text).Trim();
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal))
            throw new ArgumentException("Choose an absolute local dataset file path.");
        var key = (prediction ? PredictionKey.Text : DatasetKey.Text).Trim();
        return new MlDatasetSelection(path, key.Length == 0 ? null : key,
            ReadInt(prediction ? PredictionStart.Text : StartRow.Text, 0, 999999999, "Starting row"),
            ReadInt(prediction ? PredictionRows.Text : RowLimit.Text, 10, 100000, "Maximum rows"));
    }

    private static int ReadInt(string text, int low, int high, string label)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < low || value > high)
            throw new ArgumentException($"{label} must be an integer from {low:N0} to {high:N0}.");
        return value;
    }

    private static string Choice(ComboBox combo, string label) => combo.SelectedItem?.ToString()
        ?? throw new ArgumentException($"Choose {label}.");

    private MlTrain ReadTraining()
    {
        FeaturesGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        FeaturesGrid.CommitEdit(DataGridEditingUnit.Row, true);
        var dataset = ReadDataset();
        if (inspectedSelection != dataset) throw new ArgumentException("Inspect the current dataset selection before training. The file, key, starting row or row limit has changed.");
        var task = Choice(TaskSelector, "an experiment task");
        var supervised = task is "regression" or "classification";
        var target = supervised ? TargetSelector.SelectedItem as string : null;
        if (supervised && string.IsNullOrEmpty(target)) throw new ArgumentException("Choose the target column for this supervised experiment.");
        var selected = features.Where(feature => feature.Use && feature.Name != target).Select(feature => feature.Name).ToArray();
        if (selected.Length == 0) throw new ArgumentException("Select at least one input feature.");
        if (!double.TryParse(TestFraction.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var fraction) || !double.IsFinite(fraction) || fraction is < 0.1 or > 0.5)
            throw new ArgumentException("Test fraction must be a decimal between 0.1 and 0.5, such as 0.2.");
        var folds = ReadInt(FoldsInput.Text, 0, 10, "CV folds");
        if (folds == 1 || (!supervised && folds != 0)) throw new ArgumentException("Cross-validation uses 2–10 folds for supervised tasks; use 0 to disable it.");
        var split = Choice(SplitSelector, "a split");
        var group = split is "group" or "time" ? GroupSelector.SelectedItem as string : null;
        if (supervised && (split is "group" or "time") && string.IsNullOrEmpty(group)) throw new ArgumentException("Choose the grouping or time-order column for this split.");
        if (AlgorithmSelector.SelectedItem is not AlgorithmChoice algorithm) throw new ArgumentException("Wait for local capabilities, then choose an algorithm.");
        using var document = JsonDocument.Parse(Hyperparameters.Text, new JsonDocumentOptions { MaxDepth = 8 });
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new ArgumentException("Hyperparameters must be a JSON object.");
        var parameters = document.RootElement.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);
        var name = ExperimentName.Text.Trim();
        if (name.Length == 0) throw new ArgumentException("Enter an experiment name.");
        return new MlTrain(dataset, task, algorithm.Id, selected, target, Choice(ScalingSelector, "scaling"), fraction,
            ReadInt(SeedInput.Text, 0, int.MaxValue, "Seed"), split, group, folds, name, parameters);
    }

    private void ChooseDataset_Click(object sender, RoutedEventArgs e) => ChooseFile(DatasetPath);
    private void ChoosePrediction_Click(object sender, RoutedEventArgs e) => ChooseFile(PredictionPath);
    private void ChooseFile(TextBox destination)
    {
        var dialog = new OpenFileDialog { Title = "Choose a local dataset", CheckFileExists = true, Multiselect = false,
            Filter = "Supported datasets|*.csv;*.tsv;*.json;*.jsonl;*.parquet;*.npy;*.npz;*.h5;*.hdf5|All files|*.*" };
        if (dialog.ShowDialog(this) == true) destination.Text = dialog.FileName;
    }

    private async void Browse_Click(object sender, RoutedEventArgs e) => await HandleAsync(() => new MlBrowse(ReadDataset()));
    private async void Inspect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var selection = ReadDataset();
            var result = await ExecuteActionAsync(new MlInspect(selection));
            if (result is not null && !closed) inspectedSelection = selection;
        }
        catch (ArgumentException exception) { StatusLabel.Text = exception.Message; }
    }
    private async void Train_Click(object sender, RoutedEventArgs e) => await HandleAsync(ReadTraining);
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await ExecuteActionAsync(new MlList());
    private void GoConfigure_Click(object sender, RoutedEventArgs e) => LabTabs.SelectedItem = ConfigureTab;

    private void CancelJob_Click(object sender, RoutedEventArgs e)
    {
        var job = JobsGrid.SelectedItem as JobRow;
        if (job is null || !IsJobActive(job.JobState)) job = jobs.FirstOrDefault(candidate => IsJobActive(candidate.JobState));
        if (job is null) return;
        service.Cancel(job.Id);
        StatusLabel.Text = "Cancellation requested. Waiting for the owned worker to stop…";
    }

    private void Task_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AlgorithmSelector is null) return;
        var task = TaskSelector.SelectedItem as string;
        AlgorithmSelector.ItemsSource = algorithms.Where(algorithm => algorithm.Tasks.Contains(task ?? "", StringComparer.Ordinal)).ToArray();
        AlgorithmSelector.SelectedIndex = AlgorithmSelector.Items.Count > 0 ? 0 : -1;
        TargetSelector.IsEnabled = task is "classification" or "regression";
    }
    private void Target_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TargetSelector.SelectedItem is not string target) return;
        foreach (var feature in features.Where(feature => feature.Name == target)) feature.Use = false;
    }
    private void SuggestedFeatures_Click(object sender, RoutedEventArgs e)
    {
        foreach (var feature in features) feature.Use = feature.Suggested && feature.Name != (TargetSelector.SelectedItem as string);
    }
    private void ClearFeatures_Click(object sender, RoutedEventArgs e) { foreach (var feature in features) feature.Use = false; }

    private void Structure_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StructureGrid.SelectedItem is DataRowView row && row.Row.Table.Columns.Contains("key")) DatasetKey.Text = row["key"]?.ToString() ?? "";
    }
    private void History_SelectionChanged(object sender, SelectionChangedEventArgs e) => selectedExperiment = FindSelected(HistoryGrid, experiments);
    private void Models_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        selectedModel = FindSelected(ModelsGrid, models);
        if (selectedModel is { } model) ModelSummary.Text = $"{Text(model, "name")} · {Text(model, "algorithm")} · {Text(model, "task")}\nRequired features: {Text(model, "features")}\nVersion: {Text(model, "version")}";
    }
    private static JsonElement? FindSelected(DataGrid grid, IEnumerable<JsonElement> records)
    {
        if (grid.SelectedItem is not DataRowView row || !row.Row.Table.Columns.Contains("id")) return null;
        var id = row["id"]?.ToString();
        foreach (var record in records) if (Text(record, "id") == id) return record;
        return null;
    }
    private async void OpenResults_Click(object sender, RoutedEventArgs e)
    {
        if (selectedExperiment is not { } experiment) { StatusLabel.Text = "Select a saved experiment first."; return; }
        await ExecuteActionAsync(new MlDescribe(Text(experiment, "id")));
    }
    private async void Compare_Click(object sender, RoutedEventArgs e) => await HandleAsync(() =>
        new MlCompare(HistoryGrid.SelectedItems.OfType<DataRowView>().Select(row => row["id"]?.ToString() ?? "").ToArray()));
    private async void Export_Click(object sender, RoutedEventArgs e) => await HandleAsync(() => selectedExperiment is { } experiment
        ? new MlExport(Text(experiment, "id")) : throw new ArgumentException("Select a saved experiment first."));
    private async void Predict_Click(object sender, RoutedEventArgs e) => await HandleAsync(() => selectedModel is { } model
        ? new MlPredict(Text(model, "id"), ReadDataset(true)) : throw new ArgumentException("Select a saved model first."));

    private sealed class FeatureChoice(string name, string type, string missing, string advice, bool suggested) : INotifyPropertyChanged
    {
        private bool use = suggested;
        public string Name { get; } = name;
        public string Type { get; } = type;
        public string Missing { get; } = missing;
        public string Advice { get; } = advice;
        public bool Suggested { get; } = suggested;
        public bool Use { get => use; set { if (use == value) return; use = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Use))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed record JobRow(Guid Id, string Operation, string State, string Elapsed, string Memory, string Cpu, MlJobState JobState);
    private sealed record AlgorithmChoice(string Id, string Name, string[] Tasks) { public override string ToString() => Name; }
    private sealed record PlotChoice(string Name, string Path);
}
