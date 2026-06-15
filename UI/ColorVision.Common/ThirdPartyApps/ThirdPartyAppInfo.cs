#pragma warning disable CA1865
using ColorVision.Common.MVVM;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Common.ThirdPartyApps
{
    public class ThirdPartyAppContextAction
    {
        public string Header { get; set; } = string.Empty;
        public Action? Execute { get; set; }
        public Func<bool>? CanExecute { get; set; }

        public bool IsEnabled => Execute != null && (CanExecute?.Invoke() ?? true);

        public void Invoke()
        {
            if (!IsEnabled) return;
            Execute?.Invoke();
        }
    }

    public class ThirdPartyAppInfo : ViewModelBase
    {
        public string Name { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public int Order { get; set; } = 100;
        public string InstallerPath { get; set; } = string.Empty;
        public string? InstalledExePath { get; set; }
        public string? InstallDirectory { get; set; }
        public string[]? RegistryKeys { get; set; }
        public string[]? RegistryDisplayNames { get; set; }
        public string[]? KnownExePaths { get; set; }
        public string? ExecutableFileName { get; set; }
        public Action? InstallAction { get; set; }
        public IList<ThirdPartyAppContextAction> ContextActions { get; } = new List<ThirdPartyAppContextAction>();

        /// <summary>
        /// Portable apps are standalone executables that don't need installation.
        /// They are considered "installed" if the exe file exists.
        /// </summary>
        public bool IsPortable { get; set; }

        /// <summary>
        /// For portable apps, this is the path to the executable.
        /// </summary>
        public string? PortableExePath { get; set; }

        /// <summary>
        /// Launch command for system apps (e.g., "cmd.exe", "regedit.exe", "services.msc").
        /// When set, the app is always considered installed and launched via this command.
        /// </summary>
        public string? LaunchPath { get; set; }

        /// <summary>
        /// Optional arguments for the launch command.
        /// </summary>
        public string? LaunchArguments { get; set; }

        /// <summary>
        /// Action to execute for internal app windows.
        /// When set, the app is always considered installed and launched via this action.
        /// </summary>
        public Action? LaunchAction { get; set; }

        /// <summary>
        /// For LaunchAction apps, provides a way to get the executable path for icon extraction.
        /// Returns the path to an executable that can be used to extract the application icon.
        /// </summary>
        public Func<string?>? GetIconPath { get; set; }

        private static readonly ConcurrentDictionary<string, BitmapSource> IconSourceCache = new(StringComparer.OrdinalIgnoreCase);

        public bool IsInstalled
        {
            get => _isInstalled;
            set { _isInstalled = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayOpacity)); OnPropertyChanged(nameof(StatusText)); }
        }
        private bool _isInstalled;

        public double DisplayOpacity => IsInstalled ? 1.0 : 0.4;

        public string StatusText => IsInstalled ? string.Empty : "Not Installed";

        public ImageSource? IconSource
        {
            get => _iconSource;
            set { _iconSource = value; OnPropertyChanged(); }
        }
        private ImageSource? _iconSource;

        public ICommand DoubleClickCommand => new RelayCommand(a => OnDoubleClick());
        public ICommand OpenDirectoryCommand => new RelayCommand(a => OnOpenDirectory(), b => !string.IsNullOrEmpty(GetDirectoryPath()));

        public void RefreshStatus()
        {
            if (LaunchAction != null)
            {
                IsInstalled = true;
                LoadIcon();
                return;
            }

            if (!string.IsNullOrEmpty(LaunchPath))
            {
                IsInstalled = true;
                LoadIcon();
                return;
            }

            IsInstalled = false;
            InstallDirectory = null;
            InstalledExePath = null;

            if (IsPortable)
            {
                if (!string.IsNullOrEmpty(PortableExePath) && File.Exists(PortableExePath))
                {
                    InstalledExePath = PortableExePath;
                    InstallDirectory = Path.GetDirectoryName(PortableExePath);
                    IsInstalled = true;
                }
            }
            else if (RegistryKeys != null)
            {
                foreach (var key in RegistryKeys)
                {
                    string? path = FindExeFromRegistry(key, ExecutableFileName);
                    if (!string.IsNullOrEmpty(path))
                    {
                        SetInstalledPath(path);
                        break;
                    }
                }
            }

            if (!IsInstalled && RegistryDisplayNames != null)
            {
                foreach (var displayName in RegistryDisplayNames)
                {
                    string? path = FindExeByDisplayName(displayName, ExecutableFileName);
                    if (!string.IsNullOrEmpty(path))
                    {
                        SetInstalledPath(path);
                        break;
                    }
                }
            }

            if (!IsInstalled && KnownExePaths != null)
            {
                foreach (var path in KnownExePaths)
                {
                    string? existingPath = ResolveExistingFile(path);
                    if (!string.IsNullOrEmpty(existingPath))
                    {
                        SetInstalledPath(existingPath);
                        break;
                    }
                }
            }

            LoadIcon();
        }

        private void SetInstalledPath(string path)
        {
            InstalledExePath = path;
            InstallDirectory = Path.GetDirectoryName(path);
            IsInstalled = true;
        }

        private void LoadIcon()
        {
            IconSource = null;

            // Try to get icon from GetIconPath (for LaunchAction apps)
            if (GetIconPath != null)
            {
                try
                {
                    string? iconPath = GetIconPath();
                    if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                    {
                        ImageSource? iconSource = CreateIconSource(iconPath);
                        if (iconSource != null)
                        {
                            IconSource = iconSource;
                            return;
                        }
                    }
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(LaunchPath))
            {
                try
                {
                    string? resolvedPath = ResolveSystemPath(LaunchPath);
                    if (!string.IsNullOrEmpty(resolvedPath) && File.Exists(resolvedPath))
                    {
                        IconSource = CreateIconSource(resolvedPath);
                    }
                }
                catch { }
            }

            if (IconSource == null && IsInstalled && !string.IsNullOrEmpty(InstalledExePath) && File.Exists(InstalledExePath))
            {
                try
                {
                    IconSource = CreateIconSource(InstalledExePath);
                }
                catch { }
            }

            if (IconSource == null && !string.IsNullOrEmpty(InstallerPath) && File.Exists(InstallerPath))
            {
                try
                {
                    IconSource = CreateIconSource(InstallerPath);
                }
                catch { }
            }
        }

        private static BitmapSource? CreateIconSource(string path)
        {
            if (IconSourceCache.TryGetValue(path, out var cachedSource))
                return cachedSource;

            try
            {
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                if (icon == null)
                    return null;

                var source = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                if (source.CanFreeze)
                    source.Freeze();
                IconSourceCache.TryAdd(path, source);
                return source;
            }
            catch
            {
                return null;
            }
        }


        private static string? ResolveSystemPath(string path)
        {
            if (File.Exists(path))
                return path;

            string systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string fullPath = Path.Combine(systemDir, path);
            if (File.Exists(fullPath))
                return fullPath;

            return null;
        }

        private void OnDoubleClick()
        {
            if (LaunchAction != null)
            {
                try
                {
                    LaunchAction.Invoke();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to launch {Name}: {ex.Message}");
                }
                return;
            }

            if (!string.IsNullOrEmpty(LaunchPath))
            {
                try
                {
                    var psi = new ProcessStartInfo(LaunchPath) { UseShellExecute = true };
                    if (!string.IsNullOrEmpty(LaunchArguments))
                        psi.Arguments = LaunchArguments;
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to launch {Name}: {ex.Message}");
                }
                return;
            }

            if (IsInstalled && !string.IsNullOrEmpty(InstalledExePath) && File.Exists(InstalledExePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(InstalledExePath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to launch {Name}: {ex.Message}");
                }
            }
            else if (CanRunInstaller())
            {
                RunInstaller();
            }
        }

        public bool CanRunInstaller()
        {
            if (InstallAction != null)
                return true;

            return !string.IsNullOrEmpty(InstallerPath) && File.Exists(InstallerPath);
        }

        public void RunInstaller()
        {
            if (InstallAction != null)
            {
                try
                {
                    InstallAction.Invoke();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to run installer for {Name}: {ex.Message}");
                }
                return;
            }

            if (!string.IsNullOrEmpty(InstallerPath) && File.Exists(InstallerPath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(InstallerPath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to run installer for {Name}: {ex.Message}");
                }
            }
        }

        private string? GetDirectoryPath()
        {
            if (!string.IsNullOrEmpty(InstallDirectory))
                return InstallDirectory;
            if (GetIconPath != null)
            {
                string? iconPath = GetIconPath();
                if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                    return Path.GetDirectoryName(Path.GetFullPath(iconPath));
            }
            if (IsPortable && !string.IsNullOrEmpty(PortableExePath))
                return Path.GetDirectoryName(Path.GetFullPath(PortableExePath));
            return null;
        }

        private void OnOpenDirectory()
        {
            string? dir = GetDirectoryPath();
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                try
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to open directory for {Name}: {ex.Message}");
                }
            }
        }

        private static string? FindExeFromRegistry(string registryKey, string? executableFileName)
        {
            string? localMachinePath = FindExeFromRegistry(Registry.LocalMachine, registryKey, executableFileName);
            if (!string.IsNullOrEmpty(localMachinePath))
                return localMachinePath;

            return FindExeFromRegistry(Registry.CurrentUser, registryKey, executableFileName);
        }

        private static string? FindExeFromRegistry(RegistryKey rootKey, string registryKey, string? executableFileName)
        {
            try
            {
                using var key = rootKey.OpenSubKey(registryKey);
                return FindExeFromRegistryKey(key, executableFileName);
            }
            catch { }

            return null;
        }

        private static string? FindExeByDisplayName(string displayName, string? executableFileName)
        {
            string[] uninstallRoots =
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
            };

            foreach (var rootKey in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                foreach (var uninstallRoot in uninstallRoots)
                {
                    try
                    {
                        using var uninstallKey = rootKey.OpenSubKey(uninstallRoot);
                        if (uninstallKey == null)
                            continue;

                        foreach (var subKeyName in uninstallKey.GetSubKeyNames())
                        {
                            using var appKey = uninstallKey.OpenSubKey(subKeyName);
                            string? registryDisplayName = appKey?.GetValue("DisplayName") as string;
                            if (string.IsNullOrEmpty(registryDisplayName))
                                continue;

                            if (!registryDisplayName.Contains(displayName, StringComparison.OrdinalIgnoreCase))
                                continue;

                            string? path = FindExeFromRegistryKey(appKey, executableFileName);
                            if (!string.IsNullOrEmpty(path))
                                return path;
                        }
                    }
                    catch { }
                }
            }

            return null;
        }

        private static string? FindExeFromRegistryKey(RegistryKey? key, string? executableFileName)
        {
            if (key == null)
                return null;

            foreach (var valueName in new[] { "DisplayIcon", "InstallLocation", "UninstallString" })
            {
                string? path = ResolveExecutableValue(key.GetValue(valueName) as string, executableFileName);
                if (!string.IsNullOrEmpty(path))
                    return path;
            }

            return null;
        }

        private static string? ResolveExecutableValue(string? value, string? executableFileName)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string text = value.Trim();

            if (text.StartsWith("\"", StringComparison.Ordinal))
            {
                int endQuote = text.IndexOf('"', 1);
                if (endQuote > 1)
                {
                    string quotedPath = text.Substring(1, endQuote - 1);
                    string? resolvedQuotedPath = ResolveExistingFileOrDirectory(quotedPath, executableFileName);
                    if (!string.IsNullOrEmpty(resolvedQuotedPath))
                        return resolvedQuotedPath;
                }
            }

            string? resolvedText = ResolveExistingFileOrDirectory(text.Trim('"'), executableFileName);
            if (!string.IsNullOrEmpty(resolvedText))
                return resolvedText;

            int exeIndex = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeIndex >= 0)
            {
                string exePath = text.Substring(0, exeIndex + 4).Trim().Trim('"');
                string? resolvedExePath = ResolveExistingFileOrDirectory(exePath, executableFileName);
                if (!string.IsNullOrEmpty(resolvedExePath))
                    return resolvedExePath;
            }

            int commaIndex = text.IndexOf(',', StringComparison.Ordinal);
            if (commaIndex > 0)
            {
                string pathBeforeComma = text.Substring(0, commaIndex).Trim().Trim('"');
                string? resolvedPathBeforeComma = ResolveExistingFileOrDirectory(pathBeforeComma, executableFileName);
                if (!string.IsNullOrEmpty(resolvedPathBeforeComma))
                    return resolvedPathBeforeComma;
            }

            return null;
        }

        private static string? ResolveExistingFileOrDirectory(string path, string? executableFileName)
        {
            string? filePath = ResolveExistingFile(path);
            if (!string.IsNullOrEmpty(filePath))
            {
                if (string.IsNullOrEmpty(executableFileName) || Path.GetFileName(filePath).Equals(executableFileName, StringComparison.OrdinalIgnoreCase))
                    return filePath;

                string? directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                    return FindExecutableInDirectory(directory, executableFileName);

                return null;
            }

            try
            {
                if (Directory.Exists(path))
                    return FindExecutableInDirectory(path, executableFileName);
            }
            catch { }

            return null;
        }

        private static string? ResolveExistingFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            try
            {
                string fullPath = Path.GetFullPath(path);
                return File.Exists(fullPath) ? fullPath : null;
            }
            catch
            {
                return File.Exists(path) ? path : null;
            }
        }

        private static string? FindExecutableInDirectory(string directory, string? executableFileName)
        {
            if (!string.IsNullOrEmpty(executableFileName))
            {
                string exactPath = Path.Combine(directory, executableFileName);
                if (File.Exists(exactPath))
                    return exactPath;
            }

            foreach (var exe in Directory.GetFiles(directory, "*.exe", SearchOption.TopDirectoryOnly))
                return exe;

            return null;
        }
    }
}
