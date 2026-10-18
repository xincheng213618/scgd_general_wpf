using ColorVision.Engine;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Themes;
using ColorVision.UI.Views;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class SpectrumButtonOperationTests
{
    [Theory]
    [InlineData(MsgRecordState.Success)]
    [InlineData(MsgRecordState.Fail)]
    [InlineData(MsgRecordState.Timeout)]
    public void ProgressProtectsOnlyTheInvokedButtonAndRestoresItAtCompletion(MsgRecordState terminalState)
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
                device.SpectrumBackend.SetLocalStatus(DeviceStatusType.Opened);
                card = new DisplaySpectrum(device);
                window = new Window
                {
                    Content = card, Width = 340, Height = 600, Left = -10000, Top = -10000,
                    ShowActivated = false, ShowInTaskbar = false
                };
                window.Show();
                Pump();

                var single = Assert.IsType<Button>(card.FindName("btn_oneTest"));
                var close = Assert.IsType<Button>(card.FindName("btn_connect"));
                var panel = Assert.IsType<StackPanel>(card.FindName("StackPanelOpen"));
                var record = new MsgRecord { MsgRecordState = MsgRecordState.Sended };
                int calls = 0, completions = 0;
                Func<MsgRecord> command = () => { calls++; return record; };
                Action<MsgRecord, MsgRecordState> completed = (_, _) => completions++;
                var execute = typeof(DisplaySpectrum).GetMethod("ExecuteSpectrumCommand", BindingFlags.Instance | BindingFlags.NonPublic)!;
                execute.Invoke(card, [single, command, completed]);
                execute.Invoke(card, [single, command, completed]);
                Pump();

                Assert.Equal(1, calls);
                Assert.True(card.TryGetTimedButtonOperations()!.Get(single)!.IsRunning);
                Assert.False(single.IsEnabled);
                Assert.True(close.IsEnabled);
                Assert.True(panel.IsVisible);
                Assert.True(panel.IsEnabled);
                Assert.Equal(DeviceStatusType.Opened, device.DService.DeviceStatus);

                record.MsgRecordState = terminalState;
                Pump();
                Assert.Equal(1, completions);
                Assert.False(card.TryGetTimedButtonOperations()!.Get(single)!.IsRunning);
                Assert.True(single.IsEnabled);
                Assert.True(close.IsEnabled);
                Assert.True(panel.IsVisible);
                Assert.Equal(ColorVision.Engine.Properties.Resources.SingleTest, single.Content);

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
