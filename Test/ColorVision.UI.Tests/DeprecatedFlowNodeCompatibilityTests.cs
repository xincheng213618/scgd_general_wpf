#pragma warning disable CA1707
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace ColorVision.UI.Tests;

public class DeprecatedFlowNodeCompatibilityTests
{
    [Theory]
    [InlineData("FlowEngineLib.Node.Algorithm.AlgorithmCaliNode")]
    [InlineData("FlowEngineLib.Node.OLED.AlgorithmCompoundImgNode")]
    [InlineData("FlowEngineLib.MQTTCustomPublishNode")]
    [InlineData("FlowEngineLib.MQTTCustomSubscribeNode")]
    [InlineData("FlowEngineLib.MQTT.MQTTPublishHub")]
    [InlineData("FlowEngineLib.MQTT.MQTTSubscribeHub")]
    [InlineData("FlowEngineLib.Node.Algorithm.AlgComplianceMathNode")]
    [InlineData("FlowEngineLib.Node.Algorithm.AlgComplianceJudgmentNode")]
    [InlineData("FlowEngineLib.Node.Algorithm.AlgComplianceContrastNode")]
    [InlineData("FlowEngineLib.Node.Algorithm.TPAlgorithmNode")]
    [InlineData("FlowEngineLib.Node.Algorithm.TPAlgorithm2Node")]
    [InlineData("FlowEngineLib.Node.Camera.CalibrationROINode")]
    [InlineData("FlowEngineLib.Node.Camera.CameraROINode")]
    public void DeprecatedNode_IsExcludedFromCatalogAndSavedCanvasStillLoads(string typeName)
    {
        var nodeType = typeof(FlowEngineLib.Base.CVCommonNode).Assembly.GetType(typeName, throwOnError: true)!;
        Assert.NotNull(nodeType.GetCustomAttribute<ObsoleteAttribute>(inherit: false));

        using var nodeTree = new STNodeTreeView();
        Assert.False(nodeTree.AddNode(nodeType));

        var visibilityCheck = typeof(ColorVision.Engine.Templates.Flow.FlowEngineManager).GetMethod("IsVisibleFlowNodeType", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(visibilityCheck);
        Assert.False(Assert.IsType<bool>(visibilityCheck.Invoke(null, [nodeType])));

        var original = Assert.IsAssignableFrom<STNode>(Activator.CreateInstance(nodeType));
        original.Create();

        var container = new CVNodeContainer();
        Assert.True(container.LoadAssembly(nodeType.Assembly));
        container.LoadCanvas(CreateCanvasData(original.GetSaveData()));

        var restored = Assert.Single(container.Nodes.Cast<STNode>());
        Assert.Equal(nodeType, restored.GetType());
    }

    private static byte[] CreateCanvasData(byte[] nodeData)
    {
        using var stream = new MemoryStream();
        stream.Write(STNodeConstant.NodeFlag);
        stream.WriteByte(STNodeConstant.Version);

        using (var gzip = new GZipStream(stream, CompressionMode.Compress, leaveOpen: true))
        {
            gzip.Write(BitConverter.GetBytes(0f));
            gzip.Write(BitConverter.GetBytes(0f));
            gzip.Write(BitConverter.GetBytes(1f));
            gzip.Write(BitConverter.GetBytes(1));
            gzip.Write(BitConverter.GetBytes(nodeData.Length));
            gzip.Write(nodeData);
            gzip.Write(BitConverter.GetBytes(0));
        }

        return stream.ToArray();
    }
}

public class DeprecatedMenuCompatibilityTests
{
    [Theory]
    [InlineData("ColorVision.Engine.Templates.Validate.ExportComply")]
    [InlineData("ColorVision.Engine.Templates.Validate.ExportComplyPoint")]
    [InlineData("ColorVision.Engine.Templates.Validate.ExportComplyPointList")]
    [InlineData("ColorVision.Engine.Templates.Validate.ExportDicComply")]
    [InlineData("ColorVision.Engine.Templates.Validate.MenuItemProviderSensor")]
    [InlineData("ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates.MenuThirdPartyAlgorithms")]
    [InlineData("ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates.MenuItemProviderSensor")]
    public void DeprecatedMenuType_IsExcludedFromMenuDiscovery(string typeName)
    {
        var menuType = typeof(ColorVision.Engine.Templates.Validate.TemplateComplyParam).Assembly.GetType(typeName, throwOnError: true)!;
        Assert.NotNull(menuType.GetCustomAttribute<ObsoleteAttribute>(inherit: false));

        var candidateCheck = typeof(ColorVision.UI.Menus.MenuManager).GetMethod("IsConcreteMenuCandidate", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(candidateCheck);
        Assert.False(Assert.IsType<bool>(candidateCheck.Invoke(null, [menuType])));
    }
}
