using ColorVision.Copilot;
using ColorVision.UI.Menus;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotChatViewModelContractTests
{
    [Fact]
    public void ShortcutHelpExposesTheActivityViewGesture()
    {
        var shortcut = Assert.Single(
            CopilotKeyboardShortcutHelp.Entries,
            entry => string.Equals(entry.Keys, "Ctrl+Alt+U", StringComparison.Ordinal));

        Assert.Equal("全局", shortcut.Scope);
        Assert.Contains("活动视图", shortcut.Action, StringComparison.Ordinal);
    }

    [Fact]
    public void PanelServiceRegistersItsSingletonInstance()
    {
        Assert.Equal("CopilotChatPanel", CopilotPanelService.PanelId);
        var service = CopilotPanelService.GetInstance();
        Assert.Same(service, CopilotPanelService.GetInstance());
        Assert.Same(service, CopilotServiceRegistry.Current);
    }

    [Fact]
    public void MainStatusBarExposesChatAssistantShortcut()
    {
        StaTest.Run(() =>
        {
            var item = Assert.Single(new CopilotStatusBarProvider().GetStatusBarIconMetadata());
            Assert.Equal("CopilotAgent", item.Id);
            Assert.Equal(CopilotUiText.CopilotPanelTitle, item.Name);
            Assert.Equal(MenuItemConstants.MainWindowTarget, item.TargetName);
            Assert.Equal(StatusBarAlignment.Right, item.Alignment);
            Assert.True(item.IsVisible);
            Assert.NotNull(item.Command);
            Assert.NotNull(item.IconContent);
        });
    }
}
