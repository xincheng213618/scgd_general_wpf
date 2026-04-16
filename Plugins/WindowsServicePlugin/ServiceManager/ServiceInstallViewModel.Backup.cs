using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace WindowsServicePlugin.ServiceManager
{
    /// <summary>
    /// 备份与恢复：数据库备份/恢复、服务文件夹备份/恢复、WinRAR 集成
    /// </summary>
    public partial class ServiceInstallViewModel
    {
        private void DoBackupNow()
        {
            try
            {
                var mySqlManager = ServiceManagerViewModel.Instance.MySqlManager;
                mySqlManager.RefreshStatus(ServiceManagerViewModel.Instance.Services, ServiceManagerViewModel.Instance.Config.MySqlPort);
                if (!mySqlManager.Config.IsRunning)
                {
                    AddLog("MySQL 未运行，跳过备份");
                    return;
                }

                mySqlManager.BackupDatabase(AddLog);
            }
            catch (Exception ex)
            {
                AddLog($"备份失败: {ex.Message}");
            }
        }

        private void DoRestoreBackup()
        {
            string? filePath = null;
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "SQL 备份文件 (*.sql)|*.sql",
                    Title = "选择备份文件",
                    InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ColorVision", "Backup")
                };
                if (dlg.ShowDialog() == true)
                    filePath = dlg.FileName;
            });

            if (string.IsNullOrEmpty(filePath)) return;

            try
            {
                var mySqlManager = ServiceManagerViewModel.Instance.MySqlManager;
                mySqlManager.RefreshStatus(ServiceManagerViewModel.Instance.Services, ServiceManagerViewModel.Instance.Config.MySqlPort);
                if (!mySqlManager.Config.IsRunning)
                {
                    AddLog("MySQL 未运行，无法恢复");
                    return;
                }

                mySqlManager.RestoreDatabase(filePath, AddLog);
            }
            catch (Exception ex)
            {
                AddLog($"恢复失败: {ex.Message}");
            }
        }

        private void DoBackupServiceNow()
        {
            bool stopped = false;
            try
            {
                StopPackagedServices();
                stopped = true;
                DoBackupServiceArchiveOnly();
            }
            finally
            {
                if (stopped)
                {
                    StartPackagedServices();
                }
            }
        }

        private void DoBackupServiceArchiveOnly()
        {
            try
            {
                string basePath = Config.BaseLocation;
                if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
                {
                    AddLog("未设置安装根目录或目录不存在，跳过服务备份");
                    return;
                }

                string sourcePath = ResolveBestServiceRootForBackup(basePath);
                if (!Directory.Exists(sourcePath))
                {
                    AddLog($"服务备份源目录不存在: {sourcePath}");
                    return;
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd'T'HHmmss");
                string backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ColorVision", "ServiceBackup");
                Directory.CreateDirectory(backupDir);
                string versionTag = GetServiceVersionTag(sourcePath);
                string backupFile = BackupServiceCfgOnly
                    ? Path.Combine(backupDir, $"CVWindowsService_cfg_{versionTag}_{timestamp}.zip")
                    : Path.Combine(backupDir, $"CVWindowsService_{versionTag}_{timestamp}.rar");

                AddLog($"备份服务文件夹: {sourcePath} → {backupFile}");
                if (File.Exists(backupFile)) File.Delete(backupFile);

                if (BackupServiceCfgOnly)
                {
                    int cfgCount = CreateCfgOnlyBackupZip(sourcePath, backupFile);
                    AddLog($"CFG 备份完成，文件数: {cfgCount}");
                }
                else
                {
                    bool winRarOk = TryCreateServiceBackupWithWinRar(sourcePath, backupFile);
                    if (!winRarOk)
                    {
                        backupFile = Path.ChangeExtension(backupFile, ".zip");
                        if (File.Exists(backupFile)) File.Delete(backupFile);
                        int packedCount = CreateServiceBackupZip(sourcePath, backupFile);
                        AddLog($"WinRAR 不可用或执行失败，已回退 ZIP 全量压缩，文件数: {packedCount}");
                    }
                }

                AddLog($"服务备份完成: {backupFile}");
            }
            catch (Exception ex)
            {
                AddLog($"服务备份失败: {ex.Message}");
            }
        }

        private void DoRestoreServiceBackup()
        {
            string? filePath = null;
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "服务备份文件 (*.zip;*.rar)|*.zip;*.rar",
                    Title = "选择服务备份文件",
                    InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ColorVision", "ServiceBackup")
                };
                if (dlg.ShowDialog() == true)
                    filePath = dlg.FileName;
            });

            if (string.IsNullOrEmpty(filePath)) return;

            bool stopped = false;
            try
            {
                string basePath = Config.BaseLocation;
                if (string.IsNullOrEmpty(basePath))
                {
                    AddLog("未设置安装根目录，无法恢复");
                    return;
                }

                StopPackagedServices();
                stopped = true;

                AddLog($"恢复服务文件夹: {filePath} → {basePath}");

                // 清空目标并恢复
                if (Directory.Exists(basePath))
                    Directory.Delete(basePath, true);

                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                if (ext == ".rar")
                {
                    if (!ExtractRarToDirectory(filePath, basePath))
                    {
                        AddLog("RAR 恢复失败：未检测到可用 WinRAR/RAR 命令行");
                        return;
                    }
                }
                else
                {
                    ZipFile.ExtractToDirectory(filePath, basePath, true);
                }
                AddLog($"服务恢复完成");
            }
            catch (Exception ex)
            {
                AddLog($"服务恢复失败: {ex.Message}");
            }
            finally
            {
                if (stopped)
                {
                    StartPackagedServices();
                }
            }
        }

        private string ResolveBestServiceRootForBackup(string configuredBasePath)
        {
            var candidates = new List<string> { configuredBasePath };
            string nested = Path.Combine(configuredBasePath, "CVWindowsService");
            if (Directory.Exists(nested))
                candidates.Add(nested);

            string best = configuredBasePath;
            int bestScore = -1;

            foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(candidate))
                    continue;

                int score = 0;
                string reg = Path.Combine(candidate, "RegWindowsService");
                string mainX64 = Path.Combine(candidate, "CVMainWindowsService_x64");
                string commonDll = Path.Combine(candidate, "CommonDll");
                if (Directory.Exists(reg)) score += 200;
                if (Directory.Exists(mainX64)) score += 200;
                if (Directory.Exists(commonDll)) score += 200;

                try
                {
                    int fileCount = Directory.EnumerateFiles(candidate, "*", SearchOption.AllDirectories)
                        .Count(path => !IsLogPath(path, candidate));
                    score += fileCount;
                }
                catch
                {
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            AddLog($"服务备份源目录选择: {best}");
            return best;
        }

        private int CreateServiceBackupZip(string sourceRoot, string zipPath)
        {
            int packed = 0;
            using var fs = new FileStream(zipPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            using var archive = new ZipArchive(fs, ZipArchiveMode.Create);

            foreach (var filePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                if (IsLogPath(filePath, sourceRoot))
                    continue;

                try
                {
                    string entryName = Path.GetRelativePath(sourceRoot, filePath).Replace('\\', '/');
                    archive.CreateEntryFromFile(filePath, entryName, CompressionLevel.Optimal);
                    packed++;
                }
                catch (Exception ex)
                {
                    AddLog($"跳过文件（打包失败）: {filePath}，原因: {ex.Message}");
                }
            }

            return packed;
        }

        private int CreateCfgOnlyBackupZip(string sourceRoot, string zipPath)
        {
            int packed = 0;
            using var fs = new FileStream(zipPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            using var archive = new ZipArchive(fs, ZipArchiveMode.Create);

            string[] cfgRoots =
            {
                Path.Combine(sourceRoot, "CVMainWindowsService_dev", "cfg"),
                Path.Combine(sourceRoot, "CVMainWindowsService_x64", "cfg"),
                Path.Combine(sourceRoot, "RegWindowsService", "cfg")
            };

            foreach (string cfgRoot in cfgRoots)
            {
                if (!Directory.Exists(cfgRoot))
                    continue;

                foreach (var filePath in Directory.EnumerateFiles(cfgRoot, "*", SearchOption.AllDirectories))
                {
                    string entryName = Path.GetRelativePath(sourceRoot, filePath).Replace('\\', '/');
                    archive.CreateEntryFromFile(filePath, entryName, CompressionLevel.Optimal);
                    packed++;
                }
            }

            return packed;
        }

        private bool TryCreateServiceBackupWithWinRar(string sourceRoot, string backupFile)
        {
            string? winRarExe = FindWinRarExecutable();
            if (string.IsNullOrWhiteSpace(winRarExe))
                return false;

            string args = $"a -ma5 -r -ep1 -oi1 -x*\\log\\* \"{backupFile}\" \"{sourceRoot}\\*\"";
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = winRarExe,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = sourceRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var p = Process.Start(psi);
                if (p == null)
                    return false;

                p.WaitForExit();
                string stdErr = p.StandardError.ReadToEnd();
                string stdOut = p.StandardOutput.ReadToEnd();
                if (p.ExitCode <= 1 && File.Exists(backupFile))
                {
                    AddLog("WinRAR 压缩成功（RAR 格式，相同文件按参考保存，已忽略 log）");
                    return true;
                }

                AddLog($"WinRAR 压缩失败，ExitCode={p.ExitCode}");
                if (!string.IsNullOrWhiteSpace(stdErr))
                    AddLog($"WinRAR stderr: {stdErr}");
                if (!string.IsNullOrWhiteSpace(stdOut))
                    AddLog($"WinRAR stdout: {stdOut}");
                return false;
            }
            catch (Exception ex)
            {
                AddLog($"调用 WinRAR 异常: {ex.Message}");
                return false;
            }
        }

        private string? FindWinRarExecutable()
        {
            string[] fixedCandidates =
            {
                @"C:\Program Files\WinRAR\WinRAR.exe",
                @"C:\Program Files (x86)\WinRAR\WinRAR.exe",
                @"C:\Program Files\WinRAR\Rar.exe",
                @"C:\Program Files (x86)\WinRAR\Rar.exe"
            };

            foreach (var candidate in fixedCandidates)
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            string pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string dir in pathVar.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    string winRar = Path.Combine(dir.Trim(), "WinRAR.exe");
                    if (File.Exists(winRar))
                        return winRar;

                    string rar = Path.Combine(dir.Trim(), "Rar.exe");
                    if (File.Exists(rar))
                        return rar;
                }
                catch
                {
                }
            }

            return null;
        }

        private bool ExtractRarToDirectory(string rarFile, string targetDir)
        {
            string? winRarExe = FindWinRarExecutable();
            if (string.IsNullOrWhiteSpace(winRarExe))
                return false;

            Directory.CreateDirectory(targetDir);
            string args = $"x -y \"{rarFile}\" \"{targetDir}\\\"";
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = winRarExe,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var p = Process.Start(psi);
                if (p == null)
                    return false;

                p.WaitForExit();
                return p.ExitCode <= 1;
            }
            catch (Exception ex)
            {
                AddLog($"RAR 解压异常: {ex.Message}");
                return false;
            }
        }

        private string GetServiceVersionTag(string sourceRoot)
        {
            string[] candidates =
            {
                Path.Combine(sourceRoot, "CVMainWindowsService_x64", "CVMainWindowsService_x64.exe"),
                Path.Combine(sourceRoot, "CVMainWindowsService_dev", "CVMainWindowsService_dev.exe"),
                Path.Combine(sourceRoot, "RegWindowsService", "RegWindowsService.exe")
            };

            foreach (string exe in candidates)
            {
                if (!File.Exists(exe))
                    continue;

                var ver = WinServiceHelper.GetFileVersion(exe);
                if (ver != null)
                {
                    string version = ver.ToString();
                    if (!string.IsNullOrWhiteSpace(version))
                        return version.Replace('.', '_');
                }
            }

            return "unknown";
        }
    }
}
