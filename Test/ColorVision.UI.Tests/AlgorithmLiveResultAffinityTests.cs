using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.Messages;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Templates.Jsons.MTF2;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Views;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.MQTT;
using MQTTMessageLib.FileServer;
using MQTTnet;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using AlgorithmNode = FlowEngineLib.Algorithm.AlgorithmNode;

namespace ColorVision.UI.Tests;

public sealed partial class PostProcessDatabaseAffinityTests
{
    private static readonly TimeSpan LiveResultDeadline = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ManualMtfCommandOnlyExportsTheOwnedDatabaseResult(bool reconnect) => WpfTestHost.Invoke(async () =>
    {
        using var fixture = new LiveAlgorithmFixture();
        using var subscription = fixture.ObserveBus(out var results);
        Task presented = fixture.ObserveNextResult();
        Assert.True(await fixture.Mqtt.Connect().WaitAsync(LiveResultDeadline));
        try
        {
            MsgRecord accepted = await fixture.SendManualMtf();
            await fixture.Reply(accepted, "unrelated-device");
            await fixture.Reply(accepted, code: 102);
            await fixture.Reply(accepted, masterIdText: "invalid-master");
            await fixture.Reply(accepted, match: false);
            Assert.Equal(MsgRecordState.Sended, accepted.MsgRecordState);

            MsgRecord failed = await fixture.SendManualMtf();
            await fixture.Reply(failed, code: 500);
            Assert.Equal(MsgRecordState.Fail, await failed.WorkerCompletion!.WaitAsync(LiveResultDeadline));
            Assert.Empty(results);
            fixture.AssertNoExport();

            if (reconnect) fixture.SwitchDatabase();
            else fixture.Device.DService.ServiceToken = "rotated-after-manual-dispatch";
            await fixture.Reply(accepted);
            Assert.Equal(MsgRecordState.Success, await accepted.WorkerCompletion!.WaitAsync(LiveResultDeadline));
            await fixture.Reply(accepted);
            if (reconnect)
            {
                Assert.Empty(results);
                fixture.AssertNoExport();
            }
            else
            {
                await presented.WaitAsync(LiveResultDeadline);
                fixture.AssertOwnedExport(Assert.Single(results));
            }
        }
        finally { await fixture.Mqtt.DisconnectAsyncClient().WaitAsync(LiveResultDeadline); }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task LegacyFlowMtfDoesNotDisplayOrExportFromAReplacementDatabase(bool reconnect) => WpfTestHost.Invoke(async () =>
    {
        using var fixture = new LiveAlgorithmFixture();
        using var subscription = fixture.ObserveBus(out var results);
        using var graph = new CVNodeContainer();
        var start = new RuntimeTestStartNode();
        var node = new AlgorithmNode();
        var end = new RuntimeTestEndNode();
        foreach (STNode item in new STNode[] { start, node, end })
        {
            item.Create();
            graph.Nodes.Add(item);
        }
        node.DeviceCode = fixture.Device.Code;
        node.RemoteResultBinderOverride = RemoteDeviceFlowResult.CreateBinder(code => code == fixture.Device.Code ? fixture.Device : null);
        Assert.Equal(ConnectionStatus.Connected, start.m_op_start.ConnectOption(
            Assert.Single(node.GetAllInputOptions(), option => option.DataType == typeof(CVStartCFC)), false));
        Assert.Equal(ConnectionStatus.Connected, Assert.Single(node.GetAllOutputOptions(), option => option.DataType == typeof(CVStartCFC))
            .ConnectOption(end.m_in_start, false));
        var completed = new TaskCompletionSource<FlowEngineEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var engine = new FlowEngineControl(graph, false, new FlowNodeManager());
        engine.Finished += (_, args) => completed.TrySetResult(args);
        fixture.Device.Config.SendTopic = node.GetSendTopic();
        fixture.Device.Config.SubscribeTopic = node.GetRecvTopic();
        MQActionEvent? sent = null;
        start.OnPublish = message => sent = message;
        Task presented = fixture.ObserveNextResult();

        Assert.True(engine.TryStartNode(start.NodeName, "flow-request-serial"));
        CVMQTTRequest request = JsonConvert.DeserializeObject<CVMQTTRequest>(Assert.IsType<MQActionEvent>(sent).Message)!;
        Assert.Equal("MTF", request.EventName);
        string replyTopic = Assert.Single(start.SubscribedTopics, topic => topic == node.GetRecvTopic());
        Assert.False(node.DoServerStatusRecv(Response(code: 102)));
        Assert.False(node.DoServerStatusRecv(Response(deviceCode: "wrong-flow-device")));
        Assert.Empty(results);
        if (reconnect) fixture.SwitchDatabase();
        else fixture.Device.DService.ServiceToken = "rotated-after-flow-dispatch";

        Assert.True(node.DoServerStatusRecv(Response()));
        Assert.Equal(StatusTypeEnum.Completed, (await completed.Task.WaitAsync(LiveResultDeadline)).Status);
        Assert.False(node.DoServerStatusRecv(Response()));
        if (reconnect)
        {
            Assert.Empty(results);
            fixture.AssertNoExport();
        }
        else
        {
            await presented.WaitAsync(LiveResultDeadline);
            ResultMessage result = Assert.Single(results);
            Assert.Equal((node.NodeID, request.SerialNumber), (result.NodeId, result.SerialNumber));
            fixture.AssertOwnedExport(result);
        }

        CVBaseDataFlowResp Response(int code = 0, string? deviceCode = null) => new()
        {
            Code = code, DeviceCode = deviceCode ?? request.DeviceCode, DeviceNodeCode = node.NodeID, EventName = request.EventName,
            SerialNumber = request.SerialNumber, MsgID = request.MsgID, ServiceName = request.ServiceCode, ZIndex = request.ZIndex,
            ReceivedTopic = replyTopic, Data = new JObject { ["MasterId"] = MasterId, ["MasterResultType"] = (int)ViewResultAlgType.MTF }
        };
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task AutoExportRejectsARetiredOwnerAndAnInFlightDatabaseSwitch(bool duringBatchRead) => WpfTestHost.Invoke(async () =>
    {
        using var fixture = new LiveAlgorithmFixture();
        DeviceAlgorithm retired = LiveAlgorithmFixture.CreateOwner(fixture.Device.Code, fixture.Databases.First);
        await fixture.View.ProcessResultAsync(Message(retired));
        Assert.Equal(0, fixture.Databases.FirstReadCount);
        fixture.AssertNoExport();

        var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void PauseRead()
        {
            readStarted.TrySetResult();
            releaseRead.Task.WaitAsync(LiveResultDeadline).GetAwaiter().GetResult();
        }
        if (duringBatchRead)
            fixture.Databases.BeforeFirstQuery = sql =>
            {
                if (sql.Contains("t_scgd_measure_batch", StringComparison.OrdinalIgnoreCase)) PauseRead();
            };
        else
            fixture.Databases.BeforeFirstRead = PauseRead;
        Task pending = fixture.View.ProcessResultAsync(Message(fixture.Device));
        try
        {
            await readStarted.Task.WaitAsync(LiveResultDeadline);
            fixture.SwitchDatabase();
        }
        finally { releaseRead.TrySetResult(); }
        await pending.WaitAsync(LiveResultDeadline);
        fixture.AssertNoExport();

        ResultMessage Message(DeviceAlgorithm owner) => new(ResultRoutes.Algorithm, ResultKinds.Algorithm, fixture.Device.Code,
            "MTF", "request-serial", string.Empty, -1, new ResultReference(MasterId, (int)ViewResultAlgType.MTF),
            attachment: null, databaseEndpoint: fixture.Databases.First, sourceDevice: owner);
    });

    [Fact]
    public Task ANewDatabaseCanShowAndExportItsOwnResultWithTheSameId() => WpfTestHost.Invoke(async () =>
    {
        using var fixture = new LiveAlgorithmFixture();
        await Apply(fixture.Databases.First);
        Assert.Equal("original-master", Assert.Single(fixture.View.ViewResults).ResultDesc);

        fixture.SwitchDatabase();
        await Apply(fixture.Databases.Second);
        await Apply(fixture.Databases.Second);

        ViewResultAlg result = Assert.Single(fixture.View.ViewResults);
        Assert.Equal("replacement-master", result.ResultDesc);
        Assert.Same(fixture.Databases.Second, result.AlgResultMasterModel.DatabaseEndpoint);
        Assert.Equal(2, Directory.GetFiles(fixture.Databases.DirectoryPath, "*.csv").Length);
        string path = Path.Combine(fixture.Databases.DirectoryPath, "replacement-owned-batchMTF.csv");
        string[] lines = File.ReadAllLines(path);
        Assert.Equal(new[] { "name,x,y,w,h,mtfValue", "replacement-mtf,1,2,3,4,27" }, lines);

        Task Apply(MySqlConnectedEndpoint endpoint) => fixture.View.ProcessResultAsync(new ResultMessage(
            ResultRoutes.Algorithm, ResultKinds.Algorithm, fixture.Device.Code, "MTF", "request-serial", string.Empty, -1,
            new ResultReference(MasterId, (int)ViewResultAlgType.MTF), attachment: null,
            databaseEndpoint: endpoint, sourceDevice: fixture.Device)).WaitAsync(LiveResultDeadline);
    });

    private sealed class LiveAlgorithmFixture : IDisposable
    {
        private readonly IConfigService previousConfig = ConfigService.Instance;
        private readonly List<ResourceDictionary> dictionaries = [];
        private readonly SpectrumNdSelectionTests.CalibrationMqttClient client;

        internal LiveAlgorithmFixture()
        {
            string code = $"algorithm-offline-{Guid.NewGuid():N}";
            Databases = new ResultDatabases(mtfDeviceCode: code);
            var transport = DispatchProxy.Create<IMqttClient, SpectrumNdSelectionTests.CalibrationMqttClient>();
            client = (SpectrumNdSelectionTests.CalibrationMqttClient)(object)transport;
            Mqtt = new MQTTControl(transport);
            try
            {
                TestEnvironment.SetConfig(new ConfigHandler { IsAutoSave = false });
                foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                {
                    var dictionary = new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                    dictionaries.Add(dictionary);
                    Application.Current.Resources.MergedDictionaries.Add(dictionary);
                }
                Device = CreateOwner(code, Databases.First);
                Device.DService = new MQTTAlgorithm(Device, Mqtt);
                var registration = new DockViewRegistration(() => new AlgorithmView(Device, deferInitialization: true), code, isVisible: true);
                typeof(DeviceAlgorithm).GetField("_view", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Device, registration);
                View = Device.ViewShell;
                View.Config.AutoRefreshView = false;
                View.Config.SaveSideDataDirPath = Databases.DirectoryPath;
                View.Config.AutoSaveSideData = true;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal ResultDatabases Databases { get; }
        internal DeviceAlgorithm Device { get; } = null!;
        internal AlgorithmView View { get; } = null!;
        internal MQTTControl Mqtt { get; }

        internal void SwitchDatabase()
        {
            Databases.Current = Databases.Second;
            Device.ResultDatabaseEndpoint = Databases.Second;
        }

        internal async Task<MsgRecord> SendManualMtf()
        {
            MsgRecord record = new AlgorithmMTF2(Device).SendCommand(new TJMTFParam { Id = 42, Name = "synthetic MTF2" },
                "input-camera", "Camera", "fixture.cvraw", FileExtType.Raw);
            string sent = await client.Commands.Reader.ReadAsync().AsTask().WaitAsync(LiveResultDeadline);
            var wire = JObject.Parse(sent);
            Assert.Equal(record.MsgID, wire.Value<string>("MsgID"));
            Assert.Equal("MTF", wire.Value<string>("EventName"));
            Assert.Equal("2.0", wire["params"]!.Value<string>("Version"));
            return record;
        }

        internal Task Reply(MsgRecord record, string? deviceCode = null, int code = 0, string? masterIdText = null, bool match = true) =>
            client.ReplyAsync(Device.Config.SubscribeTopic, new MsgReturn
            {
                EventName = "MTF", MsgID = match ? record.MsgID : "unmatched-mtf", SerialNumber = "service-assigned-serial",
                DeviceCode = deviceCode ?? Device.Code, Code = code,
                Data = new JObject { ["MasterId"] = masterIdText == null ? new JValue(MasterId) : new JValue(masterIdText),
                    ["MasterResultType"] = (int)ViewResultAlgType.MTF }
            }).WaitAsync(LiveResultDeadline);

        internal IDisposable ObserveBus(out ConcurrentQueue<ResultMessage> messages)
        {
            var captured = new ConcurrentQueue<ResultMessage>();
            messages = captured;
            return ResultMessageBus.Default.Subscribe(message =>
            {
                if (message.Route == ResultRoutes.Algorithm && message.DeviceCode == Device.Code) captured.Enqueue(message);
            });
        }

        internal Task ObserveNextResult()
        {
            var presented = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            View.ViewResults.CollectionChanged += (_, args) =>
            {
                if (args.Action == NotifyCollectionChangedAction.Add) presented.TrySetResult();
            };
            return presented.Task;
        }

        internal void AssertOwnedExport(ResultMessage message)
        {
            Assert.Equal((ResultKinds.Algorithm, MasterId), (message.ResultKind, message.Data.MasterId));
            Assert.Same(Device, message.SourceDevice);
            Assert.Same(Databases.First, message.DatabaseEndpoint);
            ViewResultAlg row = Assert.Single(View.ViewResults);
            Assert.Equal("original-owned-batch", row.Batch);
            Assert.Equal("original-master", row.ResultDesc);
            Assert.Same(Databases.First, row.AlgResultMasterModel.DatabaseEndpoint);
            string csv = Assert.Single(Directory.GetFiles(Databases.DirectoryPath, "*.csv"));
            Assert.Equal("original-owned-batchMTF.csv", Path.GetFileName(csv));
            Assert.Equal(new[] { "name,x,y,w,h,mtfValue", "original-mtf,1,2,3,4,7" }, File.ReadAllLines(csv));
            Assert.Equal(0, Databases.SecondReadCount);
        }

        internal void AssertNoExport()
        {
            Assert.Empty(View.ViewResults);
            Assert.Empty(Directory.GetFiles(Databases.DirectoryPath, "*.csv"));
            Assert.Equal(0, Databases.SecondReadCount);
        }

        internal static DeviceAlgorithm CreateOwner(string code, MySqlConnectedEndpoint endpoint)
        {
            var device = (DeviceAlgorithm)RuntimeHelpers.GetUninitializedObject(typeof(DeviceAlgorithm));
            device.Config = new ConfigAlgorithm { Code = code, SendTopic = "offline/algorithm/cmd", SubscribeTopic = "offline/algorithm/status" };
            device.SysResourceModel = new SysResourceModel { Id = -2, Code = code };
            device.ResultDatabaseEndpoint = endpoint;
            return device;
        }

        public void Dispose()
        {
            Device?.ViewRegistration?.Dispose();
            Device?.DService?.Dispose();
            foreach (ResourceDictionary dictionary in dictionaries)
                Application.Current.Resources.MergedDictionaries.Remove(dictionary);
            TestEnvironment.SetConfig(previousConfig);
            Databases.Dispose();
        }
    }
}
