using ColorVision.Engine.FlowProcessing.Compilation;
using ST.Library.UI.NodeEditor;

namespace ColorVision.UI.Tests;

public sealed class FlowSubflowAuthoringTests
{
    [Fact]
    public void CaptureAndUpsert_UsesExistingConnectionAndPinnedTarget()
    {
        RunOnSta(() =>
        {
            using var editor = new STNodeEditor();
            FlowSubflowTestNode source =
                CreateNode("source");
            FlowSubflowTestNode target =
                CreateNode("target");
            editor.Nodes.Add(source);
            editor.Nodes.Add(target);
            Assert.Equal(
                ConnectionStatus.Connected,
                source.Output.ConnectOption(target.Input));

            FlowSubflowConnectionChoice connection = Assert.Single(
                FlowSubflowAuthoring.CaptureConnections(editor));
            string contentHash = new string('a', 64);
            var targetChoice = new FlowSubflowTargetChoice(
                "child",
                "flow:child",
                7,
                contentHash);

            FlowSubflowSidecar sidecar =
                FlowSubflowAuthoring.Upsert(
                    FlowSubflowSidecar.Empty,
                    "call-1",
                    connection,
                    targetChoice);
            FlowSubflowCall call = Assert.Single(sidecar.Calls);
            Assert.Equal(source.Guid, call.Source.NodeId);
            Assert.Equal(target.Guid, call.Target.NodeId);
            Assert.Equal("flow:child", call.Child.FlowKey);
            Assert.Equal("7", call.Child.Revision);
            Assert.Equal(contentHash, call.Child.ContentHash);
        });
    }

    [Fact]
    public void Upsert_ReplacesSameConnectionAndRemoveKeepsOtherCalls()
    {
        var firstConnection = new FlowSubflowConnectionChoice(
            new FlowPortReference(Guid.NewGuid(), 0),
            new FlowPortReference(Guid.NewGuid(), 0),
            "first");
        var secondConnection = new FlowSubflowConnectionChoice(
            new FlowPortReference(Guid.NewGuid(), 0),
            new FlowPortReference(Guid.NewGuid(), 0),
            "second");
        var firstTarget = new FlowSubflowTargetChoice(
            "child-a",
            "flow:a",
            1,
            new string('a', 64));
        var secondTarget = new FlowSubflowTargetChoice(
            "child-b",
            "flow:b",
            2,
            new string('b', 64));

        FlowSubflowSidecar sidecar =
            FlowSubflowAuthoring.Upsert(
                FlowSubflowSidecar.Empty,
                "first",
                firstConnection,
                firstTarget);
        sidecar = FlowSubflowAuthoring.Upsert(
            sidecar,
            "second",
            secondConnection,
            secondTarget);
        sidecar = FlowSubflowAuthoring.Upsert(
            sidecar,
            "replacement",
            firstConnection,
            secondTarget);

        Assert.Equal(2, sidecar.Calls.Count);
        Assert.DoesNotContain(
            sidecar.Calls,
            call => call.CallId == "first");
        Assert.Contains(
            sidecar.Calls,
            call => call.CallId == "replacement");

        FlowSubflowSidecar remaining =
            FlowSubflowAuthoring.Remove(
                sidecar,
                "replacement");
        Assert.Equal(
            "second",
            Assert.Single(remaining.Calls).CallId);
    }

    private static FlowSubflowTestNode CreateNode(
        string name)
    {
        var node = new FlowSubflowTestNode
        {
            NodeName = name,
        };
        node.Create();
        return node;
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null)
            throw failure;
    }
}
