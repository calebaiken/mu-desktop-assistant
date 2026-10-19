using System.Buffers;
using System.Globalization;
using System.Text;

namespace Companion.Application.MachineLearning;

/// <summary>Disposable, process-local transfer data. It grants no ML execution authority.</summary>
public sealed class ResearchRequestHandoff : IDisposable
{
    internal ResearchRequestHandoff(ResearchProject project, object lease, long revision, string request)
    { Project = project; Lease = lease; Revision = revision; this.request = request; }
    public ResearchProject Project { get; }
    private string? request;
    public string? Request => Volatile.Read(ref request);
    internal object Lease { get; }
    internal long Revision { get; }
    internal string Consume()
    {
        var value = Interlocked.Exchange(ref request, null) ?? throw new MlException("handoff_used", "This transfer has ended. Select the request and project again.");
        return value;
    }
    public void Dispose() => Interlocked.Exchange(ref request, null);

    public static void ValidateRequest(string request)
    {
        if (string.IsNullOrWhiteSpace(request) || request.Length > 2000)
            throw new ArgumentException("Enter one research request of up to 2,000 characters in the Chat message box.");
        var remaining = request.AsSpan();
        while (!remaining.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(remaining, out var rune, out var count) != OperationStatus.Done ||
                Rune.GetUnicodeCategory(rune) is UnicodeCategory.Format or UnicodeCategory.Surrogate ||
                Rune.GetUnicodeCategory(rune) == UnicodeCategory.Control && rune.Value is not (9 or 10 or 13))
                throw new ArgumentException("Remove hidden control characters or malformed text before transferring the request.");
            remaining = remaining[count..];
        }
    }
}
