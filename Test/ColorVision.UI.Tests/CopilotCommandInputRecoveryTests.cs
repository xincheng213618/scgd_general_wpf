using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotCommandInputRecoveryTests
{
    private static readonly CopilotAgentSkillCatalogItem[] Skills =
    [
        new("deploy-check", "Verify a deployment"),
        new("review-flow", "Review a flow"),
    ];

    [Theory]
    [InlineData("")]
    [InlineData("检查当前状态")]
    [InlineData("/")]
    [InlineData("$")]
    [InlineData("/status")]
    [InlineData("/stats 30")]
    public void NormalInputAndValidFixedCommandsDoNotNeedRecovery(string input)
    {
        Assert.False(CopilotCommandInputRecoveryResolver.TryResolve(
            input,
            Skills,
            out _));
    }

    [Fact]
    public void FixedCommandWithUnsupportedArgumentsReportsUsageInsteadOfSending()
    {
        Assert.True(CopilotCommandInputRecoveryResolver.TryResolve(
            "/status verbose",
            Skills,
            out var recovery));

        Assert.Equal("/status · 用法", recovery.Title);
        Assert.Contains("未发送给模型", recovery.Message);
        Assert.Contains("用法：/status", recovery.Message);
    }

    [Theory]
    [InlineData("/deploy-check now")]
    [InlineData("/DEPLOY-CHECK")]
    [InlineData("$deploy-check now")]
    [InlineData("$DEPLOY-CHECK")]
    public void ExactDynamicSkillInvocationsRemainModelInput(string input)
    {
        Assert.False(CopilotCommandInputRecoveryResolver.TryResolve(
            input,
            Skills,
            out _));
    }

    [Theory]
    [InlineData("/statsu", "/stats")]
    [InlineData("/stpo", "/stop")]
    public void MistypedFixedCommandSuggestsTheClosestCommand(
        string input,
        string expectedSuggestion)
    {
        Assert.True(CopilotCommandInputRecoveryResolver.TryResolve(
            input,
            Skills,
            out var recovery));

        Assert.Equal(input + " · 未找到", recovery.Title);
        Assert.Contains("未发送给模型", recovery.Message);
        Assert.Contains("你是否想输入：" + expectedSuggestion, recovery.Message);
    }

    [Theory]
    [InlineData("/deploy-chek", "/deploy-check")]
    [InlineData("$deploy-chek", "$deploy-check")]
    public void MistypedSkillUsesTheOriginalInvocationMarker(
        string input,
        string expectedSuggestion)
    {
        Assert.True(CopilotCommandInputRecoveryResolver.TryResolve(
            input,
            Skills,
            out var recovery));

        Assert.Contains(expectedSuggestion, recovery.Message);
        Assert.Contains("/skills", recovery.Message);
    }

    [Fact]
    public void UnknownCommandWithoutACloseMatchRemainsAvailableToModelOrUnlistedSkill()
    {
        Assert.False(CopilotCommandInputRecoveryResolver.TryResolve(
            "/definitely-unrelated-command argument",
            Skills,
            out _));
    }

    [Fact]
    public void LongUnknownTokenSkipsDistanceWork()
    {
        var input = "/" + new string('x', 512);

        Assert.False(CopilotCommandInputRecoveryResolver.TryResolve(
            input,
            Skills,
            out _));
    }
}
