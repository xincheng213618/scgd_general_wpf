using System;
using System.Collections.Generic;

namespace ColorVision.Engine.Templates.POI.AlgorithmImp
{
    /// <summary>Equal-weight CIE 1976 u'v' statistics over the finite samples supplied by the caller.</summary>
    public readonly record struct ChromaticityCenterMetrics(
        int SampleCount,
        int InvalidSampleCount,
        double AverageUPrime,
        double AverageVPrime,
        double RmsToReference,
        double CenterDistanceToReference,
        double SpatialRms)
    {
        public bool IsValid => SampleCount > 0;
    }

    public static class ChromaticityCenterCalculator
    {
        // CIE standard illuminant D65 xy=(0.31271, 0.32902), converted to CIE 1976 u'v'.
        public const double D65UPrime = 0.197829449517778;
        public const double D65VPrime = 0.468332168241386;

        public static ChromaticityCenterMetrics Calculate(
            IEnumerable<(double UPrime, double VPrime)>? samples,
            double referenceUPrime = D65UPrime,
            double referenceVPrime = D65VPrime)
        {
            if (!double.IsFinite(referenceUPrime) || !double.IsFinite(referenceVPrime))
                throw new ArgumentException("参考白 u'v' 必须是有限数值。");

            int count = 0;
            int invalidCount = 0;
            double meanU = 0;
            double meanV = 0;
            double m2U = 0;
            double m2V = 0;
            double squaredDistanceToReference = 0;
            if (samples != null)
            {
                foreach ((double u, double v) in samples)
                {
                    if (!double.IsFinite(u) || !double.IsFinite(v))
                    {
                        invalidCount++;
                        continue;
                    }

                    count++;
                    double deltaU = u - meanU;
                    double deltaV = v - meanV;
                    meanU += deltaU / count;
                    meanV += deltaV / count;
                    m2U += deltaU * (u - meanU);
                    m2V += deltaV * (v - meanV);

                    double referenceDeltaU = u - referenceUPrime;
                    double referenceDeltaV = v - referenceVPrime;
                    squaredDistanceToReference += referenceDeltaU * referenceDeltaU + referenceDeltaV * referenceDeltaV;
                }
            }

            if (count == 0)
                return new ChromaticityCenterMetrics(0, invalidCount, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);

            double centerDeltaU = meanU - referenceUPrime;
            double centerDeltaV = meanV - referenceVPrime;
            return new ChromaticityCenterMetrics(
                count,
                invalidCount,
                meanU,
                meanV,
                Math.Sqrt(Math.Max(0, squaredDistanceToReference / count)),
                Math.Sqrt(centerDeltaU * centerDeltaU + centerDeltaV * centerDeltaV),
                Math.Sqrt(Math.Max(0, (m2U + m2V) / count)));
        }
    }
}
