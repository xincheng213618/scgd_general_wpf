using System.ComponentModel;
using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Messages;
using ColorVision.Engine.MQTT;
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
using MQTTnet;
using MQTTnet.Packets;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
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
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task SpectrumNdNodeForwardsToLocalWheelAndCommitsCalibrationOnlyAfterConfirmedMove(bool localSpectrum, bool autoSwitchEnabled)
    {
        var managerField = typeof(ServiceManager).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)!;
        object? previousManager = null;
        TestScope? scope = null;
        DeviceSpectrum? spectrum = null;
        DeviceCfwPort? wheel = null;
        var native = new NdTransport();
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
                spectrum.Config.NDConfig.IsNDPort = autoSwitchEnabled;
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

            if (!autoSwitchEnabled)
            {
                DeviceSpectrum owner = spectrum!;
                native.WrongRead = false;
                WpfTestHost.Invoke(() => owner.DisplayConfig.PortNum = 2);
                MsgRecord unassociated = await NdCommand(() => owner.DService.SetPort());
                Assert.Equal(MsgRecordState.Success, unassociated.MsgRecordState);
                Assert.Equal(2, native.Port);
                WpfTestHost.Invoke(() =>
                {
                    Assert.Equal(2, owner.DisplayConfig.PortNum);
                    Assert.Equal("ND1", owner.Config.ActiveCalibrationGroupName);
                    Assert.Equal(second, owner.Config.MaguideFile);
                });

                native.Port = 0;
                MsgRecord readChangedPort = await NdCommand(() => owner.DService.GetPort());
                Assert.Equal(MsgRecordState.Success, readChangedPort.MsgRecordState);
                WpfTestHost.Invoke(() =>
                {
                    Assert.Equal(0, owner.DisplayConfig.PortNum);
                    Assert.Equal("ND0", owner.Config.ActiveCalibrationGroupName);
                    Assert.Equal(first, owner.Config.MaguideFile);
                });
            }
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

    private sealed class NdTransport : ILocalCfwPortTransport
    {
        internal int Opens, Port;
        internal bool WrongRead;
        public void Open(LocalCfwPortConnection config) => Opens++;
        public void Close() { }
        public void SetPort(int port) => Port = port;
        public int ReadPort(int timeout) => WrongRead ? Port + 1 : Port;
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task RestoringActiveNdGroupDoesNotSendCommandsAndUserSelectionUsesItsBackend(bool local, bool retireBeforeReply)
    {
        IMqttClient transport = DispatchProxy.Create<IMqttClient, CalibrationMqttClient>();
        var client = (CalibrationMqttClient)(object)transport;
        var control = new MQTTControl(transport);
        Assert.True(await control.Connect());
        try { await WpfTestHost.Invoke(RunOnDispatcher); }
        finally { await control.DisconnectAsyncClient(); }

        async Task RunOnDispatcher()
        {
            using var scope = new TestScope();
            using var device = CreateSpectrum();
            MySqlConnectedEndpoint? current = null;
            int databaseOpens = 0;
            MySqlConnectedEndpoint Endpoint() => new(candidate => ReferenceEquals(candidate, current), () =>
            {
                databaseOpens++;
                throw new InvalidOperationException("An expired calibration command must not use a database.");
            });
            if (retireBeforeReply)
            {
                device.SysResourceModel.Id = 41;
                device.ResultDatabaseEndpoint = current = Endpoint();
            }
            device.Config.NDConfig.IsBingNDDevice = false;
            device.SpectrumBackend.SetPreference(local);
            device.DService.Dispose();
            device.Config.SendTopic = "test/spectrum-nd/command";
            device.Config.SubscribeTopic = "test/spectrum-nd/reply";
            using var mqtt = new MQTTSpectrum(device, control, (_, _) => null, () => new Version(), _ => null);
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
                Assert.Equal(0, client.Commands.Reader.Count);
                Assert.True(selector.IsEnabled);

                // A queued binding update after an internal apply must also remain passive.
                device.Config.ActiveCalibrationGroupName = "ND2";
                Pump();
                Assert.Equal("ND2", selector.SelectedValue);
                Assert.Equal(0, client.Commands.Reader.Count);
                device.DisplayConfig.PortNum = 2;
                selector.SelectedIndex = 0;
                Pump();
                Assert.Equal(local ? 0 : 1, client.Commands.Reader.Count);
                Assert.Equal(local, selector.IsEnabled);
                if (retireBeforeReply)
                {
                    Assert.Equal(2, device.DisplayConfig.PortNum);
                    Assert.True(client.Commands.Reader.TryRead(out string? payload));
                    var command = JObject.Parse(payload!);
                    Assert.Equal("SetPort", command.Value<string>("EventName"));
                    Assert.Equal(1, Assert.IsType<JObject>(command["params"]).Value<int>("PortNum"));

                    // Keep the same device and resource ID while replacing its verified configuration source.
                    device.ResultDatabaseEndpoint = current = Endpoint();
                    await client.ReplyAsync(mqtt.SubscribeTopic, new MsgReturn
                    {
                        MsgID = command.Value<string>("MsgID")!, EventName = "SetPort", DeviceCode = device.Code,
                        Code = 0, Data = new JObject { ["Port"] = 1 }
                    });
                    Pump();
                    Assert.Equal("ND2", device.Config.ActiveCalibrationGroupName);
                    Assert.Equal(2, device.DisplayConfig.PortNum);
                    Assert.Equal(0, databaseOpens);
                    Assert.True(selector.IsEnabled);
                    Assert.True(device.CalibrationGroupChangeGuard.TryBeginUserSwitch());
                    device.CalibrationGroupChangeGuard.CompleteUserSwitch();
                    return;
                }
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
                    Assert.Contains("绑定 ND 滤光轮", (string)set.MsgReturn.Message);
                    Assert.Equal("ND1", device.Config.ActiveCalibrationGroupName);
                    Assert.Equal("nd1-magnitude.dat", device.Config.MaguideFile);
                    MsgRecord read = device.RunLocalSpectrumCommand("GetPort");
                    Pump();
                    Assert.Equal(MsgRecordState.Fail, read.MsgRecordState);
                    Assert.Contains("绑定 ND 滤光轮", (string)read.MsgReturn.Message);
                    Assert.Equal(0, client.Commands.Reader.Count);
                }

                // A missing external wheel is a passive no-op, including after a delayed binding update.
                selector.SetCurrentValue(ComboBox.SelectedValueProperty, device.Config.ActiveCalibrationGroupName);
                device.CalibrationGroupChangeGuard.CompleteUserSwitch();
                selector.IsEnabled = true;
                device.Config.NDConfig.IsBingNDDevice = true;
                device.Config.NDConfig.NDBindDeviceCode = "ND.NotLoaded";
                string active = device.Config.ActiveCalibrationGroupName;
                int port = device.DisplayConfig.PortNum;
                int commands = client.Commands.Reader.Count;
                selector.SelectedIndex = active == "ND1" ? 1 : 0;
                Pump();
                Assert.Equal(active, device.Config.ActiveCalibrationGroupName);
                Assert.Equal(active, selector.SelectedValue);
                Assert.Equal(port, device.DisplayConfig.PortNum);
                Assert.Equal(commands, client.Commands.Reader.Count);
            }
            finally
            {
                window.Content = null;
                window.Close();
                DockViewManager.GetInstance().RemoveView(device.ViewShell);
            }
        }
    }

    [Fact]
    public void FilterWheelShowsActualPositionAndOnlySwitchesOnButtonClick()
    {
        WpfTestHost.Invoke(() =>
        {
            using var scope = new TestScope();
            using var device = new DeviceCfwPort(new SysResourceModel { Id = -2, Code = "ND.Card", Value = "{}" });
            device.DService.Dispose();
            using var mqtt = new RecordingFilterWheel(device.Config) { Device = device };
            device.DService = mqtt;
            using var card = new DisplayCfwPort(device);
            var window = ShowCard(card);
            try
            {
                var choice = Assert.IsType<ComboBox>(card.FindName("CombPort"));
                var button = Assert.IsType<Button>(card.FindName("SwitchPositionButton"));
                var position = Assert.IsType<TextBlock>(card.FindName("CurrentHoleText"));
                var read = Assert.IsType<Button>(card.FindName("RefreshPositionButton"));
                Assert.False(choice.IsVisible);
                mqtt.DeviceStatus = DeviceStatusType.Opened;
                device.UpdateConfirmedPort(1);
                Pump();

                Assert.True(choice.IsVisible);
                Assert.Equal("ND8", position.Text);
                Assert.Equal(88d, choice.ActualWidth, precision: 0);
                Assert.Equal(position.TranslatePoint(new Point(0, position.ActualHeight / 2), card).Y,
                    choice.TranslatePoint(new Point(0, choice.ActualHeight / 2), card).Y, precision: 0);
                Assert.True(choice.TranslatePoint(default, card).X < button.TranslatePoint(default, card).X);
                Assert.True(button.TranslatePoint(default, card).X < position.TranslatePoint(default, card).X);
                Assert.True(position.TranslatePoint(default, card).X < read.TranslatePoint(default, card).X);
                Assert.Equal(1, choice.SelectedValue);
                Assert.False(button.IsEnabled);

                choice.SelectedValue = 3;
                Assert.Equal(0, mqtt.Commands);
                Assert.Equal(3, device.DisplayConfig.TargetPortNum);
                Assert.Equal("ND8", position.Text);
                Assert.True(button.IsEnabled);

                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(1, mqtt.Commands);
                Assert.False(choice.IsEnabled);
                Assert.Equal("ND8", position.Text);
                var record = Assert.IsType<MsgRecord>(mqtt.LastRecord);
                Assert.Equal(3, JObject.FromObject(record.MsgSend.Params).Value<int>("PortNum"));
                record.MsgReturn = new MsgReturn { Data = new JObject { ["Port"] = 3 } };
                record.MsgRecordState = MsgRecordState.Success;
                Pump();
                Assert.True(choice.IsEnabled);
                Assert.False(button.IsEnabled);
                Assert.Equal("ND1000", position.Text);

                mqtt.DeviceStatus = DeviceStatusType.Closed;
                Pump();
                Assert.Null(device.ConfirmedPort);
                Assert.False(choice.IsVisible);
            }
            finally { window.Content = null; window.Close(); }
        });
    }

    [Fact]
    public void FlowWheelPositionUsesSelectedDeviceNamesAndPreservesTheSavedIndex()
    {
        WpfTestHost.Invoke(() =>
        {
            using var scope = new TestScope();
            scope.AddZeroBasedPhysicalWheel();
            var physical = PhyCameraManager.GetInstance().PhyCameras[0];
            physical.Config.FilterWheelConfig.HoleMapping[2].HoleName = "专用ND";
            using var first = new DeviceCfwPort(new SysResourceModel
                { Id = -2, Code = "ND.Flow.Names", Value = "{\"SN\":\"PHY.CFW.TEST\"}" });
            using var second = new DeviceCfwPort(new SysResourceModel { Id = -3, Code = "ND.Flow.Default", Value = "{}" });
            var managerField = typeof(ServiceManager).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)!;
            var previous = managerField.GetValue(null);
            var manager = (ServiceManager)RuntimeHelpers.GetUninitializedObject(typeof(ServiceManager));
            manager.DeviceServices = [first, second];
            managerField.SetValue(null, manager);
            try
            {
                first.DService.DeviceStatus = DeviceStatusType.Opened;
                first.UpdateConfirmedPort(2);
                var node = new FWNode();
                node.Create();
                node.DeviceCode = first.Code;
                node.Port = 11;
                var property = typeof(FWNode).GetProperty(nameof(FWNode.Port))!;
                Assert.Equal(typeof(FlowFilterWheelPositionEditor), FlowNodePropertyMetadataProvider.Instance.GetEditorType(property));
                var panel = new FlowFilterWheelPositionEditor().GenProperties(property, node);
                var combo = Assert.Single(Assert.Single(panel.Children.OfType<Grid>()).Children.OfType<ComboBox>());

                Assert.Empty(combo.Items);
                var window = ShowCard(new UserControl { Content = panel });
                try
                {
                    Assert.Equal(11, combo.SelectedValue);
                    Assert.Equal(11, node.Port);
                    combo.SelectedValue = 2;
                    Assert.Equal(2, node.Port);
                    Assert.Equal(EngineLocalization.Format($"{"专用ND"}（当前）"), Label(combo.SelectedItem));

                    node.DeviceCode = second.Code;
                    Assert.Equal(2, node.Port);
                    Assert.Equal("ND64", Label(combo.SelectedItem));
                }
                finally { window.Content = null; window.Close(); }
            }
            finally { managerField.SetValue(null, previous); }

            static string Label(object item) => (string)TypeDescriptor.GetProperties(item)["Label"]!.GetValue(item)!;
        });
    }

    [Fact]
    public void FilterWheelConfigurationEditsDisplayNameInTheConnectionCard()
    {
        WpfTestHost.Invoke(() =>
        {
            using var scope = new TestScope();
            using var device = new DeviceCfwPort(new SysResourceModel
            {
                Id = -2, Code = "ND.Configuration", Name = "Original wheel", Value = "{}"
            });
            device.DisplayConfig.UseLocalCfwPort = true;
            var editor = new EditCfwPort(device);
            try
            {
                Assert.Equal("Original wheel", editor.Draft.Name);
                Pump();
                var name = Assert.IsType<TextBox>(editor.FindName("DisplayNameTextBox"));
                var nameRow = Assert.IsType<DockPanel>(name.Parent);
                var fields = Assert.IsType<StackPanel>(nameRow.Parent);
                var connectionFields = Assert.IsType<StackPanel>(editor.FindName("DeviceFields"));
                var licenseFields = Assert.IsType<StackPanel>(editor.FindName("LicenseFields"));
                Assert.Same(nameRow, fields.Children[1]);
                Assert.Same(fields, connectionFields.Parent);
                Assert.Same(fields, licenseFields.Parent);

                Assert.Equal("Original wheel", name.Text);
                name.SetCurrentValue(TextBox.TextProperty, "Renamed wheel");
                name.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                Assert.Equal("Renamed wheel", editor.Draft.Name);
                Assert.Equal("Original wheel", device.Config.Name);
                Assert.Equal((true, false), editor.Draft.Apply(device.Config, device.DisplayConfig));
                Assert.Equal("Renamed wheel", device.Config.Name);

                Assert.Equal(Visibility.Collapsed, licenseFields.Visibility);
                editor.Draft.UseLocalCfwPort = false;
                Assert.Equal(Visibility.Visible, licenseFields.Visibility);
                Assert.True(device.DisplayConfig.UseLocalCfwPort);
            }
            finally { editor.Close(); }
        });
    }

    [Fact]
    public async Task LocalOpenShowsConfirmedHoleOnTheCard()
    {
        TestScope? scope = null;
        DeviceCfwPort? device = null;
        DisplayCfwPort? card = null;
        Window? window = null;
        var positionConfirmed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            WpfTestHost.Invoke(() =>
            {
                scope = new TestScope();
                var config = new ConfigCfwPort { SzComName = "COM5718", IsAutoOpen = false };
                device = new DeviceCfwPort(new SysResourceModel
                {
                    Id = -2, Code = "ND.Local.Card", Value = JsonConvert.SerializeObject(config)
                }, new LocalCfwPortSession(new NdTransport { Port = 3 }));
                device.DisplayConfig.UseLocalCfwPort = true;
                device.DisplayConfig.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(DisplayCfwPortConfig.PortNum)) positionConfirmed.TrySetResult();
                };

                var editor = new EditCfwPort(device);
                Assert.True(editor.Draft.UseLocalCfwPort);
                Assert.Equal(config.SzComName, editor.Draft.SzComName);
                editor.Close();

                card = new DisplayCfwPort(device);
                window = ShowCard(card);
                Assert.IsType<Button>(card.FindName("ButtonOpen")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            });
            await positionConfirmed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            WpfTestHost.Invoke(() =>
            {
                Pump();
                Assert.Equal(DeviceStatusType.Opened, device!.DService.DeviceStatus);
                Assert.Equal(3, device.DisplayConfig.PortNum);
                Assert.Equal(3, device.ConfirmedPort);
                var hole = Assert.Single(device.FilterWheelConfig.HoleMapping, item => item.HoleIndex == 3);
                Assert.Equal(hole.HoleName, Assert.IsType<TextBlock>(card!.FindName("CurrentHoleText")).Text);
            });
        }
        finally
        {
            WpfTestHost.Invoke(() =>
            {
                if (window != null) { window.Content = null; window.Close(); }
                card?.Dispose();
                device?.Dispose();
                scope?.Dispose();
            });
        }
    }

    [Fact]
    public void FilterWheelCommandRepliesRememberSuccessfulPositionsAndIgnoreFailures()
    {
        WpfTestHost.Invoke(() =>
        {
            using var scope = new TestScope();
            using var device = new DeviceCfwPort(new SysResourceModel { Id = -2, Code = "ND.Replies", Value = "{}" });
            device.DService.Dispose();
            using var mqtt = new RecordingFilterWheel(device.Config) { Device = device };
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WheelReadbackUpdatesOnlyItsCurrentWheelAndOriginallyBoundSpectra(bool retireWheel)
    {
        WpfTestHost.Invoke(() =>
        {
            using var scope = new TestScope();
            scope.AddZeroBasedPhysicalWheel();
            var managerField = typeof(ServiceManager).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)!;
            object? previousManager = managerField.GetValue(null);
            var manager = (ServiceManager)RuntimeHelpers.GetUninitializedObject(typeof(ServiceManager));
            manager.DeviceServices = [];
            using var wheel = new DeviceCfwPort(new SysResourceModel { Id = -2, Code = "ND.LateReply", Value = "{\"SN\":\"PHY.CFW.TEST\"}" });
            using var original = CreateSpectrum();
            using var unchanged = CreateSpectrum();
            using var replacement = new DeviceSpectrum(new SysResourceModel
            {
                Code = original.Code,
                Value = JsonConvert.SerializeObject(original.Config),
            });
            foreach (var spectrum in new[] { original, unchanged, replacement })
            {
                spectrum.Config.NDConfig.NDBindDeviceCode = wheel.Code;
                spectrum.Config.NDConfig.IsBingNDDevice = true;
                spectrum.DisplayConfig.PortNum = 1;
            }
            wheel.DService.Dispose();
            using var mqtt = new RecordingFilterWheel(wheel.Config) { Device = wheel };
            wheel.DService = mqtt;
            mqtt.DeviceStatus = DeviceStatusType.Opened;
            wheel.DisplayConfig.PortNum = 1;
            bool databaseCurrent = true;
            wheel.SysResourceModel.Id = 81;
            wheel.ResultDatabaseEndpoint = new MySqlConnectedEndpoint(_ => databaseCurrent,
                () => throw new InvalidOperationException("Port readback must not open a database."));
            manager.DeviceServices = [wheel, original, unchanged];
            managerField.SetValue(null, manager);
            try
            {
                using var card = new DisplayCfwPort(wheel);
                typeof(DisplayCfwPort).GetMethod("GetPort_Click", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(card, [card, new RoutedEventArgs()]);
                MsgRecord record = Assert.IsType<MsgRecord>(mqtt.LastRecord);

                manager.DeviceServices.Remove(original);
                manager.DeviceServices.Add(replacement);
                databaseCurrent = !retireWheel;
                record.MsgReturn = new MsgReturn { Data = new JObject { ["Port"] = 4 } };
                record.MsgRecordState = MsgRecordState.Success;
                Pump();

                Assert.Equal(retireWheel ? 1 : 4, wheel.DisplayConfig.PortNum);
                Assert.Equal(retireWheel ? 1 : 4, unchanged.DisplayConfig.PortNum);
                Assert.Equal(1, replacement.DisplayConfig.PortNum);
                Assert.Equal("ND1", unchanged.Config.ActiveCalibrationGroupName);
                Assert.Equal("ND1", replacement.Config.ActiveCalibrationGroupName);
            }
            finally { managerField.SetValue(null, previousManager); }
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
                    if (wrongReadback) Assert.Throws<InvalidOperationException>(() => operation.SetTransmitting(false));
                    else
                    {
                        operation.SetTransmitting(false);
                        operation.SetTransmitting(true);
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
    public async Task ServiceFilterWheelCompletesSequentialCalibrationRepliesWhileDispatcherWaits()
    {
        IMqttClient transport = DispatchProxy.Create<IMqttClient, CalibrationMqttClient>();
        var client = (CalibrationMqttClient)(object)transport;
        var control = new MQTTControl(transport);
        var managerField = typeof(ServiceManager).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)!;
        object? previousManager = managerField.GetValue(null);
        TestScope? scope = null;
        DeviceCfwPort? device = null;
        List<int?> displayedPorts = [];
        try
        {
            Assert.True(await control.Connect());
            WpfTestHost.Invoke(() =>
            {
                scope = new TestScope();
                scope.AddZeroBasedPhysicalWheel();
                var manager = (ServiceManager)RuntimeHelpers.GetUninitializedObject(typeof(ServiceManager));
                manager.DeviceServices = [];
                managerField.SetValue(null, manager);
                device = new DeviceCfwPort(new SysResourceModel { Id = -2, Code = "ND.ServiceCalibration", Value = "{}" });
                device.DService.Dispose();
                device.Config.SzComName = "COM9052";
                device.Config.SendTopic = "test/cfw/cmd";
                device.Config.SubscribeTopic = "test/cfw/status";
                device.DisplayConfig.UseLocalCfwPort = false;
                device.DService = new MQTTCfwPort(device.Config, control) { Device = device };
                device.DisplayConfig.PortNum = 5;
                device.DisplayConfig.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(DisplayCfwPortConfig.PortNum))
                        displayedPorts.Add(device.DisplayConfig.PortNum);
                };
                manager.DeviceServices.Add(device);
            });

            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            Task replies = Task.Run(async () =>
            {
                (string Operation, int Port)[] expectedReplies = [("Open", -1), ("SetPort", 3), ("GetPort", 0)];
                foreach (var expected in expectedReplies)
                {
                    string payload = await client.Commands.Reader.ReadAsync(cancellation.Token);
                    Assert.DoesNotContain(nameof(MsgSend.CompleteWithoutDispatcher), payload);
                    MsgSend command = JsonConvert.DeserializeObject<MsgSend>(payload)!;
                    Assert.Equal(expected.Operation, command.EventName);
                    if (command.EventName == "SetPort")
                        Assert.Equal(0, ((JObject)command.Params).Value<int>("PortNum"));
                    await client.ReplyAsync(device!.Config.SubscribeTopic, new MsgReturn
                    {
                        MsgID = command.MsgID, EventName = command.EventName, DeviceCode = command.DeviceCode,
                        Code = 0, Data = new JObject { ["Port"] = expected.Port },
                    });
                }
            });

            using var finished = new ManualResetEventSlim();
            Task? calibration = null;
            bool completedWhileUiWaited = WpfTestHost.Invoke(() =>
            {
                calibration = Task.Run(() =>
                {
                    try
                    {
                        using (var wheel = new LocalSpectrumFilterWheel(new ShutterConfig
                        { FilterWheelDeviceCode = device!.Code, UseFilterWheel = true, MeasurementPort = 5, DarkPort = 0 }))
                            wheel.SetTransmitting(false);
                        using var available = device!.DService.ReserveForCalibration();
                    }
                    finally { finished.Set(); }
                });
                return finished.Wait(TimeSpan.FromSeconds(5));
            });

            await Task.WhenAll(replies, calibration!).WaitAsync(cancellation.Token);
            Assert.True(completedWhileUiWaited);
            await WpfTestHost.Invoke(async () => await Dispatcher.Yield(DispatcherPriority.Background));
            Assert.Equal(new int?[] { 0 }, displayedPorts);
            Assert.Equal(DeviceStatusType.Opened, device!.DService.DeviceStatus);
        }
        finally
        {
            await control.DisconnectAsyncClient();
            WpfTestHost.Invoke(() =>
            {
                device?.Dispose();
                managerField.SetValue(null, previousManager);
                scope?.Dispose();
            });
        }
    }

    public class CalibrationMqttClient : MQTTControlTests.ClientProxy
    {
        private Func<MqttApplicationMessageReceivedEventArgs, Task>? _messageReceived;
        public Channel<string> Commands { get; } = Channel.CreateUnbounded<string>();

        public Task ReplyAsync(string topic, MsgReturn response) => ReplyAsync(topic, JsonConvert.SerializeObject(response));

        public Task ReplyAsync(string topic, string payload)
        {
            var message = new MqttApplicationMessageBuilder().WithTopic(topic).WithPayload(payload).Build();
            return _messageReceived!(new("cfw-test", message, new MqttPublishPacket(), (_, _) => Task.CompletedTask));
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod!.Name)
            {
                case "add_ApplicationMessageReceivedAsync":
                    _messageReceived += (Func<MqttApplicationMessageReceivedEventArgs, Task>)args![0]!;
                    return null;
                case "PublishAsync":
                    Commands.Writer.TryWrite(Encoding.UTF8.GetString(((MqttApplicationMessage)args![0]!).Payload));
                    return Task.FromResult<MqttClientPublishResult>(null!);
                default:
                    return base.Invoke(targetMethod, args);
            }
        }
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

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ShutterButtonsUseLocalDirectionWithoutChangingServiceConvention(bool local, bool openForDark)
    {
        WpfTestHost.Invoke(() =>
        {
            using var scope = new TestScope();
            using var device = CreateSpectrum();
            device.SpectrumBackend.SetPreference(local);
            device.Config.ShutterCfg.OpenForDark = openForDark;
            device.DService.Dispose();
            using var mqtt = new RecordingSpectrum(device);
            device.DService = mqtt;

            MsgRecord dark = device.SetShutterTransmitting(false);
            MsgRecord light = device.SetShutterTransmitting(true);

            Assert.Equal(local && openForDark ? "ShutterDoOpen" : "ShutterDoClose", dark.MsgSend.EventName);
            Assert.Equal(local && openForDark ? "ShutterDoClose" : "ShutterDoOpen", light.MsgSend.EventName);
            Assert.Equal(2, mqtt.Commands);
        });
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
        public MsgRecord? LastRecord;
        internal override MsgRecord PublishAsyncClient(MsgSend msg, double timeout = 30000)
        { Commands++; return LastRecord = new() { MsgSend = msg, MsgRecordState = Immediate ? MsgRecordState.Success : MsgRecordState.Sended }; }
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
            TestEnvironment.SetConfig(new ConfigHandler { ConfigFilePath = ConfigPath, IsAutoSave = false });
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
            TestEnvironment.SetConfig(previousConfig);
            foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
            foreach (string file in Directory.EnumerateFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }
}
