using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.CfwPort;
using ColorVision.Engine.Services.Devices.CfwPort.Local;
using FlowEngineLib;
using FlowEngineLib.Base;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Views;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ColorVision.Engine.Services.Devices.Spectrum.Local;
using ColorVision.Engine.PropertyEditor;

namespace ColorVision.UI.Tests;

public sealed class SpectrumNdSelectionTests
{
    // Exercise the real registration, routing, shared wheel session and file commit without a spectrum SDK or MQTT.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SpectrumNdNodeForwardsToLocalWheelAndCommitsCalibrationOnlyAfterConfirmedMove(bool localSpectrum)
    {
        var managerField = typeof(ServiceManager).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)!;
        object? previousManager = null;
        TestScope? scope = null;
        DeviceSpectrum? spectrum = null;
        DeviceCfwPort? wheel = null;
        var native = new NdNative();
        string directory = Path.Combine(Path.GetTempPath(), "spectrum-nd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string wave = Path.Combine(directory, "wave.dat"), first = Path.Combine(directory, "nd0.dat"), second = Path.Combine(directory, "nd1.dat");
        using (var writer = new BinaryWriter(File.Create(wave))) { writer.Write(24UL); writer.Write(380d); writer.Write(780d); }
        foreach (string path in new[] { first, second })
            using (var writer = new BinaryWriter(File.Create(path)))
            { writer.Write(56UL); writer.Write(4f); writer.Write(683); writer.Write(2UL); writer.Write(380d); writer.Write(780d); writer.Write(1d); writer.Write(1d); }
        try
        {
            WpfTestHost.Invoke(() =>
            {
                scope = new TestScope();
                previousManager = managerField.GetValue(null);
                var manager = (ServiceManager)RuntimeHelpers.GetUninitializedObject(typeof(ServiceManager));
                manager.DeviceServices = [];
                managerField.SetValue(null, manager);
                wheel = new DeviceCfwPort(new SysResourceModel { Id = -2, Code = "ND.Local." + Guid.NewGuid(),
                    Value = JsonConvert.SerializeObject(new ConfigCfwPort { SzComName = "COM7800" }) }, new LocalCfwPortSession(native));
                spectrum = CreateSpectrum();
                spectrum.SpectrumBackend.SetPreference(localSpectrum);
                spectrum.Config.NDConfig.IsNDPort = true;
                spectrum.Config.NDConfig.IsBingNDDevice = true;
                spectrum.Config.NDConfig.NDBindDeviceCode = wheel.Code;
                spectrum.Config.CalibrationGroups = [new() { GroupName = "ND0", NDHoleIndex = 0, WavelengthFile = wave, MaguideFile = first },
                    new() { GroupName = "ND1", NDHoleIndex = 1, WavelengthFile = wave, MaguideFile = second }];
                spectrum.Config.ActiveCalibrationGroupName = "ND0";
                spectrum.Config.WavelengthFile = wave;
                spectrum.Config.MaguideFile = first;
                manager.DeviceServices.Add(wheel);
                manager.DeviceServices.Add(spectrum);
                var node = new FWNode();
                node.Create();
                node.ModelType = FWModelType.Spectrum_FilterWheel;
                node.DeviceCode = spectrum.Code;
                Assert.Equal(!localSpectrum, node.RequiresRemoteService);
                wheel.DisplayConfig.UseLocalCfwPort = true;
                Assert.False(node.RequiresRemoteService);
            });

            var action = new CVStartCFC("spectrum-nd-forward");
            using var resources = action.RuntimeResources;
            FlowLocalExecution? execution = null;
            WpfTestHost.Invoke(() =>
            {
                var node = new FWNode(); node.Create(); node.ModelType = FWModelType.Spectrum_FilterWheel; node.DeviceCode = spectrum!.Code;
                execution = FlowLocalExecution.CreateForNode(node, new CVMQTTRequest { DeviceCode = spectrum.Code, EventName = "SetPort", Data = new FWData(1) });
                Assert.IsType<LocalSpectrumNdFlowExecution>(execution);
            });
            using (execution)
            {
                execution!.Bind(action);
                await Task.Run(execution.Execute);
                WpfTestHost.Invoke(() =>
                {
                    Assert.Equal(1, ((JObject)execution.Complete(action)).Value<int>("Port"));
                    Assert.Equal("ND1", spectrum!.Config.ActiveCalibrationGroupName);
                    Assert.Equal(second, spectrum.Config.MaguideFile);
                    Assert.Equal(wave, spectrum.Config.WavelengthFile);
                    Assert.Equal(1, spectrum.DisplayConfig.PortNum);
                });
            }
            Assert.Equal(1, native.Opens);
            Assert.Equal(1, native.Port);
            // No spectrum connection was needed; SetPort/GetPort still use the actual ND hardware backend.
            MsgRecord read = await NdCommand(() => spectrum!.DService.GetPort());
            Assert.Equal(MsgRecordState.Success, read.MsgRecordState);
            Assert.Equal(1, JObject.FromObject((object)read.MsgReturn.Data).Value<int>("Port"));
            native.WrongRead = true;
            WpfTestHost.Invoke(() => spectrum!.DisplayConfig.PortNum = 0);
            MsgRecord failed = await NdCommand(() => spectrum!.DService.SetPort());
            Assert.Equal(MsgRecordState.Fail, failed.MsgRecordState);
            WpfTestHost.Invoke(() => Assert.Equal("ND1", spectrum!.Config.ActiveCalibrationGroupName));
            Assert.Equal(second, spectrum!.Config.MaguideFile);
        }
        finally
        {
            if (wheel != null) await wheel.LocalSession.ExecuteAsync("Close", LocalCfwPortConnection.From(wheel.Config));
            WpfTestHost.Invoke(() =>
            {
                spectrum?.Dispose(); wheel?.Dispose();
                managerField.SetValue(null, previousManager);
                scope?.Dispose();
            });
            foreach (string file in Directory.EnumerateFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    private static async Task<MsgRecord> NdCommand(Func<MsgRecord> send)
    {
        var completion = new TaskCompletionSource<MsgRecord>(TaskCreationOptions.RunContinuationsAsynchronously);
        WpfTestHost.Invoke(() =>
        {
            MsgRecord record = send();
            void Complete(object? sender, MsgRecordState state)
            {
                if (state is not (MsgRecordState.Success or MsgRecordState.Fail or MsgRecordState.Timeout)) return;
                record.MsgRecordStateChanged -= Complete;
                completion.TrySetResult(record);
            }
            record.MsgRecordStateChanged += Complete;
            Complete(record, record.MsgRecordState);
        });
        return await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private sealed class NdNative : ILocalCfwPortNative
    {
        internal int Opens, Port;
        internal bool WrongRead;
        public IntPtr Open(LocalCfwPortConnection config) { Opens++; return new IntPtr(1234); }
        public int Close(IntPtr handle) => 0;
        public int SetPort(IntPtr handle, int port) { Port = port; return 0; }
        public int GetPort(IntPtr handle, ref int port) { port = WrongRead ? Port + 1 : Port; return 0; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RestoringActiveNdGroupDoesNotSendCommandsAndUserSelectionUsesItsBackend(bool local)
    {
        WpfTestHost.Invoke(() =>
        {
            using var scope = new TestScope();
            using var device = CreateSpectrum();
            device.Config.NDConfig.IsBingNDDevice = false;
            device.SpectrumBackend.SetPreference(local);
            device.DService.Dispose();
            using var mqtt = new RecordingSpectrum(device);
            device.DService = mqtt;
            using var card = new DisplaySpectrum(device);
            var window = new Window { Content = card, Width = 350, Height = 600,
                Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false };
            try
            {
                window.Show();
                Pump();
                var selector = Assert.IsType<ComboBox>(card.FindName("ComboBoxCalibrationGroups"));
                Assert.Equal("ND1", selector.SelectedValue);
                Assert.Equal(0, mqtt.Commands);
                Assert.True(selector.IsEnabled);

                // A queued binding update after an internal apply must also remain passive.
                device.Config.ActiveCalibrationGroupName = "ND2";
                Pump();
                Assert.Equal("ND2", selector.SelectedValue);
                Assert.Equal(0, mqtt.Commands);
                selector.SelectedIndex = 0;
                Pump();
                Assert.Equal(local ? 0 : 1, mqtt.Commands);
                Assert.Equal(local, selector.IsEnabled);
                if (local)
                {
                    Assert.Equal("ND1", device.Config.ActiveCalibrationGroupName);
                    Assert.Equal("nd1-wave.dat", device.Config.WavelengthFile);
                    Assert.Equal("nd1-magnitude.dat", device.Config.MaguideFile);
                    Assert.Equal(1, device.DisplayConfig.PortNum);

                    device.DisplayConfig.PortNum = 2;
                    MsgRecord set = device.RunLocalSpectrumCommand("SetPort");
                    Pump();
                    Assert.Equal(MsgRecordState.Fail, set.MsgRecordState);
                    Assert.Contains("启用 ND", (string)set.MsgReturn.Message);
                    Assert.Equal("ND1", device.Config.ActiveCalibrationGroupName);
                    Assert.Equal("nd1-magnitude.dat", device.Config.MaguideFile);
                    MsgRecord read = device.RunLocalSpectrumCommand("GetPort");
                    Pump();
                    Assert.Equal(MsgRecordState.Fail, read.MsgRecordState);
                    Assert.Contains("启用 ND", (string)read.MsgReturn.Message);
                    Assert.Equal(0, mqtt.Commands);
                }

                // A missing external wheel is a passive no-op, including after a delayed binding update.
                selector.SetCurrentValue(ComboBox.SelectedValueProperty, device.Config.ActiveCalibrationGroupName);
                device.CalibrationGroupChangeGuard.CompleteUserSwitch();
                selector.IsEnabled = true;
                device.Config.NDConfig.IsBingNDDevice = true;
                device.Config.NDConfig.NDBindDeviceCode = "ND.NotLoaded";
                string active = device.Config.ActiveCalibrationGroupName;
                int port = device.DisplayConfig.PortNum;
                int commands = mqtt.Commands;
                selector.SelectedIndex = active == "ND1" ? 1 : 0;
                Pump();
                Assert.Equal(active, device.Config.ActiveCalibrationGroupName);
                Assert.Equal(active, selector.SelectedValue);
                Assert.Equal(port, device.DisplayConfig.PortNum);
                Assert.Equal(commands, mqtt.Commands);
            }
            finally
            {
                window.Content = null;
                window.Close();
                DockViewManager.GetInstance().RemoveView(device.ViewShell);
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FilterWheelSelectionsSurviveConfigurationRoundTripIndependentlyWithoutSendingCommands(bool zeroBased)
    {
        WpfTestHost.Invoke(() =>
        {
            using var scope = new TestScope();
            if (zeroBased) scope.AddZeroBasedPhysicalWheel();
            (string code, int port)[] positions = [("ND.Camera", zeroBased ? 0 : 3), ("ND.Spectrum", zeroBased ? 4 : 2)];
            string value = zeroBased ? "{\"SN\":\"PHY.CFW.TEST\"}" : "{}";
            foreach ((string code, int port) in positions)
            {
                using var device = new DeviceCfwPort(new SysResourceModel { Id = -2, Code = code, Value = value });
                using var mqtt = new RecordingFilterWheel(device.Config);
                mqtt.DeviceStatus = DeviceStatusType.Opened;
                device.DService.Dispose();
                device.DService = mqtt;
                using var card = new DisplayCfwPort(device);
                var window = ShowCard(card);
                try
                {
                    var selector = Assert.IsAssignableFrom<ComboBox>(card.FindName("CombPort"));
                    Pump();
                    Assert.True(selector.IsVisible);
                    Assert.Null(device.DisplayConfig.PortNum);
                    selector.SetCurrentValue(ComboBox.SelectedValueProperty, port);
                    Pump();
                    Assert.Equal(port, device.DisplayConfig.PortNum);
                    Assert.Equal(0, mqtt.Commands);
                }
                finally { window.Content = null; window.Close(); }
            }
            Assert.True(File.Exists(scope.ConfigPath), "ND selection must persist without relying on process-exit saving.");
            var restored = new ConfigHandler { ConfigFilePath = scope.ConfigPath, IsAutoSave = false };
            restored.LoadConfigs();
            ConfigService.SetInstance(restored);
            foreach ((string code, int port) in positions)
            {
                using var device = new DeviceCfwPort(new SysResourceModel { Id = -2, Code = code, Value = value });
                using var mqtt = new RecordingFilterWheel(device.Config);
                device.DService.Dispose();
                device.DService = mqtt;
                using var card = new DisplayCfwPort(device);
                var window = ShowCard(card);
                try
                {
                    Pump();
                    var selector = Assert.IsAssignableFrom<ComboBox>(card.FindName("CombPort"));
                    Assert.False(selector.IsVisible);
                    mqtt.DeviceStatus = DeviceStatusType.Opened;
                    Pump();
                    Assert.True(selector.IsVisible);
                    Assert.Equal(port, selector.SelectedValue);
                    Assert.Equal(port, Assert.IsType<HoleMap>(selector.SelectedItem).HoleIndex);
                    Assert.Equal(port, device.DisplayConfig.PortNum);
                    Assert.Equal(0, mqtt.Commands);
                }
                finally { window.Content = null; window.Close(); }
            }
        });
    }

    [Fact]
    public void FilterWheelCommandRepliesRememberSuccessfulPositionsAndIgnoreFailures()
    {
        WpfTestHost.Invoke(() =>
        {
            using var scope = new TestScope();
            using var device = new DeviceCfwPort(new SysResourceModel { Id = -2, Code = "ND.Replies", Value = "{}" });
            device.DService.Dispose();
            using var mqtt = new RecordingFilterWheel(device.Config);
            device.DService = mqtt;
            var config = device.DisplayConfig;
            config.PortNum = 1;
            foreach (MsgRecordState state in new[] { MsgRecordState.Fail, MsgRecordState.Timeout, MsgRecordState.Success })
            {
                MsgRecord record = mqtt.SetPort(3);
                record.MsgRecordState = state;
                Assert.Equal(state == MsgRecordState.Success ? 3 : 1, config.PortNum);
            }
            MsgRecord read = mqtt.GetPort();
            read.MsgReturn = new MsgReturn { Data = JObject.FromObject(new { Port = 2 }) };
            read.MsgRecordState = MsgRecordState.Success;
            Assert.Equal(2, config.PortNum);
            Assert.Equal(2, JObject.Parse(File.ReadAllText(scope.ConfigPath))
                [typeof(DisplayConfigManager).FullName!]!["Object"]![device.Code]!.Value<int>("PortNum"));
            mqtt.Immediate = true;
            mqtt.SetPort(4);
            Assert.Equal(4, config.PortNum);
        });
    }

    [Fact]
    public void FilterWheelCalibrationReservationRejectsOtherCommandsAndReleasesAfterFailure()
    {
        WpfTestHost.Invoke(() =>
        {
            using var scope = new TestScope();
            using var mqtt = new RecordingFilterWheel(new ConfigCfwPort { Code = "ND.Reservation" });
            MsgRecord pending = mqtt.SetPort(1);
            Assert.Throws<InvalidOperationException>(() => mqtt.ReserveForCalibration());
            pending.MsgRecordState = MsgRecordState.Timeout;
            using var reserved = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            Task worker = Task.Run(() =>
            {
                using var reservation = mqtt.ReserveForCalibration();
                reserved.Set();
                release.Wait();
            });
            try
            {
                Assert.True(reserved.Wait(TimeSpan.FromSeconds(5)));
                Assert.Equal(MsgRecordState.Fail, mqtt.SetPort(4).MsgRecordState);
                Assert.Equal(MsgRecordState.Fail, mqtt.Clsoe().MsgRecordState);
                Assert.Equal(1, mqtt.Commands);
            }
            finally { release.Set(); worker.GetAwaiter().GetResult(); }
            mqtt.Immediate = true;
            Assert.Equal(MsgRecordState.Success, mqtt.SetPort(5).MsgRecordState);
            using var available = mqtt.ReserveForCalibration();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FilterWheelDarkControlChecksReadbackAndKeepsSharedWheelConnected(bool wrongReadback)
    {
        WpfTestHost.Invoke(() =>
        {
            using var scope = new TestScope();
            using var device = new DeviceCfwPort(new SysResourceModel { Id = -2, Code = "ND.DarkControl", Value = "{}" });
            device.DService.Dispose();
            using var mqtt = new PositionFilterWheel(device.Config) { WrongReadback = wrongReadback };
            device.DService = mqtt;
            mqtt.DeviceStatus = DeviceStatusType.Opened;
            var manager = ServiceManager.GetInstance();
            manager.DeviceServices.Add(device);
            try
            {
                using (var operation = new LocalSpectrumFilterWheel(new ShutterConfig { FilterWheelDeviceCode = device.Code }))
                {
                    if (wrongReadback) Assert.Throws<InvalidOperationException>(() => operation.SetOpen(false));
                    else
                    {
                        operation.SetOpen(false);
                        operation.SetOpen(true);
                        Assert.Equal(new[] { "SetPort:0", "GetPort", "SetPort:5", "GetPort" }, mqtt.Commands);
                        Assert.Equal(5, device.DisplayConfig.PortNum);
                    }
                }
                Assert.Equal(DeviceStatusType.Opened, mqtt.DeviceStatus);
                using var available = mqtt.ReserveForCalibration();
            }
            finally { manager.DeviceServices.Remove(device); }
        });
    }

    [Fact]
    public void ShutterPositionChoicesShowBoundWheelNamesAndPreserveIndicesWhenChoicesChange()
    {
        WpfTestHost.Invoke(() =>
        {
            using var scope = new TestScope();
            scope.AddZeroBasedPhysicalWheel();
            PhyCameraManager.GetInstance().PhyCameras[0].Config.FilterWheelConfig.HoleMapping[0].HoleName = "遮光";
            using var standard = new DeviceCfwPort(new SysResourceModel { Id = -2, Code = "ND.Names.Standard", Value = "{}" });
            using var custom = new DeviceCfwPort(new SysResourceModel { Id = -3, Code = "ND.Names.Custom", Value = "{\"SN\":\"PHY.CFW.TEST\"}" });
            var devices = ServiceManager.GetInstance().DeviceServices;
            devices.Add(standard);
            devices.Add(custom);
            var config = new ShutterConfig { UseFilterWheel = true, FilterWheelDeviceCode = standard.Code };
            var measurementPanel = PropertyEditorHelper.GenProperties(config, nameof(ShutterConfig.MeasurementPort), ColorVision.Engine.Properties.Resources.ResourceManager);
            var darkPanel = PropertyEditorHelper.GenProperties(config, nameof(ShutterConfig.DarkPort), ColorVision.Engine.Properties.Resources.ResourceManager);
            var measurement = Assert.Single(measurementPanel.Children.OfType<ComboBox>());
            var dark = Assert.Single(darkPanel.Children.OfType<ComboBox>());
            var content = new StackPanel();
            content.Children.Add(measurementPanel);
            content.Children.Add(darkPanel);
            var window = ShowCard(new UserControl { Content = content });
            try
            {
                Assert.False(measurement.IsEditable);
                Assert.Equal("Spectrum", Assert.IsType<HoleMap>(measurement.SelectedItem).HoleName);
                Assert.Equal("ND0", Assert.IsType<HoleMap>(dark.SelectedItem).HoleName);
                config.FilterWheelDeviceCode = custom.Code;
                Pump();
                Assert.Null(measurement.SelectedItem);
                Assert.Equal(5, config.MeasurementPort);
                Assert.Equal("遮光", Assert.IsType<HoleMap>(dark.SelectedItem).HoleName);
                measurement.SetCurrentValue(ComboBox.SelectedValueProperty, 3);
                Pump();
                Assert.Equal(3, config.MeasurementPort);
                Assert.Equal("ND3", Assert.IsType<HoleMap>(measurement.SelectedItem).HoleName);
                config.FilterWheelDeviceCode = string.Empty;
                Pump();
                Assert.Null(measurement.SelectedItem);
                Assert.Null(dark.SelectedItem);
                Assert.Equal(3, config.MeasurementPort);
                Assert.Equal(0, config.DarkPort);
                config.FilterWheelDeviceCode = standard.Code;
                Pump();
                Assert.Equal("ND1000", Assert.IsType<HoleMap>(measurement.SelectedItem).HoleName);
                Assert.Equal("ND0", Assert.IsType<HoleMap>(dark.SelectedItem).HoleName);
            }
            finally
            {
                window.Content = null;
                window.Close();
                devices.Remove(custom);
                devices.Remove(standard);
            }
        });
    }

    private sealed class PositionFilterWheel(ConfigCfwPort config) : MQTTCfwPort(config)
    {
        public List<string> Commands = [];
        public bool WrongReadback;
        private int position;
        internal override MsgRecord PublishAsyncClient(MsgSend msg, double timeout = 30000)
        {
            if (msg.EventName == "SetPort")
            {
                position = JObject.FromObject(msg.Params).Value<int>("PortNum");
                Commands.Add($"SetPort:{position}");
            }
            else Commands.Add(msg.EventName);
            return new MsgRecord { MsgSend = msg, MsgReturn = new MsgReturn { Data = JObject.FromObject(new { Port = WrongReadback ? position + 1 : position }) }, MsgRecordState = MsgRecordState.Success };
        }
    }

    private static DeviceSpectrum CreateSpectrum() => new(new SysResourceModel
    {
        Code = $"ND.Spectrum.{Guid.NewGuid():N}", Value = JsonConvert.SerializeObject(new ConfigSpectrum
        {
            ActiveCalibrationGroupName = "ND1", WavelengthFile = "nd1-wave.dat", MaguideFile = "nd1-magnitude.dat",
            CalibrationGroups = [new() { GroupName = "ND1", NDHoleIndex = 1, WavelengthFile = "nd1-wave.dat", MaguideFile = "nd1-magnitude.dat" },
                new() { GroupName = "ND2", NDHoleIndex = 2, WavelengthFile = "nd2-wave.dat", MaguideFile = "nd2-magnitude.dat" }]
        })
    });

    private static void Pump() => Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

    private static Window ShowCard(UserControl card)
    {
        var window = new Window { Content = card, Width = 400, Height = 250,
            Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        Pump();
        return window;
    }

    private sealed class RecordingSpectrum(DeviceSpectrum device) : MQTTSpectrum(device)
    {
        public int Commands;
        internal override MsgRecord PublishAsyncClient(MsgSend msg, double timeout = 30000)
        { Commands++; return new() { MsgSend = msg, MsgRecordState = MsgRecordState.Sended }; }
    }

    private sealed class RecordingFilterWheel(ConfigCfwPort config) : MQTTCfwPort(config)
    {
        public int Commands;
        public bool Immediate;
        internal override MsgRecord PublishAsyncClient(MsgSend msg, double timeout = 30000)
        { Commands++; return new() { MsgSend = msg, MsgRecordState = Immediate ? MsgRecordState.Success : MsgRecordState.Sended }; }
    }

    private sealed class TestScope : IDisposable
    {
        private readonly IConfigService previousConfig = ConfigService.Instance;
        private readonly SysResourceDao previousDao = SysResourceDao.Instance;
        private readonly string directory = Path.Combine(Path.GetTempPath(), $"nd-selection-{Guid.NewGuid():N}");
        private readonly List<ResourceDictionary> dictionaries = [];
        private static readonly FieldInfo PhysicalManagerField = typeof(PhyCameraManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? previousPhysicalManager = PhysicalManagerField.GetValue(null);
        public string ConfigPath => Path.Combine(directory, "ColorVisionConfig.json");

        public TestScope()
        {
            Directory.CreateDirectory(directory);
            ConfigService.SetInstance(new ConfigHandler { ConfigFilePath = ConfigPath, IsAutoSave = false });
            SysResourceDao.Instance = new SysResourceDao(new LocalTemplateStore(Path.Combine(directory, "resources.db")), () => false);
            foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
            {
                var dictionary = new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                dictionaries.Add(dictionary);
                Application.Current.Resources.MergedDictionaries.Add(dictionary);
            }
        }

        public void AddZeroBasedPhysicalWheel()
        {
            var camera = (PhyCamera)RuntimeHelpers.GetUninitializedObject(typeof(PhyCamera));
            camera.SysResourceModel = new SysResourceModel { Code = "PHY.CFW.TEST" };
            camera.Config = new ConfigPhyCamera();
            camera.Config.FilterWheelConfig.HoleMapping = new(Enumerable.Range(0, 5)
                .Select(index => new HoleMap { HoleIndex = index, HoleName = $"ND{index}" }));
            var manager = (PhyCameraManager)RuntimeHelpers.GetUninitializedObject(typeof(PhyCameraManager));
            manager.PhyCameras = [camera];
            PhysicalManagerField.SetValue(null, manager);
        }

        public void Dispose()
        {
            SysResourceDao.Instance = previousDao;
            PhysicalManagerField.SetValue(null, previousPhysicalManager);
            ConfigService.SetInstance(previousConfig);
            foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
            foreach (string file in Directory.EnumerateFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }
}
