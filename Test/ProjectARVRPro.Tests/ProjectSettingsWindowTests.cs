using ColorVision.Themes;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ProjectARVRPro.Tests;

[Collection(nameof(FlowExecutionStatusTestGroup))]
public sealed class ProjectSettingsWindowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OtherSettingsPreserveValidationAndAutoSaveOutsideTheTestingPage(bool dark)
    {
        RunUi(dark, () =>
        {
            var config = new ProjectARVRProConfig { LuxMachineNo = "H03AR", LuxVidMin = 100, LuxVidMax = 200 };
            int saves = 0;
            var panel = new Process.OtherSettingsPanel(config, () => saves++);
            var window = new Window { Content = panel, Width = 900, Height = 520, ShowActivated = false,
                ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000 };
            window.SetResourceReference(Window.BackgroundProperty, "GlobalBackground");
            window.Closing += (_, _) => panel.CommitPendingEdits();
            try
            {
                window.Show();
                Pump();
                var inputs = Descendants<TextBox>(panel).ToArray();
                TextBox machine = inputs.Single(input => input.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == nameof(config.LuxMachineNo));
                machine.Text = "invalid;prefix";
                machine.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                Assert.True(Validation.GetHasError(machine));
                Assert.Equal("H03AR", config.LuxMachineNo);
                Assert.Equal(0, saves);
                machine.Text = "H03";
                machine.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                Assert.Equal("H03", config.LuxMachineNo);
                Assert.Equal(1, saves);
                var mode = Assert.Single(Descendants<ComboBox>(panel));
                mode.SelectedValue = LuxVidMeasurementMode.GetPosition;
                Pump();
                Assert.Equal(LuxVidMeasurementMode.GetPosition, config.LuxVidMode);
                Assert.Equal(2, saves);
                var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(window);
                var png = new PngBitmapEncoder();
                png.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(Path.GetTempPath(), $"arvrpro-lux-other-settings-{(dark ? "dark" : "light")}.png"));
                png.Save(file);
                TextBox offset = inputs.Single(input => input.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == nameof(config.LuxVidB));
                offset.Focus();
                offset.Text = "3.5";
                Assert.Equal(0, config.LuxVidB);
                window.Close();
                Assert.Equal(3.5, config.LuxVidB);
                Assert.Equal(3, saves);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void FailedConnectionSettingsSaveRestoresPreviousParameters()
    {
        RunUi(true, () =>
        {
            var project = new ProjectARVRProConfig { ThunderbirdPortName = "COM19", ThunderbirdBaudRate = 57600 };
            var window = new ThunderbirdSerialDebugWindow(project, () => throw new IOException("test failure"), false);
            try
            {
                ((ComboBox)window.FindName("ComPortComboBox")).Text = "COM20";
                ((ComboBox)window.FindName("BaudRateComboBox")).Text = "115200";
                Assert.False(window.SaveThunderbirdConfig());
                Assert.Equal("COM19", project.ThunderbirdPortName);
                Assert.Equal(57600, project.ThunderbirdBaudRate);
                Assert.Contains("保存失败", ((TextBlock)window.FindName("StatusText")).Text);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void InvalidNumericInputSurvivesSearchWithoutChangingRuntimeValue()
    {
        RunUi(true, () =>
        {
            var project = new ProjectARVRProConfig();
            var results = new ViewResultManagerConfig();
            var session = new ProjectSettingsSession(project, results, false);
            int saves = 0;
            var window = CreateWindow(session, ProjectSettingsPage.Testing, 980, () => saves++);
            try
            {
                window.Show();
                Pump();
                var attempts = FindEditor<TextBox>(window, nameof(ProjectARVRProConfig.TryCountMax));
                attempts.Text = "not-a-number";
                attempts.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                var list = (ListBox)window.FindName("SettingsList");
                list.SelectedItem = session.Sections.Single(section => section.Id == ProjectSettingsPage.Results);
                Pump();
                var search = (TextBox)window.FindName("SearchBox");
                search.Text = "图像保存";
                Pump();
                Assert.Equal("图像保存", search.Text);
                Assert.True(Validation.GetHasError(attempts));
                Assert.Equal(2, project.TryCountMax);
                Assert.Equal(0, saves);
                search.Clear();
                attempts.Text = "4";
                attempts.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                Pump();
                Assert.Equal(1, saves);
                Assert.Equal(4, project.TryCountMax);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void TextAutoSaveWaitsForCompletionAndTitleBarCloseFlushesTheLastEdit()
    {
        RunUi(true, () =>
        {
            var project = new ProjectARVRProConfig();
            var results = new ViewResultManagerConfig();
            int saves = 0;
            var window = CreateWindow(new(project, results, true), ProjectSettingsPage.Testing, 840, () => saves++);
            try
            {
                window.Show();
                Pump();
                var attempts = FindEditor<TextBox>(window, nameof(ProjectARVRProConfig.TryCountMax));
                attempts.Focus();
                attempts.Text = "12";
                Pump();
                Assert.Equal(2, project.TryCountMax);
                Assert.Equal(0, saves);
                ((TextBox)window.FindName("SearchBox")).Focus();
                Pump();
                Assert.Equal(12, project.TryCountMax);
                Assert.Equal(1, saves);

                var path = FindEditor<TextBox>(window, nameof(ViewResultManagerConfig.CsvSavePath));
                string oldPath = results.CsvSavePath;
                path.Focus();
                path.Text = @"C:\Exports\bad?path";
                Pump();
                Assert.Equal(oldPath, results.CsvSavePath);
                ((TextBox)window.FindName("SearchBox")).Focus();
                Pump();
                Assert.True(Validation.GetHasError(path));
                Assert.Equal(oldPath, results.CsvSavePath);
                path.Focus();
                path.Text = @"C:\Exports\NewFolder";
                path.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Enter)
                    { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                Pump();
                Assert.Equal(@"C:\Exports\NewFolder", results.CsvSavePath);
                Assert.Equal(2, saves);
                attempts.Focus();
                attempts.Text = "14";
                window.Close();
                Assert.Equal(14, project.TryCountMax);
                Assert.Equal(3, saves);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void AutoSaveFailureCanBeRetriedAndRepeatedToggleChangesArePersisted()
    {
        RunUi(true, () =>
        {
            var results = new ViewResultManagerConfig();
            bool fail = true;
            bool discard = false;
            int saves = 0;
            int warnings = 0;
            var window = new ProjectSettingsWindow(new(new(), results, true), ProjectSettingsPage.Results,
                () => { saves++; if (fail) throw new IOException("test failure"); }, () => { warnings++; return discard; })
                { ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000 };
            try
            {
                window.Show();
                Pump();
                var csv = FindEditor<ToggleButton>(window, nameof(ViewResultManagerConfig.IsSaveCsv));
                csv.IsChecked = false;
                Pump();
                Assert.True(results.IsSaveCsv);
                Assert.Equal(1, saves);
                Assert.Contains("自动保存失败", ((TextBlock)window.FindName("StatusText")).Text);
                Assert.Equal(Visibility.Visible, ((Button)window.FindName("RetryButton")).Visibility);
                window.Close();
                Assert.True(window.IsVisible);
                Assert.Equal(1, warnings);
                fail = false;
                ((Button)window.FindName("RetryButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump();
                Assert.False(results.IsSaveCsv);
                Assert.Equal(Visibility.Collapsed, ((TextBlock)window.FindName("StatusText")).Visibility);
                csv.IsChecked = true;
                Pump();
                Assert.True(results.IsSaveCsv);
                Assert.Equal(3, saves);
                window.Close();
                Assert.Equal(1, warnings);
            }
            finally { discard = true; window.Close(); }
        });
    }

    [Fact]
    public void FailedDefaultsRemainUnappliedAndRetryAsOneSave()
    {
        RunUi(true, () =>
        {
            var project = new ProjectARVRProConfig { TryCountMax = 9 };
            var results = new ViewResultManagerConfig { IsSaveCsv = false, IsSaveSourceImage = true };
            bool fail = true;
            int saves = 0;
            var window = CreateWindow(new(project, results, true), ProjectSettingsPage.Testing, 840,
                () => { saves++; if (fail) throw new IOException("test failure"); });
            try
            {
                window.Show();
                Pump();
                Assert.False(window.RestoreDefaults());
                Pump();
                Assert.Equal(9, project.TryCountMax);
                Assert.False(results.IsSaveCsv);
                Assert.True(results.IsSaveSourceImage);
                Assert.Equal(1, saves);
                Assert.Contains("恢复默认设置失败", ((TextBlock)window.FindName("StatusText")).Text);
                fail = false;
                ((Button)window.FindName("RetryButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump();
                Assert.Equal(2, project.TryCountMax);
                Assert.True(results.IsSaveCsv);
                Assert.False(results.IsSaveSourceImage);
                Assert.Equal(2, saves);
                Assert.Equal(Visibility.Collapsed, ((TextBlock)window.FindName("StatusText")).Visibility);
            }
            finally { window.Close(); }
        });
    }

    private static ProjectSettingsWindow CreateWindow(ProjectSettingsSession session, ProjectSettingsPage initial, double width, Action persist) =>
        new(session, initial, persist, () => true) { Width = width, ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000 };

    private static T FindEditor<T>(Window window, string property) where T : FrameworkElement =>
        Descendants<T>((DependencyObject)window.FindName("SettingsContent")).First(control =>
            BindingOperations.GetBindingExpression(control, control is TextBox ? TextBox.TextProperty
                : control is ComboBox ? Selector.SelectedValueProperty : ToggleButton.IsCheckedProperty)?.ParentBinding.Path.Path == property);

    private static IEnumerable<T> Descendants<T>(DependencyObject node) where T : DependencyObject
    {
        if (node is T match) yield return match;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            foreach (var child in Descendants<T>(VisualTreeHelper.GetChild(node, i))) yield return child;
    }

    private static void RunUi(bool dark, Action action) => ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() =>
    {
        Theme previous = ThemeManager.Current.CurrentUITheme;
        // The shared image tests install placeholder styles. Use the real product styles for this window.
        string[] keys = ["TextBox.Small", "ComboBox.Small"];
        var previousStyles = keys.ToDictionary(key => key, key => Application.Current.Resources[key]);
        try
        {
            foreach (string key in keys) Application.Current.Resources.Remove(key);
            ThemeManager.Current.ApplyThemeChanged(Application.Current, dark ? Theme.Light : Theme.Dark);
            ThemeManager.Current.ApplyThemeChanged(Application.Current, dark ? Theme.Dark : Theme.Light);
            action();
        }
        finally
        {
            foreach (var (key, value) in previousStyles) Application.Current.Resources[key] = value;
            ThemeManager.Current.ApplyThemeChanged(Application.Current, previous == Theme.Dark ? Theme.Light : Theme.Dark);
            ThemeManager.Current.ApplyThemeChanged(Application.Current, previous);
        }
    });

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }
}
