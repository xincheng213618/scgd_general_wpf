using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace ColorVision.ToolPlugins.ThirdPartyApps;

internal static class SscomLauncher
{
    internal const string FileName = "sscom5.13.1.exe";
    private static readonly object LaunchGate = new();

    internal static void Launch(string bundledPath, string backupDirectory, string runtimeDirectory, Action<ProcessStartInfo> start,
        IReadOnlyList<SscomCommandPreset>? presets = null, SscomTcpEndpoint? endpoint = null)
    {
        lock (LaunchGate)
        {
            string backupPath = Path.Combine(backupDirectory, FileName);
            using FileStream backup = GetVerifiedBackup(bundledPath, backupPath, out string expectedHash);
            using FileStream runtime = GetVerifiedCopy(backup, expectedHash, Path.Combine(runtimeDirectory, FileName));
            string iniPath = Path.Combine(runtimeDirectory, "sscom51.ini");
            if (presets != null || !File.Exists(iniPath))
            {
                EnsureRuntimeClosed(runtime.Name);
                if (presets == null) SscomPresetIni.InitializeDefaults(iniPath);
                else SscomPresetIni.AddPresets(iniPath, presets, endpoint);
            }
            start(new ProcessStartInfo(runtime.Name) { UseShellExecute = true, WorkingDirectory = runtimeDirectory });
        }
    }

    private static void EnsureRuntimeClosed(string runtimePath)
    {
        foreach (Process process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(FileName)))
        {
            using (process)
            {
                string? executablePath;
                try { executablePath = process.MainModule?.FileName; }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    if (process.HasExited) continue;
                    throw new IOException("无法确认 SSCOM 是否已关闭。请先关闭 SSCOM，再设置预设。", ex);
                }
                if (string.Equals(executablePath, runtimePath, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("SSCOM 正在运行。请先关闭窗口，再设置预设。");
            }
        }
    }

    private static FileStream GetVerifiedBackup(string bundledPath, string backupPath, out string expectedHash)
    {
        string checksumPath = backupPath + ".sha256";
        using FileStream? bundled = OpenIfPresent(bundledPath);
        if (bundled == null)
        {
            expectedHash = File.Exists(checksumPath) ? File.ReadAllText(checksumPath).Trim() : string.Empty;
            return OpenIfMatching(backupPath, expectedHash)
                ?? throw new InvalidDataException("安装目录缺少 SSCOM，且备份无法通过校验。请修复 ColorVision 安装后重试。");
        }
        if (bundled.Length == 0) throw new InvalidDataException("安装目录中的 SSCOM 是空文件。请修复 ColorVision 安装后重试。");

        expectedHash = ComputeHash(bundled);
        FileStream backup = GetVerifiedCopy(bundled, expectedHash, backupPath);
        try
        {
            if (!File.Exists(checksumPath) || !string.Equals(File.ReadAllText(checksumPath).Trim(), expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                string temporary = checksumPath + $".{Guid.NewGuid():N}.tmp";
                try
                {
                    File.WriteAllText(temporary, expectedHash);
                    File.Move(temporary, checksumPath, overwrite: true);
                }
                finally { File.Delete(temporary); }
            }
            return backup;
        }
        catch
        {
            backup.Dispose();
            throw;
        }
    }

    private static FileStream GetVerifiedCopy(FileStream source, string expectedHash, string destination)
    {
        FileStream? existing = OpenIfMatching(destination, expectedHash);
        if (existing != null) return existing;

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string temporary = destination + $".{Guid.NewGuid():N}.tmp";
        try
        {
            source.Position = 0;
            using (FileStream output = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                source.CopyTo(output);
            try
            {
                File.Move(temporary, destination, overwrite: true);
            }
            catch (IOException ex)
            {
                return OpenIfMatching(destination, expectedHash)
                    ?? throw new IOException($"无法更新 SSCOM 文件 {destination}。请关闭正在运行的 SSCOM，并确认目录可写后重试。", ex);
            }
            return OpenIfMatching(destination, expectedHash)
                ?? throw new InvalidDataException($"SSCOM 文件校验失败：{destination}");
        }
        finally { File.Delete(temporary); }
    }

    private static FileStream? OpenIfMatching(string path, string expectedHash)
    {
        if (expectedHash.Length != 64) return null;
        FileStream? file = OpenIfPresent(path);
        if (file == null) return null;

        bool matching = false;
        try
        {
            matching = file.Length != 0 && string.Equals(ComputeHash(file), expectedHash, StringComparison.OrdinalIgnoreCase);
            return matching ? file : null;
        }
        finally
        {
            if (!matching) file.Dispose();
        }
    }

    private static FileStream? OpenIfPresent(string path)
    {
        try { return File.OpenRead(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    private static string ComputeHash(FileStream file) => Convert.ToHexString(SHA256.HashData(file));
}
