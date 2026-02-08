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

        public bool IsInstalled
        {
            get => _isInstalled;
            set { _isInstalled = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayOpacity)); }
        }
        private bool _isInstalled;

        public double DisplayOpacity => IsInstalled ? 1.0 : 0.4;

        public ImageSource? IconSource
        {
            get => _iconSource;
            set { _iconSource = value; OnPropertyChanged(); }
        }
        private ImageSource? _iconSource;

        public ICommand DoubleClickCommand => new RelayCommand(a => OnDoubleClick());
        public ICommand OpenDirectoryCommand => new RelayCommand(a => OnOpenDirectory(), b => IsInstalled && !string.IsNullOrEmpty(InstallDirectory));

        public void RefreshStatus()
        {
            IsInstalled = false;
            InstallDirectory = null;
            InstalledExePath = null;

            if (RegistryKeys != null)
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

            if (IconSource == null && File.Exists(InstallerPath))
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
                    MessageBox.Show(ex.Message);
                }
            }
            else if (File.Exists(InstallerPath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(InstallerPath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        private void OnOpenDirectory()
        {
            if (!string.IsNullOrEmpty(InstallDirectory) && Directory.Exists(InstallDirectory))
            {
                try
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", InstallDirectory) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
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
