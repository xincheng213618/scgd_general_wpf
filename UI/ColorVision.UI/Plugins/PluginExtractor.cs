using log4net;
using Newtonsoft.Json;
using System.IO;
using System.Linq;

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

                // 2. Parse deps.json and copy required dependencies from app base directory
                if (pluginInfo.DepsJson != null)
                {
                    CopyDependencies(pluginInfo.DepsJson, appBaseDirectory, outputFolder);
                }
                else
                {
                    string[] depsFiles = Directory.GetFiles(pluginDirectory, "*. deps. json");
                    if (depsFiles.Length == 1)
                    {
                        string json = File.ReadAllText(depsFiles[0]);
                        var depsJson = JsonConvert.DeserializeObject<DepsJson>(json);
                        if (depsJson != null)
                        {
                            CopyDependencies(depsJson, appBaseDirectory, outputFolder);
                        }
                    }
                }

                // 3.  Copy native runtime DLLs if they exist
                CopyRuntimesFolder(appBaseDirectory, outputFolder);

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
        /// Extracts all DLL information from a DepsJson object
        /// </summary>
        public static List<DllInfo> ExtractAllDlls(DepsJson depsJson)
        {
            var dllList = new List<DllInfo>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (depsJson?.Targets == null)
                return dllList;

            foreach (var target in depsJson.Targets.Values)
            {
                if (target == null) continue;

                foreach (var entry in target.Values)
                {
                    if (entry == null) continue;

                    // Extract from runtime section
                    if (entry.Runtime != null)
                    {
                        foreach (var runtimeKey in entry.Runtime.Keys)
                        {
                            if (string.IsNullOrEmpty(runtimeKey)) continue;

                            string fileName = Path.GetFileName(runtimeKey);
                            if (fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                            {
                                if (seen.Add(fileName))
                                {
                                    dllList.Add(new DllInfo
                                    {
                                        FileName = fileName,
                                        RelativePath = fileName,
                                        IsResource = false
                                    });
                                }
                            }
                        }
                    }

                    // Extract from resources section (localized DLLs)
                    if (entry.Resources != null)
                    {
                        foreach (var kvp in entry.Resources)
                        {
                            string resourcePath = kvp.Key;
                            var resourceInfo = kvp.Value;

                            if (string.IsNullOrEmpty(resourcePath)) continue;

                            string fileName = Path.GetFileName(resourcePath);
                            if (fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
                                !string.IsNullOrEmpty(resourceInfo?.Locale))
                            {
                                // Build relative path: locale/filename (e.g., "zh-CN/WPFHexaEditor.resources.dll")
                                string relativePath = Path.Combine(resourceInfo.Locale, fileName);

                                if (seen.Add(relativePath))
                                {
                                    dllList.Add(new DllInfo
                                    {
                                        FileName = fileName,
                                        RelativePath = relativePath,
                                        IsResource = true,
                                        Locale = resourceInfo.Locale
                                    });
                                }
                            }
                        }
                    }

                    // Extract from runtimeTargets section (native DLLs)
                    if (entry.RuntimeTargets != null)
                    {
                        foreach (var kvp in entry.RuntimeTargets)
                        {
                            string runtimePath = kvp.Key;
                            if (string.IsNullOrEmpty(runtimePath)) continue;

                            // runtimeTargets paths are like "runtimes/win-x64/native/xxx.dll"
                            // These are handled by CopyRuntimesFolder, but we can track them
                            if (runtimePath.EndsWith(". dll", StringComparison.OrdinalIgnoreCase))
                            {
                                if (seen.Add(runtimePath))
                                {
                                    dllList.Add(new DllInfo
                                    {
                                        FileName = Path.GetFileName(runtimePath),
                                        RelativePath = runtimePath,
                                        IsResource = false
                                    });
                                }
                            }
                        }
                    }
                }
            }

            return dllList;
        }

        /// <summary>
        /// Copies all dependencies from the deps.json to the output folder
        /// </summary>
        private static void CopyDependencies(DepsJson depsJson, string sourceDirectory, string outputFolder)
        {
            if (depsJson?.Targets == null)
                return;

            var allDlls = ExtractAllDlls(depsJson);

            log.Debug($"Found {allDlls.Count} DLLs in deps.json");

            foreach (var dllInfo in allDlls)
            {
                // Skip core system assemblies only (not Microsoft.Extensions.*)
                string assemblyName = Path.GetFileNameWithoutExtension(dllInfo.FileName);

                // Handle runtimeTargets separately (they're in runtimes folder)
                if (dllInfo.RelativePath.StartsWith("runtimes", StringComparison.OrdinalIgnoreCase))
                {
                    // These are handled by CopyRuntimesFolder
                    continue;
                }

                string sourcePath = Path.Combine(sourceDirectory, dllInfo.RelativePath);
                string destPath = Path.Combine(outputFolder, dllInfo.RelativePath);

                if (File.Exists(sourcePath))
                {
                    // Create directory structure if needed (for localized resources)
                    string destDir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    if (!File.Exists(destPath))
                    {
                        File.Copy(sourcePath, destPath, false);
                        log.Debug($"Copied dependency: {dllInfo.RelativePath}");
                    }
                }
                else
                {
                    log.Debug($"Dependency not found: {sourcePath}");
                }
            }
        }

        /// <summary>
        /// Copies the runtimes folder if it exists (for native DLLs)
        /// </summary>
        private static void CopyRuntimesFolder(string sourceDirectory, string outputFolder)
        {
            string runtimesSource = Path.Combine(sourceDirectory, "runtimes");
            string runtimesDest = Path.Combine(outputFolder, "runtimes");

            if (Directory.Exists(runtimesSource))
            {
                CopyDirectory(runtimesSource, runtimesDest);
                log.Debug($"Copied runtimes folder");
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