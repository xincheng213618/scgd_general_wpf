#pragma warning disable CS8602,CS8603
#pragma warning disable CA1850
using Microsoft.Win32;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;


namespace ColorVision.UI.CUDA
{
    public static class SystemHelper 
    {
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

        public static string GetHardwareId()
        {
            if (!string.IsNullOrWhiteSpace(HardwareIdCache))
            {
                return HardwareIdCache;
            }

            try
            {
                StringBuilder fingerprint = new();
                AppendFingerprintPart(fingerprint, GetMachineGuid());
                AppendFingerprintPart(fingerprint, LocalCpuInfo);
                AppendFingerprintPart(fingerprint, GetBiosValue("SystemManufacturer"));
                AppendFingerprintPart(fingerprint, GetBiosValue("SystemProductName"));
                AppendFingerprintPart(fingerprint, GetBiosValue("BaseBoardManufacturer"));
                AppendFingerprintPart(fingerprint, GetBiosValue("BaseBoardProduct"));
                AppendFingerprintPart(fingerprint, GetMacFingerprint());

                if (fingerprint.Length == 0)
                {
                    HardwareIdCache = "Unavailable";
                    return HardwareIdCache;
                }

                using SHA256 sha256 = SHA256.Create();
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(fingerprint.ToString()));
                HardwareIdCache = Convert.ToHexString(hash);
                return HardwareIdCache;
            }
            catch
            {
                HardwareIdCache = "Unavailable";
                return HardwareIdCache;
            }
        }

        // 获取应用程序启动时间
        public static DateTime GetStartupTime()
        {
            if (StartTime == DateTime.MinValue)
            {
                StartTime = System.Diagnostics.Process.GetCurrentProcess().StartTime;
            }
            return StartTime;
        }

        private static DateTime StartTime = DateTime.MinValue;

        // 获取应用程序运行时长
        public static TimeSpan GetUptime()
        {
            return DateTime.Now - GetStartupTime();
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

        private static string HardwareIdCache = string.Empty;

        private static void AppendFingerprintPart(StringBuilder fingerprint, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (fingerprint.Length > 0)
            {
                fingerprint.Append('|');
            }

            fingerprint.Append(value.Trim());
        }

        private static string GetMachineGuid()
        {
            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Cryptography");
                return key?.GetValue("MachineGuid")?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string GetBiosValue(string valueName)
        {
            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey("HARDWARE\\DESCRIPTION\\System\\BIOS");
                return key?.GetValue(valueName)?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string GetMacFingerprint()
        {
            try
            {
                string[] ignoredKeywords = ["virtual", "vmware", "hyper-v", "bluetooth", "loopback", "vpn", "tap", "tun", "docker", "vethernet"];
                List<string> addresses = new();

                foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    {
                        continue;
                    }

                    string description = $"{networkInterface.Name} {networkInterface.Description}".ToLowerInvariant();
                    if (ignoredKeywords.Any(description.Contains))
                    {
                        continue;
                    }

                    string address = networkInterface.GetPhysicalAddress().ToString();
                    if (!string.IsNullOrWhiteSpace(address))
                    {
                        addresses.Add(address);
                    }
                }

                if (addresses.Count == 0)
                {
                    foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        string address = networkInterface.GetPhysicalAddress().ToString();
                        if (!string.IsNullOrWhiteSpace(address))
                        {
                            addresses.Add(address);
                        }
                    }
                }

                return string.Join("|", addresses.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase));
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
