using System.Buffers.Binary;
using System.Data;
using System.IO;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Companion.Application.MachineLearning;

namespace Companion.Desktop;

public partial class MlLabWindow
{
    private void ApplyResult(MlResult result)
    {
        var data = result.Data;
        switch (result.Operation)
        {
            case "capabilities":
                algorithms.Clear();
                foreach (var item in Array(data, "algorithms").Take(64))
                    algorithms.Add(new AlgorithmChoice(Text(item, "id"), Text(item, "name"),
                        Array(item, "tasks").Select(task => task.GetString() ?? "").ToArray()));
                Task_SelectionChanged(this, null!);
                if (algorithms.Count == 0) StatusLabel.Text = "The local runtime did not report any available algorithms.";
                break;
            case "browse":
                browseEntries.Clear();
                foreach (var item in Array(data, "entries").Take(256)) browseEntries.Add(item.Clone());
                StructureGrid.ItemsSource = Table(browseEntries, 256).DefaultView;
                LabTabs.SelectedItem = DataTab;
                break;
            case "inspect":
                ShowInspection(data);
                LabTabs.SelectedItem = InspectTab;
                break;
            case "train":
            case "describe":
                selectedExperiment = data.Clone();
                ShowExperiment(data);
                LabTabs.SelectedItem = ResultsTab;
                break;
            case "compare":
                ResultSummary.Text = "Model comparison · Same dataset selection, features, target and split";
                ResultWarnings.Text = Warnings(data);
                ResultMetadata.Text = Pretty(data);
                MetricsGrid.ItemsSource = Table(Array(data, "models"), 5).DefaultView;
                ShowPlots(data);
                LabTabs.SelectedItem = ResultsTab;
                break;
            case "list":
                HistoryWarnings.Text = Warnings(data);
                experiments.Clear();
                experiments.AddRange(Array(data, "experiments").Take(500).Select(item => item.Clone()));
                models.Clear();
                models.AddRange(Array(data, "models").Take(500).Select(item => item.Clone()));
                HistoryGrid.ItemsSource = Table(experiments, 500, "id", "name", "createdAt", "task", "algorithm", "status", "durationSeconds", "metrics").DefaultView;
                ModelsGrid.ItemsSource = Table(models, 500, "id", "name", "algorithm", "task", "version", "createdAt", "modelBytes", "metrics").DefaultView;
                break;
            case "predict":
                PredictionGrid.ItemsSource = Table(Array(data, "preview"), 100).DefaultView;
                PredictionSummary.Text = $"{Text(data, "rows")} rows predicted · Model version {Text(data, "version")}\nOutput: {Text(data, "outputPath")}\n{Warnings(data)}";
                LabTabs.SelectedItem = ModelsTab;
                break;
            case "export":
                ExportPath.Text = Text(data, "path");
                LabTabs.SelectedItem = HistoryTab;
                break;
        }
    }

    private void ShowInspection(JsonElement data)
    {
        var dataset = Property(data, "dataset");
        DatasetSummary.Text = $"{Text(dataset, "name")} · {Text(dataset, "format")}\nSelected rows: {Text(dataset, "selectedRows")} · Source rows: {Text(dataset, "rows")} · Columns: {Text(dataset, "columns")} · Estimated selected memory: {Text(dataset, "memoryBytes")} bytes\n" +
            (Flag(data, "sampled") ? "BOUNDED ROW SELECTION — these statistics describe the contiguous selected rows; they may not represent the whole source." : "Statistics describe the inspected selection.");
        InspectWarnings.Text = Warnings(data);
        PreviewGrid.ItemsSource = Table(Array(data, "preview"), 100).DefaultView;
        var statistics = Array(data, "features").Take(128).ToArray();
        StatisticsGrid.ItemsSource = Table(statistics, 128).DefaultView;
        DatasetMetadata.Text = Pretty(dataset);
        features.Clear();
        foreach (var feature in statistics)
        {
            var identifier = Flag(feature, "identifier");
            var constant = Flag(feature, "constant");
            var nearConstant = Flag(feature, "nearConstant");
            var advice = string.Join("; ", new[] { identifier ? "possible identifier" : null, constant ? "constant" : null, nearConstant ? "near constant" : null }.Where(value => value is not null));
            features.Add(new FeatureChoice(Text(feature, "name"), Text(feature, "type"), Text(feature, "missing"), advice, !identifier && !constant && !nearConstant));
        }
        var names = features.Select(feature => feature.Name).ToArray();
        TargetSelector.ItemsSource = names;
        TargetSelector.SelectedIndex = -1;
        GroupSelector.ItemsSource = names;
        GroupSelector.SelectedIndex = -1;
        ShowPlots(data);
    }

    private void ShowExperiment(JsonElement experiment)
    {
        ResultSummary.Text = $"{Text(experiment, "name")} · {Text(experiment, "task")} · {Text(experiment, "algorithm")}\nStatus: {Text(experiment, "status")} · Duration: {Text(experiment, "durationSeconds")} s · Seed: {Text(experiment, "seed")}\nModel: {Text(experiment, "modelId")}";
        ResultWarnings.Text = Warnings(experiment);
        ResultMetadata.Text = Pretty(experiment);
        var metrics = new DataTable();
        metrics.Columns.Add("Metric");
        metrics.Columns.Add("Value");
        FlattenMetrics(Property(experiment, "metrics"), "", metrics, 0);
        MetricsGrid.ItemsSource = metrics.DefaultView;
        ShowPlots(experiment);
    }

    private static void FlattenMetrics(JsonElement element, string prefix, DataTable rows, int depth)
    {
        if (rows.Rows.Count >= 128) return;
        if (element.ValueKind == JsonValueKind.Object && depth < 4)
        {
            foreach (var item in element.EnumerateObject()) FlattenMetrics(item.Value, prefix.Length == 0 ? item.Name : $"{prefix} / {item.Name}", rows, depth + 1);
        }
        else if (element.ValueKind != JsonValueKind.Undefined) rows.Rows.Add(prefix, Display(element, 2048));
    }

    private void ShowPlots(JsonElement owner)
    {
        PlotImage.Source = null;
        PlotSelector.ItemsSource = Array(owner, "plots").Take(32).Select(plot => new PlotChoice(Text(plot, "name"), Text(plot, "path"))).ToArray();
        PlotSelector.SelectedIndex = PlotSelector.Items.Count > 0 ? 0 : -1;
    }

    private void Plot_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        PlotImage.Source = null;
        if (PlotSelector.SelectedItem is not PlotChoice plot) return;
        try { PlotImage.Source = LoadProjectPlot(service.ProjectDirectory, plot.Path); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException or FormatException or OverflowException or System.Security.SecurityException)
        { StatusLabel.Text = "This plot could not be displayed. Only bounded PNG artifacts inside this Lab project are accepted."; }
    }

    internal static BitmapSource LoadProjectPlot(string projectDirectory, string filePath)
    {
        var root = Path.GetFullPath(projectDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(filePath);
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Plot path is outside the Lab project.");
        for (var current = path; current is not null; current = Path.GetDirectoryName(current))
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("Linked plot paths are not accepted.");
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is < 33 or > 8 * 1024 * 1024) throw new ArgumentException("Plot file size is outside the supported bound.");
        Span<byte> header = stackalloc byte[24];
        stream.ReadExactly(header);
        if (!header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) || !header.Slice(12, 4).SequenceEqual("IHDR"u8))
            throw new ArgumentException("A PNG plot is required.");
        var width = BinaryPrimitives.ReadUInt32BigEndian(header.Slice(16, 4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(header.Slice(20, 4));
        if (width is 0 or > 10000 || height is 0 or > 10000 || (long)width * height > 16000000)
            throw new ArgumentException("Plot image dimensions exceed the supported bound.");
        stream.Position = 0;
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = (int)Math.Min(width, 1600);
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static DataTable Table(IEnumerable<JsonElement> values, int maximumRows, params string[] selectedColumns)
    {
        var records = values.Take(maximumRows).Where(value => value.ValueKind == JsonValueKind.Object).ToArray();
        var columns = selectedColumns.Length > 0 ? selectedColumns : records.SelectMany(record => record.EnumerateObject().Select(property => property.Name)).Distinct(StringComparer.Ordinal).Take(128).ToArray();
        var table = new DataTable();
        foreach (var column in columns) table.Columns.Add(column, typeof(string));
        foreach (var record in records) table.Rows.Add(columns.Select(column => (object)Display(Property(record, column), 512)).ToArray());
        return table;
    }

    private static JsonElement Property(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : default;
    private static IEnumerable<JsonElement> Array(JsonElement element, string name)
    {
        var value = Property(element, name);
        return value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : [];
    }
    private static string Text(JsonElement element, string name) => Display(Property(element, name), 4096);
    private static bool Flag(JsonElement element, string name) => Property(element, name).ValueKind == JsonValueKind.True;
    private static string Warnings(JsonElement element) => string.Join(Environment.NewLine, Array(element, "warnings").Take(32).Select(warning => Display(warning, 1024)));
    private static string Display(JsonElement element, int maximumLength)
    {
        var value = element.ValueKind switch { JsonValueKind.Undefined => "", JsonValueKind.Null => "not available", JsonValueKind.String => element.GetString() ?? "", _ => element.GetRawText() };
        if (value.Length <= maximumLength) return value;
        var end = maximumLength;
        if (char.IsHighSurrogate(value[end - 1])) end--;
        return value[..end] + "…";
    }
    private static string Pretty(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Undefined) return "";
        var text = JsonSerializer.Serialize(element, new JsonSerializerOptions { WriteIndented = true });
        if (text.Length <= 262144) return text;
        var length = char.IsHighSurrogate(text[262143]) ? 262143 : 262144;
        return text[..length] + "\n[View truncated. The full metadata remains in the project artifact.]";
    }
}
