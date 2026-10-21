using ColorVision.Engine;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Local;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.Themes;
using ColorVision.UI.Views;
using cvColorVision;
using Newtonsoft.Json;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
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
            DeviceSpectrum? device = null;
            DisplaySpectrum? card = null;
            try
            {
                ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
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
                if (!opening) Assert.True(Assert.IsType<Button>(card.FindName("btn_stopTest")).IsEnabled);
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
                    var display = typeof(DeviceSpectrum).GetMethod("DisplayLocalSpectrum", BindingFlags.Instance | BindingFlags.NonPublic)!;
                    foreach (int id in new[] { 18, 19, 20 })
                    {
                        ViewSpectrum.Config.InsertAtBeginning = id != 19;
                        display.Invoke(device, [new LocalSpectrumCapture
                        {
                            MeasurementId = id, DeviceCode = device.Code, IntegralTime = 100,
                            Data = new COLOR_PARA { fPL = [1, -.5f], fSpect1 = 500, fSpect2 = 501, fInterval = 1 }
                        }, true]);
                        Assert.Equal(id, Assert.IsType<ViewResultSpectrum>(((ListView)device.View.FindName("listView1")).SelectedItem).Id);
                    }
                    Assert.Equal(new[] { 20, 18, 19 }, device.View.ViewResults.Select(row => row.Id));
                    Assert.All(device.View.ViewResults, row => Assert.Equal(new float[] { 1, -.5f }, row.fPL));
                    var original = device.View.ViewResults.ToArray();
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
                }

                // Immediate replies (for example rejected calibration) must not leave a progress overlay running.
                execute.Invoke(card, [single, command, completed]);
                Assert.Equal(2, completions);
                Assert.False(card.TryGetTimedButtonOperations()!.Get(single)!.IsRunning);
                Assert.True(single.IsEnabled);
            }
            finally
            {
                card?.Dispose();
                window?.Close();
                if (device != null)
                {
                    DockViewManager.GetInstance().RemoveView(device.ViewShell);
                    device.Dispose();
                }
                foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
                ConfigService.SetInstance(previousConfig);
            }
        });
    }

    private static void Pump() => Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
}
