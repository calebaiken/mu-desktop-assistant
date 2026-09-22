namespace Companion.Core.Configuration;

public sealed record CompanionSettings
{
    public const int CurrentVersion = 1;
    public int SchemaVersion { get; init; } = CurrentVersion;
    public string CompanionName { get; init; } = "Pip";
    public bool AlwaysOnTop { get; init; }
    public bool ReducedMotion { get; init; }
    public bool DiagnosticsEnabled { get; init; }

    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];
        if (SchemaVersion != CurrentVersion) errors.Add("This settings version is not supported.");
        if (string.IsNullOrWhiteSpace(CompanionName) || CompanionName.Length > 40 ||
            CompanionName.Any(char.IsControl))
            errors.Add("Choose a name of 1–40 characters without control characters.");
        return errors;
    }
}
