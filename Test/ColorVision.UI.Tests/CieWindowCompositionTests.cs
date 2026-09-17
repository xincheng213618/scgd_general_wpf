using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Cie;
using ColorVision.Themes;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class CieWindowCompositionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CalculationWhiteIsWindowLocalAndUpdatesPointCursorAndSamples(bool dark)
    {
        WpfTestHost.Invoke(() =>
        {
            var window = new WindowCIE { Width = 980, Height = 760, ShowActivated = false, ShowInTaskbar = false };
            var other = new WindowCIE();
            try
            {
                foreach (string source in (dark ? ThemeManager.ResourceDictionaryDark : ThemeManager.ResourceDictionaryWhite).Concat(ThemeManager.ResourceDictionaryBase))
                    window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(source, UriKind.RelativeOrAbsolute) });
                window.Show(); Pump();
                var analysis = window.SampleAnalysisView;
                var xyz = CieAnalysisMath.XyYToXyz(.3187, .3434, 80);
                window.ChangeSelect(xyz, "实测点", "XYZ");
                var sample = new CieAnalysisSample(Guid.NewGuid(), "样品", "", "XYZ", xyz, CieSampleBasis.Absolute);
                analysis.AddSamples(new[] { sample });
                var original = analysis.Rows.Single();
                string Read(string name) => ((TextBlock)window.FindName(name)).Text;
                string originalXyz = Read("TextBlockSelectedXyz"), originalCct = Read("TextBlockSelectedCct");
                string originalLab = Read("TextBlockSelectedLab"), originalPurity = Read("TextBlockSelectedPurity");
                var plot = window.DiagramView;
                Assert.Equal(CieIlluminants.D65.Chromaticity, plot.ReferenceWhite);
                Point pixel = plot.Profile.ToImagePixel(sample.Xy);
                var bitmap = (BitmapSource)plot.DiagramCanvas.Source;
                string cursor = plot.GetCursorText(new Point(pixel.X / bitmap.PixelWidth * plot.DiagramCanvas.ActualWidth, pixel.Y / bitmap.PixelHeight * plot.DiagramCanvas.ActualHeight));
                string? changedCursor = null;
                plot.CursorTextChanged += (_, text) => changedCursor = text;
                plot.Zoom(1.25);
                Matrix manual = plot.ZoomBox.ContentMatrix;

                ((ComboBox)window.FindName("ReferenceWhitePreset")).SelectedItem = CieIlluminants.D50;
                Assert.Equal(CieIlluminants.D50.Chromaticity, plot.ReferenceWhite);
                Assert.Equal(plot.ReferenceWhite, analysis.Diagram.ReferenceWhite);
                Assert.Equal(CieIlluminants.D65.Chromaticity, other.DiagramView.ReferenceWhite);
                Assert.Equal(manual, plot.ZoomBox.ContentMatrix);
                Assert.Equal(originalXyz, Read("TextBlockSelectedXyz"));
                Assert.Equal(originalCct, Read("TextBlockSelectedCct"));
                Assert.NotEqual(originalLab, Read("TextBlockSelectedLab"));
                Assert.NotEqual(originalPurity, Read("TextBlockSelectedPurity"));
                Assert.Contains("距 D50", Read("TextBlockSelectedWhiteDistance"));
                Assert.NotNull(changedCursor);
                Assert.Equal(cursor.Split('\n')[0], changedCursor.Split('\n')[0]);
                Assert.Contains("(D50)", changedCursor);
                Assert.NotEqual(original.Lab, analysis.Rows.Single().Lab);
                Assert.Equal(original.Cct, analysis.Rows.Single().Cct);
                Assert.Equal(xyz, analysis.Rows.Single().Sample.Xyz);

                ((CheckBox)window.FindName("CheckBoxD65")).IsChecked = false;
                ((CheckBox)window.FindName("CheckBoxA")).IsChecked = true;
                Assert.Equal(CieIlluminants.D50.Chromaticity, plot.ReferenceWhite);
                ((Expander)window.FindName("SelectedPointDetails")).IsExpanded = true;
                ((ToggleButton)window.FindName("ShowDisplayOptions")).IsChecked = true;
                Pump();
                Capture(window, $"cie-reference-{(dark ? "dark" : "light")}.png");
            }
            finally
            {
                window.SampleAnalysisView.LoadSession(window.SampleAnalysisView.GetSession());
                window.Close(); other.Close();
            }
        });
    }

    [Fact]
    public void CustomWhiteAndLuminanceValidateSynchronizeBothWaysAndRoundTripInExistingSession()
    {
        WpfTestHost.Invoke(() =>
        {
            var window = new WindowCIE();
            try
            {
                var analysis = window.SampleAnalysisView;
                TextBox Edit(string name) => (TextBox)window.FindName(name);
                void Apply() => ((Button)window.FindName("ApplyReferenceWhite")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var xy = new CieChromaticity(.3187, .3434);
                var xyz = CieAnalysisMath.XyYToXyz(xy.X, xy.Y, 100);
                window.ChangeSelect(xyz, "实测点", "XYZ");
                analysis.AddSamples(new[] { new CieAnalysisSample(Guid.NewGuid(), "相对样品", "", "XYZ", xyz, CieSampleBasis.Relative) });
                var relativeLab = analysis.Rows.Single().Lab;
                string absoluteLab = ((TextBlock)window.FindName("TextBlockSelectedLab")).Text;
                Edit("ReferenceWhiteLuminance").Text = "200"; Apply();
                Assert.NotEqual(absoluteLab, ((TextBlock)window.FindName("TextBlockSelectedLab")).Text);
                Assert.Equal(relativeLab, analysis.Rows.Single().Lab);

                var before = analysis.GetSession().Settings;
                foreach (var invalid in new[] { ("NaN", ".33", "100"), (".8", ".3", "100"), (".3", ".3", "0"), (".3", "1e-300", "1e300") })
                {
                    Edit("ReferenceWhiteX").Text = invalid.Item1; Edit("ReferenceWhiteY").Text = invalid.Item2; Edit("ReferenceWhiteLuminance").Text = invalid.Item3;
                    Apply();
                    Assert.Equal(before, analysis.GetSession().Settings);
                    Assert.Contains("未应用", ((TextBlock)window.FindName("ReferenceWhiteStatus")).Text);
                }
                var preset = (ComboBox)window.FindName("ReferenceWhitePreset");
                preset.SelectedIndex = preset.Items.Count - 1;
                Assert.True(((ToggleButton)window.FindName("ShowDisplayOptions")).IsChecked);
                Assert.Equal(before, analysis.GetSession().Settings);
                Edit("ReferenceWhiteX").Text = ".321"; Edit("ReferenceWhiteY").Text = ".34"; Edit("ReferenceWhiteLuminance").Text = "250"; Apply();
                Assert.Equal(new CieChromaticity(.321, .34), window.DiagramView.ReferenceWhite);
                Assert.Equal("自定义", ((CieMarker)preset.SelectedItem).Name);
                Assert.Equal(250, analysis.GetSession().Settings.AbsoluteWhiteLuminance);
                Assert.Equal(xyz, analysis.Rows.Single().Sample.Xyz);

                // Applying conditions from the samples page updates this same window's quick readouts.
                ((ComboBox)analysis.FindName("WhitePresetCombo")).SelectedItem = CieIlluminants.A;
                ((TextBox)analysis.FindName("WhiteLuminance")).Text = "300";
                ((Button)analysis.FindName("ApplyCalculationSettings")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Same(CieIlluminants.A, preset.SelectedItem);
                Assert.Equal("300", Edit("ReferenceWhiteLuminance").Text);
                Assert.Contains("距 A", ((TextBlock)window.FindName("TextBlockSelectedWhiteDistance")).Text);
                string json = CieAnalysisIO.SaveSession(analysis.GetSession());
                analysis.LoadSession(new());
                Assert.Equal(CieIlluminants.D65.Chromaticity, window.DiagramView.ReferenceWhite);
                analysis.LoadSession(CieAnalysisIO.LoadSession(json));
                Assert.Equal(CieIlluminants.A.Chromaticity, window.DiagramView.ReferenceWhite);
                Assert.Equal(300, analysis.GetSession().Settings.AbsoluteWhiteLuminance);
                Assert.Equal(1, analysis.GetSession().Version);
                Assert.Equal(xyz, analysis.Rows.Single().Sample.Xyz);
            }
            finally
            {
                window.SampleAnalysisView.LoadSession(window.SampleAnalysisView.GetSession());
                window.Close();
            }
        });
    }

    [Fact]
    public void TabsShareTheToolbarRowAndGrowTheSameWindowOnlyWhenNeeded()
    {
        WpfTestHost.Invoke(() =>
        {
            var window = new WindowCIE { Width = 720, Height = 520, ShowActivated = false, ShowInTaskbar = false };
            try
            {
                window.Show(); Pump();
                var tabs = (TabControl)window.FindName("CieTabs");
                var header = (Border)tabs.Template.FindName("CieTabHeader", tabs);
                var toolbar = (Border)window.FindName("DiagramToolbar");
                Assert.Equal(header.TranslatePoint(new Point(), window).Y, toolbar.TranslatePoint(new Point(), window).Y, 5);
                Assert.True(header.TranslatePoint(new Point(header.ActualWidth, 0), window).X <= toolbar.TranslatePoint(new Point(), window).X);
                double initialWidth = window.ActualWidth, initialHeight = window.ActualHeight;
                tabs.SelectedIndex = 1; Pump();
                Assert.True(window.ActualWidth >= initialWidth && window.ActualHeight >= initialHeight);
                window.ShowSampleAnalysis(); Pump();
                double width = window.ActualWidth, height = window.ActualHeight;
                Assert.True(width >= window.MinWidth && height >= window.MinHeight);
                var actions = (WrapPanel)window.SampleAnalysisView.FindName("SessionToolbar");
                Assert.True(header.TranslatePoint(new Point(header.ActualWidth, 0), window).X <= actions.TranslatePoint(new Point(), window).X);
                Assert.True(((DataGrid)window.SampleAnalysisView.FindName("SamplesGrid")).ActualHeight >= 90);
                window.ShowChromaticity(); Pump();
                Assert.Equal(width, window.ActualWidth); Assert.Equal(height, window.ActualHeight);
                window.WindowState = WindowState.Maximized; Pump();
                window.ShowSampleAnalysis(); Pump();
                Assert.Equal(WindowState.Maximized, window.WindowState);
            }
            finally { window.Close(); }
        });
    }

    [Theory]
    [InlineData(CieDiagramKind.Cie1931xy)]
    [InlineData(CieDiagramKind.Cie1960uv)]
    [InlineData(CieDiagramKind.Cie1976uv)]
    public void ThemeSwitchRefreshesChartWithoutChangingItsColorsOrManualZoom(CieDiagramKind kind)
    {
        WpfTestHost.Invoke(() =>
        {
            var view = new CieDiagramView();
            var window = new Window { Content = view, Width = 860, Height = 680, ShowActivated = false, ShowInTaskbar = false };
            try
            {
                window.Resources["GlobalBackground"] = Brushes.White;
                view.SetDiagram(kind);
                window.Show(); Pump();
                view.Zoom(1.25);
                Matrix manual = view.ZoomBox.ContentMatrix;
                var light = (BitmapSource)view.DiagramCanvas.Source;
                Assert.Equal(Colors.White, Pixel(light, 0, 0));
                window.Resources["GlobalBackground"] = new SolidColorBrush(Color.FromRgb(38, 38, 38));
                Pump();
                var dark = (BitmapSource)view.DiagramCanvas.Source;
                Assert.Equal(Colors.Black, Pixel(dark, 0, 0));
                Assert.Equal(Colors.Black, ((SolidColorBrush)view.Background).Color);
                Assert.Equal(manual, view.ZoomBox.ContentMatrix);
                Point sample = view.Profile.ToImagePixel(new(.3234, .3578));
                Assert.Equal(Pixel(light, (int)sample.X, (int)sample.Y), Pixel(dark, (int)sample.X, (int)sample.Y));
                window.Resources["GlobalBackground"] = Brushes.White;
                Pump();
                Assert.Same(light, view.DiagramCanvas.Source);
                Assert.Equal(manual, view.ZoomBox.ContentMatrix);
            }
            finally { window.Close(); }
        });
    }

    [Theory]
    [InlineData(720, 520, false)]
    [InlineData(1240, 880, true)]
    public void PointDetailsAndTwoLineCursorExposePurityAndRespectSourceData(double width, double height, bool dark)
    {
        WpfTestHost.Invoke(() =>
        {
            var window = new WindowCIE { Width = width, Height = height, ShowActivated = false, ShowInTaskbar = false };
            try
            {
                foreach (string source in (dark ? ThemeManager.ResourceDictionaryDark : ThemeManager.ResourceDictionaryWhite).Concat(ThemeManager.ResourceDictionaryBase))
                    window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(source, UriKind.RelativeOrAbsolute) });
                window.Show();
                CieChromaticity white = CieIlluminants.D65.Chromaticity;
                CieChromaticity spectral = CieSpectrumLocus.FindNearest(520)!.Value.Chromaticity;
                CieChromaticity midpoint = new((white.X + spectral.X) / 2, (white.Y + spectral.Y) / 2);
                window.ChangeSelect(midpoint.X, midpoint.Y);
                Assert.Contains("50.00%", ((TextBlock)window.FindName("TextBlockSelectedPurity")).Text);
                Assert.Contains("520.0 nm", ((TextBlock)window.FindName("TextBlockSelectedWavelength")).Text);
                Assert.Equal(Visibility.Collapsed, ((StackPanel)window.FindName("SelectedColorValues")).Visibility);
                window.ChangeSelect(CieAnalysisMath.XyYToXyz(.49860, .30834, 100), "POI (307, 130)", "CVCIE 原始 XYZ");
                Assert.Equal(Visibility.Visible, ((StackPanel)window.FindName("SelectedColorValues")).Visibility);
                Assert.Contains("cd/m²", ((TextBlock)window.FindName("TextBlockSelectedXyz")).Text);
                Assert.Contains("不适用", ((TextBlock)window.FindName("TextBlockSelectedCct")).Text);
                Assert.Contains("Yn=100.000", ((TextBlock)window.FindName("TextBlockSelectedConditions")).Text);
                ((Expander)window.FindName("SelectedPointDetails")).IsExpanded = true;
                var cursor = (TextBlock)window.FindName("TextBlockCursor");
                cursor.Text = new CiePointReadout(new(.3187, .3434), white).CursorText;
                Assert.Equal(2, cursor.Text.Split('\n').Length);
                Assert.Contains("激发纯度", cursor.Text);
                Pump();
                Capture(window, $"cie-current-point-{width}-{(dark ? "dark" : "light")}.png");
                window.SetSelectedMarker(null);
                Assert.Equal("激发纯度: —", ((TextBlock)window.FindName("TextBlockSelectedPurity")).Text);
                Assert.Equal(Visibility.Collapsed, ((StackPanel)window.FindName("SelectedColorValues")).Visibility);
            }
            finally { window.Close(); }
        });
    }

    private static Color Pixel(BitmapSource source, int x, int y)
    {
        var bitmap = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        byte[] pixel = new byte[4]; bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
    }

    private static void Capture(Window window, string name)
    {
        string? folder = Environment.GetEnvironmentVariable("COLORVISION_CIE_PREVIEW_OUTPUT");
        if (string.IsNullOrEmpty(folder)) return;
        Directory.CreateDirectory(folder);
        var content = (FrameworkElement)window.Content;
        var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(window.Background, null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
            dc.DrawRectangle(new VisualBrush(content), null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
        }
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(folder, name)); encoder.Save(stream);
    }

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
    public void CieWindowHostsDiagramGamutAndSamplesInOneWindow()
    {
        WpfTestHost.Invoke(() =>
        {
            WindowCIE window = new();
            try
            {
                TabControl tabs = Assert.IsType<TabControl>(window.FindName("CieTabs"));
                Assert.Equal(3, tabs.Items.Count);
                Assert.IsType<TabItem>(tabs.Items[0]);
                TabItem calculationTab = Assert.IsType<TabItem>(tabs.Items[1]);
                Assert.IsType<ManualColorGamutView>(calculationTab.Content);
                Assert.Same(window.SampleAnalysisView, Assert.IsType<TabItem>(tabs.Items[2]).Content);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
