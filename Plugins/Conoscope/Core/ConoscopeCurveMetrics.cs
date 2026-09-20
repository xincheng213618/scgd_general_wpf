using System;
using System.Collections.Generic;

namespace Conoscope.Core
{
    internal enum ConoscopeFwhmStatus { Available, InvalidAxis, NoPositivePeak, MissingCrossing, GapAtCrossing }

    internal sealed record ConoscopeFwhmResult(ConoscopeFwhmStatus Status, double PeakAngle, double PeakValue,
        double? LeftHalfMaximum, double? RightHalfMaximum, bool HasMultipleLobes, bool HasMissingSamples)
    {
        public double? Width => Status == ConoscopeFwhmStatus.Available ? RightHalfMaximum - LeftHalfMaximum : null;
    }

    internal static class ConoscopeCurveMetrics
    {
        public static bool SupportsFwhm(ConoscopeCurveSnapshot snapshot) =>
            ((snapshot.ChannelLabel == "Y" && (snapshot.UnitLabel is "cd/m²" or "cd/m2"))
                || (snapshot.ChannelLabel == "Iv" && snapshot.UnitLabel == "cd"))
            && (snapshot.AxisKey == "polar-diameter-angle"
                || snapshot.AxisKey is "HorizontalVertical:H-degrees" or "HorizontalVertical:V-degrees"
                    or "NorthPolar:H-degrees" or "NorthPolar:V-degrees" or "EastPolar:H-degrees" or "EastPolar:V-degrees");

        /// <summary>Returns a display copy; null means that no finite positive normalization peak exists.</summary>
        public static double[]? NormalizeToPeak(IReadOnlyList<double> values)
        {
            ArgumentNullException.ThrowIfNull(values);
            double peak = 0;
            foreach (double value in values) if (double.IsFinite(value)) peak = Math.Max(peak, value);
            if (peak <= 0) return null;
            double[] normalized = new double[values.Count];
            for (int index = 0; index < values.Count; index++)
                normalized[index] = double.IsFinite(values[index]) ? values[index] / peak : double.NaN;
            return normalized;
        }

        /// <summary>
        /// Width of the connected half-maximum lobe containing the largest sampled peak.
        /// Equal peaks select the one nearest zero, then the smaller angle. No baseline is subtracted.
        /// Missing values stop the crossing search; interpolation never bridges a gap.
        /// </summary>
        public static ConoscopeFwhmResult Measure(IReadOnlyList<double> positions, IReadOnlyList<double> values)
        {
            ArgumentNullException.ThrowIfNull(positions);
            ArgumentNullException.ThrowIfNull(values);
            if (positions.Count < 3 || positions.Count != values.Count) return InvalidAxis();
            int peakIndex = -1;
            bool missing = false;
            for (int index = 0; index < positions.Count; index++)
            {
                if (!double.IsFinite(positions[index]) || (index > 0 && positions[index] <= positions[index - 1])) return InvalidAxis();
                if (!double.IsFinite(values[index])) { missing = true; continue; }
                if (values[index] > 0 && (peakIndex < 0 || values[index] > values[peakIndex]
                    || (values[index] == values[peakIndex] && Math.Abs(positions[index]) < Math.Abs(positions[peakIndex]))))
                    peakIndex = index;
            }
            if (peakIndex < 0) return new(ConoscopeFwhmStatus.NoPositivePeak, double.NaN, double.NaN, null, null, false, missing);

            double half = values[peakIndex] / 2;
            (double? left, bool leftGap) = FindCrossing(-1);
            (double? right, bool rightGap) = FindCrossing(1);
            int regions = 0;
            bool inRegion = false;
            foreach (double value in values)
            {
                bool above = double.IsFinite(value) && value >= half;
                if (above && !inRegion) regions++;
                inRegion = above;
            }
            ConoscopeFwhmStatus status = leftGap || rightGap ? ConoscopeFwhmStatus.GapAtCrossing
                : !left.HasValue || !right.HasValue ? ConoscopeFwhmStatus.MissingCrossing : ConoscopeFwhmStatus.Available;
            return new(status, positions[peakIndex], values[peakIndex], left, right, regions > 1, missing);

            (double?, bool) FindCrossing(int direction)
            {
                for (int index = peakIndex + direction; index >= 0 && index < values.Count; index += direction)
                {
                    double outer = values[index], inner = values[index - direction];
                    if (!double.IsFinite(outer)) return (null, true);
                    if (outer <= half)
                    {
                        double fraction = (half - outer) / (inner - outer);
                        return (positions[index] + fraction * (positions[index - direction] - positions[index]), false);
                    }
                }
                return (null, false);
            }
        }

        private static ConoscopeFwhmResult InvalidAxis() => new(ConoscopeFwhmStatus.InvalidAxis, double.NaN, double.NaN, null, null, false, false);
    }
}
