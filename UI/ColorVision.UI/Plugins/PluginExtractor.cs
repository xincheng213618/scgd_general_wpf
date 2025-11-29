using log4net;
using Newtonsoft.Json;
using System.IO;

namespace ColorVision.UI.Plugins
{
    /// <summary>
    /// Represents a DLL to be copied with its relative path
    /// </summary>
    public class DllInfo
    {
        /// <summary>
        /// Just the file name (e.g., "WPFHexaEditor.dll")
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Relative path including subdirectory (e.g., "zh-CN/WPFHexaEditor.resources.dll")
        /// </summary>
        public string RelativePath { get; set; }

        /// <summary>
        /// True if this is a resource/localization DLL
        /// </summary>
        public bool IsResource { get; set; }

        /// <summary>
        /// Locale code if this is a resource DLL (e.g., "zh-CN")
        /// </summary>
        public string Locale { get; set; }
    }

    /// <summary>
    /// Extracts a plugin with all its stripped dependencies to a standalone folder
    /// </summary>
    public static class PluginExtractor
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PluginExtractor));

        private const string StrippedFilesJsonName = "stripped_files.json";

        /// <summary>
        /// Extracts a plugin and all its dependencies to the specified output folder
        /// </summary>
        public static bool ExtractPlugin(PluginInfo pluginInfo, string outputFolder)
        {
            if (pluginInfo == null || string.IsNullOrEmpty(outputFolder))
                return false;

            try
            {
                if (!Directory.Exists(outputFolder))
                {
                    Directory.CreateDirectory(outputFolder);
                }

                string pluginDirectory = pluginInfo.PluginDirectory;
                string appBaseDirectory = AppDomain.CurrentDomain.BaseDirectory;

                if (string.IsNullOrEmpty(pluginDirectory) || !Directory.Exists(pluginDirectory))
                {
                    log.Error($"Plugin directory not found: {pluginDirectory}");
                    return false;
                }

                // 1. Copy all files from the plugin directory
                CopyDirectory(pluginDirectory, outputFolder);

                // 2. Read stripped_files.json and restore stripped dependencies from app base directory
                string strippedFilesPath = Path.Combine(pluginDirectory, StrippedFilesJsonName);
                if (File.Exists(strippedFilesPath))
                {
                    RestoreStrippedFiles(strippedFilesPath, appBaseDirectory, outputFolder);
                }
                else
                {
                    log.Warn($"stripped_files.json not found in plugin directory: {pluginDirectory}");
                }

                log.Info($"Plugin {pluginInfo.Name} extracted to {outputFolder}");
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"Failed to extract plugin: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Restores stripped files from the main app directory using the stripped_files.json manifest
        /// </summary>
        private static void RestoreStrippedFiles(string strippedFilesJsonPath, string sourceDirectory, string outputFolder)
        {
            try
            {
                string json = File.ReadAllText(strippedFilesJsonPath);
                var strippedFiles = JsonConvert.DeserializeObject<List<string>>(json);

                if (strippedFiles == null || strippedFiles.Count == 0)
                {
                    log.Debug("No stripped files to restore");
                    return;
                }

                int copiedCount = 0;
                int skippedCount = 0;

                foreach (var relativePath in strippedFiles)
                {
                    string sourcePath = Path.Combine(sourceDirectory, relativePath);
                    string destPath = Path.Combine(outputFolder, relativePath);

                    if (File.Exists(sourcePath))
                    {
                        // Ensure destination directory exists
                        string? destDir = Path.GetDirectoryName(destPath);
                        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                        {
                            Directory.CreateDirectory(destDir);
                        }

                        if (!File.Exists(destPath))
                        {
                            File.Copy(sourcePath, destPath, false);
                            copiedCount++;
                            log.Debug($"Restored: {relativePath}");
                        }
                    }
                    else
                    {
                        skippedCount++;
                        log.Debug($"Skipped (not found): {relativePath}");
                    }
                }

                log.Info($"Restored {copiedCount} stripped files, skipped {skippedCount}");
            }
            catch (Exception ex)
            {
                log.Error($"Failed to restore stripped files: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Recursively copies a directory
        /// </summary>
        private static void CopyDirectory(string sourceDir, string destDir)
        {
            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                if (!File.Exists(destFile))
                {
                    File.Copy(file, destFile, false);
                }
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir);
            }
        }
    }
}