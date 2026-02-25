using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.ComponentModel;
using System.IO;
using System;

namespace ColorVision.UI.Desktop.Download
{
    [DisplayName("DownloadManagerConfiguration")]
    public class DownloadManagerConfig : ViewModelBase, IConfig
    {
        public static DownloadManagerConfig Instance => ConfigService.Instance.GetRequiredService<DownloadManagerConfig>();

        [DisplayName("MaxConcurrentTasks")]
        [Description("Maximum number of simultaneous download tasks")]
        public int MaxConcurrentTasks { get => _MaxConcurrentTasks; set { _MaxConcurrentTasks = Math.Max(1, Math.Min(value, 16)); OnPropertyChanged(); } }
        private int _MaxConcurrentTasks = 5;

        [DisplayName("DefaultDownloadPath")]
        [Description("Default directory for saving downloaded files")]
        public string DefaultDownloadPath { get => _DefaultDownloadPath; set { _DefaultDownloadPath = value; OnPropertyChanged(); } }
        private string _DefaultDownloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        [DisplayName("EnableSpeedLimit")]
        [Description("Whether to enable download speed limit")]
        public bool EnableSpeedLimit { get => _EnableSpeedLimit; set { _EnableSpeedLimit = value; OnPropertyChanged(); } }
        private bool _EnableSpeedLimit;

        [DisplayName("SpeedLimitMB")]
        [Description("Download speed limit in MB/s (default 100MB/s)")]
        public int SpeedLimitMB { get => _SpeedLimitMB; set { _SpeedLimitMB = Math.Max(1, value); OnPropertyChanged(); } }
        private int _SpeedLimitMB = 100;


        [DisplayName("ShowCompletedNotification")]
        [Description("Show notification dialog when download completes")]
        public bool ShowCompletedNotification { get => _ShowCompletedNotification; set { _ShowCompletedNotification = value; OnPropertyChanged(); } }
        private bool _ShowCompletedNotification;

        [DisplayName("RunFileAfterDownload")]
        [Description("Automatically run/open file after download completes")]
        public bool RunFileAfterDownload { get => _RunFileAfterDownload; set { _RunFileAfterDownload = value; OnPropertyChanged(); } }
        private bool _RunFileAfterDownload;
    }
}
