using ColorVision.Engine.FlowProcessing.Editor;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public class FlowNodeMenuPathTests
{
    [Fact]
    public void ImportTemplateAsModuleMenuItemInvokesSharedImportAction()
    {
        StaTest.Run(() =>
        {
            int invocationCount = 0;
            MenuItem item = FlowNodeContextMenuService.CreateImportModuleMenuItem(() => invocationCount++);

            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));

            Assert.Equal(ColorVision.Engine.Properties.Resources.Flow_ImportTemplateAsModule, item.Header);
            Assert.Empty(item.Items);
            Assert.Equal(1, invocationCount);
        });
    }

    [Fact]
    public void LocalizeNodeMenuPathRemovesFlowEngineLibAssemblyPrefix()
    {
        Assert.Equal("06 PG", FlowNodeContextMenuService.LocalizeNodeMenuPath("FlowEngineLib/06 PG"));
    }

    [Fact]
    public void LocalizeNodeMenuPathRemovesColorVisionEngineAssemblyPrefix()
    {
        Assert.Equal(
            ColorVision.Engine.Properties.Resources.Flow_CustomNodes,
            FlowNodeContextMenuService.LocalizeNodeMenuPath("ColorVision.Engine/Flow_CustomNodes"));
    }

    [Fact]
    public void LocalizeNodeMenuPathPreservesThirdPartyAssemblyPrefix()
    {
        Assert.Equal("ThirdParty.Nodes/Custom", FlowNodeContextMenuService.LocalizeNodeMenuPath("ThirdParty.Nodes/Custom"));
    }
}
