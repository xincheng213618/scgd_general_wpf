#pragma warning disable CS0618 // These types intentionally remain readable in existing flows.
using FlowEngineLib.Base;
using FlowEngineLib.Node.Algorithm;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;

namespace ColorVision.UI.Tests;

public sealed class LegacyThirdPartyAlgorithmNodeTests
{
    [Theory]
    [InlineData(typeof(TPAlgorithmNode))]
    [InlineData(typeof(TPAlgorithm2Node))]
    public void ExistingCanvasRetainsThirdPartyNodeAndRemoteParameters(Type nodeType)
    {
        StaTest.Run(() =>
        {
            using var original = new CVNodeContainer();
            var node = Assert.IsAssignableFrom<CVBaseServerNode>(Activator.CreateInstance(nodeType));
            node.Create();
            node.DeviceCode = "DEV.TPAlgorithms.Legacy";
            nodeType.GetProperty("Operator")!.SetValue(node, "legacy-operator");
            nodeType.GetProperty("TempName")!.SetValue(node, "legacy-template");
            original.Nodes.Add(node);

            using var restored = new CVNodeContainer();
            Assert.True(restored.LoadAssembly(nodeType.Assembly));
            restored.LoadCanvas(original.GetCanvasData());

            var restoredNode = Assert.IsAssignableFrom<CVBaseServerNode>(Assert.Single(restored.Nodes.Cast<STNode>()));
            Assert.IsType(nodeType, restoredNode);
            Assert.Equal(node.DeviceCode, restoredNode.DeviceCode);
            Assert.Equal("legacy-operator", nodeType.GetProperty("Operator")!.GetValue(restoredNode));
            Assert.Equal("legacy-template", nodeType.GetProperty("TempName")!.GetValue(restoredNode));
        });
    }
}
