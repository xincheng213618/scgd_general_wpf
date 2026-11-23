using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Messages;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.SMU;
using ColorVision.Engine.Services.Devices.SMU.Configs;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.SMU.Local;
using ColorVision.Engine.Services.Devices.SMU.Views;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using MQTTnet;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class SmuMqttResultOwnershipTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task CurrentPointAndScanReadTheOwnedRowsAndOnlyLiveSameDatabaseSpectrumReceivesThePair()
    {
        await using var fixture = WpfTestHost.Invoke(() => new Fixture());
        await fixture.ConnectAsync();
        MsgRecord wrongChannel = await fixture.SendPointAsync(SMUChannelType.A);
        await fixture.ReplyResultAsync(wrongChannel, fixture.OtherChannelId);
        await fixture.ExpectCompletionAsync(wrongChannel);

        MsgRecord pointRequest = await fixture.SendPointAsync(SMUChannelType.A);
        await fixture.ReplyResultAsync(pointRequest, fixture.PointId);
        await fixture.ExpectCompletionAsync(pointRequest);
        Assert.Equal(fixture.PointId, (await fixture.NextResultAsync()).Id);
        MsgRecord scanRequest = await fixture.SendScanAsync(ForwardScan(), SMUChannelType.A);
        await fixture.ReplyResultAsync(scanRequest, fixture.ScanId);
        await fixture.ExpectCompletionAsync(scanRequest);
        Assert.Equal(fixture.ScanId, (await fixture.NextResultAsync()).Id);

        WpfTestHost.Invoke(() =>
        {
            Assert.Collection(fixture.Presented,
                point =>
                {
                    Assert.Equal(fixture.PointId, point.Id);
                    Assert.Equal("GetData", point.Type);
                    Assert.Equal(2.5, point.VResult);
                    Assert.Equal(17, point.IResult);
                },
                scan =>
                {
                    Assert.Equal(fixture.ScanId, scan.Id);
                    Assert.Equal("Scan", scan.Type);
                    Assert.Equal(new double[] { 2.5, 3.5 }, scan.VList);
                });
            Assert.Equal(2.5, fixture.Owner.DisplayConfig.ChannelA.V);
            Assert.Equal(17, fixture.Owner.DisplayConfig.ChannelA.I);
            Assert.Equal((2.5, 17d), VoltageAndCurrent(fixture.CurrentSpectrum));
            Assert.Equal((11d, 22d), VoltageAndCurrent(fixture.OtherSpectrum));
            Assert.Equal((11d, 22d), VoltageAndCurrent(fixture.DisposedSpectrum));
        });

        MsgRecord other = await fixture.SendPointAsync(SMUChannelType.B);
        await fixture.ReplyResultAsync(other, fixture.OtherChannelId);
        await fixture.ExpectCompletionAsync(other);
        Assert.Equal(fixture.OtherChannelId, (await fixture.NextResultAsync()).Id);
        WpfTestHost.Invoke(() =>
        {
            Assert.Equal(9, fixture.Owner.DisplayConfig.ChannelB.V);
            Assert.Equal(10, fixture.Owner.DisplayConfig.ChannelB.I);
            Assert.Equal((2.5, 17d), VoltageAndCurrent(fixture.CurrentSpectrum));
        });

        WpfTestHost.Invoke(() =>
        {
            fixture.Owner.DisplayConfig.Channel = SMUChannelType.B;
            Assert.Equal((11d, 22d), VoltageAndCurrent(fixture.CurrentSpectrum));
            fixture.Owner.DisplayConfig.Channel = SMUChannelType.A;
            Assert.Equal((11d, 22d), VoltageAndCurrent(fixture.CurrentSpectrum));
            fixture.Owner.DisplayConfig.Channel = SMUChannelType.B;
        });
        MsgRecord selected = await fixture.SendPointAsync(SMUChannelType.B);
        await fixture.ReplyResultAsync(selected, fixture.OtherChannelId);
        await fixture.ExpectCompletionAsync(selected);
        Assert.Equal(fixture.OtherChannelId, (await fixture.NextResultAsync()).Id);
        WpfTestHost.Invoke(() => Assert.Equal((9d, 10d), VoltageAndCurrent(fixture.CurrentSpectrum)));
    }

    [Fact]
    public async Task IncompletePointInvalidatesOnlyItsSourceAndMeasuredZeroIsAvailable()
    {
        await using var fixture = WpfTestHost.Invoke(() => new Fixture());
        int incomplete = fixture.AddPoint(voltage: null, current: 4);
        int zero = fixture.AddPoint(voltage: 0, current: 0);
        await fixture.ConnectAsync();
        await ReadPointAsync(fixture.PointId);
        WpfTestHost.Invoke(() => Assert.Equal((2.5, 17d), VoltageAndCurrent(fixture.CurrentSpectrum)));

        await ReadPointAsync(incomplete);
        WpfTestHost.Invoke(() =>
        {
            Assert.Null(fixture.Owner.DisplayConfig.ChannelA.V);
            Assert.Equal(4, fixture.Owner.DisplayConfig.ChannelA.I);
            Assert.Equal((11d, 22d), VoltageAndCurrent(fixture.CurrentSpectrum));
        });

        await ReadPointAsync(zero);
        WpfTestHost.Invoke(() => Assert.Equal((0d, 0d), VoltageAndCurrent(fixture.CurrentSpectrum)));

        var otherSource = (DeviceSMU)RuntimeHelpers.GetUninitializedObject(typeof(DeviceSMU));
        WpfTestHost.Invoke(() => fixture.CurrentSpectrum.ApplySmuReadings(fixture.Owner.ResultDatabaseEndpoint, 6, 7,
            static () => true, sourceDevice: otherSource, sourceChannel: SMUChannelType.A));
        await ReadPointAsync(incomplete);
        WpfTestHost.Invoke(() =>
        {
            fixture.Owner.DisplayConfig.Channel = SMUChannelType.B;
            Assert.Equal((6d, 7d), VoltageAndCurrent(fixture.CurrentSpectrum));
        });
        GC.KeepAlive(otherSource);

        async Task ReadPointAsync(int id)
        {
            MsgRecord record = await fixture.SendPointAsync(SMUChannelType.A);
            await fixture.ReplyResultAsync(record, id);
            await fixture.ExpectCompletionAsync(record);
            Assert.Equal(id, (await fixture.NextResultAsync()).Id);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateOldRequestCannotReadTheSameIdFromAReboundOrRecreatedDevice(bool recreate)
    {
        await using var fixture = WpfTestHost.Invoke(() => new Fixture());
        await fixture.ConnectAsync();
        MsgRecord old = await fixture.SendPointAsync(SMUChannelType.A);
        WpfTestHost.Invoke(() => fixture.SwitchDatabase(recreate));
        MsgRecord current = await fixture.SendPointAsync(SMUChannelType.A);

        await fixture.ReplyResultAsync(old, fixture.PointId);
        await fixture.ExpectCompletionAsync(old, recreate ? MsgRecordState.Fail : MsgRecordState.Success);
        await fixture.ReplyResultAsync(current, fixture.PointId, code: 102);
        await fixture.ReplyResultAsync(current, fixture.PointId, deviceCode: "different-source-meter");
        await fixture.ReplyResultAsync(current, fixture.PointId, messageId: "unmatched-source-meter-reply");
        Assert.Equal(MsgRecordState.Sended, current.MsgRecordState);
        WpfTestHost.Invoke(() =>
        {
            Assert.Empty(fixture.Presented);
            Assert.Null(fixture.Owner.DisplayConfig.V);
            Assert.Equal((11d, 22d), VoltageAndCurrent(fixture.OtherSpectrum));
        });

        await fixture.ReplyResultAsync(current, fixture.PointId);
        await fixture.ExpectCompletionAsync(current);
        await fixture.ReplyResultAsync(current, fixture.PointId);
        MsgRecord marker = await fixture.SendPointAsync(SMUChannelType.B);
        await fixture.ReplyResultAsync(marker, fixture.OtherChannelId);
        await fixture.ExpectCompletionAsync(marker);
        Assert.Equal(fixture.PointId, (await fixture.NextResultAsync()).Id);
        Assert.Equal(fixture.OtherChannelId, (await fixture.NextResultAsync()).Id);

        Assert.Equal(new[] { "replacement", "replacement" }, fixture.Lookups.ToArray());
        WpfTestHost.Invoke(() =>
        {
            Assert.Equal(new[] { 7.5, 9 }, fixture.Presented.Select(row => row.VResult));
            Assert.Equal((7.5, 69d), VoltageAndCurrent(fixture.OtherSpectrum));
            Assert.Equal((11d, 22d), VoltageAndCurrent(fixture.CurrentSpectrum));
        });
    }

    [Theory]
    [InlineData("change database")]
    [InlineData("dispose service")]
    [InlineData("switch to local")]
    public async Task QueuedPointCannotUpdateReadoutOrSpectrumAfterLosingOwnership(string transition)
    {
        await using var fixture = WpfTestHost.Invoke(() => new Fixture());
        await fixture.ConnectAsync();
        MsgRecord record = await fixture.SendPointAsync(SMUChannelType.A);
        await WpfTestHost.Invoke(async () =>
        {
            using var posted = new ManualResetEventSlim();
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            void OnOperationPosted(object? _, DispatcherHookEventArgs args)
            {
                if (Environment.CurrentManagedThreadId == Volatile.Read(ref fixture.LookupThread)
                    && args.Operation.Priority == DispatcherPriority.Normal)
                    posted.Set();
            }

            dispatcher.Hooks.OperationPosted += OnOperationPosted;
            try
            {
                Task reply = Task.Run(() => fixture.ReplyResultAsync(record, fixture.PointId));
                Assert.True(posted.Wait(Deadline));
                fixture.ExpectCompletionAsync(record).GetAwaiter().GetResult();
                switch (transition)
                {
                    case "change database":
                        fixture.SwitchDatabase(recreate: false);
                        break;
                    case "dispose service":
                        fixture.Service.Dispose();
                        break;
                    case "switch to local":
                        fixture.Owner.SmuBackend.SetPreference(true);
                        break;
                }
                await Dispatcher.Yield(DispatcherPriority.Background);
                await reply.WaitAsync(Deadline);

                Assert.Empty(fixture.Presented);
                Assert.Null(fixture.Owner.DisplayConfig.ChannelA.V);
                Assert.Null(fixture.Owner.DisplayConfig.ChannelA.I);
                Assert.Equal((11d, 22d), VoltageAndCurrent(fixture.CurrentSpectrum));
            }
            finally { dispatcher.Hooks.OperationPosted -= OnOperationPosted; }
        });
    }

    [Fact]
    public async Task LegacyInlineScanWorksWithoutDatabaseAndCompletesThePendingCommand()
    {
        await using var fixture = WpfTestHost.Invoke(() => new Fixture());
        WpfTestHost.Invoke(() =>
        {
            fixture.Owner.ResultDatabaseEndpoint = null;
            fixture.ServiceVersion = new Version(4, 0, 2, 114);
        });
        await fixture.ConnectAsync();
        MsgRecord reversed = await fixture.SendScanAsync(new SMUParam
        {
            IsSourceV = false, StartMeasureVal = 30, StopMeasureVal = -10, LmtVal = 3, Number = 2,
        }, SMUChannelType.B);
        MsgRecord later = await fixture.SendScanAsync(ForwardScan(), SMUChannelType.A);
        await fixture.ReplyAsync(reversed, InlineScan([3.0, 1.0], [0.03, -0.01], [30.0, -10.0]));
        await fixture.ExpectCompletionAsync(reversed);
        await fixture.ReplyAsync(later, InlineScan([1.0, 2.0], [0.01, 0.02], [1.0, 2.0]));
        await fixture.ExpectCompletionAsync(later);
        var scan = await fixture.NextResultAsync();
        var forward = await fixture.NextResultAsync();
        Assert.Equal(-1, scan.Id);
        Assert.Equal(SMUChannelType.B, scan.ChannelType);
        Assert.Equal(MeasurementType.Current, scan.MeasurementType);
        Assert.Equal(-10, scan.LimitEnd);
        Assert.Equal(new[] { 3d, 1d }, scan.VList);
        Assert.Equal(new[] { .03, -.01 }, scan.IList);
        Assert.Equal(new[] { 30d, -10d }, scan.SMUDatas.Select(point => point.Current));
        Assert.Equal(SMUChannelType.A, forward.ChannelType);
        Assert.Equal(MeasurementType.Voltage, forward.MeasurementType);
        Assert.Equal(2, forward.LimitEnd);
        Assert.Empty(fixture.Lookups);
        WpfTestHost.Invoke(() =>
        {
            Assert.Null(fixture.Owner.DisplayConfig.V);
            Assert.Equal((11d, 22d), VoltageAndCurrent(fixture.CurrentSpectrum));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BlockedResultReadDoesNotHoldTheMqttPumpOrUndoAClearedReadout(bool clearOutput)
    {
        await using var fixture = WpfTestHost.Invoke(() => new Fixture());
        await fixture.ConnectAsync();
        using var other = new MQTTServiceBase(fixture.Control) { SendTopic = "test/unrelated/cmd", SubscribeTopic = "test/unrelated/status" };
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int lookups = 0;
        fixture.BeforeLookup = _ =>
        {
            if (Interlocked.Increment(ref lookups) != 1) return;
            started.TrySetResult();
            release.Task.WaitAsync(Deadline).GetAwaiter().GetResult();
        };

        MsgRecord point = await fixture.SendPointAsync(SMUChannelType.A);
        MsgRecord scan = await fixture.SendScanAsync(ForwardScan(), SMUChannelType.A);
        MsgRecord unrelated = WpfTestHost.Invoke(() => other.PublishAsyncClient(new MsgSend { EventName = "GetData" }));
        await fixture.ExpectWireAsync(unrelated);
        Task pump = Task.Run(async () =>
        {
            await fixture.ReplyResultAsync(point, fixture.PointId);
            await fixture.ReplyResultAsync(scan, fixture.ScanId);
            await fixture.ReplyAsync(unrelated, new JObject());
        });

        try
        {
            await started.Task.WaitAsync(Deadline);
            await pump.WaitAsync(Deadline);
            Assert.Equal(MsgRecordState.Success, unrelated.MsgRecordState);
            await fixture.ExpectCompletionAsync(point);
            await fixture.ExpectCompletionAsync(scan);
            Assert.Equal("original", Assert.Single(fixture.Lookups));
            WpfTestHost.Invoke(() =>
            {
                Assert.Empty(fixture.Presented);
                if (!clearOutput) return;
                fixture.Owner.SmuBackend.MarkReadoutReset(SMUChannelType.A);
                fixture.Owner.DisplayConfig.ChannelA.V = null;
                fixture.Owner.DisplayConfig.ChannelA.I = null;
            });
        }
        finally { release.TrySetResult(); }

        Assert.Equal(fixture.PointId, (await fixture.NextResultAsync()).Id);
        Assert.Equal(fixture.ScanId, (await fixture.NextResultAsync()).Id);
        Assert.Equal(new[] { "original", "original" }, fixture.Lookups.ToArray());
        WpfTestHost.Invoke(() =>
        {
            var readings = fixture.Owner.DisplayConfig;
            Assert.Equal(clearOutput ? (double?)null : 2.5, readings.ChannelA.V);
            Assert.Equal(clearOutput ? (double?)null : 17, readings.ChannelA.I);
            Assert.Equal(clearOutput ? (11d, 22d) : (2.5, 17d), VoltageAndCurrent(fixture.CurrentSpectrum));
        });
    }

    private static SMUParam ForwardScan() => new() { IsSourceV = true, StartMeasureVal = 1, StopMeasureVal = 2, LmtVal = 25, Number = 2 };

    private static JObject InlineScan(double[] voltage, double[] current, double[] source) => new()
    {
        ["VList"] = new JArray(voltage), ["IList"] = new JArray(current), ["ScanList"] = new JArray(source),
    };

    private static (double Voltage, double Current) VoltageAndCurrent(DeviceSpectrum device) =>
        (device.DisplayConfig.V, device.DisplayConfig.I);

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("cv-smu-mqtt-result-");
        private readonly SpectrumNdSelectionTests.CalibrationMqttClient _client;
        private readonly Channel<ViewResultSMU> _presentations = Channel.CreateUnbounded<ViewResultSMU>();
        private readonly string _deviceCode = $"SMU.TEST.{Guid.NewGuid():N}";
        private readonly MySqlConnectedEndpoint _original;
        private readonly MySqlConnectedEndpoint _replacement;
        private MySqlConnectedEndpoint? _active;

        internal MQTTControl Control { get; }
        internal DeviceSMU Owner { get; private set; }
        internal MQTTSMU Service => Owner.DService;
        internal DeviceSpectrum CurrentSpectrum { get; }
        internal DeviceSpectrum OtherSpectrum { get; }
        internal DeviceSpectrum DisposedSpectrum { get; }
        internal List<ViewResultSMU> Presented { get; } = [];
        internal ConcurrentQueue<string> Lookups { get; } = new();
        internal Action<string>? BeforeLookup { get; set; }
        internal Version ServiceVersion { get; set; } = new(4, 0, 2, 115);
        internal int LookupThread;
        internal int PointId { get; }
        internal int OtherChannelId { get; }
        internal int ScanId { get; }

        internal Fixture()
        {
            var transport = DispatchProxy.Create<IMqttClient, SpectrumNdSelectionTests.CalibrationMqttClient>();
            _client = (SpectrumNdSelectionTests.CalibrationMqttClient)(object)transport;
            Control = new MQTTControl(transport);
            _original = CreateEndpoint("original");
            _replacement = CreateEndpoint("replacement");
            _active = _original;
            (PointId, OtherChannelId, ScanId) = Seed("original", 2.5f, 17);
            Assert.Equal((PointId, OtherChannelId, ScanId), Seed("replacement", 7.5f, 69));

            CurrentSpectrum = NewSpectrum(_original);
            OtherSpectrum = NewSpectrum(_replacement);
            DisposedSpectrum = NewSpectrum(_original);
            SetField(DisposedSpectrum, "localDisposed", true);
            Owner = NewOwner(_original);
        }

        private DeviceSMU NewOwner(MySqlConnectedEndpoint endpoint)
        {
            var owner = (DeviceSMU)RuntimeHelpers.GetUninitializedObject(typeof(DeviceSMU));
            owner.SysResourceModel = new SysResourceModel { Code = _deviceCode };
            owner.Config = new ConfigSMU { Code = _deviceCode, SendTopic = "test/smu/cmd", SubscribeTopic = "test/smu/status" };
            owner.ResultDatabaseEndpoint = endpoint;
            SetField(owner, "<DisplayConfig>k__BackingField", new DisplaySMUConfig { UseLocalSmu = false, IsUseLimitSigned = false });
            SetField(owner, "<SmuBackend>k__BackingField", new SmuBackendState(false));
            owner.DService = new MQTTSMU(owner, Control, () => [CurrentSpectrum, OtherSpectrum, DisposedSpectrum],
                () => ServiceVersion, (_, create) =>
                {
                    ViewResultSMU result = create();
                    Presented.Add(result);
                    _presentations.Writer.TryWrite(result);
                });
            return owner;
        }

        internal async Task ConnectAsync() => Assert.True(await Control.Connect().WaitAsync(Deadline));

        internal async Task<MsgRecord> SendPointAsync(SMUChannelType channel)
        {
            MsgRecord record = WpfTestHost.Invoke(() => Assert.IsType<MsgRecord>(Service.GetData(true, 2.5, 25, channel)));
            JObject request = await ExpectWireAsync(record);
            Assert.Equal((int)channel, request["params"]!.Value<int>("Channel"));
            return record;
        }

        internal async Task<MsgRecord> SendScanAsync(SMUParam parameters, SMUChannelType channel)
        {
            MsgRecord record = WpfTestHost.Invoke(() => Assert.IsType<MsgRecord>(Service.Scan(parameters, channel)));
            JObject request = await ExpectWireAsync(record);
            Assert.Equal((int)channel, request["params"]!["DeviceParam"]!.Value<int>("Channel"));
            return record;
        }

        internal async Task<JObject> ExpectWireAsync(MsgRecord record)
        {
            JObject request = JObject.Parse(await _client.Commands.Reader.ReadAsync().AsTask().WaitAsync(Deadline));
            Assert.Equal(record.MsgID, request.Value<string>("MsgID"));
            Assert.Equal(record.MsgSend.EventName, request.Value<string>("EventName"));
            return request;
        }

        internal async Task ExpectCompletionAsync(MsgRecord record, MsgRecordState expected = MsgRecordState.Success)
        {
            Assert.NotNull(record.WorkerCompletion);
            Assert.Equal(expected, await record.WorkerCompletion!.WaitAsync(Deadline).ConfigureAwait(false));
        }

        internal Task<ViewResultSMU> NextResultAsync() => _presentations.Reader.ReadAsync().AsTask().WaitAsync(Deadline);

        internal Task ReplyResultAsync(MsgRecord record, int resultId, int code = 0, string? deviceCode = null, string? messageId = null) =>
            ReplyAsync(record, new JObject { ["MasterId"] = resultId }, code, deviceCode, messageId);

        internal Task ReplyAsync(MsgRecord record, JObject data, int code = 0, string? deviceCode = null, string? messageId = null) =>
            _client.ReplyAsync(record.SubscribeTopic, new MsgReturn
            {
                EventName = record.MsgSend.EventName, Data = data, MsgID = messageId ?? record.MsgID,
                DeviceCode = deviceCode ?? record.MsgSend.DeviceCode, Code = code,
            }).WaitAsync(Deadline);

        internal void SwitchDatabase(bool recreate)
        {
            Volatile.Write(ref _active, _replacement);
            if (recreate)
            {
                Service.Dispose();
                Owner = NewOwner(_replacement);
            }
            else Owner.ResultDatabaseEndpoint = _replacement;
        }

        private MySqlConnectedEndpoint CreateEndpoint(string name) => new(endpoint => ReferenceEquals(Volatile.Read(ref _active), endpoint), () =>
        {
            Volatile.Write(ref LookupThread, Environment.CurrentManagedThreadId);
            Lookups.Enqueue(name);
            BeforeLookup?.Invoke(name);
            return Open(name);
        });

        private SqlSugarClient Open(string name) => new(new ConnectionConfig
        {
            ConnectionString = $"Data Source={Path.Combine(_directory.FullName, name + ".db")};Pooling=False",
            DbType = DbType.Sqlite, InitKeyType = InitKeyType.Attribute, IsAutoCloseConnection = true
        });

        internal int AddPoint(float? voltage, float? current)
        {
            using var db = Open("original");
            return db.Insertable(new SMUResultModel
            {
                DeviceCode = _deviceCode, IsSourceV = true, VResult = voltage, IResult = current, CreateDate = DateTime.Now,
            }).ExecuteReturnIdentity();
        }

        private (int Point, int OtherChannel, int Scan) Seed(string name, float voltage, float current)
        {
            using var db = Open(name);
            db.CodeFirst.InitTables(typeof(SMUResultModel), typeof(SmuScanModel));
            int point = db.Insertable(new SMUResultModel
            {
                DeviceCode = _deviceCode, IsSourceV = true, VResult = voltage, IResult = current, CreateDate = DateTime.Now,
            }).ExecuteReturnIdentity();
            int other = db.Insertable(new SMUResultModel
            {
                DeviceCode = _deviceCode, ChannelType = SMUChannelType.B, IsSourceV = true, VResult = 9, IResult = 10, CreateDate = DateTime.Now,
            }).ExecuteReturnIdentity();
            int scan = db.Insertable(new SmuScanModel
            {
                DeviceCode = _deviceCode, IsSourceV = true, SrcBegin = 1, SrcEnd = 2, LimitValue = 25,
                VResult = FormattableString.Invariant($"[{voltage},{voltage + 1}]"), IResult = "[0.1,0.2]", CreateDate = DateTime.Now,
            }).ExecuteReturnIdentity();
            return (point, other, scan);
        }

        private static DeviceSpectrum NewSpectrum(MySqlConnectedEndpoint endpoint)
        {
            var device = (DeviceSpectrum)RuntimeHelpers.GetUninitializedObject(typeof(DeviceSpectrum));
            string code = $"SMU.RESULT.SPECTRUM.TEST.{Guid.NewGuid():N}";
            device.SysResourceModel = new SysResourceModel { Code = code };
            device.Config = new ConfigSpectrum { Code = code };
            device.ResultDatabaseEndpoint = endpoint;
            device.DisplayConfig.V = 11;
            device.DisplayConfig.I = 22;
            return device;
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

        public async ValueTask DisposeAsync()
        {
            Service.Dispose();
            await Control.DisconnectAsyncClient().WaitAsync(Deadline);
            foreach (var device in new[] { CurrentSpectrum, OtherSpectrum, DisposedSpectrum })
                DisplayConfigManager.Instance.Configs.TryRemove(device.Code, out _);
            foreach (FileInfo file in _directory.EnumerateFiles()) file.Delete();
            _directory.Delete();
        }
    }
}
