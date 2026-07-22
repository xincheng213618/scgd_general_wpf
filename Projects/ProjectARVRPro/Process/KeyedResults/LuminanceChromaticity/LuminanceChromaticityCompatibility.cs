using ProjectARVRPro.Process.W255;

namespace ProjectARVRPro.Process.KeyedResults.LuminanceChromaticity
{
    internal static class LuminanceChromaticityCompatibility
    {
        public static W255TestResult ToW255TestResult(LuminanceChromaticityTestResult source)
        {
            return new W255TestResult
            {
                PoixyuvDatas = source.PoixyuvDatas,
                LuminanceUniformity = source.LuminanceUniformity,
                ColorUniformity = source.ColorUniformity,
                CenterCorrelatedColorTemperature = source.CenterCorrelatedColorTemperature,
                CenterLunimance = source.CenterLuminance,
                CenterCIE1931ChromaticCoordinatesx = source.CenterCIE1931ChromaticCoordinatesx,
                CenterCIE1931ChromaticCoordinatesy = source.CenterCIE1931ChromaticCoordinatesy,
                CenterCIE1976ChromaticCoordinatesu = source.CenterCIE1976ChromaticCoordinatesu,
                CenterCIE1976ChromaticCoordinatesv = source.CenterCIE1976ChromaticCoordinatesv
            };
        }
    }
}
