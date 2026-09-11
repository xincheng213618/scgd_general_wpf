using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Testing;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.PropertyEditor;
using FlowEngineLib.Start;
using Newtonsoft.Json;
using ProjectARVRPro.Process;
using ST.Library.UI.NodeEditor;
using System.ComponentModel;
using Xunit;

namespace ProjectARVRPro.Tests;

public sealed class FlowCameraParameterOverrideServiceTests
{
    [Fact]
    public void OverrideConfigReusesTheNodeCalibrationEditorWithoutPersistingDeviceContext()
    {
        var config = new FlowCameraParameterOverrideConfig();
        config.SetEditorDeviceCode("CameraA");
        var property = typeof(FlowCameraParameterOverrideConfig)
            .GetProperty(nameof(FlowCameraParameterOverrideConfig.CalibrationTemplateName));
        PropertyEditorTypeAttribute attribute = Assert.IsType<PropertyEditorTypeAttribute>(
            Assert.Single(property!.GetCustomAttributes(typeof(PropertyEditorTypeAttribute), inherit: false)));

        Assert.Equal(typeof(FlowCalibrationTemplateEditor), attribute.EditorType);
        string json = JsonConvert.SerializeObject(config);
        Assert.DoesNotContain(nameof(FlowCameraParameterOverrideConfig.DeviceCode), json, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(FlowCameraParameterOverrideConfig.NodeType), json, StringComparison.Ordinal);
    }

    [Fact]
    public void CanvasSnapshotReadsConfiguredNodesWithoutConnectingAndDisposesThem()
    {
        StaTest.Run(() =>
        {
            byte[] canvasData;
            using (var source = new STNodeEditor())
            {
                CanvasSnapshotLifecycleStartNode start = Create(new CanvasSnapshotLifecycleStartNode());
                LVCameraNode camera = Create(new LVCameraNode());
                source.Nodes.Add(start);
                source.Nodes.Add(camera);
                Connect(start, camera);
                canvasData = source.GetCanvasData();
            }

            CanvasSnapshotLifecycleStartNode.ConnectionCallbackCount = 0;
            CanvasSnapshotLifecycleStartNode.PropertyLoadCount = 0;
            CanvasSnapshotLifecycleStartNode.EditorLoadCompletedCount = 0;
            CanvasSnapshotLifecycleStartNode.DisposeCount = 0;

            using (STNodeCanvasSnapshot snapshot = STNodeEditor.ReadCanvasSnapshot(canvasData))
            {
                CanvasSnapshotLifecycleStartNode restoredStart = Assert.Single(snapshot.Nodes.OfType<CanvasSnapshotLifecycleStartNode>());
                LVCameraNode restoredCamera = Assert.Single(snapshot.Nodes.OfType<LVCameraNode>());

                Assert.Contains(restoredCamera, snapshot.GetConnectedOutputNodes(restoredStart));
                Assert.Equal(1, CanvasSnapshotLifecycleStartNode.PropertyLoadCount);
                Assert.Equal(0, CanvasSnapshotLifecycleStartNode.ConnectionCallbackCount);
                Assert.Equal(0, CanvasSnapshotLifecycleStartNode.EditorLoadCompletedCount);
                Assert.Equal(0, CanvasSnapshotLifecycleStartNode.DisposeCount);
            }

            Assert.Equal(1, CanvasSnapshotLifecycleStartNode.DisposeCount);
        });
    }

    [Fact]
    public void DisabledOverrideDoesNotInspectOrModifyTheFlow()
    {
        var config = new FlowCameraParameterOverrideConfig
        {
            IsEnabled = false,
            ExposureTimeMs = 80,
            CalibrationTemplateName = "ExternalCalibration"
        };

        FlowCameraParameterOverrideResult result =
            FlowCameraParameterOverrideService.ApplyToLoadedFlow(config, Array.Empty<STNode>(), null);

        Assert.False(result.Applied);
        Assert.Equal(string.Empty, result.Message);
    }

    [Fact]
    public void EnabledOverrideDirectlyModifiesTheOnlyLvCameraNode()
    {
        LVCameraNode camera = Create(new LVCameraNode());
        camera.ExpTime = 15;
        camera.CaliTempName = "EmbeddedCalibration";
        var config = CreateEnabledConfig();
        config.CalibrationTemplateName = "ExternalCalibration";

        FlowCameraParameterOverrideResult result = ApplyToPath(config, camera);

        Assert.True(result.Applied, result.Message);
        Assert.Equal(80, camera.ExpTime);
        Assert.Equal("ExternalCalibration", camera.CaliTempName);
    }

    [Fact]
    public void EnabledOverrideDirectlyModifiesTheOnlyLocalCameraNode()
    {
        LocalCameraNode camera = Create(new LocalCameraNode());
        camera.ExpTime = 20;
        camera.CalibTempName = "EmbeddedCalibration";
        var config = CreateEnabledConfig();

        FlowCameraParameterOverrideResult result = ApplyToPath(config, camera);

        Assert.True(result.Applied, result.Message);
        Assert.Equal(80, camera.ExpTime);
        Assert.Equal(string.Empty, camera.CalibTempName);
    }

    [Fact]
    public void MissingCameraSkipsOverride()
    {
        FlowCameraParameterOverrideResult result =
            ApplyToPath(CreateEnabledConfig());

        Assert.False(result.Applied);
        Assert.Contains("已跳过", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MultipleCamerasSkipOverrideWithoutChangingEitherNode()
    {
        LVCameraNode lvCamera = Create(new LVCameraNode());
        LocalCameraNode localCamera = Create(new LocalCameraNode());
        lvCamera.ExpTime = 15;
        lvCamera.CaliTempName = "LvCalibration";
        localCamera.ExpTime = 20;
        localCamera.CalibTempName = "LocalCalibration";

        FlowCameraParameterOverrideResult result = ApplyToPath(CreateEnabledConfig(), lvCamera, localCamera);

        Assert.False(result.Applied);
        Assert.Contains("已跳过", result.Message, StringComparison.Ordinal);
        Assert.Equal(15, lvCamera.ExpTime);
        Assert.Equal("LvCalibration", lvCamera.CaliTempName);
        Assert.Equal(20, localCamera.ExpTime);
        Assert.Equal("LocalCalibration", localCamera.CalibTempName);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void InvalidExposureSkipsOverride(float exposureTimeMs)
    {
        LVCameraNode camera = Create(new LVCameraNode());
        camera.ExpTime = 15;
        camera.CaliTempName = "EmbeddedCalibration";
        FlowCameraParameterOverrideConfig config = CreateEnabledConfig();
        config.ExposureTimeMs = exposureTimeMs;

        FlowCameraParameterOverrideResult result = ApplyToPath(config, camera);

        Assert.False(result.Applied);
        Assert.Equal(15, camera.ExpTime);
        Assert.Equal("EmbeddedCalibration", camera.CaliTempName);
    }

    [Fact]
    public void UnexpectedInspectionFailureSkipsOverride()
    {
        IEnumerable<STNode> nodes = Enumerable.Range(0, 1)
            .Select<int, STNode>(_ => throw new InvalidOperationException("inspection failed"));

        FlowCameraParameterOverrideResult result = FlowCameraParameterOverrideService.ApplyToLoadedFlow(
            CreateEnabledConfig(),
            nodes,
            "Start");

        Assert.False(result.Applied);
        Assert.Contains("已跳过", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DisconnectedCameraDoesNotTurnOneAcquisitionIntoMultipleCameras()
    {
        LVCameraNode connectedCamera = Create(new LVCameraNode());
        connectedCamera.ExpTime = 15;
        LocalCameraNode disconnectedCamera = Create(new LocalCameraNode());
        disconnectedCamera.ExpTime = 20;
        CameraOverrideTestStartNode start = Create(new CameraOverrideTestStartNode());
        Connect(start, connectedCamera);

        FlowCameraParameterOverrideResult result = FlowCameraParameterOverrideService.ApplyToLoadedFlow(
            CreateEnabledConfig(),
            new STNode[] { start, connectedCamera, disconnectedCamera },
            start.NodeName);

        Assert.True(result.Applied, result.Message);
        Assert.Equal(80, connectedCamera.ExpTime);
        Assert.Equal(20, disconnectedCamera.ExpTime);
    }

    [Fact]
    public void UnknownCalibrationTemplateSkipsWithoutPartiallyChangingExposure()
    {
        LocalCameraNode camera = Create(new LocalCameraNode());
        camera.DeviceCode = $"MissingCamera-{Guid.NewGuid():N}";
        camera.ExpTime = 15;
        camera.CalibTempName = "EmbeddedCalibration";
        FlowCameraParameterOverrideConfig config = CreateEnabledConfig();
        config.CalibrationTemplateName = $"MissingCalibration-{Guid.NewGuid():N}";

        FlowCameraParameterOverrideResult result = ApplyToPath(config, camera);

        Assert.False(result.Applied);
        Assert.Contains("已跳过", result.Message, StringComparison.Ordinal);
        Assert.Equal(15, camera.ExpTime);
        Assert.Equal("EmbeddedCalibration", camera.CalibTempName);
    }

    [Fact]
    public void ApplyingValuesDoesNotSaveThemBackToTheFlowTemplate()
    {
        StaTest.Run(() =>
        {
            using var source = new STNodeEditor();
            CameraOverrideTestStartNode sourceStart = Create(new CameraOverrideTestStartNode());
            LVCameraNode sourceCamera = Create(new LVCameraNode());
            sourceCamera.ExpTime = 21;
            sourceCamera.CaliTempName = "EmbeddedCalibration";
            source.Nodes.Add(sourceStart);
            source.Nodes.Add(sourceCamera);
            Connect(sourceStart, sourceCamera);
            string canvasBase64 = Convert.ToBase64String(source.GetCanvasData());

            using var editor = new STNodeEditor();
            using var engine = new FlowEngineControl(false);
            engine.AttachNodeEditor(editor);
            engine.LoadFromBase64(canvasBase64);
            LVCameraNode runtimeCamera = Assert.Single(editor.Nodes.OfType<LVCameraNode>());

            FlowCameraParameterOverrideResult result = FlowCameraParameterOverrideService.ApplyToLoadedFlow(
                CreateEnabledConfig(),
                editor.Nodes.Cast<STNode>(),
                engine.GetStartNodeName());

            Assert.True(result.Applied, result.Message);
            Assert.Equal(80, runtimeCamera.ExpTime);
            Assert.Equal(string.Empty, runtimeCamera.CaliTempName);

            engine.LoadFromBase64(canvasBase64);
            LVCameraNode reloadedCamera = Assert.Single(editor.Nodes.OfType<LVCameraNode>());
            Assert.NotSame(runtimeCamera, reloadedCamera);
            Assert.Equal(21, reloadedCamera.ExpTime);
            Assert.Equal("EmbeddedCalibration", reloadedCamera.CaliTempName);
        });
    }

    private static FlowCameraParameterOverrideConfig CreateEnabledConfig()
    {
        return new FlowCameraParameterOverrideConfig
        {
            IsEnabled = true,
            ExposureTimeMs = 80,
            CalibrationTemplateName = string.Empty
        };
    }

    private static FlowCameraParameterOverrideResult ApplyToPath(
        FlowCameraParameterOverrideConfig config,
        params STNode[] path)
    {
        CameraOverrideTestStartNode start = Create(new CameraOverrideTestStartNode());
        STNode previous = start;
        foreach (STNode node in path)
        {
            Connect(previous, node);
            previous = node;
        }

        return FlowCameraParameterOverrideService.ApplyToLoadedFlow(
            config,
            new STNode[] { start }.Concat(path),
            start.NodeName);
    }

    private static void Connect(STNode source, STNode target)
    {
        STNodeOption output = source.GetAllOutputOptions().First(option => option.DataType == typeof(CVStartCFC));
        STNodeOption input = target.GetAllInputOptions().First(option => option.DataType == typeof(CVStartCFC));
        Assert.Equal(ConnectionStatus.Connected, output.ConnectOption(input, isOwnerOfOwner: false));
    }

    private static T Create<T>(T node) where T : STNode
    {
        node.Create();
        return node;
    }
}

public sealed class CameraOverrideTestStartNode : BaseStartNode
{
    public CameraOverrideTestStartNode() : base("Camera override test start")
    {
        NodeName = "CameraOverrideStart";
    }
}

public sealed class CanvasSnapshotLifecycleStartNode : BaseStartNode
{
    public static int ConnectionCallbackCount { get; set; }
    public static int PropertyLoadCount { get; set; }
    public static int EditorLoadCompletedCount { get; set; }
    public static int DisposeCount { get; set; }

    public CanvasSnapshotLifecycleStartNode() : base("Canvas snapshot lifecycle test")
    {
        NodeName = "CanvasSnapshotLifecycleStart";
    }

    protected override void DoStartConnected(STNodeOption sender, STNodeOptionEventArgs e)
    {
        ConnectionCallbackCount++;
    }

    public override void OnLoadNode(Dictionary<string, byte[]> dic)
    {
        PropertyLoadCount++;
        base.OnLoadNode(dic);
    }

    protected override void OnEditorLoadCompleted()
    {
        EditorLoadCompletedCount++;
    }

    public override void Dispose()
    {
        DisposeCount++;
        base.Dispose();
    }
}
