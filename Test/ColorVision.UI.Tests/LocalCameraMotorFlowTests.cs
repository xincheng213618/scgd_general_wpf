using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using ColorVision.Engine.Templates;
using cvColorVision;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using Newtonsoft.Json.Linq;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public class LocalCameraMotorFlowTests
{
    // Saved motor nodes must share the camera owner and completion path without MQTT.
    [Theory]
    [InlineData(CamMotorRunType.焦距, false)]
    [InlineData(CamMotorRunType.光圈, false)]
    [InlineData(CamMotorRunType.自动对焦, false)]
    [InlineData(CamMotorRunType.焦距, true)]
    public async Task ExistingMotorNodeUsesLocalCameraAndAutoConnectsWhenClosed(CamMotorRunType command, bool alreadyOpen)
    {
        using var scope = new Scope();
        if (alreadyOpen)
        {
            scope.Session.Open("camera", TakeImageMode.Measure_Normal, 16);
            scope.Backend.SetPreference(false); // The active owner wins over the next-open preference.
        }
        var result = await scope.Run(command);
        Assert.Equal(StatusTypeEnum.Completed, result.Status);
        Assert.Equal(0, scope.Start!.PublishCount);
        Assert.True(scope.Session.IsOpen);
        Assert.Equal(new[] { "open", "configure" }, scope.Native.Events);
        Assert.NotNull(scope.Response);
        if (command == CamMotorRunType.焦距) Assert.Equal(150, scope.Response.Value<int>("nPosition"));
        if (command == CamMotorRunType.自动对焦)
        {
            Assert.InRange(scope.Response.Value<int>("Position"), 462, 464);
            Assert.True(scope.Response.Value<int>("Samples") > 0);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LocalMotorOrTemplateFailureNeverFallsBackToMqtt(bool missingTemplate)
    {
        using var scope = new Scope();
        scope.Native.MoveResult = -10063;
        var result = await scope.Run(missingTemplate ? CamMotorRunType.自动对焦 : CamMotorRunType.焦距, missingTemplate);
        Assert.Equal(StatusTypeEnum.Failed, result.Status);
        Assert.Equal(0, scope.Start!.PublishCount);
        if (missingTemplate) Assert.Empty(scope.Native.Events);
        // A failed request must release the command reservation.
        scope.Backend.BeginLocalCommand();
        scope.Backend.EndLocalCommand();
    }

    [Fact]
    public void SelectionKeepsTheServiceOwnerAndUsesLocalPreferenceOnlyWhenClosed()
    {
        using var scope = new Scope();
        var node = new CamMotorNode { DeviceCode = scope.Device.Code };
        Assert.False(node.RequiresRemoteService);
        scope.Backend.ObserveService(DeviceStatusType.Opened);
        Assert.True(node.RequiresRemoteService);
        var request = new CVMQTTRequest { DeviceCode = scope.Device.Code, EventName = "Move", Data = new FocusPosData(150, true) };
        Assert.Null(LocalCameraMotorExecution.Create(request));
        scope.Backend.ObserveService(DeviceStatusType.Closed);
        scope.Backend.SetPreference(false);
        Assert.True(node.RequiresRemoteService);
        Assert.Null(LocalCameraMotorExecution.Create(request));
    }

    [Fact]
    public async Task FlowStopWaitsForCurrentFocusSampleAndPreventsFurtherMotion()
    {
        using var scope = new Scope();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        scope.Native.OnSample = () => { entered.Set(); release.Wait(); };
        var request = new CVMQTTRequest
        {
            DeviceCode = scope.Device.Code, EventName = "AutoFocus", SerialNumber = "motor-stop",
            Data = new AutoFocusData(scope.Template.Key)
        };
        using var execution = LocalCameraMotorExecution.Create(request)!;
        var action = new CVStartCFC("motor-stop");
        execution.Bind(action);
        Task task = Task.Run(execution.Execute);
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            action.RuntimeResources.Dispose();
            Assert.False(task.IsCompleted);
            Assert.Throws<InvalidOperationException>(() => scope.Backend.BeginLocalCommand());
            Assert.Equal(0, scope.Native.Closes);
            release.Set();
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await task);
            Assert.Equal(1, scope.Native.Moves);
            scope.Backend.BeginLocalCommand();
            scope.Backend.EndLocalCommand();
        }
        finally { release.Set(); }
    }

    private sealed class Scope : IDisposable
    {
        private readonly ServiceManager? previousManager = ServiceManager.Current;
        private readonly IConfigService previousConfig = ConfigService.Instance;
        private static readonly FieldInfo ManagerField = typeof(ServiceManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private CVNodeContainer? container;
        private FlowEngineControl? control;
        internal DeviceCamera Device { get; }
        internal CameraBackendState Backend { get; } = new(true);
        internal LocalCameraMotorTests.FakeMotor Native { get; } = new();
        internal LocalCameraSession Session { get; }
        internal TemplateModel<AutoFocusParam> Template { get; }
        internal RuntimeTestStartNode? Start { get; private set; }
        internal JObject? Response;

        internal Scope()
        {
            WpfTestHost.Invoke(() => ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false }));
            var config = LocalCameraMotorTests.Configuration();
            config.Code = "synthetic-camera-motor-" + Guid.NewGuid();
            config.CameraID = "camera";
            config.ImageBpp = ImageBpp.bpp16;
            config.MotorConfig.IsCameraLinkage = false;
            Device = (DeviceCamera)RuntimeHelpers.GetUninitializedObject(typeof(DeviceCamera));
            Device.Config = config;
            Device.SysResourceModel = new SysResourceModel { Code = config.Code };
            Session = new LocalCameraSession(new LocalCameraSessionTests.FakeNative(), Backend, config, motorNative: Native);
            typeof(DeviceCamera).GetField("<CameraBackend>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Device, Backend);
            typeof(DeviceCamera).GetField("<LocalCameraSession>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Device, Session);
            Template = WpfTestHost.Invoke(() =>
            {
                var manager = (ServiceManager)RuntimeHelpers.GetUninitializedObject(typeof(ServiceManager));
                manager.DeviceServices = [Device];
                ManagerField.SetValue(null, manager);
                var template = new TemplateModel<AutoFocusParam>("synthetic-focus-" + Guid.NewGuid(), new AutoFocusParam
                {
                    MinPosition = 0, MaxPosition = 1000, CurStep = 100, Forwardparam = 100, StopStep = 1, Curtailparam = 0.5
                });
                TemplateAutoFocus.Params.Add(template);
                return template;
            });
        }

        internal Task<FlowEngineEventArgs> Run(CamMotorRunType command, bool missingTemplate = false)
        {
            var completed = new TaskCompletionSource<FlowEngineEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            WpfTestHost.Invoke(() =>
            {
                container = new CVNodeContainer();
                Start = new RuntimeTestStartNode();
                var node = new CamMotorNode();
                var end = new CVEndNode();
                foreach (var item in new STNode[] { Start, node, end }) { item.Create(); container.Nodes.Add(item); }
                node.DeviceCode = Device.Code;
                node.RunType = command;
                node.Position = 150;
                node.Aperture = 2.8f;
                node.AutoFocusTemp = missingTemplate ? "missing-focus-template" : Template.Key;
                Assert.False(node.RequiresRemoteService);
                Assert.Equal(ConnectionStatus.Connected, Start.m_op_start.ConnectOption(node.GetAllInputOptions()[0], false));
                Assert.Equal(ConnectionStatus.Connected, node.GetAllOutputOptions()[0].ConnectOption(end.m_in_start, false));
                node.nodeEndEvent += (_, args) => { if (!string.IsNullOrWhiteSpace(args.RecvPayload)) Response = JObject.Parse(args.RecvPayload); };
                control = new FlowEngineControl(container, false, new FlowNodeManager());
                control.Finished += (_, args) => completed.TrySetResult(args);
                Assert.True(control.TryStartNode(Start.NodeName, "motor-flow"));
            });
            return completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        public void Dispose()
        {
            WpfTestHost.Invoke(() =>
            {
                control?.Dispose();
                container?.Dispose();
                Session.Dispose();
                TemplateAutoFocus.Params.Remove(Template);
                DisplayConfigManager.Instance.Configs.TryRemove(Device.Code, out _);
                ManagerField.SetValue(null, previousManager);
                ConfigService.SetInstance(previousConfig);
            });
        }
    }
}
