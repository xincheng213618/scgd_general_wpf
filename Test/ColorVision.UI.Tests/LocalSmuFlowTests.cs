using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.SMU;
using ColorVision.Engine.Services.Devices.SMU.Configs;
using ColorVision.Engine.Services.Devices.SMU.Local;
using ColorVision.Themes;
using cvColorVision;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using FlowEngineLib.Node.Global;
using FlowEngineLib.Node.SMU;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using Channel = ColorVision.Engine.Services.Devices.SMU.Dao.SMUChannelType;
using SMUResultData = FlowEngineLib.SMUResultData;

namespace ColorVision.UI.Tests;

public sealed class LocalSmuFlowTests
{
    // Keep saved legacy nodes on the local result and cleanup paths without MQTT.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void LegacySmuNodesCompleteAndCloseWithoutPublishing(int kind)
    {
        StaTest.Run(() =>
        {
            var previousPredicate = FlowLocalExecution.CanExecuteLocally;
            var previousFactory = FlowLocalExecution.CreateForNode;
            string? csvFile = null;
            try
            {
                FlowLocalExecution.CanExecuteLocally = LocalSmuFlowExecution.Supports;
                int completed = 0, closed = 0;
                CVStartCFC? completedAction = null;
                FlowLocalExecution.CreateForNode = (_, request) =>
                {
                    if (kind is 4 or 7 or 8) Assert.Equal(kind switch { 7 => "Close", 8 => "Reopen", _ => "Open" }, request.EventName);
                    return new FakeExecution(request.EventName == "CloseOutput" ? () =>
                    {
                        if (kind == 5) Assert.Equal(1, Newtonsoft.Json.Linq.JObject.FromObject(request.Data).Value<int>("Channel"));
                        closed++;
                    } : () => { }, action => { completed++; completedAction = action; }, request.EventName == "ModelGetData");
                };
                using var container = new CVNodeContainer();
                var start = new RuntimeTestStartNode();
                CVBaseServerNode node = kind switch { 0 => new SMUNode(), 1 => new SMUSweepNode(), 2 => new SMUSweepModelNode(),
                    3 => new SMUReaderNode(), 4 or 7 or 8 => new PhyDeviceControlNode(), 5 => new SMUModelNode(), _ => new SMUFromCSVNode() };
                var end = new CVEndNode();
                foreach (var item in new STNode[] { start, node, end }) { item.Create(); container.Nodes.Add(item); }
                if (node is PhyDeviceControlNode physical)
                {
                    physical.DeviceType = CVDeviceType.SMU;
                    physical.CmdType = kind switch { 7 => CVDeviceControlCmd.Close, 8 => CVDeviceControlCmd.Reopen, _ => CVDeviceControlCmd.Open };
                }
                if (node is SMUFromCSVNode csv)
                {
                    csvFile = System.IO.Path.GetTempFileName();
                    System.IO.File.WriteAllText(csvFile, "No,SrcValue,LimitValue\n1,5,10\n");
                    csv.CsvFileName = csvFile;
                }
                if (node is SMUBaseNode smu) smu.IsCloseOutput = true;
                Assert.True(LocalSmuFlowExecution.Supports(node));
                Assert.Equal(ConnectionStatus.Connected, start.m_op_start.ConnectOption(node.GetAllInputOptions()[0], false));
                Assert.Equal(ConnectionStatus.Connected, node.GetAllOutputOptions()[0].ConnectOption(end.m_in_start, false));
                using var control = new FlowEngineControl(container, false, new FlowNodeManager()) { PersistResults = false };
                var completion = new TaskCompletionSource<FlowEngineEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
                control.Finished += (_, result) => completion.TrySetResult(result);
                Assert.False(node.RequiresRemoteService);
                Assert.True(control.TryStartNode(start.NodeName, "local-smu"));
                var result = completion.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                Assert.Equal(StatusTypeEnum.Completed, result.Status);
                Assert.Equal(0, start.PublishCount);
                Assert.Equal(1, completed);
                Assert.Equal(kind is 0 or 5 or 6 ? 1 : 0, closed);
                if (kind == 5) Assert.Equal(FlowEngineLib.SMUChannelType.B, Assert.IsType<SMUResultData>(completedAction!.Data["SMUResult"]).Channel);
            }
            finally
            {
                FlowLocalExecution.CanExecuteLocally = previousPredicate;
                FlowLocalExecution.CreateForNode = previousFactory;
                if (csvFile != null) System.IO.File.Delete(csvFile);
            }
        });
    }

    // Exercise the registered backend rather than replacing its predicate/factory: saved nodes
    // must honor the device switch and pass the local connection settings through auto-open.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingPointNodeRoutesByDeviceSwitchAndAutoOpensWithConfiguredConnection(bool configuredAsNetwork)
    {
        string address = configuredAsNetwork ? "192.0.2.18" : "COM24";
        var sdk = new RecordingNative();
        var session = new LocalSmuSession(sdk);
        DeviceSMU? device = null;
        CVNodeContainer? container = null;
        FlowEngineControl? control = null;
        RuntimeTestStartNode? start = null;
        SMUNode? node = null;
        var completion = new TaskCompletionSource<FlowEngineEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var managerField = typeof(ServiceManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        var previousManager = ServiceManager.Current;
        ColorVision.UI.IConfigService? previousConfig = null;
        var dictionaries = new List<ResourceDictionary>();
        RecordingEndNode? end = null;
        try
        {
            WpfTestHost.Invoke(() =>
            {
                previousConfig = ColorVision.UI.ConfigService.Instance;
                ColorVision.UI.ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
                foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                {
                    var dictionary = new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                    dictionaries.Add(dictionary);
                    Application.Current.Resources.MergedDictionaries.Add(dictionary);
                }
                var manager = (ServiceManager)RuntimeHelpers.GetUninitializedObject(typeof(ServiceManager));
                manager.DeviceServices = [];
                managerField.SetValue(null, manager);
                device = new DeviceSMU(new() { Id = 1, Code = "synthetic-flow-smu-" + Guid.NewGuid(),
                    Value = Newtonsoft.Json.JsonConvert.SerializeObject(new ConfigSMU
                        { DevName = "synthetic-" + Guid.NewGuid(), DevType = Pss_Type.Keithley_2600 }) }, session);
                manager.DeviceServices.Add(device);
                device.DisplayConfig.UseLocalSmu = false;
                device.DisplayConfig.LocalBaudRate = SMUSerialBaudRate.Baud115200;
                device.Config.IsNet = configuredAsNetwork;
                if (configuredAsNetwork) device.Config.IpAddress = address;
                else device.Config.SerialPort = address;
                container = new CVNodeContainer();
                start = new RuntimeTestStartNode();
                node = new SMUNode();
                end = new RecordingEndNode();
                foreach (var item in new STNode[] { start, node, end }) { item.Create(); container.Nodes.Add(item); }
                node.DeviceCode = device.Code;
                node.Channel = FlowEngineLib.SMUChannelType.B;
                node.Source = SourceType.Current_I;
                node.BeginVal = node.EndVal = 7;
                node.PointNum = 1;
                node.LimitVal = 5;
                node.IsCloseOutput = true;
                Assert.True(node.RequiresRemoteService);
                Assert.Null(FlowLocalExecution.CreateForNode(node, new CVMQTTRequest("SVR.SMU.Default", device.Code,
                    "GetData", "switch-check", new { MeasureValue = 7, LimitValue = 5 }, "", -1)));
                device.DisplayConfig.UseLocalSmu = true;
                Assert.False(node.RequiresRemoteService);
                Assert.Equal(ConnectionStatus.Connected, start.m_op_start.ConnectOption(node.GetAllInputOptions()[0], false));
                Assert.Equal(ConnectionStatus.Connected, node.GetAllOutputOptions()[0].ConnectOption(end.m_in_start, false));
                control = new FlowEngineControl(container, false, new FlowNodeManager()) { PersistResults = false };
                control.Finished += (_, result) => completion.TrySetResult(result);
                Assert.True(control.TryStartNode(start.NodeName, "configured-local-smu"));
            });
            var result = await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(StatusTypeEnum.Completed, result.Status);
            var connection = Assert.Single(sdk.OpenedConnections);
            Assert.Equal(configuredAsNetwork, connection.IsNet);
            Assert.Equal(address, connection.DeviceName);
            Assert.Equal(configuredAsNetwork ? 9600 : 115200, connection.BaudRate);
            Assert.True(sdk.Step);
            Assert.Equal(0.007, sdk.NativeSource);
            Assert.Equal(5, sdk.NativeLimit);
            Assert.Equal(new[] { Channel.B }, sdk.ClosedOutputs);
            Assert.Equal(12, end!.SmuResult!.I);
            Assert.Equal(FlowEngineLib.SMUChannelType.B, end.SmuResult.Channel);
            Assert.Equal(0, start!.PublishCount);
            WpfTestHost.Invoke(() =>
            {
                device!.DisplayConfig.UseLocalSmu = false;
                Assert.False(node!.RequiresRemoteService);
            });
            await session.CloseAsync();
            WpfTestHost.Invoke(() =>
            {
                Assert.True(node!.RequiresRemoteService);
            });
        }
        finally
        {
            bool removedConfig = device == null;
            WpfTestHost.Invoke(() =>
            {
                control?.Dispose(); container?.Dispose(); device?.Dispose();
                managerField.SetValue(null, previousManager);
                if (device != null) removedConfig = DisplayConfigManager.Instance.Configs.TryRemove(device.Code, out _);
                foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
                if (previousConfig != null) ColorVision.UI.ConfigService.SetInstance(previousConfig);
            });
            await session.DisposeAsync();
            Assert.True(removedConfig);
        }
    }

    private sealed class RecordingEndNode : CVEndNode
    {
        internal SMUResultData? SmuResult;
        protected override void DoNodeEnded(CVStartCFC action)
        {
            if (action.Data.TryGetValue("SMUResult", out var result)) SmuResult = Assert.IsType<SMUResultData>(result);
            base.DoNodeEnded(action);
        }
    }

    private sealed class RecordingNative : ILocalSmuNative
    {
        internal readonly List<LocalSmuConnection> OpenedConnections = [];
        internal readonly List<Channel> ClosedOutputs = [];
        internal bool Step;
        internal double NativeSource, NativeLimit;
        private Channel channel;
        public int Open(LocalSmuConnection connection) { OpenedConnections.Add(connection); return 0; }
        public int GetIdn(int handle, StringBuilder text, ref int length) { text.Append("synthetic flow SMU"); return 1; }
        public int SetWiring(int handle, bool fourWire, bool front) => 1;
        public int SetDelay(int handle, double milliseconds) => 1;
        public int SetChannel(int handle, bool channelA) { channel = channelA ? Channel.A : Channel.B; return 1; }
        public int SetSource(int handle, bool voltage) => 1;
        public int Measure(int handle, LocalSmuParameters parameters, bool step, ref double voltage, ref double current)
        {
            Step = step; NativeSource = parameters.NativeSource(parameters.MeasureValue); NativeLimit = parameters.NativeLimit;
            voltage = 5; current = 0.012; return 1;
        }
        public int Read(int handle, ref double voltage, ref double current) => throw new NotSupportedException();
        public int Sweep(int handle, LocalSmuParameters parameters, double[] voltages, double[] currents) => throw new NotSupportedException();
        public int CloseOutput(int handle) { ClosedOutputs.Add(channel); return 1; }
        public int Close(int handle) => 1;
    }

    private sealed class FakeExecution(Action execute, Action<CVStartCFC> complete, bool model) : FlowLocalExecution
    {
        public override void Execute() => execute();
        public override object Complete(CVStartCFC action)
        {
            complete(action);
            var result = LocalSmuResultService.Response(new LocalSmuCapture(new LocalSmuParameters { Channel = Channel.A }, [5], [10], 1, false), 0);
            if (model)
            {
                result["ResultData"] = new Newtonsoft.Json.Linq.JObject { ["V"] = 5, ["I"] = 10 };
                result["ScanRequestParam"] = Newtonsoft.Json.Linq.JObject.FromObject(new LocalSmuParameters
                    { Channel = Channel.B, IsSourceV = true, BeginValue = 0, EndValue = 5, LimitValue = 1, Points = 5 });
            }
            return result;
        }
        public override void Dispose() { }
    }
}
