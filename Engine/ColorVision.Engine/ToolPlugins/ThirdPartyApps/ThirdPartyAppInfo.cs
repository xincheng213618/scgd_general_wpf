using ColorVision.Common.MVVM;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.ToolPlugins.ThirdPartyApps
{
    public class ThirdPartyAppInfo : ViewModelBase
    {
        public string Name { get; set; } = string.Empty;
        public string InstallerPath { get; set; } = string.Empty;
        public string? InstalledExePath { get; set; }
        public string? InstallDirectory { get; set; }
        public string[]? RegistryKeys { get; set; }

        /// <summary>
        /// Portable apps are standalone executables that don't need installation.
        /// They are considered "installed" if the exe file exists.
        /// </summary>
        public bool IsPortable { get; set; }

        /// <summary>
        /// For portable apps, this is the path to the executable.
        /// </summary>
        public string? PortableExePath { get; set; }

        public bool IsInstalled
        {
            get => _isInstalled;
            set { _isInstalled = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayOpacity)); OnPropertyChanged(nameof(StatusText)); }
        }
        private bool _isInstalled;

        public double DisplayOpacity => IsInstalled ? 1.0 : 0.4;

        public string StatusText => IsInstalled ? string.Empty : Properties.Resources.NotInstalled;

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
                    string? path = FindExeFromRegistry(key);
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        InstalledExePath = path;
                        InstallDirectory = Path.GetDirectoryName(path);
                        IsInstalled = true;
                        break;
                    }
                }
            }

            LoadIcon();
        }

        private void LoadIcon()
        {
            IconSource = null;
            if (IsInstalled && !string.IsNullOrEmpty(InstalledExePath) && File.Exists(InstalledExePath))
            {
                try
                {
                    using var icon = System.Drawing.Icon.ExtractAssociatedIcon(InstalledExePath);
                    if (icon != null)
                    {
                        IconSource = Imaging.CreateBitmapSourceFromHIcon(
                            icon.Handle,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                    }
                }
                catch { }
            }

            if (IconSource == null && !string.IsNullOrEmpty(InstallerPath) && File.Exists(InstallerPath))
            {
                try
                {
                    using var icon = System.Drawing.Icon.ExtractAssociatedIcon(InstallerPath);
                    if (icon != null)
                    {
                        IconSource = Imaging.CreateBitmapSourceFromHIcon(
                            icon.Handle,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                    }
                }
                catch { }
            }
        }

        private void OnDoubleClick()
        {
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
            else if (!string.IsNullOrEmpty(InstallerPath) && File.Exists(InstallerPath))
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

        private static string? FindExeFromRegistry(string registryKey)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(registryKey);
                if (key != null)
                {
                    var val = key.GetValue("DisplayIcon") as string
                           ?? key.GetValue("InstallLocation") as string;
                    if (!string.IsNullOrEmpty(val))
                    {
                        val = val.Trim('"');
                        if (File.Exists(val))
                            return val;
                        if (Directory.Exists(val))
                        {
                            foreach (var exe in Directory.GetFiles(val, "*.exe", SearchOption.TopDirectoryOnly))
                                return exe;
                        }
                    }
                }
            }
            catch { }

            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(registryKey);
                if (key != null)
                {
                    var val = key.GetValue("DisplayIcon") as string
                           ?? key.GetValue("InstallLocation") as string;
                    if (!string.IsNullOrEmpty(val))
                    {
                        val = val.Trim('"');
                        if (File.Exists(val))
                            return val;
                        if (Directory.Exists(val))
                        {
                            foreach (var exe in Directory.GetFiles(val, "*.exe", SearchOption.TopDirectoryOnly))
                                return exe;
                        }
                    }
                }
            }
            catch { }

            return null;
        }
    }
}
