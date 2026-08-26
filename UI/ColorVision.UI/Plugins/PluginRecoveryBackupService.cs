using ColorVision.UI.ServiceHost;
using ColorVision.Update;
using log4net;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ColorVision.UI.Plugins
{
    public interface IPluginRecoveryBackupService
    {
        PluginRecoveryBackupInfo? GetAvailableBackup(string pluginId, string pluginDirectory);

        Task RestoreAsync(PluginRecoveryBackupInfo backup, CancellationToken cancellationToken = default);
    }

    public sealed class PluginRecoveryManifestMetadata
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Version { get; set; } = string.Empty;

        public string Requires { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string DllPath { get; set; } = string.Empty;

        public string Author { get; set; } = string.Empty;

        public string Url { get; set; } = string.Empty;

        public string EntryPoint { get; set; } = string.Empty;

        public string Icon { get; set; } = string.Empty;

        public int ManifestVersion { get; set; }

        public string Sha256 { get; set; } = string.Empty;
    }

    public sealed class PluginRecoveryBackupInfo
    {
        public required string PluginId { get; init; }

        public required string PluginDirectory { get; init; }

        public required string BackupDirectory { get; init; }

        public required string PayloadDirectory { get; init; }

        public required string InstallationKey { get; init; }

        public required DateTimeOffset CreatedUtc { get; init; }

        public required string DirectoryHash { get; init; }

        public required int FileCount { get; init; }

        public required long TotalBytes { get; init; }

        public PluginRecoveryManifestMetadata? Manifest { get; init; }

        public string Version => Manifest?.Version ?? string.Empty;

        public string PluginName => string.IsNullOrWhiteSpace(Manifest?.Name) ? PluginId : Manifest.Name;
    }

    /// <summary>
    /// Creates and validates plugin-directory backups in an installation-scoped local data root.
    /// A backup is considered available only when its complete file catalog and SHA-256 digest match.
    /// </summary>
    public sealed class PluginRecoveryBackupService : IPluginRecoveryBackupService
    {
        private const int CurrentSchemaVersion = 1;
        private const int MaximumCompletedBackupsPerPlugin = 3;
        private const string MetadataFileName = "backup.json";
        private const string PayloadDirectoryName = "payload";
        private static readonly TimeSpan HealthyStartupBackupDelay = TimeSpan.FromSeconds(2);
        private static readonly ILog log = LogManager.GetLogger(typeof(PluginRecoveryBackupService));
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
        };

        private readonly string _backupRootDirectory;
        private readonly ConcurrentDictionary<string, PluginRecoveryBackupInfo> _preparedBackups = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, object> _pluginBackupLocks = new(StringComparer.OrdinalIgnoreCase);
        private int _healthyStartupBackupScheduled;

        public static PluginRecoveryBackupService Instance { get; } = new();

        public string BackupRootDirectory => _backupRootDirectory;

        public PluginRecoveryBackupService(string? backupRootDirectory = null)
        {
            string configuredBackupRoot = backupRootDirectory ?? Path.Combine(
                Environments.DirLocalAppData,
                "PluginRecovery");
            if (!Path.IsPathFullyQualified(configuredBackupRoot))
                throw new ArgumentException("Plugin recovery backup root must be an absolute path.", nameof(backupRootDirectory));
            _backupRootDirectory = Path.GetFullPath(configuredBackupRoot);
        }

        public void ScheduleHealthyStartupBackups()
        {
            if (Interlocked.Exchange(ref _healthyStartupBackupScheduled, 1) != 0)
                return;

            try
            {
                new Thread(PrepareHealthyStartupBackups)
                {
                    IsBackground = true,
                    Name = "ColorVision plugin recovery backup",
                    Priority = ThreadPriority.BelowNormal,
                }.Start();
            }
            catch (Exception ex)
            {
                Volatile.Write(ref _healthyStartupBackupScheduled, 0);
                log.Warn($"Unable to start the healthy-start plugin recovery backup worker: {ex.Message}");
            }
        }

        public PluginRecoveryBackupInfo? EnsureCurrentVersionBackup(
            string pluginId,
            string pluginDirectory,
            CancellationToken cancellationToken = default)
        {
            PluginLocation location = ResolvePluginLocation(pluginId, pluginDirectory);
            string preparedBackupKey = GetPreparedBackupKey(location);
            object backupLock = _pluginBackupLocks.GetOrAdd(preparedBackupKey, static _ => new object());

            lock (backupLock)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PluginRecoveryManifestMetadata? currentManifest = TryReadManifestMetadata(Path.Combine(location.PluginDirectory, "manifest.json"));
                if (currentManifest != null
                    && _preparedBackups.TryGetValue(preparedBackupKey, out PluginRecoveryBackupInfo? preparedBackup)
                    && Directory.Exists(preparedBackup.BackupDirectory)
                    && ManifestMetadataMatches(preparedBackup.Manifest, currentManifest))
                {
                    log.Info($"Reused healthy-start plugin recovery backup for '{pluginId}': {preparedBackup.BackupDirectory}");
                    return preparedBackup;
                }

                PluginRecoveryBackupInfo? availableBackup = GetAvailableBackup(pluginId, pluginDirectory);
                if (currentManifest != null
                    && availableBackup != null
                    && ManifestMetadataMatches(availableBackup.Manifest, currentManifest))
                {
                    _preparedBackups[preparedBackupKey] = availableBackup;
                    log.Info($"Reused verified plugin recovery backup for current '{pluginId}' installation: {availableBackup.BackupDirectory}");
                    return availableBackup;
                }

                return CreateVerifiedBackup(pluginId, pluginDirectory, cancellationToken);
            }
        }

        public PluginRecoveryBackupInfo? CreateVerifiedBackup(string pluginId, string pluginDirectory, CancellationToken cancellationToken = default)
        {
            PluginLocation location = ResolvePluginLocation(pluginId, pluginDirectory);
            if (!TryGetExistingDirectoryAttributes(location.PluginDirectory, out FileAttributes pluginAttributes))
                return null;
            if (pluginAttributes.HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidDataException($"Plugin recovery backup cannot follow a reparse-point directory: {location.PluginDirectory}");
            if (PathsOverlap(location.PluginDirectory, _backupRootDirectory))
                throw new InvalidOperationException("Plugin recovery backup storage must be outside the plugin directory being backed up.");

            cancellationToken.ThrowIfCancellationRequested();
            string pluginBackupRoot = GetPluginBackupRoot(location.InstallationKey, pluginId);
            Directory.CreateDirectory(pluginBackupRoot);
            EnsureExistingDirectoryIsNotReparsePoint(_backupRootDirectory, "Plugin recovery backup root");
            EnsureExistingDirectoryIsNotReparsePoint(Path.Combine(_backupRootDirectory, location.InstallationKey), "Installation-scoped plugin backup root");
            EnsureExistingDirectoryIsNotReparsePoint(pluginBackupRoot, "Plugin-scoped recovery backup root");

            DateTimeOffset createdUtc = DateTimeOffset.UtcNow;
            string backupName = $"{createdUtc:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}";
            string completedDirectory = Path.Combine(pluginBackupRoot, backupName);
            string creatingDirectory = completedDirectory + ".creating";
            string payloadDirectory = Path.Combine(creatingDirectory, PayloadDirectoryName);

            try
            {
                Directory.CreateDirectory(payloadDirectory);
                DirectoryCatalog sourceBeforeCopy = CreateDirectoryCatalog(location.PluginDirectory, cancellationToken);
                CopyDirectory(location.PluginDirectory, payloadDirectory, cancellationToken);
                DirectoryCatalog sourceAfterCopy = CreateDirectoryCatalog(location.PluginDirectory, cancellationToken);
                DirectoryCatalog backupCatalog = CreateDirectoryCatalog(payloadDirectory, cancellationToken);

                if (!sourceBeforeCopy.EqualsContent(sourceAfterCopy)
                    || !sourceBeforeCopy.EqualsContent(backupCatalog))
                {
                    throw new IOException($"Plugin '{pluginId}' changed while its recovery backup was being created.");
                }

                PluginRecoveryBackupMetadata metadata = new()
                {
                    SchemaVersion = CurrentSchemaVersion,
                    PluginId = pluginId,
                    PluginDirectory = location.PluginDirectory,
                    ProgramDirectory = location.ProgramDirectory,
                    InstallationKey = location.InstallationKey,
                    CreatedUtc = createdUtc,
                    DirectoryHash = backupCatalog.DirectoryHash,
                    FileCount = backupCatalog.Files.Count,
                    TotalBytes = backupCatalog.Files.Sum(file => file.Length),
                    Manifest = TryReadManifestMetadata(Path.Combine(payloadDirectory, "manifest.json")),
                    Files = backupCatalog.Files.ToList(),
                };

                File.WriteAllText(
                    Path.Combine(creatingDirectory, MetadataFileName),
                    JsonSerializer.Serialize(metadata, JsonOptions),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                cancellationToken.ThrowIfCancellationRequested();
                Directory.Move(creatingDirectory, completedDirectory);

                PluginRecoveryBackupInfo backup = ReadAndValidateBackup(completedDirectory, location, cancellationToken);
                _preparedBackups[GetPreparedBackupKey(location)] = backup;
                log.Info($"Created verified plugin recovery backup for '{pluginId}': {completedDirectory}");
                PruneOlderVerifiedBackups(pluginBackupRoot, completedDirectory, location);
                return backup;
            }
            catch
            {
                TryDeleteDirectory(creatingDirectory);
                throw;
            }
        }

        public PluginRecoveryBackupInfo? GetAvailableBackup(string pluginId, string pluginDirectory)
        {
            PluginLocation location;
            try
            {
                location = ResolvePluginLocation(pluginId, pluginDirectory);
            }
            catch (ArgumentException)
            {
                return null;
            }

            string pluginBackupRoot = GetPluginBackupRoot(location.InstallationKey, pluginId);
            if (!Directory.Exists(pluginBackupRoot))
                return null;
            EnsureDirectoryChainIsNotReparsePoint(_backupRootDirectory, pluginBackupRoot);

            foreach (string backupDirectory in Directory
                .EnumerateDirectories(pluginBackupRoot, "*", SearchOption.TopDirectoryOnly)
                .Where(path => !path.EndsWith(".creating", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            {
                if (TryReadBackupMetadata(backupDirectory, location, out PluginRecoveryBackupInfo? backup))
                    return backup;
            }

            return null;
        }

        public PluginRecoveryBackupInfo? GetRecoveryBackupCandidate(string pluginId, string pluginDirectory)
        {
            PluginLocation location;
            try
            {
                location = ResolvePluginLocation(pluginId, pluginDirectory);
            }
            catch (ArgumentException)
            {
                return null;
            }

            string pluginBackupRoot = GetPluginBackupRoot(location.InstallationKey, pluginId);
            if (!Directory.Exists(pluginBackupRoot))
                return null;
            EnsureDirectoryChainIsNotReparsePoint(_backupRootDirectory, pluginBackupRoot);

            foreach (string backupDirectory in Directory
                .EnumerateDirectories(pluginBackupRoot, "*", SearchOption.TopDirectoryOnly)
                .Where(path => !path.EndsWith(".creating", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            {
                if (TryReadRecoveryBackupCandidate(backupDirectory, location, out PluginRecoveryBackupInfo? backup))
                    return backup;
            }

            return null;
        }

        public IReadOnlyList<PluginRecoveryBackupInfo> GetAvailableBackups(string programDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(programDirectory);
            if (!Path.IsPathFullyQualified(programDirectory))
                throw new ArgumentException("ColorVision installation directory must be an absolute path.", nameof(programDirectory));
            string normalizedProgramDirectory = NormalizeDirectory(programDirectory);
            string installationKey = ExitUpdateHandoff.GetInstallationKey(normalizedProgramDirectory);
            string installationRoot = Path.Combine(_backupRootDirectory, installationKey);
            if (!Directory.Exists(installationRoot))
                return Array.Empty<PluginRecoveryBackupInfo>();
            EnsureDirectoryChainIsNotReparsePoint(_backupRootDirectory, installationRoot);

            var latestBackups = new List<PluginRecoveryBackupInfo>();
            foreach (string pluginRoot in Directory.EnumerateDirectories(installationRoot, "*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    EnsureExistingDirectoryIsNotReparsePoint(pluginRoot, "Plugin-scoped recovery backup root", mustExist: true);
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
                {
                    log.Warn($"Ignored unsafe plugin recovery backup root '{pluginRoot}': {ex.Message}");
                    continue;
                }

                PluginRecoveryBackupInfo? latest = Directory
                    .EnumerateDirectories(pluginRoot, "*", SearchOption.TopDirectoryOnly)
                    .Where(path => !path.EndsWith(".creating", StringComparison.OrdinalIgnoreCase))
                    .Select(path => TryReadBackupMetadata(path, expectedLocation: null, out PluginRecoveryBackupInfo? backup) ? backup : null)
                    .Where(backup => backup != null
                        && string.Equals(backup.InstallationKey, installationKey, StringComparison.Ordinal)
                        && string.Equals(
                            NormalizeDirectory(Path.GetDirectoryName(Path.GetDirectoryName(backup.PluginDirectory)!)!),
                            normalizedProgramDirectory,
                            StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(backup => backup!.CreatedUtc)
                    .ThenByDescending(backup => backup!.BackupDirectory, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                if (latest != null)
                    latestBackups.Add(latest);
            }

            return latestBackups
                .OrderByDescending(backup => backup.CreatedUtc)
                .ThenBy(backup => backup.PluginName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public IReadOnlyList<PluginRecoveryBackupInfo> GetRecoveryBackupCandidates(string programDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(programDirectory);
            if (!Path.IsPathFullyQualified(programDirectory))
                throw new ArgumentException("ColorVision installation directory must be an absolute path.", nameof(programDirectory));
            string normalizedProgramDirectory = NormalizeDirectory(programDirectory);
            string installationKey = ExitUpdateHandoff.GetInstallationKey(normalizedProgramDirectory);
            string installationRoot = Path.Combine(_backupRootDirectory, installationKey);
            if (!Directory.Exists(installationRoot))
                return Array.Empty<PluginRecoveryBackupInfo>();
            EnsureDirectoryChainIsNotReparsePoint(_backupRootDirectory, installationRoot);

            var latestBackups = new List<PluginRecoveryBackupInfo>();
            foreach (string pluginRoot in Directory.EnumerateDirectories(installationRoot, "*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    EnsureExistingDirectoryIsNotReparsePoint(pluginRoot, "Plugin-scoped recovery backup root", mustExist: true);
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
                {
                    log.Warn($"Ignored unsafe plugin recovery backup root '{pluginRoot}': {ex.Message}");
                    continue;
                }

                foreach (string backupDirectory in Directory
                    .EnumerateDirectories(pluginRoot, "*", SearchOption.TopDirectoryOnly)
                    .Where(path => !path.EndsWith(".creating", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
                {
                    if (!TryReadRecoveryBackupCandidate(backupDirectory, expectedLocation: null, out PluginRecoveryBackupInfo? backup)
                        || backup == null
                        || !string.Equals(backup.InstallationKey, installationKey, StringComparison.Ordinal)
                        || !string.Equals(
                            NormalizeDirectory(Path.GetDirectoryName(Path.GetDirectoryName(backup.PluginDirectory)!)!),
                            normalizedProgramDirectory,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    latestBackups.Add(backup);
                    break;
                }
            }

            return latestBackups
                .OrderByDescending(backup => backup.CreatedUtc)
                .ThenBy(backup => backup.PluginName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public PluginRecoveryBackupInfo ReadBackupMetadata(string backupDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(backupDirectory);
            if (!Path.IsPathFullyQualified(backupDirectory))
                throw new ArgumentException("Plugin recovery backup directory must be an absolute path.", nameof(backupDirectory));
            return ReadAndValidateBackup(Path.GetFullPath(backupDirectory), expectedLocation: null);
        }

        public bool TryReadBackupMetadata(string backupDirectory, out PluginRecoveryBackupInfo? backup)
        {
            if (string.IsNullOrWhiteSpace(backupDirectory))
            {
                backup = null;
                return false;
            }
            if (!Path.IsPathFullyQualified(backupDirectory))
            {
                backup = null;
                return false;
            }

            return TryReadBackupMetadata(Path.GetFullPath(backupDirectory), expectedLocation: null, out backup);
        }

        public async Task RestoreAsync(PluginRecoveryBackupInfo backup, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(backup);
            PluginLocation location = ResolvePluginLocation(backup.PluginId, backup.PluginDirectory);
            string currentProgramDirectory = NormalizeDirectory(AppDomain.CurrentDomain.BaseDirectory);
            if (!string.Equals(location.ProgramDirectory, currentProgramDirectory, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("A plugin backup can only be restored to the currently running ColorVision installation.");

            PluginRecoveryBackupInfo validatedBackup = ReadAndValidateBackup(backup.BackupDirectory, location, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            string? restoreRoot = null;
            ExitUpdateHandoffState? handoffState = null;
            bool handoffStarted = false;
            try
            {
                restoreRoot = Path.Combine(Path.GetTempPath(), $"ColorVisionPluginRestore-{Guid.NewGuid():N}");
                string stageDirectory = Path.Combine(restoreRoot, "Plugin");
                await Task.Run(() =>
                {
                    Directory.CreateDirectory(stageDirectory);
                    CopyDirectory(validatedBackup.PayloadDirectory, stageDirectory, cancellationToken);
                    DirectoryCatalog stagedCatalog = CreateDirectoryCatalog(stageDirectory, cancellationToken);
                    if (!string.Equals(stagedCatalog.DirectoryHash, validatedBackup.DirectoryHash, StringComparison.OrdinalIgnoreCase)
                        || stagedCatalog.Files.Count != validatedBackup.FileCount
                        || stagedCatalog.Files.Sum(file => file.Length) != validatedBackup.TotalBytes)
                    {
                        throw new InvalidDataException("The staged plugin recovery payload did not match its verified backup.");
                    }
                }, cancellationToken);

                ConfigService.Instance.SaveConfigs();
                PluginLoaderrConfig.Instance.Save();
                ApplicationUpdateProcessCoordinator.CloseOtherApplicationProcesses();

                string executablePath = Environment.ProcessPath
                    ?? Process.GetCurrentProcess().MainModule?.FileName
                    ?? throw new InvalidOperationException("Cannot determine the current executable path.");
                string executableName = Path.GetFileName(executablePath);
                // ExitUpdateHandoff identifies an active external update by the conventional
                // update.bat name, so recovery must use the same handoff contract.
                string batchPath = Path.Combine(restoreRoot, "update.bat");
                File.WriteAllText(batchPath, string.Empty);
                handoffState = ExitUpdateHandoff.Prepare(location.ProgramDirectory, restoreRoot);
                File.WriteAllText(
                    batchPath,
                    CreateRestoreBatch(
                        batchPath,
                        stageDirectory,
                        location.PluginDirectory,
                        location.ProgramDirectory,
                        executableName,
                        Environment.ProcessId,
                        handoffState),
                    Encoding.GetEncoding(936));

                ProcessStartInfo startInfo = new()
                {
                    FileName = batchPath,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = restoreRoot,
                };
                if (!ApplicationUpdatePrivilegeBroker.TryPrepareApplicationDirectory())
                {
                    startInfo.Verb = "runas";
                }

                using Process restoreProcess = ExitUpdateHandoff.Start(handoffState, startInfo);
                handoffStarted = true;
                try
                {
                    ApplicationUpdateShutdown.Request();
                }
                catch (Exception ex)
                {
                    log.Error("Plugin recovery updater started, but application shutdown could not be requested. The handoff remains active.", ex);
                }
            }
            catch
            {
                if (!handoffStarted)
                {
                    ExitUpdateHandoff.Clear(handoffState);
                    TryDeleteDirectory(restoreRoot);
                }
                throw;
            }
        }

        internal static string CreateRestoreBatch(
            string batchFilePath,
            string stageDirectory,
            string pluginDirectory,
            string programDirectory,
            string executableName,
            int originalProcessId,
            ExitUpdateHandoffState handoffState)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(batchFilePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(stageDirectory);
            ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);
            ArgumentException.ThrowIfNullOrWhiteSpace(programDirectory);
            ArgumentException.ThrowIfNullOrWhiteSpace(executableName);

            string pluginsDirectory = Path.GetDirectoryName(pluginDirectory)
                ?? throw new ArgumentException("Plugin directory must have a parent directory.", nameof(pluginDirectory));
            string transactionDirectory = Path.Combine(
                pluginsDirectory,
                $".ColorVisionRecovery-{Guid.NewGuid():N}");

            StringBuilder builder = new();
            builder.AppendLine("@echo off");
            builder.AppendLine("setlocal DisableDelayedExpansion");
            builder.AppendLine("title ColorVision Plugin Recovery");
            builder.AppendLine($"set \"EXEPATH={EscapeForBatchValue(Path.Combine(programDirectory, executableName))}\"");
            builder.AppendLine($"set \"UPDATE_ROOT={EscapeForBatchValue(Path.GetDirectoryName(batchFilePath)!)}\"");
            ExternalUpdateBatchScript.AppendSessionVariables(builder, originalProcessId, handoffState);
            builder.AppendLine("call :wait_for_original_process");
            ExternalUpdateBatchScript.AppendLog(builder, "Plugin recovery started.");
            PluginDirectoryTransactionBatchScript.AppendTransaction(
                builder,
                [new PluginDirectoryReplacement(Path.GetFileName(pluginDirectory), stageDirectory, pluginDirectory)],
                transactionDirectory,
                failureLabel: "fail",
                labelPrefix: "plugin_recovery_transaction");
            ExternalUpdateBatchScript.AppendLog(builder, "Plugin recovery completed.");
            builder.AppendLine("call :complete_handoff");
            builder.AppendLine("call :schedule_cleanup");
            builder.AppendLine("endlocal");
            builder.AppendLine("exit /b 0");
            builder.AppendLine(":fail");
            ExternalUpdateBatchScript.AppendLog(builder, "Plugin recovery failed.");
            builder.AppendLine("call :complete_handoff");
            builder.AppendLine("call :schedule_cleanup");
            builder.AppendLine("endlocal");
            builder.AppendLine("exit /b 1");
            builder.AppendLine(":complete_handoff");
            ExternalUpdateBatchScript.AppendRestartAndComplete(builder, restartArguments: null);
            builder.AppendLine("exit /b 0");
            builder.AppendLine(":schedule_cleanup");
            builder.AppendLine("start \"\" /d \"%TEMP%\" /b cmd /d /c ping -n 4 127.0.0.1 ^>nul ^& rd /s /q \"%UPDATE_ROOT%\" 2^>nul");
            builder.AppendLine("exit /b 0");
            PluginDirectoryTransactionBatchScript.AppendCopyCompleteDirectoryFunction(builder);
            ExternalUpdateBatchScript.AppendWaitForOriginalProcess(builder);
            return builder.ToString();
        }

        private PluginRecoveryBackupInfo ReadAndValidateBackup(
            string backupDirectory,
            PluginLocation? expectedLocation,
            CancellationToken cancellationToken = default)
        {
            (PluginRecoveryBackupInfo backup, PluginRecoveryBackupMetadata metadata) =
                ReadBackupMetadataCore(backupDirectory, expectedLocation, cancellationToken);
            DirectoryCatalog actualCatalog = CreateDirectoryCatalog(backup.PayloadDirectory, cancellationToken);
            if (metadata.FileCount != actualCatalog.Files.Count
                || metadata.TotalBytes != actualCatalog.Files.Sum(file => file.Length)
                || !string.Equals(metadata.DirectoryHash, actualCatalog.DirectoryHash, StringComparison.OrdinalIgnoreCase)
                || !new DirectoryCatalog(metadata.Files, metadata.DirectoryHash).EqualsContent(actualCatalog))
            {
                throw new InvalidDataException("Plugin recovery backup payload failed SHA-256 verification.");
            }

            PluginRecoveryManifestMetadata? actualManifest = TryReadManifestMetadata(
                Path.Combine(backup.PayloadDirectory, "manifest.json"));
            if (!ManifestMetadataMatches(metadata.Manifest, actualManifest))
                throw new InvalidDataException("Plugin recovery backup manifest metadata does not match its payload.");

            return backup;
        }

        private (PluginRecoveryBackupInfo Backup, PluginRecoveryBackupMetadata Metadata) ReadBackupMetadataCore(
            string backupDirectory,
            PluginLocation? expectedLocation,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string normalizedBackupDirectory = NormalizeDirectory(backupDirectory);
            EnsureDirectoryChainIsNotReparsePoint(_backupRootDirectory, normalizedBackupDirectory);
            string metadataPath = Path.Combine(normalizedBackupDirectory, MetadataFileName);
            if (!File.Exists(metadataPath))
                throw new InvalidDataException("Plugin recovery backup metadata is missing.");

            PluginRecoveryBackupMetadata metadata;
            try
            {
                metadata = JsonSerializer.Deserialize<PluginRecoveryBackupMetadata>(
                    File.ReadAllText(metadataPath),
                    JsonOptions) ?? throw new InvalidDataException("Plugin recovery backup metadata is empty.");
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Plugin recovery backup metadata is invalid.", ex);
            }

            if (metadata.SchemaVersion != CurrentSchemaVersion
                || string.IsNullOrWhiteSpace(metadata.PluginId)
                || string.IsNullOrWhiteSpace(metadata.PluginDirectory)
                || string.IsNullOrWhiteSpace(metadata.ProgramDirectory)
                || string.IsNullOrWhiteSpace(metadata.InstallationKey)
                || string.IsNullOrWhiteSpace(metadata.DirectoryHash)
                || metadata.Files == null
                || metadata.FileCount < 0
                || metadata.TotalBytes < 0
                || metadata.FileCount != metadata.Files.Count
                || metadata.CreatedUtc == default)
            {
                throw new InvalidDataException("Plugin recovery backup metadata is incomplete or unsupported.");
            }

            PluginLocation metadataLocation = ResolvePluginLocation(metadata.PluginId, metadata.PluginDirectory);
            if (!string.Equals(metadataLocation.ProgramDirectory, NormalizeDirectory(metadata.ProgramDirectory), StringComparison.OrdinalIgnoreCase)
                || !string.Equals(metadataLocation.InstallationKey, metadata.InstallationKey, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Plugin recovery backup installation metadata does not match its target path.");
            }

            if (expectedLocation != null
                && (!string.Equals(expectedLocation.PluginId, metadata.PluginId, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(expectedLocation.PluginDirectory, metadataLocation.PluginDirectory, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(expectedLocation.InstallationKey, metadata.InstallationKey, StringComparison.Ordinal)))
            {
                throw new InvalidDataException("Plugin recovery backup belongs to a different plugin or installation.");
            }

            string expectedPluginRoot = NormalizeDirectory(GetPluginBackupRoot(metadata.InstallationKey, metadata.PluginId));
            if (!string.Equals(Path.GetDirectoryName(normalizedBackupDirectory), expectedPluginRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Plugin recovery backup is outside its installation-scoped backup directory.");

            string payloadDirectory = Path.Combine(normalizedBackupDirectory, PayloadDirectoryName);
            EnsureDirectoryChainIsNotReparsePoint(_backupRootDirectory, payloadDirectory);
            PluginRecoveryBackupInfo backup = new()
            {
                PluginId = metadata.PluginId,
                PluginDirectory = metadataLocation.PluginDirectory,
                BackupDirectory = normalizedBackupDirectory,
                PayloadDirectory = payloadDirectory,
                InstallationKey = metadata.InstallationKey,
                CreatedUtc = metadata.CreatedUtc,
                DirectoryHash = metadata.DirectoryHash,
                FileCount = metadata.FileCount,
                TotalBytes = metadata.TotalBytes,
                Manifest = metadata.Manifest,
            };
            return (backup, metadata);
        }

        private bool TryReadBackupMetadata(
            string backupDirectory,
            PluginLocation? expectedLocation,
            out PluginRecoveryBackupInfo? backup)
        {
            try
            {
                backup = ReadAndValidateBackup(backupDirectory, expectedLocation);
                return true;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or JsonException)
            {
                log.Warn($"Ignored invalid plugin recovery backup '{backupDirectory}': {ex.Message}");
                backup = null;
                return false;
            }
        }

        private bool TryReadRecoveryBackupCandidate(
            string backupDirectory,
            PluginLocation? expectedLocation,
            out PluginRecoveryBackupInfo? backup)
        {
            try
            {
                backup = ReadBackupMetadataCore(backupDirectory, expectedLocation).Backup;
                return true;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or JsonException)
            {
                log.Warn($"Ignored invalid plugin recovery backup metadata '{backupDirectory}': {ex.Message}");
                backup = null;
                return false;
            }
        }

        private void PruneOlderVerifiedBackups(
            string pluginBackupRoot,
            string currentBackupDirectory,
            PluginLocation expectedLocation)
        {
            try
            {
                List<PluginRecoveryBackupInfo> validBackups = Directory
                    .EnumerateDirectories(pluginBackupRoot, "*", SearchOption.TopDirectoryOnly)
                    .Where(path => !path.EndsWith(".creating", StringComparison.OrdinalIgnoreCase))
                    .Select(path => TryReadBackupMetadata(path, expectedLocation, out PluginRecoveryBackupInfo? backup) ? backup : null)
                    .Where(backup => backup != null)
                    .Cast<PluginRecoveryBackupInfo>()
                    .ToList();
                if (validBackups.Count <= MaximumCompletedBackupsPerPlugin)
                    return;

                string normalizedCurrentBackup = NormalizeDirectory(currentBackupDirectory);
                HashSet<string> retainedDirectories = validBackups
                    .Where(backup => !string.Equals(backup.BackupDirectory, normalizedCurrentBackup, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(backup => backup.CreatedUtc)
                    .ThenByDescending(backup => backup.BackupDirectory, StringComparer.OrdinalIgnoreCase)
                    .Take(MaximumCompletedBackupsPerPlugin - 1)
                    .Select(backup => backup.BackupDirectory)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                retainedDirectories.Add(normalizedCurrentBackup);

                foreach (PluginRecoveryBackupInfo oldBackup in validBackups
                    .Where(backup => !retainedDirectories.Contains(backup.BackupDirectory))
                    .OrderBy(backup => backup.CreatedUtc))
                {
                    try
                    {
                        Directory.Delete(oldBackup.BackupDirectory, recursive: true);
                        log.Info($"Removed older verified plugin recovery backup: {oldBackup.BackupDirectory}");
                    }
                    catch (Exception ex)
                    {
                        log.Warn($"Failed to remove older plugin recovery backup '{oldBackup.BackupDirectory}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Retention is best-effort and must never invalidate the newly verified backup.
                log.Warn($"Failed to enforce plugin recovery backup retention for '{expectedLocation.PluginId}': {ex.Message}");
            }
        }

        private void PrepareHealthyStartupBackups()
        {
            Thread.Sleep(HealthyStartupBackupDelay);
            Stopwatch stopwatch = Stopwatch.StartNew();
            int preparedCount = 0;
            try
            {
                string pluginsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
                if (!Directory.Exists(pluginsDirectory))
                    return;

                foreach (string pluginDirectory in Directory.EnumerateDirectories(pluginsDirectory, "*", SearchOption.TopDirectoryOnly))
                {
                    PluginRecoveryManifestMetadata? manifest = TryReadManifestMetadata(Path.Combine(pluginDirectory, "manifest.json"));
                    if (manifest == null
                        || string.IsNullOrWhiteSpace(manifest.Id)
                        || !string.Equals(Path.GetFileName(pluginDirectory), manifest.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        if (EnsureCurrentVersionBackup(manifest.Id, pluginDirectory) != null)
                            preparedCount++;
                    }
                    catch (Exception ex)
                    {
                        log.Warn($"Unable to prepare background recovery backup for plugin '{manifest.Id}': {ex.Message}");
                    }

                    Thread.Sleep(50);
                }
            }
            catch (Exception ex)
            {
                log.Warn($"Unable to enumerate plugins for background recovery backup: {ex.Message}");
            }
            finally
            {
                stopwatch.Stop();
                log.Info($"Healthy-start plugin recovery backup preparation completed for {preparedCount} plugin(s) in {stopwatch.ElapsedMilliseconds} ms.");
            }
        }

        private static string GetPreparedBackupKey(PluginLocation location) => $"{location.InstallationKey}\0{location.PluginId}";

        private string GetPluginBackupRoot(string installationKey, string pluginId) => Path.Combine(
            _backupRootDirectory,
            installationKey,
            GetPluginStorageKey(pluginId));

        private static string GetPluginStorageKey(string pluginId)
        {
            string readablePrefix = string.Concat(pluginId
                .Trim()
                .Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
            if (readablePrefix.Length > 48)
                readablePrefix = readablePrefix[..48];
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(pluginId.ToUpperInvariant())))[..16];
            return $"{readablePrefix}-{hash}";
        }

        private static PluginLocation ResolvePluginLocation(string pluginId, string pluginDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
            ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);
            if (!Path.IsPathFullyQualified(pluginDirectory))
                throw new ArgumentException("Plugin directory must be an absolute path.", nameof(pluginDirectory));
            string normalizedPluginDirectory = NormalizeDirectory(pluginDirectory);
            string? pluginsDirectory = Path.GetDirectoryName(normalizedPluginDirectory);
            string? programDirectory = pluginsDirectory == null ? null : Path.GetDirectoryName(pluginsDirectory);
            if (pluginsDirectory == null
                || programDirectory == null
                || !string.Equals(Path.GetFileName(pluginsDirectory), "Plugins", StringComparison.OrdinalIgnoreCase)
                || !PluginUpdater.TryGetPluginTargetDirectory(pluginsDirectory, pluginId, out string expectedPluginDirectory)
                || !string.Equals(normalizedPluginDirectory, NormalizeDirectory(expectedPluginDirectory), StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Plugin directory must be Plugins/<plugin-id> under one application installation.", nameof(pluginDirectory));
            }

            string normalizedProgramDirectory = NormalizeDirectory(programDirectory);
            EnsureExistingDirectoryIsNotReparsePoint(normalizedProgramDirectory, "ColorVision installation directory");
            EnsureExistingDirectoryIsNotReparsePoint(pluginsDirectory, "ColorVision Plugins directory");
            EnsureExistingDirectoryIsNotReparsePoint(normalizedPluginDirectory, "Plugin directory");
            return new PluginLocation(
                pluginId.Trim(),
                normalizedPluginDirectory,
                normalizedProgramDirectory,
                ExitUpdateHandoff.GetInstallationKey(normalizedProgramDirectory));
        }

        private static DirectoryCatalog CreateDirectoryCatalog(string rootDirectory, CancellationToken cancellationToken)
        {
            if (!Directory.Exists(rootDirectory))
                throw new DirectoryNotFoundException($"Plugin directory was not found: {rootDirectory}");

            string normalizedRoot = NormalizeDirectory(rootDirectory);
            if (File.GetAttributes(normalizedRoot).HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidDataException($"Plugin backup cannot use a reparse-point root: {normalizedRoot}");
            List<string> filePaths = EnumerateSafeFiles(normalizedRoot, cancellationToken)
                .OrderBy(path => Path.GetRelativePath(normalizedRoot, path), StringComparer.OrdinalIgnoreCase)
                .ToList();
            var files = new List<PluginRecoveryFileMetadata>(filePaths.Count);
            foreach (string filePath in filePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                FileInfo fileInfo = new(filePath);
                using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                string hash = Convert.ToHexString(SHA256.HashData(stream));
                files.Add(new PluginRecoveryFileMetadata
                {
                    RelativePath = NormalizeRelativePath(Path.GetRelativePath(normalizedRoot, filePath)),
                    Length = fileInfo.Length,
                    Sha256 = hash,
                });
            }

            return new DirectoryCatalog(files, ComputeDirectoryHash(files));
        }

        private static IEnumerable<string> EnumerateSafeFiles(string rootDirectory, CancellationToken cancellationToken)
        {
            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(rootDirectory);
            while (pendingDirectories.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string directory = pendingDirectories.Pop();
                foreach (string entry in Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.TopDirectoryOnly))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    FileAttributes attributes = File.GetAttributes(entry);
                    if (attributes.HasFlag(FileAttributes.ReparsePoint))
                        throw new InvalidDataException($"Plugin backup cannot include a reparse point: {entry}");
                    if (attributes.HasFlag(FileAttributes.Directory))
                        pendingDirectories.Push(entry);
                    else
                        yield return entry;
                }
            }
        }

        private static void CopyDirectory(string sourceDirectory, string targetDirectory, CancellationToken cancellationToken)
        {
            string normalizedSource = NormalizeDirectory(sourceDirectory);
            string normalizedTarget = NormalizeDirectory(targetDirectory);
            Directory.CreateDirectory(normalizedTarget);
            foreach (string sourceFile in EnumerateSafeFiles(normalizedSource, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string relativePath = Path.GetRelativePath(normalizedSource, sourceFile);
                string targetFile = Path.GetFullPath(Path.Combine(normalizedTarget, relativePath));
                if (!IsPathUnderDirectory(normalizedTarget, targetFile))
                    throw new InvalidDataException("Plugin backup file path escaped its target directory.");
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
                File.Copy(sourceFile, targetFile, overwrite: false);
            }

            foreach (string sourceSubdirectory in EnumerateSafeDirectories(normalizedSource, cancellationToken))
            {
                string relativePath = Path.GetRelativePath(normalizedSource, sourceSubdirectory);
                Directory.CreateDirectory(Path.Combine(normalizedTarget, relativePath));
            }
        }

        private static IEnumerable<string> EnumerateSafeDirectories(string rootDirectory, CancellationToken cancellationToken)
        {
            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(rootDirectory);
            while (pendingDirectories.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string directory = pendingDirectories.Pop();
                foreach (string child in Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly))
                {
                    if (File.GetAttributes(child).HasFlag(FileAttributes.ReparsePoint))
                        throw new InvalidDataException($"Plugin backup cannot include a reparse point: {child}");
                    yield return child;
                    pendingDirectories.Push(child);
                }
            }
        }

        private static string ComputeDirectoryHash(IReadOnlyList<PluginRecoveryFileMetadata> files)
        {
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (PluginRecoveryFileMetadata file in files.OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase))
            {
                byte[] line = Encoding.UTF8.GetBytes($"{file.RelativePath}\0{file.Length}\0{file.Sha256}\n");
                hash.AppendData(line);
            }
            return Convert.ToHexString(hash.GetHashAndReset());
        }

        private static PluginRecoveryManifestMetadata? TryReadManifestMetadata(string manifestPath)
        {
            if (!File.Exists(manifestPath))
                return null;

            try
            {
                byte[] bytes = File.ReadAllBytes(manifestPath);
                using JsonDocument document = JsonDocument.Parse(bytes);
                JsonElement root = document.RootElement;
                return new PluginRecoveryManifestMetadata
                {
                    Id = GetString(root, "id"),
                    Name = GetString(root, "name"),
                    Version = GetString(root, "version"),
                    Requires = GetString(root, "requires"),
                    Description = GetString(root, "description"),
                    DllPath = GetString(root, "dllpath"),
                    Author = GetString(root, "author"),
                    Url = GetString(root, "url"),
                    EntryPoint = GetString(root, "entry_point"),
                    Icon = GetString(root, "icon"),
                    ManifestVersion = root.TryGetProperty("manifest_version", out JsonElement manifestVersion)
                        && manifestVersion.TryGetInt32(out int value) ? value : 0,
                    Sha256 = Convert.ToHexString(SHA256.HashData(bytes)),
                };
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string GetString(JsonElement element, string propertyName) =>
            element.TryGetProperty(propertyName, out JsonElement property)
                && property.ValueKind == JsonValueKind.String
                    ? property.GetString() ?? string.Empty
                    : string.Empty;

        private static bool ManifestMetadataMatches(
            PluginRecoveryManifestMetadata? expected,
            PluginRecoveryManifestMetadata? actual)
        {
            if (expected == null || actual == null)
                return expected == null && actual == null;

            return string.Equals(expected.Id, actual.Id, StringComparison.Ordinal)
                && string.Equals(expected.Name, actual.Name, StringComparison.Ordinal)
                && string.Equals(expected.Version, actual.Version, StringComparison.Ordinal)
                && string.Equals(expected.Requires, actual.Requires, StringComparison.Ordinal)
                && string.Equals(expected.Description, actual.Description, StringComparison.Ordinal)
                && string.Equals(expected.DllPath, actual.DllPath, StringComparison.Ordinal)
                && string.Equals(expected.Author, actual.Author, StringComparison.Ordinal)
                && string.Equals(expected.Url, actual.Url, StringComparison.Ordinal)
                && string.Equals(expected.EntryPoint, actual.EntryPoint, StringComparison.Ordinal)
                && string.Equals(expected.Icon, actual.Icon, StringComparison.Ordinal)
                && expected.ManifestVersion == actual.ManifestVersion
                && string.Equals(expected.Sha256, actual.Sha256, StringComparison.OrdinalIgnoreCase);
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

        private static string NormalizeRelativePath(string path) => path.Replace(Path.DirectorySeparatorChar, '/');

        private static bool IsPathUnderDirectory(string directory, string candidatePath)
        {
            string prefix = NormalizeDirectory(directory) + Path.DirectorySeparatorChar;
            return candidatePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool PathsOverlap(string firstDirectory, string secondDirectory)
        {
            string first = NormalizeDirectory(firstDirectory);
            string second = NormalizeDirectory(secondDirectory);
            if (string.Equals(first, second, StringComparison.OrdinalIgnoreCase))
                return true;

            return IsPathUnderDirectory(first, second) || IsPathUnderDirectory(second, first);
        }

        private static bool TryGetExistingDirectoryAttributes(string directory, out FileAttributes attributes)
        {
            try
            {
                attributes = File.GetAttributes(directory);
                if (!attributes.HasFlag(FileAttributes.Directory))
                    throw new IOException($"Expected a plugin directory but found a file: {directory}");
                return true;
            }
            catch (FileNotFoundException)
            {
                attributes = default;
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                attributes = default;
                return false;
            }
        }

        private static void EnsureExistingDirectoryIsNotReparsePoint(
            string directory,
            string description,
            bool mustExist = false)
        {
            if (!TryGetExistingDirectoryAttributes(directory, out FileAttributes attributes))
            {
                if (mustExist)
                    throw new DirectoryNotFoundException($"{description} was not found: {directory}");
                return;
            }

            if (attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new InvalidDataException($"{description} cannot be a reparse point: {directory}");
            }
        }

        private static void EnsureDirectoryChainIsNotReparsePoint(string rootDirectory, string descendantDirectory)
        {
            string normalizedRoot = NormalizeDirectory(rootDirectory);
            string normalizedDescendant = NormalizeDirectory(descendantDirectory);
            if (!string.Equals(normalizedRoot, normalizedDescendant, StringComparison.OrdinalIgnoreCase)
                && !IsPathUnderDirectory(normalizedRoot, normalizedDescendant))
            {
                throw new InvalidDataException("Plugin recovery path escaped its configured backup root.");
            }

            EnsureExistingDirectoryIsNotReparsePoint(normalizedRoot, "Plugin recovery backup root", mustExist: true);
            string relativePath = Path.GetRelativePath(normalizedRoot, normalizedDescendant);
            if (relativePath == ".")
                return;

            string currentDirectory = normalizedRoot;
            foreach (string segment in relativePath.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries))
            {
                currentDirectory = Path.Combine(currentDirectory, segment);
                EnsureExistingDirectoryIsNotReparsePoint(currentDirectory, "Plugin recovery backup path", mustExist: true);
            }
        }

        private static string EscapeForBatchValue(string value) => value.Replace("%", "%%", StringComparison.Ordinal);

        private static void TryDeleteDirectory(string? directory)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
            catch (Exception ex)
            {
                log.Debug($"Failed to delete plugin recovery staging directory '{directory}': {ex.Message}");
            }
        }

        private sealed record PluginLocation(
            string PluginId,
            string PluginDirectory,
            string ProgramDirectory,
            string InstallationKey);

        private sealed class PluginRecoveryBackupMetadata
        {
            public int SchemaVersion { get; set; }

            public string PluginId { get; set; } = string.Empty;

            public string PluginDirectory { get; set; } = string.Empty;

            public string ProgramDirectory { get; set; } = string.Empty;

            public string InstallationKey { get; set; } = string.Empty;

            public DateTimeOffset CreatedUtc { get; set; }

            public string DirectoryHash { get; set; } = string.Empty;

            public int FileCount { get; set; }

            public long TotalBytes { get; set; }

            public PluginRecoveryManifestMetadata? Manifest { get; set; }

            public List<PluginRecoveryFileMetadata> Files { get; set; } = new();
        }

        private sealed class PluginRecoveryFileMetadata
        {
            public string RelativePath { get; set; } = string.Empty;

            public long Length { get; set; }

            public string Sha256 { get; set; } = string.Empty;
        }

        private sealed record DirectoryCatalog(
            IReadOnlyList<PluginRecoveryFileMetadata> Files,
            string DirectoryHash)
        {
            public bool EqualsContent(DirectoryCatalog other)
            {
                if (!string.Equals(DirectoryHash, other.DirectoryHash, StringComparison.OrdinalIgnoreCase)
                    || Files.Count != other.Files.Count)
                    return false;

                for (int index = 0; index < Files.Count; index++)
                {
                    PluginRecoveryFileMetadata left = Files[index];
                    PluginRecoveryFileMetadata right = other.Files[index];
                    if (!string.Equals(left.RelativePath, right.RelativePath, StringComparison.OrdinalIgnoreCase)
                        || left.Length != right.Length
                        || !string.Equals(left.Sha256, right.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
