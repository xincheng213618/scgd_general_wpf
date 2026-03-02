using ColorVision.Common.MVVM;
using ColorVision.UI.Desktop.Download;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Update
{
    public class ChangeLogEntry : ViewModelBase
    {
        [DisplayName("Version")]
        public string Version { get; set; }
        [DisplayName("ReleaseDate")]
        public DateTime ReleaseDate { get; set; }
        public List<string> Changes { get; set; }
        [DisplayName("ChangeLog")]
        public string ChangeLog 
        {
            get 
            {
                _ChangeLog ??= string.Join("\n", Changes);
                return _ChangeLog;
            }
        }

        private string _ChangeLog;
        public RelayCommand UpdateCommand { get; set; }

        public bool IsCurrentVision  =>  Version.Trim() == AutoUpdater.CurrentVersion?.ToString();

        public string UpdateString => new Version(Version) > AutoUpdater.CurrentVersion ? Properties.Resources.Update : Properties.Resources.Rollback;
        public ContextMenu ContextMenu { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        private bool _isExpanded = true;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                OnPropertyChanged();
            }
        }


        public ChangeLogEntry()
        {
            Changes = new List<string>();
            UpdateCommand = new RelayCommand(a =>
            {
                if (Version == null) return;
                Version version = new Version(Version);
                if (version > AutoUpdater.CurrentVersion)
                {
                    AutoUpdater.GetInstance().Update(Version, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision"));
                }
                else if (version == AutoUpdater.CurrentVersion)
                {
                    MessageBox.Show(WindowHelpers.GetActiveWindow(), ColorVision.Properties.Resources.SoftwareVersionIdentical);
                }
                else if (version.Major == AutoUpdater.CurrentVersion.Major && AutoUpdater.CurrentVersion.Minor == version.Minor && AutoUpdater.CurrentVersion.Build == version.Build)
                {
                    Aria2cDownloadManager.GetInstance().AddDownload($"http://xc213618.ddns.me:9999/D%3A/ColorVision/ColorVision-{version}.exe", Aria2cDownloadManager.GetInstance().Config.DefaultDownloadPath, "1:1");
                    DownloadWindow.ShowInstance();
                }
                else
                {
                    Aria2cDownloadManager.GetInstance().AddDownload($"http://xc213618.ddns.me:9999/D%3A/ColorVision/History/{version.Major}.{version.Minor}/{version.Major}.{version.Minor}.{version.Build}/ColorVision-{version}.exe", Aria2cDownloadManager.GetInstance().Config.DefaultDownloadPath, "1:1");
                    DownloadWindow.ShowInstance();
                }
            });
            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = ColorVision.Properties.Resources.Update, Command = UpdateCommand });
        }
    }
}
