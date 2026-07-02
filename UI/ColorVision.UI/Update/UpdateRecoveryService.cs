#pragma warning disable CA1822
using ColorVision.UI;
using ColorVision.UI.Properties;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Update
{
    public sealed class UpdateRecoveryService
    {
        private const string ManifestFileName = "backup-manifest.json";
        private const string BackupStateFileName = "update-state.json";
        private const string ApplyingStateFileName = "update-state-applying.json";
        private const string AppliedStateFileName = "update-state-applied.json";
        private const string FailedStateFileName = "update-state-failed.json";

        private static readonly ILog log = LogManager.GetLogger(typeof(UpdateRecoveryService));
        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            Converters = { new StringEnumConverter() }
        };

        public static UpdateRecoveryService Instance { get; } = new();

        public string BackupRoot { get; }
        public string StateDirectory { get; }
        public string StateFilePath { get; }

        private UpdateRecoveryService()
        {
            BackupRoot = Environments.DirUpdateBackup;
            StateDirectory = Environments.DirUpdateState;
            StateFilePath = Path.Combine(StateDirectory, "update-state.json");
        }

        public UpdateBackupPrepareResult PrepareBackup(
            string stageDirectory,
            string targetDirectory,
            Version? versionBefore,
            Version? versionTarget,
            IEnumerable<string> packagePaths,
            IEnumerable<string>? pluginPackagePaths,
            string? snapshotPath = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(stageDirectory) || !Directory.Exists(stageDirectory))
                    throw new DirectoryNotFoundException($"Update staging directory does not exist: {stageDirectory}");

                if (string.IsNullOrWhiteSpace(targetDirectory) || !Directory.Exists(targetDirectory))
                    throw new DirectoryNotFoundException($"Update target directory does not exist: {targetDirectory}");

                Directory.CreateDirectory(BackupRoot);
                Directory.CreateDirectory(StateDirectory);

                string backupPath = CreateBackupDirectory();
                string normalizedStageDirectory = Path.GetFullPath(stageDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                string normalizedTargetDirectory = Path.GetFullPath(targetDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                DateTime now = DateTime.Now;

                UpdateBackupManifest manifest = new()
                {
                    CreatedAt = now,
                    ProgramDirectory = normalizedTargetDirectory,
                    VersionBefore = versionBefore?.ToString() ?? string.Empty,
                    VersionTarget = versionTarget?.ToString() ?? string.Empty,
                };

                HashSet<string> directoryBackedRelatives = BackupPluginDirectories(normalizedStageDirectory, normalizedTargetDirectory, backupPath, manifest);
                BackupStageFiles(normalizedStageDirectory, normalizedTargetDirectory, backupPath, manifest, directoryBackedRelatives);

                string manifestPath = Path.Combine(backupPath, ManifestFileName);
                WriteJsonFile(manifestPath, manifest);

                UpdateStateInfo preparedState = new()
                {
                    State = UpdateApplyState.Prepared,
                    BackupPath = backupPath,
                    SnapshotPath = NormalizePathOrEmpty(snapshotPath),
                    StagePath = normalizedStageDirectory,
                    TargetPath = normalizedTargetDirectory,
                    ExecutablePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty,
                    VersionBefore = versionBefore?.ToString() ?? string.Empty,
                    VersionTarget = versionTarget?.ToString() ?? string.Empty,
                    StartedAt = now,
                    PackagePaths = NormalizePathList(packagePaths),
                    PluginPackagePaths = NormalizePathList(pluginPackagePaths),
                };

                WriteState(preparedState);

                string applyingStatePath = Path.Combine(backupPath, ApplyingStateFileName);
                string appliedStatePath = Path.Combine(backupPath, AppliedStateFileName);
                string failedStatePath = Path.Combine(backupPath, FailedStateFileName);

                WriteStateSnapshot(applyingStatePath, CloneState(preparedState, UpdateApplyState.Applying));
                WriteStateSnapshot(appliedStatePath, CloneState(preparedState, UpdateApplyState.Applied));
                WriteStateSnapshot(failedStatePath, CloneState(preparedState, UpdateApplyState.Failed, "Update batch copy failed."));

                log.Info($"Prepared update backup at {backupPath}. Files={manifest.Files.Count}, Directories={manifest.Directories.Count}");

                return new UpdateBackupPrepareResult
                {
                    BackupPath = backupPath,
                    SnapshotPath = preparedState.SnapshotPath,
                    ManifestPath = manifestPath,
                    StateFilePath = StateFilePath,
                    ApplyingStatePath = applyingStatePath,
                    AppliedStatePath = appliedStatePath,
                    FailedStatePath = failedStatePath,
                    State = preparedState,
                };
            }
            catch (Exception ex)
            {
                log.Error("Failed to prepare update backup.", ex);
                throw;
            }
        }

        public void WriteState(UpdateStateInfo state)
        {
            try
            {
                WriteStateSnapshot(StateFilePath, state);
                WriteBackupStateCopy(state);
            }
            catch (Exception ex)
            {
                log.Error($"Failed to write update state: {StateFilePath}", ex);
                throw;
            }
        }

        public UpdateStateInfo? ReadState()
        {
            try
            {
                if (!File.Exists(StateFilePath))
                    return null;

                return JsonConvert.DeserializeObject<UpdateStateInfo>(File.ReadAllText(StateFilePath), JsonSettings);
            }
            catch (Exception ex)
            {
                log.Warn($"Failed to read update state: {StateFilePath}", ex);
                return null;
            }
        }

        public Task ResumeOrRollbackIfNeededAsync(Window? owner = null)
        {
            UpdateStateInfo? state = ReadState();
            if (state == null)
                return Task.CompletedTask;

            try
            {
                switch (state.State)
                {
                    case UpdateApplyState.Applied:
                        MarkCompleted();
                        break;
                    case UpdateApplyState.Failed when state.CompletedAt.HasValue:
                        CleanupOldBackups();
                        break;
                    case UpdateApplyState.Prepared:
                    case UpdateApplyState.Applying:
                    case UpdateApplyState.Failed:
                        ResumeRollback(state, owner);
                        break;
                    case UpdateApplyState.Completed:
                    case UpdateApplyState.RolledBack:
                        CleanupOldBackups();
                        break;
                }
            }
            catch (Exception ex)
            {
                log.Error("Failed while resuming update recovery state.", ex);
            }

            return Task.CompletedTask;
        }

        public bool RollbackLastUpdate(UpdateStateInfo state)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(state.BackupPath) || !Directory.Exists(state.BackupPath))
                    throw new DirectoryNotFoundException($"Update backup directory does not exist: {state.BackupPath}");

                UpdateBackupManifest manifest = ReadBackupManifest(state.BackupPath);
                string targetDirectory = !string.IsNullOrWhiteSpace(state.TargetPath)
                    ? state.TargetPath
                    : manifest.ProgramDirectory;

                if (string.IsNullOrWhiteSpace(targetDirectory))
                    throw new InvalidOperationException("Update rollback target directory is empty.");

                RestoreDirectories(manifest, targetDirectory);
                RestoreFiles(manifest, targetDirectory);

                state.State = UpdateApplyState.RolledBack;
                state.CompletedAt = DateTime.Now;
                state.ErrorMessage = string.Empty;
                WriteState(state);

                log.Warn($"Rolled back update from backup: {state.BackupPath}");
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"Failed to roll back update from backup: {state.BackupPath}", ex);
                TryMarkFailed(state, ex.Message);
                return false;
            }
        }

        public void MarkApplied()
        {
            UpdateExistingState(UpdateApplyState.Applied, null, false);
        }

        public void MarkCompleted()
        {
            UpdateExistingState(UpdateApplyState.Completed, null, true);
            CleanupOldBackups();
        }

        public void MarkFailed(string errorMessage)
        {
            UpdateExistingState(UpdateApplyState.Failed, errorMessage, true);
        }

        public void CleanupOldBackups(int keepCount = 3)
        {
            try
            {
                if (!Directory.Exists(BackupRoot))
                    return;

                string? currentBackupPath = NormalizeFullPath(ReadState()?.BackupPath);
                List<DirectoryInfo> backupDirectories = new DirectoryInfo(BackupRoot)
                    .GetDirectories()
                    .OrderByDescending(directory => directory.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (DirectoryInfo directory in backupDirectories.Skip(Math.Max(keepCount, 0)))
                {
                    string fullPath = NormalizeFullPath(directory.FullName) ?? directory.FullName;
                    if (!string.IsNullOrEmpty(currentBackupPath) && string.Equals(fullPath, currentBackupPath, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!CanCleanupBackup(directory.FullName))
                        continue;

                    TryDeleteDirectory(directory.FullName);
                }
            }
            catch (Exception ex)
            {
                log.Warn("Failed to cleanup old update backups.", ex);
            }
        }

        private void ResumeRollback(UpdateStateInfo state, Window? owner)
        {
            bool rolledBack = RollbackLastUpdate(state);
            if (rolledBack)
            {
                MessageBox.Show(owner, Resources.UpdateRecovery_Restored, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
                CleanupOldBackups();
                return;
            }

            CleanupOldBackups();
        }

        private string CreateBackupDirectory()
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            for (int index = 0; index < 100; index++)
            {
                string directoryName = index == 0 ? timestamp : $"{timestamp}_{index:00}";
                string backupPath = Path.Combine(BackupRoot, directoryName);
                if (Directory.Exists(backupPath))
                    continue;

                Directory.CreateDirectory(backupPath);
                Directory.CreateDirectory(Path.Combine(backupPath, "App"));
                Directory.CreateDirectory(Path.Combine(backupPath, "Plugins"));
                return backupPath;
            }

            throw new IOException("Unable to allocate a unique update backup directory.");
        }

        private static HashSet<string> BackupPluginDirectories(string stageDirectory, string targetDirectory, string backupPath, UpdateBackupManifest manifest)
        {
            HashSet<string> directoryBackedRelatives = new(StringComparer.OrdinalIgnoreCase);
            string stagePluginsDirectory = Path.Combine(stageDirectory, "Plugins");
            if (!Directory.Exists(stagePluginsDirectory))
                return directoryBackedRelatives;

            foreach (string stagePluginDirectory in Directory.GetDirectories(stagePluginsDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                string pluginDirectoryName = Path.GetFileName(stagePluginDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrWhiteSpace(pluginDirectoryName))
                    continue;

                string relativePath = Path.Combine("Plugins", pluginDirectoryName);
                string targetPluginDirectory = Path.Combine(targetDirectory, relativePath);
                string backupPluginDirectory = Path.Combine(backupPath, "Plugins", pluginDirectoryName);
                bool existsBeforeUpdate = Directory.Exists(targetPluginDirectory);

                if (existsBeforeUpdate)
                {
                    CopyDirectory(targetPluginDirectory, backupPluginDirectory, true);
                }

                manifest.Directories.Add(new UpdateBackupDirectoryEntry
                {
                    RelativePath = relativePath,
                    OriginalPath = targetPluginDirectory,
                    BackupPath = backupPluginDirectory,
                    ExistsBeforeUpdate = existsBeforeUpdate,
                });

                directoryBackedRelatives.Add(NormalizeRelativeKey(relativePath));
            }

            return directoryBackedRelatives;
        }

        private static void BackupStageFiles(string stageDirectory, string targetDirectory, string backupPath, UpdateBackupManifest manifest, HashSet<string> directoryBackedRelatives)
        {
            string backupAppDirectory = Path.Combine(backupPath, "App");
            foreach (string stageFile in Directory.GetFiles(stageDirectory, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(stageDirectory, stageFile);
                if (directoryBackedRelatives.Any(directory => IsSameOrUnder(relativePath, directory)))
                    continue;

                string targetFile = Path.Combine(targetDirectory, relativePath);
                string backupFile = Path.Combine(backupAppDirectory, relativePath);
                bool existsBeforeUpdate = File.Exists(targetFile);
                long size = 0;
                string sha256 = string.Empty;

                if (existsBeforeUpdate)
                {
                    FileInfo fileInfo = new(targetFile);
                    size = fileInfo.Length;
                    sha256 = ComputeSha256(targetFile);
                    Directory.CreateDirectory(Path.GetDirectoryName(backupFile)!);
                    File.Copy(targetFile, backupFile, true);
                }

                manifest.Files.Add(new UpdateBackupFileEntry
                {
                    RelativePath = relativePath,
                    OriginalPath = targetFile,
                    BackupPath = backupFile,
                    ExistsBeforeUpdate = existsBeforeUpdate,
                    Size = size,
                    Sha256 = sha256,
                });
            }
        }

        private static UpdateStateInfo CloneState(UpdateStateInfo state, UpdateApplyState applyState, string errorMessage = "")
        {
            return new UpdateStateInfo
            {
                State = applyState,
                BackupPath = state.BackupPath,
                SnapshotPath = state.SnapshotPath,
                StagePath = state.StagePath,
                TargetPath = state.TargetPath,
                ExecutablePath = state.ExecutablePath,
                VersionBefore = state.VersionBefore,
                VersionTarget = state.VersionTarget,
                StartedAt = state.StartedAt,
                CompletedAt = null,
                ErrorMessage = errorMessage,
                PackagePaths = state.PackagePaths.ToList(),
                PluginPackagePaths = state.PluginPackagePaths.ToList(),
            };
        }

        private static List<string> NormalizePathList(IEnumerable<string>? paths)
        {
            return paths?
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path =>
                {
                    try
                    {
                        return Path.GetFullPath(path);
                    }
                    catch
                    {
                        return path;
                    }
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();
        }

        private static string NormalizePathOrEmpty(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            try
            {
                return Path.GetFullPath(path);
            }
            catch
            {
                return path;
            }
        }

        private void UpdateExistingState(UpdateApplyState nextState, string? errorMessage, bool completed)
        {
            try
            {
                UpdateStateInfo state = ReadState() ?? new UpdateStateInfo { StartedAt = DateTime.Now };
                state.State = nextState;
                state.CompletedAt = completed ? DateTime.Now : null;
                if (errorMessage != null)
                    state.ErrorMessage = errorMessage;
                else if (nextState != UpdateApplyState.Failed)
                    state.ErrorMessage = string.Empty;

                WriteState(state);
            }
            catch (Exception ex)
            {
                log.Error($"Failed to mark update state as {nextState}.", ex);
            }
        }

        private void TryMarkFailed(UpdateStateInfo state, string errorMessage)
        {
            try
            {
                state.State = UpdateApplyState.Failed;
                state.ErrorMessage = errorMessage;
                state.CompletedAt = DateTime.Now;
                WriteState(state);
            }
            catch (Exception ex)
            {
                log.Error("Failed to mark update rollback failure state.", ex);
            }
        }

        private UpdateBackupManifest ReadBackupManifest(string backupPath)
        {
            string manifestPath = Path.Combine(backupPath, ManifestFileName);
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException("Update backup manifest was not found.", manifestPath);

            UpdateBackupManifest? manifest = JsonConvert.DeserializeObject<UpdateBackupManifest>(File.ReadAllText(manifestPath), JsonSettings);
            return manifest ?? throw new InvalidOperationException("Update backup manifest is empty or invalid.");
        }

        private static void RestoreDirectories(UpdateBackupManifest manifest, string targetDirectory)
        {
            foreach (UpdateBackupDirectoryEntry entry in manifest.Directories)
            {
                string targetPath = GetTargetPath(targetDirectory, entry.RelativePath, entry.OriginalPath);
                if (entry.ExistsBeforeUpdate)
                {
                    if (!Directory.Exists(entry.BackupPath))
                        throw new DirectoryNotFoundException($"Backup plugin directory does not exist: {entry.BackupPath}");

                    TryDeleteDirectory(targetPath);
                    CopyDirectory(entry.BackupPath, targetPath, true);
                }
                else
                {
                    TryDeleteDirectory(targetPath);
                }
            }
        }

        private static void RestoreFiles(UpdateBackupManifest manifest, string targetDirectory)
        {
            foreach (UpdateBackupFileEntry entry in manifest.Files)
            {
                string targetPath = GetTargetPath(targetDirectory, entry.RelativePath, entry.OriginalPath);
                if (entry.ExistsBeforeUpdate)
                {
                    if (!File.Exists(entry.BackupPath))
                        throw new FileNotFoundException("Backup file does not exist.", entry.BackupPath);

                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                    ClearReadOnly(targetPath);
                    File.Copy(entry.BackupPath, targetPath, true);
                }
                else
                {
                    TryDeleteFile(targetPath);
                }
            }
        }

        private static string GetTargetPath(string targetDirectory, string relativePath, string originalPath)
        {
            if (!string.IsNullOrWhiteSpace(relativePath))
                return Path.Combine(targetDirectory, relativePath);

            if (!string.IsNullOrWhiteSpace(originalPath))
                return originalPath;

            throw new InvalidOperationException("Backup entry does not contain a target path.");
        }

        private static void CopyDirectory(string sourceDirectory, string destinationDirectory, bool overwrite)
        {
            Directory.CreateDirectory(destinationDirectory);

            foreach (string sourceFile in Directory.GetFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                string destinationFile = Path.Combine(destinationDirectory, Path.GetFileName(sourceFile));
                Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
                ClearReadOnly(destinationFile);
                File.Copy(sourceFile, destinationFile, overwrite);
            }

            foreach (string sourceSubDirectory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                string destinationSubDirectory = Path.Combine(destinationDirectory, Path.GetFileName(sourceSubDirectory));
                CopyDirectory(sourceSubDirectory, destinationSubDirectory, overwrite);
            }
        }

        private static bool IsSameOrUnder(string relativePath, string directoryRelativeKey)
        {
            string key = NormalizeRelativeKey(relativePath);
            return string.Equals(key, directoryRelativeKey, StringComparison.OrdinalIgnoreCase)
                || key.StartsWith(directoryRelativeKey + "/", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeRelativeKey(string path)
        {
            return path.Replace('\\', '/').Trim('/');
        }

        private static string ComputeSha256(string filePath)
        {
            using SHA256 sha256 = SHA256.Create();
            using FileStream stream = File.OpenRead(filePath);
            byte[] hash = sha256.ComputeHash(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private void WriteBackupStateCopy(UpdateStateInfo state)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(state.BackupPath) || !Directory.Exists(state.BackupPath))
                    return;

                WriteStateSnapshot(Path.Combine(state.BackupPath, BackupStateFileName), state);
            }
            catch (Exception ex)
            {
                log.Warn($"Failed to write update backup state copy: {state.BackupPath}", ex);
            }
        }

        private static void WriteStateSnapshot(string path, UpdateStateInfo state)
        {
            WriteJsonFile(path, state);
        }

        private static void WriteJsonFile(string path, object value)
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            string tempPath = path + ".tmp";
            string json = JsonConvert.SerializeObject(value, Formatting.Indented, JsonSettings);
            File.WriteAllText(tempPath, json, new UTF8Encoding(false));
            File.Copy(tempPath, path, true);
            File.Delete(tempPath);
        }

        private static bool CanCleanupBackup(string backupPath)
        {
            try
            {
                string statePath = Path.Combine(backupPath, BackupStateFileName);
                if (!File.Exists(statePath))
                    return false;

                UpdateStateInfo? state = JsonConvert.DeserializeObject<UpdateStateInfo>(File.ReadAllText(statePath), JsonSettings);
                return state?.State == UpdateApplyState.Completed || state?.State == UpdateApplyState.RolledBack;
            }
            catch (Exception ex)
            {
                log.Warn($"Failed to inspect update backup state for cleanup: {backupPath}", ex);
                return false;
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return;

                ClearReadOnly(path);
                File.Delete(path);
            }
            catch (Exception ex)
            {
                log.Warn($"Failed to delete update-created file during rollback: {path}", ex);
                throw;
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                    return;

                foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    ClearReadOnly(file);
                }

                Directory.Delete(path, true);
            }
            catch (Exception ex)
            {
                log.Warn($"Failed to delete directory: {path}", ex);
                throw;
            }
        }

        private static void ClearReadOnly(string path)
        {
            if (!File.Exists(path))
                return;

            FileAttributes attributes = File.GetAttributes(path);
            if (attributes.HasFlag(FileAttributes.ReadOnly))
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        }

        private static string? NormalizeFullPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            try
            {
                return Path.GetFullPath(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }
            catch
            {
                return path;
            }
        }
    }
}
