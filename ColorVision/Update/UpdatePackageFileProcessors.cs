using ColorVision.UI;
using ColorVision.UI.Plugins;
using log4net;
using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace ColorVision.Update
{
    [FileExtension(".cvx")]
    public class IncrementalUpdatePackageFileProcessor : IFileProcessor
    {
        public int Order => 1;

        public void Export(string filePath)
        {

        }
        public bool Process(string filePath)
        {
            if (!AutoUpdater.IsIncrementalPackageFileReady(filePath)) return false;

            AutoUpdater.RestartIsIncrementApplication(filePath);
            return true;
        }
    }

    [FileExtension(".cvxp")]
    public class PluginPackageFileProcessor : IFileProcessor
    {
        public int Order => 1;

        public void Export(string filePath)
        {

        }

        public bool Process(string filePath)
        {
            if (!PluginUpdater.IsPluginPackageFileReady(filePath)) return false;

            PluginUpdater.UpdatePlugin(filePath);
            return true;
        }
    }

    [FileExtension(".zip")]
    public class ZipPluginPackageFileProcessor : IFileProcessor
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ZipPluginPackageFileProcessor));

        public int Order => 1;

        public void Export(string filePath)
        {

        }

        public bool Process(string filePath)
        {
            if (!PluginUpdater.IsPluginPackageFileReady(filePath)) return false;

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
