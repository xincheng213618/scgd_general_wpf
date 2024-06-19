using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Navigation;
using Microsoft.Win32;

namespace ColorVisionSetup
{
    public class Utils
    {
        public const string InstallationFolderName = "\\LGHUB";

        private const string _settingsFileName = "\\settings.db";

        private const string _frontendExecutableName = "\\lghub.exe";

        private const string _uninstallerExecutableName = "\\lghub_software_manager.exe";

        private static bool _logToFile;

        private static string _logFilePath;

        private static string _securedTempDirPath;

        private static Semaphore semaphore;

        private static bool unique;

        public static void Log(string message)
        {

        }

        public static void EnableLogs()
        {

        }

        public static string GetProgramFilesPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LGHUB");
        }

        public static string GetProgramDataPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LGHUB");
        }

        public static string GetLocalAppDataPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LGHUB");
        }


        public static string GetSecuredTempDirPath()
        {
            return _securedTempDirPath;
        }

        public static void SetSecuredTempDirPath(string folder_path)
        {
            _securedTempDirPath = folder_path;
        }

        public static bool CreateRandomSecuredTempDirectory()
        {
            _securedTempDirPath = "";
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    string path = "ghub-" + Path.GetRandomFileName();
                    _securedTempDirPath = Path.Combine(Path.GetTempPath(), path);
                    if (Directory.Exists(_securedTempDirPath))
                    {
                        continue;
                    }
                    DirectorySecurity directorySecurity = new DirectorySecurity();
                    directorySecurity.AddAccessRule(GetAdminAccessRule());
                    directorySecurity.AddAccessRule(GetSystemAccessRule());
                    directorySecurity.AddAccessRule(GetWorldAccessRule());
                    Directory.CreateDirectory(_securedTempDirPath, directorySecurity);
                    Log("Utils: created random temp directory : " + _securedTempDirPath);
                    return true;
                }
                catch (Exception ex)
                {
                    Log("Utils::createRandomSecuredTempDirectory failed -> " + ex.Message);
                }
            }
            return false;
        }

        private static FileSystemAccessRule GetAdminAccessRule()
        {
            return new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null).Translate(typeof(NTAccount)).ToString(), FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow);
        }

        private static FileSystemAccessRule GetSystemAccessRule()
        {
            return new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null).Translate(typeof(NTAccount)).ToString(), FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow);
        }

        private static FileSystemAccessRule GetWorldAccessRule()
        {
            return new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null).Translate(typeof(NTAccount)).ToString(), FileSystemRights.ReadAndExecute, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow);
        }

        public static void DeleteExistingGhubTempDirectories()
        {
            string[] directories = Directory.GetDirectories(Path.GetTempPath(), "ghub-*");
            foreach (string text in directories)
            {
                try
                {
                    Directory.Delete(text, recursive: true);
                }
                catch (Exception ex)
                {
                    Log("Utils:: Deletion of temp directory " + text + " failed -> " + ex.Message);
                }
            }
        }

        public static bool IsAlreadyInstalled()
        {
            return Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\{521c89be-637f-4274-a840-baaf7460c2b2}", "DisplayName", null) != null;
        }

        public static bool IsLGSInstalled()
        {
            object value = Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Logitech Gaming Software", "VersionMajor", null);
            object value2 = Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Logitech Gaming Software", "VersionMinor", null);
            if (value == null || value2 == null)
            {
                return false;
            }
            if ((int)value > 8 || ((int)value == 8 && (int)value2 >= 98))
            {
                return false;
            }
            return true;
        }

        public static void FocusExistingApp()
        {
            semaphore = new Semaphore(0, 1, Assembly.GetExecutingAssembly().GetName().Name, out unique);
            if (unique)
            {
                return;
            }
            Process currentProcess = Process.GetCurrentProcess();
            Process[] processesByName = Process.GetProcessesByName(currentProcess.ProcessName);
            foreach (Process process in processesByName)
            {
                if (process.Id != currentProcess.Id)
                {
                    Core.SetForegroundWindow(process.MainWindowHandle);
                    Core.ShowWindow(process.MainWindowHandle, 9u);
                    break;
                }
            }
            Environment.Exit(0);
        }

        public static bool SettingsExist()
        {
            if (Directory.Exists(GetLocalAppDataPath()) && File.Exists(GetLocalAppDataPath() + "\\settings.db"))
            {
                return true;
            }
            return false;
        }

        public static bool NextJsonExist()
        {
            if (Directory.Exists(GetProgramDataPath()) && File.Exists(GetProgramDataPath() + "/next.json"))
            {
                return true;
            }
            return false;
        }

        public static bool StartGHUB()
        {
            if (Directory.Exists(GetProgramFilesPath()))
            {
                string text = GetProgramFilesPath() + "\\lghub.exe";
                if (File.Exists(text))
                {
                    try
                    {
                        Process.Start(text);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Log("Utils::StartGHUB -> " + ex.Message);
                        return false;
                    }
                }
            }
            return false;
        }

        public static void OpenExternalHyperlink(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        public static string GetBrandName()
        {
            string text = "Logitech";
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            ServicePointManager.Expect100Continue = true;
            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create("https://util.logitech.io/brand");
            httpWebRequest.ProtocolVersion = HttpVersion.Version10;
            httpWebRequest.Method = "HEAD";
            httpWebRequest.Timeout = 5000;
            try
            {
                using (HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse())
                {
                    if (httpWebResponse.StatusDescription == "OK")
                    {
                        text = "Logicool";
                    }
                }
                Log("Using brand: " + text);
            }
            catch (WebException ex)
            {
                Log("Brand acquisition status: (" + ex.Message + "), using default: " + text);
            }
            return text;
        }

        public static string WithBrand(string text, string brandName)
        {
            return Regex.Replace(text.Replace("Logitech", brandName).Replace("Logitech".ToUpper(), brandName.ToUpper()).Replace("Logicool", brandName)
                .Replace("Logicool".ToUpper(), brandName.ToUpper()), "\\t|\\n|\\r", "");
        }

        public static bool IsVCRedistInstalled()
        {
            try
            {
                RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\DevDiv\\VC\\Servicing\\14.0\\RuntimeMinimum", writable: false);
                if (registryKey != null && ((string)registryKey.GetValue("Version")).StartsWith("14"))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
            return false;
        }
    }

}

