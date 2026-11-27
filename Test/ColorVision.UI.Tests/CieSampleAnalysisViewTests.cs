using ColorVision.ImageEditor.Cie;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public sealed class CieSampleAnalysisViewTests
{
    [Fact]
    public void DenseOverlayKeepsMarkerOrderAndEmphasizesTheSelectedMatchOnce()
    {
        WpfTestHost.Invoke(() =>
        {
            var point = new CieChromaticity(0.31, 0.32);
            var selected = new CieMarker("selected", point, Colors.Black);
            var markers = Enumerable.Range(0, 20)
                .Select(index => new CieMarker(string.Empty, point, index % 2 == 0 ? Colors.Red : Colors.Blue))
                .ToArray();
            var overlay = new CieOverlayVisual();
            Size size = new(800, 800);
            overlay.Render(CieDiagramProfiles.Cie1931xy, size, size, 2, [], [selected, .. markers],
                showCctReference: false, showDaylightReference: false, selected);

            var ellipses = overlay.Drawing.Children.OfType<GeometryDrawing>()
                .Where(drawing => drawing.Geometry is EllipseGeometry).ToArray();
            Assert.Equal(2 * (markers.Length + 1), ellipses.Length);
            Assert.Equal(markers.Select(marker => marker.Color).Append(selected.Color),
                ellipses.Where(drawing => drawing.Brush != null).Select(drawing => Assert.IsType<SolidColorBrush>(drawing.Brush).Color));
            Assert.All(ellipses.Take(2 * markers.Length), drawing =>
                Assert.Equal(9, Assert.IsType<EllipseGeometry>(drawing.Geometry).RadiusX));
            Assert.All(ellipses.Skip(2 * markers.Length), drawing =>
                Assert.Equal(12, Assert.IsType<EllipseGeometry>(drawing.Geometry).RadiusX));
        });
    }

    [Fact]
    public void FilterKeepsSampleColorsAndSessionReloadUsesNewSampleOrder()
    {
        WpfTestHost.Invoke(() =>
        {
            var settings = new CieAnalysisSettings();
            IReadOnlyList<CieAnalysisSample> samples = CieAnalysisIO.ImportSamples(
                "Name,x,y,Y,Group\nFirst,0.30,0.31,40,Shared\nSecond,0.31,0.32,40,Other\nThird,0.32,0.33,40,Shared", settings);
            var view = new CieSampleAnalysisView();
            view.AddSamples(samples);
            var filter = Assert.IsType<TextBox>(view.FindName("SampleFilter"));
            var grid = Assert.IsType<DataGrid>(view.FindName("SamplesGrid"));

            filter.Text = "shared";
            Assert.Equal("Third", Assert.IsType<CieAnalysisRow>(grid.SelectedItem).Name);
            Assert.Equal(
                new[] { ("First", Colors.RoyalBlue), ("Third", Colors.SeaGreen) },
                view.Diagram.Markers.Select(marker => (marker.Name, marker.Color)));

            view.SetReference(samples[2].Id);
            Assert.Equal(Colors.Black, view.Diagram.Markers[1].Color);
            Assert.Equal("Third", Assert.IsType<CieAnalysisRow>(grid.SelectedItem).Name);

            view.LoadSession(new CieAnalysisSession { Settings = settings, Samples = [samples[2], samples[0]] });
            Assert.Equal(
                new[] { ("Third", Colors.RoyalBlue), ("First", Colors.OrangeRed) },
                view.Diagram.Markers.Select(marker => (marker.Name, marker.Color)));
        });
    }
}
