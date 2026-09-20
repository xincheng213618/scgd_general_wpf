using Conoscope.Core;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;

namespace Conoscope.Tests;

public sealed class ConoscopeCoordinateAxisInteractionTests
{
    public static TheoryData<ConoscopeCoordinateSystem, ConoscopeCoordinateReferenceMode> ProjectedModes => new()
    {
        { ConoscopeCoordinateSystem.HorizontalVertical, ConoscopeCoordinateReferenceMode.FixedHorizontal },
        { ConoscopeCoordinateSystem.HorizontalVertical, ConoscopeCoordinateReferenceMode.FixedVertical },
        { ConoscopeCoordinateSystem.NorthPolar, ConoscopeCoordinateReferenceMode.FixedHorizontal },
        { ConoscopeCoordinateSystem.NorthPolar, ConoscopeCoordinateReferenceMode.FixedVertical },
        { ConoscopeCoordinateSystem.EastPolar, ConoscopeCoordinateReferenceMode.FixedHorizontal },
        { ConoscopeCoordinateSystem.EastPolar, ConoscopeCoordinateReferenceMode.FixedVertical },
    };

    [Theory]
    [MemberData(nameof(ProjectedModes))]
    public void FixedMarkerTracksOnlyItsSelectedProjectedAngle(ConoscopeCoordinateSystem coordinateSystem, ConoscopeCoordinateReferenceMode mode)
    {
        RunOnStaThread(() =>
        {
            ConoscopeCoordinateAxisParam parameters = CreateParameters(coordinateSystem, mode);
            using ConoscopeCoordinateAxisVisual axis = new(parameters);
            axis.Configure(1000, 1000, new Point(500, 500), 500, 60, 1, 1);

            Assert.True(axis.UpdateReferenceFromPoint(ToProjectedPoint(-24, 18)));
            Assert.Equal(mode == ConoscopeCoordinateReferenceMode.FixedHorizontal ? -24 : 0, parameters.ReferenceHorizontalAngle, 8);
            Assert.Equal(mode == ConoscopeCoordinateReferenceMode.FixedVertical ? 18 : 0, parameters.ReferenceVerticalAngle, 8);

            Point orthogonalDrag = mode == ConoscopeCoordinateReferenceMode.FixedHorizontal
                ? ToProjectedPoint(-24, -18)
                : ToProjectedPoint(24, 18);
            Assert.False(axis.UpdateReferenceFromPoint(orthogonalDrag));
            Assert.True(axis.UpdateReferenceFromPoint(ToProjectedPoint(-12, 6)));
            Assert.Equal(mode == ConoscopeCoordinateReferenceMode.FixedHorizontal ? -12 : 0, parameters.ReferenceHorizontalAngle, 8);
            Assert.Equal(mode == ConoscopeCoordinateReferenceMode.FixedVertical ? 6 : 0, parameters.ReferenceVerticalAngle, 8);

            Assert.False(axis.UpdateReferenceFromPoint(ToProjectedPoint(55, 55)));
            Assert.Equal(mode == ConoscopeCoordinateReferenceMode.FixedHorizontal ? -12 : 0, parameters.ReferenceHorizontalAngle, 8);
            Assert.Equal(mode == ConoscopeCoordinateReferenceMode.FixedVertical ? 6 : 0, parameters.ReferenceVerticalAngle, 8);
            Assert.Equal(47, parameters.ReferenceAngle);
            Assert.Equal(23, parameters.ReferenceRadiusAngle);
        });
    }

    [Theory]
    [MemberData(nameof(ProjectedModes))]
    public void FixedMarkerDrawsTheCorrectLineInsideTheProjectedDomain(ConoscopeCoordinateSystem coordinateSystem, ConoscopeCoordinateReferenceMode mode)
    {
        RunOnStaThread(() =>
        {
            ConoscopeCoordinateAxisParam parameters = CreateParameters(coordinateSystem, mode);
            parameters.ReferenceHorizontalAngle = 30;
            parameters.ReferenceVerticalAngle = -30;
            using ConoscopeCoordinateAxisVisual axis = new(parameters);

            foreach (double ratio in new[] { 0.25, 1, 2 })
            {
                axis.Configure(1000, 1000, new Point(500, 500), 500, 60, 1, ratio);
                var reference = Assert.Single(EnumerateReferenceDrawings(axis.Drawing));
                LineGeometry line = Assert.IsType<LineGeometry>(reference.Drawing.Geometry);
                Assert.Equal(parameters.ReferenceLineWidth / ratio, reference.Drawing.Pen.Thickness);

                if (mode == ConoscopeCoordinateReferenceMode.FixedHorizontal)
                {
                    Assert.Equal(new Point(750, 0), line.StartPoint);
                    Assert.Equal(new Point(750, 1000), line.EndPoint);
                }
                else
                {
                    Assert.Equal(new Point(0, 750), line.StartPoint);
                    Assert.Equal(new Point(1000, 750), line.EndPoint);
                }

                Assert.NotNull(reference.Clip);
                Point midpoint = new((line.StartPoint.X + line.EndPoint.X) / 2, (line.StartPoint.Y + line.EndPoint.Y) / 2);
                Assert.True(reference.Clip.FillContains(midpoint));
                Assert.False(reference.Clip.FillContains(line.StartPoint));
                Assert.False(reference.Clip.FillContains(line.EndPoint));
            }
        });
    }

    [Theory]
    [InlineData(ConoscopeCoordinateReferenceMode.FixedHorizontal)]
    [InlineData(ConoscopeCoordinateReferenceMode.FixedVertical)]
    public void FixedMarkerHasNoImplicitPolarInterpretation(ConoscopeCoordinateReferenceMode mode)
    {
        RunOnStaThread(() =>
        {
            ConoscopeCoordinateAxisParam parameters = CreateParameters(ConoscopeCoordinateSystem.Polar, mode);
            using ConoscopeCoordinateAxisVisual axis = new(parameters);
            axis.Configure(1000, 1000, new Point(500, 500), 500, 60, 1, 1);

            Assert.False(axis.UpdateReferenceFromPoint(new Point(750, 250)));
            Assert.Equal(0, parameters.ReferenceHorizontalAngle);
            Assert.Equal(0, parameters.ReferenceVerticalAngle);
            Assert.Equal(47, parameters.ReferenceAngle);
            Assert.Equal(23, parameters.ReferenceRadiusAngle);
            Assert.Empty(EnumerateReferenceDrawings(axis.Drawing));
        });
    }

    private static ConoscopeCoordinateAxisParam CreateParameters(ConoscopeCoordinateSystem coordinateSystem, ConoscopeCoordinateReferenceMode mode) => new()
    {
        CoordinateSystem = coordinateSystem,
        ReferenceMode = mode,
        ReferenceAngle = 47,
        ReferenceRadiusAngle = 23,
        ReferenceBrush = Brushes.Coral,
        IsTextVisible = false,
    };

    private static Point ToProjectedPoint(double horizontal, double vertical) => new(500 + horizontal / 60 * 500, 500 - vertical / 60 * 500);

    private static IEnumerable<(GeometryDrawing Drawing, Geometry? Clip)> EnumerateReferenceDrawings(Drawing drawing, Geometry? clip = null)
    {
        if (drawing is GeometryDrawing geometry && geometry.Pen?.Brush is SolidColorBrush brush && brush.Color == Colors.Coral)
        {
            yield return (geometry, clip);
        }
        else if (drawing is DrawingGroup group)
        {
            foreach (Drawing child in group.Children)
                foreach (var reference in EnumerateReferenceDrawings(child, group.ClipGeometry ?? clip))
                    yield return reference;
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
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "STA coordinate-axis interaction test did not finish.");
        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
