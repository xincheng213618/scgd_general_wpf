using System;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Cie
{
    public static class CieColorConverter
    {
        private static readonly Lazy<CctUvSample[]> PlanckianCctSamples = new(CreatePlanckianCctSamples);

        private readonly record struct CctUvSample(double TemperatureKelvin, CieChromaticity Uv);

        private static double LinearizeSrgbComponent(int value)
        {
            double normalized = Math.Clamp(value, 0, 255) / 255.0;
            return normalized <= 0.04045
                ? normalized / 12.92
                : Math.Pow((normalized + 0.055) / 1.055, 2.4);
        }

        public static CieXyz RgbToXyz(int r, int g, int b)
        {
            double red = LinearizeSrgbComponent(r);
            double green = LinearizeSrgbComponent(g);
            double blue = LinearizeSrgbComponent(b);

            double x = red * 0.4124564 + green * 0.3575761 + blue * 0.1804375;
            double y = red * 0.2126729 + green * 0.7151522 + blue * 0.0721750;
            double z = red * 0.0193339 + green * 0.1191920 + blue * 0.9503041;

            return new CieXyz(x, y, z);
        }

        public static CieChromaticity RgbToCie1931xy(int r, int g, int b)
        {
            return XyzToCie1931xy(RgbToXyz(r, g, b));
        }

        public static CieChromaticity RgbToCie1976uv(int r, int g, int b)
        {
            return XyzToCie1976uv(RgbToXyz(r, g, b));
        }

        public static CieChromaticity XyzToCie1931xy(CieXyz xyz)
        {
            double sum = xyz.X + xyz.Y + xyz.Z;
            if (!xyz.IsFinite || Math.Abs(sum) < double.Epsilon)
            {
                return CieChromaticity.Empty;
            }

            return new CieChromaticity(xyz.X / sum, xyz.Y / sum);
        }

        public static CieChromaticity XyzToCie1976uv(CieXyz xyz)
        {
            double denominator = xyz.X + 15 * xyz.Y + 3 * xyz.Z;
            if (!xyz.IsFinite || Math.Abs(denominator) < double.Epsilon)
            {
                return CieChromaticity.Empty;
            }

            return new CieChromaticity(
                4 * xyz.X / denominator,
                9 * xyz.Y / denominator);
        }

        public static CieChromaticity XyToCie1976uv(CieChromaticity xy)
        {
            if (!xy.IsFinite)
            {
                return CieChromaticity.Empty;
            }

            double denominator = -2 * xy.X + 12 * xy.Y + 3;
            if (Math.Abs(denominator) < double.Epsilon)
            {
                return CieChromaticity.Empty;
            }

            return new CieChromaticity(
                4 * xy.X / denominator,
                9 * xy.Y / denominator);
        }

        public static CieChromaticity XyToCie1960uv(CieChromaticity xy)
        {
            if (!xy.IsFinite)
            {
                return CieChromaticity.Empty;
            }

            double denominator = -2 * xy.X + 12 * xy.Y + 3;
            if (Math.Abs(denominator) < double.Epsilon)
            {
                return CieChromaticity.Empty;
            }

            return new CieChromaticity(
                4 * xy.X / denominator,
                6 * xy.Y / denominator);
        }

        public static CieChromaticity Uv1976ToXy(CieChromaticity uv)
        {
            if (!uv.IsFinite)
            {
                return CieChromaticity.Empty;
            }

            double denominator = 6 * uv.X - 16 * uv.Y + 12;
            if (Math.Abs(denominator) < double.Epsilon)
            {
                return CieChromaticity.Empty;
            }

            return new CieChromaticity(
                9 * uv.X / denominator,
                4 * uv.Y / denominator);
        }

        public static CieChromaticity Uv1960ToXy(CieChromaticity uv)
        {
            if (!uv.IsFinite)
            {
                return CieChromaticity.Empty;
            }

            double denominator = 3 * uv.X - 8 * uv.Y + 6;
            if (Math.Abs(denominator) < double.Epsilon)
            {
                return CieChromaticity.Empty;
            }

            return new CieChromaticity(
                3 * uv.X / denominator,
                2 * uv.Y / denominator);
        }

        public static CieChromaticity CctToApproximatePlanckianXy(double kelvin)
        {
            double temperature = Math.Clamp(kelvin, 1667, 25000);
            double x = temperature <= 4000
                ? -0.2661239e9 / Math.Pow(temperature, 3) - 0.2343580e6 / Math.Pow(temperature, 2) + 0.8776956e3 / temperature + 0.179910
                : -3.0258469e9 / Math.Pow(temperature, 3) + 2.1070379e6 / Math.Pow(temperature, 2) + 0.2226347e3 / temperature + 0.240390;

            double y;
            if (temperature <= 2222)
            {
                y = -1.1063814 * Math.Pow(x, 3) - 1.34811020 * Math.Pow(x, 2) + 2.18555832 * x - 0.20219683;
            }
            else if (temperature <= 4000)
            {
                y = -0.9549476 * Math.Pow(x, 3) - 1.37418593 * Math.Pow(x, 2) + 2.09137015 * x - 0.16748867;
            }
            else
            {
                y = 3.0817580 * Math.Pow(x, 3) - 5.87338670 * Math.Pow(x, 2) + 3.75112997 * x - 0.37001483;
            }

            return new CieChromaticity(x, y);
        }

        public static CieChromaticity DaylightCctToXy(double kelvin)
        {
            double temperature = Math.Clamp(kelvin, 4000, 25000);
            double x = temperature <= 7000
                ? -4.6070e9 / Math.Pow(temperature, 3) + 2.9678e6 / Math.Pow(temperature, 2) + 0.09911e3 / temperature + 0.244063
                : -2.0064e9 / Math.Pow(temperature, 3) + 1.9018e6 / Math.Pow(temperature, 2) + 0.24748e3 / temperature + 0.237040;
            double y = -3.0 * Math.Pow(x, 2) + 2.870 * x - 0.275;

            return new CieChromaticity(x, y);
        }

        public static CieCctResult EstimateCctAndDuv(CieChromaticity xy)
        {
            CieChromaticity targetUv = XyToCie1960uv(xy);
            if (!targetUv.IsFinite)
            {
                return CieCctResult.Empty;
            }

            double bestTemperature = 0;
            double bestDistanceSquared = double.MaxValue;
            CieChromaticity bestUv = CieChromaticity.Empty;

            foreach (CctUvSample sample in PlanckianCctSamples.Value)
            {
                UpdateBestCctCandidate(targetUv, sample, ref bestTemperature, ref bestDistanceSquared, ref bestUv);
            }

            if (!bestUv.IsFinite)
            {
                return CieCctResult.Empty;
            }

            double duv = Math.Sqrt(bestDistanceSquared);
            if (targetUv.Y < bestUv.Y)
            {
                duv = -duv;
            }

            return new CieCctResult(bestTemperature, duv);
        }

        private static void UpdateBestCctCandidate(
            CieChromaticity targetUv,
            CctUvSample sample,
            ref double bestTemperature,
            ref double bestDistanceSquared,
            ref CieChromaticity bestUv)
        {
            CieChromaticity candidateUv = sample.Uv;
            if (!candidateUv.IsFinite)
            {
                return;
            }

            double distanceSquared = Math.Pow(targetUv.X - candidateUv.X, 2) + Math.Pow(targetUv.Y - candidateUv.Y, 2);
            if (distanceSquared >= bestDistanceSquared)
            {
                return;
            }

            bestTemperature = sample.TemperatureKelvin;
            bestDistanceSquared = distanceSquared;
            bestUv = candidateUv;
        }

        private static CctUvSample[] CreatePlanckianCctSamples()
        {
            int count = (25000 - 1667) / 5 + 1;
            CctUvSample[] samples = new CctUvSample[count];
            int index = 0;
            for (double temperature = 1667; index < samples.Length; temperature += 5, index++)
            {
                samples[index] = new CctUvSample(
                    temperature,
                    XyToCie1960uv(CctToApproximatePlanckianXy(temperature)));
            }

            return samples;
        }

        public static Color ToMarkerColor(int r, int g, int b)
        {
            return Color.FromRgb((byte)Math.Clamp(r, 0, 255), (byte)Math.Clamp(g, 0, 255), (byte)Math.Clamp(b, 0, 255));
        }
    }
}
