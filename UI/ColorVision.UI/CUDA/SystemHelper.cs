using ColorVision.Common.MVVM;
using Microsoft.Win32;
using System.Reflection;
using System.Windows.Forms;


namespace ColorVision.UI.CUDA
{
    public class SystemHelper : ViewModelBase, IConfig, IConfigSettingProvider
    {
        public static SystemHelper Instance => ConfigService.Instance.GetRequiredService<SystemHelper>();

        // 获取是否处于调试模式
        public static bool IsDebugMode()
        {
            #if DEBUG
            return true;
            #else
            return false;
            #endif
        }

        // 获取操作系统版本
        public static string GetOSVersion()
        {
            return Environment.OSVersion.ToString();
        }

        // 获取 .NET 运行时版本
        public static string GetDotNetVersion()
        {
            return Environment.Version.ToString();
        }

        // 获取应用程序版本
        public static string? GetApplicationVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString();
        }

        // 获取系统内存信息
        public static string GetMemoryInfo()
        {
            var info = new Microsoft.VisualBasic.Devices.ComputerInfo();
            return $"Available Physical Memory: {info.AvailablePhysicalMemory / (1024 * 1024)} MB, Total Physical Memory: {info.TotalPhysicalMemory / (1024 * 1024)} MB";
        }

        public static string GetTotalPhysicalMemory()
        {
            var info = new Microsoft.VisualBasic.Devices.ComputerInfo();
            return Common.Utilities.MemorySize.MemorySizeText((long)info.TotalPhysicalMemory);
        }



        // 获取系统语言
        public static string GetSystemLanguage()
        {
            return System.Globalization.CultureInfo.InstalledUICulture.EnglishName;
        }

        // 获取当前屏幕分辨率
        public static string GetScreenResolution()
        {
            return $"{Screen.PrimaryScreen?.Bounds.Width} x {Screen.PrimaryScreen?.Bounds.Height}";
        }

        // 获取当前计算机名称
        public static string GetMachineName()
        {
            return Environment.MachineName;
        }

        // 获取当前用户名称
        public static string GetUserName()
        {
            return Environment.UserName;
        }

        public static string LocalCpuInfo
        {
            get
            {
                try
                {
                    RegistryKey reg = Registry.LocalMachine;
                    reg = reg.OpenSubKey("HARDWARE\\DESCRIPTION\\System\\CentralProcessor\\0");
                    return reg.GetValue("ProcessorNameString").ToString();
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata>
            {

            };
        }
    }
}
