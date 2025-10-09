using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
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

        public bool IsUpdateAvailable => AutoUpdater.IsUpdateAvailable(Version);

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
                NotifyPropertyChanged();
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
                    AutoUpdater.GetInstance().Update(Version, Path.GetTempPath());
                }
                else if (version == AutoUpdater.CurrentVersion)
                {
                    MessageBox.Show(WindowHelpers.GetActiveWindow(), "软件版本相同");
                }
                else
                {
                    MessageBox.Show(WindowHelpers.GetActiveWindow(), "回退软件需要先卸载在安装，或者是安装后重新运行安装包；");
                    PlatformHelper.Open($"http://xc213618.ddns.me:9998/upload/ColorVision/History/{version.Major}.{version.Minor}.{version.Build}");
                }
            });
            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = ColorVision.Properties.Resources.Update, Command = UpdateCommand });
        }
    }
}
