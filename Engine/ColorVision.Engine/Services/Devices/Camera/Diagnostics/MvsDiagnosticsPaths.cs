using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.Services.Devices.Camera.Diagnostics
{
    internal static class MvsDiagnosticsPaths
    {
        internal static IReadOnlyList<string> GetInstallationDirectories()
        {
            var directories = new List<string>();
            AddInstallationDirectory(directories, Environment.GetEnvironmentVariable("MVCAM_COMMON_RUNENV"));
            foreach (RegistryHive hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
            foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using RegistryKey root = RegistryKey.OpenBaseKey(hive, view);
                    using RegistryKey? key = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\HIK_MVS");
                    AddInstallationDirectory(directories, key?.GetValue("InstallLocation") as string);
                    AddInstallationDirectory(directories, GetExecutablePath(key?.GetValue("DisplayIcon") as string));
                    AddInstallationDirectory(directories, GetExecutablePath(key?.GetValue("UninstallString") as string));
                }
                catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
                {
                    // Installation discovery must still work when one registry view is unavailable.
                }
            }

            foreach (Environment.SpecialFolder folder in new[] { Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.ProgramFiles })
            {
                string directory = Environment.GetFolderPath(folder);
                if (!string.IsNullOrEmpty(directory)) AddInstallationDirectory(directories, Path.Combine(directory, "MVS"));
            }
            return directories;
        }

        private static void AddInstallationDirectory(List<string> directories, string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                string directory = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')));
                if (directory.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) directory = Path.GetDirectoryName(directory)!;
                while (Path.GetFileName(Path.TrimEndingDirectorySeparator(directory)) is string name
                    && new[] { "Development", "Applications", "Win64", "Win32", "Runtime", "Win64_x64", "Win32_i86" }.Contains(name, StringComparer.OrdinalIgnoreCase))
                    directory = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(directory))!;
                directory = Path.TrimEndingDirectorySeparator(directory);
                if (!directories.Contains(directory, StringComparer.OrdinalIgnoreCase)) directories.Add(directory);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException) { }
        }

        internal static string? FindLogViewer(IEnumerable<string> installationDirectories)
        {
            string[] directories = installationDirectories.ToArray();
            foreach (string architecture in new[] { "Win64", "Win32" })
            foreach (string directory in directories)
            {
                string path = Path.Combine(directory, "Applications", architecture, "LogViewer.exe");
                if (File.Exists(path)) return path;
            }
            return null;
        }

        internal static ProcessStartInfo CreateLogViewerStartInfo(string executablePath) => new()
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath)!,
            UseShellExecute = true,
            // The vendor LogViewer manifest requires administrator privileges.
            Verb = "runas",
        };

        internal static IReadOnlyList<string> GetLogServiceDirectories()
        {
            var directories = new List<string>();
            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\MvLogServer");
                string? executable = GetExecutablePath(key?.GetValue("ImagePath") as string);
                if (executable != null) directories.Add(Path.GetDirectoryName(executable)!);
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }

            foreach (Environment.SpecialFolder folder in new[] { Environment.SpecialFolder.CommonProgramFilesX86, Environment.SpecialFolder.CommonProgramFiles })
            {
                string directory = Environment.GetFolderPath(folder);
                if (!string.IsNullOrEmpty(directory)) directories.Add(Path.Combine(directory, "MVS", "Service", "Log"));
            }
            return directories.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        internal static string? GetExecutablePath(string? command)
        {
            if (string.IsNullOrWhiteSpace(command)) return null;
            string expanded = Environment.ExpandEnvironmentVariables(command.Trim().TrimStart('"'));
            int end = expanded.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            return end < 0 || !Path.IsPathFullyQualified(expanded[..(end + 4)]) ? null : expanded[..(end + 4)];
        }
    }
}
