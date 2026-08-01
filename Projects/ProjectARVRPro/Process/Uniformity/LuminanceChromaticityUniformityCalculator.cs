using ColorVision.Engine.Templates.POI.AlgorithmImp;

namespace ProjectARVRPro.Process.Uniformity
{
    public sealed class LuminanceChromaticityUniformityCalculationResult
    {
        public bool Success { get; init; }
        public string ErrorMessage { get; init; } = string.Empty;
        public int PointCount { get; init; }
        public double LuminanceUniformity { get; init; }
        public double ColorUniformity { get; init; }
    }

    public static class LuminanceChromaticityUniformityCalculator
    {
        public const string DefaultLuminanceResultName = "Luminance_uniformity";
        public const string DefaultColorResultName = "Color_uniformity";

        public static string NormalizeResultName(string? value, string defaultValue) =>
            string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();

        public static bool MatchesResultName(string? templateName, string resultName) =>
            templateName?.Contains(resultName, StringComparison.OrdinalIgnoreCase) == true;

        public static LuminanceChromaticityUniformityCalculationResult Calculate(IReadOnlyList<PoiResultCIExyuvData>? points)
        {
            if (points == null || points.Count < 2)
                return Fail("至少需要两个POI才能计算亮度和色度均匀性。");

            if (points.Any(point => point == null || !double.IsFinite(point.Y) || !double.IsFinite(point.u) || !double.IsFinite(point.v)))
                return Fail("修正后的POI中存在无效的Y、u或v数据。");

            if (points.Any(point => point.Y <= 0))
                return Fail("修正后的POI亮度必须全部大于0。");

            double minimumLuminance = points.Min(point => point.Y);
            double maximumLuminance = points.Max(point => point.Y);
            double luminanceUniformity = minimumLuminance / maximumLuminance;
            double colorUniformity = CalculateMaximumDeltaUv(points);
            if (!double.IsFinite(luminanceUniformity) || !double.IsFinite(colorUniformity))
                return Fail("修正后的POI均匀性计算结果无效。");

            return new LuminanceChromaticityUniformityCalculationResult
            {
                Success = true,
                PointCount = points.Count,
                LuminanceUniformity = luminanceUniformity,
                ColorUniformity = colorUniformity
            };
        }

        private static double CalculateMaximumDeltaUv(IReadOnlyList<PoiResultCIExyuvData> points)
        {
            double maximum = 0;
            for (int i = 0; i < points.Count; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                {
                    double deltaU = points[i].u - points[j].u;
                    double deltaV = points[i].v - points[j].v;
                    maximum = Math.Max(maximum, Math.Sqrt(deltaU * deltaU + deltaV * deltaV));
                }
            }

            return maximum;
        }

        private static LuminanceChromaticityUniformityCalculationResult Fail(string errorMessage) => new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
