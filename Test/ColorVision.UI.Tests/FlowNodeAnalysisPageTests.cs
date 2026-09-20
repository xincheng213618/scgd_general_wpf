using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Themes;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public class FlowNodeAnalysisPageTests
{
    [Theory]
    [InlineData(true, 1100, 570)]
    [InlineData(false, 1100, 570)]
    [InlineData(true, 1480, 780)]
    [InlineData(false, 1480, 780)]
    public void NodeDetailsRespectThemeAndRemainUsableAtMinimumWindowSize(bool dark, int width, int height)
    {
        WpfTestHost.Invoke(() =>
        {
            var previous = ThemeManager.Current.CurrentUITheme;
            ThemeManager.Current.ApplyTheme(Application.Current, dark ? Theme.Dark : Theme.Light);
            try
            {
                FlowNodeAnalysisPage page = CreatePage();
                Layout(page, width, height);
                // The disconnected page renders without Loaded, which would query the user's history DB.
                Assert.False(page.IsLoaded);
                var expected = (SolidColorBrush)page.FindResource("GlobalTextBrush");
                Assert.Equal(expected.Color, Assert.IsType<SolidColorBrush>(Find<TextBlock>(page, "NodeElapsedText").Foreground).Color);
                Assert.Equal(expected.Color, Assert.IsType<SolidColorBrush>(Find<TextBlock>(page, "NodeShareText").Foreground).Color);
                Assert.Equal(expected.Color, Assert.IsType<SolidColorBrush>(Find<TextBox>(page, "SendPayloadTextBox").Foreground).Color);
                Assert.InRange(Find<ListView>(page, "MessageListView").ActualHeight, 30, 100);
                AssertInside(page, Find<ListView>(page, "NodeHistoryListView"));
                Assert.Contains("OpenImage", Find<TextBox>(page, "RecvPayloadTextBox").Text);
                Assert.Equal(VerticalAlignment.Top, Find<TextBox>(page, "RecvPayloadTextBox").VerticalContentAlignment);
                AssertInside(page, Find<TextBox>(page, "RecvPayloadTextBox"));
                SavePreview(page, dark, width, height, "payload");

                // Selecting a legacy message must replace the previous stage log.
                Find<ListView>(page, "MessageListView").ItemsSource = new[] { new FlowNodeMessage { RecvTopic = "service", RecvPayload = "{\"result\":1}" } };
                Find<ListView>(page, "MessageListView").SelectedIndex = 0;
                Assert.DoesNotContain("OpenImage", Find<TextBox>(page, "RecvPayloadTextBox").Text);
                Assert.Contains("result", Find<TextBox>(page, "RecvPayloadTextBox").Text);
            }
            finally { ThemeManager.Current.ApplyTheme(Application.Current, previous); }
        });
    }

    private static FlowNodeAnalysisPage CreatePage()
    {
        var message = new FlowNodeMessage
        {
            NodeRecordId = 1, BatchId = 75, NodeId = "camera-node", SerialNumber = "sample-run",
            EventName = "GetData", State = FlowMessageState.Success, StatusMessage = "Finish",
            SendTopic = "LOCAL", RecvTopic = "LOCAL",
            SendTime = new DateTime(2026, 9, 16, 1, 17, 29, 57),
            RecvTime = new DateTime(2026, 9, 16, 1, 17, 30, 336),
            SendPayload = "{\"FileUrl\":\"sample.png\"}",
            RecvPayload = "{\"Timing\":{\"Version\":1,\"Unit\":\"ms\",\"TotalMs\":1279,\"Stages\":[{\"Name\":\"OpenImage\",\"ElapsedMs\":113,\"Status\":\"Completed\"}]}}"
        };
        var record = new FlowNodeRecord
        {
            Id = 1, BatchId = 75, NodeId = message.NodeId, NodeName = "相机取图", NodeType = "Camera",
            SerialNumber = message.SerialNumber, StartTime = message.SendTime, EndTime = message.RecvTime, ElapsedMs = message.ElapsedMs
        };
        var session = new FlowExecutionAnalysisSession(75, message.SerialNumber, null, null, [record], [message], [], message.RecvTime!.Value, 3000);
        var page = new FlowNodeAnalysisPage(session, record, true, _ => { }, _ => { }, _ => { }, (_, _) => { }, () => { }, _ => { });
        Find<ListView>(page, "NodeHistoryListView").ItemsSource = FlowExecutionAnalysisPresentation.BuildNodeHistoryItems([record], [message], session.CapturedAt);
        Find<TextBlock>(page, "NodeHistoryHintText").Text = "最近 1 次执行 · 单击一行切换";
        Find<TextBlock>(page, "NodeHistorySuccessAverageText").Text = "1.28 s";
        Find<TextBlock>(page, "NodeHistorySuccessP95Text").Text = "P95 1.28 s";
        Find<TextBlock>(page, "NodeHistoryFailureAverageText").Text = "—";
        Find<TextBlock>(page, "NodeHistorySuccessCountText").Text = "1";
        return page;
    }

    private static T Find<T>(FrameworkElement page, string name) where T : FrameworkElement => (T)page.FindName(name);

    private static void Layout(FrameworkElement page, int width, int height)
    {
        page.Measure(new Size(width, height));
        page.Arrange(new Rect(0, 0, width, height));
        page.UpdateLayout();
    }

    private static void AssertInside(FrameworkElement page, FrameworkElement child)
    {
        Rect bounds = child.TransformToAncestor(page).TransformBounds(new Rect(child.RenderSize));
        Assert.True(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= page.ActualWidth && bounds.Bottom <= page.ActualHeight, $"{child.Name}: {bounds}, page: {page.RenderSize}");
    }

    private static void SavePreview(FrameworkElement page, bool dark, int width, int height, string detail)
    {
        string? directory = Environment.GetEnvironmentVariable("COLORVISION_ANALYSIS_PREVIEW_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(page);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, $"analysis-{(dark ? "dark" : "light")}-{width}-{detail}.png"));
        encoder.Save(stream);
    }
}
