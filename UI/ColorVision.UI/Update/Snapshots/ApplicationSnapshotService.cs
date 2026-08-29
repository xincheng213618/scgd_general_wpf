#pragma warning disable CA1822
using ColorVision.UI;
using ColorVision.UI.ServiceHost;
using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Update
{
    public sealed class ApplicationSnapshotInfo
    {
        public required string FilePath { get; init; }

        public required string FileName { get; init; }

        public required string Version { get; init; }

        public required string VersionTarget { get; init; }

        public required DateTime CreatedAt { get; init; }

        public required long SizeBytes { get; init; }

        public required bool IsDefault { get; init; }

        public required bool IsUpdate { get; init; }

        public required bool IsAutomatic { get; init; }

        public string SnapshotTypeText => IsAutomatic ? "自动存档" : IsDefault ? "默认快照" : IsUpdate ? "更新快照" : "用户快照";

        public string VersionText => string.IsNullOrWhiteSpace(VersionTarget) ? Version : $"{Version} -> {VersionTarget}";

        public string CreatedAtText => CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);

        public string SizeText => FormatSize(SizeBytes);

        private static string FormatSize(long size)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            double value = size;
            int unitIndex = 0;
            while (value >= 1024 && unitIndex < units.Length - 1)
            {
                value /= 1024;
                unitIndex++;
            }

            return $"{value:0.##} {units[unitIndex]}";
        }
    }

    public sealed class ApplicationSnapshotManifest
    {
        public string SnapshotKind { get; set; } = "Application";

        public DateTime CreatedAt { get; set; }

        public string Version { get; set; } = string.Empty;

        public string VersionTarget { get; set; } = string.Empty;

        public string ProgramDirectory { get; set; } = string.Empty;

        public bool IsDefault { get; set; }
    }

    public sealed class ApplicationSnapshotService
    {
        private const string ManifestFileName = "snapshot-manifest.json";
        private const string DefaultSnapshotFileName = "default.zip";
        private const string AutomaticSnapshotFileName = "autosave.zip";
        private const int MaxAutomaticUpdateSnapshots = 3;
        private const int CopyBufferSize = 1024 * 1024;
        private static readonly TimeSpan HealthyStartupSnapshotDelay = TimeSpan.FromSeconds(10);
        private static readonly ILog log = LogManager.GetLogger(typeof(ApplicationSnapshotService));
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
        private static readonly object SnapshotCreationLock = new();
        private static readonly HashSet<string> AlreadyCompressedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".7z", ".avi", ".cvx", ".cvxp", ".gif", ".gz", ".jpeg", ".jpg", ".mp3", ".mp4", ".pdf", ".png", ".rar", ".webp", ".zip"
        };
        private int _healthyStartupSnapshotScheduled;

        public static ApplicationSnapshotService Instance { get; } = new();

        public event EventHandler<ApplicationSnapshotInfo>? SnapshotCreated;

        public string SnapshotDirectory => Path.Combine(
            Environments.DirApplicationSnapshots,
            ExitUpdateHandoff.GetInstallationKey(AppDomain.CurrentDomain.BaseDirectory));

        public string DefaultSnapshotPath => Path.Combine(SnapshotDirectory, DefaultSnapshotFileName);

        public string AutomaticSnapshotDirectory => ResolveAutomaticSnapshotDirectory(GetConfiguredAutomaticSnapshotDirectory());

        public string AutomaticSnapshotPath => Path.Combine(AutomaticSnapshotDirectory, AutomaticSnapshotFileName);

        private ApplicationSnapshotService()
        {
        }

        public Task<ApplicationSnapshotInfo> CreateDefaultSnapshotAsync(bool force, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => CreateSnapshotCore(DefaultSnapshotPath, SnapshotKind.Default, versionTarget: string.Empty, overwrite: force, cancellationToken), cancellationToken);
        }

        public void ScheduleHealthyStartupAutomaticSnapshot()
        {
            if (!GetSnapshotConfig().CreateAutomaticSnapshotAfterHealthyStartup
                || Interlocked.Exchange(ref _healthyStartupSnapshotScheduled, 1) != 0)
            {
                return;
            }

            try
            {
                new Thread(CreateHealthyStartupAutomaticSnapshot)
                {
                    IsBackground = true,
                    Name = "ColorVision application automatic snapshot",
                    Priority = ThreadPriority.Lowest,
                }.Start();
            }
            catch (Exception ex)
            {
                Volatile.Write(ref _healthyStartupSnapshotScheduled, 0);
                log.Warn($"Unable to start the automatic application snapshot worker: {ex.Message}");
            }
        }

        public Task<ApplicationSnapshotInfo> CreateUserSnapshotAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                Directory.CreateDirectory(SnapshotDirectory);
                string version = GetCurrentVersionText();
                string fileName = $"ColorVision-{SanitizeFilePart(version)}-{DateTime.Now:yyyyMMdd-HHmmss}.zip";
                string snapshotPath = Path.Combine(SnapshotDirectory, fileName);
                return CreateSnapshotCore(snapshotPath, SnapshotKind.User, versionTarget: string.Empty, overwrite: false, cancellationToken);
            }, cancellationToken);
        }

        public ApplicationSnapshotInfo CreateUpdateSnapshot(string versionTarget = "", CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(SnapshotDirectory);
            string version = GetCurrentVersionText();
            string fileName = $"ColorVision-update-{SanitizeFilePart(version)}-{DateTime.Now:yyyyMMdd-HHmmss-fff}.zip";
            ApplicationSnapshotInfo snapshot = CreateSnapshotCore(
                Path.Combine(SnapshotDirectory, fileName),
                SnapshotKind.Update,
                versionTarget,
                overwrite: false,
                cancellationToken);
            TrimAutomaticUpdateSnapshots();
            return snapshot;
        }

        public ApplicationSnapshotInfo? CreateUpdateSnapshotIfEnabled(string versionTarget = "", CancellationToken cancellationToken = default)
        {
            if (!ShouldCreateUpdateSnapshot(ConfigService.Instance))
                return null;

            log.Info("Creating a full application snapshot before update.");
            return CreateUpdateSnapshot(versionTarget, cancellationToken);
        }

        internal static bool ShouldCreateUpdateSnapshot(IConfigService? configService)
        {
            return configService != null
                && configService.GetRequiredService<ApplicationSnapshotConfig>().CreateSnapshotBeforeUpdate;
        }

        public ApplicationSnapshotInfo GetSnapshotInfo(string snapshotPath)
        {
            if (string.IsNullOrWhiteSpace(snapshotPath) || !File.Exists(snapshotPath))
                throw new FileNotFoundException("Snapshot file does not exist.", snapshotPath);

            return ReadSnapshotInfo(snapshotPath);
        }

        public IReadOnlyList<ApplicationSnapshotInfo> ListSnapshots()
        {
            Directory.CreateDirectory(SnapshotDirectory);
            return GetSnapshotSearchDirectories()
                .SelectMany(directory => Directory.Exists(directory)
                    ? Directory.EnumerateFiles(directory, "*.zip", SearchOption.TopDirectoryOnly)
                    : Enumerable.Empty<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(TryReadSnapshotInfoOrIgnore)
                .OfType<ApplicationSnapshotInfo>()
                .OrderByDescending(item => item.IsAutomatic)
                .ThenByDescending(item => item.IsDefault)
                .ThenByDescending(item => item.CreatedAt)
                .ToList();
        }

        private IEnumerable<string> GetSnapshotSearchDirectories()
        {
            yield return SnapshotDirectory;
            string automaticSnapshotDirectory = AutomaticSnapshotDirectory;
            if (!string.Equals(automaticSnapshotDirectory, SnapshotDirectory, StringComparison.OrdinalIgnoreCase))
                yield return automaticSnapshotDirectory;
            if (!string.Equals(SnapshotDirectory, Environments.DirApplicationSnapshots, StringComparison.OrdinalIgnoreCase))
                yield return Environments.DirApplicationSnapshots;
        }

        public string ResolveAutomaticSnapshotDirectory(string? configuredDirectory)
        {
            string directory = string.IsNullOrWhiteSpace(configuredDirectory)
                ? SnapshotDirectory
                : Environment.ExpandEnvironmentVariables(configuredDirectory.Trim());
            if (!Path.IsPathFullyQualified(directory))
                throw new InvalidOperationException("自动存档位置必须是完整路径。");

            string fullPath = NormalizeDirectory(directory);
            string programDirectory = NormalizeDirectory(AppDomain.CurrentDomain.BaseDirectory);
            if (string.Equals(fullPath, programDirectory, StringComparison.OrdinalIgnoreCase)
                || IsPathUnderDirectory(programDirectory, fullPath))
            {
                throw new InvalidOperationException("自动存档位置不能放在 ColorVision 程序目录内。");
            }

            return fullPath;
        }

        internal static bool ShouldCreateAutomaticSnapshot(string snapshotPath, string currentVersion)
        {
            if (!File.Exists(snapshotPath))
                return true;

            try
            {
                ApplicationSnapshotInfo snapshot = ReadSnapshotInfo(snapshotPath);
                return !snapshot.IsAutomatic
                    || !string.Equals(snapshot.Version, currentVersion, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                return true;
            }
        }

        public Task DeleteSnapshotAsync(ApplicationSnapshotInfo snapshot, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            return Task.Run(() =>
            {
                if (File.Exists(snapshot.FilePath))
                    File.Delete(snapshot.FilePath);
            }, cancellationToken);
        }

        public Task RestoreSnapshotAsync(ApplicationSnapshotInfo snapshot, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            return Task.Run(() => RestoreSnapshotCore(snapshot, cancellationToken), cancellationToken);
        }

        private static ApplicationSnapshotInfo CreateSnapshotCore(string snapshotPath, SnapshotKind kind, string versionTarget, bool overwrite, CancellationToken cancellationToken)
        {
            lock (SnapshotCreationLock)
            {
                return CreateSnapshotCoreLocked(snapshotPath, kind, versionTarget, overwrite, cancellationToken);
            }
        }

        private static ApplicationSnapshotInfo CreateSnapshotCoreLocked(string snapshotPath, SnapshotKind kind, string versionTarget, bool overwrite, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);

            if (File.Exists(snapshotPath))
            {
                if (!overwrite)
                    return ReadSnapshotInfo(snapshotPath);
            }

            string tempPath = $"{snapshotPath}.{Guid.NewGuid():N}.tmp";

            string programDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            ApplicationSnapshotManifest manifest = new()
            {
                CreatedAt = DateTime.Now,
                SnapshotKind = kind.ToString(),
                Version = GetCurrentVersionText(),
                VersionTarget = versionTarget,
                ProgramDirectory = programDirectory,
                IsDefault = kind == SnapshotKind.Default,
            };

            try
            {
                using (FileStream zipStream = new(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                using (ZipArchive archive = new(zipStream, ZipArchiveMode.Create))
                {
                    foreach (string filePath in Directory.EnumerateFiles(programDirectory, "*", SearchOption.AllDirectories))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (ShouldIncludeSnapshotFile(programDirectory, filePath))
                            AddFileEntry(archive, programDirectory, filePath);
                    }

                    ZipArchiveEntry manifestEntry = archive.CreateEntry(ManifestFileName, CompressionLevel.Fastest);
                    using Stream manifestStream = manifestEntry.Open();
                    JsonSerializer.Serialize(manifestStream, manifest, JsonOptions);
                }

                if (kind == SnapshotKind.Automatic)
                    PromoteCompletedAutomaticSnapshot(tempPath, snapshotPath);
                else
                    PromoteCompletedSnapshot(tempPath, snapshotPath);
                return ReadSnapshotInfo(snapshotPath);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        private static void AddFileEntry(ZipArchive archive, string rootDirectory, string filePath)
        {
            string relativePath = Path.GetRelativePath(rootDirectory, filePath)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');

            CompressionLevel compressionLevel = AlreadyCompressedExtensions.Contains(Path.GetExtension(filePath))
                ? CompressionLevel.NoCompression
                : CompressionLevel.Fastest;
            ZipArchiveEntry entry = archive.CreateEntry(relativePath, compressionLevel);
            using Stream entryStream = entry.Open();
            using FileStream sourceStream = new(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                CopyBufferSize,
                FileOptions.SequentialScan);
            sourceStream.CopyTo(entryStream, CopyBufferSize);
        }

        internal static bool ShouldIncludeSnapshotFile(string rootDirectory, string filePath)
        {
            string relativePath = Path.GetRelativePath(rootDirectory, filePath);
            string[] parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (parts.Length > 1 && string.Equals(parts[0], "log", StringComparison.OrdinalIgnoreCase))
                return false;

            string extension = Path.GetExtension(filePath);
            return !string.Equals(extension, ".pdb", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(extension, ".tmp", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(Path.GetFileName(filePath), "update.bat", StringComparison.OrdinalIgnoreCase);
        }

        private void TrimAutomaticUpdateSnapshots()
        {
            try
            {
                TrimAutomaticUpdateSnapshots(SnapshotDirectory, MaxAutomaticUpdateSnapshots);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                log.Warn($"Failed to trim automatic update snapshots: {ex.Message}");
            }
        }

        internal static int TrimAutomaticUpdateSnapshots(string snapshotDirectory, int maximumCount)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(maximumCount);
            if (!Directory.Exists(snapshotDirectory))
                return 0;

            ApplicationSnapshotInfo[] obsoleteSnapshots = Directory
                .EnumerateFiles(snapshotDirectory, "*.zip", SearchOption.TopDirectoryOnly)
                .Select(TryReadSnapshotInfoOrIgnore)
                .OfType<ApplicationSnapshotInfo>()
                .Where(item => item.IsUpdate)
                .OrderByDescending(item => item.CreatedAt)
                .Skip(maximumCount)
                .ToArray();

            foreach (ApplicationSnapshotInfo snapshot in obsoleteSnapshots)
            {
                File.Delete(snapshot.FilePath);
                log.Info($"Removed obsolete automatic update snapshot: {snapshot.FilePath}");
            }

            return obsoleteSnapshots.Length;
        }

        private static ApplicationSnapshotInfo ReadSnapshotInfo(string snapshotPath)
        {
            FileInfo fileInfo = new(snapshotPath);
            using ZipArchive archive = ZipFile.OpenRead(snapshotPath);
            _ = archive.Entries.Count;
            ApplicationSnapshotManifest? manifest = ReadManifest(archive, snapshotPath);
            bool isDefault = string.Equals(fileInfo.Name, DefaultSnapshotFileName, StringComparison.OrdinalIgnoreCase)
                || manifest?.IsDefault == true;
            bool isUpdate = string.Equals(manifest?.SnapshotKind, SnapshotKind.Update.ToString(), StringComparison.OrdinalIgnoreCase)
                || fileInfo.Name.StartsWith("ColorVision-update-", StringComparison.OrdinalIgnoreCase);
            bool isAutomatic = string.Equals(manifest?.SnapshotKind, SnapshotKind.Automatic.ToString(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileInfo.Name, AutomaticSnapshotFileName, StringComparison.OrdinalIgnoreCase);

            return new ApplicationSnapshotInfo
            {
                FilePath = snapshotPath,
                FileName = fileInfo.Name,
                Version = string.IsNullOrWhiteSpace(manifest?.Version) ? "未知" : manifest.Version,
                VersionTarget = manifest?.VersionTarget ?? string.Empty,
                CreatedAt = manifest?.CreatedAt ?? fileInfo.CreationTime,
                SizeBytes = fileInfo.Exists ? fileInfo.Length : 0,
                IsDefault = isDefault,
                IsUpdate = isUpdate,
                IsAutomatic = isAutomatic,
            };
        }

        private static ApplicationSnapshotInfo? TryReadSnapshotInfoOrIgnore(string snapshotPath)
        {
            if (TryReadSnapshotInfo(snapshotPath, out ApplicationSnapshotInfo? snapshotInfo))
                return snapshotInfo;

            return null;
        }

        private static bool TryReadSnapshotInfo(string snapshotPath, out ApplicationSnapshotInfo? snapshotInfo)
        {
            try
            {
                snapshotInfo = ReadSnapshotInfo(snapshotPath);
                return true;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                log.Warn($"Snapshot file is invalid and will be ignored: {snapshotPath}. {ex.Message}");
                snapshotInfo = null;
                return false;
            }
        }

        internal static void PromoteCompletedSnapshot(string completedSnapshotPath, string snapshotPath)
        {
            string? recoveryPath = File.Exists(snapshotPath)
                ? MoveSnapshotToRecovery(snapshotPath, "replaced")
                : null;

            try
            {
                File.Move(completedSnapshotPath, snapshotPath);
            }
            catch
            {
                if (recoveryPath != null && !File.Exists(snapshotPath) && File.Exists(recoveryPath))
                    File.Move(recoveryPath, snapshotPath);
                throw;
            }
        }

        internal static void PromoteCompletedAutomaticSnapshot(string completedSnapshotPath, string snapshotPath)
        {
            if (!File.Exists(snapshotPath))
            {
                File.Move(completedSnapshotPath, snapshotPath);
                return;
            }

            try
            {
                File.Replace(completedSnapshotPath, snapshotPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or PlatformNotSupportedException)
            {
                log.Debug($"Atomic automatic snapshot replacement was unavailable for '{snapshotPath}': {ex.Message}");
            }

            string previousPath = $"{snapshotPath}.{Guid.NewGuid():N}.previous";
            File.Move(snapshotPath, previousPath);
            try
            {
                File.Move(completedSnapshotPath, snapshotPath);
            }
            catch
            {
                if (!File.Exists(snapshotPath) && File.Exists(previousPath))
                    File.Move(previousPath, snapshotPath);
                throw;
            }

            try
            {
                File.Delete(previousPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                log.Debug($"Unable to remove the replaced automatic snapshot '{previousPath}': {ex.Message}");
            }
        }

        private static string MoveSnapshotToRecovery(string snapshotPath, string reason)
        {
            string recoveryDirectory = Path.Combine(Path.GetDirectoryName(snapshotPath)!, "Recovery");
            Directory.CreateDirectory(recoveryDirectory);
            string recoveryFileName = $"{Path.GetFileNameWithoutExtension(snapshotPath)}-{reason}-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}{Path.GetExtension(snapshotPath)}";
            string recoveryPath = Path.Combine(recoveryDirectory, recoveryFileName);
            File.Move(snapshotPath, recoveryPath);
            log.Warn($"Moved snapshot to recovery storage: {recoveryPath}");
            return recoveryPath;
        }

        private static ApplicationSnapshotManifest? ReadManifest(ZipArchive archive, string snapshotPath)
        {
            ZipArchiveEntry? entry = archive.GetEntry(ManifestFileName);
            if (entry == null)
                return null;

            try
            {
                using Stream stream = entry.Open();
                return JsonSerializer.Deserialize<ApplicationSnapshotManifest>(stream);
            }
            catch (JsonException ex)
            {
                log.Warn($"Failed to read snapshot manifest: {snapshotPath}. {ex.Message}");
                return null;
            }
        }

        private static void RestoreSnapshotCore(ApplicationSnapshotInfo snapshot, CancellationToken cancellationToken)
        {
            if (!File.Exists(snapshot.FilePath))
                throw new FileNotFoundException("Snapshot file does not exist.", snapshot.FilePath);

            string restoreRoot = Path.Combine(Path.GetTempPath(), "ColorVisionSnapshotRestore");
            Directory.CreateDirectory(restoreRoot);
            string stageDirectory = Path.Combine(restoreRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stageDirectory);

            ZipFile.ExtractToDirectory(snapshot.FilePath, stageDirectory, true);
            cancellationToken.ThrowIfCancellationRequested();

            string manifestPath = Path.Combine(stageDirectory, ManifestFileName);
            if (File.Exists(manifestPath))
                File.Delete(manifestPath);

            string programDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            string executableName = Path.GetFileName(Environment.ProcessPath) ?? "ColorVision.exe";
            RemoveShellExtensionFilesFromRestoreStage(stageDirectory);

            string batchPath = Path.Combine(stageDirectory, "update.bat");
            File.WriteAllText(batchPath, string.Empty);
            ExitUpdateHandoffState handoffState = ExitUpdateHandoff.Prepare(programDirectory, stageDirectory);
            File.WriteAllText(
                batchPath,
                CreateRestoreBatch(stageDirectory, programDirectory, executableName, Environment.ProcessId, handoffState),
                Encoding.UTF8);

            ProcessStartInfo startInfo = new()
            {
                FileName = batchPath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };

            if (!ApplicationUpdatePrivilegeBroker.TryPrepareApplicationDirectory())
            {
                startInfo.Verb = "runas";
                startInfo.WindowStyle = ProcessWindowStyle.Normal;
            }

            try
            {
                using Process restoreProcess = ExitUpdateHandoff.Start(handoffState, startInfo);
                ApplicationUpdateShutdown.Request();
            }
            catch
            {
                ExitUpdateHandoff.Clear(handoffState);
                TryDeleteRestoreStage(stageDirectory);
                throw;
            }
        }

        internal static string CreateRestoreBatch(
            string stageDirectory,
            string programDirectory,
            string executableName,
            int originalProcessId,
            ExitUpdateHandoffState handoffState)
        {
            string executablePath = Path.Combine(programDirectory, executableName);
            StringBuilder sb = new();
            sb.AppendLine("@echo off");
            sb.AppendLine("setlocal DisableDelayedExpansion");
            sb.AppendLine("title ColorVision Snapshot Restore");
            sb.AppendLine($"set \"STAGE={EscapeForBatchValue(stageDirectory)}\"");
            sb.AppendLine($"set \"TARGET={EscapeForBatchValue(programDirectory)}\"");
            sb.AppendLine($"set \"EXE={EscapeForBatchValue(executableName)}\"");
            sb.AppendLine($"set \"EXEPATH={EscapeForBatchValue(executablePath)}\"");
            ExternalUpdateBatchScript.AppendSessionVariables(sb, originalProcessId, handoffState);
            sb.AppendLine("call :wait_for_original_process");
            ExternalUpdateBatchScript.AppendLog(sb, "Snapshot restore started.");
            sb.AppendLine("robocopy \"%STAGE%\" \"%TARGET%\" *.* /E /XF update.bat snapshot-manifest.json /NFL /NDL /NP /NJH /NJS /R:2 /W:1");
            sb.AppendLine("if %ERRORLEVEL% GEQ 8 goto fail");
            ExternalUpdateBatchScript.AppendLog(sb, "Snapshot restore completed.");
            ExternalUpdateBatchScript.AppendRestartAndComplete(sb, restartArguments: null);
            sb.AppendLine("start \"\" /d \"%TEMP%\" /b cmd /d /c ping -n 4 127.0.0.1 ^>nul ^& rd /s /q \"%STAGE%\" 2^>nul");
            sb.AppendLine("exit /b 0");
            sb.AppendLine(":fail");
            ExternalUpdateBatchScript.AppendLog(sb, "Snapshot restore failed.");
            ExternalUpdateBatchScript.AppendRestartAndComplete(sb, restartArguments: null);
            sb.AppendLine("exit /b 1");
            ExternalUpdateBatchScript.AppendWaitForOriginalProcess(sb);
            return sb.ToString();
        }

        internal static int RemoveShellExtensionFilesFromRestoreStage(string stageDirectory)
        {
            if (!Directory.Exists(stageDirectory))
                return 0;

            int removedCount = 0;
            foreach (string filePath in Directory.EnumerateFiles(stageDirectory, "ColorVision.ShellExtension*", SearchOption.AllDirectories))
            {
                FileAttributes attributes = File.GetAttributes(filePath);
                if (attributes.HasFlag(FileAttributes.ReadOnly))
                    File.SetAttributes(filePath, attributes & ~FileAttributes.ReadOnly);

                File.Delete(filePath);
                removedCount++;
            }

            return removedCount;
        }

        private static void TryDeleteRestoreStage(string stageDirectory)
        {
            try
            {
                if (Directory.Exists(stageDirectory))
                    Directory.Delete(stageDirectory, recursive: true);
            }
            catch (Exception ex)
            {
                log.Debug($"Failed to delete unused snapshot restore stage '{stageDirectory}': {ex.Message}");
            }
        }

        public static string GetCurrentVersionText()
        {
            return Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
                ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? "unknown";
        }

        private static string SanitizeFilePart(string value)
        {
            string sanitized = Regex.Replace(value, @"[\\/:*?""<>|]+", "_");
            return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
        }

        private static string EscapeForBatchValue(string value)
        {
            return value
                .Replace("^", "^^")
                .Replace("&", "^&")
                .Replace("|", "^|")
                .Replace("<", "^<")
                .Replace(">", "^>");
        }

        private void CreateHealthyStartupAutomaticSnapshot()
        {
            Thread.Sleep(HealthyStartupSnapshotDelay);
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                ApplicationSnapshotConfig config = GetSnapshotConfig();
                if (!config.CreateAutomaticSnapshotAfterHealthyStartup)
                    return;

                string snapshotDirectory = ResolveAutomaticSnapshotDirectory(config.AutomaticSnapshotDirectory);
                string snapshotPath = Path.Combine(snapshotDirectory, AutomaticSnapshotFileName);
                string currentVersion = GetCurrentVersionText();
                if (!ShouldCreateAutomaticSnapshot(snapshotPath, currentVersion))
                {
                    log.Info($"Automatic application snapshot is already current: {snapshotPath}");
                    return;
                }

                DeleteAbandonedAutomaticSnapshotFiles(snapshotDirectory);
                ApplicationSnapshotInfo snapshot = CreateSnapshotCore(
                    snapshotPath,
                    SnapshotKind.Automatic,
                    versionTarget: string.Empty,
                    overwrite: true,
                    CancellationToken.None);
                try
                {
                    SnapshotCreated?.Invoke(this, snapshot);
                }
                catch (Exception ex)
                {
                    log.Debug($"Automatic snapshot notification failed: {ex.Message}");
                }
                log.Info($"Created automatic application snapshot in {stopwatch.ElapsedMilliseconds} ms: {snapshotPath}");
            }
            catch (Exception ex)
            {
                log.Warn($"Automatic application snapshot failed after {stopwatch.ElapsedMilliseconds} ms: {ex.Message}");
            }
        }

        private static ApplicationSnapshotConfig GetSnapshotConfig()
        {
            return ConfigService.Instance.GetRequiredService<ApplicationSnapshotConfig>();
        }

        private static string? GetConfiguredAutomaticSnapshotDirectory()
        {
            try
            {
                return ConfigService.Instance?.GetRequiredService<ApplicationSnapshotConfig>().AutomaticSnapshotDirectory;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private static void DeleteAbandonedAutomaticSnapshotFiles(string snapshotDirectory)
        {
            if (!Directory.Exists(snapshotDirectory))
                return;

            foreach (string filePath in Directory.EnumerateFiles(snapshotDirectory, $"{AutomaticSnapshotFileName}.*.tmp", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    log.Debug($"Unable to remove abandoned automatic snapshot file '{filePath}': {ex.Message}");
                }
            }

            foreach (string filePath in Directory.EnumerateFiles(snapshotDirectory, $"{AutomaticSnapshotFileName}.*.previous", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    log.Debug($"Unable to remove a replaced automatic snapshot '{filePath}': {ex.Message}");
                }
            }
        }

        private static bool IsPathUnderDirectory(string directory, string candidatePath)
        {
            string normalizedDirectory = NormalizeDirectory(directory);
            string prefix = Path.EndsInDirectorySeparator(normalizedDirectory)
                ? normalizedDirectory
                : normalizedDirectory + Path.DirectorySeparatorChar;
            return candidatePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeDirectory(string directory)
        {
            string fullPath = Path.GetFullPath(directory);
            string? root = Path.GetPathRoot(fullPath);
            string trimmed = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.IsNullOrEmpty(trimmed) || (root != null && trimmed.Length < root.Length)
                ? fullPath
                : trimmed;
        }

        private enum SnapshotKind
        {
            Default,
            User,
            Update,
            Automatic
        }
    }
}
