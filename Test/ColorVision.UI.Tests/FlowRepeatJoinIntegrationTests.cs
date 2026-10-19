using ColorVision.Engine.FlowProcessing.Nodes;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using FlowEngineLib.Node.OLED;
using FlowEngineLib.Repeat;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ColorVision.UI.Tests;

public sealed class FlowRepeatJoinIntegrationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ActualAlgorithmHubPairsImageAndRoiFromEveryRoundAndReleasesOnlyTheFinalResult(bool roiFirst, bool deferredResponse)
        => StaTest.Run(() => VerifyAlgorithmAsync(roiFirst, deferredResponse).GetAwaiter().GetResult());

    private static async Task VerifyAlgorithmAsync(bool roiFirst, bool deferredResponse)
    {
        using var graph = new JoinGraph(roiFirst, local: false, controlled: deferredResponse);
        graph.StartFlow();
        if (deferredResponse)
            for (int round = 1; round <= 3; round++)
            {
                CVMQTTRequest request = await graph.Requests.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
                await Task.Run(() => graph.Reply(request));
            }
        Assert.Equal(StatusTypeEnum.Completed, (await graph.Completed.Task.WaitAsync(TimeSpan.FromSeconds(10))).Status);
        graph.AssertThreeRounds();
        Assert.Equal(3, graph.Start.PublishCount);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ActualBuildPoiAndAlgorithmKeepCameraBranchUnchangedForSynchronousAndDeferredRoiResponses(bool roiFirst, bool deferredRoi)
        => StaTest.Run(() => VerifyActualBuildPoiAsync(roiFirst, deferredRoi).GetAwaiter().GetResult());

    private static async Task VerifyActualBuildPoiAsync(bool roiFirst, bool deferredRoi)
    {
        using var graph = new JoinGraph(roiFirst, local: false, controlled: false, realRoi: true, controlledRoi: deferredRoi);
        graph.Camera.IncludeImageMarker = true;
        graph.StartFlow();
        if (deferredRoi)
            for (int round = 1; round <= 3; round++)
            {
                CVMQTTRequest request = await graph.RoiRequests.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
                await Task.Run(() => graph.ReplyRoi(request));
            }
        Assert.Equal(StatusTypeEnum.Completed, (await graph.Completed.Task.WaitAsync(TimeSpan.FromSeconds(10))).Status);
        graph.AssertThreeRounds();
        Assert.Equal(6, graph.Start.PublishCount);
        CVStartCFC[] cameraOutputs = graph.Camera.Published.ToArray();
        Assert.Equal(3, cameraOutputs.Length);
        for (int round = 1; round <= cameraOutputs.Length; round++)
        {
            Assert.Equal(1000 + round, Convert.ToInt32(cameraOutputs[round - 1].Data["MasterId"]));
            Assert.Equal("camera-image-" + round, cameraOutputs[round - 1].Data["Image"]);
        }
    }

    [Fact]
    public void AcceptedRoiIsNotVisibleToTheImageHandlerUntilItsOwnHubHandlerCanConsumeIt()
        => StaTest.Run(() => VerifyConcurrentHubArrivalAsync().GetAwaiter().GetResult());

    private static async Task VerifyConcurrentHubArrivalAsync()
    {
        var algorithm = new PausedRoiAlgorithm { MaxTime = 10000 };
        using var graph = new JoinGraph(roiFirst: true, local: false, controlled: false, algorithmOverride: algorithm);
        var deliveries = new ConcurrentQueue<Task>();
        graph.Roi.DispatchOutput = action =>
        {
            var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            deliveries.Enqueue(delivered.Task);
            _ = Task.Run(() =>
            {
                try { graph.Roi.Output.TransferData(action); delivered.TrySetResult(); }
                catch (Exception ex) { delivered.TrySetException(ex); }
            });
            if (Convert.ToInt32(action.Data["MasterId"]) == 2001)
                algorithm.RoiAccepted.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        };
        try
        {
            graph.StartFlow();
            // The image handler has returned, but the ROI is still between the runtime gate and its Hub handler.
            Assert.True(algorithm.RoiAccepted.Task.IsCompleted);
            Assert.Empty(graph.Pairs);
            Assert.Equal(0, graph.Start.PublishCount);
            Assert.False(graph.Completed.Task.IsCompleted);
            algorithm.AllowRoi.TrySetResult();
            Assert.Equal(StatusTypeEnum.Completed, (await graph.Completed.Task.WaitAsync(TimeSpan.FromSeconds(10))).Status);
            graph.AssertThreeRounds();
        }
        finally
        {
            algorithm.AllowRoi.TrySetResult();
            Task[] current;
            do
            {
                current = deliveries.ToArray();
                await Task.WhenAll(current).WaitAsync(TimeSpan.FromSeconds(10));
            } while (current.Length != deliveries.Count);
        }
    }

    [Fact]
    public void SynchronousHubResponseCannotClearTheNextRoundWhileItsRoiIsStillPending()
        => StaTest.Run(() => VerifyReentrantHubAsync().GetAwaiter().GetResult());

    private static async Task VerifyReentrantHubAsync()
    {
        using var graph = new JoinGraph(roiFirst: false, local: false, controlled: false);
        graph.Roi.HoldRound = 2;
        graph.StartFlow();
        CVStartCFC pendingRoi = await graph.Roi.Pending.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Single(graph.Pairs);
        Assert.Equal(2, graph.Camera.Calls);
        graph.Roi.Output.TransferData(pendingRoi);
        Assert.Equal(StatusTypeEnum.Completed, (await graph.Completed.Task.WaitAsync(TimeSpan.FromSeconds(10))).Status);
        graph.AssertThreeRounds();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void LocalTwoInputNodeRetainsBothRoundSnapshotsAcrossImmediateAndControlledWork(bool roiFirst, bool controlled)
        => StaTest.Run(() => VerifyLocalAsync(roiFirst, controlled).GetAwaiter().GetResult());

    private static async Task VerifyLocalAsync(bool roiFirst, bool controlled)
    {
        using var graph = new JoinGraph(roiFirst, local: true, controlled);
        graph.StartFlow();
        if (controlled)
            for (int round = 1; round <= 3; round++)
            {
                ProbeExecution execution = await graph.LocalJoin!.Executions.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal((1000 + round, 2000 + round), (execution.ImageId, execution.RoiId));
                execution.Continue.TrySetResult();
            }
        Assert.Equal(StatusTypeEnum.Completed, (await graph.Completed.Task.WaitAsync(TimeSpan.FromSeconds(10))).Status);
        graph.AssertThreeRounds();
        Assert.Equal(0, graph.Start.PublishCount);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void FailedOrCanceledJoinDoesNotStartAnotherRoundOrReleaseASuccessfulOutput(bool local, bool cancel)
        => StaTest.Run(() => VerifyStoppedJoinAsync(local, cancel).GetAwaiter().GetResult());

    private static async Task VerifyStoppedJoinAsync(bool local, bool cancel)
    {
        using var graph = new JoinGraph(roiFirst: false, local, controlled: true);
        graph.StartFlow();
        ProbeExecution? execution = local
            ? await graph.LocalJoin!.Executions.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10)) : null;
        CVMQTTRequest? request = local
            ? null : await graph.Requests.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        if (cancel)
        {
            graph.Control.StopNode(graph.Start.NodeName, JoinGraph.SerialNumber);
            execution?.Continue.TrySetResult();
            if (request != null) graph.Algorithm!.DoServerStatusRecv(graph.Response(request, 0));
        }
        else if (execution != null) execution.Continue.TrySetException(new InvalidOperationException("synthetic join failure"));
        else graph.Reply(request!, 500);
        Assert.Equal(cancel ? StatusTypeEnum.Canceled : StatusTypeEnum.Failed,
            (await graph.Completed.Task.WaitAsync(TimeSpan.FromSeconds(10))).Status);
        if (execution != null) await execution.Exited.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(new[] { (1001, 2001) }, graph.Pairs.ToArray());
        Assert.Equal(1, graph.Camera.Calls);
        Assert.Equal(1, graph.Roi.Calls);
        Assert.Equal(0, graph.End.SuccessfulOutputs);
    }

    private sealed class JoinGraph : IDisposable
    {
        internal const string SerialNumber = "repeat-image-roi-join";
        private readonly CVNodeContainer container = new();
        internal RuntimeTestStartNode Start { get; } = new();
        internal ResultNode Camera { get; } = new(false);
        internal ResultNode Roi { get; } = new(true);
        internal RecordingEndNode End { get; } = new();
        internal Algorithm2InNode? Algorithm { get; }
        internal BuildPOINode? ServerRoi { get; }
        internal LocalJoinProbe? LocalJoin { get; }
        internal CVCommonNode[] Members { get; }
        internal FlowEngineControl Control { get; }
        internal Channel<CVMQTTRequest> Requests { get; } = Channel.CreateUnbounded<CVMQTTRequest>();
        internal Channel<CVMQTTRequest> RoiRequests { get; } = Channel.CreateUnbounded<CVMQTTRequest>();
        private readonly ConcurrentQueue<int> serverRoiImageIds = new();
        internal ConcurrentQueue<(int ImageId, int RoiId)> Pairs { get; } = new();
        internal TaskCompletionSource<FlowEngineEventArgs> Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal JoinGraph(bool roiFirst, bool local, bool controlled, bool realRoi = false, bool controlledRoi = false, Algorithm2InNode? algorithmOverride = null)
        {
            CVCommonNode join;
            if (local) join = LocalJoin = new LocalJoinProbe(controlled, Pairs);
            else join = Algorithm = algorithmOverride ?? new Algorithm2InNode { MaxTime = 10000 };
            CVCommonNode roiNode = realRoi ? ServerRoi = new BuildPOINode { MaxTime = 10000 } : Roi;
            Members = [Camera, roiNode, join];
            foreach (STNode node in new STNode[] { Start, Camera, roiNode, join, End }) { node.Create(); container.Nodes.Add(node); }
            string region = Guid.NewGuid().ToString();
            foreach (CVCommonNode node in Members) node.SetRepeatRegion(region, 3, Members.Length);
            STNodeOption imageInput = join.GetAllInputOptions().Single(port => port.Text == "IN_IMG");
            STNodeOption roiInput = join.GetAllInputOptions().Single(port => port.Text == "IN_ROI");
            STNodeOption roiNodeInput = roiNode.GetAllInputOptions().Single(port => port.DataType == typeof(CVStartCFC));
            STNodeOption roiNodeOutput = roiNode.GetAllOutputOptions().Single(port => port.DataType == typeof(CVStartCFC));
            Assert.Equal(ConnectionStatus.Connected, Start.m_op_start.ConnectOption(Camera.Input, false));
            // Preserve the screenshot's direct camera branch and camera -> ROI branch while testing both arrival orders.
            if (roiFirst)
            {
                Assert.Equal(ConnectionStatus.Connected, Camera.Output.ConnectOption(roiNodeInput, false));
                Assert.Equal(ConnectionStatus.Connected, Camera.Output.ConnectOption(imageInput, false));
            }
            else
            {
                Assert.Equal(ConnectionStatus.Connected, Camera.Output.ConnectOption(imageInput, false));
                Assert.Equal(ConnectionStatus.Connected, Camera.Output.ConnectOption(roiNodeInput, false));
            }
            Assert.Equal(ConnectionStatus.Connected, roiNodeOutput.ConnectOption(roiInput, false));
            Assert.Equal(ConnectionStatus.Connected, join.GetAllOutputOptions().Single(port => port.DataType == typeof(CVStartCFC)).ConnectOption(End.m_in_start, false));
            Assert.True(FlowRepeatRegion.TryCreate(Members, out _, out string error), error);
            Control = new FlowEngineControl(container, false, new FlowNodeManager()) { PersistResults = false };
            Control.Finished += (_, args) => Completed.TrySetResult(args);
            Start.OnPublish = action =>
            {
                CVMQTTRequest request = JsonConvert.DeserializeObject<CVMQTTRequest>(action.Message)!;
                JObject parameters = JObject.FromObject(request.Data);
                if (request.DeviceNodeCode == ServerRoi?.NodeID)
                {
                    serverRoiImageIds.Enqueue(parameters.Value<int>("MasterId"));
                    RoiRequests.Writer.TryWrite(request);
                    if (!controlledRoi) ReplyRoi(request);
                    return;
                }
                Pairs.Enqueue((parameters.Value<int>("MasterId"), parameters.Value<int>("POI_MasterId")));
                Requests.Writer.TryWrite(request);
                if (!controlled) Reply(request);
            };
        }

        internal void StartFlow() => Assert.True(Control.TryStartNode(Start.NodeName, SerialNumber));

        internal void Reply(CVMQTTRequest request, int code = 0) => Assert.True(Algorithm!.DoServerStatusRecv(Response(request, code)));

        internal void ReplyRoi(CVMQTTRequest request) => Assert.True(ServerRoi!.DoServerStatusRecv(Response(request, 0, roi: true)));

        internal CVBaseDataFlowResp Response(CVMQTTRequest request, int code, bool roi = false)
        {
            int round = JObject.FromObject(request.Data).Value<int>("MasterId") - 1000;
            return new CVBaseDataFlowResp
            {
                Code = code, Message = code == 0 ? "ok" : "synthetic join failure", Version = request.Version,
                ServiceName = request.ServiceCode, DeviceNodeCode = request.DeviceNodeCode, EventName = request.EventName,
                SerialNumber = request.SerialNumber, MsgID = request.MsgID, SendTime = DateTime.Now, ZIndex = request.ZIndex,
                Data = new JObject { ["MasterId"] = (roi ? 2000 : 3000) + round, ["MasterResultType"] = roi ? 25 : 250 }
            };
        }

        internal void AssertThreeRounds()
        {
            Assert.Equal(new[] { (1001, 2001), (1002, 2002), (1003, 2003) }, Pairs.ToArray());
            Assert.Equal(new[] { 1001, 1002, 1003 }, ServerRoi == null ? Roi.ParentImageIds.ToArray() : serverRoiImageIds.ToArray());
            Assert.Equal(3, Camera.Calls);
            Assert.Equal(3, ServerRoi == null ? Roi.Calls : serverRoiImageIds.Count);
            Assert.Equal(1, End.SuccessfulOutputs);
            Assert.Equal(3003, End.FinalMasterId);
            Assert.All(Members, node => Assert.Equal(STNodeGroupState.Completed, ((ISTNodeGroupMember)node).GroupState));
        }

        public void Dispose()
        {
            LocalJoin?.ReleasePending();
            Control.Dispose();
            container.Dispose();
        }
    }

    private sealed class ResultNode(bool roi) : CVCommonNode(roi ? "ROI fixture" : "Camera fixture", "JoinFixture", "JoinFixture")
    {
        private int calls;
        internal int Calls => Volatile.Read(ref calls);
        internal int HoldRound { get; set; }
        internal bool IncludeImageMarker { get; set; }
        internal Action<CVStartCFC>? DispatchOutput { get; set; }
        internal STNodeOption Input { get; private set; } = null!;
        internal STNodeOption Output { get; private set; } = null!;
        internal Channel<CVStartCFC> Pending { get; } = Channel.CreateUnbounded<CVStartCFC>();
        internal ConcurrentQueue<int> ParentImageIds { get; } = new();
        internal ConcurrentQueue<CVStartCFC> Published { get; } = new();

        protected override void OnCreate()
        {
            base.OnCreate();
            Input = InputOptions.Add("IN", typeof(CVStartCFC), true);
            Output = OutputOptions.Add("OUT", typeof(CVStartCFC), false);
            Input.DataTransfer += (_, args) =>
            {
                if (args.TargetOption.Data is not CVStartCFC incoming) return;
                if (!incoming.IsRunning) { Output.TransferData(incoming); return; }
                var action = new CVStartCFC(incoming);
                int round = Interlocked.Increment(ref calls);
                if (roi) ParentImageIds.Enqueue(Convert.ToInt32(incoming.Data["MasterId"]));
                action.MasterValue(null!, (roi ? 2000 : 1000) + round, roi ? 25 : 100);
                if (IncludeImageMarker) action.Data["Image"] = "camera-image-" + round;
                Published.Enqueue(action);
                if (round == HoldRound) Pending.Writer.TryWrite(action);
                else if (DispatchOutput != null) DispatchOutput(action);
                else Output.TransferData(action);
            };
        }
    }

    private sealed class PausedRoiAlgorithm : Algorithm2InNode
    {
        private int paused;
        internal TaskCompletionSource RoiAccepted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource AllowRoi { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override bool OnInputDataTransfer(STNodeOption input, STNodeOptionEventArgs e)
        {
            bool accepted = base.OnInputDataTransfer(input, e);
            if (accepted && input.Text == "IN_ROI" && e.TargetOption.Data is CVStartCFC { IsRunning: true }
                && Interlocked.CompareExchange(ref paused, 1, 0) == 0)
            {
                RoiAccepted.TrySetResult();
                AllowRoi.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            }
            return accepted;
        }
    }

    private sealed class ProbeExecution(int imageId, int roiId)
    {
        internal int ImageId { get; } = imageId;
        internal int RoiId { get; } = roiId;
        internal TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Exited { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class LocalJoinProbe(bool controlled, ConcurrentQueue<(int ImageId, int RoiId)> pairs)
        : LocalFlowNodeBase("Local join fixture", "LocalJoinFixture", "Join", "IN_IMG", "IN_ROI")
    {
        internal Channel<ProbeExecution> Executions { get; } = Channel.CreateUnbounded<ProbeExecution>();
        private readonly ConcurrentQueue<ProbeExecution> pending = new();

        protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
        {
            if (!TryGetInputMasterResult(action, 0, out int imageId, out int imageType, out _)
                || !TryGetInputMasterResult(action, 1, out int roiId, out int roiType, out _))
                throw new InvalidOperationException("The join lost one input snapshot.");
            Assert.Equal(100, imageType);
            Assert.Equal(25, roiType);
            var execution = new ProbeExecution(imageId, roiId);
            pending.Enqueue(execution);
            pairs.Enqueue((imageId, roiId));
            Executions.Writer.TryWrite(execution);
            try
            {
                if (controlled) execution.Continue.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                action.RuntimeResources.StopToken.ThrowIfCancellationRequested();
                action.MasterValue(null!, imageId + 2000, 250);
                return new LocalNodeExecutionResult();
            }
            finally { execution.Exited.TrySetResult(); }
        }

        internal void ReleasePending()
        {
            foreach (ProbeExecution execution in pending) execution.Continue.TrySetResult();
        }
    }

    private sealed class RecordingEndNode : CVEndNode
    {
        internal int SuccessfulOutputs, FinalMasterId;
        protected override void DoNodeEnded(CVStartCFC action)
        {
            if (action.IsRunning)
            {
                Interlocked.Increment(ref SuccessfulOutputs);
                FinalMasterId = Convert.ToInt32(action.Data["MasterId"]);
            }
            base.DoNodeEnded(action);
        }
    }
}
