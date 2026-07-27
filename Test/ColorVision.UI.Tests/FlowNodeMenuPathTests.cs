using ColorVision.Engine.FlowProcessing.Editor;

namespace ColorVision.UI.Tests;

public class FlowNodeMenuPathTests
{
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
