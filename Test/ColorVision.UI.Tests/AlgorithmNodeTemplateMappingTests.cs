using System.Reflection;
using System.ComponentModel;
using ColorVision.Engine.PropertyEditor;
using FlowEngineLib.Algorithm;

namespace ColorVision.UI.Tests;

public class AlgorithmNodeTemplateMappingTests
{
    [Fact]
    public void ArvrPoiTemplateUsesInlinePoiTemplateEditor()
    {
        Assert.Equal(
            typeof(PoiTemplatePropertiesEditor),
            typeof(AlgorithmARVRNode).GetProperty(nameof(AlgorithmARVRNode.POITempName))!.GetCustomAttribute<PropertyEditorTypeAttribute>()?.EditorType);
    }
}
