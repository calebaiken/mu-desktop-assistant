using System.Text.Json;
using System.Text.Json.Nodes;

namespace Companion.Application.MachineLearning;

/// <summary>Closed data-only proposal shapes. The ordinary action validator remains authoritative.</summary>
internal static class MlProposalSchema
{
    internal static JsonElement Create(bool hasSelection, IReadOnlyList<string> columns)
    {
        var selection = JsonNode.Parse("""
            {"type":"object","required":["path"],"additionalProperties":false,"properties":{
              "path":{"type":"string","minLength":1,"maxLength":2048},
              "key":{"type":["string","null"],"maxLength":512},
              "start":{"type":"integer","minimum":0},"rowLimit":{"type":"integer","minimum":1,"maximum":100000}}}
            """)!;
        JsonNode Dataset() => hasSelection
            ? new JsonObject { ["const"] = "current" }
            : selection.DeepClone();
        JsonNode TableAction() => hasSelection
            ? Object(new JsonObject { ["selection"] = new JsonObject { ["const"] = "current" } }, "selection")
            : selection.DeepClone();
        var feature = new JsonObject { ["type"] = "string", ["minLength"] = 1, ["maxLength"] = 200 };
        if (columns.Count > 0) feature["enum"] = JsonSerializer.SerializeToNode(columns);
        var train = JsonNode.Parse("""
            {"type":"object","required":["dataset","task","algorithm","features"],"additionalProperties":false,"properties":{
              "task":{"enum":["regression","classification","clustering","reduction","anomaly"]},
              "algorithm":{"enum":["linear","ridge","logistic","decision_tree","random_forest","extra_trees","gradient_boosting","svr","svc","knn","kmeans","gaussian_mixture","dbscan","pca","isolation_forest","local_outlier_factor"]},
              "target":{"type":["string","null"],"maxLength":200},"scaling":{"enum":["standard","robust","minmax","none"]},
              "testFraction":{"type":"number","minimum":0.1,"maximum":0.5},"seed":{"type":"integer","minimum":0},
              "split":{"enum":["random","stratified","group","time"]},"groupColumn":{"type":["string","null"],"maxLength":200},
              "folds":{"enum":[0,2,3,4,5,6,7,8,9,10]},"name":{"type":"string","minLength":1,"maxLength":100}}}
            """)!.AsObject();
        train["properties"]!["dataset"] = Dataset();
        train["properties"]!["features"] = new JsonObject { ["type"] = "array", ["minItems"] = 1, ["maxItems"] = 128, ["items"] = feature };
        var branches = new JsonArray();
        void Add(string action, JsonNode parameters) => branches.Add(Object(new JsonObject
        {
            ["action"] = new JsonObject { ["const"] = action }, ["parameters"] = parameters,
            ["reason"] = new JsonObject { ["type"] = "string", ["minLength"] = 1, ["maxLength"] = 240 }
        }, "action", "parameters", "reason"));
        Add("inspect", TableAction());
        Add("browse", TableAction());
        Add("train", train);
        Add("list", Object(new()));
        Add("predict", Object(new JsonObject { ["modelId"] = Id(), ["dataset"] = Dataset() }, "modelId", "dataset"));
        Add("export", Object(new JsonObject { ["experimentId"] = Id() }, "experimentId"));
        Add("describe", Object(new JsonObject { ["experimentId"] = Id() }, "experimentId"));
        Add("compare", Object(new JsonObject { ["experimentIds"] = new JsonObject { ["type"] = "array", ["minItems"] = 2, ["maxItems"] = 5, ["items"] = Id() } }, "experimentIds"));
        return JsonSerializer.SerializeToElement(new JsonObject { ["anyOf"] = branches });
    }

    private static JsonObject Id() => new() { ["type"] = "string", ["minLength"] = 1, ["maxLength"] = 80 };
    private static JsonObject Object(JsonObject properties, params string[] required) => new()
    {
        ["type"] = "object", ["properties"] = properties, ["required"] = JsonSerializer.SerializeToNode(required), ["additionalProperties"] = false
    };

    internal static JsonElement ResolveSelection(string operation, JsonElement parameters, MlDatasetSelection? selection)
    {
        if (parameters.ValueKind != JsonValueKind.Object) throw new ArgumentException("Proposal parameters must be an object.");
        if (operation is "inspect" or "browse" && parameters.TryGetProperty("selection", out var alias))
        {
            if (parameters.EnumerateObject().Count() != 1 || alias.ValueKind != JsonValueKind.String || alias.GetString() != "current" || selection is null)
                throw new ArgumentException("Select a dataset before using the current selection.");
            return JsonSerializer.SerializeToElement(selection, MlActionCodec.Json);
        }
        if (operation is "train" or "predict" && parameters.TryGetProperty("dataset", out var dataset) && dataset.ValueKind == JsonValueKind.String)
        {
            if (dataset.GetString() != "current" || selection is null) throw new ArgumentException("Select a dataset before using the current selection.");
            // Preserve all other fields, including duplicates, for the strict codec to reject.
            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                foreach (var property in parameters.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    if (property.Name == "dataset") JsonSerializer.Serialize(writer, selection, MlActionCodec.Json);
                    else property.Value.WriteTo(writer);
                }
                writer.WriteEndObject();
            }
            using var resolved = JsonDocument.Parse(buffer.ToArray());
            return resolved.RootElement.Clone();
        }
        return parameters;
    }
}
