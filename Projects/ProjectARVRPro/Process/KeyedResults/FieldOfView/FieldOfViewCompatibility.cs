using ProjectARVRPro.Process.W51;

namespace ProjectARVRPro.Process.KeyedResults.FieldOfView
{
    internal static class FieldOfViewCompatibility
    {
        internal static bool IsProjectionOf(W51TestResult? legacy, FieldOfViewTestResult source)
        {
            return legacy != null
                && ReferenceEquals(legacy.HorizontalFieldOfViewAngle, source.HorizontalFieldOfViewAngle)
                && ReferenceEquals(legacy.VerticalFieldOfViewAngle, source.VerticalFieldOfViewAngle)
                && ReferenceEquals(legacy.DiagonalFieldOfViewAngle, source.DiagonalFieldOfViewAngle);
        }

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
