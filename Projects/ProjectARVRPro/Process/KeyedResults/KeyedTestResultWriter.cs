using ProjectARVRPro.Process.Chessboard;
using ProjectARVRPro.Process.KeyedResults.FieldOfView;
using ProjectARVRPro.Process.KeyedResults.LuminanceChromaticity;
using ProjectARVRPro.Process.MTF.MTF07.MTFH;
using ProjectARVRPro.Process.MTF.MTF07.MTFV;

namespace ProjectARVRPro.Process.KeyedResults
{
    public static class KeyedTestResultWriter
    {
        public static void Write(ObjectiveTestResult destination, string? key, LuminanceChromaticityTestResult result)
        {
            ArgumentNullException.ThrowIfNull(destination);
            ArgumentNullException.ThrowIfNull(result);

            destination.LuminanceChromaticityTestResults ??= new();
            string outputKey = Write(destination.LuminanceChromaticityTestResults, key, result);
            if (KeyedTestResultDictionary.IsKey(outputKey, "White"))
                destination.W255TestResult = LuminanceChromaticityCompatibility.ToW255TestResult(result);
        }

        public static void Write(ObjectiveTestResult destination, string? key, LuminanceChromaticityYWTestResult result)
        {
            ArgumentNullException.ThrowIfNull(destination);
            ArgumentNullException.ThrowIfNull(result);

            destination.LuminanceChromaticityYWTestResults ??= new();
            Write(destination.LuminanceChromaticityYWTestResults, key, result, "YW");
        }

        public static void Write(ObjectiveTestResult destination, string? key, FieldOfViewTestResult result)
        {
            ArgumentNullException.ThrowIfNull(destination);
            ArgumentNullException.ThrowIfNull(result);

            destination.FieldOfViewTestResults ??= new();
            string outputKey = Write(destination.FieldOfViewTestResults, key, result);
            if (KeyedTestResultDictionary.IsKey(outputKey, "White"))
                destination.W51TestResult = FieldOfViewCompatibility.ToW51TestResult(result);
        }

        public static void Write(ObjectiveTestResult destination, string? key, ChessboardTestResult result)
        {
            ArgumentNullException.ThrowIfNull(destination);
            ArgumentNullException.ThrowIfNull(result);

            destination.ChessboardTestResults ??= new();
            string outputKey = Write(destination.ChessboardTestResults, key, result, "Chessboard");
            if (KeyedTestResultDictionary.IsKey(outputKey, "Chessboard"))
                destination.ChessboardTestResult = result;
        }

        public static void Write(ObjectiveTestResult destination, string? key, MTFH07TestResult result)
        {
            ArgumentNullException.ThrowIfNull(destination);
            ArgumentNullException.ThrowIfNull(result);
            destination.MTFH07TestResults ??= new();
            Write(destination.MTFH07TestResults, key, result, "MTFH07");
        }

        public static void Write(ObjectiveTestResult destination, string? key, MTFV07TestResult result)
        {
            ArgumentNullException.ThrowIfNull(destination);
            ArgumentNullException.ThrowIfNull(result);
            destination.MTFV07TestResults ??= new();
            Write(destination.MTFV07TestResults, key, result, "MTFV07");
        }

        public static string Write<T>(IDictionary<string, T> results, string? key, T result, string defaultKey = "White") where T : class
        {
            ArgumentNullException.ThrowIfNull(results);
            ArgumentNullException.ThrowIfNull(result);

            string outputKey = KeyedTestResultDictionary.NormalizeKey(key, defaultKey);
            KeyedTestResultDictionary.Set(results, outputKey, result);
            return outputKey;
        }
    }
}
