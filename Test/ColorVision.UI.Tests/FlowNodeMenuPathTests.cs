using ColorVision.Engine.Templates.Flow;

namespace ColorVision.UI.Tests;

public class FlowNodeMenuPathTests
{
    [Fact]
    public void LocalizeNodeMenuPathRemovesFlowEngineLibAssemblyPrefix()
    {
        Assert.Equal("06 PG", STNodeEditorHelper.LocalizeNodeMenuPath("FlowEngineLib/06 PG"));
    }

    [Fact]
    public void LocalizeNodeMenuPathRemovesColorVisionEngineAssemblyPrefix()
    {
        Assert.Equal(
            ColorVision.Engine.Properties.Resources.Flow_CustomNodes,
            STNodeEditorHelper.LocalizeNodeMenuPath("ColorVision.Engine/Flow_CustomNodes"));
    }

    [Fact]
    public void LocalizeNodeMenuPathPreservesThirdPartyAssemblyPrefix()
    {
        Assert.Equal("ThirdParty.Nodes/Custom", STNodeEditorHelper.LocalizeNodeMenuPath("ThirdParty.Nodes/Custom"));
    }
}
