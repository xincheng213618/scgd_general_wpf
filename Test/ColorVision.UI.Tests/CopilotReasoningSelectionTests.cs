using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotReasoningSelectionTests
{
    [Fact]
    public void ReasoningAndEffortCommandsShareTheProfileReasoningPath()
    {
        var reasoning = CopilotLocalCommandCatalog.Parse("/reasoning high");
        var effort = CopilotLocalCommandCatalog.Parse("/effort max");

        Assert.NotNull(reasoning);
        Assert.Equal(CopilotLocalCommandKind.SelectReasoning, reasoning.Command.Kind);
        Assert.Equal("high", reasoning.Arguments);
        Assert.False(reasoning.Command.AvailableWhileAgentRuns);
        Assert.NotNull(effort);
        Assert.Equal(CopilotLocalCommandKind.SelectReasoning, effort.Command.Kind);
        Assert.Equal("max", effort.Arguments);
        Assert.False(effort.Command.AvailableWhileAgentRuns);
        Assert.Contains(CopilotLocalCommandCatalog.Suggest("/"), command => command.Name == "/reasoning");
        Assert.Contains(CopilotLocalCommandCatalog.Suggest("/"), command => command.Name == "/effort");

        CopilotAgentSkillCatalogItem[] skills =
        [
            new("skill-one", "One"),
            new("skill-two", "Two"),
            new("skill-three", "Three"),
            new("skill-four", "Four"),
            new("skill-five", "Five"),
            new("skill-six", "Six"),
            new("skill-seven", "Seven"),
            new("skill-eight", "Eight"),
        ];
        var bareSlash = CopilotLocalCommandCatalog.Suggest("/", skills);
        Assert.Equal(36, bareSlash.Count);
        Assert.Contains(bareSlash, command => command.Name == "/skill-one");
        Assert.Contains(bareSlash, command => command.Name == "/skill-six");
        Assert.DoesNotContain(bareSlash, command => command.Name == "/skill-seven");
    }

    [Theory]
    [InlineData("auto", CopilotReasoningMode.Default)]
    [InlineData("DEFAULT", CopilotReasoningMode.Default)]
    [InlineData("关闭", CopilotReasoningMode.Disabled)]
    [InlineData("off", CopilotReasoningMode.Disabled)]
    [InlineData("none", CopilotReasoningMode.Disabled)]
    [InlineData("high", CopilotReasoningMode.High)]
    [InlineData("最高", CopilotReasoningMode.Max)]
    [InlineData("max", CopilotReasoningMode.Max)]
    public void DeepSeekAcceptsOnlyItsOfferedReasoningLevels(
        string query,
        CopilotReasoningMode expected)
    {
        var profile = CreateProfile(CopilotVendorType.DeepSeek);

        var option = CopilotReasoningCapabilities.FindCommandOption(profile, query);

        Assert.NotNull(option);
        Assert.Equal(expected, option.Mode);
        Assert.Null(CopilotReasoningCapabilities.FindCommandOption(profile, "on"));
        Assert.Null(CopilotReasoningCapabilities.FindCommandOption(profile, "xhigh"));
        Assert.Equal(
            "auto（默认）、off（关闭）、high（高）、max（最高）",
            CopilotReasoningCapabilities.GetCommandOptionSummary(profile));
    }

    [Theory]
    [InlineData("auto", CopilotReasoningMode.Default)]
    [InlineData("关闭", CopilotReasoningMode.Disabled)]
    [InlineData("disabled", CopilotReasoningMode.Disabled)]
    [InlineData("on", CopilotReasoningMode.Enabled)]
    [InlineData("ENABLED", CopilotReasoningMode.Enabled)]
    [InlineData("开启", CopilotReasoningMode.Enabled)]
    public void XiaomiAcceptsOnlyItsOfferedThinkingModes(
        string query,
        CopilotReasoningMode expected)
    {
        var profile = CreateProfile(CopilotVendorType.Xiaomi);

        var option = CopilotReasoningCapabilities.FindCommandOption(profile, query);

        Assert.NotNull(option);
        Assert.Equal(expected, option.Mode);
        Assert.Null(CopilotReasoningCapabilities.FindCommandOption(profile, "high"));
        Assert.Null(CopilotReasoningCapabilities.FindCommandOption(profile, "max"));
        Assert.Equal(
            "auto（默认）、off（关闭）、on（开启）",
            CopilotReasoningCapabilities.GetCommandOptionSummary(profile));
    }

    [Fact]
    public void ProvidersWithoutDeclaredReasoningOptionsStayOnTheirDefault()
    {
        var profile = CreateProfile(CopilotVendorType.OpenAI);

        Assert.False(CopilotReasoningCapabilities.HasConfigurableReasoning(profile));
        Assert.Null(CopilotReasoningCapabilities.FindCommandOption(profile, "high"));
        Assert.Equal("auto（默认）", CopilotReasoningCapabilities.GetCommandOptionSummary(profile));
    }

    [Fact]
    public void ResolvedModesUseTheExistingProviderRequestMapper()
    {
        var deepSeek = CreateProfile(CopilotVendorType.DeepSeek);
        deepSeek.ProviderType = CopilotProviderType.OpenAICompatible;
        deepSeek.ReasoningMode = CopilotReasoningCapabilities.FindCommandOption(deepSeek, "high")!.Mode;
        var deepSeekPayload = new Dictionary<string, object?>();

        var xiaomi = CreateProfile(CopilotVendorType.Xiaomi);
        xiaomi.ReasoningMode = CopilotReasoningCapabilities.FindCommandOption(xiaomi, "on")!.Mode;
        var xiaomiPayload = new Dictionary<string, object?>();

        CopilotReasoningRequestMapper.Apply(deepSeek, deepSeekPayload);
        CopilotReasoningRequestMapper.Apply(xiaomi, xiaomiPayload);

        Assert.Equal("high", deepSeekPayload["reasoning_effort"]);
        Assert.Equal(
            "enabled",
            Assert.IsType<Dictionary<string, object?>>(deepSeekPayload["thinking"])["type"]);
        Assert.Equal(
            "enabled",
            Assert.IsType<Dictionary<string, object?>>(xiaomiPayload["thinking"])["type"]);
    }

    private static CopilotProfileConfig CreateProfile(CopilotVendorType vendorType)
    {
        return new CopilotProfileConfig
        {
            VendorType = vendorType,
            Name = vendorType.ToString(),
            Model = "reasoning-model",
        };
    }
}
