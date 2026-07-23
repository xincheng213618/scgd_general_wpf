using ProjectARVRPro.Process.W51;

namespace ProjectARVRPro.Process.KeyedResults.FieldOfView
{
    internal static class FieldOfViewCompatibility
    {
        public static W51TestResult ToW51TestResult(FieldOfViewTestResult source)
        {
            return new W51TestResult
            {
                HorizontalFieldOfViewAngle = source.HorizontalFieldOfViewAngle,
                VerticalFieldOfViewAngle = source.VerticalFieldOfViewAngle,
                DiagonalFieldOfViewAngle = source.DiagonalFieldOfViewAngle
            };
        }
    }
}
