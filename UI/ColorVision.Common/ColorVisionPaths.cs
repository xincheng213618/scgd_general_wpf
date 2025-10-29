using System;
using System.IO;

namespace ColorVision.Common
{
    /// <summary>
    /// Provides common application paths used throughout ColorVision projects
    /// </summary>
    public static class ColorVisionPaths
    {
        /// <summary>
        /// Gets the base configuration directory path: %AppData%\ColorVision\Config\
        /// </summary>
        public static string ConfigDirectory { get; } = 
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                "ColorVision", 
                "Config");
    }
}
