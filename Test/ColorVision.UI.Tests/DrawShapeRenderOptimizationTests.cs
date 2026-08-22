using ColorVision.ImageEditor.Draw;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class DrawShapeRenderOptimizationTests
{
    [Fact]
    public void SetRectStillRendersAfterThePublicAttributeIsReplaced()
    {
        RunOnStaThread(() =>
        {
            Rect rect = new(20, 30, 80, 60);

            CountingCircle circle = new() { Attribute = new CircleProperties() };
            circle.SetRect(rect);
            Assert.Equal(1, circle.RenderCount);
            Assert.Equal(new Point(60, 60), circle.Center);
            Assert.Equal(30, circle.Radius);

            CountingDatumCircle datumCircle = new() { Attribute = new CircleProperties() };
            datumCircle.SetRect(rect);
            Assert.Equal(1, datumCircle.RenderCount);

            CountingCircleText circleText = new() { Attribute = new CircleTextProperties() };
            circleText.SetRect(rect);
            Assert.Equal(1, circleText.RenderCount);
            Assert.Equal(rect, circleText.GetRect());

            CountingRectangle rectangle = new() { Attribute = new RectangleProperties() };
            rectangle.SetRect(rect);
            Assert.Equal(1, rectangle.RenderCount);

            CountingRectangleText rectangleText = new() { Attribute = new RectangleTextProperties() };
            rectangleText.SetRect(rect);
            Assert.Equal(1, rectangleText.RenderCount);
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
}
