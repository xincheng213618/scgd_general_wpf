using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Cie;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class CieAnalysisTests
{
    public static IEnumerable<object[]> SharmaPairs()
    {
        // Sharma, Wu & Dalal, Color Research & Application 30(1), 2005, pp.21–30.
        // https://hajim.rochester.edu/ece/sites/gsharma/ciede2000/dataNprograms/ciede2000testdata.txt
        foreach (string line in File.ReadLines(Path.Combine(AppContext.BaseDirectory, "TestData", "Cie", "ciede2000testdata.txt")))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            double[] values = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
            yield return values.Cast<object>().ToArray();
        }
    }

    [Theory]
    [MemberData(nameof(SharmaPairs))]
    public void Ciede2000MatchesPublishedSupplementaryData(double l1, double a1, double b1, double l2, double a2, double b2, double expected)
    {
        CieLab a = new(l1, a1, b1), b = new(l2, a2, b2);
        // The published results have four decimal places.
        Assert.InRange(Math.Abs(CieAnalysisMath.DeltaE2000(a, b) - expected), 0, 0.00005);
        Assert.Equal(CieAnalysisMath.DeltaE2000(a, b), CieAnalysisMath.DeltaE2000(b, a), 10);
    }

    [Fact]
    public void Cie94AndCmcMatchIndependentColourExamplesWithReferenceFirst()
    {
        CieLab reference = new(100, 21.57210357, 272.22819350), sample = new(100, 426.67945353, 72.39590835);
        Assert.Equal(83.7792255, CieAnalysisMath.DeltaE94(reference, sample), 6);
        Assert.Equal(172.7047713, CieAnalysisMath.DeltaECmc(reference, sample, 2), 6);
        Assert.NotEqual(CieAnalysisMath.DeltaE94(reference, sample), CieAnalysisMath.DeltaE94(sample, reference));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0.2, 0.1, 0.3)]
    [InlineData(20, 25, 15)]
    [InlineData(95.047, 100, 108.883)]
    public void LabAndLuvRoundTripIncludingBlackAndLinearBranch(double x, double y, double z)
    {
        CieXyz xyz = new(x, y, z), white = new(95.047, 100, 108.883);
        CieXyz labRoundTrip = CieAnalysisMath.LabToXyz(CieAnalysisMath.XyzToLab(xyz, white), white);
        CieXyz luvRoundTrip = CieAnalysisMath.LuvToXyz(CieAnalysisMath.XyzToLuv(xyz, white), white);
        foreach (CieXyz actual in new[] { labRoundTrip, luvRoundTrip })
        {
            Assert.Equal(x, actual.X, 8); Assert.Equal(y, actual.Y, 8); Assert.Equal(z, actual.Z, 8);
        }
        Assert.Equal(new CieLab(100, 0, 0), CieAnalysisMath.XyzToLab(white, white));
    }

    [Theory]
    [InlineData(CieInputSpace.XyY, 0.31271, 0.32902, 100)]
    [InlineData(CieInputSpace.XYZ, 95.0428545377181, 100, 108.890037079813)]
    [InlineData(CieInputSpace.Lab, 100, 0, 0)]
    [InlineData(CieInputSpace.Luv, 100, 0, 0)]
    public void InputModesAgreeAtReferenceWhite(CieInputSpace space, double a, double b, double c)
    {
        CieAnalysisSample sample = CieAnalysisSample.Create("white", "", "test", space, a, b, c, CieSampleBasis.Relative, new());
        Assert.Equal(0.31271, sample.Xy.X, 6); Assert.Equal(0.32902, sample.Xy.Y, 6);
        Assert.Equal(100, sample.Xyz.Y, 6);
    }

    [Fact]
    public void MissingLuminanceAndMixedScalesNeverProduceFullColourDifference()
    {
        var settings = new CieAnalysisSettings();
        CieAnalysisSample reference = CieAnalysisSample.Create("ref", "", "test", CieInputSpace.XyY, .31271, .32902, 100, CieSampleBasis.Relative, settings);
        var xyOnly = CieAnalysisSample.Create("xy", "", "test", CieInputSpace.Xy, .3187, .3434, 0, CieSampleBasis.Relative, settings);
        var absolute = reference with { Id = Guid.NewGuid(), Basis = CieSampleBasis.Absolute };
        foreach (var sample in new[] { xyOnly, absolute })
        {
            var row = new CieAnalysisRow(sample, reference, settings);
            Assert.Null(row.DeltaE00); Assert.NotNull(row.DeltaUv); Assert.NotNull(row.Jncd);
            Assert.NotEqual("阈值内", row.Result);
        }
        var black = reference with { Id = Guid.NewGuid(), Xyz = new(0, 0, 0) };
        var blackRow = new CieAnalysisRow(black, reference, settings);
        Assert.NotNull(blackRow.DeltaE00);
        Assert.Null(blackRow.DeltaUv);
        Assert.False(blackRow.Cct.IsFinite);
    }

    [Fact]
    public void GamutCoverageUsesIntersectionAndPreservesAreaRatioAndWinding()
    {
        CieChromaticity[] reference = { new(0, 0), new(1, 0), new(0, 1) };
        CieChromaticity[] sample = { new(.5, 0), new(1.5, 0), new(.5, 1) };
        foreach (var r in new[] { reference, reference.Reverse().ToArray() })
        {
            var result = CieGamutGeometry.Compare(sample, r, CieDiagramKind.Cie1931xy);
            Assert.Equal(100, result.AreaRatioPercent, 8);
            Assert.Equal(25, result.CoveragePercent, 8);
            Assert.Equal(.125, result.IntersectionArea, 8);
        }
        var disjoint = sample.Select(p => new CieChromaticity(p.X + 2, p.Y)).ToArray();
        Assert.Equal(0, CieGamutGeometry.Compare(disjoint, reference, CieDiagramKind.Cie1931xy).CoveragePercent);
        Assert.Throws<ArgumentException>(() => CieGamutGeometry.Compare(new[] { new CieChromaticity(0, 0), new(1, 1), new(2, 2) }, reference, CieDiagramKind.Cie1931xy));
        foreach (CieDiagramKind kind in Enum.GetValues<CieDiagramKind>())
            foreach (CieGamut gamut in CieGamuts.Defaults)
                Assert.Equal(100, CieGamutGeometry.Compare(gamut.Vertices, gamut.Vertices, kind).CoveragePercent, 8);
    }

    [Fact]
    public void WavelengthHandlesSpectralPurpleWhiteAndOutsidePoints()
    {
        WpfTestHost.Invoke(() =>
        {
            CieChromaticity white = CieIlluminants.D65.Chromaticity;
            var locus = CieSpectrumLocus.Points;
            CieChromaticity spectral = CieSpectrumLocus.FindNearest(520)!.Value.Chromaticity;
            var half = new CieChromaticity((white.X + spectral.X) / 2, (white.Y + spectral.Y) / 2);
            var result = CieGamutGeometry.Wavelength(half, white, locus)!.Value;
            Assert.False(result.IsComplementary); Assert.Equal(520, result.Wavelength, 5); Assert.Equal(.5, result.Purity, 6);
            CieChromaticity purple = new((locus[0].Chromaticity.X + locus[^1].Chromaticity.X) / 2, (locus[0].Chromaticity.Y + locus[^1].Chromaticity.Y) / 2);
            result = CieGamutGeometry.Wavelength(new((white.X + purple.X) / 2, (white.Y + purple.Y) / 2), white, locus)!.Value;
            Assert.True(result.IsComplementary); Assert.Equal(.5, result.Purity, 6);
            Assert.Null(CieGamutGeometry.Wavelength(white, white, locus));
            Assert.Null(CieGamutGeometry.Wavelength(new(.8, .8), white, locus));
        });
    }

    [Fact]
    public void CctUses1960UvProjectionAndDoesNotReportSaturatedColours()
    {
        CieChromaticity xy = CieColorConverter.CctToApproximatePlanckianXy(6500);
        Assert.InRange(CieAnalysisMath.EstimateCct(xy).TemperatureKelvin, 6499, 6501);
        CieChromaticity uv = CieColorConverter.XyToCie1960uv(xy);
        Assert.True(CieAnalysisMath.EstimateCct(CieColorConverter.Uv1960ToXy(new(uv.X, uv.Y + .003))).Duv > 0);
        Assert.True(CieAnalysisMath.EstimateCct(CieColorConverter.Uv1960ToXy(new(uv.X, uv.Y - .003))).Duv < 0);
        Assert.False(CieAnalysisMath.EstimateCct(new(.7, .29)).IsFinite);
        Assert.False(CieAnalysisMath.EstimateCct(CieChromaticity.Empty).IsFinite);
    }

    [Theory]
    [InlineData(.31271, .32902)]
    [InlineData(.64, .33)]
    [InlineData(.15, .06)]
    public void BothUcsCoordinatesInvertToTheOriginalXy(double x, double y)
    {
        CieChromaticity original = new(x, y);
        foreach (CieChromaticity restored in new[] { CieColorConverter.Uv1960ToXy(CieColorConverter.XyToCie1960uv(original)), CieColorConverter.Uv1976ToXy(CieColorConverter.XyToCie1976uv(original)) })
        {
            Assert.Equal(x, restored.X, 12); Assert.Equal(y, restored.Y, 12);
        }
    }

    [Fact]
    public void CsvAndSessionRoundTripOriginalDataIncludingBlackAndQuotedMetadata()
    {
        var settings = new CieAnalysisSettings { WhiteX = .34567, WhiteY = .3585, JncdStep = .003, DiagramKind = CieDiagramKind.Cie1976uv };
        var samples = CieAnalysisIO.ImportSamples("Name,X,Y,Z,Group,Source\r\n\"a,\"\"b\"\"\",0,0,0,\"line\n2\",=SUM(A1)\r\nwhite,96.422,100,82.521,g,manual", settings).ToList();
        var session = new CieAnalysisSession { Settings = settings, Samples = samples, ReferenceId = samples[1].Id };
        CieAnalysisSession restored = CieAnalysisIO.LoadSession(CieAnalysisIO.SaveSession(session));
        Assert.Equal(samples, restored.Samples); Assert.Equal(session.ReferenceId, restored.ReferenceId);
        Assert.Equal(settings.JncdStep, restored.Settings.JncdStep);
        var rows = samples.Select(s => new CieAnalysisRow(s, samples[1], settings)).ToArray();
        string csv = CieAnalysisIO.ExportSamples(rows);
        Assert.Contains("'=SUM(A1)", csv);
        var imported = CieAnalysisIO.ImportSamples(csv, settings);
        for (int i = 0; i < samples.Count; i++)
        {
            Assert.Equal(samples[i].Xyz, imported[i].Xyz); Assert.Equal(samples[i].Basis, imported[i].Basis);
            Assert.Equal(samples[i].Name, imported[i].Name); Assert.Equal(samples[i].Group, imported[i].Group); Assert.Equal(samples[i].Source, imported[i].Source);
        }
    }

    [Theory]
    [InlineData("x,y,Y\n0.3,0,100")]
    [InlineData("X,Y,Z\nNaN,1,1")]
    [InlineData("X,Y,Z\n1,-1,1")]
    [InlineData("X,Y,Z\n1,2")]
    [InlineData("X,Y,Z\n\"1,2,3")]
    [InlineData("X,Y,Z\n1,2,3\n1,Infinity,1")]
    [InlineData("Space,V1,V2,V3\nSRgb,300,0,0")]
    public void ImportRejectsInvalidBatches(string text) => Assert.Throws<ArgumentException>(() => CieAnalysisIO.ImportSamples(text, new()));

    [Fact]
    public void ExcelTsvAndChromaticityOnlyHeadersAreSupported()
    {
        var samples = CieAnalysisIO.ImportSamples("Name\tx\ty\r\n点1\t0.3\t0.32", new());
        Assert.Equal(CieSampleBasis.ChromaticityOnly, Assert.Single(samples).Basis);
        Assert.Throws<ArgumentException>(() => CieAnalysisIO.LoadSession("{\"Version\":42}"));
        Assert.Throws<ArgumentException>(() => CieAnalysisIO.LoadSession("{\"Version\":1,\"Samples\":null}"));
    }

    [Fact]
    public void ReportEscapesUserContentAndIncludesConditions()
    {
        var sample = CieAnalysisSample.Create("<script>alert(1)</script>", "<img>", "test", CieInputSpace.XyY, .3, .32, 100, CieSampleBasis.Relative, new());
        var session = new CieAnalysisSession { Samples = new() { sample } };
        string report = CieAnalysisIO.ExportReport(session, new[] { new CieAnalysisRow(sample, null, session.Settings) }, Array.Empty<byte>());
        Assert.DoesNotContain("<script>", report); Assert.Contains("&lt;script&gt;", report);
        Assert.Contains("0.004", report); Assert.Contains("当前视图", report); Assert.Contains("data:image/png;base64,", report);
    }

    [Theory]
    [InlineData(1240, 880, false)]
    [InlineData(720, 520, false)]
    [InlineData(980, 720, false)]
    [InlineData(1240, 880, true)]
    public void CieConnectsSampleSelectionReferenceGamutAndSession(double width, double height, bool dark)
    {
        WpfTestHost.Invoke(() =>
        {
            var window = new WindowCIE { Width = width, Height = height, ShowActivated = false, ShowInTaskbar = false };
            var analysis = window.SampleAnalysisView;
            window.ShowSampleAnalysis();
            try
            {
                foreach (string source in (dark ? ColorVision.Themes.ThemeManager.ResourceDictionaryDark : ColorVision.Themes.ThemeManager.ResourceDictionaryWhite).Concat(ColorVision.Themes.ThemeManager.ResourceDictionaryBase))
                    window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(source, UriKind.RelativeOrAbsolute) });
                window.Show(); Pump();
                var diagramCombo = (ComboBox)analysis.FindName("DiagramKindCombo");
                Assert.Equal(window.FindResource("GlobalTextBrush"), diagramCombo.Foreground);
                Assert.Equal(window.FindResource("ButtonBackground"), diagramCombo.Background);
                Click(analysis, "AddSampleButton");
                Assert.Single(analysis.Rows);
                Click(analysis, "SetReferenceButton");
                Assert.True(analysis.Rows[0].IsReference);
                var name = (TextBox)analysis.FindName("SampleName");
                name.Text = "样品 A";
                ((TextBox)analysis.FindName("Value1")).Text = "0.3187";
                ((TextBox)analysis.FindName("Value2")).Text = "0.3434";
                Click(analysis, "AddSampleButton");
                Pump();
                Assert.Equal(2, analysis.Rows.Count); Assert.NotNull(analysis.Rows[1].DeltaE00);
                Assert.Contains("样品 A", ((TextBlock)analysis.FindName("SelectedTitle")).Text);
                Assert.True(analysis.Diagram.ActualWidth > 0);
                Assert.NotEmpty(analysis.CaptureDiagramPng());
                CapturePreview(window, $"cie-samples-{width}-{(dark ? "dark" : "light")}.png");
                string? output = Environment.GetEnvironmentVariable("COLORVISION_CIE_PREVIEW_OUTPUT");
                if (width == 1240 && !dark && !string.IsNullOrEmpty(output))
                {
                    File.WriteAllText(Path.Combine(output, "cie-analysis-demo.html"), CieAnalysisIO.ExportReport(analysis.GetSession(), analysis.Rows, analysis.CaptureDiagramPng()));
                    File.WriteAllText(Path.Combine(output, "cie-analysis-demo.csv"), CieAnalysisIO.ExportSamples(analysis.Rows));
                    File.WriteAllText(Path.Combine(output, "cie-analysis-demo.cie-session.json"), CieAnalysisIO.SaveSession(analysis.GetSession()));
                }
                var tabs = (TabControl)window.FindName("CieTabs");
                Assert.Equal(3, tabs.Items.Count);
                ((MenuItem)analysis.FindName("UseAsRed")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Pump();
                Assert.Equal(1, tabs.SelectedIndex);
                var gamut = (ManualColorGamutView)window.FindName("GamutView");
                Assert.Equal(analysis.Rows[1].Xy.X, double.Parse(((TextBox)gamut.FindName("TextBoxRedX")).Text, System.Globalization.CultureInfo.InvariantCulture));
                Assert.Equal("0.6000", ((TextBox)gamut.FindName("TextBoxGreenY")).Text);
                var grid = (DataGrid)gamut.FindName("ResultGrid");
                Assert.Single(grid.Items);
                Assert.Contains(grid.Columns, c => Equals(c.Header, "覆盖率(%)"));
                ((ComboBox)gamut.FindName("GamutPlane")).SelectedIndex = 1; Pump();
                Assert.Single(grid.Items);
                CapturePreview(window, $"cie-gamut-{width}-{(dark ? "dark" : "light")}.png");
                analysis.LoadSession(CieAnalysisIO.LoadSession(CieAnalysisIO.SaveSession(analysis.GetSession())));
                Assert.Equal(2, analysis.Rows.Count);
                window.ShowSampleAnalysis(); Pump();
                Assert.Equal("样品 3", ((TextBox)analysis.FindName("SampleName")).Text);
                Assert.Same(analysis, ((TabItem)tabs.Items[2]).Content);
                ((System.Windows.Controls.Primitives.ToggleButton)analysis.FindName("ShowHelp")).IsChecked = true;
                Pump();
                Assert.True(((ScrollViewer)analysis.FindName("CalculationHelp")).IsVisible);
                Assert.False(((Grid)analysis.FindName("AnalysisContent")).IsVisible);
            }
            finally
            {
                analysis.LoadSession(analysis.GetSession()); // End the test with no unsaved user document.
                window.Close();
            }
        });
    }

    [Fact]
    public void CieCapturesCurrentPoiWithoutOpeningAnotherWindowAndKeepsItsSnapshot()
    {
        WpfTestHost.Invoke(() =>
        {
            var source = new Window { ShowActivated = false, ShowInTaskbar = false };
            source.Show();
            var window = new WindowCIE { Owner = source, ShowActivated = false, ShowInTaskbar = false };
            var analysis = window.SampleAnalysisView;
            try
            {
                window.Show();
                int count = Application.Current.Windows.Count;
                CieXyz measurement = new(95, 100, 109);
                window.ChangeSelect(measurement, "POI", "CVCIE 原始 XYZ / POI");
                window.ShowSampleAnalysis();
                Click(analysis, "CaptureSourceButton");
                Assert.Equal(measurement, Assert.Single(analysis.Rows).Sample.Xyz);
                Assert.Equal(CieSampleBasis.Absolute, analysis.Rows[0].Sample.Basis);
                window.SetSelectedMarker(new CieMarker("", new(.31, .33), Colors.Black));
                Click(analysis, "CaptureSourceButton");
                Assert.Equal(CieSampleBasis.ChromaticityOnly, analysis.Rows[1].Sample.Basis);
                window.ShowChromaticity();
                window.ShowSampleAnalysis();
                window.SetSelectedMarker(null);
                Assert.False(((Button)analysis.FindName("CaptureSourceButton")).IsEnabled);
                Assert.Equal(measurement, analysis.Rows[0].Sample.Xyz);
                Assert.Equal(2, analysis.Rows.Count);
                Assert.Equal(count, Application.Current.Windows.Count);
                Assert.Null(window.Owner);
                source.Close();
                Assert.True(window.IsVisible);
            }
            finally
            {
                source.Close();
                analysis.LoadSession(analysis.GetSession());
                window.Close();
            }
        });
    }

    private static void Click(FrameworkElement root, string name) => ((Button)root.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private static void CapturePreview(Window window, string name)
    {
        string? folder = Environment.GetEnvironmentVariable("COLORVISION_CIE_PREVIEW_OUTPUT");
        if (string.IsNullOrWhiteSpace(folder)) return;
        Directory.CreateDirectory(folder);
        var content = (FrameworkElement)window.Content;
        var image = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth), (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(window.Background, null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
            dc.DrawRectangle(new VisualBrush(content), null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
        }
        image.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(Path.Combine(folder, name)); encoder.Save(stream);
    }
}
