using ColorVision.UI;
using ColorVision.UI.Plugins;
using log4net;
using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

namespace ColorVision.Update
{
    [FileExtension(".cvx")]
    public class CVXFileProcess : IFileProcessor
    {
        public int Order => 1;

        public void Export(string filePath)
        {

        }
        public bool Process(string filePath)
        {
            if (!File.Exists(filePath)) return false;

            AutoUpdater.RestartIsIncrementApplication(filePath);
            return true;
        }
    }

    [FileExtension(".cvxp")]
    public class CVXPProcessUpdte : IFileProcessor
    {
        public int Order => 1;

        public void Export(string filePath)
        {

        }

        public bool Process(string filePath)
        {
            if (!File.Exists(filePath)) return false;

            PluginUpdater.UpdatePlugin(filePath);
            return true;
        }
    }

    [FileExtension(".zip")]
    public class FileProcessUpdte : IFileProcessor
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FileProcessUpdte));

        public int Order => 1;

        public void Export(string filePath)
        {

        }

        public bool Process(string filePath)
        {
            if (!File.Exists(filePath)) return false;

            string fileName = Path.GetFileName(filePath);

            if (Regex.IsMatch(fileName, @"^ColorVision-Update-\[.*\]\.zip$", RegexOptions.IgnoreCase))
            {
                AutoUpdater.RestartIsIncrementApplication(filePath);
                return true;
            }

            try
            {
                using ZipArchive archive = ZipFile.OpenRead(filePath);
                if (archive.Entries.Any(IsTopLevelPluginManifest))
                {
                    PluginUpdater.UpdatePlugin(filePath);
                    return true;
                }
            }
            catch (Exception ex)
            {
                log.Warn($"Failed to inspect update package '{filePath}': {ex.Message}");
            }

            return false;
        }

        private static bool IsTopLevelPluginManifest(ZipArchiveEntry entry)
        {
            if (!string.Equals(Path.GetFileName(entry.FullName), "manifest.json", StringComparison.OrdinalIgnoreCase))
                return false;

            return entry.FullName.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Length <= 2;
        }
    }
}
