#pragma warning disable CS8604
#pragma warning disable CA1863
using ColorVision.Update;
using ColorVision.UI.ServiceHost;
using log4net;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows;

namespace ColorVision.UI.Plugins
{
    public static class PluginUpdater
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PluginUpdater));

        /// <summary>
        /// Deletes one or more plugins.
        /// </summary>
        /// <param name="packageNames">The package names of the plugins to delete.</param>
        public static void DeletePlugin(params string[] packageNames)
        {
            if (packageNames == null || packageNames.Length == 0) return;

            string programPluginsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            List<string> targetPluginDirectories = new();
            foreach (string packageName in packageNames)
            {
                if (TryGetPluginTargetDirectory(programPluginsDirectory, packageName, out string targetPluginDirectory))
                {
                    targetPluginDirectories.Add(targetPluginDirectory);
                }
                else
                {
                    log.Warn($"Ignored invalid plugin directory name during deletion: {packageName}");
                }
            }

            targetPluginDirectories = targetPluginDirectories.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (targetPluginDirectories.Count == 0) return;

            ConfigService.Instance.SaveConfigs();
            PluginLoaderrConfig.Instance.Save();

            string tempDirectory = Path.Combine(Path.GetTempPath(), $"ColorVisionPluginsUpdate-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);
            string batchFilePath = Path.Combine(tempDirectory, "update.bat");
            string executableName = Path.GetFileName(Environment.ProcessPath) ?? "ColorVision.exe";
            string executablePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, executableName);

            var batchContentBuilder = new StringBuilder();
            batchContentBuilder.AppendLine("@echo off");
            batchContentBuilder.AppendLine("setlocal DisableDelayedExpansion");
            batchContentBuilder.AppendLine($"set \"EXE={EscapeForBatchValue(executableName)}\"");
            batchContentBuilder.AppendLine($"set \"EXEPATH={EscapeForBatchValue(executablePath)}\"");
            batchContentBuilder.AppendLine($"set \"TEMP_DIR={EscapeForBatchValue(tempDirectory)}\"");
            batchContentBuilder.AppendLine("taskkill /f /im \"%EXE%\" >nul 2>nul");
            batchContentBuilder.AppendLine("ping -n 2 127.0.0.1 >nul");

            foreach (string targetPluginDirectory in targetPluginDirectories)
            {
                batchContentBuilder.AppendLine($"set \"TARGET={EscapeForBatchValue(targetPluginDirectory)}\"");
                batchContentBuilder.AppendLine("if exist \"%TARGET%\" rd /s /q \"%TARGET%\"");
            }

            batchContentBuilder.AppendLine("start \"\" /b \"%EXEPATH%\" -c MenuPluginManager");
            batchContentBuilder.AppendLine("start \"\" /b cmd /d /c ping -n 4 127.0.0.1 ^>nul ^& rd /s /q \"%TEMP_DIR%\" 2^>nul");
            batchContentBuilder.AppendLine("endlocal");
            batchContentBuilder.AppendLine("exit /b 0");
            File.WriteAllText(batchFilePath, batchContentBuilder.ToString());

            // 设置批处理文件的启动信息
            ProcessStartInfo startInfo = new()
            {
                FileName = batchFilePath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            if (!ApplicationUpdatePrivilegeBroker.TryPrepareApplicationDirectory())
            {
                startInfo.Verb = "runas"; // 请求管理员权限
                startInfo.WindowStyle = ProcessWindowStyle.Normal;
            }
            Process.Start(startInfo);
            Environment.Exit(0);

        }

        internal static bool TryGetPluginTargetDirectory(string pluginsDirectory, string packageName, out string targetDirectory)
        {
            targetDirectory = string.Empty;
            if (string.IsNullOrWhiteSpace(pluginsDirectory) || string.IsNullOrWhiteSpace(packageName))
                return false;

            try
            {
                string rootDirectory = Path.GetFullPath(pluginsDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                string candidate = Path.GetFullPath(Path.Combine(rootDirectory, packageName.Trim()));
                if (!string.Equals(Path.GetDirectoryName(candidate), rootDirectory, StringComparison.OrdinalIgnoreCase))
                    return false;

                targetDirectory = candidate;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Updates one or more plugins from their downloaded ZIP archives.
        /// 1. Extracts all ZIPs to a unique temporary staging directory under %TEMP%.
        /// 2. Generates a batch script to kill the main process, replace the old plugin files with the new ones, and restart the application.
        /// </summary>
        /// <param name="downloadPaths">Full paths to the downloaded plugin ZIP files.</param>
        public static void UpdatePlugin(params string[] downloadPaths) => UpdatePluginWithRestartArguments("-c MenuPluginManager", downloadPaths);

        public static void UpdatePluginWithRestartArguments(string? restartArguments, params string[] downloadPaths)
        {
            if (downloadPaths == null || downloadPaths.Length == 0) return;

            string? tempRoot = null;
            try
            {
                // 1. 保存配置（原逻辑）
                ConfigService.Instance.SaveConfigs();
                PluginLoaderrConfig.Instance.Save();

                // 2. 定义临时与目标路径
                tempRoot = Path.Combine(Path.GetTempPath(), $"ColorVisionPluginsUpdate-{Guid.NewGuid():N}");
                string stageRoot = Path.Combine(tempRoot, "ColorVision");
                string stagingRoot = Path.Combine(stageRoot, "Plugins"); // Staging for all plugins
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;     // 程序当前目录
                string exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
                string exeName = Path.GetFileName(exePath);

                if (string.IsNullOrEmpty(exeName))
                    throw new InvalidOperationException("Cannot determine the current executable file name.");

                // 3. 创建本次更新独立的临时目录
                Directory.CreateDirectory(stagingRoot);

                // 4. 将不同来源的插件包统一整理为 Plugins/<manifest.id>/
                StagePluginPackages(downloadPaths, stagingRoot, Path.Combine(tempRoot, "Packages"));

                // 5. 生成批处理
                string batchFilePath = Path.Combine(tempRoot, "update.bat");
                GenerateBatchFile(
                    batchFilePath: batchFilePath,
                    baseDir: baseDir,
                    exeName: exeName,
                    restartArguments: restartArguments
                );

                // 6. 启动批处理（管理员权限：如果安装在 Program Files 下）
                var psi = new ProcessStartInfo
                {
                    FileName = batchFilePath,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = tempRoot
                };

                if (!ApplicationUpdatePrivilegeBroker.TryPrepareApplicationDirectory())
                {
                    psi.Verb = "runas";
                    psi.WindowStyle = ProcessWindowStyle.Normal;
                }
                _ = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start plugin update batch.");
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                TryDeleteDirectory(tempRoot);
                log.Error("Plugin update failed before updater batch completed.", ex);
                MessageBox.Show($"Update failed: {ex.Message}");
            }
        }

        private static void TryDeleteDirectory(string? directory)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
            catch (Exception ex)
            {
                log.Warn($"Failed to remove plugin update staging directory '{directory}': {ex.Message}");
            }
        }

        public static int StagePluginPackages(IEnumerable<string> packagePaths, string stagingRoot, string extractionRoot)
        {
            if (packagePaths == null)
                throw new ArgumentNullException(nameof(packagePaths));

            List<string> paths = packagePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (paths.Count == 0)
                throw new InvalidOperationException("No plugin package was provided.");

            HashSet<string> stagedPluginIds = new(StringComparer.OrdinalIgnoreCase);
            foreach (string packagePath in paths)
                StagePluginPackage(packagePath, stagingRoot, extractionRoot, stagedPluginIds);
            return paths.Count;
        }

        public static bool IsPluginPackageFileReady(string? packagePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(packagePath)
                    || !File.Exists(packagePath)
                    || File.Exists(packagePath + ".aria2")
                    || new FileInfo(packagePath).Length == 0)
                {
                    return false;
                }

                string extension = Path.GetExtension(packagePath);
                if (!string.Equals(extension, ".cvxp", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                using ZipArchive archive = ZipFile.OpenRead(packagePath);
                return archive.Entries.Any(entry => !string.IsNullOrEmpty(entry.Name));
            }
            catch
            {
                return false;
            }
        }

        internal static string? StagePluginPackage(string packagePath, string stagingRoot, string extractionRoot, ISet<string>? stagedPluginIds = null)
        {
            if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
                throw new FileNotFoundException("Plugin package was not found.", packagePath);
            if (!IsPluginPackageFileReady(packagePath))
                throw new InvalidDataException("Plugin package is incomplete or invalid.");
            if (string.IsNullOrWhiteSpace(stagingRoot))
                throw new ArgumentException("Plugin staging directory cannot be empty.", nameof(stagingRoot));
            if (string.IsNullOrWhiteSpace(extractionRoot))
                throw new ArgumentException("Plugin extraction directory cannot be empty.", nameof(extractionRoot));

            Directory.CreateDirectory(stagingRoot);
            Directory.CreateDirectory(extractionRoot);

            string packageExtractionDirectory = Path.Combine(extractionRoot, Guid.NewGuid().ToString("N"));
            ZipFile.ExtractToDirectory(packagePath, packageExtractionDirectory);
            if (!Directory.Exists(packageExtractionDirectory)
                || !Directory.EnumerateFileSystemEntries(packageExtractionDirectory).Any())
                throw new InvalidDataException("Plugin package is empty.");

            List<string> manifestPaths = Directory.EnumerateFiles(packageExtractionDirectory, "*", SearchOption.AllDirectories)
                .Where(path => string.Equals(Path.GetFileName(path), "manifest.json", StringComparison.OrdinalIgnoreCase))
                .Where(path => Path.GetRelativePath(packageExtractionDirectory, path)
                    .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries).Length <= 2)
                .ToList();

            if (manifestPaths.Count == 0)
            {
                // Legacy packages without a manifest keep their existing directory layout.
                ZipFile.ExtractToDirectory(packagePath, stagingRoot);
                return null;
            }

            if (manifestPaths.Count > 1)
                throw new InvalidDataException("Plugin package contains more than one top-level manifest.json.");

            PluginManifest? manifest;
            try
            {
                manifest = JsonConvert.DeserializeObject<PluginManifest>(File.ReadAllText(manifestPaths[0]));
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Plugin manifest.json is not valid JSON.", ex);
            }

            string pluginId = manifest?.Id?.Trim() ?? string.Empty;
            if (!TryGetPluginTargetDirectory(stagingRoot, pluginId, out string targetPluginDirectory))
                throw new InvalidDataException("Plugin manifest id must be a valid single directory name.");
            if (stagedPluginIds != null && !stagedPluginIds.Add(pluginId))
                throw new InvalidDataException($"Plugin package '{pluginId}' was supplied more than once.");

            string pluginSourceDirectory = Path.GetDirectoryName(manifestPaths[0])!;
            if (Directory.Exists(targetPluginDirectory))
            {
                OverlayDirectory(pluginSourceDirectory, targetPluginDirectory);
                Directory.Delete(pluginSourceDirectory, recursive: true);
            }
            else
            {
                Directory.Move(pluginSourceDirectory, targetPluginDirectory);
            }
            return pluginId;
        }

        private static void OverlayDirectory(string sourceDirectory, string targetDirectory)
        {
            foreach (string directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(targetDirectory, Path.GetRelativePath(sourceDirectory, directory)));

            foreach (string file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                string targetPath = Path.Combine(targetDirectory, Path.GetRelativePath(sourceDirectory, file));
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                File.Copy(file, targetPath, overwrite: true);
            }
        }

        internal static string EscapeForBatchValue(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            return path.Replace("%", "%%");
        }

        internal static void GenerateBatchFile(
            string batchFilePath,
            string baseDir,
            string exeName,
            string? restartArguments = "-c MenuPluginManager"
        )
        {
            if (string.IsNullOrWhiteSpace(batchFilePath))
                throw new ArgumentException(Properties.Resources.BatchFilePathCannotBeEmpty, nameof(batchFilePath));
            if (string.IsNullOrWhiteSpace(baseDir))
                throw new ArgumentException(Properties.Resources.BaseDirCannotBeEmpty, nameof(baseDir));
            if (string.IsNullOrWhiteSpace(exeName))
                throw new ArgumentException(Properties.Resources.ExeNameCannotBeEmpty, nameof(exeName));

            baseDir = baseDir.TrimEnd('\\');

            var escapedBaseDir = EscapeForBatchValue(baseDir);
            var escapedExePath = EscapeForBatchValue(Path.Combine(baseDir, exeName));

            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("setlocal DisableDelayedExpansion");
            sb.AppendLine("title ColorVision Updater");
            sb.AppendLine();
            sb.AppendLine(string.Format(Properties.Resources.EchoTerminatingProcess, exeName));
            sb.AppendLine($"taskkill /f /im \"{exeName}\" >nul 2>nul");
            sb.AppendLine("ping -n 2 127.0.0.1 >nul");
            sb.AppendLine();
            sb.AppendLine(Properties.Resources.EchoStartCopyingFiles);
            sb.AppendLine(Properties.Resources.RemStagePointsToTemp);
            sb.AppendLine("set \"STAGE=%~dp0ColorVision\"");
            sb.AppendLine($"set \"TARGET={escapedBaseDir}\"");
            sb.AppendLine();

            sb.AppendLine("where robocopy >nul 2>nul");
            sb.AppendLine("if errorlevel 1 goto fallback_copy");
            sb.AppendLine("robocopy \"%STAGE%\" \"%TARGET%\" *.* /E /IS /IT /NFL /NDL /NP /NJH /NJS /R:2 /W:1");
            sb.AppendLine("if errorlevel 8 goto fallback_copy");
            sb.AppendLine("goto copy_done");
            sb.AppendLine();

            sb.AppendLine(":fallback_copy");
            sb.AppendLine(Properties.Resources.EchoUsingXCOPY);
            sb.AppendLine("xcopy /y /e /i \"%STAGE%\\*\" \"%TARGET%\\\" >nul");
            sb.AppendLine("if errorlevel 1 (");
            sb.AppendLine(Properties.Resources.EchoXCOPYFailed);
            sb.AppendLine("  goto fail");
            sb.AppendLine(")");
            sb.AppendLine("goto copy_done");
            sb.AppendLine();

            sb.AppendLine(":copy_done");
            sb.AppendLine(Properties.Resources.EchoCopyComplete);
            sb.AppendLine();

            sb.AppendLine(Properties.Resources.EchoRestartingProgram);
            if (string.IsNullOrWhiteSpace(restartArguments))
            {
                sb.AppendLine($"start \"\" /b \"{escapedExePath}\"");
            }
            else
            {
                sb.AppendLine($"start \"\" /b \"{escapedExePath}\" {restartArguments}");
            }
            sb.AppendLine();

            sb.AppendLine(Properties.Resources.EchoUpdateComplete);
            sb.AppendLine("call :schedule_cleanup");
            sb.AppendLine("endlocal");
            sb.AppendLine("exit /b 0");
            sb.AppendLine();

            sb.AppendLine(":fail");
            if (string.IsNullOrWhiteSpace(restartArguments))
            {
                sb.AppendLine($"start \"\" /b \"{escapedExePath}\"");
            }
            else
            {
                sb.AppendLine($"start \"\" /b \"{escapedExePath}\" {restartArguments}");
            }
            sb.AppendLine("call :schedule_cleanup");
            sb.AppendLine("endlocal");
            sb.AppendLine("exit /b 1");
            sb.AppendLine();
            sb.AppendLine(":schedule_cleanup");
            sb.AppendLine(Properties.Resources.EchoSchedulingCleanup);
            sb.AppendLine("set \"SELF_DIR=%~dp0\"");
            sb.AppendLine("start \"\" /b cmd /d /c ping -n 4 127.0.0.1 ^>nul ^& rd /s /q \"%SELF_DIR%\" 2^>nul");
            sb.AppendLine("exit /b 0");

            File.WriteAllText(batchFilePath, sb.ToString(), Encoding.GetEncoding(936));
        }
    }
}
