using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Integration;
using ColorVision.UI;
using ST.Library.UI.NodeEditor;
using System.Reflection;

namespace ColorVision.UI.Tests;

public sealed class CopilotFlowDisconnectionTests
{
    [Fact]
    public void PreviewReadsTheExactEdgeWithoutEventsHistoryOrCanvasChanges()
    {
        WithGraph((host, editor, source, target, other) =>
        {
            var before = editor.GetCanvasData();
            var revision = host.CaptureCopilotFlowSnapshot().Revision;
            var events = 0;
            source.Output.DisConnecting += (_, _) => events++;
            source.Output.DisConnected += (_, _) => events++;
            target.Input.DisConnecting += (_, _) => events++;
            target.Input.DisConnected += (_, _) => events++;
            target.Input.DataTransfer += (_, _) => events++;
            editor.OptionDisConnected += (_, _) => events++;

            var edge = host.PreviewCopilotFlowDisconnection(source.Guid.ToString(), "out:0", target.Guid.ToString(), "in:0", revision);

            Assert.Equal(source.Guid.ToString(), edge.SourceNodeId);
            Assert.Equal(target.Guid.ToString(), edge.TargetNodeId);
            Assert.Equal("out:0", edge.SourcePortId);
            Assert.Equal("in:0", edge.TargetPortId);
            Assert.Equal(0, events);
            Assert.Equal(before, editor.GetCanvasData());
            Assert.Equal(revision, host.CaptureCopilotFlowSnapshot().Revision);
            Assert.Equal(2, editor.GetConnections().Length);
            Assert.False(editor.CanUndo);
            Assert.False(editor.CanRedo);
            Assert.False(editor.IsModified);
            Task.Run(() => Assert.Throws<InvalidOperationException>(() => host.PreviewCopilotFlowDisconnection(
                source.Guid.ToString(), "out:0", target.Guid.ToString(), "in:0", revision))).GetAwaiter().GetResult();
            Assert.Equal(0, events);
        });
    }

    [Fact]
    public void ApplyRemovesOnlyTheReviewedEdgeAndNativeUndoRedoPreserveItsSibling()
    {
        WithGraph((host, editor, source, target, other) =>
        {
            var nodes = editor.Nodes.Cast<STNode>().ToArray();
            var revision = host.CaptureCopilotFlowSnapshot().Revision;
            var result = host.DisconnectCopilotFlowNodes(source.Guid.ToString(), "out:0", target.Guid.ToString(), "in:0", revision);

            var remaining = Assert.Single(editor.GetConnections());
            Assert.Same(source.Output, remaining.Output);
            Assert.Same(other.Input, remaining.Input);
            Assert.DoesNotContain(target.Input, source.Output.ConnectedOption);
            Assert.Contains(other.Input, source.Output.ConnectedOption);
            Assert.NotEqual(revision, result.Revision);
            Assert.Equal(host.CaptureCopilotFlowSnapshot().Revision, result.Revision);
            Assert.Equal(other.Guid.ToString(), Assert.Single(result.Edges).TargetNodeId);
            Assert.True(editor.CanUndo);
            Assert.False(editor.CanRedo);
            Assert.True(editor.IsModified);

            editor.Undo();
            Assert.Equal(2, editor.GetConnections().Length);
            Assert.Contains(target.Input, source.Output.ConnectedOption);
            Assert.Contains(other.Input, source.Output.ConnectedOption);
            Assert.Equal(revision, host.CaptureCopilotFlowSnapshot().Revision);
            Assert.False(editor.CanUndo);
            Assert.True(editor.CanRedo);
            Assert.False(editor.IsModified);
            editor.Redo();
            Assert.Single(editor.GetConnections());
            Assert.DoesNotContain(target.Input, source.Output.ConnectedOption);
            Assert.Contains(other.Input, source.Output.ConnectedOption);
            Assert.Equal(result.Revision, host.CaptureCopilotFlowSnapshot().Revision);
            Assert.Equal(nodes, editor.Nodes.Cast<STNode>().ToArray());
        });
    }

    [Theory]
    [InlineData("stale")]
    [InlineData("read-only")]
    [InlineData("active-lifecycle")]
    public void ExistingMutationGuardsRejectPreviewAndApplyBeforeDisconnecting(string guard)
    {
        WithGraph((host, editor, source, target, other) =>
        {
            var before = editor.GetCanvasData();
            var revision = host.CaptureCopilotFlowSnapshot().Revision;
            var manager = (FlowEngineManager)typeof(FlowCopilotService).GetField("_manager", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(host)!;
            var session = (FlowExecutionSession)typeof(ViewFlow).GetField("_executionSession", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(manager.View)!;
            var lifecycle = (FlowRunLifecycleGate)typeof(FlowExecutionSession).GetField("_runLifecycle", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
            using var cancellation = new CancellationTokenSource();
            if (guard == "read-only") editor.EnableEdit = false;
            if (guard == "stale") revision = new string(revision[0] == 'a' ? 'b' : 'a', 64);
            if (guard == "active-lifecycle")
            {
                Assert.True(lifecycle.TryBegin("disconnect-guard-test", cancellation, false));
                Assert.True(manager.View.IsExecutionActive);
                Assert.False(manager.FlowControl.IsFlowRun);
            }
            try
            {
                Assert.Throws<InvalidOperationException>(() => host.PreviewCopilotFlowDisconnection(source.Guid.ToString(), "out:0", target.Guid.ToString(), "in:0", revision));
                Assert.Throws<InvalidOperationException>(() => host.DisconnectCopilotFlowNodes(source.Guid.ToString(), "out:0", target.Guid.ToString(), "in:0", revision));
                Assert.Equal(before, editor.GetCanvasData());
                Assert.Equal(2, editor.GetConnections().Length);
                Assert.False(editor.CanUndo);
                Assert.False(editor.IsModified);
            }
            finally
            {
                editor.EnableEdit = true;
                if (guard == "active-lifecycle") lifecycle.Complete("disconnect-guard-test");
            }
        });
    }

    [Theory]
    [InlineData("source-alias")]
    [InlineData("wrong-direction")]
    [InlineData("missing-edge")]
    [InlineData("canonical-source")]
    [InlineData("revision-shape")]
    public void ExactIdentityDirectionExistenceAndCanonicalPortGuardsLeaveTheGraphUnchanged(string mutation)
    {
        WithGraph((host, editor, source, target, other) =>
        {
            var before = editor.GetCanvasData();
            var sourceId = mutation == "source-alias" ? "legacy-node-alias" : mutation == "missing-edge" ? target.Guid.ToString() : source.Guid.ToString();
            var targetId = mutation == "missing-edge" ? other.Guid.ToString() : target.Guid.ToString();
            var sourcePort = mutation == "canonical-source" ? "out:00" : mutation == "wrong-direction" ? "in:0" : "out:0";
            var revision = mutation == "revision-shape" ? "legacy-revision" : host.CaptureCopilotFlowSnapshot().Revision;
            var expectedException = mutation == "revision-shape" ? typeof(ArgumentException) : typeof(InvalidOperationException);
            Assert.Throws(expectedException, () => host.PreviewCopilotFlowDisconnection(sourceId, sourcePort, targetId, "in:0", revision));
            Assert.Throws(expectedException, () => host.DisconnectCopilotFlowNodes(sourceId, sourcePort, targetId, "in:0", revision));
            Assert.Equal(before, editor.GetCanvasData());
            Assert.Equal(2, editor.GetConnections().Length);
            Assert.False(editor.CanUndo);
            Assert.False(editor.CanRedo);
            Assert.False(editor.IsModified);
        });
    }

    private static void WithGraph(Action<FlowCopilotService, STNodeEditor, PortNode, PortNode, PortNode> action)
    {
        FlowExecutionEditingTests.WithCopilotGraph((host, editor) =>
        {
            editor.EnableHistory = true;
            var source = new PortNode();
            var target = new PortNode();
            var other = new PortNode();
            source.Create(); target.Create(); other.Create();
            editor.Nodes.AddRange([source, target, other]);
            Assert.Equal(ConnectionStatus.Connected, source.Output.ConnectOption(target.Input));
            Assert.Equal(ConnectionStatus.Connected, source.Output.ConnectOption(other.Input));
            editor.ClearHistory();
            action(host, editor, source, target, other);
        });
    }

    private sealed class PortNode : STNode
    {
        public STNodeOption Input { get; private set; } = null!;
        public STNodeOption Output { get; private set; } = null!;
        protected override void OnCreate()
        {
            base.OnCreate();
            Input = InputOptions.Add("IN", typeof(int), false);
            Output = OutputOptions.Add("OUT", typeof(int), false);
        }
    }
}
