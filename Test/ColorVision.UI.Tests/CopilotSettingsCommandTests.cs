using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotSettingsCommandTests
{
    [Theory]
    [InlineData("/settings", "")]
    [InlineData("/config agent", "agent")]
    [InlineData("/preferences mcp", "mcp")]
    [InlineData("/prefs sync", "sync")]
    public void FamiliarAliasesRouteToIdleOnlySettings(
        string input,
        string expectedArguments)
    {
        var invocation = CopilotLocalCommandCatalog.Parse(input);

        Assert.NotNull(invocation);
        Assert.Equal(CopilotLocalCommandKind.Settings, invocation.Command.Kind);
        Assert.Equal("/settings", invocation.Command.Name);
        Assert.False(invocation.Command.AvailableWhileAgentRuns);
        Assert.Equal(["models", "agent", "mcp", "sync"], invocation.Command.Arguments!.Select(item => item.Value));
        Assert.Equal(expectedArguments, invocation.Arguments);
    }

    [Theory]
    [InlineData("", CopilotSettingsPage.Models)]
    [InlineData("MODEL", CopilotSettingsPage.Models)]
    [InlineData("models", CopilotSettingsPage.Models)]
    [InlineData("agent", CopilotSettingsPage.Agent)]
    [InlineData("MCP", CopilotSettingsPage.Mcp)]
    [InlineData("sync", CopilotSettingsPage.BackendSync)]
    [InlineData("backend", CopilotSettingsPage.BackendSync)]
    public void ResolverSelectsTheRequestedSettingsPage(
        string arguments,
        CopilotSettingsPage expectedPage)
    {
        Assert.True(CopilotSettingsCommand.TryResolvePage(arguments, out var page));
        Assert.Equal(expectedPage, page);
    }

    [Fact]
    public void InvalidPageDoesNotOpenAnUnexpectedSettingsTab()
    {
        Assert.False(CopilotSettingsCommand.TryResolvePage("unknown", out var page));
        Assert.Equal(CopilotSettingsPage.Models, page);
        Assert.Contains("/settings [models|agent|mcp|sync]", CopilotSettingsCommand.Usage);
    }
}
