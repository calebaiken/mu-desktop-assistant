using System.Text.Json;
using Companion.Application.MachineLearning;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

/// <summary>Reviewer-owned closed action boundary checks. No worker or dataset is started.</summary>
[TestClass]
public sealed class IndependentMlActionTests
{
    [TestMethod]
    public void AllAdvertisedActionsRoundTripThroughOneRegistry()
    {
        MlAction[] actions =
        [
            new MlCapabilities(), new MlList(),
            new MlInspect(new(@"C:\research\sample.csv")),
            new MlBrowse(new(@"C:\research\events.h5", "/events", 3, 25)),
            new MlTrain(new(@"C:\research\sample.csv"), "regression", "ridge", ["x"], "y"),
            new MlPredict(Guid.NewGuid().ToString(), new(@"C:\research\new.csv")),
            new MlExport(Guid.NewGuid().ToString()), new MlDescribe(Guid.NewGuid().ToString()),
            new MlCompare([Guid.NewGuid().ToString(), Guid.NewGuid().ToString()])
        ];
        foreach (var original in actions)
        {
            var encoded = JsonSerializer.SerializeToElement(MlActionCodec.Parameters(original), MlActionCodec.Json);
            var decoded = MlActionCodec.Parse(original.Operation, encoded);
            Assert.AreEqual(original.GetType(), decoded.GetType());
            Assert.AreEqual(original.Operation, decoded.Operation);
            Assert.AreEqual(encoded.GetRawText(),
                JsonSerializer.SerializeToElement(MlActionCodec.Parameters(decoded), MlActionCodec.Json).GetRawText());
        }
    }

    [TestMethod]
    public void UnknownActionAndExecutableTextAreNeverRegistered()
    {
        foreach (var operation in new[] { "", "python", "exec", "__import__", "train; shutdown", "Train", "delete_source" })
            AssertRejected(operation, "{}");
    }

    [TestMethod]
    public void UnknownFieldsAreRejectedForEveryActionShape()
    {
        AssertRejected("capabilities", "{\"code\":\"PRIVATE_FIELD_SENTINEL\"}");
        AssertRejected("list", "{\"upload\":true}");
        AssertRejected("inspect", "{\"path\":\"C:\\\\research\\\\data.csv\",\"execute\":true}");
        AssertRejected("browse", "{\"path\":\"C:\\\\research\\\\data.h5\",\"followExternalLinks\":true}");
        AssertRejected("train", "{\"dataset\":{\"path\":\"C:\\\\research\\\\data.csv\"},\"task\":\"regression\",\"algorithm\":\"ridge\",\"features\":[\"x\"],\"target\":\"y\",\"code\":\"anything\"}");
        AssertRejected("predict", "{\"modelId\":\"5b254eba-8943-4cdf-b17e-d9b810f50536\",\"dataset\":{\"path\":\"C:\\\\research\\\\data.csv\"},\"trustAllTypes\":true}");
        AssertRejected("export", "{\"experimentId\":\"5b254eba-8943-4cdf-b17e-d9b810f50536\",\"destination\":\"C:\\\\outside.py\"}");
    }

    [TestMethod]
    public void NestedDatasetUnknownFieldsAreRejected()
    {
        AssertRejected("train", "{\"dataset\":{\"path\":\"C:\\\\data.csv\",\"allowPickle\":true},\"task\":\"regression\",\"algorithm\":\"ridge\",\"features\":[\"x\"],\"target\":\"y\"}");
    }

    [TestMethod]
    public void ModelCannotOverrideRegisteredOperationThroughParameters()
    {
        AssertRejected("train", "{\"operation\":\"delete_source\",\"dataset\":{\"path\":\"C:\\\\data.csv\"},\"task\":\"regression\",\"algorithm\":\"ridge\",\"features\":[\"x\"],\"target\":\"y\"}");
    }

    [TestMethod]
    public void NonObjectParametersAreRejectedEvenForEmptyActions()
    {
        foreach (var operation in new[] { "capabilities", "list", "inspect", "browse", "train", "predict", "export", "describe", "compare" })
            foreach (var value in new[] { "null", "[]", "\"anything\"", "true", "12" })
                AssertRejected(operation, value);
    }

    [TestMethod]
    public void DuplicatePropertiesCannotCreateAmbiguousProposals()
    {
        AssertRejected("inspect", "{\"path\":\"C:\\\\first.csv\",\"path\":\"C:\\\\second.csv\"}");
        AssertRejected("export", "{\"experimentId\":\"5b254eba-8943-4cdf-b17e-d9b810f50536\",\"experimentId\":null}");
    }

    [TestMethod]
    public void MissingAndNullRequiredValuesAreRejectedAtActionBoundary()
    {
        foreach (var operation in new[] { "inspect", "browse", "train", "predict", "export", "describe", "compare" })
            AssertRejected(operation, "{}");
        AssertRejected("inspect", "{\"path\":null}");
        AssertRejected("train", "{\"dataset\":null,\"task\":null,\"algorithm\":null,\"features\":null}");
        AssertRejected("compare", "{\"experimentIds\":null}");
        AssertRejected("compare", "{\"experimentIds\":[]}");
        AssertRejected("compare", "{\"experimentIds\":[\"5b254eba-8943-4cdf-b17e-d9b810f50536\",\"5b254eba-8943-4cdf-b17e-d9b810f50536\"]}");
    }

    [TestMethod]
    public void ComputeLimitsRejectInvalidAndOverflowValues()
    {
        foreach (var limits in new[] { new MlLimits(0), new MlLimits(-1), new MlLimits(int.MaxValue),
                     new MlLimits(MemoryMiB: 0), new MlLimits(MemoryMiB: int.MaxValue),
                     new MlLimits(Threads: 0), new MlLimits(Threads: int.MaxValue) })
            Assert.ThrowsExactly<ArgumentException>(limits.Validate);
        new MlLimits().Validate();
    }

    private static void AssertRejected(string operation, string json)
    {
        using var document = JsonDocument.Parse(json);
        try { _ = MlActionCodec.Parse(operation, document.RootElement); }
        catch (Exception error) when (error is ArgumentException or JsonException) { return; }
        Assert.Fail($"Action boundary accepted invalid {operation} parameters.");
    }
}
