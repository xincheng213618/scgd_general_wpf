using ColorVision.Common.MVVM;
using ColorVision.UI;
using System;
using System.ComponentModel;

namespace ColorVision.Database.SqliteLog
{
    public class SqliteLogManagerConfig : ViewModelBase, IConfig
    {
        public event EventHandler<bool> SqliteLogEnabledChanged;

        [ConfigSetting(Order = 1)]
        [DisplayName("启用日志记录")]
        public bool IsEnabled { get => _IsEnabled; set { _IsEnabled = value; OnPropertyChanged(); SqliteLogEnabledChanged?.Invoke(this, value); } }
        private bool _IsEnabled;

        [ConfigSetting(Order = 2)]
        [DisplayName("启用压缩归档")]
        [Description("如果启用，日志超过大小限制后将被压缩保存；如果不启用，超过限制将直接删除旧日志。")]
        public bool IsCompressionEnabled { get => _IsCompressionEnabled; set { _IsCompressionEnabled = value; OnPropertyChanged(); } }
        private bool _IsCompressionEnabled = true;

        [ConfigSetting(Order = 3)]
        [DisplayName("单文件大小限制 (MB)")]
        public int MaxFileSizeInMB { get => _MaxFileSizeInMB; set { _MaxFileSizeInMB = Math.Max(10, value); OnPropertyChanged(); } }
        private int _MaxFileSizeInMB = 1024; // 默认 1024MB

        [ConfigSetting(Order = 4)]
        [DisplayName("归档保留数量")]
        [Description("超过该数量后将自动删除最旧的归档（含 .db/.zip），防止磁盘被持续占满。")]
        public int MaxArchiveFiles { get => _MaxArchiveFiles; set { _MaxArchiveFiles = Math.Max(1, value); OnPropertyChanged(); } }
        private int _MaxArchiveFiles = 30;
    }
}