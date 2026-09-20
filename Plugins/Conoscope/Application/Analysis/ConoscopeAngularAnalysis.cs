using Conoscope.Core;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Conoscope.ApplicationServices.Analysis
{
    internal sealed record ConoscopeAngularCurve(double? AzimuthDegrees, IReadOnlyList<double> Positions, IReadOnlyList<double> Values,
        int MirroredRaySamples = 0, int UnavailableMirrorRaySamples = 0);

    /// <summary>
    /// Read-only luminance analysis of the original polar image. Preview projection,
    /// legacy reference sampling, preprocessing and exported source values are not changed.
    /// </summary>
    internal static class ConoscopeAngularAnalysis
    {
        public const double RadialStepDegrees = 0.1;
        public const int AzimuthSampleCount = 360;

        public static IReadOnlyList<ConoscopeAngularCurve> Analyze(ConoscopeExportContext source, CancellationToken cancellationToken = default,
            ConoscopeAngularAnalysisOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(source.ReadXyz);
            if (source.ImageWidth <= 0 || source.ImageHeight <= 0
                || !double.IsFinite(source.Center.X) || !double.IsFinite(source.Center.Y)
                || !double.IsFinite(source.PixelsPerDegree) || source.PixelsPerDegree <= 0
                || !double.IsFinite(source.MaxAngle) || source.MaxAngle <= 0 || source.MaxAngle > 90)
                throw new ArgumentException("Angular analysis requires finite source geometry and a polar limit in (0, 90] degrees.", nameof(source));

            cancellationToken.ThrowIfCancellationRequested();
            options ??= new();
            options.Validate(source.MaxAngle);
            // Resolve the default rectangle once, without allocating a region for every ray.
            if (options.MirrorDirection != ConoscopeMirrorDirection.None)
                options = options with { MirrorRegion = options.GetMirrorRegion(source.MaxAngle) };
            int intervals = (int)Math.Ceiling(source.MaxAngle / RadialStepDegrees);
            if (intervals > 1 && source.MaxAngle - (intervals - 1) * RadialStepDegrees <= 1e-10) intervals--;
            double[] radii = new double[intervals + 1];
            double[] positions = new double[2 * intervals + 1];
            for (int index = 0; index <= intervals; index++)
            {
                double radius = index == intervals ? source.MaxAngle : index * RadialStepDegrees;
                radii[index] = radius;
                positions[intervals + index] = radius;
                positions[intervals - index] = radius == 0 ? 0 : -radius;
            }
            IReadOnlyList<double> sharedPositions = Array.AsReadOnly(positions);
            List<ConoscopeAngularCurve> curves = new(5);
            foreach (double azimuth in new double[] { 0, 45, 90, 135 })
            {
                double phi = azimuth * Math.PI / 180;
                double cos = Math.Cos(phi), sin = Math.Sin(phi);
                double[] values = new double[positions.Length];
                int mirrored = 0, unavailable = 0;
                for (int index = 0; index < positions.Length; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    values[index] = Sample(positions[index], cos, sin, ref mirrored, ref unavailable);
                }
                curves.Add(new(azimuth, sharedPositions, Array.AsReadOnly(values), mirrored, unavailable));
            }

            double[] mean = new double[positions.Length];
            double[] cosines = new double[AzimuthSampleCount], sines = new double[AzimuthSampleCount];
            for (int index = 0; index < AzimuthSampleCount; index++)
            {
                double phi = index * 2 * Math.PI / AzimuthSampleCount;
                cosines[index] = Math.Cos(phi);
                sines[index] = Math.Sin(phi);
            }
            int meanMirrored = 0, meanUnavailable = 0;
            for (int index = 0; index < radii.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // At the pole all azimuths refer to the same point; avoid rounding it 360 times.
                double average = index == 0 ? Sample(0, 1, 0, ref meanMirrored, ref meanUnavailable) : 0;
                if (index != 0)
                {
                    for (int azimuth = 0; azimuth < AzimuthSampleCount; azimuth++)
                    {
                        double value = Sample(radii[index], cosines[azimuth], sines[azimuth], ref meanMirrored, ref meanUnavailable);
                        // A partial ring is not a 360-degree mean. Preserve its missing coverage.
                        if (!double.IsFinite(value)) { average = double.NaN; continue; }
                        average += value / AzimuthSampleCount;
                    }
                }
                // Signed display of a radial mean; source-image pixels are never modified.
                mean[intervals + index] = average;
                mean[intervals - index] = average;
            }
            curves.Add(new(null, sharedPositions, Array.AsReadOnly(mean), meanMirrored, meanUnavailable));
            return curves.AsReadOnly();

            double Sample(double theta, double cos, double sin, ref int mirrored, ref int unavailable)
            {
                double angleX = theta * cos, angleY = theta * sin;
                bool replaced = options.TryMirror(angleX, angleY, source.MaxAngle, out double referenceX, out double referenceY);
                // Keep the default multiplication order to preserve previously published samples bit for bit.
                double value = replaced ? ReadLuminance(source, referenceX, referenceY, 1, 1)
                    : ReadLuminance(source, theta, theta, cos, sin);
                if (replaced)
                {
                    if (double.IsFinite(value)) mirrored++;
                    else unavailable++;
                }
                if (options.Quantity == ConoscopeAngularQuantity.LuminousIntensity && double.IsFinite(value))
                {
                    double projection = Math.Abs(theta) >= 90 ? 0 : Math.Cos(theta * Math.PI / 180);
                    value *= options.AreaSquareMeters * projection;
                }
                return double.IsFinite(value) ? value : double.NaN;
            }
        }

        private static double ReadLuminance(ConoscopeExportContext source, double thetaX, double thetaY, double cos, double sin)
        {
            double x = source.Center.X + thetaX * source.PixelsPerDegree * cos, y = source.Center.Y - thetaY * source.PixelsPerDegree * sin;
            const double roundoff = 1e-9;
            if (x < -roundoff || y < -roundoff || x > source.ImageWidth - 1 + roundoff || y > source.ImageHeight - 1 + roundoff)
                return double.NaN;
            // Only absorb trig roundoff at the last valid pixel, never replicate outside pixels.
            x = Math.Clamp(x, 0, source.ImageWidth - 1);
            y = Math.Clamp(y, 0, source.ImageHeight - 1);
            int left = (int)Math.Floor(x), top = (int)Math.Floor(y);
            int right = Math.Min(left + 1, source.ImageWidth - 1), bottom = Math.Min(top + 1, source.ImageHeight - 1);
            double fx = x - left, fy = y - top;
            double upper = source.ReadXyz(left, top).Y;
            if (fx > 0) upper = upper * (1 - fx) + source.ReadXyz(right, top).Y * fx;
            double value = upper;
            if (fy > 0)
            {
                double lower = source.ReadXyz(left, bottom).Y;
                if (fx > 0) lower = lower * (1 - fx) + source.ReadXyz(right, bottom).Y * fx;
                value = upper * (1 - fy) + lower * fy;
            }
            return double.IsFinite(value) ? value : double.NaN;
        }
    }
}
