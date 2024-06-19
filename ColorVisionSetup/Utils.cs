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
        private static Semaphore semaphore;

        private static bool unique;

        public static void Log(string message)
        {

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

