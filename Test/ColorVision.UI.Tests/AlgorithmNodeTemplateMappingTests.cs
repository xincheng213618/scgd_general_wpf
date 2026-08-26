using FlowEngineLib.Algorithm;
using FlowEngineLib.PropertyEditor;

namespace ColorVision.UI.Tests;

public class AlgorithmNodeTemplateMappingTests
{
    [Fact]
    public void ArvrPoiTemplateUsesInlinePoiTemplateEditor()
    {
        Assert.Equal(
            typeof(FlowPoiTemplateEditor),
            FlowNodePropertyEditorAttribute.Resolve(typeof(AlgorithmARVRNode), nameof(AlgorithmARVRNode.POITempName)));
    }
}
