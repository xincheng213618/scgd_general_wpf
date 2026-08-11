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
    public sealed record PluginPackageStagingPlan(
        IReadOnlyList<string> ManifestPluginIds,
        bool HasLegacyPackages);

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

            string? tempDirectory = null;
            ExitUpdateHandoffState? handoffState = null;
            bool handoffStarted = false;
            try
            {
                string programDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string programPluginsDirectory = Path.Combine(programDirectory, "Plugins");
                EnsureExistingDirectoryIsNotReparsePoint(programDirectory, "ColorVision installation directory", mustExist: true);
                EnsureExistingDirectoryIsNotReparsePoint(programPluginsDirectory, "ColorVision Plugins directory");
                List<string> targetPluginDirectories = new();
                foreach (string packageName in packageNames)
                {
                    if (TryGetPluginTargetDirectory(programPluginsDirectory, packageName, out string targetPluginDirectory))
                    {
                        EnsureExistingDirectoryIsNotReparsePoint(targetPluginDirectory, "Plugin deletion target");
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

                tempDirectory = Path.Combine(Path.GetTempPath(), $"ColorVisionPluginsUpdate-{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempDirectory);
                string batchFilePath = Path.Combine(tempDirectory, "update.bat");
                string executableName = Path.GetFileName(Environment.ProcessPath) ?? "ColorVision.exe";
                string executablePath = Path.Combine(programDirectory, executableName);
                File.WriteAllText(batchFilePath, string.Empty);
                handoffState = ExitUpdateHandoff.Prepare(programDirectory, tempDirectory);
                ApplicationUpdateProcessCoordinator.CloseOtherApplicationProcesses();
                GenerateDeleteBatchFile(batchFilePath, targetPluginDirectories, executablePath, Environment.ProcessId, handoffState);

                ProcessStartInfo startInfo = new()
                {
                    FileName = batchFilePath,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = tempDirectory,
                };
                if (!ApplicationUpdatePrivilegeBroker.TryPrepareApplicationDirectory())
                {
                    startInfo.Verb = "runas";
                    startInfo.WindowStyle = ProcessWindowStyle.Normal;
                }

                using Process updateProcess = ExitUpdateHandoff.Start(handoffState, startInfo);
                handoffStarted = true;
                try
                {
                    ApplicationUpdateShutdown.Request();
                }
                catch (Exception ex)
                {
                    log.Error("Plugin deletion updater started, but application shutdown could not be requested. The handoff remains active.", ex);
                }
            }
            catch (Exception ex)
            {
                if (!handoffStarted)
                {
                    ExitUpdateHandoff.Clear(handoffState);
                    TryDeleteDirectory(tempDirectory);
                    log.Error("Plugin deletion failed before updater batch started.", ex);
                    MessageBox.Show($"Delete failed: {ex.Message}");
                }
                else
                {
                    log.Error("Plugin deletion handoff is active; updater staging and marker were preserved.", ex);
                }
            }
        }

        public static bool TryGetPluginTargetDirectory(string pluginsDirectory, string packageName, out string targetDirectory)
        {
            targetDirectory = string.Empty;
            if (string.IsNullOrWhiteSpace(pluginsDirectory) || string.IsNullOrWhiteSpace(packageName))
                return false;

            try
            {
                string directoryName = packageName.Trim();
                if (directoryName.Length == 0
                    || !string.Equals(directoryName, packageName, StringComparison.Ordinal)
                    || directoryName.EndsWith(' ')
                    || directoryName.EndsWith('.')
                    || directoryName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    return false;
                }

                string rootDirectory = Path.GetFullPath(pluginsDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                string candidate = Path.GetFullPath(Path.Combine(rootDirectory, directoryName));
                if (!string.Equals(Path.GetDirectoryName(candidate), rootDirectory, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(Path.GetFileName(candidate), directoryName, StringComparison.Ordinal))
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
        /// 2. Generates a batch script that waits for the main process to exit, replaces the old plugin files, and restarts the application.
        /// </summary>
        /// <param name="downloadPaths">Full paths to the downloaded plugin ZIP files.</param>
        public static void UpdatePlugin(params string[] downloadPaths) => UpdatePluginWithRestartArguments("-c MenuPluginManager", downloadPaths);

        public static void UpdatePluginWithRestartArguments(string? restartArguments, params string[] downloadPaths)
        {
            if (downloadPaths == null || downloadPaths.Length == 0) return;

            string? tempRoot = null;
            ExitUpdateHandoffState? handoffState = null;
            bool handoffStarted = false;
            try
            {
                // 1. 保存配置（原逻辑）
                ConfigService.Instance.SaveConfigs();
                PluginLoaderrConfig.Instance.Save();

                // 2. 定义临时与目标路径
                tempRoot = Path.Combine(Path.GetTempPath(), $"ColorVisionPluginsUpdate-{Guid.NewGuid():N}");
                string stageRoot = Path.Combine(tempRoot, "ColorVision");
                string stagingRoot = Path.Combine(stageRoot, "Plugins");
                string legacyStagingRoot = Path.Combine(tempRoot, "LegacyOverlay");
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;     // 程序当前目录
                string programPluginsDirectory = Path.Combine(baseDir, "Plugins");
                EnsureExistingDirectoryIsNotReparsePoint(baseDir, "ColorVision installation directory", mustExist: true);
                EnsureExistingDirectoryIsNotReparsePoint(programPluginsDirectory, "ColorVision Plugins directory");
                string exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
                string exeName = Path.GetFileName(exePath);

                if (string.IsNullOrEmpty(exeName))
                    throw new InvalidOperationException("Cannot determine the current executable file name.");

                // 3. 创建本次更新独立的临时目录
                Directory.CreateDirectory(stagingRoot);

                // 4. Manifest packages are complete plugin directories. Legacy packages keep
                // their historical overlay layout in an isolated compatibility stage.
                PluginPackageStagingPlan stagingPlan = StagePluginPackagesForUpdate(
                    downloadPaths,
                    stagingRoot,
                    legacyStagingRoot,
                    Path.Combine(tempRoot, "Packages"));

                // 5. Close other processes from this exact installation, then create and verify
                // a persistent backup for every installed manifest target before any overwrite.
                string batchFilePath = Path.Combine(tempRoot, "update.bat");
                File.WriteAllText(batchFilePath, string.Empty);
                handoffState = ExitUpdateHandoff.Prepare(baseDir, tempRoot);
                ApplicationUpdateProcessCoordinator.CloseOtherApplicationProcesses();
                foreach (string pluginId in stagingPlan.ManifestPluginIds)
                {
                    if (!TryGetPluginTargetDirectory(programPluginsDirectory, pluginId, out string targetPluginDirectory))
                        throw new InvalidDataException($"Plugin manifest id '{pluginId}' does not resolve inside the installation Plugins directory.");

                    PluginRecoveryBackupService.Instance.CreateVerifiedBackup(pluginId, targetPluginDirectory);
                }

                GenerateBatchFile(
                    batchFilePath: batchFilePath,
                    baseDir: baseDir,
                    exeName: exeName,
                    originalProcessId: Environment.ProcessId,
                    handoffState: handoffState,
                    restartArguments: restartArguments,
                    manifestPluginIds: stagingPlan.ManifestPluginIds,
                    legacyStageDirectory: stagingPlan.HasLegacyPackages ? legacyStagingRoot : null
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
                }

                // 主程序、插件和组合更新共用同一份完整程序快照策略。
                ApplicationSnapshotService.Instance.CreateUpdateSnapshotIfEnabled();
                using Process updateProcess = ExitUpdateHandoff.Start(handoffState, psi);
                handoffStarted = true;
                try
                {
                    ApplicationUpdateShutdown.Request();
                }
                catch (Exception ex)
                {
                    log.Error("Plugin updater started, but application shutdown could not be requested. The handoff remains active.", ex);
                }
            }
            catch (Exception ex)
            {
                if (!handoffStarted)
                {
                    ExitUpdateHandoff.Clear(handoffState);
                    TryDeleteDirectory(tempRoot);
                    log.Error("Plugin update failed before updater batch started.", ex);
                    MessageBox.Show($"Update failed: {ex.Message}");
                }
                else
                {
                    log.Error("Plugin update handoff is active; updater staging and marker were preserved.", ex);
                }
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
            ArgumentNullException.ThrowIfNull(packagePaths);

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

        public static PluginPackageStagingPlan StagePluginPackagesForUpdate(
            IEnumerable<string> packagePaths,
            string manifestStagingRoot,
            string legacyStagingRoot,
            string extractionRoot)
        {
            ArgumentNullException.ThrowIfNull(packagePaths);
            manifestStagingRoot = NormalizeAbsoluteDirectory(manifestStagingRoot, nameof(manifestStagingRoot));
            legacyStagingRoot = NormalizeAbsoluteDirectory(legacyStagingRoot, nameof(legacyStagingRoot));
            extractionRoot = NormalizeAbsoluteDirectory(extractionRoot, nameof(extractionRoot));
            if (PathsOverlap(manifestStagingRoot, legacyStagingRoot)
                || PathsOverlap(manifestStagingRoot, extractionRoot)
                || PathsOverlap(legacyStagingRoot, extractionRoot))
            {
                throw new ArgumentException("Manifest, legacy, and extraction staging roots must be separate directories.");
            }
            EnsureExistingDirectoryIsNotReparsePoint(manifestStagingRoot, "Manifest plugin staging root");
            EnsureExistingDirectoryIsNotReparsePoint(legacyStagingRoot, "Legacy plugin staging root");
            EnsureExistingDirectoryIsNotReparsePoint(extractionRoot, "Plugin extraction root");

            List<string> paths = packagePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (paths.Count == 0)
                throw new InvalidOperationException("No plugin package was provided.");

            HashSet<string> stagedPluginIds = new(StringComparer.OrdinalIgnoreCase);
            bool hasLegacyPackages = false;
            foreach (string packagePath in paths)
            {
                string? pluginId = StagePluginPackage(
                    packagePath,
                    manifestStagingRoot,
                    extractionRoot,
                    stagedPluginIds,
                    legacyStagingRoot);
                hasLegacyPackages |= pluginId == null;
            }

            return new PluginPackageStagingPlan(
                stagedPluginIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToList(),
                hasLegacyPackages);
        }

        /// <summary>
        /// Moves each direct child carrying a matching manifest.json out of the application
        /// overlay tree into a dedicated complete-directory transaction stage.
        /// Directories without a manifest stay in the legacy overlay tree.
        /// </summary>
        public static IReadOnlyList<string> PrepareManifestPluginDirectoriesForTransaction(
            string pluginsStagingRoot,
            string transactionStagingRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pluginsStagingRoot);
            ArgumentException.ThrowIfNullOrWhiteSpace(transactionStagingRoot);
            string normalizedPluginsStagingRoot = NormalizeAbsoluteDirectory(pluginsStagingRoot, nameof(pluginsStagingRoot));
            string normalizedTransactionStagingRoot = NormalizeAbsoluteDirectory(transactionStagingRoot, nameof(transactionStagingRoot));
            if (PathsOverlap(normalizedPluginsStagingRoot, normalizedTransactionStagingRoot))
                throw new ArgumentException("Manifest transaction staging must be separate from the application overlay staging directory.", nameof(transactionStagingRoot));
            if (!Directory.Exists(normalizedPluginsStagingRoot))
                return Array.Empty<string>();
            EnsureExistingDirectoryIsNotReparsePoint(normalizedPluginsStagingRoot, "Plugin staging root", mustExist: true);
            EnsureExistingDirectoryIsNotReparsePoint(normalizedTransactionStagingRoot, "Plugin transaction staging root");

            var manifestDirectories = new List<(string PluginId, string SourceDirectory)>();
            foreach (string sourceDirectory in Directory.EnumerateDirectories(normalizedPluginsStagingRoot, "*", SearchOption.TopDirectoryOnly))
            {
                EnsureExistingDirectoryIsNotReparsePoint(sourceDirectory, "Staged plugin directory", mustExist: true);
                string manifestPath = Path.Combine(sourceDirectory, "manifest.json");
                if (!File.Exists(manifestPath))
                    continue;

                PluginManifest manifest;
                try
                {
                    manifest = JsonConvert.DeserializeObject<PluginManifest>(File.ReadAllText(manifestPath))
                        ?? throw new InvalidDataException($"Plugin manifest is empty: {manifestPath}");
                }
                catch (JsonException ex)
                {
                    throw new InvalidDataException($"Plugin manifest is invalid: {manifestPath}", ex);
                }

                string pluginId = manifest.Id?.Trim() ?? string.Empty;
                if (!TryGetPluginTargetDirectory(normalizedPluginsStagingRoot, pluginId, out string expectedSourceDirectory)
                    || !string.Equals(expectedSourceDirectory, Path.GetFullPath(sourceDirectory), StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(Path.GetFileName(sourceDirectory), pluginId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"Plugin manifest id '{pluginId}' does not match its direct staging directory.");
                }

                manifestDirectories.Add((pluginId, sourceDirectory));
            }

            if (manifestDirectories.Count == 0)
                return Array.Empty<string>();

            Directory.CreateDirectory(normalizedTransactionStagingRoot);
            foreach ((string pluginId, string sourceDirectory) in manifestDirectories.OrderBy(item => item.PluginId, StringComparer.OrdinalIgnoreCase))
            {
                if (!TryGetPluginTargetDirectory(normalizedTransactionStagingRoot, pluginId, out string targetDirectory))
                    throw new InvalidDataException($"Plugin manifest id '{pluginId}' is not a safe transaction directory name.");
                if (Directory.Exists(targetDirectory) || File.Exists(targetDirectory))
                    throw new InvalidDataException($"Plugin manifest id '{pluginId}' was staged more than once.");
                Directory.Move(sourceDirectory, targetDirectory);
            }

            return manifestDirectories
                .Select(item => item.PluginId)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Builds complete manifest-plugin directories for a combined incremental application
        /// update. Application .cvx packages contain only changed files, so each such plugin is
        /// assembled from the installed directory plus the staged delta. A full manifest-backed
        /// .cvxp package, when present, replaces that assembly and is the authoritative directory.
        /// Non-manifest directories remain in the application overlay for legacy compatibility.
        /// </summary>
        public static IReadOnlyList<string> PrepareCombinedManifestPluginDirectoriesForTransaction(
            string applicationPluginsStagingRoot,
            string manifestPackageStagingRoot,
            string installedPluginsRoot,
            string transactionStagingRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(applicationPluginsStagingRoot);
            ArgumentException.ThrowIfNullOrWhiteSpace(manifestPackageStagingRoot);
            ArgumentException.ThrowIfNullOrWhiteSpace(installedPluginsRoot);
            ArgumentException.ThrowIfNullOrWhiteSpace(transactionStagingRoot);

            string normalizedApplicationRoot = NormalizeAbsoluteDirectory(applicationPluginsStagingRoot, nameof(applicationPluginsStagingRoot));
            string normalizedManifestPackageRoot = NormalizeAbsoluteDirectory(manifestPackageStagingRoot, nameof(manifestPackageStagingRoot));
            string normalizedInstalledRoot = NormalizeAbsoluteDirectory(installedPluginsRoot, nameof(installedPluginsRoot));
            string normalizedTransactionRoot = NormalizeAbsoluteDirectory(transactionStagingRoot, nameof(transactionStagingRoot));
            if (!string.Equals(Path.GetFileName(normalizedInstalledRoot), "Plugins", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Installed plugin root must be the installation Plugins directory.", nameof(installedPluginsRoot));
            if (PathsOverlap(normalizedInstalledRoot, normalizedApplicationRoot)
                || PathsOverlap(normalizedInstalledRoot, normalizedManifestPackageRoot)
                || PathsOverlap(normalizedInstalledRoot, normalizedTransactionRoot)
                || PathsOverlap(normalizedApplicationRoot, normalizedManifestPackageRoot)
                || PathsOverlap(normalizedApplicationRoot, normalizedTransactionRoot)
                || PathsOverlap(normalizedManifestPackageRoot, normalizedTransactionRoot))
            {
                throw new ArgumentException("Installed, package, application, and transaction plugin roots must be separate directories.");
            }
            EnsureExistingDirectoryIsNotReparsePoint(normalizedApplicationRoot, "Application plugin delta root");
            EnsureExistingDirectoryIsNotReparsePoint(normalizedManifestPackageRoot, "Manifest plugin package root");
            EnsureExistingDirectoryIsNotReparsePoint(normalizedInstalledRoot, "Installed Plugins root");
            EnsureExistingDirectoryIsNotReparsePoint(normalizedTransactionRoot, "Plugin transaction staging root");

            var applicationDirectories = Directory.Exists(normalizedApplicationRoot)
                ? Directory.EnumerateDirectories(normalizedApplicationRoot, "*", SearchOption.TopDirectoryOnly)
                    .ToDictionary(path => Path.GetFileName(path) ?? throw new InvalidDataException("A staged plugin directory has no name."), StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var packageDirectories = Directory.Exists(normalizedManifestPackageRoot)
                ? Directory.EnumerateDirectories(normalizedManifestPackageRoot, "*", SearchOption.TopDirectoryOnly)
                    .ToDictionary(path => Path.GetFileName(path) ?? throw new InvalidDataException("A staged plugin package directory has no name."), StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var manifestPluginIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach ((string directoryName, string sourceDirectory) in applicationDirectories)
            {
                EnsureExistingDirectoryIsNotReparsePoint(sourceDirectory, "Application plugin delta directory", mustExist: true);
                if (!TryGetPluginTargetDirectory(normalizedInstalledRoot, directoryName, out string installedDirectory))
                    continue;

                string stagedManifestPath = Path.Combine(sourceDirectory, "manifest.json");
                string installedManifestPath = Path.Combine(installedDirectory, "manifest.json");
                if (!File.Exists(stagedManifestPath) && !File.Exists(installedManifestPath))
                    continue;

                string manifestPath = File.Exists(stagedManifestPath) ? stagedManifestPath : installedManifestPath;
                string pluginId = ReadValidatedManifestId(manifestPath, directoryName);
                manifestPluginIds.Add(pluginId);
            }

            foreach ((string directoryName, string sourceDirectory) in packageDirectories)
            {
                EnsureExistingDirectoryIsNotReparsePoint(sourceDirectory, "Manifest plugin package directory", mustExist: true);
                manifestPluginIds.Add(ReadValidatedManifestId(Path.Combine(sourceDirectory, "manifest.json"), directoryName));
            }

            if (manifestPluginIds.Count == 0)
                return Array.Empty<string>();

            Directory.CreateDirectory(normalizedTransactionRoot);
            foreach (string pluginId in manifestPluginIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase))
            {
                if (!TryGetPluginTargetDirectory(normalizedApplicationRoot, pluginId, out string applicationDirectory)
                    || !TryGetPluginTargetDirectory(normalizedManifestPackageRoot, pluginId, out string packageDirectory)
                    || !TryGetPluginTargetDirectory(normalizedInstalledRoot, pluginId, out string installedDirectory)
                    || !TryGetPluginTargetDirectory(normalizedTransactionRoot, pluginId, out string transactionDirectory))
                {
                    throw new InvalidDataException($"Plugin manifest id '{pluginId}' is not a safe direct-child directory name.");
                }

                bool hasApplicationDelta = Directory.Exists(applicationDirectory);
                bool hasFullManifestPackage = Directory.Exists(packageDirectory);
                if (hasFullManifestPackage)
                {
                    // Full cvxp packages are authoritative and must not inherit obsolete files
                    // from either the installed version or the application delta.
                    Directory.Move(packageDirectory, transactionDirectory);
                    if (hasApplicationDelta)
                        Directory.Delete(applicationDirectory, recursive: true);
                    continue;
                }

                if (!hasApplicationDelta)
                    throw new InvalidDataException($"Plugin '{pluginId}' has no staged update directory.");

                if (Directory.Exists(installedDirectory))
                {
                    string installedManifestPath = Path.Combine(installedDirectory, "manifest.json");
                    if (!File.Exists(installedManifestPath))
                        throw new InvalidDataException($"Installed plugin '{pluginId}' has no manifest.json and cannot seed an incremental directory transaction.");
                    ReadValidatedManifestId(installedManifestPath, pluginId);
                    Directory.CreateDirectory(transactionDirectory);
                    OverlayDirectory(installedDirectory, transactionDirectory);
                    OverlayDirectory(applicationDirectory, transactionDirectory);
                    Directory.Delete(applicationDirectory, recursive: true);
                }
                else
                {
                    // A plugin absent from the installed baseline is complete in a file-delta
                    // package because every one of its files is new.
                    Directory.Move(applicationDirectory, transactionDirectory);
                }

                ReadValidatedManifestId(Path.Combine(transactionDirectory, "manifest.json"), pluginId);
            }

            return manifestPluginIds
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Appends a complete-directory plugin transaction to another external updater batch.
        /// The caller supplies an installation-scoped Plugins directory and a prepared stage
        /// containing one direct child per manifest plugin.
        /// </summary>
        public static void AppendPreparedManifestDirectoryTransaction(
            StringBuilder builder,
            string preparedPluginsRoot,
            string targetPluginsRoot,
            string failureLabel,
            string labelPrefix = "combined_plugin_transaction")
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentException.ThrowIfNullOrWhiteSpace(preparedPluginsRoot);
            ArgumentException.ThrowIfNullOrWhiteSpace(targetPluginsRoot);
            ArgumentException.ThrowIfNullOrWhiteSpace(failureLabel);
            ArgumentException.ThrowIfNullOrWhiteSpace(labelPrefix);
            string normalizedPreparedRoot = NormalizeAbsoluteDirectory(preparedPluginsRoot, nameof(preparedPluginsRoot));
            string normalizedTargetRoot = NormalizeAbsoluteDirectory(targetPluginsRoot, nameof(targetPluginsRoot));
            if (PathsOverlap(normalizedPreparedRoot, normalizedTargetRoot))
                throw new ArgumentException("Prepared and installed plugin roots must not overlap.", nameof(targetPluginsRoot));
            if (!Directory.Exists(normalizedPreparedRoot))
                return;
            EnsureExistingDirectoryIsNotReparsePoint(normalizedPreparedRoot, "Prepared plugin transaction root", mustExist: true);
            EnsureExistingDirectoryIsNotReparsePoint(normalizedTargetRoot, "Installed Plugins root");

            var replacements = new List<PluginDirectoryReplacement>();
            foreach (string sourceDirectory in Directory
                .EnumerateDirectories(normalizedPreparedRoot, "*", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                string pluginId = Path.GetFileName(sourceDirectory);
                if (!TryGetPluginTargetDirectory(normalizedPreparedRoot, pluginId, out string expectedSourceDirectory)
                    || !string.Equals(expectedSourceDirectory, Path.GetFullPath(sourceDirectory), StringComparison.OrdinalIgnoreCase)
                    || !TryGetPluginTargetDirectory(normalizedTargetRoot, pluginId, out string targetDirectory))
                {
                    throw new InvalidDataException($"Prepared plugin directory '{sourceDirectory}' is outside a safe direct-child transaction path.");
                }

                replacements.Add(new PluginDirectoryReplacement(pluginId, sourceDirectory, targetDirectory));
            }

            if (replacements.Count == 0)
                return;

            string transactionDirectory = Path.Combine(
                normalizedTargetRoot,
                $".ColorVisionUpdate-{Guid.NewGuid():N}");
            PluginDirectoryTransactionBatchScript.AppendTransaction(
                builder,
                replacements,
                transactionDirectory,
                failureLabel,
                labelPrefix);
            string helpersCompleteLabel = labelPrefix + "_helpers_complete";
            builder.AppendLine("goto " + helpersCompleteLabel);
            PluginDirectoryTransactionBatchScript.AppendCopyCompleteDirectoryFunction(builder);
            builder.AppendLine(":" + helpersCompleteLabel);
        }

        private static bool PathsOverlap(string firstDirectory, string secondDirectory)
        {
            string first = firstDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string second = secondDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(first, second, StringComparison.OrdinalIgnoreCase))
                return true;

            return first.StartsWith(second + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || second.StartsWith(first + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeAbsoluteDirectory(string directory, string parameterName)
        {
            if (!Path.IsPathFullyQualified(directory))
                throw new ArgumentException("Directory path must be absolute.", parameterName);
            string fullPath = Path.GetFullPath(directory);
            string? root = Path.GetPathRoot(fullPath);
            string trimmed = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.IsNullOrEmpty(trimmed) || (root != null && trimmed.Length < root.Length)
                ? fullPath
                : trimmed;
        }

        private static void EnsureExistingDirectoryIsNotReparsePoint(
            string directory,
            string description,
            bool mustExist = false)
        {
            try
            {
                FileAttributes attributes = File.GetAttributes(directory);
                if (!attributes.HasFlag(FileAttributes.Directory))
                    throw new IOException($"{description} is not a directory: {directory}");
                if (attributes.HasFlag(FileAttributes.ReparsePoint))
                    throw new InvalidDataException($"{description} cannot be a reparse point: {directory}");
            }
            catch (FileNotFoundException) when (!mustExist)
            {
            }
            catch (DirectoryNotFoundException) when (!mustExist)
            {
            }
        }

        private static string ReadValidatedManifestId(string manifestPath, string directoryName)
        {
            if (!File.Exists(manifestPath))
                throw new InvalidDataException($"Plugin manifest is missing: {manifestPath}");

            PluginManifest manifest;
            try
            {
                manifest = JsonConvert.DeserializeObject<PluginManifest>(File.ReadAllText(manifestPath))
                    ?? throw new InvalidDataException($"Plugin manifest is empty: {manifestPath}");
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"Plugin manifest is invalid: {manifestPath}", ex);
            }

            string pluginId = manifest.Id?.Trim() ?? string.Empty;
            if (!string.Equals(pluginId, directoryName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Plugin manifest id '{pluginId}' does not match directory '{directoryName}'.");
            return pluginId;
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

        internal static string? StagePluginPackage(
            string packagePath,
            string stagingRoot,
            string extractionRoot,
            ISet<string>? stagedPluginIds = null,
            string? legacyStagingRoot = null)
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
                // Legacy packages without a manifest keep their existing directory layout and
                // compatibility overlay behavior. They deliberately have no reliable rollback.
                string legacyTarget = legacyStagingRoot ?? stagingRoot;
                Directory.CreateDirectory(legacyTarget);
                ZipFile.ExtractToDirectory(packagePath, legacyTarget, overwriteFiles: true);
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
            string normalizedSource = NormalizeAbsoluteDirectory(sourceDirectory, nameof(sourceDirectory));
            string normalizedTarget = NormalizeAbsoluteDirectory(targetDirectory, nameof(targetDirectory));
            if (PathsOverlap(normalizedSource, normalizedTarget))
                throw new InvalidDataException("Plugin staging source and target directories must not overlap.");
            if (File.GetAttributes(normalizedSource).HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidDataException($"Plugin staging cannot follow a reparse point: {normalizedSource}");

            Directory.CreateDirectory(normalizedTarget);
            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(normalizedSource);
            while (pendingDirectories.Count > 0)
            {
                string currentDirectory = pendingDirectories.Pop();
                foreach (string entry in Directory.EnumerateFileSystemEntries(currentDirectory, "*", SearchOption.TopDirectoryOnly))
                {
                    FileAttributes attributes = File.GetAttributes(entry);
                    if (attributes.HasFlag(FileAttributes.ReparsePoint))
                        throw new InvalidDataException($"Plugin staging cannot follow a reparse point: {entry}");

                    string relativePath = Path.GetRelativePath(normalizedSource, entry);
                    string targetPath = Path.GetFullPath(Path.Combine(normalizedTarget, relativePath));
                    if (!targetPath.StartsWith(normalizedTarget + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Plugin staging entry escaped its target directory.");

                    if (attributes.HasFlag(FileAttributes.Directory))
                    {
                        Directory.CreateDirectory(targetPath);
                        pendingDirectories.Push(entry);
                    }
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                        File.Copy(entry, targetPath, overwrite: true);
                    }
                }
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
            int originalProcessId,
            ExitUpdateHandoffState handoffState,
            string? restartArguments = "-c MenuPluginManager",
            IReadOnlyList<string>? manifestPluginIds = null,
            string? legacyStageDirectory = null
        )
        {
            if (string.IsNullOrWhiteSpace(batchFilePath))
                throw new ArgumentException(Properties.Resources.BatchFilePathCannotBeEmpty, nameof(batchFilePath));
            if (string.IsNullOrWhiteSpace(baseDir))
                throw new ArgumentException(Properties.Resources.BaseDirCannotBeEmpty, nameof(baseDir));
            if (string.IsNullOrWhiteSpace(exeName))
                throw new ArgumentException(Properties.Resources.ExeNameCannotBeEmpty, nameof(exeName));
            if (!Path.IsPathFullyQualified(batchFilePath))
                throw new ArgumentException("Batch file path must be absolute.", nameof(batchFilePath));
            if (!Path.IsPathFullyQualified(baseDir))
                throw new ArgumentException("ColorVision installation directory must be absolute.", nameof(baseDir));

            baseDir = Path.GetFullPath(baseDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string stageRoot = Path.Combine(Path.GetDirectoryName(batchFilePath)!, "ColorVision");
            string manifestStagingRoot = Path.Combine(stageRoot, "Plugins");
            string targetPluginsRoot = Path.Combine(baseDir, "Plugins");
            EnsureExistingDirectoryIsNotReparsePoint(baseDir, "ColorVision installation directory");
            EnsureExistingDirectoryIsNotReparsePoint(targetPluginsRoot, "ColorVision Plugins directory");
            List<string> normalizedManifestPluginIds = manifestPluginIds?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();
            var replacements = new List<PluginDirectoryReplacement>(normalizedManifestPluginIds.Count);
            foreach (string pluginId in normalizedManifestPluginIds)
            {
                if (!TryGetPluginTargetDirectory(manifestStagingRoot, pluginId, out string sourceDirectory)
                    || !TryGetPluginTargetDirectory(targetPluginsRoot, pluginId, out string targetDirectory))
                {
                    throw new InvalidDataException($"Plugin manifest id '{pluginId}' is not a safe direct child directory.");
                }

                replacements.Add(new PluginDirectoryReplacement(pluginId, sourceDirectory, targetDirectory));
            }

            // Calls from the historical test/compatibility surface omitted an explicit plan and
            // therefore retain the legacy whole-tree overlay. Production manifest updates pass
            // an explicit list and never enter this branch.
            string? effectiveLegacyStageDirectory = legacyStageDirectory;
            if (manifestPluginIds == null && legacyStageDirectory == null)
                effectiveLegacyStageDirectory = stageRoot;
            if (!string.IsNullOrWhiteSpace(effectiveLegacyStageDirectory))
            {
                string normalizedLegacyStage = NormalizeAbsoluteDirectory(effectiveLegacyStageDirectory, nameof(legacyStageDirectory));
                EnsureExistingDirectoryIsNotReparsePoint(normalizedLegacyStage, "Legacy plugin staging directory");
                effectiveLegacyStageDirectory = normalizedLegacyStage;
            }

            var escapedBaseDir = EscapeForBatchValue(baseDir);
            var escapedExePath = EscapeForBatchValue(Path.Combine(baseDir, exeName));

            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("setlocal DisableDelayedExpansion");
            sb.AppendLine("title ColorVision Updater");
            sb.AppendLine($"set \"EXEPATH={escapedExePath}\"");
            sb.AppendLine($"set \"UPDATE_ROOT={EscapeForBatchValue(Path.GetDirectoryName(batchFilePath)!)}\"");
            ExternalUpdateBatchScript.AppendSessionVariables(sb, originalProcessId, handoffState);
            sb.AppendLine();
            sb.AppendLine(string.Format(Properties.Resources.EchoTerminatingProcess, exeName));
            sb.AppendLine("call :wait_for_original_process");
            ExternalUpdateBatchScript.AppendLog(sb, "Plugin update started.");
            sb.AppendLine();
            sb.AppendLine($"set \"TARGET={escapedBaseDir}\"");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(effectiveLegacyStageDirectory))
            {
                sb.AppendLine(Properties.Resources.EchoStartCopyingFiles);
                sb.AppendLine(Properties.Resources.RemStagePointsToTemp);
                if (manifestPluginIds == null && legacyStageDirectory == null)
                    sb.AppendLine("set \"STAGE=%~dp0ColorVision\"");
                else
                    sb.AppendLine($"set \"STAGE={EscapeForBatchValue(Path.GetFullPath(effectiveLegacyStageDirectory))}\"");
                bool legacyStageRepresentsApplicationRoot = manifestPluginIds == null && legacyStageDirectory == null;
                if (!legacyStageRepresentsApplicationRoot)
                    sb.AppendLine($"set \"LEGACY_TARGET={EscapeForBatchValue(targetPluginsRoot)}\"");
                ExternalUpdateBatchScript.AppendLog(sb, "Legacy plugin overlay started; reliable rollback is unavailable for packages without manifest.json.");
                sb.AppendLine("where robocopy >nul 2>nul");
                sb.AppendLine("if errorlevel 1 goto fallback_copy");
                sb.AppendLine(legacyStageRepresentsApplicationRoot
                    ? "robocopy \"%STAGE%\" \"%TARGET%\" *.* /E /IS /IT /NFL /NDL /NP /NJH /NJS /R:2 /W:1"
                    : "robocopy \"%STAGE%\" \"%LEGACY_TARGET%\" *.* /E /IS /IT /NFL /NDL /NP /NJH /NJS /R:2 /W:1");
                sb.AppendLine("if errorlevel 8 goto fallback_copy");
                sb.AppendLine("goto copy_done");
                sb.AppendLine();

                sb.AppendLine(":fallback_copy");
                sb.AppendLine(Properties.Resources.EchoUsingXCOPY);
                sb.AppendLine(legacyStageRepresentsApplicationRoot
                    ? "xcopy /y /e /i \"%STAGE%\\*\" \"%TARGET%\\\" >nul"
                    : "xcopy /y /e /i \"%STAGE%\\*\" \"%LEGACY_TARGET%\\\" >nul");
                sb.AppendLine("if errorlevel 1 goto fail");
                sb.AppendLine("goto copy_done");
                sb.AppendLine();

                sb.AppendLine(":copy_done");
                sb.AppendLine(Properties.Resources.EchoCopyComplete);
            }

            if (replacements.Count > 0)
            {
                ExternalUpdateBatchScript.AppendLog(sb, $"Preparing {replacements.Count} manifest plugin directory replacement(s).");
                string transactionDirectory = Path.Combine(
                    targetPluginsRoot,
                    $".ColorVisionUpdate-{Guid.NewGuid():N}");
                PluginDirectoryTransactionBatchScript.AppendTransaction(
                    sb,
                    replacements,
                    transactionDirectory,
                    failureLabel: "fail");
            }

            ExternalUpdateBatchScript.AppendLog(sb, "Plugin update completed.");
            sb.AppendLine();

            sb.AppendLine(Properties.Resources.EchoUpdateComplete);
            sb.AppendLine("call :complete_handoff");
            sb.AppendLine("call :schedule_cleanup");
            sb.AppendLine("endlocal");
            sb.AppendLine("exit /b 0");
            sb.AppendLine();

            sb.AppendLine(":fail");
            ExternalUpdateBatchScript.AppendLog(sb, "Plugin update failed.");
            sb.AppendLine("call :complete_handoff");
            sb.AppendLine("call :schedule_cleanup");
            sb.AppendLine("endlocal");
            sb.AppendLine("exit /b 1");
            sb.AppendLine();

            sb.AppendLine(":complete_handoff");
            ExternalUpdateBatchScript.AppendRestartAndComplete(sb, restartArguments);
            sb.AppendLine("exit /b 0");
            sb.AppendLine();

            sb.AppendLine(":schedule_cleanup");
            sb.AppendLine(Properties.Resources.EchoSchedulingCleanup);
            sb.AppendLine("start \"\" /d \"%TEMP%\" /b cmd /d /c ping -n 4 127.0.0.1 ^>nul ^& rd /s /q \"%UPDATE_ROOT%\" 2^>nul");
            sb.AppendLine("exit /b 0");
            sb.AppendLine();

            if (replacements.Count > 0)
                PluginDirectoryTransactionBatchScript.AppendCopyCompleteDirectoryFunction(sb);

            ExternalUpdateBatchScript.AppendWaitForOriginalProcess(sb);

            File.WriteAllText(batchFilePath, sb.ToString(), Encoding.GetEncoding(936));
        }

        internal static void GenerateDeleteBatchFile(
            string batchFilePath,
            IReadOnlyList<string> targetPluginDirectories,
            string executablePath,
            int originalProcessId,
            ExitUpdateHandoffState handoffState)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(batchFilePath);
            ArgumentNullException.ThrowIfNull(targetPluginDirectories);
            ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
            if (!Path.IsPathFullyQualified(batchFilePath))
                throw new ArgumentException("Batch file path must be absolute.", nameof(batchFilePath));
            if (!Path.IsPathFullyQualified(executablePath))
                throw new ArgumentException("Executable path must be absolute.", nameof(executablePath));
            string normalizedExecutablePath = Path.GetFullPath(executablePath);
            string programDirectory = Path.GetDirectoryName(normalizedExecutablePath)
                ?? throw new ArgumentException("Executable path must have an installation directory.", nameof(executablePath));
            string expectedPluginsDirectory = Path.Combine(programDirectory, "Plugins");
            EnsureExistingDirectoryIsNotReparsePoint(programDirectory, "ColorVision installation directory");
            EnsureExistingDirectoryIsNotReparsePoint(expectedPluginsDirectory, "ColorVision Plugins directory");
            foreach (string targetPluginDirectory in targetPluginDirectories)
            {
                if (!Path.IsPathFullyQualified(targetPluginDirectory)
                    || !string.Equals(Path.GetDirectoryName(Path.GetFullPath(targetPluginDirectory)), expectedPluginsDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("Plugin deletion target must be a direct child of the current installation Plugins directory.");
                }
                EnsureExistingDirectoryIsNotReparsePoint(targetPluginDirectory, "Plugin deletion target");
            }

            StringBuilder builder = new();
            builder.AppendLine("@echo off");
            builder.AppendLine("setlocal DisableDelayedExpansion");
            builder.AppendLine($"set \"EXEPATH={EscapeForBatchValue(executablePath)}\"");
            builder.AppendLine($"set \"UPDATE_ROOT={EscapeForBatchValue(Path.GetDirectoryName(batchFilePath)!)}\"");
            ExternalUpdateBatchScript.AppendSessionVariables(builder, originalProcessId, handoffState);
            builder.AppendLine("call :wait_for_original_process");
            ExternalUpdateBatchScript.AppendLog(builder, "Plugin deletion started.");

            foreach (string targetPluginDirectory in targetPluginDirectories)
            {
                builder.AppendLine($"set \"TARGET={EscapeForBatchValue(targetPluginDirectory)}\"");
                builder.AppendLine("if exist \"%TARGET%\" rd /s /q \"%TARGET%\"");
                builder.AppendLine("if exist \"%TARGET%\" goto fail");
            }

            ExternalUpdateBatchScript.AppendLog(builder, "Plugin deletion completed.");
            builder.AppendLine("call :complete_handoff");
            builder.AppendLine("call :schedule_cleanup");
            builder.AppendLine("endlocal");
            builder.AppendLine("exit /b 0");
            builder.AppendLine();
            builder.AppendLine(":fail");
            ExternalUpdateBatchScript.AppendLog(builder, "Plugin deletion failed.");
            builder.AppendLine("call :complete_handoff");
            builder.AppendLine("call :schedule_cleanup");
            builder.AppendLine("endlocal");
            builder.AppendLine("exit /b 1");
            builder.AppendLine();
            builder.AppendLine(":complete_handoff");
            ExternalUpdateBatchScript.AppendRestartAndComplete(builder, "-c MenuPluginManager");
            builder.AppendLine("exit /b 0");
            builder.AppendLine();
            builder.AppendLine(":schedule_cleanup");
            builder.AppendLine("start \"\" /d \"%TEMP%\" /b cmd /d /c ping -n 4 127.0.0.1 ^>nul ^& rd /s /q \"%UPDATE_ROOT%\" 2^>nul");
            builder.AppendLine("exit /b 0");
            builder.AppendLine();
            ExternalUpdateBatchScript.AppendWaitForOriginalProcess(builder);
            File.WriteAllText(batchFilePath, builder.ToString(), Encoding.GetEncoding(936));
        }
    }
}
