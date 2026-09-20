using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotWorkspaceDiffPresentationTests
{
    [Theory]
    [InlineData(false, 220)]
    [InlineData(true, 220)]
    [InlineData(true, 420)]
    public void RecoveryWarningStaysVisibleWithCollapsedDiffAndFitsNarrowWidths(bool hasDiff, double width)
    {
        StaTest.Run(() =>
        {
            try
            {
                var panel = new CopilotChatPanel();
                var message = new CopilotChatMessage(CopilotChatRole.Assistant, "文件操作失败");
                message.ApplyWorkspaceDiff(new(hasDiff ? "--- a/camera.json\n+++ b/camera.json\n-old\n+new" : string.Empty, hasDiff ? 1 : 0, false)
                {
                    VerificationWarning = "文件状态待核查 · 2 个文件\n文件操作未确认完成，已有差异可能不完整。请核查以下文件：\n• profiles/camera.json\n• " + new string('a', 70) + "/config.txt",
                });
                var presenter = new ContentPresenter { Content = message, ContentTemplate = (DataTemplate)panel.Resources["WorkspaceDiffTemplate"] };
                var host = new Border { Width = width, Padding = new Thickness(10), Background = Brushes.WhiteSmoke, Child = presenter };
                host.Resources.MergedDictionaries.Add(panel.Resources);
                host.Resources["GlobalTextBrush"] = Brushes.Black;
                host.Resources["GlobalBorderBrush"] = Brushes.White;
                Layout(host, width);
                var warning = Assert.Single(Visuals<Border>(presenter), border => border.Name == "WorkspaceDiffWarningBorder");
                Assert.Equal(Visibility.Visible, warning.Visibility);
                var text = Assert.IsType<TextBlock>(warning.Child);
                Assert.Equal(message.WorkspaceDiffWarning, text.Text);
                Assert.Equal(TextWrapping.Wrap, text.TextWrapping);
                Assert.True(text.ActualHeight > 20);
                var bounds = text.TransformToAncestor(host).TransformBounds(new Rect(text.RenderSize));
                Assert.InRange(bounds.Right, 1, width - 10);
                var expander = Assert.Single(Visuals<Expander>(presenter));
                Assert.False(expander.IsExpanded);
                Assert.Equal(hasDiff ? Visibility.Visible : Visibility.Collapsed, expander.Visibility);
                if (hasDiff) Assert.Contains("待核查", Assert.IsType<TextBlock>(Assert.IsType<Border>(expander.Header).Child).Text, StringComparison.Ordinal);

                var evidenceDirectory = Environment.GetEnvironmentVariable("COLORVISION_COPILOT_UI_EVIDENCE_DIRECTORY");
                if (!string.IsNullOrWhiteSpace(evidenceDirectory))
                {
                    Directory.CreateDirectory(evidenceDirectory);
                    var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width), (int)Math.Ceiling(host.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(host);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Path.Combine(evidenceDirectory, $"workspace-warning-{hasDiff}-{width:0}.png"));
                    encoder.Save(stream);
                }
                // Replacing the authoritative snapshot clears the warning and its layout space.
                message.ApplyWorkspaceDiff(new(string.Empty, 0, false));
                Layout(host, width);
                Assert.Equal(Visibility.Collapsed, warning.Visibility);
                Assert.Equal(Visibility.Collapsed, expander.Visibility);
            }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
    }

    private static void Layout(FrameworkElement host, double width)
    {
        host.Measure(new Size(width, double.PositiveInfinity));
        host.Arrange(new Rect(0, 0, width, host.DesiredSize.Height));
        host.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        host.UpdateLayout();
    }

    private static IEnumerable<T> Visuals<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var descendant in Visuals<T>(child)) yield return descendant;
        }
    }
}
