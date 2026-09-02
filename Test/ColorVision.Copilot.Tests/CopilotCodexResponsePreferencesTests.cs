using ColorVision.Copilot;
using System;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexResponsePreferencesTests
{
    [Theory]
    [InlineData("fast", "fast", "priority")]
    [InlineData("FLEX", "flex", "flex")]
    [InlineData("model.tier-2", "model.tier-2", "model.tier-2")]
    public void ServiceTierNormalizationPreservesAdvertisedTokensAndMapsFast(
        string value,
        string expectedConfigured,
        string expectedRequest)
    {
        Assert.True(CopilotCodexServiceTierSelection.TryNormalize(value, out var configured));
        Assert.Equal(expectedConfigured, configured);
        Assert.Equal(expectedRequest, CopilotCodexServiceTierSelection.GetRequestToken(configured));
    }
}
