using System;
using System.Collections.Generic;
using Point = System.Windows.Point;

namespace Conoscope.Core
{
    internal enum ConoscopeFixedAxis
    {
        // A fixed H coordinate is a vertical line sampled along V.
        Horizontal,
        // A fixed V coordinate is a horizontal line sampled along H.
        Vertical
    }

    internal readonly record struct ConoscopeHorizontalVerticalSample(
        double PositionDegrees,
        double HorizontalAngle,
        double VerticalAngle,
        double SourceX,
        double SourceY,
        ConoscopeXyzValue Xyz,
        bool IsValid);

    /// <summary>
    /// Samples fixed H/V lines from original polar XYZ pixels, independently of preview resolution.
    /// Invalid geometry is retained as a gap. Channel validity and derived quantities belong to the caller.
    /// </summary>
    internal static class ConoscopeHorizontalVerticalCrossSection
    {
        internal const double MinimumStepDegrees = 0.01;

        public static IReadOnlyList<ConoscopeHorizontalVerticalSample> Sample(
            ConoscopeExportContext context,
            ConoscopeCoordinateSystem coordinateSystem,
            ConoscopeFixedAxis fixedAxis,
            double fixedAngle,
            double stepDegrees)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(context.ReadXyz);
            if (context.ImageWidth <= 0 || context.ImageHeight <= 0)
            {
                throw new ArgumentException("Source image dimensions must be positive.", nameof(context));
            }

            if (!double.IsFinite(context.Center.X) || !double.IsFinite(context.Center.Y)
                || !double.IsFinite(context.PixelsPerDegree) || context.PixelsPerDegree <= 0
                || !double.IsFinite(context.MaxAngle) || context.MaxAngle <= 0 || context.MaxAngle >= 90)
            {
                throw new ArgumentException("Source geometry must be finite, with a positive scale and a polar limit below 90 degrees.", nameof(context));
            }

            if (!ConoscopeHorizontalVerticalProjection.IsProjectedCoordinateSystem(coordinateSystem))
            {
                throw new ArgumentOutOfRangeException(nameof(coordinateSystem));
            }

            if (fixedAxis is not (ConoscopeFixedAxis.Horizontal or ConoscopeFixedAxis.Vertical))
            {
                throw new ArgumentOutOfRangeException(nameof(fixedAxis));
            }

            if (!double.IsFinite(fixedAngle) || Math.Abs(fixedAngle) >= 90)
            {
                throw new ArgumentOutOfRangeException(nameof(fixedAngle));
            }

            if (!double.IsFinite(stepDegrees) || stepDegrees < MinimumStepDegrees)
            {
                throw new ArgumentOutOfRangeException(nameof(stepDegrees));
            }

            double span = 2 * context.MaxAngle;
            int intervals = Math.Max(1, (int)Math.Ceiling(span / stepDegrees));
            // Avoid a near-duplicate endpoint caused only by floating-point division.
            if (intervals > 1 && span - (intervals - 1) * stepDegrees <= Math.Max(1, span) * 1e-12)
            {
                intervals--;
            }

            // MaxAngle < 90 and step >= 0.01 bound this to at most 18,001 points.
            ConoscopeHorizontalVerticalSample[] samples = new ConoscopeHorizontalVerticalSample[intervals + 1];
            ConoscopeXyzValue invalidXyz = new(double.NaN, double.NaN, double.NaN);
            for (int index = 0; index <= intervals; index++)
            {
                double position = index == intervals ? context.MaxAngle : -context.MaxAngle + index * stepDegrees;
                double horizontal = fixedAxis == ConoscopeFixedAxis.Horizontal ? fixedAngle : position;
                double vertical = fixedAxis == ConoscopeFixedAxis.Vertical ? fixedAngle : position;
                bool valid = ConoscopeHorizontalVerticalProjection.TryMapHorizontalVerticalToSource(
                    coordinateSystem, horizontal, vertical, context.Center, context.PixelsPerDegree,
                    context.MaxAngle, context.ImageWidth, context.ImageHeight, out Point source, out _, out _);

                samples[index] = valid
                    ? new(position, horizontal, vertical, source.X, source.Y, ReadBilinear(context, source), true)
                    : new(position, horizontal, vertical, double.NaN, double.NaN, invalidXyz, false);
            }

            return samples;
        }

        private static ConoscopeXyzValue ReadBilinear(ConoscopeExportContext context, Point source)
        {
            // Mapping accepts only [0, width - 1] x [0, height - 1]. A coordinate at width or
            // height is invalid, not clamped. At the last valid pixel no outside neighbour is read.
            int left = (int)Math.Floor(source.X);
            int top = (int)Math.Floor(source.Y);
            int right = left == context.ImageWidth - 1 ? left : left + 1;
            int bottom = top == context.ImageHeight - 1 ? top : top + 1;
            double xFraction = source.X - left;
            double yFraction = source.Y - top;

            ConoscopeXyzValue upper = context.ReadXyz(left, top);
            if (right != left && xFraction > 0)
            {
                upper = Lerp(upper, context.ReadXyz(right, top), xFraction);
            }

            if (bottom == top || yFraction == 0)
            {
                return upper;
            }

            ConoscopeXyzValue lower = context.ReadXyz(left, bottom);
            if (right != left && xFraction > 0)
            {
                lower = Lerp(lower, context.ReadXyz(right, bottom), xFraction);
            }

            return Lerp(upper, lower, yFraction);
        }

        private static ConoscopeXyzValue Lerp(ConoscopeXyzValue first, ConoscopeXyzValue second, double fraction)
        {
            return new(
                first.X * (1 - fraction) + second.X * fraction,
                first.Y * (1 - fraction) + second.Y * fraction,
                first.Z * (1 - fraction) + second.Z * fraction);
        }
    }
}
