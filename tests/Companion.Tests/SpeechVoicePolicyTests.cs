using Companion.Application.Voice;
using Companion.Infrastructure.Voice;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Companion.Tests;

[TestClass]
public sealed class SpeechVoicePolicyTests
{
    private static readonly SpeechVoiceRegistration Microsoft = new("TTS_MS_TEST", "Microsoft Test", "en-US", "Microsoft", true);

    [TestMethod]
    public void MixedEnabledEnginesMayBeListedButCannotBeSelectedForPlayback()
    {
        var mixed = new[] { Microsoft, new SpeechVoiceRegistration("external", "External Test", "en-US", "External", true) };
        Assert.AreEqual(1, SpeechVoicePolicy.GetEligible(mixed).Count);
        var error = Assert.ThrowsExactly<SpeechException>(() => SpeechVoicePolicy.AdmitSelection(mixed, Microsoft.Id!));
        Assert.AreEqual(SpeechFailure.Unavailable, error.Failure);
    }

    [TestMethod]
    public void DisabledExternalRegistrationDoesNotPreventExactMicrosoftSelection()
    {
        var disabled = new[] { Microsoft, new SpeechVoiceRegistration("external", "External Test", "en-US", "External", false) };
        Assert.AreEqual(Microsoft.Id, SpeechVoicePolicy.AdmitSelection(disabled, Microsoft.Id!).Id);
        Assert.ThrowsExactly<SpeechException>(() => SpeechVoicePolicy.AdmitSelection(disabled, "external"));
        Assert.ThrowsExactly<SpeechException>(() => SpeechVoicePolicy.AdmitSelection(disabled, Microsoft.Id!.ToLowerInvariant()));
    }

    [TestMethod]
    public void MicrosoftLabelsMustAgreeAndMalformedTokensFailAdmission()
    {
        foreach (var invalid in new[]
        {
            Microsoft with { Vendor = "External" }, Microsoft with { Name = "External" },
            Microsoft with { Id = "external" }, Microsoft with { Culture = null },
            Microsoft with { Id = "TTS_MS_\ud800" }, Microsoft with { Name = new string('x', 129) }
        })
        {
            Assert.AreEqual(0, SpeechVoicePolicy.GetEligible([invalid]).Count);
            Assert.ThrowsExactly<SpeechException>(() => SpeechVoicePolicy.AdmitSelection([invalid], Microsoft.Id!));
        }
    }

    [TestMethod]
    public void AmbiguousNamesAndIdsAreNeverSilentlySubstituted()
    {
        Assert.AreEqual(0, SpeechVoicePolicy.GetEligible([Microsoft, Microsoft with { Id = "TTS_MS_OTHER" }]).Count);
        Assert.AreEqual(0, SpeechVoicePolicy.GetEligible([Microsoft, Microsoft with { Name = "Microsoft Other" }]).Count);
        Assert.AreEqual(0, SpeechVoicePolicy.GetEligible([Microsoft, Microsoft with { Id = "TTS_MS_OTHER" },
            Microsoft with { Name = "Microsoft Other" }]).Count);
        Assert.ThrowsExactly<SpeechException>(() => SpeechVoicePolicy.AdmitSelection([Microsoft with { Enabled = false }], Microsoft.Id!));
    }
}
