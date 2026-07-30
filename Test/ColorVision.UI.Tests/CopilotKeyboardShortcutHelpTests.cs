using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotKeyboardShortcutHelpTests
{
    [Fact]
    public void ShortcutsCommandIsArgumentFreeAndAvailableDuringAgentRuns()
    {
        var invocation = CopilotLocalCommandCatalog.Parse("/shortcuts");

        Assert.NotNull(invocation);
        Assert.Equal(CopilotLocalCommandKind.Shortcuts, invocation.Command.Kind);
        Assert.True(invocation.Command.AvailableWhileAgentRuns);
        Assert.Null(CopilotLocalCommandCatalog.Parse("/shortcuts composer"));
        Assert.Contains(CopilotLocalCommandCatalog.Suggest("/"), command => command.Name == "/shortcuts");
    }

    [Fact]
    public void ShortcutEntriesAreUniqueWithinEachFocusScope()
    {
        Assert.NotEmpty(CopilotKeyboardShortcutHelp.Entries);
        Assert.All(CopilotKeyboardShortcutHelp.Entries, entry =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Scope));
            Assert.False(string.IsNullOrWhiteSpace(entry.Keys));
            Assert.False(string.IsNullOrWhiteSpace(entry.Action));
        });
        Assert.Equal(
            CopilotKeyboardShortcutHelp.Entries.Count,
            CopilotKeyboardShortcutHelp.Entries
                .Select(entry => $"{entry.Scope}\0{entry.Keys}")
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    [Fact]
    public void ReportExplainsFocusSpecificBindingsAndSafety()
    {
        var report = CopilotKeyboardShortcutHelp.Format();

        Assert.Contains("全局", report);
        Assert.Contains("输入框", report);
        Assert.Contains("侧栏搜索", report);
        Assert.Contains("Ctrl+/ — 打开这份快捷键速查", report);
        Assert.Contains("Ctrl+R — 搜索当前会话的可见历史请求", report);
        Assert.Contains("Ctrl+R — 重命名高亮会话，不切换", report);
        Assert.Contains("不会绕过权限或确认", report);
        Assert.Contains("输入 /help 查看 Slash 命令目录", report);
    }
}
