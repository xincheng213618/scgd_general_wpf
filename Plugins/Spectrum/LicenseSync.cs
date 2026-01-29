using System.IO;
using System.Reflection;

namespace Spectrum
{
    public static class LicenseSync
    {
        public static readonly string CompanyName = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;
        public static readonly string LocalLicenseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license");
        public static readonly string GlobalLicenseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),CompanyName, "license");

        public static void SyncLicenses()
        {
            // 1. 确保全局目录存在
            Directory.CreateDirectory(GlobalLicenseDir);

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

                    }

                }
            }
            else
            {
                Directory.CreateDirectory(LocalLicenseDir);
            }

            // 3. 检查全局目录中的 .lic 文件，如果本地没有，则复制回来
            foreach (var globalFile in Directory.GetFiles(GlobalLicenseDir, "*.lic"))
            {
                var localFile = Path.Combine(LocalLicenseDir, Path.GetFileName(globalFile));
                if (!File.Exists(localFile))
                {
                    Directory.CreateDirectory(LocalLicenseDir);
                    File.Copy(globalFile, localFile, true);
                }
            }
        }
    }
}