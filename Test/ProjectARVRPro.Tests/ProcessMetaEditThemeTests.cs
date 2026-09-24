using ColorVision.Themes;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.KeyedResults.LuminanceChromaticity;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ProjectARVRPro.Tests;

[Collection(nameof(FlowExecutionStatusTestGroup))]
public sealed class ProcessMetaEditThemeTests
{
    [Fact]
    public void ProcessTypeEditorKeepsListAndConfigurationTextReadableWhenThemeChanges()
    {
        ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() =>
        {
            var previousTheme = ThemeManager.Current.CurrentUITheme;
            ThemeManager.Current.ApplyTheme(Application.Current, Theme.Dark);
            var process = new LuminanceChromaticityProcess();
            var window = new ProcessMetaEditWindow(
                [], [process], "修改处理类型", process: process,
                isEdit: true, editTarget: ProcessMetaEditTarget.Process)
            {
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Left = -10000,
                Top = -10000
            };

            try
            {
                window.Show();
                Pump();
                AssertThemeText(window);

                ThemeManager.Current.ApplyTheme(Application.Current, Theme.Light);
                Pump();
                AssertThemeText(window);
            }
            finally
            {
                window.Close();
                ThemeManager.Current.ApplyTheme(Application.Current, previousTheme);
            }
        });
    }

    private static void AssertThemeText(ProcessMetaEditWindow window)
    {
        var expected = Assert.IsType<SolidColorBrush>(window.FindResource("GlobalTextBrush")).Color;
        var categories = Descendants<ListBox>(window).Single(list => list.Items.Cast<object>().Contains(ProcessTypeCatalog.ArvrCategory));
        var category = Assert.IsType<ListBoxItem>(categories.ItemContainerGenerator.ContainerFromIndex(0));
        Assert.Equal(expected, Assert.IsType<SolidColorBrush>(category.Foreground).Color);

        var types = Descendants<ListBox>(window).Single(list => list.ItemsSource?.Cast<object>().FirstOrDefault() is ProcessTypeOption);
        var card = Assert.IsType<ListBoxItem>(types.ItemContainerGenerator.ContainerFromIndex(0));
        Assert.Equal(expected, Assert.IsType<SolidColorBrush>(card.Foreground).Color);
        Assert.Equal(expected, Assert.IsType<SolidColorBrush>(Descendants<TextBlock>(card).Single(text => text.Text == "亮色度").Foreground).Color);

        foreach (var label in new[] { "处理配置", "输出Key", "Center解析Key", "显示内容" })
        {
            var text = Descendants<TextBlock>(window).Single(item => item.Text == label);
            Assert.Equal(expected, Assert.IsType<SolidColorBrush>(text.Foreground).Color);
        }

        var displayLabel = Descendants<TextBlock>(window).Single(item => item.Text == "显示内容");
        var displayField = Assert.IsType<DockPanel>(VisualTreeHelper.GetParent(displayLabel));
        var displaySummary = displayField.Children.OfType<TextBlock>().Single(item => item != displayLabel);
        Assert.Equal(expected, Assert.IsType<SolidColorBrush>(displaySummary.Foreground).Color);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T value) yield return value;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }
}
