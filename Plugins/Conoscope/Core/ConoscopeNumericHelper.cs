using System;
using System.Globalization;

namespace Conoscope.Core
{
    internal static class ConoscopeNumericHelper
    {
        public static bool TryParseDouble(string? text, out double value)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
                || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public static int ClampToInt(int value, int min, int max)
        {
            if (max < min)
            {
                return min;
            }

            return Math.Max(min, Math.Min(value, max));
        }

        public static int NormalizeOddKernelSize(int kernelSize)
        {
            kernelSize = Math.Max(1, kernelSize);
            return kernelSize % 2 == 0 ? kernelSize + 1 : kernelSize;
        }
    }
}