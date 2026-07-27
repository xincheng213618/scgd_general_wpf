using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;
using System.Reflection;

namespace ColorVision.UI.Tests;

public class FlowNodePropertyMetadataTests
{
    [Fact]
    public void CVCommonNode_ZIndex_RemainsVisibleAndEditable()
    {
        var property = typeof(CVCommonNode).GetProperty(nameof(CVCommonNode.ZIndex));
        var attribute = property?.GetCustomAttribute<STNodePropertyAttribute>(inherit: true);

        Assert.NotNull(attribute);
        Assert.True(attribute.IsEditEnable);
        Assert.False(attribute.IsHide);
        Assert.False(attribute.IsReadOnly);
    }
}
