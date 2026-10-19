using System.Globalization;
using System.Text;

namespace Companion.Application.MachineLearning;

public sealed record ResearchProject(Guid Id, string Name, string Directory, DateTimeOffset CreatedAt, bool IsLegacy = false);

public interface IResearchProjectStore
{
    Task<IReadOnlyList<ResearchProject>> ListAsync(CancellationToken cancellationToken = default);
    Task<ResearchProject> CreateAsync(string name, CancellationToken cancellationToken = default);
    Task<ResearchProject> OpenAsync(Guid id, CancellationToken cancellationToken = default);
}

public static class ResearchProjects
{
    public const int MaximumProjects = 32;
    public const int MaximumNameCharacters = 80;
    public static readonly Guid LegacyId = Guid.Empty;

    public static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaximumNameCharacters || name != name.Trim())
            throw new ArgumentException("Use a project name of 1–80 characters without leading or trailing spaces.");
        var remaining = name.AsSpan();
        while (!remaining.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(remaining, out var rune, out var count) != System.Buffers.OperationStatus.Done ||
                Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
                throw new ArgumentException("Project names cannot contain control or invisible formatting characters.");
            remaining = remaining[count..];
        }
    }
}
