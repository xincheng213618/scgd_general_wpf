using ColorVision.Themes;
using ColorVision.UI.Controls;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ProjectARVRPro.Tests;

[CollectionDefinition(nameof(FlowExecutionStatusTestGroup), DisableParallelization = true)]
public sealed class FlowExecutionStatusTestGroup { }

[Collection(nameof(FlowExecutionStatusTestGroup))]
public sealed class FlowExecutionStatusTests
{
    [Fact]
    public void LongFailureKeepsFullDiagnosticTextWhenSummaryIsFlattened()
    {
        const string reason = "十字图像文件不存在。\r\n请检查目录：C:\\images\\cross\\sample.tif";
        var status = FlowExecutionStatusInfo.Finished("flow2", "Failed", reason, null);
        Assert.DoesNotContain('\n', status.Message);
        Assert.Contains(reason, status.Details);
        Assert.Contains("flow2", status.Details);
        Assert.Empty(status.ElapsedText); // Preparation failures must not show the previous run's time.
    }

    [Fact]
    public void CompletedFlowDoesNotPresentItsPayloadAsAnErrorOrClaimInspectionPass()
    {
        var status = FlowExecutionStatusInfo.Finished("flow2", "Completed", "{\"result\":false}", 1234);
        Assert.Equal("流程执行完成", status.Message);
        Assert.Equal("用时 1234 ms", status.ElapsedText);
        Assert.Contains("{\"result\":false}", status.Details);
        Assert.Equal(FlowExecutionStatusKind.Completed, status.Kind);
    }

    [Fact]
    public void ElapsedTimeKeepsTotalMillisecondsAndDoesNotProduceNegativeRemainingEstimate()
    {
        var status = FlowExecutionStatusInfo.Running("flow2", "L/BV相机", 3_665_800, 60000);
        Assert.Equal("已用 3665800 ms", status.ElapsedText);
        Assert.DoesNotContain("预计剩余", status.Details);
        Assert.Contains("上次执行", status.Details);
    }

    [Fact]
    public void AuxiliaryMessagesKeepTheirRawContentWithoutChangingTheFlowOutcome()
    {
        const string reply = "N: returned\r\nraw MES response";
        var completed = FlowExecutionStatusInfo.Finished("KB", "Completed", null, 1200);
        var withReply = completed.WithAdditionalMessage("MES 返回", reply);
        Assert.Equal(completed.Kind, withReply.Kind);
        Assert.Equal(completed.ElapsedText, withReply.ElapsedText);
        Assert.Contains(reply, withReply.Details);
        Assert.DoesNotContain('\n', withReply.Message);
        var notice = FlowExecutionStatusInfo.Notice("服务重启失败\n请重试", isError: true);
        Assert.Empty(notice.ElapsedText);
        Assert.Equal("服务重启失败\n请重试", notice.Details);
    }

    [Theory]
    [InlineData(640, true)]
    [InlineData(640, false)]
    [InlineData(360, true)]
    public void StatusRowsStayCompactAndDetailsRemainAccessible(double width, bool dark)
    {
        RunUi(dark, () =>
        {
            var root = new StackPanel { Margin = new Thickness(16) };
            root.Children.Add(new TextBlock { Text = "执行状态 · 紧凑布局", FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 16) });
            FlowExecutionStatusInfo[] states =
            [
                FlowExecutionStatusInfo.Idle,
                FlowExecutionStatusInfo.Running("Black_Test_1", "L/BV相机, 亮度计算", 10518, 25000),
                FlowExecutionStatusInfo.Finished("flow2", "Failed", "十字图像文件不存在。", 10518),
                FlowExecutionStatusInfo.Finished("flow2", "Failed", "十字图像文件不存在，请检查采集目录、图像文件名以及当前流程对应的模板配置。\n完整路径：C:\\images\\cross\\sample.tif", 10518),
                FlowExecutionStatusInfo.Finished("flow2", "Completed", null, 12345),
                FlowExecutionStatusInfo.Finished("flow2", "OverTime", "等待相机响应超时", 60000),
            ];
            var controls = states.Select(status => new FlowExecutionStatus { Status = status, Margin = new Thickness(0, 0, 0, 12) }).ToArray();
            foreach (var control in controls) root.Children.Add(control);
            var window = new Window { Content = root, Width = width + 32, SizeToContent = SizeToContent.Height, ShowInTaskbar = false, WindowStyle = WindowStyle.None, Left = -10000, Top = -10000 };
            window.Resources = CreateTheme(dark);
            window.SetResourceReference(Window.BackgroundProperty, "GlobalBackground");
            window.SetResourceReference(Window.ForegroundProperty, "GlobalTextBrush");
            try
            {
                window.Show();
                Pump();
                foreach (var control in controls)
                {
                    Assert.InRange(control.ActualHeight, 36, 60);
                    var message = (TextBlock)control.FindName("MessageText");
                    var button = (Button)control.FindName("DetailsButton");
                    var label = (TextBlock)control.FindName("StateLabel");
                    // TextBlock.BaselineOffset describes the last line for wrapped text.
                    // Keep both first line boxes aligned, and compare baselines for single-line text.
                    Assert.Equal(label.TranslatePoint(new Point(), control).Y, message.TranslatePoint(new Point(), control).Y);
                    if (message.ActualHeight == label.ActualHeight)
                        Assert.Equal(label.BaselineOffset, message.BaselineOffset);
                    Assert.True(message.ActualWidth > 100);
                    Assert.True(message.TranslatePoint(new Point(message.ActualWidth, 0), control).X <= button.TranslatePoint(new Point(), control).X);
                    Assert.True(button.TranslatePoint(new Point(button.ActualWidth, 0), control).X <= control.ActualWidth);
                    Assert.Equal(width < 440 ? Visibility.Collapsed : Visibility.Visible, ((TextBlock)control.FindName("ElapsedText")).Visibility);
                }
                // Compare the two layouts instead of depending on physical-pixel rounding at the host DPI.
                Assert.True(controls[1].ActualHeight < controls[3].ActualHeight);
                Capture(window, $"status-{width}-{(dark ? "dark" : "light")}.png");
                var failure = controls[3];
                var detailsButton = (Button)failure.FindName("DetailsButton");
                detailsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump();
                var popup = (Popup)failure.FindName("DetailsPopup");
                var details = (TextBox)failure.FindName("DetailsText");
                Assert.True(popup.IsOpen);
                Assert.Equal(states[3].Details, details.Text);
                Assert.True(details.IsReadOnly);
                Capture((FrameworkElement)failure.FindName("DetailsSurface"), $"details-{width}-{(dark ? "dark" : "light")}.png");
                var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(details)!, 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                details.RaiseEvent(key);
                Assert.False(popup.IsOpen);
                Assert.True(key.Handled);
            }
            finally { window.Close(); }
        });
    }

    private static ResourceDictionary CreateTheme(bool dark)
    {
        var resources = new ResourceDictionary();
        foreach (var source in (dark ? ThemeManager.ResourceDictionaryDark : ThemeManager.ResourceDictionaryWhite).Concat(ThemeManager.ResourceDictionaryBase))
            resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(source, UriKind.RelativeOrAbsolute) });
        return resources;
    }

    private static void RunUi(bool dark, Action action)
    {
        // Use the project's existing WPF host; theme brushes belong to its dispatcher.
        ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() =>
        {
            var previous = Application.Current.Resources;
            try { Application.Current.Resources = CreateTheme(dark); action(); }
            finally { Application.Current.Resources = previous; }
        });
    }
    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

    private static void Capture(FrameworkElement element, string name)
    {
        string? directory = Environment.GetEnvironmentVariable("ARVR_STATUS_PREVIEW_OUTPUT");
        if (string.IsNullOrWhiteSpace(directory)) return;
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
