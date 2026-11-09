using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.ComponentModel;
using System.IO;

namespace ColorVision.UI.Desktop.Download
{
    [DisplayName("DownloadManagerConfiguration")]
    public class DownloadManagerConfig : ViewModelBase, IConfig
    {
        public static DownloadManagerConfig Instance => ConfigService.Instance.GetRequiredService<DownloadManagerConfig>();

        [DisplayName("MaxConcurrentTasks")]
        [Description("MaxConcurrentTasksDescription")]
        public int MaxConcurrentTasks { get => _MaxConcurrentTasks; set { _MaxConcurrentTasks = Math.Max(1, Math.Min(value, 16)); OnPropertyChanged(); } }
        private int _MaxConcurrentTasks = 5;

        [DisplayName("DefaultDownloadPath")]
        [Description("DefaultDownloadPathDescription")]
        [PropertyEditorType(typeof(TextSelectFolderPropertiesEditor))]
        public string DefaultDownloadPath { get => _DefaultDownloadPath; set { _DefaultDownloadPath = value; OnPropertyChanged(); } }
        private string _DefaultDownloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        [DisplayName("EnableSpeedLimit")]
        [Description("EnableSpeedLimitDescription")]
        public bool EnableSpeedLimit { get => _EnableSpeedLimit; set { _EnableSpeedLimit = value; OnPropertyChanged(); } }
        private bool _EnableSpeedLimit;

        [DisplayName("SpeedLimitMB")]
        [Description("SpeedLimitMBDescription")]
        public int SpeedLimitMB { get => _SpeedLimitMB; set { _SpeedLimitMB = Math.Max(1, value); OnPropertyChanged(); } }
        private int _SpeedLimitMB = 100;


        [DisplayName("RpcPort")]
        [Description("RpcPortDescription")]
        public int RpcPort { get => _RpcPort; set { _RpcPort = Math.Max(1024, Math.Min(value, 65535)); OnPropertyChanged(); } }
        private int _RpcPort = 6800;

        [DisplayName("ShowCompletedNotification")]
        [Description("ShowCompletedNotificationDescription")]
        public bool ShowCompletedNotification { get => _ShowCompletedNotification; set { _ShowCompletedNotification = value; OnPropertyChanged(); } }
        private bool _ShowCompletedNotification;

        [DisplayName("RunFileAfterDownload")]
        [Description("RunFileAfterDownloadDescription")]
        public bool RunFileAfterDownload { get => _RunFileAfterDownload; set { _RunFileAfterDownload = value; OnPropertyChanged(); } }
        private bool _RunFileAfterDownload;

        [DisplayName("PromptDeleteFile")]
        [Description("PromptDeleteFileDescription")]
        public bool PromptDeleteFile { get => _PromptDeleteFile; set { _PromptDeleteFile = value; OnPropertyChanged(); } }
        private bool _PromptDeleteFile = true;

        [DisplayName("DefaultDeleteFile")]
        [Description("DefaultDeleteFileDescription")]
        public bool DefaultDeleteFile { get => _DefaultDeleteFile; set { _DefaultDeleteFile = value; OnPropertyChanged(); } }
        private bool _DefaultDeleteFile;
    }
}
