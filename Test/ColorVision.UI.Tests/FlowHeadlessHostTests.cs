using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using FlowEngineLib.Start;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class FlowHeadlessHostTests
{
    [Fact]
    public void HeadlessControlLoadsAndExecutesExistingStnBytes()
    {
        StaTest.Run(() =>
        {
            byte[] canvas = CreateCanvas();
            using var container = new CVNodeContainer();
            var nodeManager = new FlowNodeManager();
            using var control = new FlowEngineControl(
                container,
                isAutoStartName: false,
                nodeManager);
            FlowEngineEventArgs? completed = null;
            control.Finished += (_, args) => completed = args;

            control.Load(canvas, waitReady: false);
            bool started = control.TryStartNode("HeadlessStart", "SN-HEADLESS");

            Assert.True(started);
            Assert.NotNull(completed);
            Assert.Equal("SN-HEADLESS", completed!.SerialNumber);
            Assert.Equal(StatusTypeEnum.Completed, completed.Status);
            Assert.False(control.IsRunning);
            Assert.Equal(2, container.Nodes.Count);
        });
    }

    [Fact]
    public void CorruptReplacementPreservesReadyHeadlessGeneration()
    {
        StaTest.Run(() =>
        {
            byte[] canvas = CreateCanvas();
            byte[] corrupt = (byte[])canvas.Clone();
            corrupt[^1] ^= 0x5a;
            using var container = new CVNodeContainer();
            using var control = new FlowEngineControl(
                container,
                isAutoStartName: false,
                new FlowNodeManager());
            control.Load(canvas, waitReady: false);

            Assert.ThrowsAny<Exception>(() => control.Load(corrupt, waitReady: false));

            Assert.Equal(new[] { "HeadlessStart" }, control.GetStartNodeNames());
            Assert.Equal(2, container.Nodes.Count);
            Assert.True(control.TryStartNode("HeadlessStart", "SN-AFTER-FAILURE"));
        });
    }

    [Fact]
    public void HeadlessControlLoadsExistingStnFileThroughPublicApi()
    {
        StaTest.Run(() =>
        {
            string filePath = Path.Combine(
                Path.GetTempPath(),
                $"flow-{Guid.NewGuid():N}.stn");
            try
            {
                File.WriteAllBytes(filePath, CreateCanvas());
                using var container = new CVNodeContainer();
                using var control = new FlowEngineControl(
                    container,
                    isAutoStartName: false,
                    new FlowNodeManager());

                control.LoadFromFile(filePath);

                Assert.Equal(new[] { "HeadlessStart" }, control.GetStartNodeNames());
                Assert.Equal(2, container.Nodes.Count);
            }
            finally
            {
                File.Delete(filePath);
            }
        });
    }

    [Fact]
    public void HeadlessClearDisconnectsDetachedNodesAndPublishesRemoval()
    {
        StaTest.Run(() =>
        {
            using var container = new CVNodeContainer();
            container.LoadCanvas(CreateCanvas());
            var start = Assert.IsType<HeadlessTestStartNode>(container.Nodes[0]);
            var end = Assert.IsType<CVEndNode>(container.Nodes[1]);
            int removed = 0;
            container.NodeRemoved += (_, _) => removed++;

            Assert.Equal(1, start.m_op_start.ConnectionCount);
            Assert.Single(end.m_in_start.GetConnectedOption());

            container.Clear();

            Assert.Equal(0, start.m_op_start.ConnectionCount);
            Assert.Equal(0, end.m_in_start.ConnectionCount);
            Assert.Equal(2, removed);
            Assert.Equal(0, container.Nodes.Count);
        });
    }

    [Fact]
    public void EditorAndHeadlessHostsDecodeEquivalentRuntimeGraphs()
    {
        StaTest.Run(() =>
        {
            byte[] canvas = CreateCanvas();
            using var editor = new STNodeEditor();
            using var container = new CVNodeContainer();

            editor.LoadCanvas(canvas);
            container.LoadCanvas(canvas);

            Assert.Equal(editor.Nodes.Count, container.Nodes.Count);
            Assert.Equal(
                editor.Nodes.Cast<STNode>().Select(node => node.GetType()),
                container.Nodes.Cast<STNode>().Select(node => node.GetType()));
            Assert.Equal(
                editor.Nodes.Cast<STNode>().Sum(node =>
                    node.GetAllOutputOptions().Sum(option => option.ConnectionCount)),
                container.Nodes.Cast<STNode>().Sum(node =>
                    node.GetAllOutputOptions().Sum(option => option.ConnectionCount)));
        });
    }

    private static byte[] CreateCanvas()
    {
        using var editor = new STNodeEditor();
        var start = new HeadlessTestStartNode();
        var end = new CVEndNode();
        start.Create();
        end.Create();
        editor.Nodes.Add(start);
        editor.Nodes.Add(end);
        Assert.Equal(
            ConnectionStatus.Connected,
            start.m_op_start.ConnectOption(end.m_in_start));
        return editor.GetCanvasData();
    }
}

public sealed class HeadlessTestStartNode : BaseStartNode
{
    public HeadlessTestStartNode()
        : base("Headless test start")
    {
        NodeName = "HeadlessStart";
    }
}
