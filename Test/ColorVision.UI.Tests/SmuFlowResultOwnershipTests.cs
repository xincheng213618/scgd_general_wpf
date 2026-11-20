using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.Devices.SMU;
using ColorVision.Engine.Services.Devices.SMU.Configs;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.SMU.Local;
using ColorVision.Engine.Services.Devices.SMU.Views;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.MQTT;
using FlowEngineLib.Node.SMU;
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
using System.Threading.Channels;

namespace ColorVision.UI.Tests;

public sealed class SmuFlowResultOwnershipTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RetiredPointOrTemplateSweepCannotReadItsResultFromTheReplacementDatabase(bool scan) =>
        RunGraphAsync(() => scan ? new SMUSweepModelNode() : new SMUNode(), new Version(4, 0, 2, 115), async fixture =>
        {
            var directory = Directory.CreateTempSubdirectory("cv-smu-flow-");
            try
            {
                var lookups = new ConcurrentQueue<string>();
                MySqlConnectedEndpoint? current = null;
                var first = Seed("first", 2);
                var next = Seed("next", 8);
                Assert.Equal(first.Id, next.Id);

                current = first.Endpoint;
                fixture.Owner.ResultDatabaseEndpoint = first.Endpoint;
                CVMQTTRequest request = fixture.Start("first", first.Endpoint);
                Assert.Equal(scan ? "Scan" : "GetData", request.EventName);
                Assert.False(fixture.Receive(request, Point(first.Id), code: 102));
                Assert.False(fixture.Receive(request, Point(first.Id), receivedTopic: "test/unrelated/status"));
                Assert.Empty(lookups);

                // The old command still finishes the graph. Its result has no authority over the newly bound database.
                current = next.Endpoint;
                fixture.Owner.ResultDatabaseEndpoint = next.Endpoint;
                await fixture.CompleteAsync(request, Point(first.Id));
                Assert.False(fixture.Receive(request, Point(first.Id)));

                var nextRequest = fixture.Start("next", next.Endpoint);
                await fixture.CompleteAsync(nextRequest, Point(next.Id));
                var result = await fixture.ReadAsync();
                Assert.Equal(next.Id, result.Id);
                Assert.Equal(8d, result.VList[0]);
                Assert.Equal("next", Assert.Single(lookups));

                (MySqlConnectedEndpoint Endpoint, int Id) Seed(string name, float voltage)
                {
                    using var db = Open(name);
                    db.CodeFirst.InitTables(typeof(SMUResultModel), typeof(SmuScanModel));
                    int id = scan
                        ? db.Insertable(new SmuScanModel { DeviceCode = fixture.Owner.Code, VResult = FormattableString.Invariant($"[{voltage}]"), IResult = "[0.01]", CreateDate = DateTime.Now }).ExecuteReturnIdentity()
                        : db.Insertable(new SMUResultModel { DeviceCode = fixture.Owner.Code, VResult = voltage, IResult = 10, CreateDate = DateTime.Now }).ExecuteReturnIdentity();
                    return (new(endpoint => ReferenceEquals(Volatile.Read(ref current), endpoint), () =>
                    {
                        lookups.Enqueue(name);
                        return Open(name);
                    }), id);
                }

                SqlSugarClient Open(string name) => new(new ConnectionConfig
                {
                    ConnectionString = $"Data Source={Path.Combine(directory.FullName, name + ".db")};Pooling=False",
                    DbType = DbType.Sqlite, InitKeyType = InitKeyType.Attribute, IsAutoCloseConnection = true,
                });
            }
            finally
            {
                foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
                directory.Delete();
            }
        });

    [Fact]
    public Task InlineSweepUsesTheSubmittedParametersWithoutADatabase() =>
        RunGraphAsync(() => new SMUSweepNode(), new Version(4, 0, 2, 114), async fixture =>
        {
            var sweep = Assert.IsType<SMUSweepNode>(fixture.Node);
            sweep.Source = SourceType.Voltage_V;
            sweep.EndVal = 12;
            var request = fixture.Start("inline");
            Assert.Equal("Scan", request.EventName);
            sweep.Source = SourceType.Current_I;
            sweep.EndVal = 31;

            await fixture.CompleteAsync(request, new JObject
            {
                ["VList"] = new JArray(2, 3), ["IList"] = new JArray(.01, .02), ["ScanList"] = new JArray(2, 3),
            });
            ViewResultSMU result = await fixture.ReadAsync();
            Assert.Equal(-1, result.Id);
            Assert.True(result.IsSourceV);
            Assert.Equal(12f, result.LimitEnd);
            Assert.Equal(new double[] { 2, 3 }, result.VList);
        });

    private static JObject Point(int masterId) => new() { ["V"] = 2, ["I"] = 10, ["MasterId"] = masterId, ["MasterResultType"] = 200 };

    private static Task RunGraphAsync(Func<CVBaseServerNode> buildNode, Version serviceVersion, Func<Fixture, Task> run) => WpfTestHost.Invoke(async () =>
    {
        var transport = DispatchProxy.Create<IMqttClient, SpectrumNdSelectionTests.CalibrationMqttClient>();
        var control = new MQTTControl(transport);
        using var fixture = new Fixture(buildNode(), control, serviceVersion);
        Assert.True(await control.Connect().WaitAsync(Deadline));
        try { await run(fixture); }
        finally { await control.DisconnectAsyncClient().WaitAsync(Deadline); }
    });

    private sealed class Fixture : IDisposable
    {
        private readonly CVNodeContainer graph = new();
        private readonly FlowEngineControl engine;
        private readonly RuntimeTestStartNode start = new();
        private readonly Channel<ViewResultSMU> presented = Channel.CreateUnbounded<ViewResultSMU>();
        private MQActionEvent? sent;

        internal CVBaseServerNode Node { get; }
        internal DeviceSMU Owner { get; }

        internal Fixture(CVBaseServerNode node, MQTTControl control, Version serviceVersion)
        {
            Node = node;
            var end = new RuntimeTestEndNode();
            foreach (STNode item in new STNode[] { start, node, end })
            {
                item.Create();
                graph.Nodes.Add(item);
            }

            string deviceCode = $"SMU.FLOW.TEST.{Guid.NewGuid():N}";
            node.DeviceCode = deviceCode;
            if (node is SMUSweepModelNode template) template.ModelName = "ServerTemplate";
            Owner = (DeviceSMU)RuntimeHelpers.GetUninitializedObject(typeof(DeviceSMU));
            Owner.SysResourceModel = new SysResourceModel { Id = 1, Code = deviceCode };
            Owner.Config = new ConfigSMU { Code = deviceCode, SendTopic = node.GetSendTopic(), SubscribeTopic = node.GetRecvTopic() };
            SetField("<DisplayConfig>k__BackingField", new DisplaySMUConfig());
            SetField("<SmuBackend>k__BackingField", new SmuBackendState(false));
            Owner.DService = new MQTTSMU(Owner, control, () => [], () => serviceVersion, (_, create) => presented.Writer.TryWrite(create()));
            node.RemoteResultBinderOverride = RemoteDeviceFlowResult.CreateBinder(code => code == deviceCode ? Owner : null);
            Assert.Equal(ConnectionStatus.Connected, start.m_op_start.ConnectOption(node.GetAllInputOptions()[0], false));
            Assert.Equal(ConnectionStatus.Connected, node.GetAllOutputOptions()[0].ConnectOption(end.m_in_start, false));
            engine = new FlowEngineControl(graph, false, new FlowNodeManager()) { PersistResults = false };
            start.OnPublish = action => sent = action;
        }

        internal CVMQTTRequest Start(string serial, MySqlConnectedEndpoint? batchEndpoint = null)
        {
            sent = null;
            var action = engine.TryStartRun(start.NodeName, serial, trackCompletion: false,
                initializeRuntimeResources: batchEndpoint == null ? null : new FlowMeasurementBatch(serial, 1, "offline", 0, batchEndpoint).Bind);
            Assert.NotNull(action);
            var published = Assert.IsType<MQActionEvent>(sent);
            Assert.Equal(Owner.Config.SendTopic, published.Topic);
            Assert.Contains(Owner.Config.SubscribeTopic, start.SubscribedTopics);
            return JsonConvert.DeserializeObject<CVMQTTRequest>(published.Message)!;
        }

        internal bool Receive(CVMQTTRequest request, JObject data, int code = 0, string? receivedTopic = null) => Node.DoServerStatusRecv(new CVBaseDataFlowResp
        {
            Code = code, MsgID = request.MsgID, SerialNumber = request.SerialNumber, ServiceName = request.ServiceCode,
            DeviceNodeCode = Node.NodeID, DeviceCode = request.DeviceCode, EventName = request.EventName, Data = data,
            ZIndex = request.ZIndex, ReceivedTopic = receivedTopic ?? Owner.Config.SubscribeTopic,
        });

        internal async Task CompleteAsync(CVMQTTRequest request, JObject data)
        {
            var completed = new TaskCompletionSource<FlowEngineEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            FlowEngineEventHandler handler = (_, args) =>
            {
                if (args.SerialNumber == request.SerialNumber) completed.TrySetResult(args);
            };
            engine.Finished += handler;
            try
            {
                Assert.True(Receive(request, data));
                Assert.Equal(StatusTypeEnum.Completed, (await completed.Task.WaitAsync(Deadline)).Status);
            }
            finally { engine.Finished -= handler; }
        }

        internal Task<ViewResultSMU> ReadAsync() => presented.Reader.ReadAsync().AsTask().WaitAsync(Deadline);

        private void SetField(string name, object value) => typeof(DeviceSMU).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Owner, value);

        public void Dispose()
        {
            Owner.DService.Dispose();
            engine.Dispose();
            graph.Dispose();
        }
    }
}
