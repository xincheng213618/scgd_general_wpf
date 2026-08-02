using ColorVision.Engine.FlowProcessing.Editor;
using FlowEngineLib.Base;

namespace ColorVision.UI.Tests;

public class FlowNodePropertyMetadataProviderTests
{
    [Fact]
    public void ContinueOnFail_IsAnAdvancedFlowProperty()
    {
        var property = typeof(CVBaseServerNode).GetProperty(nameof(CVBaseServerNode.ContinueOnFail));

        Assert.NotNull(property);
        Assert.True(FlowNodePropertyMetadataProvider.AdvancedOptions.IsAdvancedProperty(property));
    }
}
