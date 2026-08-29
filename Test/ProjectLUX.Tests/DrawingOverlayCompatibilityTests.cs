using ColorVision.Engine.Templates.Jsons.MTF2;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using ProjectLUX.Process;
using ProjectLUX.Process.Distortion;
using ProjectLUX.Process.MTFHVAR;
using ProjectLUX.Process.MTFHV;
using ProjectLUX.Process.VR.MTFH;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Xunit;

namespace ProjectLUX.Tests;

public sealed class DrawingOverlayCompatibilityTests
{
    [Fact]
    public void StandardAndArMtfRenderKeepRectangleBusinessFields()
    {
        WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            MTFItem item = new()
            {
                name = "Center",
                id = 7,
                x = 125,
                y = 80,
                w = 48,
                h = 32,
                mtfValue = 0.7364,
            };

            AssertMtfOverlay(
                new MTFHVProcess().Render,
                new MTFHVViewTestResult { MTFDetailViewReslut = CreateMtfDetail(item) },
                item);
            AssertMtfOverlay(
                new MTFHVARProcess().Render,
                new MTFHVARViewTestResult { MTFDetailViewReslut = CreateMtfDetail(item) },
                item);
        });
    }

    [Fact]
    public void DistortionRenderKeepsZoomAdjustedGeometryAndAuthoredFontSize()
    {
        WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            using ImageView imageView = new();
            imageView.Config.IsLayoutUpdated = false;
            imageView.Zoombox1.ContentMatrix = new Matrix(2, 0, 0, 2, 0, 0);
            Point center = new(640, 360);
            DistortionViewTestResult viewResult = new() { Points = [center] };

            new DistortionProcess().Render(CreateContext(imageView, viewResult));

            DVCircleText circle = Assert.Single(imageView.ImageShow.Visuals.OfType<DVCircleText>());
            Assert.Equal(center, circle.Attribute.Center);
            Assert.Equal(10, circle.Attribute.Radius);
            Assert.Equal(10, circle.Attribute.RadiusY);
            Assert.Equal(0.5, circle.Attribute.Pen.Thickness, 6);
            Assert.Equal(Colors.Red, GetBrushColor(circle.Attribute.Pen.Brush));
            Assert.Equal(Colors.Transparent, GetBrushColor(circle.Attribute.Brush));
            Assert.Equal(20, circle.Attribute.FontSize);
            Assert.Equal($"{Environment.NewLine} X:640{Environment.NewLine}Y:360", circle.Attribute.Text);
            Assert.Equal(0, circle.Attribute.Id);
            Assert.Null(circle.Attribute.Msg);
            Assert.NotNull(circle.Drawing);
            Assert.Single(imageView.ImageShow.UndoStack);
        });
    }

    [Fact]
    public void VrMtfRenderKeepsSortedIdsAndNineHundredRegionColors()
    {
        WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            List<MTFItem> items = Enumerable.Range(0, 900)
                .Reverse()
                .Select(index => new MTFItem
                {
                    name = $"P{index:D3}",
                    id = index,
                    x = index % 30,
                    y = index / 30,
                    w = 1,
                    h = 1,
                    mtfValue = index / 1000.0,
                })
                .ToList();
            VRMTFHViewTestResult viewResult = new()
            {
                MTFDetailViewReslut = CreateMtfDetail(items),
            };

            using ImageView imageView = new();
            new VRMTFHProcess().Render(CreateContext(imageView, viewResult));

            List<DVRectangleText> rectangles = imageView.ImageShow.Visuals.OfType<DVRectangleText>().ToList();
            Assert.Equal(900, rectangles.Count);
            Assert.Equal(Enumerable.Range(1, 900), rectangles.Select(rectangle => rectangle.Attribute.Id));
            Assert.Equal(new Rect(0, 0, 1, 1), rectangles[0].Attribute.Rect);
            Assert.Equal(new Rect(29, 29, 1, 1), rectangles[^1].Attribute.Rect);
            Assert.Equal(Colors.Red, GetStrokeColor(rectangles[0]));
            Assert.Equal(Colors.Gray, GetStrokeColor(rectangles[14 * 30 + 14]));
            Assert.Equal(Colors.Green, GetStrokeColor(rectangles[14 * 30 + 17]));
            Assert.Equal(Colors.Blue, GetStrokeColor(rectangles[14 * 30 + 20]));
            Assert.Equal(Colors.Yellow, GetStrokeColor(rectangles[14 * 30 + 25]));
            Assert.All(rectangles, rectangle =>
            {
                Assert.Equal(1, rectangle.Attribute.Pen.Thickness, 6);
                Assert.Equal(Colors.Transparent, GetBrushColor(rectangle.Attribute.Brush));
                Assert.Equal(string.Empty, rectangle.Attribute.Text);
                Assert.Null(rectangle.Attribute.Msg);
                Assert.Equal(10, rectangle.Attribute.FontSize);
                Assert.NotNull(rectangle.Drawing);
            });
            Assert.Equal(900, imageView.ImageShow.UndoStack.Count);
        });
    }

    private static void AssertMtfOverlay(Action<IProcessExecutionContext> render, object viewResult, MTFItem item)
    {
        using ImageView imageView = new();
        render(CreateContext(imageView, viewResult));

        DVRectangleText rectangle = Assert.Single(imageView.ImageShow.Visuals.OfType<DVRectangleText>());
        Assert.Equal(new Rect(item.x, item.y, item.w, item.h), rectangle.Attribute.Rect);
        Assert.Equal(1, rectangle.Attribute.Id);
        Assert.Equal(item.name + "_" + item.id, rectangle.Attribute.Text);
        Assert.Equal(item.mtfValue?.ToString(CultureInfo.CurrentCulture), rectangle.Attribute.Msg);
        Assert.Equal(1, rectangle.Attribute.Pen.Thickness, 6);
        Assert.Equal(Colors.Red, GetStrokeColor(rectangle));
        Assert.Equal(Colors.Transparent, GetBrushColor(rectangle.Attribute.Brush));
        Assert.Equal(10, rectangle.Attribute.FontSize);
        Assert.NotNull(rectangle.Drawing);
        Assert.Single(imageView.ImageShow.UndoStack);
    }

    private static MTFDetailViewReslut CreateMtfDetail(params MTFItem[] items)
    {
        return CreateMtfDetail((IEnumerable<MTFItem>)items);
    }

    private static MTFDetailViewReslut CreateMtfDetail(IEnumerable<MTFItem> items)
    {
        return new MTFDetailViewReslut
        {
            MTFResult = new MTFResult
            {
                result = items.ToList(),
                resultChild = [],
            },
        };
    }

    private static IProcessExecutionContext CreateContext(ImageView imageView, object viewResult)
    {
        return new IProcessExecutionContext
        {
            ImageView = imageView,
            Result = new ProjectLUXReuslt
            {
                ViewResultJson = JsonConvert.SerializeObject(viewResult),
            },
        };
    }

    private static Color GetStrokeColor(DVRectangleText rectangle)
    {
        return GetBrushColor(rectangle.Attribute.Pen.Brush);
    }

    private static Color GetBrushColor(Brush brush)
    {
        return Assert.IsType<SolidColorBrush>(brush).Color;
    }

    private static void EnsureImageViewTestResources()
    {
        Application application = Application.Current ?? new Application();
        application.Resources["TextBox.Small"] = new Style(typeof(TextBox));
        application.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
        application.Resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
        application.Resources["ToolBarImage"] = new Style(typeof(Image));
        application.Resources["BaseStyle"] = new Style(typeof(Control));
        application.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
        application.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
    }
}
