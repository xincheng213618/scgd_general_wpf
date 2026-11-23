using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.Messages;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Views;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Templates;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.MQTT;
using MQTTnet;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SqlSugar;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public sealed partial class CameraResultSelectionTests
{
    private const string FixtureCameraCode = "offline-camera-result-selection";

    [Fact]
    public async Task LateManualCaptureCannotReadNewDatabaseBeforeItsOwnCaptureCompletes()
    {
        using var databases = new CameraResultDatabases();
        var transport = DispatchProxy.Create<IMqttClient, SpectrumNdSelectionTests.CalibrationMqttClient>();
        var client = (SpectrumNdSelectionTests.CalibrationMqttClient)(object)transport;
        var control = new MQTTControl(transport);
        var fixture = WpfTestHost.Invoke(() => new ResultViewFixture(control, databases.Read, endpoint: databases.First));
        using var subscription = ObserveCameraBus(fixture.Device, out var results);
        try
        {
            Assert.True(await control.Connect().WaitAsync(Deadline));
            MsgRecord oldCapture = await SendCameraCaptureAsync(fixture, client);
            WpfTestHost.Invoke(() => databases.SwitchToSecond(fixture.Device));
            MsgRecord newCapture = await SendCameraCaptureAsync(fixture, client);
            MsgRecord failedCapture = await SendCameraCaptureAsync(fixture, client);

            await ReplyCameraCaptureAsync(fixture, client, oldCapture, databases.MasterId);
            Assert.Equal(MsgRecordState.Success, await oldCapture.WorkerCompletion!.WaitAsync(Deadline));
            await ReplyCameraCaptureAsync(fixture, client, newCapture, databases.MasterId, code: 102);
            await ReplyCameraCaptureAsync(fixture, client, newCapture, databases.MasterId, deviceCode: "other-camera");
            await ReplyCameraCaptureAsync(fixture, client, newCapture, databases.MasterId, messageId: "unmatched-camera-result");
            Assert.Equal(MsgRecordState.Sended, newCapture.MsgRecordState);
            await ReplyCameraCaptureAsync(fixture, client, failedCapture, databases.MasterId, code: -1);
            Assert.Equal(MsgRecordState.Fail, await failedCapture.WorkerCompletion!.WaitAsync(Deadline));
            await PublishAndWaitForMarkerAsync(fixture, 91);

            Assert.Empty(results);
            Assert.Empty(databases.Reads);
            WpfTestHost.Invoke(() => Assert.Equal(91, Assert.Single(fixture.View.ViewResults).Id));

            await ReplyCameraCaptureAsync(fixture, client, newCapture, databases.MasterId);
            Assert.Equal(MsgRecordState.Success, await newCapture.WorkerCompletion!.WaitAsync(Deadline));
            await ReplyCameraCaptureAsync(fixture, client, newCapture, databases.MasterId);
            await PublishAndWaitForMarkerAsync(fixture, 92);

            ResultMessage notice = Assert.Single(results);
            Assert.Same(databases.Second, notice.DatabaseEndpoint);
            Assert.Same(fixture.Device, notice.SourceDevice);
            Assert.Same(fixture.Device.DService, notice.SourceService);
            Assert.Equal((databases.Second, databases.MasterId), Assert.Single(databases.Reads));
            WpfTestHost.Invoke(() => Assert.Equal("B", Assert.Single(fixture.View.ViewResults, row => row.Id == databases.MasterId).ResultMsg));
        }
        finally
        {
            await control.DisconnectAsyncClient().WaitAsync(Deadline);
            WpfTestHost.Invoke(fixture.Dispose);
        }
    }

    [Fact]
    public async Task NewCaptureReplacesTheSameIdAndPathFromThePreviousDatabase()
    {
        using var databases = new CameraResultDatabases(sharedFileName: "shared-capture.cvraw");
        var transport = DispatchProxy.Create<IMqttClient, SpectrumNdSelectionTests.CalibrationMqttClient>();
        var client = (SpectrumNdSelectionTests.CalibrationMqttClient)(object)transport;
        var control = new MQTTControl(transport);
        var fixture = WpfTestHost.Invoke(() =>
        {
            var view = new ResultViewFixture(control, databases.Read, endpoint: databases.First);
            ViewCamera.Config.AutoRefreshView = false;
            return view;
        });
        try
        {
            Assert.True(await control.Connect().WaitAsync(Deadline));
            await CaptureAndDrain(91);
            ViewResultImage original = WpfTestHost.Invoke(() =>
                Assert.Single(fixture.View.ViewResults, item => item.Id == databases.MasterId));
            Assert.Equal("A", original.ResultMsg);
            Assert.Same(databases.First, original.DatabaseEndpoint);

            WpfTestHost.Invoke(() => databases.SwitchToSecond(fixture.Device));
            await CaptureAndDrain(92);
            WpfTestHost.Invoke(() =>
            {
                ViewResultImage replacement = Assert.Single(fixture.View.ViewResults, item => item.Id == databases.MasterId);
                Assert.NotSame(original, replacement);
                Assert.Equal(original.FileUrl, replacement.FileUrl);
                Assert.Equal("shared-capture.cvraw", Path.GetFileName(replacement.FileUrl));
                Assert.Equal("B", replacement.ResultMsg);
                Assert.Same(databases.Second, replacement.DatabaseEndpoint);
            });
            Assert.Equal(new[] { (databases.First, databases.MasterId), (databases.Second, databases.MasterId) }, databases.Reads);
        }
        finally
        {
            await control.DisconnectAsyncClient().WaitAsync(Deadline);
            WpfTestHost.Invoke(fixture.Dispose);
        }

        async Task CaptureAndDrain(int marker)
        {
            MsgRecord capture = await SendCameraCaptureAsync(fixture, client);
            await ReplyCameraCaptureAsync(fixture, client, capture, databases.MasterId);
            Assert.Equal(MsgRecordState.Success, await capture.WorkerCompletion!.WaitAsync(Deadline));
            await PublishAndWaitForMarkerAsync(fixture, marker);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyFlowCameraOnlyPresentsAMatchedResultFromItsAdmittedDatabase(bool reconnect)
    {
        using var databases = new CameraResultDatabases();
        var transport = DispatchProxy.Create<IMqttClient, SpectrumNdSelectionTests.CalibrationMqttClient>();
        var control = new MQTTControl(transport);
        var fixture = WpfTestHost.Invoke(() => new ResultViewFixture(control, databases.Read, endpoint: databases.First));
        using var subscription = ObserveCameraBus(fixture.Device, out var results);
        try
        {
            await WpfTestHost.Invoke(async () =>
            {
                using var graph = new CVNodeContainer();
                var node = new CVCameraNode();
                var start = new RuntimeTestStartNode();
                var end = new RuntimeTestEndNode();
                foreach (STNode item in new STNode[] { start, node, end })
                {
                    item.Create();
                    graph.Nodes.Add(item);
                }
                node.DeviceCode = fixture.Device.Code;
                node.RemoteResultBinderOverride = RemoteDeviceFlowResult.CreateBinder(code => code == fixture.Device.Code ? fixture.Device : null);
                STNodeOption input = Assert.Single(node.GetAllInputOptions(), option => option.DataType == typeof(CVStartCFC));
                STNodeOption output = Assert.Single(node.GetAllOutputOptions(), option => option.DataType == typeof(CVStartCFC));
                Assert.Equal(ConnectionStatus.Connected, start.m_op_start.ConnectOption(input, false));
                Assert.Equal(ConnectionStatus.Connected, output.ConnectOption(end.m_in_start, false));
                using var engine = new FlowEngineControl(graph, false, new FlowNodeManager());
                fixture.Device.Config.SendTopic = node.GetSendTopic();
                fixture.Device.Config.SubscribeTopic = node.GetRecvTopic();
                var completed = new TaskCompletionSource<FlowEngineEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
                engine.Finished += (_, args) => completed.TrySetResult(args);
                MQActionEvent? sent = null;
                start.OnPublish = action => sent = action;

                Assert.True(engine.TryStartNode(start.NodeName, "camera-flow-test"));
                CVMQTTRequest request = JsonConvert.DeserializeObject<CVMQTTRequest>(Assert.IsType<MQActionEvent>(sent).Message)!;
                Assert.Equal("GetData", request.EventName);
                string topic = Assert.Single(start.SubscribedTopics, candidate => candidate == node.GetRecvTopic());
                Assert.False(node.DoServerStatusRecv(Response(102)));
                Assert.False(node.DoServerStatusRecv(Response(deviceCode: "other-camera")));
                Assert.False(node.DoServerStatusRecv(Response(messageId: "unmatched-flow-capture")));
                Assert.Empty(results);
                if (reconnect) databases.SwitchToSecond(fixture.Device);

                Assert.True(node.DoServerStatusRecv(Response()));
                Assert.Equal(StatusTypeEnum.Completed, (await completed.Task.WaitAsync(Deadline)).Status);
                Assert.False(node.DoServerStatusRecv(Response()));
                await PublishAndWaitForMarkerAsync(fixture, 91);

                if (reconnect)
                {
                    Assert.Empty(results);
                    Assert.Empty(databases.Reads);
                    Assert.Equal(91, Assert.Single(fixture.View.ViewResults).Id);
                }
                else
                {
                    ResultMessage notice = Assert.Single(results);
                    Assert.Same(databases.First, notice.DatabaseEndpoint);
                    Assert.Same(fixture.Device, notice.SourceDevice);
                    Assert.Same(fixture.Device.DService, notice.SourceService);
                    Assert.Equal((node.NodeID, request.SerialNumber, databases.MasterId), (notice.NodeId, notice.SerialNumber, notice.Data.MasterId));
                    Assert.Equal((databases.First, databases.MasterId), Assert.Single(databases.Reads));
                    Assert.Equal("A", Assert.Single(fixture.View.ViewResults, row => row.Id == databases.MasterId).ResultMsg);
                }

                CVBaseDataFlowResp Response(int code = 0, string? deviceCode = null, string? messageId = null) => new()
                {
                    Code = code, MsgID = messageId ?? request.MsgID, SerialNumber = request.SerialNumber, ZIndex = request.ZIndex,
                    ServiceName = request.ServiceCode, DeviceNodeCode = node.NodeID, DeviceCode = deviceCode ?? request.DeviceCode,
                    EventName = request.EventName, ReceivedTopic = topic,
                    Data = new JObject { ["MasterId"] = databases.MasterId, ["MasterResultType"] = 100 },
                };
            });
        }
        finally { WpfTestHost.Invoke(fixture.Dispose); }
    }

    [Fact]
    public async Task CameraViewRejectsTheRetiredOwnerAndServiceOfTheSameDeviceCode()
    {
        using var databases = new CameraResultDatabases();
        var transport = DispatchProxy.Create<IMqttClient, SpectrumNdSelectionTests.CalibrationMqttClient>();
        var control = new MQTTControl(transport);
        var fixture = WpfTestHost.Invoke(() => new ResultViewFixture(control, databases.Read, endpoint: databases.First));
        try
        {
            var retiredOwner = (DeviceCamera)RuntimeHelpers.GetUninitializedObject(typeof(DeviceCamera));
            retiredOwner.SysResourceModel = fixture.Device.SysResourceModel;
            retiredOwner.Config = fixture.Device.Config;
            using var retiredService = WpfTestHost.Invoke(() => new MQTTCamera(fixture.Device, control, (_, _) => { }));

            Publish(retiredOwner, fixture.Device.DService);
            Publish(fixture.Device, retiredService);
            await PublishAndWaitForMarkerAsync(fixture, 91);

            Assert.Empty(databases.Reads);
            WpfTestHost.Invoke(() => Assert.Equal(91, Assert.Single(fixture.View.ViewResults).Id));

            void Publish(DeviceCamera owner, MQTTCamera service) => ResultMessageBus.Default.PublishPersisted(
                ResultRoutes.Camera, ResultKinds.Image, fixture.Device.Code, "GetData", "previous-camera", "", 0,
                databases.MasterId, 100, databaseEndpoint: databases.First, sourceDevice: owner, sourceService: service);
        }
        finally { WpfTestHost.Invoke(fixture.Dispose); }
    }

    private static async Task<MsgRecord> SendCameraCaptureAsync(ResultViewFixture fixture, SpectrumNdSelectionTests.CalibrationMqttClient client)
    {
        MsgRecord record = WpfTestHost.Invoke(() => fixture.Device.DService.GetData([1], new CalibrationParam { Id = -1 }, new ParamBase { Id = -1 }));
        string wire = await client.Commands.Reader.ReadAsync().AsTask().WaitAsync(Deadline);
        var command = JObject.Parse(wire);
        Assert.Equal(record.MsgID, command.Value<string>("MsgID"));
        Assert.Equal("GetData", command.Value<string>("EventName"));
        return record;
    }

    private static Task ReplyCameraCaptureAsync(ResultViewFixture fixture, SpectrumNdSelectionTests.CalibrationMqttClient client,
        MsgRecord record, int masterId, int code = 0, string? deviceCode = null, string? messageId = null) =>
        client.ReplyAsync(record.SubscribeTopic, new MsgReturn
        {
            MsgID = messageId ?? record.MsgID, DeviceCode = deviceCode ?? fixture.Device.Code,
            EventName = "GetData", SerialNumber = record.MsgSend.SerialNumber, Code = code,
            Data = new JObject { ["MasterId"] = masterId, ["MasterResultType"] = 100 },
        }).WaitAsync(Deadline);

    private static IDisposable ObserveCameraBus(DeviceCamera device, out ConcurrentQueue<ResultMessage> results)
    {
        var messages = new ConcurrentQueue<ResultMessage>();
        results = messages;
        return ResultMessageBus.Default.Subscribe(message =>
        {
            if (message.Route == ResultRoutes.Camera && message.DeviceCode == device.Code && message.Attachment == null)
                messages.Enqueue(message);
        });
    }

    private static async Task PublishAndWaitForMarkerAsync(ResultViewFixture fixture, int id)
    {
        var presented = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        WpfTestHost.Invoke(() => fixture.View.ViewResults.CollectionChanged += (_, change) =>
        {
            if (change.NewItems?[0] is ViewResultImage result && result.Id == id) presented.TrySetResult();
        });
        ResultMessageBus.Default.Publish(new(ResultRoutes.Camera, ResultKinds.Image, fixture.Device.Code, "GetData", "", "", 0,
            new ResultReference(id, 100), new MeasureResultImgModel { Id = id }));
        await presented.Task.WaitAsync(Deadline);
    }

    private sealed class CameraResultDatabases : IDisposable
    {
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("cv-camera-live-results-");
        private MySqlConnectedEndpoint current;

        internal CameraResultDatabases(string? sharedFileName = null)
        {
            First = Endpoint("A");
            Second = Endpoint("B");
            current = First;
            string? fileUrl = sharedFileName == null ? null : Path.Combine(directory.FullName, sharedFileName);
            MasterId = Seed(First, "A", fileUrl);
            Assert.Equal(MasterId, Seed(Second, "B", fileUrl));
        }

        internal MySqlConnectedEndpoint First { get; }
        internal MySqlConnectedEndpoint Second { get; }
        internal int MasterId { get; }
        internal ConcurrentQueue<(MySqlConnectedEndpoint Endpoint, int MasterId)> Reads { get; } = new();
        internal Action<string>? BeforeOpen { get; set; }

        internal void SwitchToSecond(DeviceCamera device)
        {
            Volatile.Write(ref current, Second);
            device.ResultDatabaseEndpoint = Second;
        }

        internal MeasureResultImgModel? Read(MySqlConnectedEndpoint endpoint, int id)
        {
            Reads.Enqueue((endpoint, id));
            return MeasureImgResultDao.ReadResult(endpoint, id);
        }

        internal void SetFileUrl(MySqlConnectedEndpoint endpoint, string path)
        {
            using var db = endpoint.CreateDbClient();
            Assert.Equal(1, db.Updateable<MeasureResultImgModel>().SetColumns(row => row.FileUrl == path)
                .Where(row => row.Id == MasterId).ExecuteCommand());
        }

        private MySqlConnectedEndpoint Endpoint(string name) => new(candidate => ReferenceEquals(Volatile.Read(ref current), candidate),
            () =>
            {
                BeforeOpen?.Invoke(name);
                return new SqlSugarClient(new ConnectionConfig
                {
                    ConnectionString = $"Data Source={Path.Combine(directory.FullName, name + ".db")};Pooling=False",
                    DbType = DbType.Sqlite, InitKeyType = InitKeyType.Attribute, IsAutoCloseConnection = true,
                });
            });

        private static int Seed(MySqlConnectedEndpoint endpoint, string result, string? fileUrl)
        {
            using var db = endpoint.CreateDbClient();
            db.CodeFirst.InitTables<MeasureResultImgModel>();
            return db.Insertable(new MeasureResultImgModel { DeviceCode = FixtureCameraCode, Result = result, FileUrl = fileUrl }).ExecuteReturnIdentity();
        }

        public void Dispose()
        {
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    }
}
