using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Cie;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class CieWindowCompositionTests
{
    [Theory]
    [InlineData(CieDiagramKind.Cie1931xy)]
    [InlineData(CieDiagramKind.Cie1960uv)]
    [InlineData(CieDiagramKind.Cie1976uv)]
    public void DiagramFitsAfterLayoutAndResizeButPreservesManualZoom(CieDiagramKind kind)
    {
        WpfTestHost.Invoke(() =>
        {
            var view = new CieDiagramView();
            var window = new Window { Content = view, Width = 860, Height = 680, ShowActivated = false, ShowInTaskbar = false };
            try
            {
                view.SetDiagram(kind);
                window.Show();
                Pump();
                AssertCenteredFit(view);
                window.Width = 720;
                window.Height = 520;
                Pump();
                AssertCenteredFit(view);
                view.Zoom(1.25);
                Matrix manual = view.ZoomBox.ContentMatrix;
                window.Width = 900;
                Pump();
                Assert.Equal(manual, view.ZoomBox.ContentMatrix);
                view.ZoomUniform();
                Pump();
                AssertCenteredFit(view);
            }
            finally { window.Close(); }
        });
    }

    [Theory]
    [InlineData(860, 680)]
    [InlineData(720, 520)]
    public void OptionsAreInitiallyCollapsedAndChartAvoidsTheOpenedPanel(double width, double height)
    {
        WpfTestHost.Invoke(() =>
        {
            var window = new WindowCIE { Width = width, Height = height, ShowActivated = false, ShowInTaskbar = false };
            try
            {
                window.Show();
                Pump();
                var panel = Assert.IsType<Border>(window.FindName("DisplayOptionsPanel"));
                Assert.False(panel.IsVisible);
                AssertCenteredFit(window.DiagramView);
                Assert.Single(window.DiagramView.Gamuts);
                Assert.Single(window.DiagramView.ReferenceMarkers);
                Assert.False(window.DiagramView.ShowCctReference);
                Assert.False(window.DiagramView.ShowDaylightReference);
                Assert.IsType<ToggleButton>(window.FindName("ShowDisplayOptions")).IsChecked = true;
                Pump();
                Assert.True(panel.IsVisible);
                Point plotRight = window.DiagramView.TranslatePoint(new Point(window.DiagramView.ActualWidth, 0), window);
                Point panelLeft = panel.TranslatePoint(new Point(), window);
                Assert.True(plotRight.X <= panelLeft.X);
                AssertCenteredFit(window.DiagramView);
                window.SetDiagram(CieDiagramKind.Cie1960uv);
                Pump();
                AssertCenteredFit(window.DiagramView);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void CalculationKeepsItsInputsAndManualViewWhilePanelsCanBeCollapsed()
    {
        WpfTestHost.Invoke(() =>
        {
            var window = new WindowCIE { ShowActivated = false, ShowInTaskbar = false };
            try
            {
                window.Show();
                var tabs = Assert.IsType<TabControl>(window.FindName("CieTabs"));
                tabs.SelectedIndex = 1;
                Pump();
                var calculation = Assert.IsType<ManualColorGamutView>(Assert.IsType<TabItem>(tabs.Items[1]).Content);
                var plot = Assert.IsType<CieDiagramView>(calculation.FindName("CieDiagram"));
                var results = Assert.IsType<DataGrid>(calculation.FindName("ResultGrid"));
                var area = Assert.IsType<TextBlock>(calculation.FindName("TextBlockSampleArea"));
                Assert.Single(results.Items);
                AssertCenteredFit(plot);
                string originalArea = area.Text;
                plot.Zoom(1.25);
                Matrix manual = plot.ZoomBox.ContentMatrix;
                var redX = Assert.IsType<TextBox>(calculation.FindName("TextBoxRedX"));
                redX.Text = "0.65";
                Pump();
                Assert.Single(results.Items);
                Assert.NotEqual(originalArea, area.Text);
                Assert.Equal(manual, plot.ZoomBox.ContentMatrix);
                Assert.True(Assert.IsType<Button>(calculation.FindName("ButtonExport")).IsEnabled);
                double originalWidth = plot.ActualWidth;
                double originalHeight = plot.ActualHeight;
                Assert.IsType<ToggleButton>(calculation.FindName("ShowParameters")).IsChecked = false;
                Assert.IsType<Expander>(calculation.FindName("ResultsExpander")).IsExpanded = false;
                Pump();
                Assert.True(plot.ActualWidth > originalWidth);
                Assert.True(plot.ActualHeight > originalHeight);
                Assert.Equal(manual, plot.ZoomBox.ContentMatrix);
                tabs.SelectedIndex = 0;
                Pump();
                tabs.SelectedIndex = 1;
                Pump();
                Assert.Equal("0.65", redX.Text);
                Assert.Equal(manual, plot.ZoomBox.ContentMatrix);
                plot.ZoomUniform();
                Pump();
                AssertCenteredFit(plot);
            }
            finally { window.Close(); }
        });
    }

    private static void AssertCenteredFit(CieDiagramView view)
    {
        Size content = view.DiagramCanvas.DesiredSize;
        var zoom = view.ZoomBox;
        Assert.True(content.Width > 0 && content.Height > 0);
        double scale = Math.Min(zoom.ActualWidth / content.Width, zoom.ActualHeight / content.Height);
        Assert.Equal(scale, zoom.ContentMatrix.M11, 6);
        Assert.Equal(scale, zoom.ContentMatrix.M22, 6);
        Assert.Equal((zoom.ActualWidth - content.Width * scale) / 2, zoom.ContentMatrix.OffsetX, 6);
        Assert.Equal((zoom.ActualHeight - content.Height * scale) / 2, zoom.ContentMatrix.OffsetY, 6);
    }

    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    [Fact]
    public void CieWindowHostsDiagramAndColorGamutCalculationTabs()
    {
        WpfTestHost.Invoke(() =>
        {
            WindowCIE window = new();
            try
            {
                TabControl tabs = Assert.IsType<TabControl>(window.FindName("CieTabs"));
                Assert.Equal(2, tabs.Items.Count);
                Assert.IsType<TabItem>(tabs.Items[0]);
                TabItem calculationTab = Assert.IsType<TabItem>(tabs.Items[1]);
                Assert.IsType<ManualColorGamutView>(calculationTab.Content);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
