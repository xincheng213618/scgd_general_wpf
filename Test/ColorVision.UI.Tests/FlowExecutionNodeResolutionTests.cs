#pragma warning disable CA1707
using ColorVision.Engine.FlowProcessing.Editor;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;
using System.Windows;

namespace ColorVision.UI.Tests;

public class FlowExecutionNodeResolutionTests
{
    [Fact]
    public void ResolveExecutionNode_UsesPreferredRuntimeNodeWhenNamesAreDuplicated()
    {
        CVCommonNode first = CreateNode("PG.GECS", "SVR.PG.Default");
        CVCommonNode failed = CreateNode("PG.GECS", "SVR.PG.Default");

        CVCommonNode? resolved = FlowExecutionNavigator.ResolveExecutionNode(
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

        CVCommonNode? resolved = FlowExecutionNavigator.ResolveExecutionNode(
            new[] { first, second },
            "PG.GECS.SVR.PG.Default");

        Assert.Null(resolved);
    }

    [Fact]
    public void ResolveExecutionNode_FindsUniqueNodeByStableId()
    {
        CVCommonNode node = CreateNode("PG.GECS", "SVR.PG.Default");

        CVCommonNode? resolved = FlowExecutionNavigator.ResolveExecutionNode(
            new[] { node },
            node.NodeID);

        Assert.Same(node, resolved);
    }

    [Fact]
    public void ResolveExecutionNode_UsesStableIdWhenNamesAreDuplicated()
    {
        CVCommonNode first = CreateNode("PG.GECS", "SVR.PG.Default");
        CVCommonNode failed = CreateNode("PG.GECS", "SVR.PG.Default");

        CVCommonNode? resolved = FlowExecutionNavigator.ResolveExecutionNode(
            new[] { first, failed },
            failed.NodeID);

        Assert.Same(failed, resolved);
    }

    [Fact]
    public void StableIdentityCannotBeOverriddenByAnotherNodesNameOrPreferredInstance()
    {
        CVCommonNode target = CreateNode("Camera", "Target");
        CVCommonNode misleading = CreateNode("Camera", target.NodeID);

        Assert.Same(target, FlowExecutionNavigator.ResolveExecutionNode([target, misleading], target.NodeID, misleading));
        Assert.False(FlowExecutionNavigator.IsExecutionNodeNameMatch(misleading, target.NodeID));
        Assert.Null(FlowExecutionNavigator.ResolveExecutionNode([misleading], target.NodeID, misleading));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HistoricalIdentityMissingFromCanvasDoesNotFocusAReplacementWithTheSameName(bool malformedId)
    {
        WpfTestHost.Invoke(() =>
        {
            using var editor = new STNodeEditor { LimitCanvasToContentBounds = false };
            CVCommonNode current = CreateNode("Camera", "CameraA");
            CVCommonNode replacement = CreateNode("Algorithm", "OriginalName");
            current.Create(); replacement.Create();
            replacement.Location = new System.Drawing.Point(1300, 900);
            editor.Nodes.AddRange([current, replacement]);
            editor.SetActiveNode(current);
            editor.MoveCanvas(35, 45, false, CanvasMoveArgs.All);
            float offsetX = editor.CanvasOffsetX;
            float offsetY = editor.CanvasOffsetY;

            var navigator = new FlowExecutionNavigator(editor);
            string removedId = malformedId ? replacement.NodeName : Guid.NewGuid().ToString();
            Assert.False(navigator.TryFocusExecutionNode(removedId, "Algorithm.OriginalName"));
            Assert.Same(current, editor.ActiveNode);
            Assert.Equal([current], editor.GetSelectedNode());
            Assert.Equal(offsetX, editor.CanvasOffsetX);
            Assert.Equal(offsetY, editor.CanvasOffsetY);
        });
    }

    [Fact]
    public void NavigationUsesStableIdentityAfterRenameAndOnlyUniqueNamesForLegacyRecords()
    {
        WpfTestHost.Invoke(() =>
        {
            using var editor = new STNodeEditor { LimitCanvasToContentBounds = false, EnableHistory = true };
            editor.Measure(new Size(800, 600));
            editor.Arrange(new Rect(0, 0, 800, 600));
            CVCommonNode first = CreateNode("Camera", "Duplicate");
            CVCommonNode target = CreateNode("Camera", "Renamed");
            CVCommonNode second = CreateNode("Camera", "Duplicate");
            foreach (CVCommonNode node in new[] { first, target, second }) { node.Create(); editor.Nodes.Add(node); }
            target.Location = new System.Drawing.Point(1300, 900);
            editor.SetActiveNode(first);
            second.SetSelected(true, false);
            editor.ScaleCanvas(0.6f, 0, 0);
            editor.ClearHistory();
            var navigator = new FlowExecutionNavigator(editor);

            Assert.True(navigator.TryFocusExecutionNode(target.NodeID, "Camera.OldName"));
            Assert.Same(target, editor.ActiveNode);
            Assert.Equal([target], editor.GetSelectedNode());
            Assert.Equal(editor.ClientSize.Width / 2f - (target.Left + target.Width / 2f) * editor.CanvasScale, editor.CanvasOffsetX);
            Assert.Equal(editor.ClientSize.Height / 2f - (target.Top + target.Height / 2f) * editor.CanvasScale, editor.CanvasOffsetY);
            Assert.False(editor.IsModified);

            Assert.False(navigator.TryFocusExecutionNode(null, "Camera.Duplicate"));
            Assert.Same(target, editor.ActiveNode);
            Assert.True(navigator.TryFocusExecutionNode(null, "Camera.Renamed"));
            Assert.Same(target, editor.ActiveNode);
            Assert.False(editor.IsModified);
        });
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
        return new CVDeviceNode(title, "SVR", nodeName, string.Empty)
        {
            Title = title
        };
    }
}
