using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace WindowsServicePlugin.ServiceManager
{
    /// <summary>
    /// 增量更新包安装：检测、应用（跳过 cfg/ 目录以保留现有环境配置）
    ///
    /// 增量包结构（示例：4.0.3.318.zip）：
    ///   4.0.3.318/
    ///     CommonDll/         ← 共享 DLL，复制到所有服务目录
    ///     CVMainWindowsService_dev/
    ///       CVMainWindowsService_dev.exe
    ///       plugin/...
    ///     CVMainWindowsService_x64/
    ///       CVMainWindowsService_x64.exe
    ///       cfg_files/...    ← 静态设备文件，允许覆盖
    ///       plugin/...
    ///     RegWindowsService/
    ///       *.exe / *.dll
    ///
    /// 与完整包的区别：
    ///   1. 根目录为版本号文件夹（X.X.X.X），而非 CVWindowsService/
    ///   2. 只含变更文件，不重新注册 Windows 服务
    ///   3. 保留每个服务的 cfg/ 目录（含 MySql.config / MQTT.config / WinService.config）
    /// </summary>
    public partial class ServiceInstallViewModel
    {
        /// <summary>
        /// 判断给定 zip 是否为增量更新包：
        /// 条件：zip 根目录下只有一个 X.X.X.X 格式的文件夹。
        /// </summary>
        internal static bool IsIncrementalUpdatePackage(string zipPath)
        {
            try
            {
                using var archive = ZipFile.OpenRead(zipPath);
                var topLevelFolders = archive.Entries
                    .Where(e => e.FullName.EndsWith('/') && e.FullName.Count(c => c == '/') == 1)
                    .Select(e => e.FullName.TrimEnd('/'))
                    .ToList();

                return topLevelFolders.Count == 1
                    && Regex.IsMatch(topLevelFolders[0], @"^\d+\.\d+\.\d+\.\d+$");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 应用增量更新包。
        /// 调用前服务应已停止；调用后由外部流程同步配置并启动服务。
        /// </summary>
        /// <param name="basePath">服务安装根目录（BaseLocation）</param>
        internal void ApplyIncrementalUpdate(string basePath)
        {
            string zipPath = ServicePackagePath;
            string tempDir = Path.Combine(Path.GetTempPath(), "CVIncrUpdate_" + Path.GetRandomFileName());
            try
            {
                AddLog($"解压增量更新包…");
                ZipFile.ExtractToDirectory(zipPath, tempDir);

                // 唯一子目录即版本根目录，例如 4.0.3.318
                string versionRoot = Directory.GetDirectories(tempDir).FirstOrDefault() ?? tempDir;
                string versionName = Path.GetFileName(versionRoot);
                AddLog($"增量更新版本: {versionName}");

                string installRoot = ResolveServiceInstallRoot(basePath);
                AddLog($"安装根目录: {installRoot}");

                // 1. 先将 CommonDll 复制到各服务目录根层
                string commonDllDir = Path.Combine(versionRoot, "CommonDll");
                if (Directory.Exists(commonDllDir))
                {
                    string[] serviceTargets = ["RegWindowsService", "CVMainWindowsService_x64", "CVMainWindowsService_dev"];
                    foreach (var targetName in serviceTargets)
                    {
                        string targetDir = Path.Combine(installRoot, targetName);
                        if (!Directory.Exists(targetDir))
                            continue;
                        int count = CopyFilesSkippingCfg(commonDllDir, targetDir);
                        AddLog($"CommonDll → {targetName}: {count} 个文件");
                    }
                }

                // 2. 复制各服务目录（跳过 cfg/ 子目录）
                foreach (var srcServiceDir in Directory.GetDirectories(versionRoot))
                {
                    string serviceFolderName = Path.GetFileName(srcServiceDir);
                    if (serviceFolderName.Equals("CommonDll", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string destDir = Path.Combine(installRoot, serviceFolderName);
                    if (!Directory.Exists(destDir))
                    {
                        AddLog($"目标目录不存在，跳过: {serviceFolderName}");
                        continue;
                    }

                    int count = CopyFilesSkippingCfg(srcServiceDir, destDir);
                    AddLog($"已更新 {serviceFolderName}: {count} 个文件");
                }

                AddLog($"增量更新 {versionName} 文件复制完成");
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                }
                catch (Exception ex)
                {
                    AddLog($"清理临时目录失败（可忽略）: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 递归复制目录内容到目标目录，跳过名为 cfg 的子目录以保留现有环境配置。
        /// </summary>
        private int CopyFilesSkippingCfg(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            int count = 0;

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);
                count++;
            }

            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(subDir);
                if (dirName.Equals("cfg", StringComparison.OrdinalIgnoreCase))
                {
                    AddLog($"  跳过 cfg 目录（保留现有配置）");
                    continue;
                }
                count += CopyFilesSkippingCfg(subDir, Path.Combine(destDir, dirName));
            }

            return count;
        }
    }
}
