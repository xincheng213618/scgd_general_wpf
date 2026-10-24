using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using ColorVision.Engine.FlowProcessing.Compilation;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.Runtime;
using FlowEngineLib.Node.Spectrum;
using FlowEngineLib.Repeat;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;

namespace ColorVision.UI.Tests;

public sealed class TimerRepeatTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SourceTimerSpectrumAcceptsTheActualDevicePortsAndTheUnusedSparePair(bool localSpectrum)
        => StaTest.Run(() =>
        {
            var source = new LocalSmuNode();
            CVCommonNode spectrum = localSpectrum ? new LocalSpectrumNode() : new SpectrumNode();
            CVCommonNode timer = Timer(0);
            source.Create();
            spectrum.Create();
            Connect(FlowOutput(source), timer.GetAllInputOptions()[0]);
            Connect(timer.GetAllOutputOptions()[0], FlowInput(spectrum));
            Assert.Equal(2, timer.InputOptionsCount);
            Assert.True(FlowRepeatRegion.TryCreate([source, timer, spectrum], out var region, out string error), error);
            Assert.Equal(new CVCommonNode[] { source, timer, spectrum }, region.Nodes);
            Assert.True(FlowRepeatRegion.TryCreate([timer], out _, out error), error);
        });

    [Theory]
    [InlineData(0, false)]
    [InlineData(5, false)]
    [InlineData(0, true)]
    [InlineData(5, true)]
    public void DelayRunsInEveryRoundAndOnlyTheFinalRoundEscapes(int milliseconds, bool timerOnly)
        => StaTest.Run(() => VerifyRepeatsAsync(milliseconds, timerOnly).GetAwaiter().GetResult());

    private static async Task VerifyRepeatsAsync(int milliseconds, bool timerOnly)
    {
        var source = Probe();
        var spectrum = Probe();
        var after = Probe();
        CVCommonNode timer = Timer(milliseconds);
        Connect(source.Output, timer.GetAllInputOptions()[0]);
        Connect(timer.GetAllOutputOptions()[0], spectrum.Input);
        Connect(spectrum.Output, after.Input);
        CVCommonNode[] members = timerOnly ? [timer] : [source, timer, spectrum];
        string id = Guid.NewGuid().ToString();
        foreach (var node in members) node.SetRepeatRegion(id, 3, members.Length);
        int delays = 0;
        var runs = new ConcurrentQueue<FlowEngineNodeRunEventArgs>();
        var ends = new ConcurrentQueue<FlowEngineNodeEndEventArgs>();
        timer.nodeRunEvent += (_, args) => runs.Enqueue(args);
        timer.nodeEndEvent += (_, args) => ends.Enqueue(args);
        var records = new ConcurrentBag<FlowNodeRecord>();
        var messages = new ConcurrentBag<FlowNodeMessage>();
        int nextRecord = 0, nextMessage = 0;
        using var recorder = new FlowNodeExecutionRecorder(record =>
        { record.Id = Interlocked.Increment(ref nextRecord); records.Add(record); return record.Id; }, _ => { }, message =>
        { message.Id = Interlocked.Increment(ref nextMessage); messages.Add(message); return message.Id; }, _ => { }, _ => true, _ => { });
        recorder.AttachNodes([timer]);
        FlowInput(timer).DataTransfer += (_, _) => Interlocked.Increment(ref delays);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        after.Work = _ => completed.TrySetResult();
        var action = Action("repeat");
        recorder.StartRun(1, action.SerialNumber);
        try
        {
            source.Input.ReceiveData(action);
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(3, delays);
            Assert.Equal(timerOnly ? 1 : 3, source.Count);
            Assert.Equal(timerOnly ? 1 : 3, spectrum.Count);
            Assert.Equal(1, after.Count);
            Assert.True(await recorder.CompleteRunAsync(action.SerialNumber));
            Assert.Equal(3, runs.Count);
            Assert.Equal(runs.Select(run => run.SendMsgId), ends.Select(end => end.RecvMsgId));
            Assert.Equal(3, runs.Select(run => run.SendMsgId).Distinct().Count());
            Assert.All(runs, run => { Assert.Equal(milliseconds, run.ExpectedDurationMs); Assert.Equal("LOCAL", run.SendTopic); });
            Assert.All(ends, end => Assert.Equal(0, end.RecvStatusCode));
            Assert.Equal(3, records.Count);
            Assert.All(records, record => { Assert.NotNull(record.EndTime); Assert.Equal(timer.NodeID, record.NodeId); });
            Assert.Equal(3, messages.Count);
            Assert.All(messages, message => Assert.Equal(FlowMessageState.Success, message.State));
            Assert.All(members, node => Assert.Equal(STNodeGroupState.Completed, ((ISTNodeGroupMember)node).GroupState));
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Fact]
    public void PendingDelaysKeepTheirOwnSnapshotEvenWhenSerialNumbersAndOutputPortsAreShared()
        => StaTest.Run(() =>
        {
            CVCommonNode timer = Timer(int.MaxValue);
            var after = Probe();
            after.Work = null;
            Connect(timer.GetAllOutputOptions()[0], after.Input);
            STNodeOption input = FlowInput(timer);
            var first = Action("same-sn");
            var second = Action("same-sn");
            first.Data["value"] = 11;
            second.Data["value"] = 22;
            try
            {
                input.ReceiveData(first);
                object firstDelay = Assert.Single(Pending(timer));
                first.Data["value"] = 99;
                input.ReceiveData(second);
                object secondDelay = Pending(timer).Single(delay => !ReferenceEquals(delay, firstDelay));
                Assert.Equal(0, after.Count);
                FlowOutput(timer).Data = second;
                CompleteDelay(firstDelay);
                CompleteDelay(secondDelay);
                Assert.Equal(new[] { 11, 22 }, after.Inputs);
            }
            finally { first.RuntimeResources.Dispose(); second.RuntimeResources.Dispose(); }
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StoppedOrDisposedDelayCannotPublishALateRunningOutput(bool disposeResources)
        => StaTest.Run(() =>
        {
            CVCommonNode timer = Timer(int.MaxValue);
            var after = Probe();
            Connect(timer.GetAllOutputOptions()[0], after.Input);
            var action = Action("stop");
            var runs = new ConcurrentQueue<FlowEngineNodeRunEventArgs>();
            var ends = new ConcurrentQueue<FlowEngineNodeEndEventArgs>();
            var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            timer.nodeRunEvent += (_, args) => runs.Enqueue(args);
            timer.nodeEndEvent += (_, args) => { ends.Enqueue(args); canceled.TrySetResult(); };
            try
            {
                FlowInput(timer).ReceiveData(action);
                object delay = Assert.Single(Pending(timer));
                if (disposeResources) action.RuntimeResources.Dispose();
                else
                {
                    var stopped = new CVStartCFC(action) { FlowStatus = StatusTypeEnum.Canceled };
                    FlowInput(timer).ReceiveData(stopped);
                }
                CompleteDelay(delay);
                canceled.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                Assert.Equal(Assert.Single(runs).SendMsgId, Assert.Single(ends).RecvMsgId);
                Assert.Equal(FlowFailureKind.Canceled, Assert.Single(ends).FailureKind);
                Assert.Equal(0, after.Count);
            }
            finally { action.RuntimeResources.Dispose(); }
        });

    [Fact]
    public void TimerDiagnosticsFailuresDoNotBlockOutputOrOtherObservers()
        => StaTest.Run(() =>
        {
            CVCommonNode timer = Timer(0);
            var after = Probe();
            Connect(timer.GetAllOutputOptions()[0], after.Input);
            int started = 0, ended = 0;
            timer.nodeRunEvent += (_, _) => throw new InvalidOperationException("start observer failed");
            timer.nodeRunEvent += (_, _) => started++;
            timer.nodeEndEvent += (_, _) => throw new InvalidOperationException("end observer failed");
            timer.nodeEndEvent += (_, _) => { Assert.Equal(0, after.Count); ended++; };
            var action = Action("timer-observer-failure");
            try
            {
                FlowInput(timer).ReceiveData(action);
                Assert.Equal(1, started);
                Assert.Equal(1, ended);
                Assert.Equal(1, after.Count);
            }
            finally { action.RuntimeResources.Dispose(); }
        });

    [Fact]
    public void HubPortCountConnectionsAndRepeatSettingsSurviveSaveLoadUndoAndCopy()
        => StaTest.Run(() =>
        {
            using var editor = new STNodeEditor { EnableHistory = true };
            var source = Probe();
            CVCommonNode timer = Timer(17);
            var after = Probe();
            foreach (STNode node in new STNode[] { source, timer, after }) editor.Nodes.Add(node);
            Connect(source.Output, timer.GetAllInputOptions()[0]);
            Connect(timer.GetAllOutputOptions()[0], after.Input);
            string id = Guid.NewGuid().ToString();
            editor.ExecuteEditTransaction("repeat", () => timer.SetRepeatRegion(id, 3, 1));
            editor.Undo();
            Assert.Empty(editor.GetNodeGroups());
            editor.Redo();
            Assert.Single(editor.GetNodeGroups());
            using var saved = new MemoryStream();
            editor.SaveCanvas(saved);
            var decoded = StnV1NeutralCodec.Decode(saved.ToArray(), new());
            var timerSchema = decoded.Nodes.Single(node => node.Schema.NodeType == timer.GetType()).Schema;
            Assert.Equal(2, timerSchema.Inputs.Length);
            Assert.Equal(2, timerSchema.Outputs.Length);
            using var loaded = new CVNodeContainer();
            loaded.LoadCanvas(StnV1NeutralCodec.Encode(decoded, new()));
            CVCommonNode restored = loaded.Nodes.Cast<STNode>().OfType<CVCommonNode>().Single(node => node.GetType() == timer.GetType());
            Assert.Equal(2, restored.InputOptionsCount);
            Assert.Equal(17, restored.GetType().GetProperty("Time")!.GetValue(restored));
            Assert.Equal(id, restored.RepeatRegionId);
            Assert.Equal(3, restored.RepeatCount);
            Assert.Equal(1, FlowInput(restored).ConnectionCount);
            Assert.Equal(1, FlowOutput(restored).ConnectionCount);
            Assert.True(FlowRepeatRegion.TryValidate(loaded.Nodes.Cast<STNode>(), out string error), error);
            var copy = Assert.IsAssignableFrom<CVCommonNode>(Assert.Single(editor.ImportSelectionData(editor.GetNodesData([timer]), new System.Drawing.Point(500, 100))));
            Assert.NotEqual(id, copy.RepeatRegionId);
            Assert.Equal(3, copy.RepeatCount);
        });

    [Fact]
    public void ConnectedNonFlowHubChannelsAndMultipleFlowChannelsRemainOutsideTheRepeatContract()
        => StaTest.Run(() =>
        {
            using var editor = new STNodeEditor();
            CVCommonNode timer = Timer(0);
            var source = Probe();
            editor.Nodes.Add(timer);
            editor.Nodes.Add(source);
            Connect(source.Output, timer.GetAllInputOptions()[0]);
            var extra = new IntNode();
            extra.Create();
            editor.Nodes.Add(extra);
            Connect(extra.Output, timer.GetAllInputOptions()[1]);
            Assert.False(FlowRepeatRegion.TryCreate([timer], out _, out _));
            var sink = new IntNode();
            sink.Create();
            editor.Nodes.Add(sink);
            Connect(timer.GetAllOutputOptions()[1], sink.Input);
            extra.Output.TransferData(42);
            Assert.Equal(42, sink.Value);
            extra.Output.DisConnectionAll();
            sink.Input.DisConnectionAll();
            var second = Probe();
            editor.Nodes.Add(second);
            Connect(second.Output, timer.GetAllInputOptions()[1]);
            Assert.False(FlowRepeatRegion.TryCreate([timer], out _, out _));
        });

    private static CVCommonNode Timer(int milliseconds)
    {
        Type type = typeof(CVCommonNode).Assembly.GetType("FlowEngineLib.simulator.Timer", true)!;
        var node = (CVCommonNode)Activator.CreateInstance(type)!;
        node.Create();
        type.GetProperty("Time")!.SetValue(node, milliseconds);
        return node;
    }

    private static FlowRepeatRegionTests.ProbeNode Probe()
    {
        var node = new FlowRepeatRegionTests.ProbeNode();
        node.Create();
        return node;
    }

    private static CVStartCFC Action(string serialNumber)
    {
        var action = new CVStartCFC(serialNumber) { FlowStatus = StatusTypeEnum.Runing };
        action.Data["value"] = 0;
        return action;
    }

    private static STNodeOption FlowInput(STNode node) => node.GetAllInputOptions().Single(port => port.DataType == typeof(CVStartCFC));
    private static STNodeOption FlowOutput(STNode node) => node.GetAllOutputOptions().Single(port => port.DataType == typeof(CVStartCFC));
    private static void Connect(STNodeOption output, STNodeOption input) => Assert.Equal(ConnectionStatus.Connected, output.ConnectOption(input, false));
    private static object[] Pending(CVCommonNode timer) => ((IDictionary)timer.GetType().GetField("timers", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(timer)!).Values.Cast<object>().ToArray();
    private static void CompleteDelay(object delay) => delay.GetType().GetMethod("TransferData", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(delay, null);

    private sealed class IntNode : STNode
    {
        public STNodeOption Input { get; private set; } = null!;
        public STNodeOption Output { get; private set; } = null!;
        public int? Value { get; private set; }
        protected override void OnCreate()
        {
            Input = InputOptions.Add("IN", typeof(int), true);
            Output = OutputOptions.Add("OUT", typeof(int), false);
            Input.DataTransfer += (_, args) => Value = args.TargetOption.Data as int?;
        }
    }
}
