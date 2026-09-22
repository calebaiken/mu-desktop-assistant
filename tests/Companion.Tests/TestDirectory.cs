namespace Companion.Tests;

internal sealed class TestDirectory : IDisposable
{
    private static readonly string Parent = Path.Combine(Path.GetTempPath(), "Companion.Tests");
    public string PathValue { get; } = Path.Combine(Parent, Guid.NewGuid().ToString("N"));
    public TestDirectory() => Directory.CreateDirectory(PathValue);
    public string FilePath(string name) => Path.Combine(PathValue, name);
    public void Dispose()
    {
        if (Path.GetDirectoryName(Path.GetFullPath(PathValue)) != Parent ||
            !Guid.TryParseExact(Path.GetFileName(PathValue), "N", out _))
            throw new InvalidOperationException("Unsafe fixture cleanup.");
        Directory.Delete(PathValue, recursive: true);
    }
}
