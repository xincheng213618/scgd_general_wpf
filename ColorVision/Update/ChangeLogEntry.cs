using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Update
{
    public class ChangeLogEntry : ViewModelBase
    {
        public string Version { get; set; }
        public DateTime ReleaseDate { get; set; }
        public List<string> Changes { get; set; }
        public string ChangeLog { get => string.Join("\n", Changes);}

        public RelayCommand UpdateCommand { get; set; }
        public RelayCommand DownLoadCommand { get; set; }

        public bool IsUpdateAvailable => AutoUpdater.IsUpdateAvailable(Version);

        public bool IsCurrentVision  =>  Version.Trim() == AutoUpdater.CurrentVersion?.ToString();

        public string UpdateString => new Version(Version) > AutoUpdater.CurrentVersion ? Properties.Resources.Upgrade : ColorVision.Properties.Resources.Rollback;
        public ContextMenu ContextMenu { get; set; }
        public ChangeLogEntry()
        {
            Changes = new List<string>();
            UpdateCommand = new RelayCommand(a =>
            {
                if (Version == null) return;
                if (new Version(Version)> AutoUpdater.CurrentVersion)
                {
                    AutoUpdater.GetInstance().Update(Version, Path.GetTempPath());
                }
                else if (new Version(Version) == AutoUpdater.CurrentVersion)
                {
                    MessageBox.Show(WindowHelpers.GetActiveWindow(), "软件版本相同");
                }
                else
                {
                    MessageBox.Show(WindowHelpers.GetActiveWindow(), "回退软件需要先卸载在安装，或者是安装后重新运行安装包；");
                    AutoUpdater.GetInstance().Update(Version, Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
                }
            });
            DownLoadCommand = new RelayCommand(a => PlatformHelper.Open($"{AutoUpdateConfig.Instance.UpdatePath}/ColorVision/ColorVision-{Version}.exe"));
            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = "更新", Command = UpdateCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = "从浏览器中下载", Command = DownLoadCommand });
        }
    }
}
