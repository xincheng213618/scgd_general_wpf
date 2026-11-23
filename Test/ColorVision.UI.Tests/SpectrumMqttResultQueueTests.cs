using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.Messages;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Local;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Views;
using cvColorVision;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.MQTT;
using FlowEngineLib.Node.Spectrum;
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
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class SpectrumMqttResultQueueTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    [Fact]
    public void SmuReadingsExpireWithoutReplacingThePersistedManualInputs()
    {
        var settings = new DisplaySpectrumConfig { V = 8.25, I = .125 };
        bool sourceCurrent = true;
        settings.ApplySmuReadings(4.5, .75, () => sourceCurrent);
        Assert.Equal((4.5, .75), settings.ReadElectricalInputs());
        var restored = JObject.FromObject(settings).ToObject<DisplaySpectrumConfig>();
        Assert.NotNull(restored);
        Assert.Equal((8.25, .125), restored.ReadElectricalInputs());
        var editor = PropertyEditSession.Create(settings, PropertyEditorEditMode.Immediate);
        settings.ManualVoltage = 4;
        settings.ManualCurrent = .25;
        editor.Reset();
        Assert.Equal((8.25, .125), settings.ReadElectricalInputs());
        Assert.Equal((8.25, .125), (settings.ManualVoltage, settings.ManualCurrent));
        settings.ApplySmuReadings(4.5, .75, () => sourceCurrent);
        sourceCurrent = false;
        Assert.Equal((8.25, .125), settings.ReadElectricalInputs());
        settings.ApplySmuReadings(6, 2, () => true);
        settings.V = 2.5;
        Assert.Equal((2.5, .125), settings.ReadElectricalInputs());
    }

    [Theory]
    [InlineData("direct")]
    [InlineData("data")]
    [InlineData("continuous")]
    public async Task EqeCommandUsesOneElectricalSnapshotAndTheDevicesDatabase(string entry)
    {
        IMqttClient transport = DispatchProxy.Create<IMqttClient, SpectrumNdSelectionTests.CalibrationMqttClient>();
        var client = (SpectrumNdSelectionTests.CalibrationMqttClient)(object)transport;
        var control = new MQTTControl(transport);
        using var stores = new EqeDatabases();
        using var spectrum = WpfTestHost.Invoke(() => new SpectrumScope(control, (_, _) => null, () => new Version(), _ => null, stores.Original));
        Assert.True(await control.Connect());
        try
        {
            stores.WriteSyntheticCalibration(spectrum.Device.Config);
            WpfTestHost.Invoke(() =>
            {
                spectrum.Device.DisplayConfig.IsLuminousFluxMode = true;
                spectrum.Device.DisplayConfig.IsWithND = true;
                spectrum.Device.DisplayConfig.Divisor = 1.25;
                spectrum.Device.ApplySmuReadings(stores.Original, 4.5, .75, () => true);
            });
            stores.OnOpenOriginal = () =>
            {
                // Mutate the display after the command has captured its pair but before the insert.
                spectrum.Device.DisplayConfig.V = 9;
                spectrum.Device.DisplayConfig.I = 2;
            };

            MsgRecord record = WpfTestHost.Invoke(() => entry switch
            {
                "data" => spectrum.Service.GetData(15000),
                "continuous" => spectrum.Service.GetDataAuto(),
                _ => spectrum.Service.GetEqe(),
            });
            JObject packet = JObject.Parse(await client.Commands.Reader.ReadAsync().AsTask().WaitAsync(Deadline));
            Assert.Equal(entry == "continuous" ? "EQE.GetDataAuto" : "EQE.GetData", packet.Value<string>("EventName"));
            var parameters = Assert.IsType<JObject>(packet["params"]);
            if (entry == "direct") Assert.Null(parameters["IsWithND"]);
            else Assert.True(parameters.Value<bool>("IsWithND"));
            Assert.Equal(1.25, parameters.Value<double>("AFactor"));
            Assert.Equal("EQEData.json", parameters.Value<string>("OutputDataFilename"));
            var smuData = Assert.IsType<JObject>(parameters["SMUData"]);
            var row = Assert.Single(stores.ReadOriginal());
            Assert.Equal(row.Id, smuData.Value<int>("MasterId"));
            Assert.Equal(200, smuData.Value<int>("MasterResultType"));
            Assert.Equal(0, smuData.Value<int>("Channel"));
            Assert.Equal(4.5f, row.VResult);
            Assert.Equal(.75f, row.IResult);
            Assert.Equal(4.5, smuData.Value<double>("V"));
            Assert.Equal(.75, smuData.Value<double>("I"));
            var untouched = Assert.Single(stores.ReadReplacement());
            Assert.Equal(row.Id, untouched.Id);
            Assert.Equal(99f, untouched.VResult);

            var reply = spectrum.Reply(record.MsgSend.EventName, -1, new JObject());
            reply.MsgID = record.MsgID;
            await client.ReplyAsync(spectrum.Service.SubscribeTopic, reply);
        }
        finally
        {
            await control.DisconnectAsyncClient();
        }
    }

    [Fact]
    public async Task EqeReconnectDuringInsertRollsBackAndReleasesTheContinuousLease()
    {
        IMqttClient transport = DispatchProxy.Create<IMqttClient, SpectrumNdSelectionTests.CalibrationMqttClient>();
        var client = (SpectrumNdSelectionTests.CalibrationMqttClient)(object)transport;
        var control = new MQTTControl(transport);
        using var stores = new EqeDatabases();
        using var spectrum = WpfTestHost.Invoke(() => new SpectrumScope(control, (_, _) => null, () => new Version(), _ => null, stores.Original));
        Assert.True(await control.Connect());
        try
        {
            stores.OnOriginalInsert = () => stores.Active = stores.Replacement;
            var rejected = new TaskCompletionSource<MsgRecordState>(TaskCreationOptions.RunContinuationsAsynchronously);
            WpfTestHost.Invoke(() =>
            {
                spectrum.Device.DisplayConfig.IsLuminousFluxMode = true;
                var record = spectrum.Service.GetDataAuto();
                record.MsgRecordStateChanged += (_, state) => rejected.TrySetResult(state);
            });
            Assert.Equal(MsgRecordState.Fail, await rejected.Task.WaitAsync(Deadline));
            Assert.Empty(stores.ReadOriginal());
            Assert.Single(stores.ReadReplacement());
            Assert.False(client.Commands.Reader.TryRead(out _));

            var retry = WpfTestHost.Invoke(() =>
            {
                spectrum.Device.ResultDatabaseEndpoint = stores.Replacement;
                return spectrum.Service.GetEqe();
            });
            Assert.Equal("EQE.GetData", JObject.Parse(await client.Commands.Reader.ReadAsync().AsTask().WaitAsync(Deadline)).Value<string>("EventName"));
            Assert.Equal(2, stores.ReadReplacement().Count);
            var reply = spectrum.Reply(retry.MsgSend.EventName, -1, new JObject());
            reply.MsgID = retry.MsgID;
            await client.ReplyAsync(spectrum.Service.SubscribeTopic, reply);
        }
        finally
        {
            await control.DisconnectAsyncClient();
        }
    }

    [Fact]
    public async Task BlockedLookupDoesNotHoldSerialMqttPumpAndResultsStayInOrder()
    {
        IMqttClient transport = DispatchProxy.Create<IMqttClient, SpectrumNdSelectionTests.CalibrationMqttClient>();
        var client = (SpectrumNdSelectionTests.CalibrationMqttClient)(object)transport;
        var control = new MQTTControl(transport);
        var lookupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var legacyPresented = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLookup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lookupIds = new ConcurrentQueue<int>();
        var plots = new List<(float Chromaticity, bool? Eqe)>();
        var integrationTimes = new List<double>();
        Version version = new(4, 0, 1, 105);

        using var stores = new EqeDatabases();
        using var spectrum = WpfTestHost.Invoke(() => new SpectrumScope(control, (_, id) =>
        {
            lookupIds.Enqueue(id);
            if (id == 31)
            {
                lookupStarted.TrySetResult();
                releaseLookup.Task.WaitAsync(Deadline).GetAwaiter().GetResult();
            }
            return id switch
            {
                31 => new SpectumResultEntity { Id = 31, IntTime = 12, fx = .31f },
                72 => new SpectumResultEntity { Id = 72, DataType = true, fx = .72f },
                _ => null,
            };
        }, () => version, _ => result =>
        {
            plots.Add((result.fx, result.AcquisitionFluxMode));
            if (result.fx == .24f) legacyPresented.TrySetResult();
        }, stores.Original));
        stores.WriteSyntheticCalibration(spectrum.Device.Config);
        WpfTestHost.Invoke(() => spectrum.Device.DisplayConfig.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(DisplaySpectrumConfig.IntTime))
                integrationTimes.Add(spectrum.Device.DisplayConfig.IntTime);
        });
        using var other = new MQTTServiceBase(control) { SendTopic = "test/other/cmd", SubscribeTopic = "test/other/status" };
        Assert.True(await control.Connect());
        try
        {
            MsgRecord eqe = await SendSingleAsync(spectrum, client, eqe: true);
            MsgRecord unrelated = WpfTestHost.Invoke(() => other.PublishAsyncClient(new MsgSend { EventName = "GetData" }));
            await client.Commands.Reader.ReadAsync().AsTask().WaitAsync(Deadline);
            // MQTTnet awaits a publish callback before it dispatches the next publish packet.
            Task pump = Task.Run(async () =>
            {
                await client.ReplyAsync(spectrum.Service.SubscribeTopic, spectrum.Reply("GetDataAuto", 102, new JObject { ["MasterId"] = 31 }));
                await ReplySingleAsync(spectrum, client, eqe, new JValue("72"));
                Assert.Equal(MsgRecordState.Success, await ScheduledDeviceJobHelper.GetTerminalCompletion(eqe).WaitAsync(Deadline));
                MsgRecord data = await SendSingleAsync(spectrum, client);
                await ReplySingleAsync(spectrum, client, data, new JValue(99));
                version = new Version(4, 0, 1, 104);
                await ReplyContinuousInlineAsync(spectrum, client, .23f, code: 102);
                await ReplyContinuousInlineAsync(spectrum, client, .24f, code: 0);
                await client.ReplyAsync(other.SubscribeTopic, new MsgReturn { MsgID = unrelated.MsgID, EventName = "GetData", Code = 0 });
            });

            await lookupStarted.Task.WaitAsync(Deadline);
            await pump.WaitAsync(Deadline);
            Assert.Equal(MsgRecordState.Success, unrelated.MsgRecordState);
            Assert.Equal(new[] { 31 }, lookupIds);
            WpfTestHost.Invoke(() => Assert.Empty(plots));

            releaseLookup.TrySetResult();
            await legacyPresented.Task.WaitAsync(Deadline);
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal(new[] { 31, 72, 99 }, lookupIds);
                Assert.Equal(new (float Chromaticity, bool? Eqe)[] { (.31f, false), (.72f, true), (.23f, null), (.24f, null) }, plots);
                Assert.Equal(new[] { 12d, 0d }, integrationTimes);
                Assert.Equal(0d, spectrum.Device.DisplayConfig.IntTime);
            });
        }
        finally
        {
            releaseLookup.TrySetResult();
            await control.DisconnectAsyncClient();
        }
    }

    [Fact]
    public async Task RetiredSpectrumDatabaseCannotPublishResultsAfterASwitch()
    {
        IMqttClient transport = DispatchProxy.Create<IMqttClient, SpectrumNdSelectionTests.CalibrationMqttClient>();
        var client = (SpectrumNdSelectionTests.CalibrationMqttClient)(object)transport;
        var control = new MQTTControl(transport);
        var lookupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLookup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var legacyPresented = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var currentPresented = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queries = new ConcurrentQueue<(MySqlConnectedEndpoint Endpoint, int Id)>();
        var plots = new List<float>();
        using var stores = new EqeDatabases();
        MySqlConnectedEndpoint first = stores.Original;
        MySqlConnectedEndpoint second = stores.Replacement;

        using var spectrum = WpfTestHost.Invoke(() => new SpectrumScope(control, (endpoint, id) =>
        {
            queries.Enqueue((endpoint, id));
            var data = EqeDatabases.ReadSpectrum(endpoint, id);
            if (ReferenceEquals(endpoint, first))
            {
                lookupStarted.TrySetResult();
                releaseLookup.Task.WaitAsync(Deadline).GetAwaiter().GetResult();
            }
            return data;
        }, () => new Version(4, 0, 1, 104), _ => result =>
        {
            float value = result.fx;
            plots.Add(value);
            if (value == .23f) legacyPresented.TrySetResult();
            if (value == .24f) currentPresented.TrySetResult();
        }, first));
        int masterId = stores.SeedSpectrumResults(spectrum.Device.Code);
        stores.WriteSyntheticCalibration(spectrum.Device.Config);

        Assert.True(await control.Connect().WaitAsync(Deadline));
        try
        {
            MsgRecord reading = await SendSingleAsync(spectrum, client);
            await ReplySingleAsync(spectrum, client, reading, new JValue(masterId));
            Assert.Equal(MsgRecordState.Success, await ScheduledDeviceJobHelper.GetTerminalCompletion(reading).WaitAsync(Deadline));
            await lookupStarted.Task.WaitAsync(Deadline);
            MsgRecord late = await SendSingleAsync(spectrum, client);
            WpfTestHost.Invoke(() => stores.SwitchToReplacement(spectrum.Device));
            await ReplySingleAsync(spectrum, client, late, new JValue(masterId));
            Assert.Equal(MsgRecordState.Success, await ScheduledDeviceJobHelper.GetTerminalCompletion(late).WaitAsync(Deadline));

            MsgRecord failed = await SendSingleAsync(spectrum, client);
            await ReplySingleAsync(spectrum, client, failed, new JValue(masterId), code: -1);
            Assert.Equal(MsgRecordState.Fail, await ScheduledDeviceJobHelper.GetTerminalCompletion(failed).WaitAsync(Deadline));

            MsgRecord current = await SendSingleAsync(spectrum, client);
            await ReplySingleAsync(spectrum, client, current, new JValue(masterId), code: 102);
            await ReplySingleAsync(spectrum, client, current, new JValue(masterId), deviceCode: "other-spectrum");
            await ReplySingleAsync(spectrum, client, current, new JValue(masterId), messageId: "unmatched-spectrum-result");
            await ReplySingleAsync(spectrum, client, current, new JValue("invalid-result-id"));
            await ReplyContinuousInlineAsync(spectrum, client, .9f, code: 0, messageId: current.MsgID);
            Assert.Equal(MsgRecordState.Sended, current.MsgRecordState);
            await ReplyContinuousInlineAsync(spectrum, client, .23f);

            releaseLookup.TrySetResult();
            await legacyPresented.Task.WaitAsync(Deadline);
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal(new[] { .23f }, plots);
                Assert.Equal(100d, spectrum.Device.DisplayConfig.IntTime);
            });
            Assert.Equal((first, masterId), Assert.Single(queries));

            await ReplySingleAsync(spectrum, client, current, new JValue(masterId));
            Assert.Equal(MsgRecordState.Success, await ScheduledDeviceJobHelper.GetTerminalCompletion(current).WaitAsync(Deadline));
            await ReplySingleAsync(spectrum, client, current, new JValue(masterId));
            await ReplyContinuousInlineAsync(spectrum, client, .24f);
            await currentPresented.Task.WaitAsync(Deadline);
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal(new[] { .23f, .72f, .24f }, plots);
                Assert.Equal(22d, spectrum.Device.DisplayConfig.IntTime);
            });
            Assert.Equal(new[] { (first, masterId), (second, masterId) }, queries.ToArray());
        }
        finally
        {
            releaseLookup.TrySetResult();
            await control.DisconnectAsyncClient().WaitAsync(Deadline);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueuedPresentationCannotChangeLocalOrRetiredSpectrum(bool switchToLocal)
    {
        IMqttClient transport = DispatchProxy.Create<IMqttClient, SpectrumNdSelectionTests.CalibrationMqttClient>();
        var client = (SpectrumNdSelectionTests.CalibrationMqttClient)(object)transport;
        var control = new MQTTControl(transport);
        int lookupThread = 0;
        int plots = 0;
        using var posted = new ManualResetEventSlim();
        using var stores = new EqeDatabases();
        using var spectrum = WpfTestHost.Invoke(() => new SpectrumScope(control, (_, id) =>
        {
            Volatile.Write(ref lookupThread, Environment.CurrentManagedThreadId);
            return new SpectumResultEntity { Id = id, IntTime = 14 };
        }, () => new Version(), _ => result => plots++, stores.Original));
        stores.WriteSyntheticCalibration(spectrum.Device.Config);

        Assert.True(await control.Connect().WaitAsync(Deadline));
        try
        {
            MsgRecord capture = await SendSingleAsync(spectrum, client);
            await WpfTestHost.Invoke(async () =>
            {
                Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
                void OnOperationPosted(object? _, DispatcherHookEventArgs args)
                {
                    if (Environment.CurrentManagedThreadId == Volatile.Read(ref lookupThread) && args.Operation.Priority == DispatcherPriority.Normal)
                        posted.Set();
                }

                dispatcher.Hooks.OperationPosted += OnOperationPosted;
                try
                {
                    Task receive = Task.Run(() => ReplySingleAsync(spectrum, client, capture, new JValue(31)));
                    // Hold the UI until the worker has posted the result, then change ownership before it can apply.
                    Assert.True(posted.Wait(Deadline));
                    if (switchToLocal)
                        spectrum.Device.SpectrumBackend.SetPreference(true);
                    else
                        spectrum.Service.Dispose();
                    await Dispatcher.Yield(DispatcherPriority.Background);
                    await receive.WaitAsync(Deadline);
                    Assert.Equal(0, plots);
                    Assert.Equal(100d, spectrum.Device.DisplayConfig.IntTime);
                }
                finally
                {
                    dispatcher.Hooks.OperationPosted -= OnOperationPosted;
                }
            });
        }
        finally { await control.DisconnectAsyncClient().WaitAsync(Deadline); }
    }

    [Fact]
    public async Task PersistedSamplesLoadOffDispatcherAndStayWithTheAdmittedView()
    {
        IMqttClient transport = DispatchProxy.Create<IMqttClient, SpectrumNdSelectionTests.CalibrationMqttClient>();
        var client = (SpectrumNdSelectionTests.CalibrationMqttClient)(object)transport;
        var control = new MQTTControl(transport);
        var hiddenResultApplied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var decodeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDecode = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var currentResultPresented = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var decodedIds = new ConcurrentQueue<int>();
        using var stores = new EqeDatabases();
        string samples = stores.WriteSamples(JsonConvert.SerializeObject("[1,2,3]"));
        using var spectrum = WpfTestHost.Invoke(() => new SpectrumScope(control, (_, id) => new SpectumResultEntity
        {
            Id = id, IntTime = 100 + id, fx = .3f, fy = .4f,
            fSpect1 = 500, fSpect2 = 502, fInterval = 1,
            fPL = "[9,9,9]", fPL_file_name = samples,
        }, () => new Version(4, 0, 1, 105), device => device.CaptureLiveResultPresentation(), stores.Original, row =>
        {
            decodedIds.Enqueue(row.Id);
            if (row.Id == 2)
            {
                decodeStarted.TrySetResult();
                releaseDecode.Task.GetAwaiter().GetResult();
            }
            return new ViewResultSpectrum(row);
        }));
        using var registration = WpfTestHost.Invoke(() =>
        {
            var view = new DockViewRegistration(() => new ViewSpectrum(spectrum.Device, deferInitialization: true), spectrum.Device.Code, isVisible: false);
            typeof(DeviceSpectrum).GetField("_view", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(spectrum.Device, view);
            spectrum.Device.DisplayConfig.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(DisplaySpectrumConfig.IntTime) && spectrum.Device.DisplayConfig.IntTime == 101)
                    hiddenResultApplied.TrySetResult();
            };
            return view;
        });

        Assert.True(await control.Connect().WaitAsync(Deadline));
        try
        {
            await ReplyPersistedAsync(1);
            await hiddenResultApplied.Task.WaitAsync(Deadline);
            Assert.Empty(decodedIds);
            Assert.Null(registration.Current);

            ViewSpectrum oldView = WpfTestHost.Invoke(() =>
            {
                registration.IsVisible = true;
                return (ViewSpectrum)registration.GetOrCreate();
            });
            await ReplyPersistedAsync(2);
            await decodeStarted.Task.WaitAsync(Deadline);

            // This dispatcher operation must finish while the persisted sample loader is still blocked.
            ViewSpectrum currentView = await oldView.Dispatcher.InvokeAsync(() =>
            {
                registration.IsVisible = false;
                registration.IsVisible = true;
                var view = (ViewSpectrum)registration.GetOrCreate();
                var dictionaries = ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase)
                    .Select(uri => new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) }).ToArray();
                foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Add(dictionary);
                try { view.EnsureInitialized(); }
                finally { foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary); }
                view.ViewResults.CollectionChanged += (_, _) => currentResultPresented.TrySetResult();
                return view;
            }).Task.WaitAsync(Deadline);
            Assert.NotSame(oldView, currentView);

            releaseDecode.TrySetResult();
            await ReplyPersistedAsync(3);
            await currentResultPresented.Task.WaitAsync(Deadline);
            WpfTestHost.Invoke(() =>
            {
                Assert.Empty(oldView.ViewResults);
                ViewResultSpectrum result = Assert.Single(currentView.ViewResults);
                Assert.Equal(3, result.Id);
                Assert.Equal([1f, 2f, 3f], result.CreateRawExportRecord(false).Samples);
                Assert.True(result.DominantWavelengthColor.IsFrozen);
                Assert.Equal(103d, spectrum.Device.DisplayConfig.IntTime);
            });
            Assert.Equal([2, 3], decodedIds);
        }
        finally
        {
            releaseDecode.TrySetResult();
            await control.DisconnectAsyncClient().WaitAsync(Deadline);
        }

        Task ReplyPersistedAsync(int id) => client.ReplyAsync(spectrum.Service.SubscribeTopic,
            spectrum.Reply("GetDataAuto", 102, new JObject { ["MasterId"] = id })).WaitAsync(Deadline);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacySpectrumFlowPresentsOnlyItsMatchedResultFromTheAdmittedDatabase(bool switchDatabase)
    {
        IMqttClient transport = DispatchProxy.Create<IMqttClient, SpectrumNdSelectionTests.CalibrationMqttClient>();
        var client = (SpectrumNdSelectionTests.CalibrationMqttClient)(object)transport;
        var control = new MQTTControl(transport);
        using var stores = new EqeDatabases();
        var queries = new ConcurrentQueue<(MySqlConnectedEndpoint Endpoint, int Id)>();
        var plots = new List<float>();
        var marker = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var spectrum = WpfTestHost.Invoke(() => new SpectrumScope(control, (endpoint, id) =>
        {
            queries.Enqueue((endpoint, id));
            return EqeDatabases.ReadSpectrum(endpoint, id);
        }, () => new Version(4, 0, 1, 104), _ => result =>
        {
            float value = result.fx;
            plots.Add(value);
            if (value == .23f) marker.TrySetResult();
        }, stores.Original));
        int masterId = stores.SeedSpectrumResults(spectrum.Device.Code);

        Assert.True(await control.Connect().WaitAsync(Deadline));
        try
        {
            await WpfTestHost.Invoke(async () =>
            {
                using var graph = new CVNodeContainer();
                var node = new SpectrumNode();
                var start = new RuntimeTestStartNode();
                var end = new RuntimeTestEndNode();
                foreach (STNode item in new STNode[] { start, node, end })
                {
                    item.Create();
                    graph.Nodes.Add(item);
                }
                node.DeviceCode = spectrum.Device.Code;
                node.RemoteResultBinderOverride = RemoteDeviceFlowResult.CreateBinder(code => code == spectrum.Device.Code ? spectrum.Device : null);
                STNodeOption input = Assert.Single(node.GetAllInputOptions(), option => option.DataType == typeof(CVStartCFC));
                STNodeOption output = Assert.Single(node.GetAllOutputOptions(), option => option.DataType == typeof(CVStartCFC));
                Assert.Equal(ConnectionStatus.Connected, start.m_op_start.ConnectOption(input, false));
                Assert.Equal(ConnectionStatus.Connected, output.ConnectOption(end.m_in_start, false));
                using var engine = new FlowEngineControl(graph, false, new FlowNodeManager());
                spectrum.Device.Config.SendTopic = node.GetSendTopic();
                spectrum.Device.Config.SubscribeTopic = node.GetRecvTopic();
                var completed = new TaskCompletionSource<FlowEngineEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
                engine.Finished += (_, args) => completed.TrySetResult(args);
                MQActionEvent? sent = null;
                start.OnPublish = action => sent = action;

                Assert.True(engine.TryStartNode(start.NodeName, "spectrum-flow-test"));
                CVMQTTRequest request = JsonConvert.DeserializeObject<CVMQTTRequest>(Assert.IsType<MQActionEvent>(sent).Message)!;
                Assert.Equal("GetData", request.EventName);
                string topic = Assert.Single(start.SubscribedTopics, candidate => candidate == node.GetRecvTopic());
                Assert.False(node.DoServerStatusRecv(Response(102)));
                Assert.False(node.DoServerStatusRecv(Response(deviceCode: "other-spectrum")));
                Assert.False(node.DoServerStatusRecv(Response(receivedTopic: "other-spectrum/status")));
                Assert.False(node.DoServerStatusRecv(Response(messageId: "unmatched-spectrum-flow")));
                if (switchDatabase) stores.SwitchToReplacement(spectrum.Device);

                Assert.True(node.DoServerStatusRecv(Response()));
                Assert.Equal(StatusTypeEnum.Completed, (await completed.Task.WaitAsync(Deadline)).Status);
                Assert.False(node.DoServerStatusRecv(Response()));
                await ReplyContinuousInlineAsync(spectrum, client, .23f);
                await marker.Task.WaitAsync(Deadline);

                if (switchDatabase)
                {
                    Assert.Empty(queries);
                    Assert.Equal(new[] { .23f }, plots);
                    Assert.Equal(100d, spectrum.Device.DisplayConfig.IntTime);
                }
                else
                {
                    Assert.Equal((stores.Original, masterId), Assert.Single(queries));
                    Assert.Equal(new[] { .31f, .23f }, plots);
                    Assert.Equal(12d, spectrum.Device.DisplayConfig.IntTime);
                }

                CVBaseDataFlowResp Response(int code = 0, string? deviceCode = null, string? receivedTopic = null, string? messageId = null) => new()
                {
                    Code = code, MsgID = messageId ?? request.MsgID, SerialNumber = request.SerialNumber, ZIndex = request.ZIndex,
                    ServiceName = request.ServiceCode, DeviceNodeCode = node.NodeID, DeviceCode = deviceCode ?? request.DeviceCode,
                    EventName = request.EventName, ReceivedTopic = receivedTopic ?? topic,
                    Data = new JObject { ["MasterId"] = masterId },
                };
            });
        }
        finally { await control.DisconnectAsyncClient().WaitAsync(Deadline); }
    }

    private static async Task<MsgRecord> SendSingleAsync(SpectrumScope spectrum,
        SpectrumNdSelectionTests.CalibrationMqttClient client, bool eqe = false)
    {
        MsgRecord record = WpfTestHost.Invoke(() => eqe ? spectrum.Service.GetEqe() : spectrum.Service.GetData());
        var packet = JObject.Parse(await client.Commands.Reader.ReadAsync().AsTask().WaitAsync(Deadline));
        Assert.Equal(eqe ? "EQE.GetData" : "GetData", packet.Value<string>("EventName"));
        Assert.Equal(record.MsgID, packet.Value<string>("MsgID"));
        return record;
    }

    private static Task ReplySingleAsync(SpectrumScope spectrum, SpectrumNdSelectionTests.CalibrationMqttClient client,
        MsgRecord record, JToken masterId, int code = 0, string? deviceCode = null, string? messageId = null)
    {
        MsgReturn reply = spectrum.Reply(record.MsgSend.EventName, code, new JObject { ["MasterId"] = masterId });
        reply.MsgID = messageId ?? record.MsgID;
        reply.DeviceCode = deviceCode ?? spectrum.Device.Code;
        return client.ReplyAsync(record.SubscribeTopic, reply).WaitAsync(Deadline);
    }

    private static Task ReplyContinuousInlineAsync(SpectrumScope spectrum, SpectrumNdSelectionTests.CalibrationMqttClient client,
        float chromaticity, int code = 102, string? messageId = null)
    {
        var data = new SpectrumData(91, new COLOR_PARA { fx = chromaticity, fPL = [], fRi = [] });
        MsgReturn reply = spectrum.Reply("GetDataAuto", code, JObject.FromObject(data));
        reply.MsgID = messageId ?? reply.MsgID;
        return client.ReplyAsync(spectrum.Service.SubscribeTopic, reply).WaitAsync(Deadline);
    }

    private static MySqlConnectedEndpoint CreateEndpoint(Func<MySqlConnectedEndpoint, bool> isCurrent) =>
        new(isCurrent, () => throw new InvalidOperationException("The spectrum reader must use the offline test double."));

    private sealed class SpectrumScope : IDisposable
    {
        public DeviceSpectrum Device { get; }
        public MQTTSpectrum Service { get; }

        public SpectrumScope(MQTTControl control, Func<MySqlConnectedEndpoint, int, SpectumResultEntity?> lookup, Func<Version> version,
            Func<DeviceSpectrum, Action<ViewResultSpectrum>?> capturePresentation, MySqlConnectedEndpoint? endpoint = null,
            Func<SpectumResultEntity, ViewResultSpectrum>? materialize = null)
        {
            string code = $"SPECTRUM.TEST.{Guid.NewGuid():N}";
            Device = (DeviceSpectrum)RuntimeHelpers.GetUninitializedObject(typeof(DeviceSpectrum));
            Device.SysResourceModel = new SysResourceModel { Code = code };
            Device.Config = new ConfigSpectrum { Code = code, SendTopic = "test/spectrum/cmd", SubscribeTopic = "test/spectrum/status" };
            Device.ResultDatabaseEndpoint = endpoint ?? CreateEndpoint(_ => true);
            typeof(DeviceSpectrum).GetField("<SpectrumBackend>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(Device, new SpectrumBackendState(false));
            typeof(DeviceSpectrum).GetField("correctionMeasurementGate", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(Device, new SemaphoreSlim(1, 1));
            Service = Device.DService = new MQTTSpectrum(Device, control, lookup, version, capturePresentation, materialize);
        }

        public MsgReturn Reply(string eventName, int code, JObject data) => new()
        {
            MsgID = Guid.NewGuid().ToString("N"), DeviceCode = Service.DeviceCode, EventName = eventName, Code = code, Data = data,
        };

        public void Dispose()
        {
            Service.Dispose();
            DisplayConfigManager.Instance.Configs.TryRemove(Device.Code, out _);
        }
    }

    private sealed class EqeDatabases : IDisposable
    {
        private readonly DirectoryInfo _files = Directory.CreateTempSubdirectory("cv-eqe-ownership-");
        private MySqlConnectedEndpoint? _active;
        internal MySqlConnectedEndpoint Original { get; }
        internal MySqlConnectedEndpoint Replacement { get; }
        internal MySqlConnectedEndpoint? Active { get => Volatile.Read(ref _active); set => Volatile.Write(ref _active, value); }
        internal Action? OnOpenOriginal { get; set; }
        internal Action? OnOriginalInsert { get; set; }

        internal EqeDatabases()
        {
            Original = new(candidate => ReferenceEquals(candidate, Active), () =>
            {
                OnOpenOriginal?.Invoke();
                var db = Open("original.db");
                db.Aop.OnLogExecuted = (sql, _) =>
                {
                    if (sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase)) OnOriginalInsert?.Invoke();
                };
                return db;
            });
            Replacement = new(candidate => ReferenceEquals(candidate, Active), () => Open("replacement.db"));
            Active = Original;
            using var original = Open("original.db");
            using var replacement = Open("replacement.db");
            original.CodeFirst.InitTables<SMUResultModel>();
            replacement.CodeFirst.InitTables<SMUResultModel>();
            replacement.Insertable(new SMUResultModel { VResult = 99, CreateDate = DateTime.Now }).ExecuteCommand();
        }

        internal List<SMUResultModel> ReadOriginal() => Read("original.db");
        internal List<SMUResultModel> ReadReplacement() => Read("replacement.db");

        internal void SwitchToReplacement(DeviceSpectrum device)
        {
            Active = Replacement;
            device.ResultDatabaseEndpoint = Replacement;
        }

        internal int SeedSpectrumResults(string deviceCode)
        {
            int id = Insert(Original, 12, .31f);
            Assert.Equal(id, Insert(Replacement, 22, .72f));
            return id;

            int Insert(MySqlConnectedEndpoint endpoint, float integration, float chromaticity)
            {
                using var db = endpoint.CreateDbClient();
                db.CodeFirst.InitTables<SpectumResultEntity>();
                return db.Insertable(new SpectumResultEntity
                {
                    DeviceCode = deviceCode, IntTime = integration, fx = chromaticity,
                }).ExecuteReturnIdentity();
            }
        }

        internal static SpectumResultEntity? ReadSpectrum(MySqlConnectedEndpoint endpoint, int id)
        {
            using var db = endpoint.CreateDbClient();
            return MQTTSpectrum.ReadServiceResult(db, id);
        }

        internal void WriteSyntheticCalibration(ConfigSpectrum config)
        {
            config.WavelengthFile = Path.Combine(_files.FullName, "wavelength.dat");
            config.MaguideFile = Path.Combine(_files.FullName, "magnitude.dat");
            using (var wavelength = new BinaryWriter(File.Create(config.WavelengthFile)))
            {
                wavelength.Write(16UL);
                wavelength.Write(500d);
            }
            using var magnitude = new BinaryWriter(File.Create(config.MaguideFile));
            magnitude.Write(40UL);
            magnitude.Write(10f);
            magnitude.Write(1);
            magnitude.Write(1UL);
            magnitude.Write(500d);
            magnitude.Write(1d);
        }

        internal string WriteSamples(string json)
        {
            string path = Path.Combine(_files.FullName, "samples.json");
            File.WriteAllText(path, json);
            return path;
        }

        private List<SMUResultModel> Read(string name)
        {
            using var db = Open(name);
            return db.Queryable<SMUResultModel>().ToList();
        }

        private SqlSugarClient Open(string name) => new(new ConnectionConfig
        {
            DbType = DbType.Sqlite, ConnectionString = $"Data Source={Path.Combine(_files.FullName, name)};Pooling=False",
            IsAutoCloseConnection = true, InitKeyType = InitKeyType.Attribute,
        });

        public void Dispose()
        {
            foreach (FileInfo file in _files.EnumerateFiles()) file.Delete();
            _files.Delete();
        }
    }
}
