using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Messages;
using ColorVision.Database;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Local;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.Themes;
using ColorVision.UI.Views;
using cvColorVision;
using Newtonsoft.Json;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class SpectrumButtonOperationTests
{
    [Theory]
    [InlineData(MsgRecordState.Success, false)]
    [InlineData(MsgRecordState.Fail, false)]
    [InlineData(MsgRecordState.Timeout, false)]
    [InlineData(MsgRecordState.Success, true)]
    [InlineData(MsgRecordState.Fail, true)]
    [InlineData(MsgRecordState.Timeout, true)]
    public void ProgressProtectsOnlyTheInvokedButtonAndRestoresItAtCompletion(MsgRecordState terminalState, bool opening)
    {
        WpfTestHost.Invoke(() =>
        {
            var previousConfig = ConfigService.Instance;
            List<ResourceDictionary> dictionaries = [];
            Window? window = null;
            LocalSpectrumWindow? localWindow = null;
            DeviceSpectrum? device = null;
            DisplaySpectrum? card = null;
            try
            {
                TestEnvironment.SetConfig(new ConfigHandler { IsAutoSave = false });
                foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                {
                    var dictionary = new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                    dictionaries.Add(dictionary);
                    Application.Current.Resources.MergedDictionaries.Add(dictionary);
                }
                device = new DeviceSpectrum(new SysResourceModel
                {
                    Id = -2, Code = $"spectrum-button-{Guid.NewGuid():N}", Name = "Offline spectrum button test", Value = "{}"
                });
                device.SpectrumBackend.SetLocalStatus(opening ? DeviceStatusType.Closed : DeviceStatusType.Opened);
                card = new DisplaySpectrum(device);
                window = new Window
                {
                    Content = card, Width = 340, Height = 600, Left = -10000, Top = -10000,
                    ShowActivated = false, ShowInTaskbar = false
                };
                window.Show();
                Pump();

                var single = Assert.IsType<Button>(card.FindName(opening ? "btn_connect" : "btn_oneTest"));
                var close = Assert.IsType<Button>(card.FindName("btn_connect"));
                var panel = Assert.IsType<StackPanel>(card.FindName("StackPanelOpen"));
                var content = Assert.IsType<StackPanel>(card.FindName("StackPanelContent"));
                var record = new MsgRecord { MsgRecordState = MsgRecordState.Sended };
                int calls = 0, completions = 0;
                Func<MsgRecord> command = () => { calls++; return record; };
                Action<MsgRecord, MsgRecordState> completed = (_, _) => completions++;
                var execute = typeof(DisplaySpectrum).GetMethod("ExecuteSpectrumCommand", BindingFlags.Instance | BindingFlags.NonPublic)!;
                execute.Invoke(card, [single, command, completed]);
                execute.Invoke(card, [single, command, completed]);
                if (opening) device.SpectrumBackend.SetLocalStatus(DeviceStatusType.Opening);
                Pump();

                Assert.Equal(1, calls);
                Assert.True(card.TryGetTimedButtonOperations()!.Get(single)!.IsRunning);
                Assert.False(single.IsEnabled);
                Assert.Equal(!opening, close.IsEnabled);
                Assert.True(content.IsVisible);
                Assert.Equal(!opening, panel.IsVisible);
                Assert.True(panel.IsEnabled);
                Assert.Equal(Visibility.Collapsed, Assert.IsType<Button>(card.FindName("btn_stopTest")).Visibility);
                Assert.Equal(opening ? DeviceStatusType.Opening : DeviceStatusType.Opened, device.DService.DeviceStatus);

                if (opening) device.SpectrumBackend.SetLocalStatus(terminalState == MsgRecordState.Success ? DeviceStatusType.Opened : DeviceStatusType.Closed);
                record.MsgRecordState = terminalState;
                Pump();
                Assert.Equal(1, completions);
                Assert.False(card.TryGetTimedButtonOperations()!.Get(single)!.IsRunning);
                Assert.True(single.IsEnabled);
                Assert.True(close.IsEnabled);
                Assert.True(content.IsVisible);
                Assert.Equal(!opening || terminalState == MsgRecordState.Success, panel.IsVisible);
                Assert.Equal(opening
                    ? terminalState == MsgRecordState.Success ? ColorVision.Engine.Properties.Resources.Close : ColorVision.Engine.Properties.Resources.Open
                    : ColorVision.Engine.Properties.Resources.SingleTest, single.Content);

                if (!opening && terminalState == MsgRecordState.Success)
                {
                    // Continuous measurements retain every saved frame and read the insertion setting for each capture.
                    // Result delivery targets an existing view, as created when the workspace opens the device page.
                    ViewSpectrum spectrumView = device.View;
                    bool databaseCurrent = true;
                    var resultDatabase = new MySqlConnectedEndpoint(_ => databaseCurrent, () => throw new InvalidOperationException("Synthetic results need no database client."));
                    var configurationTarget = device.CaptureConfigurationTarget();
                    foreach (int id in new[] { 18, 19, 20 })
                    {
                        ViewSpectrum.Config.InsertAtBeginning = id != 19;
                        device.DisplayLocalSpectrum(new LocalSpectrumCapture
                        {
                            MeasurementId = id, DeviceCode = device.Code, IntegralTime = 100,
                            Data = new COLOR_PARA { fPL = [1, -.5f], fSpect1 = 500, fSpect2 = 501, fInterval = 1 }
                        }, true, configurationTarget, resultDatabase);
                        Assert.Equal(id, Assert.IsType<ViewResultSpectrum>(((ListView)spectrumView.FindName("listView1")).SelectedItem).Id);
                    }
                    Assert.Equal(new[] { 20, 18, 19 }, device.View.ViewResults.Select(row => row.Id));
                    Assert.All(device.View.ViewResults, row => Assert.Equal(new float[] { 1, -.5f }, row.fPL));
                    var original = device.View.ViewResults.ToArray();
                    databaseCurrent = false;
                    device.DisplayLocalSpectrum(new LocalSpectrumCapture { MeasurementId = 18, DeviceCode = device.Code, IntegralTime = 200 },
                        true, configurationTarget, resultDatabase);
                    Assert.Equal(original, device.View.ViewResults);
                    Assert.Equal(100, device.DisplayConfig.IntTime);

                    // A valid measurement is still published when its ND configuration write fails.
                    databaseCurrent = true;
                    device.SysResourceModel.Id = 31;
                    device.ResultDatabaseEndpoint = resultDatabase;
                    device.DisplayConfig.PortNum = 2;
                    LocalSpectrumCapture? delivered = null;
                    device.LocalResultAvailable += (result, _) => delivered = result;
                    var measured = new LocalSpectrumCapture
                    {
                        MeasurementId = 21, DeviceCode = device.Code, IntegralTime = 120, NDPort = 1,
                        NdCalibration = new(1, "other-wave.dat", "other-magnitude.dat", "ND1"),
                        Data = new COLOR_PARA { fPL = [1, -.5f], fSpect1 = 500, fSpect2 = 501, fInterval = 1 }
                    };
                    device.DisplayLocalSpectrum(measured, true, device.CaptureConfigurationTarget(), resultDatabase);
                    Assert.Same(measured, delivered);
                    Assert.Equal(120, device.DisplayConfig.IntTime);
                    Assert.Equal(2, device.DisplayConfig.PortNum);
                    Assert.Contains(device.View.ViewResults, row => row.Id == measured.MeasurementId);
                    original = device.View.ViewResults.ToArray();

                    IEnumerable<ViewResultSpectrum> InvalidHistory()
                    {
                        yield return original[0];
                        throw new JsonSerializationException("Unreadable history row");
                    }
                    Assert.Throws<JsonSerializationException>(() => device.View.ReplaceHistoryResults(InvalidHistory()));
                    Assert.Equal(original, device.View.ViewResults);
                    device.SpectrumBackend.SetLocalStatus(DeviceStatusType.Closing);
                    Pump();
                    Assert.True(content.IsVisible);
                    Assert.True(panel.IsVisible);
                    device.SpectrumBackend.SetLocalStatus(DeviceStatusType.Closed);
                    Pump();
                    Assert.True(content.IsVisible);
                    Assert.False(panel.IsVisible);

                    device.SpectrumBackend.SetLocalStatus(DeviceStatusType.Opened);
                    localWindow = new LocalSpectrumWindow(device) { Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false };
                    localWindow.Show();
                    Pump();
                    var stop = Assert.IsType<Button>(card.FindName("btn_stopTest"));
                    var localStop = Assert.IsType<Button>(localWindow.FindName("StopCaptureButton"));
                    Assert.Equal(Visibility.Collapsed, localStop.Visibility);
                    device.SpectrumBackend.SetLocalStatus(DeviceStatusType.SP_Continuous_Mode);
                    Pump();
                    Assert.True(stop.IsVisible);
                    Assert.True(localStop.IsVisible);
                    device.SpectrumBackend.SetLocalStatus(DeviceStatusType.Opened);
                    Pump();
                    Assert.Equal(Visibility.Collapsed, stop.Visibility);
                    Assert.Equal(Visibility.Collapsed, localStop.Visibility);

                    // Reset completion clears the display's automatic dark modes only after success.
                    var startRecord = typeof(DeviceSpectrum).GetMethod("StartLocalRecord", BindingFlags.Instance | BindingFlags.NonPublic)!;
                    device.DisplayConfig.IsShutter = true;
                    Func<Task<object?>> failedReset = () => Task.FromException<object?>(new InvalidOperationException("reset rejected"));
                    var reset = Assert.IsType<MsgRecord>(startRecord.Invoke(device, ["ResetDark", failedReset, null]));
                    Pump();
                    Assert.Equal(MsgRecordState.Fail, reset.MsgRecordState);
                    Assert.True(device.DisplayConfig.IsShutter);
                    Func<Task<object?>> resetWork = () => Task.FromResult<object?>(null);
                    reset = Assert.IsType<MsgRecord>(startRecord.Invoke(device, ["ResetDark", resetWork, null]));
                    Pump();
                    Assert.Equal(MsgRecordState.Success, reset.MsgRecordState);
                    Assert.False(device.DisplayConfig.IsAutoDark);
                    Assert.False(device.DisplayConfig.IsShutter);
                }

                // Immediate replies (for example rejected calibration) must not leave a progress overlay running.
                execute.Invoke(card, [single, command, completed]);
                Assert.Equal(2, completions);
                Assert.False(card.TryGetTimedButtonOperations()!.Get(single)!.IsRunning);
                Assert.True(single.IsEnabled);
            }
            finally
            {
                localWindow?.Close();
                card?.Dispose();
                window?.Close();
                if (device != null)
                {
                    DockViewManager.GetInstance().RemoveView(device.ViewShell);
                    device.Dispose();
                }
                foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
                TestEnvironment.SetConfig(previousConfig);
            }
        });
    }

    [Fact]
    public void CurrentEditorAndApplyToNodeUseTheConsumerUnit()
    {
        WpfTestHost.Invoke(() =>
        {
            var previous = ConfigService.Instance;
            List<ResourceDictionary> dictionaries = [];
            try
            {
                TestEnvironment.SetConfig(new ConfigHandler { IsAutoSave = false });
                foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                {
                    var dictionary = new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                    dictionaries.Add(dictionary);
                    Application.Current.Resources.MergedDictionaries.Add(dictionary);
                }
                using var device = new DeviceSpectrum(new SysResourceModel
                {
                    Id = -2, Code = $"spectrum-current-{Guid.NewGuid():N}", Name = "Offline current units", Value = "{}"
                });
                device.DisplayConfig.UseLocalSpectrum = true;
                device.DisplayConfig.IsLuminousFluxMode = true;
                device.DisplayConfig.ApplySmuReadings(4.5, 17, () => true);
                using var card = new DisplaySpectrum(device) { Language = XmlLanguage.GetLanguage("en-US") };
                var input = Assert.IsType<TextBox>(card.FindName("SpectrumCurrentInput"));
                var unit = Assert.IsType<TextBlock>(card.FindName("SpectrumCurrentUnit"));
                var node = new LocalSpectrumNode { UseUpstreamVI = false };
                var window = new LocalSpectrumWindow(device);
                try
                {
                    window.AttachNode(node);
                    var apply = Assert.IsType<Button>(window.FindName("ApplyNodeButton"));
                    Pump();
                    Assert.Equal("A", unit.Text);
                    Assert.Equal(.017, double.Parse(input.Text, CultureInfo.InvariantCulture));
                    apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.Equal(.017f, node.Current);
                    Assert.Equal(4.5f, node.Voltage);

                    device.DisplayConfig.UseLocalSpectrum = false;
                    Pump();
                    Assert.Equal("mA", unit.Text);
                    Assert.Equal(17, double.Parse(input.Text, CultureInfo.InvariantCulture));
                    input.SetCurrentValue(TextBox.TextProperty, "40");
                    input.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                    Assert.Equal(40, device.DisplayConfig.ManualCurrent);
                    apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.Equal(.04f, node.Current);

                    device.SpectrumBackend.BeginServiceCommand("GetData");
                    try
                    {
                        device.DisplayConfig.UseLocalSpectrum = true;
                        Pump();
                        Assert.Equal("mA", unit.Text);
                    }
                    finally { device.SpectrumBackend.EndServiceCommand(); }
                    Pump();
                    Assert.Equal("A", unit.Text);
                    Assert.Equal(.04, double.Parse(input.Text, CultureInfo.InvariantCulture));
                    input.SetCurrentValue(TextBox.TextProperty, "0.025");
                    input.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                    Assert.Equal(.025, device.DisplayConfig.ManualCurrent);
                    apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.Equal(.025f, node.Current);

                    device.DisplayConfig.UseLocalSpectrum = false;
                    var managerField = typeof(ServiceManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
                    var previousManager = ServiceManager.Current;
                    var manager = new ServiceManager(Application.Current.Dispatcher);
                    manager.DeviceServices.Add(device);
                    var windowField = typeof(DeviceSpectrum).GetField("localWindow", BindingFlags.Instance | BindingFlags.NonPublic)!;
                    managerField.SetValue(null, manager);
                    windowField.SetValue(device, window);
                    try
                    {
                        node.DeviceCode = device.Code;
                        node.Current = .013f;
                        node.OpenLocalSpectrumManagerCommand.Execute(null);
                        Pump();
                        Assert.Equal("mA", unit.Text);
                        Assert.Equal(13, double.Parse(input.Text, CultureInfo.InvariantCulture), precision: 6);
                        Assert.Equal(.013f, device.BuildLocalSpectrumParameters(eqe: true).Current);
                        apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Assert.Equal(.013f, node.Current);
                    }
                    finally
                    {
                        windowField.SetValue(device, null);
                        managerField.SetValue(null, previousManager);
                    }

                    var saved = JsonConvert.DeserializeObject<DisplaySpectrumConfig>(JsonConvert.SerializeObject(device.DisplayConfig))!;
                    Assert.Equal(.013, saved.ReadEffectiveElectricalInputs().NativeCurrentAmps, precision: 7);
                    Assert.Equal(13, saved.ReadEffectiveElectricalInputs().LegacyRemoteCurrent, precision: 5);
                    JsonConvert.PopulateObject("""{"I":0.125}""", saved);
                    Assert.Equal(.125, saved.ReadEffectiveElectricalInputs().NativeCurrentAmps);
                    Assert.Equal(.125, saved.ReadEffectiveElectricalInputs().LegacyRemoteCurrent);
                }
                finally { window.Close(); }
            }
            finally
            {
                foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
                TestEnvironment.SetConfig(previous);
            }
        });
    }

    private static void Pump() => Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
}
