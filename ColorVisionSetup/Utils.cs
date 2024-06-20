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
            string[] directories = Directory.GetDirectories(Path.GetTempPath(), "ColorVision-*");
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

    }

}

