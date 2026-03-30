using log4net;
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

        public static void SyncLicenses()
        {
            // 1. 确保全局目录存在
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
            else
            {
                EnsureDirectoryExists(LocalLicenseDir);
            }

            // 3. 检查全局目录中的 .lic 文件，如果本地没有，则复制回来
            foreach (var globalFile in Directory.GetFiles(GlobalLicenseDir, "*.lic"))
            {
                var localFile = Path.Combine(LocalLicenseDir, Path.GetFileName(globalFile));
                if (!File.Exists(localFile))
                {
                    EnsureDirectoryExists(LocalLicenseDir);
                    File.Copy(globalFile, localFile, true);
                }
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