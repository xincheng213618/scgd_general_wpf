using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotProfileConfigTests
{
    [Fact]
    public void ClonePreservesCredentialRecoveryState()
    {
        var source = CopilotProfileConfig.CreateDefault();
        source.ApiKey = string.Empty;
        source.CredentialNeedsReentry = true;

        var clone = source.Clone();

        Assert.True(clone.CredentialNeedsReentry);
        Assert.Contains("re-entry required", clone.ConfigurationStatusToolTip, StringComparison.Ordinal);
    }
}
