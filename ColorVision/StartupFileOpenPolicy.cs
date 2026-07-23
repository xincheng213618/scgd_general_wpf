using System;
using System.IO;

namespace ColorVision
{
    internal static class StartupFileOpenPolicy
    {
        internal static bool ShouldOpenBeforeMainWindow(string? inputPath)
        {
            if (string.IsNullOrWhiteSpace(inputPath))
                return false;

            string extension = Path.GetExtension(inputPath);
            return string.Equals(extension, ".cvraw", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".cvcie", StringComparison.OrdinalIgnoreCase);
        }
    }
}
