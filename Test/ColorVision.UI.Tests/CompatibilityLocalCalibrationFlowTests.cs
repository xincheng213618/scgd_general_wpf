using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Templates.POI;
using FlowEngineLib;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using FlowEngineLib.Node.POI;
using Newtonsoft.Json.Linq;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ColorVision.UI.Tests;

public sealed class CompatibilityLocalCalibrationFlowTests
{
    // Protect old canvas routing, image ownership and result identities across the backend switch.
    [Fact]
    public void BothNodesFollowTheirImageBranchCalibrationPreference() => StaTest.Run(() =>
    {
        using var scope = new DeviceScope();
        using var canvas = new CVNodeContainer();
        var calibration = new CalibrationNode { DeviceCode = scope.First.Code };
        var poi = new RealPOINode();
        foreach (STNode node in new STNode[] { calibration, poi }) { node.Create(); canvas.Nodes.Add(node); }
        calibration.GetAllOutputOptions()[0].ConnectOption(poi.GetAllInputOptions()[0], false);
        scope.Second.DisplayConfig.UseLocalCalibration = false;
        foreach (bool local in new[] { true, false })
        {
            scope.First.DisplayConfig.UseLocalCalibration = local;
            Assert.Equal(!local, calibration.RequiresRemoteService);
            Assert.Equal(!local, poi.RequiresRemoteService);
            CVMQTTRequest request = CalibrationRequest();
            request.DeviceCode = scope.First.Code;
            using var execution = CreateExecution(calibration, request);
            Assert.Equal(local, execution != null);
        }
        // An explicit device remains authoritative even when another service is enabled.
        scope.First.DisplayConfig.UseLocalCalibration = true;
        calibration.DeviceCode = scope.Second.Code;
        Assert.True(poi.RequiresRemoteService);
        calibration.DeviceCode = "missing-device";
        Assert.True(poi.RequiresRemoteService);
    });

    [Fact]
    public void PoiWithoutCalibrationBranchUsesTheOnlyServiceAndKeepsAdvancedRequest() => StaTest.Run(() =>
    {
        using var scope = new DeviceScope();
        scope.Manager.DeviceServices.Remove(scope.Second);
        var poi = new RealPOINode();
        poi.Create();
        poi.FilterTemplateName = "missing-filter";
        poi.ReviseTemplateName = "missing-revise";
        poi.ReviseFileName = "missing-file";
        poi.OutputTemplateName = "missing-output";
        poi.SubPixelTemplateName = "missing-subpixel";
        scope.First.DisplayConfig.UseLocalCalibration = false;
        Assert.True(poi.RequiresRemoteService);
        var data = (RealPOIData)typeof(RealPOINode).GetMethod("getBaseEventData", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(poi, [new CVStartCFC("request")])!;
        Assert.Equal("missing-filter", data.FilterTemplate.Name);
        Assert.Equal("missing-revise", data.ReviseTemplate.Name);
        Assert.Equal("missing-file", data.POIReviseFileName);
        Assert.Equal("missing-output", data.OutputTemplate.Name);
        Assert.Equal("missing-subpixel", data.SubPixelTemplate.Name);
        Assert.Null(CreateExecution(poi, new CVMQTTRequest { Data = data }));
        scope.First.DisplayConfig.UseLocalCalibration = true;
        Assert.False(poi.RequiresRemoteService);
    });

    [Fact]
    public void BridgeUsesImageInputAndCommitsTheExistingLocalNodeResult()
    {
        var image = new CVStartCFC("local-contract");
        using var resources = image.RuntimeResources;
        image.SetCurrentFrame(CreateCieFrame(7));
        image.MasterValue(null, 301, 30);
        var poi = new CVStartCFC(image);
        poi.SetCurrentFrame(CreateCieFrame(99));
        poi.MasterValue(null, 10, 10);
        var node = new ProbeNode();
        using var execution = node.CreateCompatibilityExecution(PoiRequest(301), null, image, poi);
        execution.Bind(poi);
        execution.Execute();
        Assert.Equal(10, poi.Data["MasterId"]);
        JObject result = JObject.FromObject(execution.Complete(poi)!);
        Assert.Equal(7, (float)result["Y"]!);
        Assert.Equal(301, (int)result["ImageMasterId"]!);
        Assert.Equal(10, (int)result["PoiMasterId"]!);
        Assert.Equal("poi-node", (string?)result["NodeId"]);
        Assert.Equal(4, (int)result["ZIndex"]!);
        Assert.Equal(1801, poi.Data["MasterId"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyPoiGraphUsesTheImagePortInEitherArrivalOrder(bool imageFirst) => StaTest.Run(() =>
    {
        using var scope = new DeviceScope();
        scope.Manager.DeviceServices.Remove(scope.Second);
        using var canvas = new CVNodeContainer();
        var start = new RuntimeTestStartNode();
        var image = new SeedNode(true);
        var layout = new SeedNode(false);
        var probe = new ProbeNode();
        var poi = new InternalPoiNode(probe);
        var end = new CVEndNode();
        foreach (STNode node in new STNode[] { start, image, layout, poi, end }) { node.Create(); canvas.Nodes.Add(node); }
        foreach (SeedNode seed in imageFirst ? new[] { image, layout } : new[] { layout, image })
            start.m_op_start.ConnectOption(seed.GetAllInputOptions()[0], false);
        image.GetAllOutputOptions()[0].ConnectOption(poi.GetAllInputOptions()[0], false);
        layout.GetAllOutputOptions()[0].ConnectOption(poi.GetAllInputOptions()[1], false);
        poi.GetAllOutputOptions()[0].ConnectOption(end.m_in_start, false);
        using var control = new FlowEngineControl(canvas, false, new FlowNodeManager()) { PersistResults = false };
        var completed = new TaskCompletionSource<FlowEngineEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        control.Finished += (_, result) => completed.TrySetResult(result);
        Assert.True(control.TryStartNode(start.NodeName, "local-contract"));
        Assert.Equal(StatusTypeEnum.Completed, completed.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult().Status);
        Assert.Equal(0, start.PublishCount);
        Assert.Equal(7, probe.Y);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BridgeRejectsStoppedWorkAndDoesNotCommitResults(bool stopBeforeExecute)
    {
        var action = new CVStartCFC("local-contract");
        LocalFlowFrame frame = CreateCieFrame(5);
        action.SetCurrentFrame(frame);
        var node = new ProbeNode();
        using var execution = node.CreateCompatibilityExecution(PoiRequest(-1), null, action, action);
        execution.Bind(action);
        if (!stopBeforeExecute) execution.Execute();
        action.RuntimeResources.Dispose();
        if (stopBeforeExecute) Assert.Throws<OperationCanceledException>(execution.Execute);
        Assert.Throws<OperationCanceledException>(() => execution.Complete(action));
        Assert.False(action.Data.ContainsKey("LocalProbe"));
        Assert.Throws<ObjectDisposedException>(() => frame.Acquire());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InternalFailureCompletesLegacyNodeWithoutPublishingMqtt(bool poi) => StaTest.Run(() =>
    {
        using var scope = new DeviceScope();
        using var canvas = new CVNodeContainer();
        var start = new RuntimeTestStartNode();
        scope.Manager.DeviceServices.Remove(scope.Second);
        CVBaseServerNode node = poi ? new RealPOINode() : new CalibrationNode();
        node.DeviceCode = scope.First.Code;
        var end = new CVEndNode();
        foreach (STNode item in new STNode[] { start, node, end }) { item.Create(); canvas.Nodes.Add(item); }
        foreach (STNodeOption input in node.GetAllInputOptions()) start.m_op_start.ConnectOption(input, false);
        node.GetAllOutputOptions()[0].ConnectOption(end.m_in_start, false);
        using var control = new FlowEngineControl(canvas, false, new FlowNodeManager()) { PersistResults = false };
        var completed = new TaskCompletionSource<FlowEngineEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        control.Finished += (_, result) => completed.TrySetResult(result);
        Assert.True(control.TryStartNode(start.NodeName, "local-failure"));
        Assert.Equal(StatusTypeEnum.Failed, completed.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult().Status);
        Assert.Equal(0, start.PublishCount);
    });

    [Theory]
    [InlineData(POIPointTypes.None, PoiShape.Point)]
    [InlineData(POIPointTypes.SubPixel, PoiShape.Point)]
    [InlineData(POIPointTypes.Rect, PoiShape.Rect)]
    [InlineData(POIPointTypes.Circle, PoiShape.Circle)]
    public void BasicPointOverridesDoNotMutateUpstreamPoi(POIPointTypes type, PoiShape expected)
    {
        PoiParam source = CreatePoi();
        PoiParam measured = LocalRealPoiNode.ApplyPointType(source, new POITypeData { PointType = type, Width = 4, Height = 2 });
        Assert.Equal(expected, measured.PoiPoints.Single().PointType);
        Assert.Equal(PoiShape.Point, source.PoiPoints.Single().PointType);
    }

    private static LocalFlowFrame CreateCieFrame(float value)
    {
        LocalFlowFrame frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
            { Width = 1, Height = 1, Channels = 1, SourceBpp = 16, PrimaryBufferKind = LocalFrameBufferKind.CvCie, IsMirrorReady = true }, 0, sizeof(float));
        using var lease = frame.Acquire();
        Marshal.Copy(new[] { value }, 0, lease.CiePointer, 1);
        return frame;
    }

    private static PoiParam CreatePoi()
    {
        PoiParam poi = new() { Id = 10, Name = "upstream" };
        poi.PoiPoints.Add(new PoiPoint { Id = 1, Name = "center", PointType = PoiShape.Point, PixWidth = 1, PixHeight = 1 });
        return poi;
    }

    private static CVMQTTRequest CalibrationRequest() => new()
    {
        SerialNumber = "local-contract", EventName = "Calibration", DeviceNodeCode = "calibration-node", ZIndex = 3,
        Data = new CalibrationData("", new AlgorithmPreStepParam(), true) { TemplateParam = new CVTemplateParam { Name = "selected-calibration" } }
    };

    private static CVMQTTRequest PoiRequest(int imageMasterId) => new()
    {
        SerialNumber = "local-contract", EventName = "Real_POI", DeviceNodeCode = "poi-node", ZIndex = 4,
        Data = new RealPOIData("", "missing-filter", "missing-revise", "missing-file", "missing-output", "missing-subpixel",
            new POITypeData { PointType = POIPointTypes.SubPixel }, imageMasterId, 30, 10, true, true, false)
    };

    private static FlowLocalExecution? CreateExecution(CVBaseServerNode node, CVMQTTRequest request)
        => (FlowLocalExecution?)node.GetType().GetMethod("CreateLocalExecution", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(node, [request]);

    private sealed class ProbeNode() : LocalFlowNodeBase("Probe", "Probe", "Probe", "IN_CIE", "IN_POI")
    {
        internal float Y;
        protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
        {
            Assert.True(action.TryAcquireCurrentFrame(out LocalFlowFrameLease? frame));
            using (frame)
            {
                float[] values = new float[1];
                Marshal.Copy(frame!.CiePointer, values, 0, 1);
                Y = values[0];
            }
            TryGetInputMasterResult(action, 0, out int imageMasterId, out _, out _);
            TryGetInputMasterResult(action, 1, out int poiMasterId, out _, out _);
            action.Data["LocalProbe"] = true;
            action.MasterValue(null, 1801, 18);
            return new LocalNodeExecutionResult { Data = new { Y, ImageMasterId = imageMasterId, PoiMasterId = poiMasterId, NodeId = ExecutionNodeId, ZIndex } };
        }
    }

    private sealed class InternalPoiNode(ProbeNode probe) : RealPOINode
    {
        protected override FlowLocalExecution CreateLocalExecution(CVMQTTRequest request)
            => probe.CreateCompatibilityExecution(request, null, GetInputAction(0), GetInputAction(1));
    }

    private sealed class SeedNode(bool image) : STNode
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            STNodeOption input = InputOptions.Add("IN", typeof(CVStartCFC), true);
            STNodeOption output = OutputOptions.Add("OUT", typeof(CVStartCFC), false);
            input.DataTransfer += (_, e) =>
            {
                if (e.TargetOption.Data is not CVStartCFC action || !action.IsRunning) return;
                var branch = new CVStartCFC(action);
                LocalFlowFrame frame = CreateCieFrame(image ? 7 : 99);
                frame.MasterId = image ? 301 : 102;
                branch.SetCurrentFrame(frame);
                branch.MasterValue(null, image ? 301 : 10, image ? 100 : 10);
                output.TransferData(branch);
            };
        }
    }

    private sealed class DeviceScope : IDisposable
    {
        private readonly FieldInfo managerField = typeof(ServiceManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? previous;
        public ServiceManager Manager { get; }
        public DeviceCalibration First { get; } = CreateDevice();
        public DeviceCalibration Second { get; } = CreateDevice();
        public DeviceScope()
        {
            previous = managerField.GetValue(null);
            Manager = (ServiceManager)RuntimeHelpers.GetUninitializedObject(typeof(ServiceManager));
            Manager.DeviceServices = [First, Second];
            managerField.SetValue(null, Manager);
        }
        private static DeviceCalibration CreateDevice()
        {
            var device = (DeviceCalibration)RuntimeHelpers.GetUninitializedObject(typeof(DeviceCalibration));
            string code = "synthetic-calibration-" + Guid.NewGuid().ToString("N");
            device.Config = new ConfigCalibration { Code = code };
            device.SysResourceModel = new SysResourceModel { Code = code };
            return device;
        }
        public void Dispose()
        {
            managerField.SetValue(null, previous);
            DisplayConfigManager.Instance.Configs.TryRemove(First.Code, out _);
            DisplayConfigManager.Instance.Configs.TryRemove(Second.Code, out _);
        }
    }
}
