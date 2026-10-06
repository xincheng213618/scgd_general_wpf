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
    [InlineData(840, true)]
    [InlineData(760, false)]
    public void NavigationSearchAndConditionalEditorsKeepAutomaticallySavedValues(double width, bool dark)
    {
        RunUi(dark, () =>
        {
            var project = new ProjectARVRProConfig();
            var results = new ViewResultManagerConfig();
            var session = new ProjectSettingsSession(project, results, true);
            int saves = 0;
            var window = CreateWindow(session, ProjectSettingsPage.Results, width, () => saves++);
            try
            {
                window.Show();
                Pump();
                Assert.Equal(0, saves);
                Assert.Null(window.FindName("SaveButton"));
                Assert.Null(window.FindName("CancelButton"));
                Assert.Equal(Visibility.Collapsed, ((TextBlock)window.FindName("StatusText")).Visibility);
                var list = (ListBox)window.FindName("SettingsList");
                Assert.Equal(ProjectSettingsPage.Results, ((ProjectSettingsSection)list.SelectedItem).Id);
                Assert.Equal(4, list.Items.Count);
                var content = (StackPanel)window.FindName("SettingsContent");
                Assert.Equal(4, content.Children.Count);
                Assert.All(content.Children.Cast<FrameworkElement>(), section => Assert.Equal(Visibility.Visible, section.Visibility));
                Assert.Empty(Descendants<Expander>(window));
                Assert.True(FindEditor<ToggleButton>(window, nameof(ViewResultManagerConfig.AutoCleanupEnabled)).IsVisible);
                Assert.False(FindEditor<TextBox>(window, nameof(ViewResultManagerConfig.MinimumFreeSpaceGB)).IsVisible);
                FindEditor<ToggleButton>(window, nameof(ViewResultManagerConfig.AutoCleanupEnabled)).IsChecked = true;
                Pump();
                Assert.True(results.AutoCleanupEnabled);
                var reserve = FindEditor<TextBox>(window, nameof(ViewResultManagerConfig.MinimumFreeSpaceGB));
                Assert.True(reserve.IsVisible);
                reserve.Text = "80";
                reserve.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                Pump();
                Assert.Equal(80, results.MinimumFreeSpaceGB);
                Capture(window, $"settings-Results-storage-{width}-{(dark ? "dark" : "light")}.png");
                Assert.True(FindEditor<TextBox>(window, nameof(ViewResultManagerConfig.CodeDateFormat)).IsVisible);
                Assert.True(FindEditor<ToggleButton>(window, nameof(ViewResultManagerConfig.UseLegacyARVROutput)).IsVisible);
                Assert.True(FindEditor<ToggleButton>(window, nameof(ViewResultManagerConfig.IsSaveCustomXlsx)).IsVisible);
                FindEditor<ToggleButton>(window, nameof(ViewResultManagerConfig.IsSaveCustomXlsx)).IsChecked = true;
                Pump();
                Assert.True(FindEditor<ComboBox>(window, nameof(ViewResultManagerConfig.CustomOutputProfile)).IsVisible);
                Capture(window, $"settings-Results-customer-{width}-{(dark ? "dark" : "light")}.png");
                session.Results.IsSaveCustomXlsx = false;
                var search = (TextBox)window.FindName("SearchBox");
                search.Text = "CodeDateFormat";
                Pump();
                Assert.Single(content.Children.Cast<FrameworkElement>(), section => section.Visibility == Visibility.Visible);
                Assert.True(FindEditor<TextBox>(window, nameof(ViewResultManagerConfig.CodeDateFormat)).IsVisible);
                search.Text = "TIFF";
                Pump();
                Assert.Equal(ProjectSettingsPage.Images, ((ProjectSettingsSection)list.SelectedItem).Id);
                Assert.Single(list.Items.Cast<object>());
                Assert.False(FindEditor<ComboBox>(window, nameof(ViewResultManagerConfig.SourceExportFormatWithBmp)).IsVisible);
                session.Results.IsSaveSourceImage = true;
                session.Results.IsSaveImageReuslt = true;
                Pump();
                Assert.True(FindEditor<ComboBox>(window, nameof(ViewResultManagerConfig.SourceExportFormatWithBmp)).IsVisible);
                FindEditor<ComboBox>(window, nameof(ViewResultManagerConfig.SourceExportFormatWithBmp)).SelectedValue = SourceImageFormat.BMP;
                Pump();
                Assert.Equal(SourceImageFormat.BMP, session.Results.SourceExportFormat);
                Assert.False(FindEditor<ComboBox>(window, nameof(ViewResultManagerConfig.SourceTiffCompressionMode)).IsVisible);
                search.Clear();
                foreach (ProjectSettingsSection section in session.Sections)
                {
                    list.SelectedItem = section;
                    Pump();
                    foreach (var textBox in Descendants<TextBox>((DependencyObject)window.FindName("SettingsContent")).Where(control => control.IsVisible))
                    {
                        Assert.True(textBox.ActualWidth > 0);
                        Point right = textBox.TranslatePoint(new Point(textBox.ActualWidth, 0), window);
                        Assert.True(right.X <= window.ActualWidth, $"Clipped input in {section.Title}");
                    }
                    Capture(window, $"settings-{section.Id}-{width}-{(dark ? "dark" : "light")}.png");
                }
                search.Text = "missing-setting-123";
                Pump();
                Assert.Equal(Visibility.Visible, ((FrameworkElement)window.FindName("EmptyState")).Visibility);
                Assert.Empty(list.Items);
                search.Clear();
                Assert.Equal(4, list.Items.Count);
                ((Button)window.FindName("CloseButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.True(results.IsSaveSourceImage);
                Assert.True(results.IsSaveImageReuslt);
                Assert.Equal(SourceImageFormat.BMP, results.SourceExportFormat);
                Assert.True(saves > 0);
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
    public void NumberingRulesStayExpandedAndInvalidFormatDoesNotBlockOtherAutoSaves()
    {
        RunUi(true, () =>
        {
            var results = new ViewResultManagerConfig();
            var session = new ProjectSettingsSession(new(), results, false);
            var window = CreateWindow(session, ProjectSettingsPage.Results, 840, () => { });
            try
            {
                window.Show();
                Pump();
                Assert.Empty(Descendants<Expander>(window));
                var format = FindEditor<TextBox>(window, nameof(ViewResultManagerConfig.CodeDateFormat));
                format.Text = "%";
                format.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                var list = (ListBox)window.FindName("SettingsList");
                list.SelectedIndex = 0;
                session.Results.IsSaveCsv = false;
                Pump();
                Assert.True(Validation.GetHasError(format));
                Assert.NotEqual("%", results.CodeDateFormat);
                Assert.False(results.IsSaveCsv);
                Assert.Equal(ProjectSettingsPage.Testing, ((ProjectSettingsSection)list.SelectedItem).Id);
                Assert.True(format.IsVisible);
                format.Text = "yyyyMMdd";
                format.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                Pump();
                Assert.Equal("yyyyMMdd", results.CodeDateFormat);
                Assert.Equal(Visibility.Collapsed, ((TextBlock)window.FindName("StatusText")).Visibility);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void ConnectionSettingsCanBeLoadedAndSavedWithoutConnectingHardware()
    {
        RunUi(true, () =>
        {
            var project = new ProjectARVRProConfig { ThunderbirdPortName = "COM19", ThunderbirdBaudRate = 57600,
                ThunderbirdTimeoutMs = 2000, ThunderbirdAutoConnect = true };
            int saves = 0;
            var window = new ThunderbirdSerialDebugWindow(project, () => saves++, false);
            try
            {
                window.ShowInTaskbar = false;
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = -10000;
                window.Top = -10000;
                window.Show();
                Pump();
                Capture(window, "connection-settings-dark.png");
                Assert.Equal("COM19", ((ComboBox)window.FindName("ComPortComboBox")).Text);
                Assert.Equal("57600", ((ComboBox)window.FindName("BaudRateComboBox")).Text);
                Assert.True(((CheckBox)window.FindName("AutoConnectCheckBox")).IsChecked);
                Assert.Equal(0, saves);
                var timeout = (TextBox)window.FindName("TimeoutTextBox");
                timeout.Text = "invalid";
                Assert.False(window.SaveThunderbirdConfig());
                Assert.Equal(2000, project.ThunderbirdTimeoutMs);
                Assert.Equal(0, saves);
                timeout.Text = "0";
                Assert.True(window.SaveThunderbirdConfig());
                Assert.Equal(1, saves);
                Assert.Equal(0, project.ThunderbirdTimeoutMs);
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

    [Theory]
    [InlineData(840, 560, true)]
    [InlineData(760, 440, false)]
    public void ContinuousSectionsScrollAndLeftAnchorsTrackWithoutReplacingEditors(double width, double height, bool dark)
    {
        RunUi(dark, () =>
        {
            var session = new ProjectSettingsSession(new(), new(), true);
            var window = CreateWindow(session, ProjectSettingsPage.Testing, width, () => { });
            window.Height = height;
            try
            {
                window.Show();
                Pump();
                var content = (StackPanel)window.FindName("SettingsContent");
                var scroll = (ScrollViewer)window.FindName("PageScroll");
                var list = (ListBox)window.FindName("SettingsList");
                var attempts = FindEditor<TextBox>(window, nameof(ProjectARVRProConfig.TryCountMax));
                var sections = content.Children.Cast<FrameworkElement>().ToArray();
                Assert.All(sections, section => Assert.True(section.IsVisible));
                Assert.DoesNotContain(Descendants<TextBlock>(window), text => text.Text == "快速定位");
                Assert.True(list.TranslatePoint(new Point(list.ActualWidth, 0), window).X < scroll.TranslatePoint(new Point(), window).X);
                for (int i = 1; i < sections.Length; i++)
                    Assert.True(sections[i].TranslatePoint(new Point(), content).Y >= sections[i - 1].TranslatePoint(new Point(0, sections[i - 1].ActualHeight), content).Y);
                var search = (TextBox)window.FindName("SearchBox");
                Assert.Equal(list.TranslatePoint(new Point(), window).X, search.TranslatePoint(new Point(), window).X);
                Assert.True(search.TranslatePoint(new Point(search.ActualWidth, 0), window).X < scroll.TranslatePoint(new Point(), window).X);
                Assert.True(scroll.TranslatePoint(new Point(), window).Y < search.TranslatePoint(new Point(0, search.ActualHeight), window).Y);
                Point footer = ((Button)window.FindName("CloseButton")).TranslatePoint(new Point(), window);

                list.SelectedItem = session.Sections[2];
                Pump();
                Assert.Equal(ProjectSettingsPage.Results, ((ProjectSettingsSection)list.SelectedItem).Id);
                Assert.Equal(Math.Min(sections[2].TranslatePoint(new Point(), content).Y, scroll.ScrollableHeight), scroll.VerticalOffset, 1);
                Assert.Same(attempts, FindEditor<TextBox>(window, nameof(ProjectARVRProConfig.TryCountMax)));
                Assert.All(sections, section => Assert.Equal(Visibility.Visible, section.Visibility));
                Capture(window, $"settings-continuous-results-{width}-{(dark ? "dark" : "light")}.png");

                scroll.ScrollToVerticalOffset(sections[1].TranslatePoint(new Point(), content).Y + 2);
                Pump();
                Assert.Equal(ProjectSettingsPage.Display, ((ProjectSettingsSection)list.SelectedItem).Id);
                scroll.ScrollToBottom();
                Pump();
                Assert.Equal(ProjectSettingsPage.Images, ((ProjectSettingsSection)list.SelectedItem).Id);
                scroll.ScrollToTop();
                Pump();
                Assert.Equal(ProjectSettingsPage.Testing, ((ProjectSettingsSection)list.SelectedItem).Id);
                Assert.Equal(footer, ((Button)window.FindName("CloseButton")).TranslatePoint(new Point(), window));
                Capture(window, $"settings-continuous-start-{width}-{(dark ? "dark" : "light")}.png");
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void ScrollingTransfersOldNavigationFocusButKeepsEditorFocus()
    {
        RunUi(true, () =>
        {
            var session = new ProjectSettingsSession(new(), new(), true);
            var window = CreateWindow(session, ProjectSettingsPage.Testing, 840, () => { });
            try
            {
                window.Show();
                Pump();
                var list = (ListBox)window.FindName("SettingsList");
                var scroll = (ScrollViewer)window.FindName("PageScroll");
                var displayItem = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(1);
                list.SelectedItem = session.Sections[1];
                displayItem.Focus();
                Pump();
                Assert.True(displayItem.IsKeyboardFocused);
                Assert.Equal(ProjectSettingsPage.Display, ((ProjectSettingsSection)list.SelectedItem).Id);

                scroll.ScrollToBottom();
                Pump();
                Assert.Equal(ProjectSettingsPage.Images, ((ProjectSettingsSection)list.SelectedItem).Id);
                Assert.False(displayItem.IsKeyboardFocused, $"Scroll focusable: {scroll.Focusable}; scroll focused: {scroll.IsKeyboardFocused}");
                Assert.False(list.IsKeyboardFocusWithin);
                Capture(window, "settings-scroll-focus-dark.png");

                var format = FindEditor<TextBox>(window, nameof(ViewResultManagerConfig.CodeDateFormat));
                format.Focus();
                Pump();
                Assert.True(format.IsKeyboardFocused);
                scroll.ScrollToTop();
                Pump();
                Assert.Equal(ProjectSettingsPage.Testing, ((ProjectSettingsSection)list.SelectedItem).Id);
                Assert.True(format.IsKeyboardFocused);

                // Returning to the directory with the keyboard still has a visible focus cue.
                list.SelectedItem = session.Sections[1];
                displayItem.Focus();
                Pump();
                Assert.True(displayItem.IsKeyboardFocused);
                Assert.Equal(ProjectSettingsPage.Display, ((ProjectSettingsSection)list.SelectedItem).Id);
                var surface = (Border)displayItem.Template.FindName("Surface", displayItem);
                Assert.Equal(window.FindResource("PrimaryBrush"), surface.BorderBrush);
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
    public void DefaultsSaveTogetherAndLeaveConnectionAndRuntimeStateUntouched()
    {
        RunUi(true, () =>
        {
            var project = new ProjectARVRProConfig { TryCountMax = 9, ThunderbirdPortName = "COM19", StepIndex = 3 };
            var results = new ViewResultManagerConfig { IsSaveCsv = false, IsSaveSourceImage = true, Height = 500 };
            int saves = 0;
            var window = CreateWindow(new(project, results, true), ProjectSettingsPage.Testing, 840, () => saves++);
            try
            {
                window.Show();
                Pump();
                FindEditor<TextBox>(window, nameof(ProjectARVRProConfig.TryCountMax)).Text = "invalid";
                Assert.True(window.RestoreDefaults());
                Pump();
                Assert.Equal(1, saves);
                Assert.Equal(2, project.TryCountMax);
                Assert.True(results.IsSaveCsv);
                Assert.False(results.IsSaveSourceImage);
                Assert.Equal("COM19", project.ThunderbirdPortName);
                Assert.Equal(3, project.StepIndex);
                Assert.Equal(500, results.Height);
                Assert.Equal(Visibility.Collapsed, ((TextBlock)window.FindName("StatusText")).Visibility);
            }
            finally { window.Close(); }
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

    private static void Capture(FrameworkElement element, string name)
    {
        string? directory = Environment.GetEnvironmentVariable("ARVR_SETTINGS_PREVIEW_OUTPUT");
        if (string.IsNullOrWhiteSpace(directory)) return;
        // Preview only: let toggle presentation animations settle; assertions never depend on this delay.
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
        Directory.CreateDirectory(directory);
        var bounds = new Rect(0, 0, element.ActualWidth, element.ActualHeight);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle((Brush)element.FindResource("GlobalBackground"), null, bounds);
            drawing.DrawRectangle(new VisualBrush(element) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = bounds }, null, bounds);
        }
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(bounds.Width * 1.5), (int)Math.Ceiling(bounds.Height * 1.5), 144, 144, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, name));
        encoder.Save(stream);
    }
}
