namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexReasoningOptionsTests
{
    [Fact]
    public void NoneReasoningIsOnlyAcceptedForPlanMode()
    {
        Assert.False(CopilotCodexReasoningEffortSelection.TryParse("none", out _));
        Assert.True(CopilotCodexReasoningEffortSelection.TryParsePlanMode("none", out var effort));
        Assert.Equal(CopilotCodexReasoningEffort.None, effort);
    }

    [Fact]
    public void LocalCodexGptModelCanSelectAndSendExplicitReasoningEffort()
    {
        var profile = new CopilotProfileConfig
        {
            VendorType = CopilotVendorType.OpenAI,
            ProviderType = CopilotProviderType.LocalCodex,
            Model = "gpt-5.6-sol",
            ReasoningMode = CopilotReasoningMode.XHigh,
        };

        Assert.True(CopilotReasoningCapabilities.HasConfigurableReasoning(profile));
        Assert.Equal(CopilotReasoningMode.XHigh, CopilotReasoningCapabilities.GetEffectiveMode(profile));
        Assert.Contains(
            CopilotReasoningCapabilities.GetOptions(profile),
            option => option.Mode == CopilotReasoningMode.XHigh && option.IsSelected);
        Assert.Equal(
            Microsoft.Extensions.AI.ReasoningEffort.ExtraHigh,
            CopilotMicrosoftAgentFrameworkRuntime.BuildReasoningOptions(profile)?.Effort);
    }
}
