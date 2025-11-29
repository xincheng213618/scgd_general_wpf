using log4net;
using Newtonsoft.Json;
using System.IO;

namespace ColorVision.UI.Plugins
{
    /// <summary>
    /// Extracts a plugin with all its stripped dependencies to a standalone folder
    /// </summary>
    public static class PluginExtractor
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PluginExtractor));

        /// <summary>
        /// Extracts a plugin and all its dependencies to the specified output folder
        /// </summary>
        /// <param name="pluginInfo">The plugin to extract</param>
        /// <param name="outputFolder">The target folder (should be empty)</param>
        /// <returns>True if extraction succeeded, false otherwise</returns>
        public static bool ExtractPlugin(PluginInfo pluginInfo, string outputFolder)
        {
            if (pluginInfo == null || string.IsNullOrEmpty(outputFolder))
                return false;

            try
            {
                // Create output directory if it doesn't exist
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
                    // Try to load deps.json from the plugin directory
                    string[] depsFiles = Directory.GetFiles(pluginDirectory, "*.deps.json");
                    if (depsFiles.Length == 1)
                    {
                        string json = File.ReadAllText(depsFiles[0]);
                        var depsObj = JsonConvert.DeserializeObject<DepsJson>(json);
                        if (depsObj != null)
                        {
                            CopyDependencies(depsObj, appBaseDirectory, outputFolder);
                        }
                    }
                }

                // 3. Copy native runtime DLLs if they exist (e.g., runtimes folder for OpenCvSharp)
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
        /// Copies all dependencies from the deps.json to the output folder
        /// </summary>
        private static void CopyDependencies(DepsJson depsJson, string sourceDirectory, string outputFolder)
        {
            if (depsJson?.Targets == null)
                return;

            var mainTargetDict = depsJson.Targets.Values.FirstOrDefault();
            if (mainTargetDict == null)
                return;

            // Collect all dependencies from all packages
            var allDependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var package in mainTargetDict)
            {
                // Add the package itself (extract package name from "PackageName/Version")
                string packageName = package.Key.Split('/')[0];
                allDependencies.Add(packageName);

                // Add all dependencies of this package
                if (package.Value?.Dependencies != null)
                {
                    foreach (var dep in package.Value.Dependencies)
                    {
                        allDependencies.Add(dep.Key);
                    }
                }
            }

            // Copy each dependency DLL from source to output
            foreach (var depName in allDependencies)
            {
                // Skip system assemblies
                if (IsSystemAssembly(depName))
                    continue;

                // Try to find and copy the DLL
                string dllName = depName + ".dll";
                string sourcePath = Path.Combine(sourceDirectory, dllName);

                if (File.Exists(sourcePath))
                {
                    string destPath = Path.Combine(outputFolder, dllName);
                    if (!File.Exists(destPath))
                    {
                        File.Copy(sourcePath, destPath, false);
                        log.Debug($"Copied dependency: {dllName}");
                    }
                }
            }
        }

        /// <summary>
        /// Checks if an assembly is a system assembly that should be skipped
        /// </summary>
        private static bool IsSystemAssembly(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
                return true;

            // Skip .NET runtime and common Microsoft assemblies
            string[] systemPrefixes = new[]
            {
                "System.",
                "Microsoft.",
                "mscorlib",
                "netstandard",
                "WindowsBase",
                "PresentationCore",
                "PresentationFramework"
            };

            foreach (var prefix in systemPrefixes)
            {
                if (assemblyName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                    assemblyName.Equals(prefix.TrimEnd('.'), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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

            // Copy all files
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                if (!File.Exists(destFile))
                {
                    File.Copy(file, destFile, false);
                }
            }

            // Copy all subdirectories recursively
            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir);
            }
        }
    }
}
