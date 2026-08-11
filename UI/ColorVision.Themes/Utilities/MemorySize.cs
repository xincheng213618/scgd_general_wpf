using System;

namespace ColorVision.Common.Utilities
{
    internal static class MemorySize
    {
        public static string MemorySizeText(long memorySize)
        {
            // Define unit sizes for better readability and maintainability
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;
            const long TB = GB * 1024;
            const long PB = TB * 1024;

            // Tuple array to hold unit values and their corresponding names
            var units = new[] {
                Tuple.Create(PB, "PB"),
                Tuple.Create(TB, "TB"),
                Tuple.Create(GB, "GB"),
                Tuple.Create(MB, "MB"),
                Tuple.Create(KB, "kB"),
                Tuple.Create(1L, "Byte")
            };

            foreach (var unit in units)
            {
                if (memorySize >= unit.Item1)
                {
                    double value = (double)memorySize / unit.Item1;
                    // Check if we need to format the value to one decimal place
                    if (memorySize < unit.Item1 * 10)
                    {
                        return $"{value:F1} {unit.Item2}";
                    }
                    return $"{(long)value} {unit.Item2}";
                }
            }
            return "0 Byte"; // In case memorySize is 0
        }
    }
}

