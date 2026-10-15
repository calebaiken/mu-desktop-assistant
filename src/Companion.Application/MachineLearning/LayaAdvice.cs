namespace Companion.Application.MachineLearning;

/// <summary>A local model's task suggestion. Scores are not calibrated scientific confidence.</summary>
public sealed record LayaAdvice(string Category, double Confidence, IReadOnlyDictionary<string, double> Probabilities,
    bool Truncated, string ModelRevision, double ElapsedSeconds, IReadOnlyList<string> Warnings)
{
    public static IReadOnlyList<string> Categories { get; } = Array.AsReadOnly(new[]
        { "regression", "classification", "clustering", "reduction", "anomaly", "other" });
    public bool CanSuggest => !Truncated && Category != "other" && Confidence >= .65;
    public string Explanation => Category switch
    {
        "regression" => "Predict a numerical target. Ridge offers an interpretable baseline; compare it with Random Forest for nonlinear relationships. Fit preprocessing only on training rows and inspect residuals.",
        "classification" => "Predict a known class label. Start with Logistic Regression and compare Random Forest. Use stratification when appropriate and inspect class balance, precision, recall and the confusion matrix.",
        "clustering" => "Explore unlabeled groups. Inspect a PCA projection, then compare k-Means or a Gaussian Mixture with DBSCAN. Clusters depend on the selected representation and do not establish physical populations.",
        "reduction" => "Explore structure with fewer dimensions. PCA is an inspectable baseline with component loadings and explained variance. Scaling changes the representation; honor your chosen units and overrides.",
        "anomaly" => "Rank unusual records. Compare Isolation Forest with Local Outlier Factor and inspect the records and score distributions. Anomaly scores depend on assumptions; preserve all outliers.",
        _ => "LAYA did not identify one of the supported ML tasks clearly. Specify whether you want to predict a number, predict a label, find groups, reduce dimensions or rank unusual records."
    };
}

public interface ILayaDecisionService : IAsyncDisposable
{
    Task<LayaAdvice> AdviseAsync(string prompt, CancellationToken cancellationToken = default);
}

public sealed class LayaException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
