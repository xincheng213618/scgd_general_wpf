using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotResponseRetryCommandTests
{
    [Fact]
    public void RetryCommandAcceptsSavedOrRefreshedContextModes()
    {
        var saved = CopilotLocalCommandCatalog.Parse("/retry");
        var refreshed = CopilotLocalCommandCatalog.Parse("/retry refresh");

        Assert.NotNull(saved);
        Assert.Equal(CopilotLocalCommandKind.RetryResponse, saved.Command.Kind);
        Assert.False(saved.Command.AvailableWhileAgentRuns);
        Assert.NotNull(refreshed);
        Assert.Equal("refresh", refreshed.Arguments);
        Assert.Contains(
            CopilotLocalCommandCatalog.Suggest("/retry "),
            suggestion => suggestion.CompletionText == "/retry refresh");
    }

    [Theory]
    [InlineData("", true, false)]
    [InlineData("  REFRESH  ", true, true)]
    [InlineData("fresh", false, false)]
    [InlineData("2", false, false)]
    public void RetryOptionParserRejectsUnknownModes(
        string arguments,
        bool expectedValid,
        bool expectedRefresh)
    {
        var valid = CopilotResponseRetryCommand.TryParse(
            arguments,
            out var refreshExternalContext);

        Assert.Equal(expectedValid, valid);
        Assert.Equal(expectedRefresh, refreshExternalContext);
    }

    [Fact]
    public void RetryHelpExplainsLatestTurnAndRefreshBehavior()
    {
        var help = CopilotLocalCommandHelp.Format("retry");

        Assert.StartsWith("/retry [refresh]", help, StringComparison.Ordinal);
        Assert.Contains("当前会话最后一轮", help, StringComparison.Ordinal);
        Assert.Contains("重新读取附件与网页上下文", help, StringComparison.Ordinal);
        Assert.Contains("Agent 运行中：当前任务结束后执行", help, StringComparison.Ordinal);
    }
}
