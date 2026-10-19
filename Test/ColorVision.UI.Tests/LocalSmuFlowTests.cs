using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.SMU;
using ColorVision.Engine.Services.Devices.SMU.Configs;
using ColorVision.Engine.Services.Devices.SMU.Local;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Themes;
using cvColorVision;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using FlowEngineLib.Node.SMU;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using Newtonsoft.Json.Linq;
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
    [InlineData(5)]
    [InlineData(6)]
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
                    return new FakeExecution(request.EventName == "CloseOutput" ? () =>
                    {
                        if (kind == 5) Assert.Equal(1, Newtonsoft.Json.Linq.JObject.FromObject(request.Data).Value<int>("Channel"));
                        closed++;
                    } : () => { }, action => { completed++; completedAction = action; }, request.EventName == "ModelGetData");
                };
                using var container = new CVNodeContainer();
                var start = new RuntimeTestStartNode();
                CVBaseServerNode node = kind switch { 0 => new SMUNode(), 1 => new SMUSweepNode(), 2 => new SMUSweepModelNode(),
                    3 => new SMUReaderNode(), 5 => new SMUModelNode(), _ => new SMUFromCSVNode() };
                var end = new CVEndNode();
                foreach (var item in new STNode[] { start, node, end }) { item.Create(); container.Nodes.Add(item); }
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
    [InlineData(false, false, RepeatScanScenario.Ordinary)]
    [InlineData(true, false, RepeatScanScenario.Ordinary)]
    [InlineData(false, true, RepeatScanScenario.Ordinary)]
    [InlineData(true, true, RepeatScanScenario.Ordinary)]
    [InlineData(false, true, RepeatScanScenario.Linear)]
    [InlineData(false, true, RepeatScanScenario.List)]
    [InlineData(false, true, RepeatScanScenario.NoCapabilities)]
    [InlineData(false, true, RepeatScanScenario.VaryingLimits)]
    [InlineData(false, true, RepeatScanScenario.MultipleNodes)]
    [InlineData(false, true, RepeatScanScenario.LinearWithOnlyListCapability)]
    [InlineData(false, true, RepeatScanScenario.FailedLinear)]
    [InlineData(false, true, RepeatScanScenario.CanceledList)]
    [InlineData(false, true, RepeatScanScenario.InvalidLaterPoint)]
    [InlineData(false, true, RepeatScanScenario.NodeDelayOverride)]
    public async Task PointNodesAutoOpenWithConfiguredConnectionAndCloseAtFlowEnd(bool configuredAsNetwork, bool singlePointNode, RepeatScanScenario scanScenario)
    {
        string address = configuredAsNetwork ? "192.0.2.18" : "COM24";
        double[] sources = scanScenario is RepeatScanScenario.Ordinary or RepeatScanScenario.List or RepeatScanScenario.CanceledList ? [7, 9, 4] : [7, 9, 11];
        double[] limits = scanScenario is RepeatScanScenario.Ordinary or RepeatScanScenario.VaryingLimits ? [5, 6, 7] : [5, 5, 5];
        if (scanScenario == RepeatScanScenario.InvalidLaterPoint) { sources = [7, 10000, 4]; limits = [5, 6, 7]; }
        bool usesScan = scanScenario is RepeatScanScenario.Linear or RepeatScanScenario.List or RepeatScanScenario.FailedLinear or RepeatScanScenario.CanceledList;
        var sdk = new RecordingNative
        {
            SweepCapabilities = scanScenario switch
            {
                RepeatScanScenario.Linear or RepeatScanScenario.FailedLinear => 1,
                RepeatScanScenario.List or RepeatScanScenario.LinearWithOnlyListCapability or RepeatScanScenario.CanceledList => 2,
                RepeatScanScenario.VaryingLimits or RepeatScanScenario.MultipleNodes => 3,
                _ => 0
            },
            SweepCode = scanScenario == RepeatScanScenario.FailedLinear ? 0 : 1,
            BlockScan = scanScenario == RepeatScanScenario.CanceledList
        };
        var session = new LocalSmuSession(sdk);
        DeviceSMU? device = null;
        CVNodeContainer? container = null;
        FlowEngineControl? control = null;
        RuntimeTestStartNode? start = null;
        CVCommonNode? node = null;
        var completion = new TaskCompletionSource<FlowEngineEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var managerField = typeof(ServiceManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        var previousManager = ServiceManager.Current;
        ColorVision.UI.IConfigService? previousConfig = null;
        var dictionaries = new List<ResourceDictionary>();
        RecordingEndNode? end = null;
        RepeatParameterRuntimeTests.ParameterProbeNode? repeatedCompanion = null;
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
                if (scanScenario == RepeatScanScenario.NodeDelayOverride) device.Config.DelayTime = 100;
                container = new CVNodeContainer();
                start = new RuntimeTestStartNode();
                node = singlePointNode ? new LocalSmuNode() : new SMUNode();
                end = new RecordingEndNode();
                foreach (var item in new STNode[] { start, node, end }) { item.Create(); container.Nodes.Add(item); }
                if (node is LocalSmuNode local)
                {
                    local.DeviceCode = device.Code;
                    local.Channel = FlowEngineLib.SMUChannelType.B;
                    local.Source = SourceType.Current_I;
                    local.MeasureValue = 7;
                    local.LimitValue = 5;
                    local.CloseOutputOnFinish = true;
                    if (scanScenario == RepeatScanScenario.NodeDelayOverride)
                    {
                        local.UseDeviceDelay = false;
                        local.MeasureDelayMs = 0;
                    }
                    Assert.Single(local.GetAllInputOptions());
                    Assert.Single(local.GetAllOutputOptions());
                    local.SetRepeatRegion(Guid.NewGuid().ToString(), 3, 1);
                    local.SetRepeatParameterValues(new Dictionary<string, double[]>
                    {
                        [nameof(LocalSmuNode.MeasureValue)] = sources,
                        [nameof(LocalSmuNode.LimitValue)] = limits
                    });
                    if (scanScenario == RepeatScanScenario.MultipleNodes)
                    {
                        repeatedCompanion = new RepeatParameterRuntimeTests.ParameterProbeNode();
                        repeatedCompanion.Create();
                        container.Nodes.Add(repeatedCompanion);
                        local.SetRepeatRegion(local.RepeatRegionId, 3, 2);
                        repeatedCompanion.SetRepeatRegion(local.RepeatRegionId, 3, 2);
                    }
                }
                else
                {
                    var legacy = Assert.IsType<SMUNode>(node);
                    legacy.DeviceCode = device.Code;
                    legacy.Channel = FlowEngineLib.SMUChannelType.B;
                    legacy.Source = SourceType.Current_I;
                    legacy.BeginVal = legacy.EndVal = 7;
                    legacy.PointNum = 1;
                    legacy.LimitVal = 5;
                    legacy.IsCloseOutput = true;
                    Assert.True(legacy.RequiresRemoteService);
                    Assert.Null(FlowLocalExecution.CreateForNode(legacy, new CVMQTTRequest("SVR.SMU.Default", device.Code,
                        "GetData", "switch-check", new { MeasureValue = 7, LimitValue = 5 }, "", -1)));
                }
                device.DisplayConfig.UseLocalSmu = true;
                if (node is CVBaseServerNode serverNode) Assert.False(serverNode.RequiresRemoteService);
                if (scanScenario == RepeatScanScenario.NoCapabilities)
                {
                    var seed = new ScanSeedNode(); seed.Create(); container.Nodes.Add(seed);
                    Assert.Equal(ConnectionStatus.Connected, start.m_op_start.ConnectOption(seed.Input, false));
                    Assert.Equal(ConnectionStatus.Connected, seed.Output.ConnectOption(node.GetAllInputOptions()[0], false));
                }
                else Assert.Equal(ConnectionStatus.Connected, start.m_op_start.ConnectOption(node.GetAllInputOptions()[0], false));
                if (repeatedCompanion == null)
                    Assert.Equal(ConnectionStatus.Connected, node.GetAllOutputOptions()[0].ConnectOption(end.m_in_start, false));
                else
                {
                    Assert.Equal(ConnectionStatus.Connected, node.GetAllOutputOptions()[0].ConnectOption(repeatedCompanion.Input, false));
                    Assert.Equal(ConnectionStatus.Connected, repeatedCompanion.Output.ConnectOption(end.m_in_start, false));
                }
                control = new FlowEngineControl(container, false, new FlowNodeManager()) { PersistResults = false };
                control.Finished += (_, result) => completion.TrySetResult(result);
                Assert.True(control.TryStartNode(start.NodeName, "configured-local-smu"));
            });
            if (scanScenario == RepeatScanScenario.CanceledList)
            {
                await sdk.ScanStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var stopObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                CVStartCFC running = start!.GetCFC("configured-local-smu");
                using var registration = running.RuntimeResources.StopToken.Register(() => stopObserved.TrySetResult());
                Task stopping = Task.Run(() => control!.StopNode(start.NodeName, "configured-local-smu"));
                try
                {
                    await stopObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    Assert.Empty(sdk.ClosedOutputs);
                }
                finally { sdk.ReleaseScan.TrySetResult(); }
                await stopping.WaitAsync(TimeSpan.FromSeconds(10));
            }
            var result = await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (scanScenario == RepeatScanScenario.InvalidLaterPoint)
            {
                Assert.Equal(StatusTypeEnum.Failed, result.Status);
                Assert.Empty(sdk.Points);
                Assert.Empty(sdk.OpenedConnections);
                Assert.Equal(0, sdk.Sweeps + sdk.Lists);
                Assert.Equal(0, end!.RunningCount);
                return;
            }
            if (scanScenario is RepeatScanScenario.FailedLinear or RepeatScanScenario.CanceledList)
            {
                Assert.Equal(scanScenario == RepeatScanScenario.FailedLinear ? StatusTypeEnum.Failed : StatusTypeEnum.Canceled, result.Status);
                Assert.Equal(1, sdk.Sweeps + sdk.Lists);
                Assert.Empty(sdk.Points);
                Assert.Equal(new[] { Channel.B }, sdk.ClosedOutputs);
                Assert.Null(end!.Scan);
                Assert.Null(end.SmuResult);
                Assert.Equal(0, end.RunningCount);
                Assert.Equal(0, start!.PublishCount);
                return;
            }
            Assert.Equal(StatusTypeEnum.Completed, result.Status);
            var connection = Assert.Single(sdk.OpenedConnections);
            Assert.Equal(configuredAsNetwork, connection.IsNet);
            Assert.Equal(address, connection.DeviceName);
            Assert.Equal(configuredAsNetwork ? 9600 : 115200, connection.BaudRate);
            if (scanScenario == RepeatScanScenario.NodeDelayOverride)
                Assert.Equal(new double[] { 100, 0, 0, 0 }, sdk.AppliedDelays);
            Assert.Equal(!usesScan, sdk.Step);
            Assert.Equal(singlePointNode ? sources[^1] / 1000 : 0.007, sdk.NativeSource);
            Assert.Equal(singlePointNode ? limits[^1] : 5, sdk.NativeLimit);
            if (singlePointNode)
            {
                if (usesScan)
                {
                    Assert.Empty(sdk.Points);
                    Assert.Empty(sdk.ClosedBeforePoint);
                    Assert.Equal(scanScenario == RepeatScanScenario.Linear ? 1 : 0, sdk.Sweeps);
                    Assert.Equal(scanScenario == RepeatScanScenario.List ? 1 : 0, sdk.Lists);
                    Assert.Equal(0, sdk.ClosedBeforeScan);
                    Assert.Equal(sources, sdk.ScanParameters!.SourceValues);
                    Assert.Equal(3, sdk.ScanParameters.Points);
                    Assert.Equal(scanScenario == RepeatScanScenario.List, sdk.ScanParameters.IsListSweep);
                    if (scanScenario == RepeatScanScenario.List) Assert.Equal(sources, sdk.ListSourceValues);
                    Assert.Equal(new double[] { 5, 6, 7 }, end!.Scan!.Voltages);
                    Assert.Equal(new double[] { 10, 11, 12 }, end.Scan.Currents);
                    Assert.Equal(sources, end.Scan.Parameters.SourceValues);
                    Assert.Equal(sources.Select(value => value / 1000).ToArray(), end.ScanResponse!["ScanList"]!.ToObject<double[]>());
                    Assert.Equal(new double[] { 0.01, 0.011, 0.012 }, end.ScanResponse["IList"]!.ToObject<double[]>());
                    Assert.False(end.Point!.IsScan);
                    Assert.Equal(sources[^1], end.Point.Parameters.MeasureValue);
                    Assert.Equal(limits[^1], end.Point.Parameters.LimitValue);
                    Assert.Equal(7, end.SmuResult!.V);
                }
                else
                {
                    Assert.Equal(sources.Zip(limits, (source, limit) => (source / 1000, limit)), sdk.Points);
                    Assert.Equal(new[] { 0, 0, 0 }, sdk.ClosedBeforePoint);
                    Assert.Equal(0, sdk.Sweeps + sdk.Lists);
                    Assert.Null(end!.Scan);
                    Assert.Null(end.ScanResponse);
                }
                Assert.Equal(7, Assert.IsType<LocalSmuNode>(node).MeasureValue);
                Assert.Equal(5, Assert.IsType<LocalSmuNode>(node).LimitValue);
                Assert.Equal(sources, node!.RepeatParameterValues[nameof(LocalSmuNode.MeasureValue)]);
                Assert.Equal(limits, node.RepeatParameterValues[nameof(LocalSmuNode.LimitValue)]);
                Assert.Equal(STNodeGroupState.Completed, ((ISTNodeGroupMember)node).GroupState);
                if (repeatedCompanion != null) Assert.Equal(3, repeatedCompanion.Received.Count);
            }
            Assert.Equal(new[] { Channel.B }, sdk.ClosedOutputs);
            Assert.Equal(12, end!.SmuResult!.I);
            Assert.Equal(FlowEngineLib.SMUChannelType.B, end.SmuResult.Channel);
            Assert.Equal(1, end.RunningCount);
            Assert.Equal(0, start!.PublishCount);
            WpfTestHost.Invoke(() =>
            {
                device!.DisplayConfig.UseLocalSmu = false;
                if (node is CVBaseServerNode serverNode) Assert.False(serverNode.RequiresRemoteService);
            });
            await session.CloseAsync();
            WpfTestHost.Invoke(() =>
            {
                if (node is CVBaseServerNode serverNode) Assert.True(serverNode.RequiresRemoteService);
            });
        }
        finally
        {
            sdk.ReleaseScan.TrySetResult();
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

    public enum RepeatScanScenario { Ordinary, Linear, List, NoCapabilities, VaryingLimits, MultipleNodes, LinearWithOnlyListCapability, FailedLinear, CanceledList, InvalidLaterPoint, NodeDelayOverride }

    // An upstream scan must not survive a later point-only result under the same latest-result keys.
    private sealed class ScanSeedNode() : CVCommonNode("Upstream scan", "Test", "Test")
    {
        internal STNodeOption Input = null!, Output = null!;
        protected override void OnCreate()
        {
            base.OnCreate();
            Input = InputOptions.Add("IN", typeof(CVStartCFC), true);
            Output = OutputOptions.Add("OUT", typeof(CVStartCFC), false);
            Input.DataTransfer += (_, args) =>
            {
                if (args.Status != ConnectionStatus.Connected || args.TargetOption.Data is not CVStartCFC received) return;
                if (!received.IsRunning) { Output.TransferData(received); return; }
                var action = new CVStartCFC(received);
                action.Data["LocalSmuScan"] = new LocalSmuCapture(new LocalSmuParameters(), [1, 2], [3, 4], 0, true);
                action.Data["SMUScanResult"] = new JObject { ["stale"] = true };
                Output.TransferData(action);
            };
        }
    }

    private sealed class RecordingEndNode : CVEndNode
    {
        internal SMUResultData? SmuResult;
        internal LocalSmuCapture? Point, Scan;
        internal JObject? ScanResponse;
        internal int RunningCount;
        protected override void DoNodeEnded(CVStartCFC action)
        {
            if (action.IsRunning) RunningCount++;
            if (action.Data.TryGetValue("SMUResult", out var result)) SmuResult = Assert.IsType<SMUResultData>(result);
            if (action.Data.TryGetValue("LocalSmu", out var point)) Point = Assert.IsType<LocalSmuCapture>(point);
            if (action.Data.TryGetValue("LocalSmuScan", out var scan)) Scan = Assert.IsType<LocalSmuCapture>(scan);
            if (action.Data.TryGetValue("SMUScanResult", out var scanResponse)) ScanResponse = Assert.IsType<JObject>(scanResponse);
            base.DoNodeEnded(action);
        }
    }

    private sealed class RecordingNative : ILocalSmuNative
    {
        internal readonly List<LocalSmuConnection> OpenedConnections = [];
        internal readonly List<Channel> ClosedOutputs = [];
        internal bool Step;
        internal int SweepCapabilities, SweepCode = 1, ListCode = 1, Sweeps, Lists, ClosedBeforeScan;
        internal bool BlockScan;
        internal readonly TaskCompletionSource ScanStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource ReleaseScan = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal double NativeSource, NativeLimit;
        internal LocalSmuParameters? ScanParameters;
        internal double[]? ListSourceValues;
        internal readonly List<(double Source, double Limit)> Points = [];
        internal readonly List<double> AppliedDelays = [];
        internal readonly List<int> ClosedBeforePoint = [];
        private Channel channel;
        public int Open(LocalSmuConnection connection) { OpenedConnections.Add(connection); return 0; }
        public int GetIdn(int handle, StringBuilder text, ref int length) { text.Append("synthetic flow SMU"); return 1; }
        public int SetWiring(int handle, bool fourWire, bool front) => 1;
        public int SetDelay(int handle, double milliseconds) { AppliedDelays.Add(milliseconds); return 1; }
        public int SetChannel(int handle, bool channelA) { channel = channelA ? Channel.A : Channel.B; return 1; }
        public int SetSource(int handle, bool voltage) => 1;
        public int Measure(int handle, LocalSmuParameters parameters, bool step, ref double voltage, ref double current)
        {
            Step = step; NativeSource = parameters.NativeSource(parameters.MeasureValue); NativeLimit = parameters.NativeLimit;
            Points.Add((NativeSource, NativeLimit)); ClosedBeforePoint.Add(ClosedOutputs.Count);
            voltage = 5; current = 0.012; return 1;
        }
        public int Read(int handle, ref double voltage, ref double current) => throw new NotSupportedException();
        public int GetSweepCapabilities(int handle) => SweepCapabilities;
        public int Sweep(int handle, LocalSmuParameters parameters, double[] voltages, double[] currents)
        {
            Sweeps++; ScanParameters = parameters; ClosedBeforeScan = ClosedOutputs.Count;
            WaitForScan();
            NativeSource = parameters.NativeSource(parameters.EndValue); NativeLimit = parameters.NativeLimit;
            for (int index = 0; index < voltages.Length; index++) { voltages[index] = index + 5; currents[index] = (index + 10) / 1000d; }
            return SweepCode;
        }
        public int List(int handle, LocalSmuParameters parameters, double[] sourceValues, double[] voltages, double[] currents)
        {
            Lists++; ScanParameters = parameters; ListSourceValues = (double[])sourceValues.Clone(); ClosedBeforeScan = ClosedOutputs.Count;
            WaitForScan();
            NativeSource = parameters.NativeSource(sourceValues[^1]); NativeLimit = parameters.NativeLimit;
            for (int index = 0; index < voltages.Length; index++) { voltages[index] = index + 5; currents[index] = (index + 10) / 1000d; }
            return ListCode;
        }
        private void WaitForScan()
        {
            ScanStarted.TrySetResult();
            if (BlockScan) ReleaseScan.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        }
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
