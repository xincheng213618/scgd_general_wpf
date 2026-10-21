using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.CfwPort;
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

namespace ColorVision.UI.Tests;

public sealed class SpectrumNdSelectionTests
{
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
                    Assert.Equal(MsgRecordState.Success, set.MsgRecordState);
                    Assert.Equal("ND2", device.Config.ActiveCalibrationGroupName);
                    Assert.Equal("nd2-magnitude.dat", device.Config.MaguideFile);
                    MsgRecord read = device.RunLocalSpectrumCommand("GetPort");
                    Pump();
                    Assert.Equal(MsgRecordState.Success, read.MsgRecordState);
                    Assert.Equal(2, JObject.FromObject((object)read.MsgReturn.Data).Value<int>("Port"));
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
