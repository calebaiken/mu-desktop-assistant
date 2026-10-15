using System.Text.Json;
using System.Text.Json.Serialization;

namespace Companion.Application.MachineLearning;

public sealed record MlLimits(int MaxRows = 10000, int MemoryMiB = 1024, int Threads = 2)
{
    public void Validate()
    {
        if (MaxRows is < 10 or > 100000 || MemoryMiB is < 256 or > 4096 || Threads is < 1 or > 8)
            throw new ArgumentException("Use 10–100,000 rows, 256–4,096 MiB and 1–8 CPU threads.");
    }
}

public sealed record MlDatasetSelection(string Path, string? Key = null, int Start = 0, int RowLimit = 10000);
public abstract record MlAction(string Operation);
public sealed record MlCapabilities() : MlAction("capabilities");
public sealed record MlInspect(MlDatasetSelection Dataset) : MlAction("inspect");
public sealed record MlBrowse(MlDatasetSelection Dataset) : MlAction("browse");
public sealed record MlList() : MlAction("list");
public sealed record MlTrain(MlDatasetSelection Dataset, string Task, string Algorithm,
    string[] Features, string? Target = null, string Scaling = "standard", double TestFraction = 0.2,
    int Seed = 42, string Split = "random", string? GroupColumn = null, int Folds = 0,
    string Name = "Baseline", Dictionary<string, JsonElement>? Hyperparameters = null) : MlAction("train");
public sealed record MlPredict(string ModelId, MlDatasetSelection Dataset) : MlAction("predict");
public sealed record MlExport(string ExperimentId) : MlAction("export");
public sealed record MlDescribe(string ExperimentId) : MlAction("describe");
public sealed record MlCompare(string[] ExperimentIds) : MlAction("compare");

public enum MlJobState { Queued, Preparing, Running, Completed, Cancelled, Failed }
public sealed record MlJobUpdate(Guid Id, string Operation, MlJobState State, TimeSpan Elapsed,
    long WorkerMemoryBytes = 0, double? CpuPercent = null);
public sealed record MlResult(Guid OperationId, string Operation, JsonElement Data);

public sealed class MlException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public interface IMlLabService : IAsyncDisposable
{
    string ProjectDirectory { get; }
    IReadOnlyList<MlJobUpdate> Jobs { get; }
    void Cancel(Guid jobId);
    event Action<MlJobUpdate>? JobChanged;
    Task<MlResult> ExecuteAsync(MlAction action, MlLimits? limits = null, CancellationToken cancellationToken = default);
}

/// <summary>Closed action registry shared by UI and local conversational proposals.</summary>
public static class MlActionCodec
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { MaxDepth = 24, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public static object Parameters(MlAction action) => action switch
    {
        MlCapabilities or MlList => new { },
        MlInspect inspect => inspect.Dataset,
        MlBrowse browse => browse.Dataset,
        MlTrain train => new { train.Dataset, train.Task, train.Algorithm, train.Features, train.Target,
            train.Scaling, train.TestFraction, train.Seed, train.Split, train.GroupColumn, train.Folds,
            train.Name, Hyperparameters = train.Hyperparameters ?? [] },
        MlPredict predict => new { predict.ModelId, predict.Dataset },
        MlExport export => new { export.ExperimentId },
        MlDescribe describe => new { describe.ExperimentId },
        MlCompare compare => new { compare.ExperimentIds },
        _ => throw new ArgumentException("Unknown ML action.")
    };

    public static MlAction Parse(string operation, JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Object) throw new ArgumentException("ML parameters must be an object.");
        CheckObject(parameters);
        if (parameters.EnumerateObject().Any(field => string.Equals(field.Name, "operation", StringComparison.OrdinalIgnoreCase)) || operation is "capabilities" or "list" && parameters.EnumerateObject().Any())
            throw new ArgumentException("Unexpected ML action parameter.");
        var action = ParseCore(operation, parameters);
        MlValidation.Validate(action, new(MaxRows: 100000));
        return action;
    }

    private static void CheckObject(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in element.EnumerateObject())
            {
                if (!names.Add(field.Name)) throw new ArgumentException("Duplicate ML parameter.");
                CheckObject(field.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) CheckObject(item);
    }

    private static MlAction ParseCore(string operation, JsonElement parameters) => operation switch
    {
        "capabilities" => new MlCapabilities(),
        "list" => new MlList(),
        "inspect" => new MlInspect(Read<MlDatasetSelection>(parameters)),
        "browse" => new MlBrowse(Read<MlDatasetSelection>(parameters)),
        "train" => Read<MlTrain>(parameters),
        "predict" => Read<MlPredict>(parameters),
        "export" => Read<MlExport>(parameters),
        "describe" => Read<MlDescribe>(parameters),
        "compare" => Read<MlCompare>(parameters),
        _ => throw new ArgumentException("This ML action is not available.")
    };

    private static T Read<T>(JsonElement element) => element.Deserialize<T>(Json)
        ?? throw new ArgumentException("The ML action is incomplete.");
}

public static class MlValidation
{
    public static void Validate(MlAction action, MlLimits limits)
    {
        ArgumentNullException.ThrowIfNull(action);
        limits.Validate();
        var expected = action switch
        {
            MlCapabilities => "capabilities", MlList => "list", MlInspect => "inspect", MlBrowse => "browse",
            MlTrain => "train", MlPredict => "predict", MlExport => "export", MlDescribe => "describe", MlCompare => "compare", _ => ""
        };
        if (expected.Length == 0 || action.Operation != expected) throw new ArgumentException("The ML action type and operation must agree.");
        _ = MlActionCodec.Parameters(action);
        switch (action)
        {
            case MlInspect inspect: Dataset(inspect.Dataset, limits); break;
            case MlBrowse browse: Dataset(browse.Dataset, limits); break;
            case MlPredict predict:
                Identifier(predict.ModelId); Dataset(predict.Dataset, limits); break;
            case MlExport export: Identifier(export.ExperimentId); break;
            case MlDescribe describe: Identifier(describe.ExperimentId); break;
            case MlCompare compare:
                if (compare.ExperimentIds is not { Length: >= 2 and <= 5 } || compare.ExperimentIds.Distinct(StringComparer.Ordinal).Count() != compare.ExperimentIds.Length)
                    throw new ArgumentException("Select two to five distinct completed experiments to compare.");
                foreach (var id in compare.ExperimentIds) Identifier(id);
                break;
            case MlTrain train:
                Dataset(train.Dataset, limits);
                if (train.Task is not ("regression" or "classification" or "clustering" or "reduction" or "anomaly") ||
                    string.IsNullOrWhiteSpace(train.Algorithm) || train.Algorithm.Length > 64 ||
                    train.Features is not { Length: > 0 and <= 128 } || train.Features.Any(InvalidName) ||
                    train.Features.Distinct(StringComparer.Ordinal).Count() != train.Features.Length ||
                    train.Target is not null && InvalidName(train.Target) ||
                    train.Target is not null && train.Features.Contains(train.Target, StringComparer.Ordinal) ||
                    train.Task is "regression" or "classification" && train.Target is null ||
                    train.Scaling is not ("standard" or "robust" or "minmax" or "none") ||
                    !double.IsFinite(train.TestFraction) || train.TestFraction is < 0.1 or > 0.5 ||
                    train.Seed is < 0 || train.Split is not ("random" or "stratified" or "group" or "time") ||
                    train.Split == "group" && InvalidName(train.GroupColumn) ||
                    train.Folds is < 0 or 1 or > 10 || InvalidName(train.Name) ||
                    train.Hyperparameters?.Count > 20)
                    throw new ArgumentException("Check the task, features, target, preprocessing and split configuration.");
                break;
        }
    }

    private static bool InvalidName(string? value) => string.IsNullOrWhiteSpace(value) || value.Length > 200 || value.Any(char.IsControl);
    private static void Identifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 80 || value.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
            throw new ArgumentException("Select an existing local experiment or model.");
    }
    private static void Dataset(MlDatasetSelection dataset, MlLimits limits)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        if (string.IsNullOrWhiteSpace(dataset.Path) || dataset.Path.Length > 2048 ||
            !Path.IsPathFullyQualified(dataset.Path) || dataset.Path.StartsWith("\\\\", StringComparison.Ordinal) || dataset.Path.StartsWith("//", StringComparison.Ordinal) ||
            dataset.Path.AsSpan(2).Contains(':') ||
            dataset.Path.Any(char.IsControl) || dataset.Start < 0 || dataset.RowLimit < 1 || dataset.RowLimit > limits.MaxRows ||
            dataset.Key is { Length: > 512 } || dataset.Key?.Any(char.IsControl) == true)
            throw new ArgumentException("Choose an absolute local dataset path and a row selection within the compute limit.");
    }
}
