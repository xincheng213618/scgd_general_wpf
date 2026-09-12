using Conoscope.Core;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;

namespace Conoscope.Tests;

public sealed class ConoscopeCoordinateAxisPresentationTests
{
    [Theory]
    [InlineData(ConoscopeCoordinateSystem.Polar, 0.25, 12)]
    [InlineData(ConoscopeCoordinateSystem.Polar, 0.25, 24)]
    [InlineData(ConoscopeCoordinateSystem.Polar, 1, 12)]
    [InlineData(ConoscopeCoordinateSystem.Polar, 2, 24)]
    [InlineData(ConoscopeCoordinateSystem.HorizontalVertical, 0.25, 24)]
    [InlineData(ConoscopeCoordinateSystem.HorizontalVertical, 1, 12)]
    public void FittedAxisKeepsLabelsInsideImageAtDifferentZoomLevels(ConoscopeCoordinateSystem coordinateSystem, double ratio, double fontSize)
    {
        RunOnStaThread(() =>
        {
            ConoscopeCoordinateAxisParam parameters = new()
            {
                CoordinateSystem = coordinateSystem,
                FontSize = fontSize,
            };
            using ConoscopeCoordinateAxisVisual axis = new(parameters);
            axis.Configure(1000, 1000, new Point(500, 500), 500, 60, 1, ratio);

            GlyphRunDrawing[] labels = EnumerateGlyphs(axis.Drawing).ToArray();
            Assert.NotEmpty(labels);
            Assert.All(labels, label =>
            {
                Assert.True(label.Bounds.Left >= 0 && label.Bounds.Top >= 0, $"Label starts outside the image: {label.Bounds}");
                Assert.True(label.Bounds.Right <= 1000 && label.Bounds.Bottom <= 1000, $"Label ends outside the image: {label.Bounds}");
            });
            Assert.Equal(fontSize, parameters.FontSize);
            Assert.Equal(500, parameters.AxisRadius);
            Assert.Equal(60, parameters.MaxAngle);
        });
    }

    private static IEnumerable<GlyphRunDrawing> EnumerateGlyphs(Drawing drawing)
    {
        if (drawing is GlyphRunDrawing glyph)
        {
            yield return glyph;
        }
        else if (drawing is DrawingGroup group)
        {
            foreach (Drawing child in group.Children)
                foreach (GlyphRunDrawing childGlyph in EnumerateGlyphs(child))
                    yield return childGlyph;
        }
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
        Assert.True(thread.TrySetApartmentState(ApartmentState.STA));
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "STA coordinate-axis test did not finish.");
        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
