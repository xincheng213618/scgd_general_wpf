using log4net;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Spectrum.License
{
    public static class LicenseSync
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LicenseSync));

        public static readonly string CompanyName = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;
        public static readonly string LocalLicenseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license");
        public static readonly string GlobalLicenseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),CompanyName, "license");

        /// <summary>
        /// Ensures the specified directory exists.
        /// If a file (not a directory) with the same name exists, it is renamed to avoid conflicts.
        /// </summary>
        public static void EnsureDirectoryExists(string directoryPath)
        {
            if (File.Exists(directoryPath))
            {
                string backupPath = directoryPath + ".bak";
                try
                {
                    if (File.Exists(backupPath))
                        File.Delete(backupPath);
                    File.Move(directoryPath, backupPath);
                    log.Warn($"路径 '{directoryPath}' 处存在同名文件，已重命名为 '{backupPath}'");
                }
                catch (Exception ex)
                {
                    log.Error($"无法重命名冲突文件 '{directoryPath}'", ex);
                    try
                    {
                        File.Delete(directoryPath);
                    }
                    catch (Exception deleteEx)
                    {
                        log.Error($"无法删除冲突文件 '{directoryPath}'，目录创建可能失败", deleteEx);
                    }
                }
            }
            Directory.CreateDirectory(directoryPath);
        }

        /// <summary>
        /// 将许可证文件复制到本地目录（通常在安装目录下）。
        /// 如果没有写权限（如 Program Files），则先写入临时目录，再通过管理员权限 xcopy 复制。
        /// </summary>
        public static bool CopyToLocalLicenseDir(IEnumerable<string> sourceFiles)
        {
            var files = sourceFiles.ToList();
            if (files.Count == 0) return true;

            // 尝试直接复制
            try
            {
                EnsureDirectoryExists(LocalLicenseDir);

                foreach (var source in files)
                {
                    var dest = Path.Combine(LocalLicenseDir, Path.GetFileName(source));
                    File.Copy(source, dest, true);
                }
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                log.Info("本地许可证目录无写权限，尝试提权复制");
            }
            catch (IOException ex) when (ex.Message.Contains("denied", StringComparison.OrdinalIgnoreCase) ||
                                          ex.Message.Contains("拒绝", StringComparison.OrdinalIgnoreCase))
            {
                log.Info("本地许可证目录无写权限，尝试提权复制");
            }

            // 通过临时目录 + 管理员权限 xcopy 复制
            return CopyToLocalElevated(files);
        }

        /// <summary>
        /// 通过管理员权限将文件从临时目录复制到本地许可证目录。
        /// </summary>
        private static bool CopyToLocalElevated(List<string> sourceFiles)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "ColorVisionLicenses_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tempDir);
                foreach (var file in sourceFiles)
                {
                    var dest = Path.Combine(tempDir, Path.GetFileName(file));
                    File.Copy(file, dest, true);
                }

                // 构建批处理命令: 创建目录 -> 复制文件 -> 删除临时目录
                string cmdArgs = $"/c mkdir \"{LocalLicenseDir}\" 2>nul & xcopy \"{tempDir}\\*.*\" \"{LocalLicenseDir}\\\" /Y /I & rmdir /s /q \"{tempDir}\"";

                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = cmdArgs,
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                var proc = Process.Start(psi);
                proc?.WaitForExit();
                log.Info($"已通过管理员权限复制 {sourceFiles.Count} 个许可证文件到本地目录");
                return true;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                log.Warn("用户取消了管理员权限提权或提权失败", ex);
                try { Directory.Delete(tempDir, true); } catch { }
                return false;
            }
            catch (Exception ex)
            {
                log.Error("提权复制许可证到本地目录失败", ex);
                try { Directory.Delete(tempDir, true); } catch { }
                return false;
            }
        }

        public static void SyncLicenses()
        {
            // 1. 确保全局目录存在（在 AppData 下，始终可写）
            EnsureDirectoryExists(GlobalLicenseDir);

            // 2. 把当前目录下 license 文件夹下所有 .lic 文件拷贝到全局目录（全覆盖）
            if (Directory.Exists(LocalLicenseDir))
            {
                foreach (var file in Directory.GetFiles(LocalLicenseDir, "*.lic"))
                {
                    try
                    {
                        var dest = Path.Combine(GlobalLicenseDir, Path.GetFileName(file));
                        File.Copy(file, dest, true); // 覆盖
                    }
                    catch(Exception ex)
                    {
                        log.Debug($"同步许可证到全局目录失败: {file}", ex);
                    }
                }
            }

            // 3. 检查全局目录中的 .lic 文件，如果本地没有，则复制回来（支持提权）
            var filesToSync = new List<string>();
            foreach (var globalFile in Directory.GetFiles(GlobalLicenseDir, "*.lic"))
            {
                var localFile = Path.Combine(LocalLicenseDir, Path.GetFileName(globalFile));
                if (!File.Exists(localFile))
                {
                    filesToSync.Add(globalFile);
                }
            }

            if (filesToSync.Count > 0)
            {
                CopyToLocalLicenseDir(filesToSync);
            }

            // 4. Also sync via database
            try
            {
                LicenseDatabase.Instance.SyncToLocal();
            }
            catch (Exception ex)
            {
                log.Debug($"数据库同步许可证失败", ex);
            }
        }
    }
}