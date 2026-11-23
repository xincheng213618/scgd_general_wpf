using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Messages;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Calibration.Views;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Services.Results;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Views;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.MQTT;
using FlowEngineLib.Node.Algorithm;
using MQTTMessageLib.FileServer;
using MQTTnet;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SqlSugar;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CalibrationLoopNode = FlowEngineLib.Algorithm.CalibrationLoopNode;
using CalibrationNode = FlowEngineLib.Algorithm.CalibrationNode;

namespace ColorVision.UI.Tests;

public sealed partial class CalibrationResultOwnershipTests
{
    private const string ReplyTopic = "offline/calibration/shared/status";
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task OnlyTheMatchedSuccessfulRequestOnASharedTopicReachesItsDeviceView()
    {
        using var fixture = WpfTestHost.Invoke(() => new Fixture());
        using var events = fixture.ObserveBus(out var notices);
        Task added = WpfTestHost.Invoke(() => ObserveNextResult(fixture.FirstView));
        Assert.True(await fixture.Control.Connect().WaitAsync(Deadline));
        try
        {
            MsgRecord first = WpfTestHost.Invoke(() => StartCalibration(fixture.First, "first-request"));
            await fixture.WaitForSent(first);
            MsgRecord second = WpfTestHost.Invoke(() => StartCalibration(fixture.Second, "second-request"));
            await fixture.WaitForSent(second);

            await fixture.Reply(first, fixture.Second, fixture.FirstImageId);
            await fixture.Reply(first, fixture.First, masterId: 0);
            await fixture.Reply(first, fixture.First, fixture.FirstImageId, includeCode: false);
            await fixture.Reply(first, fixture.First, fixture.FirstImageId, code: 102);
            await fixture.Reply(first, fixture.First, fixture.FirstImageId, messageId: "unmatched-calibration");
            Assert.Equal(MsgRecordState.Sended, first.MsgRecordState);
            Assert.Empty(notices);

            await fixture.Reply(second, fixture.Second, fixture.SecondImageId, code: 500);
            Assert.Equal(MsgRecordState.Fail, await second.WorkerCompletion!.WaitAsync(Deadline));
            Assert.Empty(notices);

            await fixture.Reply(first, fixture.First, fixture.FirstImageId);
            Assert.Equal(MsgRecordState.Success, await first.WorkerCompletion!.WaitAsync(Deadline));
            await added.WaitAsync(Deadline);
            await fixture.Reply(first, fixture.First, fixture.FirstImageId);

            ResultMessage notice = Assert.Single(notices);
            Assert.Equal((ResultRoutes.Calibration, ResultKinds.Image, fixture.First.Code, "first-request", fixture.FirstImageId),
                (notice.Route, notice.ResultKind, notice.DeviceCode, notice.SerialNumber, notice.Data.MasterId));
            Assert.Same(fixture.Original, notice.DatabaseEndpoint);
            Assert.Same(fixture.First, notice.SourceDevice);
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal("original-first", Assert.Single(fixture.FirstView.ViewResults).ResultMsg);
                Assert.Empty(fixture.SecondView.ViewResults);
            });
        }
        finally { await fixture.Control.DisconnectAsyncClient().WaitAsync(Deadline); }
    }

    [Fact]
    public async Task AManualRequestCannotPublishItsSameIdIntoTheReplacementDatabase()
    {
        using var fixture = WpfTestHost.Invoke(() => new Fixture());
        using var events = fixture.ObserveBus(out var notices);
        Assert.True(await fixture.Control.Connect().WaitAsync(Deadline));
        try
        {
            MsgRecord admitted = WpfTestHost.Invoke(() => StartCalibration(fixture.First, "old-endpoint"));
            await fixture.WaitForSent(admitted);
            WpfTestHost.Invoke(fixture.SwitchDatabase);

            await fixture.Reply(admitted, fixture.First, fixture.FirstImageId);
            Assert.Equal(MsgRecordState.Success, await admitted.WorkerCompletion!.WaitAsync(Deadline));
            Assert.Empty(notices);
            Assert.Equal(0, fixture.ReplacementReads);
            WpfTestHost.Invoke(() => Assert.Empty(fixture.FirstView.ViewResults));
        }
        finally { await fixture.Control.DisconnectAsyncClient().WaitAsync(Deadline); }
    }

    [Fact]
    public async Task ViewRejectsUntaggedForeignAndLateResultsBeforeApplyingTheImage()
    {
        using var fixture = WpfTestHost.Invoke(() => new Fixture());
        await Apply(fixture.Message(fixture.First, fixture.FirstImageId, null));
        await Apply(fixture.Message(fixture.Second, fixture.SecondImageId, fixture.Original));
        await Apply(fixture.Message(fixture.First, fixture.FirstImageId, fixture.Replacement));
        var retiredOwner = (DeviceCalibration)RuntimeHelpers.GetUninitializedObject(typeof(DeviceCalibration));
        retiredOwner.Config = fixture.First.Config;
        retiredOwner.SysResourceModel = fixture.First.SysResourceModel;
        retiredOwner.ResultDatabaseEndpoint = fixture.Original;
        await Apply(fixture.Message(fixture.First, fixture.FirstImageId, fixture.Original, retiredOwner));
        Assert.Equal(0, fixture.OriginalReads);
        Assert.Equal(0, fixture.ReplacementReads);

        // A message addressed to the right view cannot authorize a row owned by a different device.
        await Apply(fixture.Message(fixture.First, fixture.SecondImageId, fixture.Original));
        WpfTestHost.Invoke(() => Assert.Empty(fixture.FirstView.ViewResults));

        var lookupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowLookup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.BeforeOriginalRead = () =>
        {
            lookupStarted.TrySetResult();
            allowLookup.Task.WaitAsync(Deadline).GetAwaiter().GetResult();
        };
        Task late = Apply(fixture.Message(fixture.First, fixture.FirstImageId, fixture.Original));
        try
        {
            await lookupStarted.Task.WaitAsync(Deadline);
            WpfTestHost.Invoke(fixture.SwitchDatabase);
        }
        finally { allowLookup.TrySetResult(); }
        await late;
        WpfTestHost.Invoke(() => Assert.Empty(fixture.FirstView.ViewResults));

        await Apply(fixture.Message(fixture.First, fixture.FirstImageId, fixture.Replacement));
        WpfTestHost.Invoke(() => Assert.Equal("replacement-first", Assert.Single(fixture.FirstView.ViewResults).ResultMsg));
        Assert.Equal(1, fixture.ReplacementReads);

        Task Apply(ResultMessage message) => WpfTestHost.Invoke(() => fixture.FirstView.ProcessResultAsync(message)).WaitAsync(Deadline);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task LegacyFlowPublishesOneOwnedImageAndIgnoresRepliesAfterReconnect(bool loopNode, bool reconnect)
    {
        using var fixture = WpfTestHost.Invoke(() => new Fixture());
        using var events = fixture.ObserveBus(out var notices);
        await WpfTestHost.Invoke(async () =>
        {
            fixture.First.DisplayConfig.UseLocalCalibration = false;
            using var graph = new CVNodeContainer();
            var start = new RuntimeTestStartNode();
            CVBaseServerNode node = loopNode
                ? new CalibrationLoopNode(RemoteCalibrationFlowResult.CreateBinder(FindDevice))
                : new CalibrationNode(FindDevice);
            var end = new RuntimeTestEndNode();
            foreach (STNode item in new STNode[] { start, node, end })
            {
                item.Create();
                graph.Nodes.Add(item);
            }
            node.DeviceCode = fixture.First.Code;
            if (node is CalibrationLoopNode loop) loop.Params = [CreateLoopStep()];
            var input = Assert.Single(node.GetAllInputOptions(), option => option.DataType == typeof(CVStartCFC));
            var output = Assert.Single(node.GetAllOutputOptions(), option => option.DataType == typeof(CVStartCFC));
            Assert.Equal(ConnectionStatus.Connected, start.m_op_start.ConnectOption(input, false));
            Assert.Equal(ConnectionStatus.Connected, output.ConnectOption(end.m_in_start, false));
            var finished = new TaskCompletionSource<FlowEngineEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var engine = new FlowEngineControl(graph, false, new FlowNodeManager()) { PersistResults = true };
            fixture.First.Config.SendTopic = node.GetSendTopic();
            fixture.First.Config.SubscribeTopic = node.GetRecvTopic();
            engine.Finished += (_, result) => finished.TrySetResult(result);
            MQActionEvent? command = null;
            start.OnPublish = action => command = action;
            Task presented = ObserveNextResult(fixture.FirstView);

            Assert.True(engine.TryStartNode(start.NodeName, "legacy-flow-calibration"));
            CVMQTTRequest request = JsonConvert.DeserializeObject<CVMQTTRequest>(Assert.IsType<MQActionEvent>(command).Message)!;
            string subscribedTopic = Assert.Single(start.SubscribedTopics, topic => topic == node.GetRecvTopic());
            Assert.Equal("Calibration", request.EventName);
            if (loopNode)
            {
                Assert.Equal(1, request.ZIndex);
                Assert.Equal("offline-loop-input.cvraw", Assert.IsType<JObject>(request.Data).Value<string>("ImgFileName"));
            }
            Assert.False(node.DoServerStatusRecv(Response(request.MsgID, topic: subscribedTopic + "/foreign")));
            Assert.False(node.DoServerStatusRecv(Response(request.MsgID, deviceCode: fixture.Second.Code)));
            Assert.False(node.DoServerStatusRecv(Response(request.MsgID, code: 102)));
            Assert.False(node.DoServerStatusRecv(Response("unmatched-flow-calibration")));
            Assert.Empty(notices);
            if (reconnect) fixture.SwitchDatabase();

            Assert.True(node.DoServerStatusRecv(Response(request.MsgID)));
            Assert.Equal(StatusTypeEnum.Completed, (await finished.Task.WaitAsync(Deadline)).Status);
            Assert.Equal(1, start.PublishCount);
            Assert.False(node.DoServerStatusRecv(Response(request.MsgID)));
            if (reconnect)
            {
                Assert.Empty(notices);
                Assert.Empty(fixture.FirstView.ViewResults);
                Assert.Equal(0, fixture.ReplacementReads);
            }
            else
            {
                await presented.WaitAsync(Deadline);
                var notice = Assert.Single(notices);
                Assert.Equal((node.NodeID, request.SerialNumber, request.ZIndex, fixture.FirstImageId),
                    (notice.NodeId, notice.SerialNumber, notice.ZIndex, notice.Data.MasterId));
                Assert.Same(fixture.Original, notice.DatabaseEndpoint);
                Assert.Same(fixture.First, notice.SourceDevice);
                Assert.Equal("original-first", Assert.Single(fixture.FirstView.ViewResults).ResultMsg);
                Assert.Empty(fixture.SecondView.ViewResults);
            }

            DeviceCalibration FindDevice(string code) => code == fixture.First.Code ? fixture.First : null!;

            CVBaseDataFlowResp Response(string messageId, int code = 0, string? deviceCode = null, string? topic = null) => new()
            {
                Code = code, Message = "synthetic response", MsgID = messageId, Version = request.Version,
                DeviceCode = deviceCode ?? request.DeviceCode, DeviceNodeCode = node.NodeID, EventName = request.EventName,
                SerialNumber = request.SerialNumber, ServiceName = request.ServiceCode, ZIndex = request.ZIndex,
                ReceivedTopic = topic ?? subscribedTopic,
                Data = new JObject { ["MasterId"] = fixture.FirstImageId, ["MasterResultType"] = 30 }
            };
        });
    }

    [Fact]
    public void LegacyCalibrationLoopRetainsItsSavedIdentityAndParameters() => StaTest.Run(() =>
    {
        const string model = "FlowEngineLib.dll|FlowEngineLib.Algorithm.CalibrationLoopNode";
        Assert.Equal(model, STNodeTypeRegistry.GetModelByType(typeof(CalibrationLoopNode)));
        Assert.True(STNodeTypeRegistry.TryGetNodeType(string.Empty, model, out Type restoredType));
        Assert.Equal(typeof(CalibrationLoopNode), restoredType);

        using var graph = new CVNodeContainer();
        var node = new CalibrationLoopNode();
        node.Create();
        node.Params = [CreateLoopStep()];
        graph.Nodes.Add(node);
        string nodeId = node.NodeID;

        graph.LoadCanvas(graph.GetCanvasData());
        var restored = Assert.IsType<CalibrationLoopNode>(Assert.Single(graph.Nodes.Cast<STNode>()));
        Assert.Equal(nodeId, restored.NodeID);
        CalibrationNodeProperty step = Assert.Single(restored.Params);
        Assert.Equal(CreateLoopStep().ToItemArray(1), step.ToItemArray(1));
    });

    private static CalibrationNodeProperty CreateLoopStep() => new()
    {
        ImgFileName = "offline-loop-input.cvraw", TempName = "loop-template", ExpTempName = "loop-exposure"
    };

    private static MsgRecord StartCalibration(DeviceCalibration device, string serialNumber) =>
        device.DService.Calibration(new CalibrationParam(), "offline-input.cvraw", FileExtType.Raw, 17, "test", serialNumber, 1, 1, 1);

    private static Task ObserveNextResult(ViewCalibration view)
    {
        var added = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        view.ViewResults.CollectionChanged += (_, args) =>
        {
            if (args.Action == NotifyCollectionChangedAction.Add) added.TrySetResult();
        };
        return added.Task;
    }

    private sealed class Fixture : IDisposable
    {
        private readonly IConfigService previousConfig = ConfigService.Instance;
        private readonly List<ResourceDictionary> dictionaries = [];
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("cv-calibration-result-ownership-");
        private readonly SpectrumNdSelectionTests.CalibrationMqttClient client;
        private MySqlConnectedEndpoint active;
        private int originalReads;
        private int replacementReads;

        internal Fixture()
        {
            var transport = DispatchProxy.Create<IMqttClient, SpectrumNdSelectionTests.CalibrationMqttClient>();
            client = (SpectrumNdSelectionTests.CalibrationMqttClient)(object)transport;
            Control = new MQTTControl(transport);
            Original = new MySqlConnectedEndpoint(IsCurrent, () =>
            {
                Interlocked.Increment(ref originalReads);
                BeforeOriginalRead?.Invoke();
                return Open("original");
            });
            Replacement = new MySqlConnectedEndpoint(IsCurrent, () =>
            {
                Interlocked.Increment(ref replacementReads);
                return Open("replacement");
            });
            active = Original;

            try
            {
                TestEnvironment.SetConfig(new ConfigHandler { IsAutoSave = false });
                foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                {
                    var dictionary = new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                    dictionaries.Add(dictionary);
                    Application.Current.Resources.MergedDictionaries.Add(dictionary);
                }
                ViewCalibration.Config.AutoRefreshView = false;
                First = CreateOwner("first");
                Second = CreateOwner("second");
                (FirstImageId, SecondImageId) = Seed("original");
                Assert.Equal((FirstImageId, SecondImageId), Seed("replacement"));
                FirstView = First.ViewShell;
                SecondView = Second.ViewShell;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal MQTTControl Control { get; }
        internal MySqlConnectedEndpoint Original { get; }
        internal MySqlConnectedEndpoint Replacement { get; }
        internal DeviceCalibration First { get; } = null!;
        internal DeviceCalibration Second { get; } = null!;
        internal ViewCalibration FirstView { get; } = null!;
        internal ViewCalibration SecondView { get; } = null!;
        internal int FirstImageId { get; }
        internal int SecondImageId { get; }
        internal int OriginalReads => Volatile.Read(ref originalReads);
        internal int ReplacementReads => Volatile.Read(ref replacementReads);
        internal Action? BeforeOriginalRead { get; set; }

        internal void SwitchDatabase()
        {
            Volatile.Write(ref active, Replacement);
            First.ResultDatabaseEndpoint = Replacement;
            Second.ResultDatabaseEndpoint = Replacement;
        }

        internal string SeedHistory()
        {
            const string cameraCode = "offline-calibration-history-camera";
            First.Config.CameraCode = cameraCode;
            foreach (string name in new[] { "original", "replacement" })
            {
                using var db = Open(name);
                string previewPath = Path.Combine(directory.FullName, name + ".png");
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2, 2, 96, 96, PixelFormats.Gray8, null, new byte[4], 2)));
                using (FileStream preview = File.Create(previewPath)) encoder.Save(preview);
                Assert.Equal(1, db.Updateable<MeasureResultImgModel>().SetColumns(row => row.FileUrl == previewPath)
                    .Where(row => row.Id == FirstImageId).ExecuteCommand());

                Insert(First.Code, "with-legacy-parameters", "legacy non-JSON parameters");
                Insert(cameraCode, "calibrated-camera", new JObject { ["CalibrationDeviceCode"] = First.Code }.ToString(Formatting.None));
                Insert(cameraCode, "raw-camera");
                Insert(null, "untagged");
                Insert(First.Code, "owned-by-second", new JObject { ["CalibrationDeviceCode"] = Second.Code }.ToString(Formatting.None));
                Insert(First.Code.ToUpperInvariant(), "wrong-case");

                void Insert(string? code, string label, string? parameters = null) =>
                    db.Insertable(new MeasureResultImgModel { DeviceCode = code, Result = $"{name}-{label}", Params = parameters })
                        .ExecuteCommand();
            }
            return cameraCode;
        }

        internal int CountHistoryRows(string name)
        {
            using var db = Open(name);
            return db.Queryable<MeasureResultImgModel>().Count();
        }

        internal Task Reply(MsgRecord record, DeviceCalibration sender, int masterId, int code = 0, string? messageId = null, bool includeCode = true)
        {
            JObject json = JObject.FromObject(new MsgReturn
            {
                EventName = "Calibration", MsgID = messageId ?? record.MsgID, SerialNumber = record.MsgSend.SerialNumber,
                DeviceCode = sender.Code, Code = code, Data = new JObject { ["MasterId"] = masterId }, Message = "synthetic reply"
            });
            if (!includeCode) json.Remove(nameof(MsgReturn.Code));
            return client.ReplyAsync(ReplyTopic, json.ToString(Formatting.None)).WaitAsync(Deadline);
        }

        internal async Task WaitForSent(MsgRecord record)
        {
            string wire = await client.Commands.Reader.ReadAsync().AsTask().WaitAsync(Deadline);
            Assert.Equal(record.MsgID, JObject.Parse(wire).Value<string>("MsgID"));
        }

        internal ResultMessage Message(DeviceCalibration device, int masterId, MySqlConnectedEndpoint? endpoint, DeviceCalibration? source = null) => new(
            ResultRoutes.Calibration, ResultKinds.Image, device.Code, "Calibration", "fixture", string.Empty, -1,
            new ResultReference(masterId, 30), attachment: null, databaseEndpoint: endpoint, sourceDevice: source);

        internal IDisposable ObserveBus(out ConcurrentQueue<ResultMessage> notices)
        {
            var messages = new ConcurrentQueue<ResultMessage>();
            notices = messages;
            return ResultMessageBus.Default.Subscribe(message =>
            {
                if (message.Route == ResultRoutes.Calibration && (message.DeviceCode == First.Code || message.DeviceCode == Second.Code))
                    messages.Enqueue(message);
            });
        }

        private bool IsCurrent(MySqlConnectedEndpoint endpoint) => ReferenceEquals(endpoint, Volatile.Read(ref active));

        private DeviceCalibration CreateOwner(string name)
        {
            var device = (DeviceCalibration)RuntimeHelpers.GetUninitializedObject(typeof(DeviceCalibration));
            string code = $"offline-calibration-{name}-{Guid.NewGuid():N}";
            device.Config = new ConfigCalibration { Code = code, SendTopic = "offline/calibration/shared/cmd", SubscribeTopic = ReplyTopic };
            device.SysResourceModel = new SysResourceModel { Id = -2, Code = code };
            device.ResultDatabaseEndpoint = Original;
            device.DService = new MQTTCalibration(device, Control);
            var registration = new DockViewRegistration(() => new ViewCalibration(device, deferInitialization: true), code, isVisible: true);
            typeof(DeviceCalibration).GetField("_view", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(device, registration);
            return device;
        }

        private (int FirstId, int SecondId) Seed(string name)
        {
            using var db = Open(name);
            db.CodeFirst.InitTables<MeasureResultImgModel>();
            return (Insert(First, "first"), Insert(Second, "second"));

            int Insert(DeviceCalibration owner, string label) => db.Insertable(new MeasureResultImgModel
                { DeviceCode = owner.Code, Result = $"{name}-{label}" }).ExecuteReturnIdentity();
        }

        private SqlSugarClient Open(string name) => new(new ConnectionConfig
        {
            ConnectionString = $"Data Source={Path.Combine(directory.FullName, name + ".db")};Pooling=False",
            DbType = DbType.Sqlite, IsAutoCloseConnection = true, InitKeyType = InitKeyType.Attribute
        });

        public void Dispose()
        {
            WpfTestHost.Invoke(() =>
            {
                foreach (DeviceCalibration? device in new[] { First, Second })
                {
                    device?.ViewRegistration.Dispose();
                    device?.DService.Dispose();
                }
                foreach (ResourceDictionary dictionary in dictionaries)
                    Application.Current.Resources.MergedDictionaries.Remove(dictionary);
                TestEnvironment.SetConfig(previousConfig);
            });
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    }
}
