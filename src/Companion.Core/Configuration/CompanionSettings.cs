using Companion.Core.Desktop;

namespace Companion.Core.Configuration;

public sealed record CompanionSettings
{
    public const int CurrentVersion = 3;
    public int SchemaVersion { get; init; } = CurrentVersion;
    public string CompanionName { get; init; } = "Mu";
    public bool AlwaysOnTop { get; init; }
    public bool ReducedMotion { get; init; }
    public bool DiagnosticsEnabled { get; init; }
    public double AvatarScale { get; init; } = 1;
    public double AvatarOpacity { get; init; } = 1;
    public AvatarPreset AvatarPreset { get; init; } = AvatarPreset.Mu;
    public AvatarAccent AvatarAccent { get; init; } = AvatarAccent.Teal;
    public AvatarDecoration AvatarDecoration { get; init; } = AvatarDecoration.HaloAndTrail;
    public AvatarFace AvatarFace { get; init; } = AvatarFace.Friendly;
    public SummonHotkey SummonShortcut { get; init; } = SummonHotkey.CtrlAltSpace;

    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];
        if (SchemaVersion != CurrentVersion) errors.Add("This settings version is not supported.");
        if (string.IsNullOrWhiteSpace(CompanionName) || CompanionName.Length > 40 ||
            CompanionName.Any(char.IsControl))
            errors.Add("Choose a name of 1–40 characters without control characters.");
        if (!double.IsFinite(AvatarScale) || AvatarScale is < 0.75 or > 1.5)
            errors.Add("Choose an avatar scale from 0.75 to 1.5.");
        if (!double.IsFinite(AvatarOpacity) || AvatarOpacity is < 0.4 or > 1)
            errors.Add("Choose an avatar opacity from 0.4 to 1.");
        if (!Enum.IsDefined(SummonShortcut)) errors.Add("Choose one of the supported summon shortcuts.");
        if (!Enum.IsDefined(AvatarPreset)) errors.Add("Choose one of the supported avatar presets.");
        if (!Enum.IsDefined(AvatarAccent)) errors.Add("Choose one of the supported accent palettes.");
        if (!Enum.IsDefined(AvatarDecoration)) errors.Add("Choose one of the supported avatar decorations.");
        if (!Enum.IsDefined(AvatarFace)) errors.Add("Choose one of the supported avatar faces.");
        return errors;
    }
}
