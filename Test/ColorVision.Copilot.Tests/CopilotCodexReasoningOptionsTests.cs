using ColorVision.Copilot;
using System;

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
}
