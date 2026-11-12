using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Templates.Jsons.Distortion2;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class DistortionHistoryOverlayTests
{
    [Fact]
    public void HistoricalResultAddsCoordinatesGridAndMetricsWithoutDuplicatingOnReselection()
    {
        WpfTestHost.Invoke(() =>
        {
            var resources = Application.Current.Resources;
            resources["TextBox.Small"] = new Style(typeof(TextBox));
            resources["ComboBox.Small"] = new Style(typeof(ComboBox));
            resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
            resources["ToolBarImage"] = new Style(typeof(Image));
            resources["BaseStyle"] = new Style(typeof(Control));
            resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
            resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
            using ImageView image = new();
            image.OpenImage(new WriteableBitmap(320, 320, 96, 96, PixelFormats.Gray8, null));
            var points = Enumerable.Range(0, 9).Select(index => new ColorVision.Engine.Templates.Jsons.Distortion2.Point
                { Id = index, X = 40 + index % 3 * 100, Y = 40 + index / 3 * 100 }).ToList();
            Distortion2View detail = new()
            {
                Result = "{\"Units\":\"percent\"}",
                DistortionReslut = new()
                {
                    TVDistortion = new() { FinalPoints = points, HorizontalRatio = -0.1, VerticalRatio = 0.2 },
                    Analysis = new() { Geometry = new() { MaximumTiltDegrees = 2.5 } }
                }
            };
            ViewResultAlg result = new() { ResultType = ViewResultAlgType.Distortion, Version = "2.0", ViewResults = new ObservableCollection<IViewResult> { detail } };
            var context = new ViewResultContext { ImageView = image, ListView = new ListView { View = new GridView() }, LeftGridViewColumnVisibilitys = new(), SideTextBox = new() };
            ViewHandleDistortion2 handler = new();
            handler.Handle(context, result);
            var labels = image.ImageShow.Visuals.OfType<DVCircleText>().ToArray();
            Assert.Equal(9, labels.Length);
            Assert.Contains(labels, marker => marker.Attribute.Msg == "P1\nX:40.0\nY:40.0");
            Assert.Contains(labels, marker => marker.Center == new System.Windows.Point(240, 240));
            Assert.NotEmpty(image.ImageShow.Visuals.OfType<DVLine>());
            int visualCount = image.ImageShow.Visuals.Count;
            Assert.Contains("水平 TV", context.SideTextBox.Text);
            Assert.Contains("最大倾角", context.SideTextBox.Text);
            Assert.Contains("原始结果", context.SideTextBox.Text);
            handler.Handle(context, result);
            Assert.Equal(visualCount, image.ImageShow.Visuals.Count);
            points[0].X = 500; // A corrupt historical point must not leave a misleading old grid behind.
            handler.Handle(context, result);
            Assert.Equal(8, image.ImageShow.Visuals.OfType<DVCircleText>().Count());
            Assert.Empty(image.ImageShow.Visuals.OfType<DVLine>());
        });
    }

    [Theory]
    [InlineData("", 9, 3, 3)]
    [InlineData("", 49, 0, 0)]
    [InlineData("{\"GridDistortion\":{\"ExpectedRows\":7,\"ExpectedCols\":7}}", 49, 7, 7)]
    [InlineData("{\"GridDistortion\":{\"ExpectedRows\":70000000000,\"ExpectedCols\":7}}", 49, 0, 0)]
    public void GridConnectionsRequireKnownLayout(string raw, int count, int rows, int columns)
    {
        Assert.Equal((rows, columns), ViewHandleDistortion2.GetGridShape(new Distortion2View { Result = raw }, count));
    }
}
