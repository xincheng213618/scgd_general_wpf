using System.Drawing;
using System.IO;
using System.Text;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.Repeat;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;

namespace ColorVision.UI.Tests;

public sealed class RepeatParameterRuntimeTests
{
    [Fact]
    public void ArraysResolveInOrderWithoutChangingDefaultsAndOnlyFinalOutputEscapes()
    {
        var body = CreateNode(3);
        var after = new FlowRepeatRegionTests.ProbeNode();
        after.Create();
        body.Output.ConnectOption(after.Input, false);
        body.SetRepeatParameterValues(new Dictionary<string, double[]>
        {
            ["Exposure"] = new double[] { 100, 150, 90 },
            ["Average"] = new double[] { 1, 2, 4 }
        });
        var action = CreateAction();
        try
        {
            body.Input.ReceiveData(action);
            Assert.Equal(new[] { (100d, 1d), (150d, 2d), (90d, 4d) }, body.Received);
            Assert.Equal(350, body.Exposure);
            Assert.Equal(5, body.Average);
            Assert.Equal(1, after.Count);
            Assert.Equal(STNodeGroupState.Completed, ((ISTNodeGroupMember)body).GroupState);
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Fact]
    public void InputBuffersGetterCopiesAndLaterEditsCannotChangeActiveRun()
    {
        var body = CreateNode(2);
        body.Manual = true;
        double[] values = { 100, 200 };
        body.SetRepeatParameterValues(new Dictionary<string, double[]> { ["Exposure"] = values });
        values[0] = 999;
        body.RepeatParameterValues["Exposure"][0] = 888;
        var action = CreateAction();
        try
        {
            body.Input.ReceiveData(action);
            body.SetRepeatParameterValues(new Dictionary<string, double[]> { ["Exposure"] = new double[] { 10, 20 } });
            body.Output.TransferData(body.Pending.Dequeue());
            body.Output.TransferData(body.Pending.Dequeue());
            Assert.Equal(new[] { (100d, 5d), (200d, 5d) }, body.Received);
        }
        finally { action.RuntimeResources.Dispose(); }
        body.Manual = false;
        var next = CreateAction();
        try
        {
            body.Input.ReceiveData(next);
            Assert.Equal(new[] { (10d, 5d), (20d, 5d) }, body.Received.Skip(2));
        }
        finally { next.RuntimeResources.Dispose(); }
    }

    [Fact]
    public void ParametersOfAllMembersAreCapturedBeforeFirstNodeExecutes()
    {
        var first = CreateNode(2);
        var second = CreateNode(2);
        second.SetRepeatRegion(first.RepeatRegionId, 2, 2);
        first.SetRepeatRegion(first.RepeatRegionId, 2, 2);
        first.Manual = true;
        first.Output.ConnectOption(second.Input, false);
        second.SetRepeatParameterValues(new Dictionary<string, double[]> { ["Exposure"] = new double[] { 100, 200 } });
        var action = CreateAction();
        try
        {
            first.Input.ReceiveData(action);
            second.SetRepeatParameterValues(new Dictionary<string, double[]> { ["Exposure"] = new double[] { 900, 800 } });
            first.Output.TransferData(first.Pending.Dequeue());
            first.Output.TransferData(first.Pending.Dequeue());
            Assert.Equal(new[] { (100d, 5d), (200d, 5d) }, second.Received);
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    public static IEnumerable<object[]> InvalidSequences()
    {
        yield return new object[] { "Exposure", new double[] { 10 }, false };
        yield return new object[] { "Exposure", new double[] { 10, double.NaN }, false };
        yield return new object[] { "Exposure", new double[] { double.PositiveInfinity, 10 }, false };
        yield return new object[] { "Exposure", new double[] { 10, 0 }, false };
        yield return new object[] { "Average", new double[] { 1, 1.5 }, false };
        yield return new object[] { "Average", new double[] { 1, 257 }, false };
        yield return new object[] { "Unsupported", new double[] { 1, 2 }, false };
        yield return new object[] { "Exposure", new double[] { 10, 20 }, true };
    }

    [Theory]
    [MemberData(nameof(InvalidSequences))]
    public void InvalidArraysFailBeforeDeviceWorkAndNotifyCompletion(string key, double[] values, bool unavailable)
    {
        var node = CreateNode(2);
        using var start = new RuntimeTestStartNode();
        start.Create();
        int completed = 0;
        StatusTypeEnum? status = null;
        start.Finished += (_, args) => { completed++; status = args.Status; };
        node.Unavailable = unavailable;
        node.SetRepeatParameterValues(new Dictionary<string, double[]> { [key] = values });
        Assert.False(FlowRepeatRegion.TryValidate(new[] { node }, out string error));
        Assert.False(string.IsNullOrWhiteSpace(error));
        var action = CreateAction();
        action.SetStartNode(start);
        node.Input.ReceiveData(action);
        Assert.Empty(node.Received);
        Assert.Equal(StatusTypeEnum.Failed, action.FlowStatus);
        Assert.Equal(StatusTypeEnum.Failed, status);
        Assert.Equal(1, completed);
        Assert.True(action.RuntimeResources.IsDisposed);
    }

    [Fact]
    public void UnsupportedNodeRejectsStoredOverridesInsteadOfSilentlyIgnoringThem()
    {
        var node = new FlowRepeatRegionTests.ProbeNode();
        node.Create();
        node.SetRepeatRegion(Guid.NewGuid().ToString(), 2, 1);
        node.SetRepeatParameterValues(new Dictionary<string, double[]> { ["Exposure"] = new double[] { 10, 20 } });
        Assert.False(FlowRepeatRegion.TryValidate(new[] { node }, out _));
        var action = CreateAction();
        node.Input.ReceiveData(action);
        Assert.Equal(0, node.Count);
        Assert.Equal(StatusTypeEnum.Failed, action.FlowStatus);
        Assert.True(action.RuntimeResources.IsDisposed);
    }

    [Fact]
    public void SavedArraysSurviveReopenUndoAndWholeCopyWhilePartialCopyClearsThem()
    {
        StaTest.Run(() =>
        {
            using var editor = new STNodeEditor { EnableHistory = true };
            var first = CreateNode(2);
            var second = CreateNode(2);
            first.Left = 40;
            second.Left = 240;
            first.SetRepeatRegion(first.RepeatRegionId, 2, 2);
            second.SetRepeatRegion(first.RepeatRegionId, 2, 2);
            editor.Nodes.Add(first);
            editor.Nodes.Add(second);
            first.Output.ConnectOption(second.Input);
            editor.ExecuteEditTransaction("parameters", () => first.SetRepeatParameterValues(new Dictionary<string, double[]>
            {
                ["Exposure"] = new double[] { 100, 200 },
                ["Average"] = new double[] { 1, 2 }
            }));
            editor.Undo();
            Assert.Empty(first.RepeatParameterValues);
            editor.Redo();
            Assert.Equal(new double[] { 100, 200 }, first.RepeatParameterValues["Exposure"]);
            using var saved = new MemoryStream();
            editor.SaveCanvas(saved);
            using var reopened = new CVNodeContainer();
            reopened.LoadCanvas(saved.ToArray());
            ParameterProbeNode restored = reopened.Nodes.OfType<ParameterProbeNode>().Single(node => node.Left == 40);
            Assert.Equal(new double[] { 100, 200 }, restored.RepeatParameterValues["Exposure"]);
            Assert.Equal(new double[] { 1, 2 }, restored.RepeatParameterValues["Average"]);
            var copied = editor.ImportSelectionData(editor.GetNodesData(new STNode[] { first, second }), new Point(40, 250)).Cast<ParameterProbeNode>().ToArray();
            ParameterProbeNode copiedFirst = copied.Single(node => node.RepeatParameterValues.Count > 0);
            Assert.NotEqual(first.RepeatRegionId, copiedFirst.RepeatRegionId);
            Assert.Equal(new double[] { 100, 200 }, copiedFirst.RepeatParameterValues["Exposure"]);
            copiedFirst.SetRepeatParameterValues(new Dictionary<string, double[]> { ["Exposure"] = new double[] { 10, 20 } });
            Assert.Equal(new double[] { 100, 200 }, first.RepeatParameterValues["Exposure"]);
            var partial = Assert.IsType<ParameterProbeNode>(Assert.Single(editor.ImportSelectionData(editor.GetNodesData(new[] { first }), new Point(440, 250))));
            Assert.Empty(partial.RepeatRegionId);
            Assert.Empty(partial.RepeatParameterValues);
            editor.ExecuteEditTransaction("remove region", () => first.SetRepeatRegion(string.Empty, 1, 0));
            Assert.Empty(first.RepeatParameterValues);
            editor.Undo();
            Assert.Equal(new double[] { 100, 200 }, first.RepeatParameterValues["Exposure"]);
        });
    }

    [Fact]
    public void OlderRegionWithoutParameterDataUsesConfiguredDefaults()
    {
        var node = CreateNode(2);
        node.OnLoadNode(new Dictionary<string, byte[]>
        {
            [nameof(CVCommonNode.RepeatRegionId)] = Encoding.UTF8.GetBytes("legacy-region"),
            [nameof(CVCommonNode.RepeatCount)] = BitConverter.GetBytes(2),
            [nameof(CVCommonNode.RepeatMemberCount)] = BitConverter.GetBytes(1)
        });
        Assert.Empty(node.RepeatParameterValues);
        var action = CreateAction();
        try
        {
            node.Input.ReceiveData(action);
            Assert.Equal(new[] { (350d, 5d), (350d, 5d) }, node.Received);
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Fact]
    public void SingleNodeBatchUsesIndependentFrozenArraysAndCompletesAllRoundsWithOneOutput()
    {
        var node = CreateNode(3);
        node.Manual = true;
        node.SetRepeatParameterValues(new Dictionary<string, double[]> { ["Exposure"] = new double[] { 100, 200, 300 } });
        var after = new FlowRepeatRegionTests.ProbeNode();
        after.Create();
        node.Output.ConnectOption(after.Input, false);
        var action = CreateAction();
        var resource = new TrackedResource();
        try
        {
            node.Input.ReceiveData(action);
            CVStartCFC batch = node.Pending.Dequeue();
            batch.SetRuntimeResource("batch-result", resource);
            Assert.True(node.TryGetSingleNodeRepeatParameters(batch, out int count, out var values));
            Assert.Equal(3, count);
            Assert.Equal(new double[] { 100, 200, 300 }, values["Exposure"]);
            values["Exposure"][1] = 999;
            node.SetRepeatParameterValues(new Dictionary<string, double[]> { ["Exposure"] = new double[] { 10, 20, 30 } });
            Assert.True(node.TryGetSingleNodeRepeatParameters(batch, out _, out var reread));
            Assert.Equal(new double[] { 100, 200, 300 }, reread["Exposure"]);
            Assert.Equal(100, node.ResolveRepeatParameter(batch, "Exposure", 350));

            node.CompleteRepeatBatch(batch, count);
            Assert.Equal(0, after.Count);
            node.Output.TransferData(batch);
            node.Output.TransferData(batch);
            Assert.Single(node.Received);
            Assert.Equal(1, after.Count);
            Assert.Equal(STNodeGroupState.Completed, ((ISTNodeGroupMember)node).GroupState);
            Assert.Equal(string.Format(FlowRepeatRegion.Text("已完成 {0} / {1} 次"), 3, 3), ((ISTNodeGroupMember)node).GroupTitle);
            Assert.Equal(0, resource.DisposeCount);
            Assert.Equal(new double[] { 10, 20, 30 }, node.RepeatParameterValues["Exposure"]);
        }
        finally { action.RuntimeResources.Dispose(); }
        Assert.Equal(1, resource.DisposeCount);
    }

    [Fact]
    public void OnlyClaimingActionWithExactCountCanCompleteBatchAndCopiesCannotForwardIt()
    {
        var node = CreateNode(3);
        node.Manual = true;
        var after = new FlowRepeatRegionTests.ProbeNode();
        after.Create();
        node.Output.ConnectOption(after.Input, false);
        var action = CreateAction();
        try
        {
            node.Input.ReceiveData(action);
            CVStartCFC batch = node.Pending.Dequeue();
            Assert.Throws<InvalidOperationException>(() => node.CompleteRepeatBatch(batch, 3));
            Assert.True(node.TryGetSingleNodeRepeatParameters(batch, out int count, out _));
            var copy = new CVStartCFC(batch);
            Assert.False(node.TryGetSingleNodeRepeatParameters(copy, out _, out _));
            Assert.Throws<InvalidOperationException>(() => node.CompleteRepeatBatch(copy, count));
            Assert.Throws<InvalidOperationException>(() => node.CompleteRepeatBatch(batch, count - 1));
            var foreign = CreateNode(count);
            Assert.False(foreign.TryGetSingleNodeRepeatParameters(batch, out _, out _));
            Assert.Throws<InvalidOperationException>(() => foreign.CompleteRepeatBatch(batch, count));
            node.CompleteRepeatBatch(batch, count);
            Assert.Throws<InvalidOperationException>(() => node.CompleteRepeatBatch(batch, count));
            node.Output.TransferData(copy);
            Assert.Equal(0, after.Count);
            Assert.Single(node.Received);
            node.Output.TransferData(batch);
            Assert.Equal(1, after.Count);
            Assert.False(node.TryGetSingleNodeRepeatParameters(batch, out _, out _));
            Assert.Throws<InvalidOperationException>(() => node.CompleteRepeatBatch(batch, count));
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Theory]
    [InlineData(StatusTypeEnum.Canceled)]
    [InlineData(StatusTypeEnum.Failed)]
    [InlineData(StatusTypeEnum.OverTime)]
    public void UnsuccessfulBatchKeepsStopPropagationAndNeverReportsAllRoundsComplete(StatusTypeEnum status)
    {
        var node = CreateNode(3);
        node.Manual = true;
        var after = new FlowRepeatRegionTests.ProbeNode();
        after.Create();
        node.Output.ConnectOption(after.Input, false);
        var action = CreateAction();
        try
        {
            node.Input.ReceiveData(action);
            CVStartCFC batch = node.Pending.Dequeue();
            Assert.True(node.TryGetSingleNodeRepeatParameters(batch, out int count, out _));
            batch.FlowStatus = status;
            Assert.Throws<InvalidOperationException>(() => node.CompleteRepeatBatch(batch, count));
            node.Output.TransferData(batch);
            Assert.Single(node.Received);
            Assert.Equal(0, after.Count);
            Assert.Equal(status, after.LastReceived!.FlowStatus);
            Assert.Equal(status == StatusTypeEnum.Canceled ? STNodeGroupState.Stopped : STNodeGroupState.Failed,
                ((ISTNodeGroupMember)node).GroupState);
            Assert.Equal(string.Format(FlowRepeatRegion.Text(status == StatusTypeEnum.Canceled ? "已停止 · 第 {0} / {1} 次" : "失败 · 第 {0} / {1} 次"), 1, 3),
                ((ISTNodeGroupMember)node).GroupTitle);
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Fact]
    public void BatchIsUnavailableForMultipleNodesAndNormalIterationRemainsUnchanged()
    {
        var first = CreateNode(2);
        var second = CreateNode(2);
        first.SetRepeatRegion(first.RepeatRegionId, 2, 2);
        second.SetRepeatRegion(first.RepeatRegionId, 2, 2);
        first.Output.ConnectOption(second.Input, false);
        first.Manual = true;
        var action = CreateAction();
        try
        {
            first.Input.ReceiveData(action);
            CVStartCFC current = first.Pending.Dequeue();
            Assert.False(first.TryGetSingleNodeRepeatParameters(current, out _, out _));
            Assert.Throws<InvalidOperationException>(() => first.CompleteRepeatBatch(current, 2));
            first.Output.TransferData(current);
            current = first.Pending.Dequeue();
            Assert.False(first.TryGetSingleNodeRepeatParameters(current, out _, out _));
            first.Output.TransferData(current);
            Assert.Equal(2, first.Received.Count);
            Assert.Equal(2, second.Received.Count);
        }
        finally { action.RuntimeResources.Dispose(); }

        var once = CreateNode(1);
        once.Manual = true;
        var singleAction = CreateAction();
        try
        {
            once.Input.ReceiveData(singleAction);
            CVStartCFC current = once.Pending.Dequeue();
            Assert.False(once.TryGetSingleNodeRepeatParameters(current, out _, out _));
            Assert.Throws<InvalidOperationException>(() => once.CompleteRepeatBatch(current, 1));
            once.Output.TransferData(current);
            Assert.Equal(STNodeGroupState.Completed, ((ISTNodeGroupMember)once).GroupState);
        }
        finally { singleAction.RuntimeResources.Dispose(); }
    }

    private sealed class TrackedResource : IDisposable
    {
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }

    private static ParameterProbeNode CreateNode(int count)
    {
        var node = new ParameterProbeNode();
        node.Create();
        node.SetRepeatRegion(Guid.NewGuid().ToString(), count, 1);
        return node;
    }

    private static CVStartCFC CreateAction()
    {
        var action = new CVStartCFC("repeat-parameters") { FlowStatus = StatusTypeEnum.Runing };
        action.Data["value"] = 0;
        return action;
    }

    [STNode("Tests")]
    public sealed class ParameterProbeNode : CVCommonNode, IRepeatParameterNode
    {
        public STNodeOption Input { get; private set; } = null!;
        public STNodeOption Output { get; private set; } = null!;
        public double Exposure { get; set; } = 350;
        public int Average { get; set; } = 5;
        public bool Unavailable { get; set; }
        public bool Manual { get; set; }
        public List<(double Exposure, double Average)> Received { get; } = new();
        public Queue<CVStartCFC> Pending { get; } = new();

        public ParameterProbeNode() : base("Parameter probe", "ParameterProbe", "ParameterProbe") { }

        public IReadOnlyList<RepeatParameterDefinition> GetRepeatParameters() => new[]
        {
            new RepeatParameterDefinition("Exposure", "曝光时间", "ms", Exposure, minimum: 1, maximum: 10000,
                unavailableReason: Unavailable ? "当前设备不支持设置曝光。" : null),
            new RepeatParameterDefinition("Average", "平均次数", "", Average, isInteger: true, minimum: 1, maximum: 256)
        };

        protected override void OnCreate()
        {
            base.OnCreate();
            Input = InputOptions.Add("IN", typeof(CVStartCFC), true);
            Output = OutputOptions.Add("OUT", typeof(CVStartCFC), false);
            Input.DataTransfer += (_, args) =>
            {
                if (args.TargetOption.Data is not CVStartCFC received || !received.IsRunning) return;
                var action = new CVStartCFC(received);
                Received.Add((ResolveRepeatParameter(action, "Exposure", Exposure), ResolveRepeatParameter(action, "Average", Average)));
                if (Manual) Pending.Enqueue(action);
                else Output.TransferData(action);
            };
        }
    }
}
