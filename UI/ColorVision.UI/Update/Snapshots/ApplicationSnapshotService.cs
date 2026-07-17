#pragma warning disable CA1822
using ColorVision.Common.Utilities;
using ColorVision.UI;
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

        public string SnapshotTypeText => IsDefault ? "默认快照" : IsUpdate ? "更新快照" : "用户快照";

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
        private static readonly ILog log = LogManager.GetLogger(typeof(ApplicationSnapshotService));
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public static ApplicationSnapshotService Instance { get; } = new();

        public string SnapshotDirectory => Environments.DirApplicationSnapshots;

        public string DefaultSnapshotPath => Path.Combine(SnapshotDirectory, DefaultSnapshotFileName);

        private ApplicationSnapshotService()
        {
        }

        public Task<ApplicationSnapshotInfo> EnsureDefaultSnapshotAsync(CancellationToken cancellationToken = default)
        {
            if (File.Exists(DefaultSnapshotPath))
            {
                if (TryReadSnapshotInfo(DefaultSnapshotPath, out ApplicationSnapshotInfo? snapshotInfo) && snapshotInfo != null)
                    return Task.FromResult(snapshotInfo);

                MoveUnreadableSnapshotToRecovery(DefaultSnapshotPath);
            }

            return CreateDefaultSnapshotAsync(force: true, cancellationToken);
        }

        public Task<ApplicationSnapshotInfo> CreateDefaultSnapshotAsync(bool force, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => CreateSnapshotCore(DefaultSnapshotPath, SnapshotKind.Default, versionTarget: string.Empty, overwrite: force, cancellationToken), cancellationToken);
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

        public Task<ApplicationSnapshotInfo> CreateUpdateSnapshotAsync(Version? versionBefore, Version? versionTarget, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => CreateUpdateSnapshot(versionBefore, versionTarget, cancellationToken), cancellationToken);
        }

        public ApplicationSnapshotInfo CreateUpdateSnapshot(Version? versionBefore, Version? versionTarget, CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(SnapshotDirectory);
            string fromVersion = versionBefore?.ToString() ?? GetCurrentVersionText();
            string toVersion = versionTarget?.ToString() ?? "unknown";
            string fileName = $"ColorVision-update-{SanitizeFilePart(fromVersion)}-to-{SanitizeFilePart(toVersion)}-{DateTime.Now:yyyyMMdd-HHmmss}.zip";
            string snapshotPath = Path.Combine(SnapshotDirectory, fileName);
            return CreateSnapshotCore(snapshotPath, SnapshotKind.Update, versionTarget: toVersion, overwrite: false, cancellationToken);
        }

        public ApplicationSnapshotInfo GetSnapshotInfo(string snapshotPath)
        {
            if (string.IsNullOrWhiteSpace(snapshotPath) || !File.Exists(snapshotPath))
                throw new FileNotFoundException("Snapshot file does not exist.", snapshotPath);

            EnsureSnapshotArchiveReadable(snapshotPath);
            return ReadSnapshotInfo(snapshotPath);
        }

        public IReadOnlyList<ApplicationSnapshotInfo> ListSnapshots()
        {
            Directory.CreateDirectory(SnapshotDirectory);
            return Directory.EnumerateFiles(SnapshotDirectory, "*.zip", SearchOption.TopDirectoryOnly)
                .Select(TryReadSnapshotInfoOrIgnore)
                .OfType<ApplicationSnapshotInfo>()
                .OrderByDescending(item => item.IsDefault)
                .ThenByDescending(item => item.CreatedAt)
                .ToList();
        }

        public async Task DeleteSnapshotAsync(ApplicationSnapshotInfo snapshot, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            await Task.Run(() =>
            {
                if (File.Exists(snapshot.FilePath))
                    File.Delete(snapshot.FilePath);
            }, cancellationToken).ConfigureAwait(false);

            if (snapshot.IsDefault)
                await CreateDefaultSnapshotAsync(force: true, cancellationToken).ConfigureAwait(false);
        }

        public Task RestoreSnapshotAsync(ApplicationSnapshotInfo snapshot, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            return Task.Run(() => RestoreSnapshotCore(snapshot, cancellationToken), cancellationToken);
        }

        private static ApplicationSnapshotInfo CreateSnapshotCore(string snapshotPath, SnapshotKind kind, string versionTarget, bool overwrite, CancellationToken cancellationToken)
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
                        AddFileEntry(archive, programDirectory, filePath);
                    }

                    ZipArchiveEntry manifestEntry = archive.CreateEntry(ManifestFileName, CompressionLevel.Optimal);
                    using Stream manifestStream = manifestEntry.Open();
                    JsonSerializer.Serialize(manifestStream, manifest, JsonOptions);
                }

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

            ZipArchiveEntry entry = archive.CreateEntry(relativePath, CompressionLevel.Optimal);
            using Stream entryStream = entry.Open();
            using FileStream sourceStream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            sourceStream.CopyTo(entryStream);
        }

        private static ApplicationSnapshotInfo ReadSnapshotInfo(string snapshotPath)
        {
            FileInfo fileInfo = new(snapshotPath);
            ApplicationSnapshotManifest? manifest = TryReadManifest(snapshotPath);
            bool isDefault = string.Equals(fileInfo.Name, DefaultSnapshotFileName, StringComparison.OrdinalIgnoreCase)
                || manifest?.IsDefault == true;
            bool isUpdate = string.Equals(manifest?.SnapshotKind, SnapshotKind.Update.ToString(), StringComparison.OrdinalIgnoreCase)
                || fileInfo.Name.StartsWith("ColorVision-update-", StringComparison.OrdinalIgnoreCase);

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
                EnsureSnapshotArchiveReadable(snapshotPath);
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

        private static void EnsureSnapshotArchiveReadable(string snapshotPath)
        {
            using ZipArchive archive = ZipFile.OpenRead(snapshotPath);
            _ = archive.Entries.Count;
        }

        internal static string MoveUnreadableSnapshotToRecovery(string snapshotPath)
        {
            return MoveSnapshotToRecovery(snapshotPath, "unreadable");
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

        private static ApplicationSnapshotManifest? TryReadManifest(string snapshotPath)
        {
            try
            {
                using ZipArchive archive = ZipFile.OpenRead(snapshotPath);
                ZipArchiveEntry? entry = archive.GetEntry(ManifestFileName);
                if (entry == null)
                    return null;

                using Stream stream = entry.Open();
                return JsonSerializer.Deserialize<ApplicationSnapshotManifest>(stream);
            }
            catch (Exception ex)
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
            string batchPath = Path.Combine(stageDirectory, "restore-snapshot.bat");
            File.WriteAllText(batchPath, CreateRestoreBatch(stageDirectory, programDirectory, executableName), Encoding.UTF8);

            ProcessStartInfo startInfo = new()
            {
                FileName = batchPath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };

            if (!Tool.HasWritePermission(programDirectory))
            {
                startInfo.Verb = "runas";
                startInfo.WindowStyle = ProcessWindowStyle.Normal;
            }

            Process.Start(startInfo);
            Environment.Exit(0);
        }

        private static string CreateRestoreBatch(string stageDirectory, string programDirectory, string executableName)
        {
            string executablePath = Path.Combine(programDirectory, executableName);
            StringBuilder sb = new();
            sb.AppendLine("@echo off");
            sb.AppendLine("setlocal enabledelayedexpansion");
            sb.AppendLine("title ColorVision Snapshot Restore");
            sb.AppendLine($"set \"STAGE={EscapeForBatchValue(stageDirectory)}\"");
            sb.AppendLine($"set \"TARGET={EscapeForBatchValue(programDirectory)}\"");
            sb.AppendLine($"set \"EXE={EscapeForBatchValue(executableName)}\"");
            sb.AppendLine($"set \"EXEPATH={EscapeForBatchValue(executablePath)}\"");
            sb.AppendLine("set \"NEED_RESTART_EXPLORER=0\"");
            sb.AppendLine("taskkill /f /im \"%EXE%\" >nul 2>nul");
            sb.AppendLine("timeout /t 2 /nobreak >nul");
            sb.AppendLine("dir /b /s \"%STAGE%\\ColorVision.ShellExtension*\" >nul 2>nul");
            sb.AppendLine("if !ERRORLEVEL! EQU 0 (");
            sb.AppendLine("  taskkill /f /im explorer.exe >nul 2>nul");
            sb.AppendLine("  taskkill /f /im dllhost.exe >nul 2>nul");
            sb.AppendLine("  set \"NEED_RESTART_EXPLORER=1\"");
            sb.AppendLine("  timeout /t 2 /nobreak >nul");
            sb.AppendLine(")");
            sb.AppendLine("where robocopy >nul 2>nul");
            sb.AppendLine("if !ERRORLEVEL! EQU 0 (");
            sb.AppendLine("  robocopy \"%STAGE%\" \"%TARGET%\" *.* /E /XF restore-snapshot.bat snapshot-manifest.json /NFL /NDL /NP /NJH /NJS /R:2 /W:1");
            sb.AppendLine("  set \"RC=!ERRORLEVEL!\"");
            sb.AppendLine("  if !RC! GEQ 8 goto fail");
            sb.AppendLine(") else (");
            sb.AppendLine("  xcopy /y /e /i \"%STAGE%\\*\" \"%TARGET%\\\" >nul");
            sb.AppendLine("  if !ERRORLEVEL! NEQ 0 goto fail");
            sb.AppendLine(")");
            sb.AppendLine("if \"%NEED_RESTART_EXPLORER%\"==\"1\" start \"\" explorer.exe");
            sb.AppendLine("start \"\" \"%EXEPATH%\"");
            sb.AppendLine("start \"\" cmd /c \"ping -n 4 127.0.0.1 >nul & rd /s /q \\\"%STAGE%\\\" 2>nul\"");
            sb.AppendLine("exit /b 0");
            sb.AppendLine(":fail");
            sb.AppendLine("if \"%NEED_RESTART_EXPLORER%\"==\"1\" start \"\" explorer.exe");
            sb.AppendLine("start \"\" \"%EXEPATH%\"");
            sb.AppendLine("exit /b 1");
            return sb.ToString();
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

        private enum SnapshotKind
        {
            Default,
            User,
            Update
        }
    }
}
