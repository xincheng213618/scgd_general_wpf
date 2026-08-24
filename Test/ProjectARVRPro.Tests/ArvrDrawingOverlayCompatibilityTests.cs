using ColorVision.Engine.Templates.Jsons.MTF2;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI.Authorizations;
using Newtonsoft.Json;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.Distortion;
using ProjectARVRPro.Process.MTF;
using ProjectARVRPro.Process.ScreenDefects;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Xunit;

namespace ProjectARVRPro.Tests;

public sealed class ArvrDrawingOverlayCompatibilityTests
{
    private static readonly Lazy<Dispatcher> WpfDispatcher = new(CreateWpfDispatcher);

    [Fact]
    public void InvalidBusinessCoordinatesAreSkippedInsteadOfCreatingDefaultOverlays()
    {
        Assert.True(ProjectARVRPro.Process.ProcessExtensions.TryCreateOverlayPoint(12, 34, out Point point));
        Assert.Equal(new Point(12, 34), point);
        Assert.False(ProjectARVRPro.Process.ProcessExtensions.TryCreateOverlayPoint(double.NaN, 34, out point));
        Assert.Equal(default, point);

        Assert.True(ProjectARVRPro.Process.ProcessExtensions.TryCreateOverlayRect(10, 20, 30, 40, out Rect rect));
        Assert.Equal(new Rect(10, 20, 30, 40), rect);
        Assert.False(ProjectARVRPro.Process.ProcessExtensions.TryCreateOverlayRect(10, 20, -30, 40, out rect));
        Assert.Equal(Rect.Empty, rect);
        Assert.False(ProjectARVRPro.Process.ProcessExtensions.TryCreateOverlayRect(10, 20, 0, 40, out rect));
        Assert.False(ProjectARVRPro.Process.ProcessExtensions.TryCreateOverlayRect(double.NaN, 20, 30, 40, out rect));
        Assert.False(ProjectARVRPro.Process.ProcessExtensions.TryCreateOverlayRect(double.MaxValue, 20, double.MaxValue, 40, out rect));
    }

    [Fact]
    public void ProductionRenderPathsUseImageViewCommandPipelineAndSkipInvalidRectangles()
    {
        RunOnStaThread(() =>
        {
            using ImageView imageView = new();
            imageView.Config.IsLayoutUpdated = false;
            imageView.Config.DrawingTextFontSize = 10;
            imageView.Config.IsShowText = true;
            imageView.Config.IsShowMsg = true;

            int initialVisualCount = imageView.ImageShow.Visuals.Count;
            int initialDrawingVisualCount = imageView.EditorContext.DrawEditorContext.DrawingVisualLists.Count;
            int initialUndoCount = imageView.ImageShow.UndoStack.Count;

            int visualsAddCount = 0;
            int visualsChangedCount = 0;
            imageView.ImageShow.VisualsAdd += (_, _) => visualsAddCount++;
            imageView.ImageShow.VisualsChanged += (_, _) => visualsChangedCount++;

            IProcessExecutionContext context = new()
            {
                ImageView = imageView,
                Result = new ProjectARVRReuslt(),
            };

            context.Result.ViewResultJson = JsonConvert.SerializeObject(new DistortionViewTestResult
            {
                Points = [new Point(640, 360)],
            });
            new DistortionProcess().Render(context);

            context.Result.ViewResultJson = JsonConvert.SerializeObject(new MTFViewTestResult
            {
                MTFDetailViewReslut = new MTFDetailViewReslut
                {
                    MTFResult = new MTFResult
                    {
                        result =
                        [
                            new MTFItem { name = "valid", x = 125, y = 80, w = 48, h = 32, mtfValue = 0.7364 },
                            new MTFItem { name = "invalid", x = 20, y = 30, w = 0, h = 10, mtfValue = 0.5 },
                        ],
                        resultChild = [],
                    },
                },
            });
            new MTFProcess().Render(context);

            context.Result.ViewResultJson = JsonConvert.SerializeObject(new ScreenDefectsData
            {
                DefectCount = 2,
                Defects =
                [
                    new ScreenDefectData
                    {
                        Id = 17,
                        Type = "line",
                        X = 15,
                        Y = 25,
                        Width = 36,
                        Height = 18,
                        Area = 12.34567,
                        Contrast = 0.12567,
                        MeanValue = 128.25,
                        LocalMean = 126.75,
                    },
                    new ScreenDefectData { Id = 18, Type = "point", X = 20, Y = 30, Width = 10, Height = 0 },
                ],
            });
            new DetectScreenDefectsProcess().Render(context);

            Assert.Equal(initialVisualCount + 3, imageView.ImageShow.Visuals.Count);
            Assert.Equal(initialDrawingVisualCount + 3, imageView.EditorContext.DrawEditorContext.DrawingVisualLists.Count);
            Assert.Equal(initialUndoCount + 3, imageView.ImageShow.UndoStack.Count);
            Assert.Equal(3, visualsAddCount);
            Assert.Equal(3, visualsChangedCount);

            DVCircleText circle = Assert.Single(imageView.ImageShow.Visuals.OfType<DVCircleText>());
            Assert.Equal(new Point(640, 360), circle.Center);
            Assert.Equal(200, circle.Radius);
            Assert.Equal($"{Environment.NewLine} X:640{Environment.NewLine}Y:360", circle.Attribute.Text);
            Assert.NotNull(circle.Drawing);

            List<DVRectangleText> rectangles = imageView.ImageShow.Visuals.OfType<DVRectangleText>().ToList();
            Assert.Equal(2, rectangles.Count);

            DVRectangleText mtf = Assert.Single(rectangles, rectangle => rectangle.ID == 1);
            Assert.Equal(new Rect(125, 80, 48, 32), mtf.Attribute.Rect);
            Assert.Equal(0.7364.ToString("F3", CultureInfo.CurrentCulture), mtf.Attribute.Msg);
            Assert.Equal(string.Empty, mtf.Attribute.Text);
            Assert.NotNull(mtf.Drawing);

            DVRectangleText defect = Assert.Single(rectangles, rectangle => rectangle.ID == 17);
            Assert.Equal(new Rect(15, 25, 36, 18), defect.Attribute.Rect);
            Assert.Equal("17", defect.Attribute.Text);
            Assert.StartsWith("type:line", defect.Attribute.Msg, StringComparison.Ordinal);
            Assert.NotNull(defect.Drawing);

            Assert.All(imageView.ImageShow.Visuals.OfType<DrawingVisualBase>(), visual =>
            {
                Assert.Null(visual.BaseAttribute.Name);
                Assert.Null(visual.BaseAttribute.Tag);
            });
        });
    }

    [Fact]
    public void DistortionCircleKeepsBusinessGeometryAndCoordinateText()
    {
        RunOnStaThread(() =>
        {
            Point center = new(640, 360);
            string text = $"{Environment.NewLine} X:{center.X:F0}{Environment.NewLine}Y:{center.Y:F0}";
            CircleTextProperties properties = new()
            {
                Center = center,
                Radius = 200,
                Brush = Brushes.Transparent,
                Pen = new Pen(Brushes.Red, 1),
                Text = text,
            };
            CountingCircleText circle = new(properties);
            circle.TextAttribute.FontSize = 20;

            Assert.Equal(center, circle.Attribute.Center);
            Assert.Equal(200, circle.Attribute.Radius);
            Assert.Equal(200, circle.Attribute.RadiusY);
            Assert.Equal(text, circle.Attribute.Text);
            Assert.Null(circle.Attribute.Msg);
            Assert.Equal(20, circle.Attribute.FontSize);
            Assert.Equal(new Rect(440, 160, 400, 400), circle.GetRect());
            Assert.Null(circle.Drawing);

            using DrawCanvas canvas = CreateArvrOverlayCanvas();
            canvas.AddVisual(circle);
            Assert.Equal(1, circle.RenderCount);
            Assert.Equal(8, circle.Attribute.FontSize);

            List<Drawing> drawings = GetDrawings(circle);
            GeometryDrawing shape = Assert.Single(drawings.OfType<GeometryDrawing>());
            EllipseGeometry geometry = Assert.IsType<EllipseGeometry>(shape.Geometry);
            Assert.Equal(center, geometry.Center);
            Assert.Equal(200, geometry.RadiusX);
            Assert.Equal(200, geometry.RadiusY);
            AssertShapeStyle(shape, Colors.Red, 0.8);
            Assert.True(drawings.OfType<GlyphRunDrawing>().Count() >= 2);
        });
    }

    [Fact]
    public void MtfRectangleKeepsRegionIdAndMessageOnlyRendering()
    {
        RunOnStaThread(() =>
        {
            Rect region = new(125, 80, 48, 32);
            string message = 0.7364.ToString("F3", CultureInfo.CurrentCulture);
            CountingRectangleText rectangle = new(new RectangleTextProperties
            {
                Rect = region,
                Brush = Brushes.Transparent,
                Pen = new Pen(Brushes.Red, 1),
                Id = 3,
                Msg = message,
            });

            Assert.Equal(region, rectangle.Attribute.Rect);
            Assert.Equal(region, rectangle.GetRect());
            Assert.Equal(3, rectangle.Attribute.Id);
            Assert.Equal(string.Empty, rectangle.Attribute.Text);
            Assert.Equal(message, rectangle.Attribute.Msg);
            Assert.Equal(10, rectangle.Attribute.FontSize);
            Assert.Null(rectangle.Drawing);

            using DrawCanvas canvas = CreateArvrOverlayCanvas();
            canvas.AddVisual(rectangle);
            Assert.Equal(1, rectangle.RenderCount);
            Assert.Equal(8, rectangle.Attribute.FontSize);

            List<Drawing> drawings = GetDrawings(rectangle);
            GeometryDrawing shape = Assert.Single(drawings.OfType<GeometryDrawing>());
            RectangleGeometry geometry = Assert.IsType<RectangleGeometry>(shape.Geometry);
            Assert.Equal(region, geometry.Rect);
            AssertShapeStyle(shape, Colors.Red, 0.8);
            Assert.NotEmpty(drawings.OfType<GlyphRunDrawing>());
        });
    }

    [Fact]
    public void ScreenDefectRectangleKeepsIdTextAndMultilineDiagnosticMessage()
    {
        RunOnStaThread(() =>
        {
            Rect region = new(15, 25, 36, 18);
            const int defectId = 17;
            string message =
                $"type:line{Environment.NewLine}" +
                $"area:{12.34567.ToString("F4", CultureInfo.InvariantCulture)}{Environment.NewLine}" +
                $"contrast:{0.12567.ToString("F4", CultureInfo.InvariantCulture)}{Environment.NewLine}" +
                $"mean:{128.25.ToString("F4", CultureInfo.InvariantCulture)}{Environment.NewLine}" +
                $"local:{126.75.ToString("F4", CultureInfo.InvariantCulture)}";
            CountingRectangleText rectangle = new(new RectangleTextProperties
            {
                Rect = region,
                Brush = Brushes.Transparent,
                Pen = new Pen(Brushes.OrangeRed, 1),
                Id = defectId,
                Text = defectId.ToString(CultureInfo.InvariantCulture),
                Msg = message,
            });

            Assert.Equal(region, rectangle.Attribute.Rect);
            Assert.Equal(region, rectangle.GetRect());
            Assert.Equal(defectId, rectangle.Attribute.Id);
            Assert.Equal("17", rectangle.Attribute.Text);
            Assert.Equal(message, rectangle.Attribute.Msg);
            Assert.Equal(10, rectangle.Attribute.FontSize);
            Assert.Null(rectangle.Drawing);

            using DrawCanvas canvas = CreateArvrOverlayCanvas();
            canvas.AddVisual(rectangle);
            Assert.Equal(1, rectangle.RenderCount);
            Assert.Equal(8, rectangle.Attribute.FontSize);

            List<Drawing> drawings = GetDrawings(rectangle);
            GeometryDrawing shape = Assert.Single(drawings.OfType<GeometryDrawing>());
            RectangleGeometry geometry = Assert.IsType<RectangleGeometry>(shape.Geometry);
            Assert.Equal(region, geometry.Rect);
            AssertShapeStyle(shape, Colors.OrangeRed, 0.8);
            Assert.True(drawings.OfType<GlyphRunDrawing>().Count() >= 2);
        });
    }

    private static List<Drawing> GetDrawings(DrawingVisual visual)
    {
        DrawingGroup root = Assert.IsType<DrawingGroup>(visual.Drawing);
        Assert.NotEmpty(root.Children);
        Assert.False(root.Bounds.IsEmpty);

        List<Drawing> drawings = [];
        AddDrawings(root, drawings);
        return drawings;
    }

    private static void AddDrawings(Drawing drawing, List<Drawing> drawings)
    {
        if (drawing is DrawingGroup group)
        {
            foreach (Drawing child in group.Children)
                AddDrawings(child, drawings);
            return;
        }

        drawings.Add(drawing);
    }

    private static DrawCanvas CreateArvrOverlayCanvas()
    {
        return new DrawCanvas
        {
            IsLayoutUpdated = false,
            Scale = 1,
            TextFontSizeOverride = 8,
        };
    }

    private static void AssertShapeStyle(GeometryDrawing shape, Color strokeColor, double thickness)
    {
        SolidColorBrush fill = Assert.IsType<SolidColorBrush>(shape.Brush);
        Assert.Equal(Colors.Transparent, fill.Color);
        Pen pen = Assert.IsType<Pen>(shape.Pen);
        SolidColorBrush stroke = Assert.IsType<SolidColorBrush>(pen.Brush);
        Assert.Equal(strokeColor, stroke.Color);
        Assert.Equal(thickness, pen.Thickness, 6);
    }

    private sealed class CountingCircleText : DVCircleText
    {
        public CountingCircleText(CircleTextProperties properties) : base(properties)
        {
        }

        public int RenderCount { get; private set; }

        public override void Render()
        {
            RenderCount++;
            base.Render();
        }
    }

    private sealed class CountingRectangleText : DVRectangleText
    {
        public CountingRectangleText(RectangleTextProperties properties) : base(properties)
        {
        }

        public int RenderCount { get; private set; }

        public override void Render()
        {
            RenderCount++;
            base.Render();
        }
    }

    private static void RunOnStaThread(Action action)
    {
        Dispatcher dispatcher = WpfDispatcher.Value;
        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }

    private static Dispatcher CreateWpfDispatcher()
    {
        Dispatcher? dispatcher = null;
        Exception? startupFailure = null;
        using ManualResetEventSlim ready = new();
        Thread thread = new(() =>
        {
            try
            {
                Application application = Application.Current ?? new Application();
                application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                Authorization.Instance ??= new Authorization();
                application.Resources["TextBox.Small"] = new Style(typeof(TextBox));
                application.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
                application.Resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
                application.Resources["ToolBarImage"] = new Style(typeof(Image));
                application.Resources["BaseStyle"] = new Style(typeof(Control));
                application.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
                application.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
                dispatcher = Dispatcher.CurrentDispatcher;
            }
            catch (Exception ex)
            {
                startupFailure = ex;
            }
            finally
            {
                ready.Set();
            }

            if (dispatcher != null)
                Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "ProjectARVRPro Tests WPF Host",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();

        if (startupFailure != null)
            throw new InvalidOperationException("Unable to start the ProjectARVRPro WPF test host.", startupFailure);
        return dispatcher ?? throw new InvalidOperationException("The ProjectARVRPro WPF test host did not create a dispatcher.");
    }
}
