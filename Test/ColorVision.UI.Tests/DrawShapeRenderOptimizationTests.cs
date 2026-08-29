using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class DrawShapeRenderOptimizationTests
{
    [Fact]
    public void SetRectRendersOnceWithOriginalOrReplacedPublicAttribute()
    {
        RunOnStaThread(() =>
        {
            Rect rect = new(20, 30, 80, 60);

            CountingCircle circle = new() { Attribute = new CircleProperties() };
            int circleRenderCount = circle.RenderCount;
            circle.SetRect(rect);
            Assert.Equal(circleRenderCount + 1, circle.RenderCount);
            Assert.Equal(new Point(60, 60), circle.Center);
            Assert.Equal(30, circle.Radius);

            CountingCircle subscribedCircle = new();
            subscribedCircle.SetRect(rect);
            Assert.Equal(1, subscribedCircle.RenderCount);

            CountingDatumCircle datumCircle = new() { Attribute = new CircleProperties() };
            int datumCircleRenderCount = datumCircle.RenderCount;
            datumCircle.SetRect(rect);
            Assert.Equal(datumCircleRenderCount + 1, datumCircle.RenderCount);

            CountingDatumCircle subscribedDatumCircle = new();
            subscribedDatumCircle.SetRect(rect);
            Assert.Equal(1, subscribedDatumCircle.RenderCount);

            CountingCircleText circleText = new() { Attribute = new CircleTextProperties() };
            int circleTextRenderCount = circleText.RenderCount;
            circleText.SetRect(rect);
            Assert.Equal(circleTextRenderCount + 1, circleText.RenderCount);
            Assert.Equal(rect, circleText.GetRect());

            CountingCircleText subscribedCircleText = new();
            subscribedCircleText.SetRect(rect);
            Assert.Equal(1, subscribedCircleText.RenderCount);

            CountingRectangle rectangle = new() { Attribute = new RectangleProperties() };
            int rectangleRenderCount = rectangle.RenderCount;
            rectangle.SetRect(rect);
            Assert.Equal(rectangleRenderCount + 1, rectangle.RenderCount);

            CountingRectangle subscribedRectangle = new();
            subscribedRectangle.SetRect(rect);
            Assert.Equal(1, subscribedRectangle.RenderCount);

            CountingRectangleText rectangleText = new() { Attribute = new RectangleTextProperties() };
            int rectangleTextRenderCount = rectangleText.RenderCount;
            rectangleText.SetRect(rect);
            Assert.Equal(rectangleTextRenderCount + 1, rectangleText.RenderCount);

            CountingRectangleText subscribedRectangleText = new();
            subscribedRectangleText.SetRect(rect);
            Assert.Equal(1, subscribedRectangleText.RenderCount);
        });
    }

    [Fact]
    public void AttributeReplacementMovesAutomaticRenderingToTheNewShapeProperties()
    {
        RunOnStaThread(() =>
        {
            CountingCircle circle = new();
            circle.Render();
            CircleProperties oldCircle = circle.Attribute;
            CircleProperties newCircle = new();
            circle.Attribute = newCircle;
            Assert.Equal(2, circle.RenderCount);
            newCircle.Center = new Point(12, 18);
            Assert.Equal(3, circle.RenderCount);
            oldCircle.Center = new Point(30, 40);
            Assert.Equal(3, circle.RenderCount);

            CountingCircleText circleText = new();
            circleText.Render();
            CircleTextProperties oldCircleText = circleText.Attribute;
            CircleTextProperties newCircleText = new();
            circleText.Attribute = newCircleText;
            Assert.Equal(2, circleText.RenderCount);
            newCircleText.Text = "new circle";
            Assert.Equal(3, circleText.RenderCount);
            oldCircleText.Text = "old circle";
            Assert.Equal(3, circleText.RenderCount);

            CountingDatumCircle datumCircle = new();
            datumCircle.Render();
            CircleProperties oldDatumCircle = datumCircle.Attribute;
            CircleProperties newDatumCircle = new();
            datumCircle.Attribute = newDatumCircle;
            Assert.Equal(2, datumCircle.RenderCount);
            newDatumCircle.Radius = 14;
            Assert.Equal(3, datumCircle.RenderCount);
            oldDatumCircle.Radius = 15;
            Assert.Equal(3, datumCircle.RenderCount);

            CountingRectangle rectangle = new();
            rectangle.Render();
            RectangleProperties oldRectangle = rectangle.Attribute;
            RectangleProperties newRectangle = new();
            rectangle.Attribute = newRectangle;
            Assert.Equal(2, rectangle.RenderCount);
            newRectangle.Rect = new Rect(1, 2, 30, 40);
            Assert.Equal(3, rectangle.RenderCount);
            oldRectangle.Rect = new Rect(2, 3, 40, 50);
            Assert.Equal(3, rectangle.RenderCount);

            CountingRectangleText rectangleText = new();
            rectangleText.Render();
            RectangleTextProperties oldRectangleText = rectangleText.Attribute;
            RectangleTextProperties newRectangleText = new();
            rectangleText.Attribute = newRectangleText;
            Assert.Equal(2, rectangleText.RenderCount);
            newRectangleText.Text = "new rectangle";
            Assert.Equal(3, rectangleText.RenderCount);
            oldRectangleText.Text = "old rectangle";
            Assert.Equal(3, rectangleText.RenderCount);

            CountingDatumRectangle datumRectangle = new();
            datumRectangle.Render();
            RectangleProperties oldDatumRectangle = datumRectangle.Attribute;
            RectangleProperties newDatumRectangle = new();
            datumRectangle.Attribute = newDatumRectangle;
            Assert.Equal(2, datumRectangle.RenderCount);
            newDatumRectangle.Rect = new Rect(3, 4, 50, 60);
            Assert.Equal(3, datumRectangle.RenderCount);
            oldDatumRectangle.Rect = new Rect(4, 5, 60, 70);
            Assert.Equal(3, datumRectangle.RenderCount);

            CountingDatumRectangle manualDatumRectangle = new() { AutoAttributeChanged = false };
            manualDatumRectangle.Render();
            manualDatumRectangle.Attribute = new RectangleProperties();
            manualDatumRectangle.Attribute.Rect = new Rect(5, 6, 70, 80);
            Assert.Equal(1, manualDatumRectangle.RenderCount);
        });
    }

    [Fact]
    public void AttributeReplacementUsesTheNewLayoutScaleBaselines()
    {
        RunOnStaThread(() =>
        {
            DrawingVisualScaleContext context = new(false, 1, 0);

            DVCircleText circle = new(new CircleTextProperties { Pen = new Pen(Brushes.Red, 2), FontSize = 20 });
            circle.ApplyLayoutScale(context);
            circle.Attribute = new CircleTextProperties { Pen = new Pen(Brushes.Blue, 5), FontSize = 50 };
            circle.ApplyLayoutScale(context);
            Assert.Equal(5, circle.Pen.Thickness);
            Assert.Equal(50, circle.TextAttribute.FontSize);

            DVRectangleText rectangle = new(new RectangleTextProperties { Pen = new Pen(Brushes.Red, 2), FontSize = 20 });
            rectangle.ApplyLayoutScale(context);
            rectangle.Attribute = new RectangleTextProperties { Pen = new Pen(Brushes.Blue, 5), FontSize = 50 };
            rectangle.ApplyLayoutScale(context);
            Assert.Equal(5, rectangle.Pen.Thickness);
            Assert.Equal(50, rectangle.TextAttribute.FontSize);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FrozenPenLayoutScalingRendersEachShapeOnce(bool replaceAttribute)
    {
        RunOnStaThread(() =>
        {
            Pen frozenPen = new(Brushes.Red, 3);
            frozenPen.Freeze();
            CountingCircle circle = new();
            CountingCircleText circleText = new();
            CountingRectangle rectangle = new();
            CountingRectangleText rectangleText = new();

            if (replaceAttribute)
            {
                circle.Attribute = new CircleProperties { Pen = frozenPen };
                circleText.Attribute = new CircleTextProperties { Pen = frozenPen };
                rectangle.Attribute = new RectangleProperties { Pen = frozenPen };
                rectangleText.Attribute = new RectangleTextProperties { Pen = frozenPen };
            }
            else
            {
                circle.Attribute.Pen = frozenPen;
                circleText.Attribute.Pen = frozenPen;
                rectangle.Attribute.Pen = frozenPen;
                rectangleText.Attribute.Pen = frozenPen;
            }

            int circleRenderCount = circle.RenderCount;
            int circleTextRenderCount = circleText.RenderCount;
            int rectangleRenderCount = rectangle.RenderCount;
            int rectangleTextRenderCount = rectangleText.RenderCount;
            DrawingVisualScaleContext context = new(true, 2, 0);

            circle.ApplyLayoutScale(context);
            circleText.ApplyLayoutScale(context);
            rectangle.ApplyLayoutScale(context);
            rectangleText.ApplyLayoutScale(context);

            Assert.Equal(circleRenderCount + 1, circle.RenderCount);
            Assert.Equal(circleTextRenderCount + 1, circleText.RenderCount);
            Assert.Equal(rectangleRenderCount + 1, rectangle.RenderCount);
            Assert.Equal(rectangleTextRenderCount + 1, rectangleText.RenderCount);
            Assert.Equal(2, circle.Pen.Thickness);
            Assert.Equal(2, circleText.Pen.Thickness);
            Assert.Equal(2, rectangle.Pen.Thickness);
            Assert.Equal(2, rectangleText.Pen.Thickness);
            Assert.Equal(3, frozenPen.Thickness);
        });
    }

    [Fact]
    public void InvalidShapeGeometryIsPreservedAsDataButNeverRenderedAsAFakeRegion()
    {
        RunOnStaThread(() =>
        {
            CircleTextProperties properties = new()
            {
                Center = new Point(50, 40),
                Radius = -12,
                RadiusY = -8,
            };
            DVCircleText circle = new(properties);
            Assert.Equal(-12, properties.Radius);
            Assert.Equal(-8, properties.RadiusY);
            Assert.Equal(new Rect(38, 32, 24, 16), circle.GetRect());

            circle.SetRect(Rect.Empty);
            Assert.Equal(-12, properties.Radius);
            Assert.Equal(-8, properties.RadiusY);
            Assert.Equal(new Rect(38, 32, 24, 16), circle.GetRect());

            properties.Center = new Point(double.NaN, 10);
            Assert.Equal(Rect.Empty, circle.GetRect());
            circle.Render();
            Assert.True(circle.Drawing == null || circle.Drawing.Bounds.IsEmpty);

            Rect invalidRect = new(double.NaN, 20, 40, 30);
            RectangleTextProperties rectangleProperties = new() { Rect = invalidRect };
            DVRectangleText rectangle = new(rectangleProperties);
            Assert.True(double.IsNaN(rectangle.Attribute.Rect.X));
            Assert.Equal(Rect.Empty, rectangle.GetRect());
            rectangle.Render();
            Assert.True(rectangle.Drawing == null || rectangle.Drawing.Bounds.IsEmpty);

            properties.Center = new Point(1.5e308, 1.5e308);
            properties.Radius = 4e307;
            Assert.Equal(Rect.Empty, circle.GetRect());
            circle.Render();
            Assert.True(circle.Drawing == null || circle.Drawing.Bounds.IsEmpty);

            Rect endpointOverflowRect = new(1.5e308, 1.5e308, 4e307, 4e307);
            rectangleProperties.Rect = endpointOverflowRect;
            Assert.True(double.IsPositiveInfinity(endpointOverflowRect.Right));
            Assert.True(double.IsPositiveInfinity(endpointOverflowRect.Bottom));
            Assert.Equal(Rect.Empty, rectangle.GetRect());
            rectangle.Render();
            Assert.True(rectangle.Drawing == null || rectangle.Drawing.Bounds.IsEmpty);
        });
    }

    [Fact]
    public void RectangleTextPositionChangesRenderAndUnknownValuesUseCenter()
    {
        RunOnStaThread(() =>
        {
            RectangleTextProperties properties = new()
            {
                Rect = new Rect(50, 40, 80, 50),
                Brush = Brushes.Transparent,
                Pen = new Pen(Brushes.Red, 1),
                Text = "ABC",
                Position = RectangleTextPosition.Center,
            };
            CountingRectangleText rectangle = new() { Attribute = properties };
            rectangle.Render();
            int renderCount = rectangle.RenderCount;
            byte[] centeredPixels = Rasterize(rectangle);

            properties.Position = RectangleTextPosition.Top;
            Assert.Equal(renderCount + 1, rectangle.RenderCount);

            properties.Position = (RectangleTextPosition)999;
            Assert.Equal(renderCount + 2, rectangle.RenderCount);
            Assert.Equal(centeredPixels, Rasterize(rectangle));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DragPreviewRendersOnceWithOriginalOrReplacedPublicAttribute(bool replaceAttribute)
    {
        RunOnStaThread(() =>
        {
            DrawCanvas drawCanvas = new();
            Zoombox zoombox = new() { Child = drawCanvas, ContentMatrix = Matrix.Identity };
            DrawEditorContext context = new(drawCanvas, zoombox);
            CircleManager circleManager = new(context);
            RectangleManager rectangleManager = new(context);
            CountingCircleText circle = new();
            CountingRectangleText rectangle = new();
            CountingRectangleText centeredRectangle = new();
            if (replaceAttribute)
            {
                circle.Attribute = new CircleTextProperties();
                rectangle.Attribute = new RectangleTextProperties();
                centeredRectangle.Attribute = new RectangleTextProperties();
            }

            int circleRenderCount = circle.RenderCount;
            int rectangleRenderCount = rectangle.RenderCount;
            int centeredRectangleRenderCount = centeredRectangle.RenderCount;

            try
            {
                SetPreviewVisualAndUpdate(circleManager, "DrawCircleCache", circle, new Point(3, 4));
                SetPreviewVisualAndUpdate(rectangleManager, "DrawingRectangleCache", rectangle, new Point(8, 6));
                rectangleManager.Config.UseCenter = true;
                SetPreviewVisualAndUpdate(rectangleManager, "DrawingRectangleCache", centeredRectangle, new Point(8, 6));

                Assert.Equal(circleRenderCount + 1, circle.RenderCount);
                Assert.Equal(5, circle.Attribute.Radius);
                Assert.Equal(rectangleRenderCount + 1, rectangle.RenderCount);
                Assert.Equal(new Rect(0, 0, 8, 6), rectangle.Attribute.Rect);
                Assert.Equal(centeredRectangleRenderCount + 1, centeredRectangle.RenderCount);
                Assert.Equal(new Rect(-8, -6, 16, 12), centeredRectangle.Attribute.Rect);
            }
            finally
            {
                circleManager.Dispose();
                rectangleManager.Dispose();
                drawCanvas.Dispose();
            }
        });
    }

    [Theory]
    [InlineData(12, 7)]
    [InlineData(-12, 7)]
    [InlineData(12, -7)]
    [InlineData(-12, -7)]
    public void CenteredRectangleKeepsTheMouseDownPointAcrossAllQuadrants(double deltaX, double deltaY)
    {
        RunOnStaThread(() =>
        {
            DrawCanvas drawCanvas = new();
            Zoombox zoombox = new() { Child = drawCanvas, ContentMatrix = Matrix.Identity };
            DrawEditorContext context = new(drawCanvas, zoombox);
            RectangleManager manager = new(context);
            CountingRectangleText rectangle = new();
            Point center = new(100, 80);

            try
            {
                manager.Config.UseCenter = true;
                typeof(DragDrawingToolBase)
                    .GetField("<MouseDownPoint>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(manager, center);
                SetPreviewVisualAndUpdate(manager, "DrawingRectangleCache", rectangle, center + new Vector(deltaX, deltaY));

                Assert.Equal(new Rect(88, 73, 24, 14), rectangle.Rect);
                Assert.Equal(1, rectangle.RenderCount);
            }
            finally
            {
                manager.Dispose();
                drawCanvas.Dispose();
            }
        });
    }

    [Theory]
    [InlineData("Main")]
    [InlineData("   ")]
    public void CircleTextReusesMeasuredTextWithoutChangingPixels(string text)
    {
        RunOnStaThread(() =>
        {
            CircleTextProperties properties = new()
            {
                Center = new Point(80, 65),
                Radius = 32,
                RadiusY = 24,
                Brush = Brushes.Transparent,
                Pen = new Pen(Brushes.Red, 1.6),
                IsShowText = true,
                Text = text,
                Msg = "Message",
            };
            properties.TextAttribute.Brush = Brushes.Navy;
            DVCircleText optimized = new(properties);
            optimized.Render();

            DrawingVisual legacy = RenderLegacyCircleText(properties);

            Assert.Equal(Rasterize(legacy), Rasterize(optimized));
        });
    }

    [Fact]
    public void CircleReusesTypefaceAndDpiWithoutChangingPixels()
    {
        RunOnStaThread(() =>
        {
            CircleProperties properties = new()
            {
                Center = new Point(75, 60),
                Radius = 28,
                Brush = Brushes.Transparent,
                Pen = new Pen(Brushes.DarkGreen, 1.5),
                Msg = "Center",
            };
            DVCircle optimized = new(properties) { IsDrawing = true };
            optimized.Render();
            TextAttribute textAttribute = (TextAttribute)typeof(DVCircle)
                .GetField("TextAttribute", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(optimized)!;

            DrawingVisual legacy = RenderLegacyCircle(properties, textAttribute);

            Assert.Equal(Rasterize(legacy), Rasterize(optimized));
        });
    }

    private static DrawingVisual RenderLegacyCircleText(CircleTextProperties properties)
    {
        DrawingVisual visual = new();
        using DrawingContext dc = visual.RenderOpen();
        dc.DrawEllipse(properties.Brush, properties.Pen, properties.Center, properties.Radius, properties.RadiusY);

        double size = 0;
        if (properties.IsShowText)
        {
            FormattedText measured = CreateFormattedText(properties.TextAttribute, properties.Text, properties.TextAttribute.Brush, visual);
            size = measured.Width / 2;
            if (!string.IsNullOrWhiteSpace(properties.Text))
            {
                dc.DrawText(
                    CreateFormattedText(properties.TextAttribute, properties.Text, properties.TextAttribute.Brush, visual),
                    new Point(properties.Center.X - size, properties.Center.Y - measured.Height / 2));
            }
        }

        if (!string.IsNullOrWhiteSpace(properties.Msg))
        {
            FormattedText measured = CreateFormattedText(properties.TextAttribute, properties.Msg, properties.TextAttribute.Brush, visual);
            dc.DrawText(
                CreateFormattedText(properties.TextAttribute, properties.Msg, properties.TextAttribute.Brush, visual),
                new Point(properties.Center.X + size + properties.Radius / 2, properties.Center.Y - measured.Height / 2));
        }
        return visual;
    }

    private static DrawingVisual RenderLegacyCircle(CircleProperties properties, TextAttribute textAttribute)
    {
        DrawingVisual visual = new();
        using DrawingContext dc = visual.RenderOpen();
        string centerText = properties.Center.X.ToString("F0") + "," + properties.Center.Y.ToString("F0");
        dc.DrawEllipse(properties.Brush, properties.Pen, properties.Center, properties.Radius, properties.Radius);
        dc.DrawText(CreateFormattedText(textAttribute, centerText, textAttribute.Brush, visual), properties.Center);
        dc.DrawText(
            CreateFormattedText(textAttribute, properties.Radius.ToString("F2"), textAttribute.Brush, visual),
            new Point(properties.Radius + properties.Center.X, properties.Center.Y));

        if (!string.IsNullOrWhiteSpace(properties.Msg))
        {
            FormattedText message = CreateFormattedText(textAttribute, properties.Msg, textAttribute.Brush, visual);
            dc.DrawText(message, new Point(properties.Center.X - message.Width / 2, properties.Center.Y - message.Height / 2));
        }
        return visual;
    }

    private static FormattedText CreateFormattedText(TextAttribute attribute, string text, Brush brush, Visual visual)
    {
        return new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            attribute.FlowDirection,
            new Typeface(attribute.FontFamily, attribute.FontStyle, attribute.FontWeight, attribute.FontStretch),
            attribute.FontSize,
            brush,
            VisualTreeHelper.GetDpi(visual).PixelsPerDip);
    }

    private static byte[] Rasterize(DrawingVisual visual)
    {
        const int width = 180;
        const int height = 140;
        RenderTargetBitmap bitmap = new(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        byte[] pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        return pixels;
    }

    private static void SetPreviewVisualAndUpdate(object manager, string cacheFieldName, object visual, Point point)
    {
        Type managerType = manager.GetType();
        managerType.GetField(cacheFieldName, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, visual);
        MouseEventArgs mouseEventArgs = new(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseMoveEvent };
        managerType.GetMethod("OnUpdateDraw", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(manager, [point, mouseEventArgs]);
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure != null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private sealed class CountingCircle : DVCircle
    {
        public int RenderCount { get; private set; }
        public override void Render()
        {
            RenderCount++;
            base.Render();
        }
    }

    private sealed class CountingDatumCircle : DVDatumCircle
    {
        public int RenderCount { get; private set; }
        public override void Render()
        {
            RenderCount++;
            base.Render();
        }
    }

    private sealed class CountingCircleText : DVCircleText
    {
        public int RenderCount { get; private set; }
        public override void Render()
        {
            RenderCount++;
            base.Render();
        }
    }

    private sealed class CountingRectangle : DVRectangle
    {
        public int RenderCount { get; private set; }
        public override void Render()
        {
            RenderCount++;
            base.Render();
        }
    }

    private sealed class CountingRectangleText : DVRectangleText
    {
        public int RenderCount { get; private set; }
        public override void Render()
        {
            RenderCount++;
            base.Render();
        }
    }

    private sealed class CountingDatumRectangle : DVDatumRectangle
    {
        public int RenderCount { get; private set; }
        public override void Render()
        {
            RenderCount++;
            base.Render();
        }
    }
}
