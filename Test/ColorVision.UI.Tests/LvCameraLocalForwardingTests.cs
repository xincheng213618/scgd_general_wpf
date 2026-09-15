using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using FlowEngineLib;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ColorVision.UI.Tests;

public sealed class LvCameraLocalForwardingTests
{
    [Fact]
    public void ConsecutiveLvNodesUseCurrentLocalSessionAndKeepFlowResults() => Run(async () =>
    {
        using var scope = new CaptureScope(localOpen: true, preferLocal: false, expectedExecutions: 2);
        using var graph = new Graph(scope.CreateNode(), scope.CreateNode());
        graph.End.Inspect = action =>
        {
            Assert.True(action.TryAcquireCurrentFrame(out var lease));
            using (lease!) Assert.Equal(new byte[] { 11, 22 }, lease!.CopyRawToArray());
            Assert.Equal(102, action.Data["MasterId"]);
            Assert.Equal(100, action.Data["MasterResultType"]);
        };
        FlowEngineEventArgs finished = await graph.StartAsync();
        await scope.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(StatusTypeEnum.Completed, finished.Status);
        Assert.Equal(0, graph.Start.PublishCount);
        Assert.Equal(2, scope.Services.Requests.Count);
        Assert.Equal(2, scope.Services.Saved.Count);
        Assert.Equal(new[] { graph.Nodes[0].ZIndex, graph.Nodes[1].ZIndex }, scope.Services.Saved.Select(item => item.ZIndex));
        Assert.All(scope.Services.Saved, item => Assert.Equal(Graph.Serial, item.SerialNumber));
        Assert.Equal(graph.Nodes.Select(node => node.NodeID), scope.Services.PublishedNodeIds);
        Assert.Equal(2, graph.Runs.Count);
        Assert.Equal(2, graph.Ends.Count);
        Assert.Equal(graph.Runs.Select(run => run.SendMsgId).Order(), graph.Ends.Select(end => end.RecvMsgId).Order());
        foreach (LocalCameraCaptureRequest request in scope.Services.Requests)
        {
            Assert.Equal(42, request.CameraParameters!.ExpTime);
            Assert.Equal(42, request.CameraParameters.ExpTimeR);
            Assert.Equal(17, request.CameraParameters.Gain);
            Assert.Equal(3, request.CameraParameters.AvgCount);
            Assert.Equal(CVImageFlipMode.Y, request.FlipMode);
            Assert.True(request.SaveFiles);
            Assert.False(request.SaveCieFile);
        }
        Assert.All(scope.Services.Frames, frame => Assert.Throws<ObjectDisposedException>(() => frame.Acquire()));
    });

    [Fact]
    public void LocalPreferenceAloneKeepsLvServiceRequestAndPoiParameters() => Run(async () =>
    {
        using var scope = new CaptureScope(localOpen: false, preferLocal: true);
        scope.Backend.ObserveService(DeviceStatusType.Opened);
        using var graph = new Graph(scope.CreateNode());
        graph.Start.OnPublish = message =>
        {
            var request = JsonConvert.DeserializeObject<CVMQTTRequest>(message.Message)!;
            var data = (JObject)request.Data;
            Assert.Equal("missing-poi", data["POIParam"]!["POI"]!["Name"]);
            Assert.Equal("missing-filter", data["POIParam"]!["Filter"]!["Name"]);
            Assert.Equal("missing-revise", data["POIParam"]!["Revise"]!["Name"]);
            Assert.Equal(42, data["ExpTime"]![0]);
            Respond(graph.Nodes[0], request);
        };
        Assert.Equal(StatusTypeEnum.Completed, (await graph.StartAsync()).Status);
        Assert.Equal(1, graph.Start.PublishCount);
        Assert.Empty(scope.Services.Requests);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClosedCameraUsesNextOpenPreferenceForLvCapture(bool preferLocal) => Run(async () =>
    {
        using var scope = new CaptureScope(localOpen: false, preferLocal: preferLocal);
        scope.Backend.ObserveService(DeviceStatusType.Closed);
        using var graph = new Graph(scope.CreateNode());
        graph.Start.OnPublish = message => Respond(graph.Nodes[0], JsonConvert.DeserializeObject<CVMQTTRequest>(message.Message)!);

        Assert.Equal(StatusTypeEnum.Completed, (await graph.StartAsync()).Status);
        if (preferLocal)
        {
            await scope.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(0, graph.Start.PublishCount);
            Assert.Single(scope.Services.Requests);
            Assert.Single(scope.Services.Saved);
        }
        else
        {
            Assert.Equal(1, graph.Start.PublishCount);
            Assert.Empty(scope.Services.Requests);
        }
    });

    [Fact]
    public void CvNodeRemainsOnServiceEvenWhenLocalCameraIsOpen() => Run(async () =>
    {
        using var scope = new CaptureScope(localOpen: true, preferLocal: true);
        using var graph = new Graph(new CVCameraNode());
        graph.Start.OnPublish = message => Respond(graph.Nodes[0], JsonConvert.DeserializeObject<CVMQTTRequest>(message.Message)!);
        Assert.Equal(StatusTypeEnum.Completed, (await graph.StartAsync()).Status);
        Assert.Equal(1, graph.Start.PublishCount);
        Assert.Empty(scope.Services.Requests);
    });

    [Fact]
    public void WithoutMatchingLocalDeviceLvKeepsOriginalServiceRequest() => Run(async () =>
    {
        using var scope = new CaptureScope(localOpen: false, preferLocal: true);
        using var graph = new Graph(new LVCameraNode());
        graph.Start.OnPublish = message =>
        {
            var request = JsonConvert.DeserializeObject<CVMQTTRequest>(message.Message)!;
            var data = (JObject)request.Data;
            Assert.Equal("missing-poi", data["POIParam"]!["POI"]!["Name"]);
            Assert.Equal(42, data["ExpTime"]![0]);
            Respond(graph.Nodes[0], request);
        };
        Assert.Equal(StatusTypeEnum.Completed, (await graph.StartAsync()).Status);
        Assert.Equal(1, graph.Start.PublishCount);
        Assert.Empty(scope.Services.Requests);
    });

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void LocalCaptureOrPersistenceFailureDoesNotFallBackToMqtt(bool localOpen, bool failSave) => Run(async () =>
    {
        using var scope = new CaptureScope(localOpen: localOpen, preferLocal: true);
        scope.Services.FailCapture = !failSave;
        scope.Services.FailSave = failSave;
        using var graph = new Graph(scope.CreateNode());
        Assert.Equal(StatusTypeEnum.Failed, (await graph.StartAsync()).Status);
        await scope.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, graph.Start.PublishCount);
        Assert.Single(graph.Ends);
        Assert.Empty(scope.Services.PublishedNodeIds);
        Assert.All(scope.Services.Frames, frame => Assert.Throws<ObjectDisposedException>(() => frame.Acquire()));
        scope.Backend.BeginLocalCommand();
        scope.Backend.EndLocalCommand();
    });

    [Fact]
    public void StopDuringCaptureDiscardsLateFrameWithoutSavingOrContinuing() => Run(async () =>
    {
        using var scope = new CaptureScope(localOpen: true, preferLocal: true);
        scope.Services.BlockCapture = true;
        using var graph = new Graph(scope.CreateNode());
        Task<FlowEngineEventArgs> completion = graph.StartAsync();
        await scope.Services.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        // Changing the next-open preference does not change this in-flight request.
        scope.Backend.SetPreference(false);
        graph.Control.StopNode(graph.Start.NodeName, Graph.Serial);
        Assert.Equal(StatusTypeEnum.Canceled, (await completion).Status);
        scope.Services.Release.TrySetResult();
        await scope.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, graph.Start.PublishCount);
        Assert.Empty(scope.Services.Saved);
        Assert.Empty(scope.Services.PublishedNodeIds);
        Assert.Single(graph.Ends);
        Assert.All(scope.Services.Frames, frame => Assert.Throws<ObjectDisposedException>(() => frame.Acquire()));
    });

    [Fact]
    public void ExpiredLocalCommandCannotPublishAResult() => Run(async () =>
    {
        using var scope = new CaptureScope(localOpen: true, preferLocal: true);
        using var graph = new Graph(scope.CreateNode(immediateTimeout: true));
        Assert.Equal(StatusTypeEnum.OverTime, (await graph.StartAsync()).Status);
        await scope.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, graph.Start.PublishCount);
        Assert.Empty(scope.Services.Saved);
        Assert.Single(graph.Ends);
    });

    [Fact]
    public void MissingCalibrationFailsLocallyAndSaveOptOutIsRetained() => Run(async () =>
    {
        using var scope = new CaptureScope(localOpen: true, preferLocal: true);
        scope.Camera.DisplayConfig.SaveLocalCaptureFiles = false;
        using var graph = new Graph(scope.CreateNode());
        ((LVCameraNode)graph.Nodes[0]).CaliTempName = "missing-calibration";
        Assert.Equal(StatusTypeEnum.Failed, (await graph.StartAsync()).Status);
        Assert.Equal(0, graph.Start.PublishCount);
        Assert.Empty(scope.Services.Requests);
        CameraData parameters = new(CVImageFlipMode.None, false, 0, 0, 1, 2, [10], "", "missing-poi", "missing-filter", "missing-revise", "");
        Assert.False(LocalLvCameraExecution.BuildCaptureRequest(scope.Camera, parameters).SaveFiles);
    });

    private static void Respond(CVBaseServerNode node, CVMQTTRequest request) => Assert.True(node.DoServerStatusRecv(new CVBaseDataFlowResp
    {
        MsgID = request.MsgID, SerialNumber = request.SerialNumber, DeviceNodeCode = request.DeviceNodeCode,
        EventName = request.EventName, ServiceName = request.ServiceCode, ZIndex = request.ZIndex,
        Code = 0, Message = "ok", Data = new { MasterId = 7, MasterResultType = 100 }
    }));

    private static void Run(Func<Task> test) => StaTest.Run(() => test().GetAwaiter().GetResult());

    private sealed class TestLvNode(CaptureScope scope, bool immediateTimeout) : LVCameraNode
    {
        protected override int GetMaxDelay() => immediateTimeout ? 0 : base.GetMaxDelay();
        protected override FlowLocalExecution? CreateLocalExecution(CVMQTTRequest request) => scope.CreateExecution(request);
    }

    private sealed class InspectEndNode : CVEndNode
    {
        public Action<CVStartCFC>? Inspect;
        protected override void DoNodeEnded(CVStartCFC action)
        {
            if (action.IsRunning) Inspect?.Invoke(action);
            base.DoNodeEnded(action);
        }
    }

    private sealed class Graph : IDisposable
    {
        public const string Serial = "SN-LOCAL-LV-FORWARD";
        private readonly CVNodeContainer container = new();
        public RuntimeTestStartNode Start { get; } = new();
        public InspectEndNode End { get; } = new();
        public CVBaseServerNode[] Nodes { get; }
        public FlowEngineControl Control { get; }
        public ConcurrentQueue<FlowEngineNodeRunEventArgs> Runs { get; } = new();
        public ConcurrentQueue<FlowEngineNodeEndEventArgs> Ends { get; } = new();
        private readonly TaskCompletionSource<FlowEngineEventArgs> completed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Graph(params CVBaseServerNode[] nodes)
        {
            Nodes = nodes;
            Start.Create(); End.Create();
            container.Nodes.Add(Start);
            STNodeOption output = Start.m_op_start;
            foreach (CVBaseServerNode node in nodes)
            {
                node.Create();
                node.DeviceCode = "lv-test";
                if (node is LVCameraNode lv)
                {
                    lv.ExpTime = 42; lv.Gain = 17; lv.AvgCount = 3; lv.FlipMode = CVImageFlipMode.Y;
                    lv.POITempName = "missing-poi"; lv.POIFilterTempName = "missing-filter"; lv.POIReviseTempName = "missing-revise";
                }
                container.Nodes.Add(node);
                Assert.Equal(ConnectionStatus.Connected, output.ConnectOption(node.GetAllInputOptions()[0], false));
                output = node.GetAllOutputOptions()[0];
                node.nodeRunEvent += (_, args) => Runs.Enqueue(args);
                node.nodeEndEvent += (_, args) => Ends.Enqueue(args);
            }
            container.Nodes.Add(End);
            Assert.Equal(ConnectionStatus.Connected, output.ConnectOption(End.m_in_start, false));
            Control = new FlowEngineControl(container, false, new FlowNodeManager());
            Control.Finished += (_, args) => completed.TrySetResult(args);
        }

        public Task<FlowEngineEventArgs> StartAsync()
        {
            Assert.True(Control.TryStartNode(Start.NodeName, Serial));
            return completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        public void Dispose() { Control.Dispose(); container.Dispose(); }
    }

    private sealed class CaptureScope : IDisposable
    {
        private readonly IConfigService previousConfig = ConfigService.Instance;
        private readonly int expectedExecutions;
        private int disposedExecutions;
        public DeviceCamera Camera { get; }
        public CameraBackendState Backend { get; }
        public FakeServices Services { get; } = new();
        public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CaptureScope(bool localOpen, bool preferLocal, int expectedExecutions = 1)
        {
            this.expectedExecutions = expectedExecutions;
            ConfigService.SetInstance(new ConfigHandler());
            Camera = (DeviceCamera)RuntimeHelpers.GetUninitializedObject(typeof(DeviceCamera));
            Camera.Config = new() { Code = "lv-test", IsCVCIEFileSave = false };
            Camera.SysResourceModel = new SysResourceModel { Code = "lv-test" };
            Backend = new CameraBackendState(preferLocal);
            if (localOpen) { Backend.BeginLocalOpen(); Backend.SetLocalStatus(DeviceStatusType.Opened); }
            typeof(DeviceCamera).GetField("<CameraBackend>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Camera, Backend);
        }

        public LVCameraNode CreateNode(bool immediateTimeout = false) => new TestLvNode(this, immediateTimeout);

        public FlowLocalExecution? CreateExecution(CVMQTTRequest request)
        {
            FlowLocalExecution? execution = LocalLvCameraExecution.CreateForDevice(Camera, request, Services);
            return execution == null ? null : new TrackedExecution(execution, () =>
            {
                if (System.Threading.Interlocked.Increment(ref disposedExecutions) == expectedExecutions)
                    Disposed.TrySetResult();
            });
        }

        public void Dispose()
        {
            Services.Release.TrySetResult();
            ConfigService.SetInstance(previousConfig);
        }
    }

    private sealed class TrackedExecution(FlowLocalExecution inner, Action disposed) : FlowLocalExecution
    {
        public override void Execute() => inner.Execute();
        public override object Complete(CVStartCFC action) => inner.Complete(action);
        public override void Dispose() { inner.Dispose(); disposed(); }
    }

    private sealed class FakeServices : ILocalLvCameraServices
    {
        public bool FailCapture, FailSave, BlockCapture;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<LocalCameraCaptureRequest> Requests { get; } = [];
        public List<LocalFlowFrame> Frames { get; } = [];
        public List<(string SerialNumber, int ZIndex)> Saved { get; } = [];
        public List<string> PublishedNodeIds { get; } = [];
        public LocalCameraCaptureResult Capture(LocalCameraCaptureRequest request)
        {
            Requests.Add(request);
            Started.TrySetResult();
            if (BlockCapture) Release.Task.GetAwaiter().GetResult();
            if (FailCapture) throw new InvalidOperationException("capture failed");
            var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata { Width = 2, Height = 1, SourceBpp = 8, Channels = 1 }, 2, 0);
            using (var lease = frame.Acquire()) Marshal.Copy(new byte[] { 11, 22 }, 0, lease.RawPointer, 2);
            Frames.Add(frame);
            return new LocalCameraCaptureResult { Frame = frame };
        }
        public MeasureResultImgModel Save(CVStartCFC action, int zIndex, LocalCameraCaptureRequest request, LocalCameraCaptureResult capture)
        {
            if (FailSave) throw new InvalidOperationException("save failed");
            Saved.Add((action.SerialNumber, zIndex));
            return new MeasureResultImgModel { Id = 100 + Saved.Count, ZIndex = zIndex, BatchId = 55 };
        }
        public void Publish(CVStartCFC action, string nodeId, int zIndex, LocalCameraCaptureRequest request, LocalCameraCaptureResult capture, MeasureResultImgModel model)
            => PublishedNodeIds.Add(nodeId);
    }
}
