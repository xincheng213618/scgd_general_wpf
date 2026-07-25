#pragma warning disable CA1707
using ColorVision.Engine.FlowProcessing.Editor;
using FlowEngineLib.Base;

namespace ColorVision.UI.Tests;

public class FlowExecutionNodeResolutionTests
{
    [Fact]
    public void ResolveExecutionNode_UsesPreferredRuntimeNodeWhenNamesAreDuplicated()
    {
        CVCommonNode first = CreateNode("PG.GECS", "SVR.PG.Default");
        CVCommonNode failed = CreateNode("PG.GECS", "SVR.PG.Default");

        CVCommonNode? resolved = STNodeEditorHelper.ResolveExecutionNode(
            new[] { first, failed },
            "PG.GECS.SVR.PG.Default",
            failed);

        Assert.Same(failed, resolved);
    }

    [Fact]
    public void ResolveExecutionNode_DoesNotGuessWhenExecutionNameIsAmbiguous()
    {
        CVCommonNode first = CreateNode("PG.GECS", "SVR.PG.Default");
        CVCommonNode second = CreateNode("PG.GECS", "SVR.PG.Default");

        CVCommonNode? resolved = STNodeEditorHelper.ResolveExecutionNode(
            new[] { first, second },
            "PG.GECS.SVR.PG.Default");

        Assert.Null(resolved);
    }

    [Fact]
    public void ResolveExecutionNode_FindsUniqueNodeByStableId()
    {
        CVCommonNode node = CreateNode("PG.GECS", "SVR.PG.Default");

        CVCommonNode? resolved = STNodeEditorHelper.ResolveExecutionNode(
            new[] { node },
            node.NodeID);

        Assert.Same(node, resolved);
    }

    [Fact]
    public void ResolveExecutionNode_UsesStableIdWhenNamesAreDuplicated()
    {
        CVCommonNode first = CreateNode("PG.GECS", "SVR.PG.Default");
        CVCommonNode failed = CreateNode("PG.GECS", "SVR.PG.Default");

        CVCommonNode? resolved = STNodeEditorHelper.ResolveExecutionNode(
            new[] { first, failed },
            failed.NodeID);

        Assert.Same(failed, resolved);
    }

    [Fact]
    public void FailedAction_CarriesStableNodeIdToFlowCompletionData()
    {
        var action = new CVBaseCFC("serial", ActionTypeEnum.Start);

        action.Failed("failed", "PG.GECS.SVR.PG.Default", DateTime.Now, "node-id");

        Assert.Equal("node-id", action.Data["ErrorNodeId"]);
    }

    private static CVCommonNode CreateNode(string title, string nodeName)
    {
        return new CVCommonNode(title, "SVR", nodeName, string.Empty)
        {
            Title = title
        };
    }
}
