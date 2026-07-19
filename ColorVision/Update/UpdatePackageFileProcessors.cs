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
    public class IncrementalUpdatePackageFileProcessor : IFileOpenActionProcessor
    {
        public int Order => 1;

        public FileOpenRouteResult OpenFile(string filePath)
        {
            if (!AutoUpdater.IsIncrementalPackageFileReady(filePath))
                return new FileOpenRouteResult(true, false, "不是有效的增量更新包。");

            AutoUpdater.RestartIsIncrementApplication(new[] { filePath }, null);
            return new FileOpenRouteResult(true, true);
        }
    }

    [FileExtension(".cvxp")]
    public class PluginPackageFileProcessor : IFileOpenActionProcessor
    {
        public int Order => 1;

        public FileOpenRouteResult OpenFile(string filePath)
        {
            if (!PluginUpdater.IsPluginPackageFileReady(filePath))
                return new FileOpenRouteResult(true, false, "不是有效的插件包。");

            PluginUpdater.UpdatePlugin(filePath);
            return new FileOpenRouteResult(true, true);
        }
    }

    [FileExtension(".zip")]
    public class ZipPluginPackageFileProcessor : IFileOpenActionProcessor
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ZipPluginPackageFileProcessor));

        public int Order => 1;

        public FileOpenRouteResult OpenFile(string filePath)
        {
            if (!PluginUpdater.IsPluginPackageFileReady(filePath))
                return FileOpenRouteResult.NotHandled;

            try
            {
                using ZipArchive archive = ZipFile.OpenRead(filePath);
                if (archive.Entries.Any(IsTopLevelPluginManifest))
                {
                    PluginUpdater.UpdatePlugin(filePath);
                    return new FileOpenRouteResult(true, true);
                }
            }
            catch (Exception ex)
            {
                log.Warn($"Failed to inspect update package '{filePath}': {ex.Message}");
            }

            return FileOpenRouteResult.NotHandled;
        }

        private static bool IsTopLevelPluginManifest(ZipArchiveEntry entry)
        {
            if (!string.Equals(Path.GetFileName(entry.FullName), "manifest.json", StringComparison.OrdinalIgnoreCase))
                return false;

            return entry.FullName.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Length <= 2;
        }
    }
}
