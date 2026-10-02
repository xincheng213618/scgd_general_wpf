using ColorVision.Engine.Services.Devices.Sensor;
using ColorVision.Engine.Services.Devices.Sensor.Templates;
using ColorVision.Engine.Templates;
using ColorVision.Themes;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class TemporarySensorEditorReviewTests
{
    [Fact]
    public void SingleCommandFormPreservesAllRowsAndRenders()
    {
        WpfTestHost.Invoke(() =>
        {
            var previousConfig = ConfigService.Instance;
            var previousCulture = CultureInfo.CurrentUICulture;
            List<ResourceDictionary> dictionaries = [];
            Window? window = null;
            try
            {
                ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
                foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                {
                    var dictionary = new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                    dictionaries.Add(dictionary);
                    Application.Current.Resources.MergedDictionaries.Add(dictionary);
                }
                var param = new SensorParam(new ModMasterModel { Name = "Sensor.Default1" }, []);
                var first = new ModDetailModel { ValueA = "READ?,OK,Ascii,1000/0,0", ValueB = "original-backup" };
                var second = new ModDetailModel { ValueA = "STATUS?,READY,Ascii,1500/20,2", ValueB = "second-backup" };
                param.ModDetailModels.Add(first);
                param.ModDetailModels.Add(second);
                var editor = new EditTemplateSensor();
                editor.SetParam(param);
                window = new Window { Content = editor, Width = 1050, Height = 650, Title = "Sensor.Default 编辑", Left = -10000, Top = -10000, ShowInTaskbar = false, ShowActivated = false };
                window.Show();
                Pump();
                var form = (Grid)editor.FindName("CommandForm");
                var selector = (ComboBox)editor.FindName("CommandSelector");
                var request = (TextBox)editor.FindName("TextBoxSendCommand");
                Assert.Same(param.SensorCommands[0], form.DataContext);
                Assert.Equal("READ?,OK,Ascii,1000/0,0", first.ValueA);
                Assert.Equal("STATUS?,READY,Ascii,1500/20,2", second.ValueA);
                selector.SelectedIndex = 1;
                Pump();
                Assert.Equal("STATUS?", request.Text);
                request.Text = "STATUS2?";
                Pump();
                Assert.Equal("STATUS2?,READY,Ascii,1500/20,2", second.ValueA);
                Assert.Equal("READ?,OK,Ascii,1000/0,0", first.ValueA);
                Assert.Equal("original-backup", first.ValueB);
                Assert.Equal("second-backup", second.ValueB);
                var third = new ModDetailModel { ValueA = "NEW,ACK,Ascii,1000/0,0" };
                param.ModDetailModels.Add(third);
                Pump();
                Assert.Same(param.SensorCommands[2], form.DataContext);
                ((Button)editor.FindName("DeleteCommandButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump();
                Assert.Equal(2, param.SensorCommands.Count);
                Assert.Same(param.SensorCommands[1], form.DataContext);
                editor.SetParam(param);
                Pump();
                Assert.Same(param.SensorCommands[0], form.DataContext);
                Capture(window, "sensor-editor-multiple.png");
                param.ModDetailModels.Remove(second);
                Pump();
                Assert.Equal(Visibility.Collapsed, ((FrameworkElement)editor.FindName("CommandSelectionPanel")).Visibility);
                Capture(window, "sensor-editor-single.png");
                window.Content = null;
                window.Close();
                window = new PropertyEditorWindow(new DisplaySensorConfig { UseLocalSensor = true }) { Left = -10000, Top = -10000, ShowInTaskbar = false, ShowActivated = false };
                window.Show();
                Pump();
                Capture(window, "sensor-local-settings.png");
            }
            finally
            {
                if (window != null) { window.Content = null; window.Close(); }
                foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
                CultureInfo.CurrentUICulture = previousCulture;
                ConfigService.SetInstance(previousConfig);
            }
        });
    }

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static void Capture(Window window, string name)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        using var stream = File.Create(Path.Combine(Path.GetTempPath(), name));
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(stream);
    }
}
