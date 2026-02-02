using ColorVision.Common.MVVM;
using ColorVision.UI;
using System;
using System.ComponentModel;

namespace ColorVision.Database.SqliteLog
{
    public class SqliteLogManagerConfig : ViewModelBase, IConfig
    {
        public event EventHandler<bool> SqliteLogEnabledChanged;

        [DisplayName("启用日志记录")]
        public bool IsEnabled { get => _IsEnabled; set { _IsEnabled = value; OnPropertyChanged(); SqliteLogEnabledChanged?.Invoke(this, value); } }
        private bool _IsEnabled;

        [DisplayName("启用压缩归档")]
        [Description("如果启用，日志超过大小限制后将被压缩保存；如果不启用，超过限制将直接删除旧日志。")]
        public bool IsCompressionEnabled { get => _IsCompressionEnabled; set { _IsCompressionEnabled = value; OnPropertyChanged(); } }
        private bool _IsCompressionEnabled = true;

        [DisplayName("单文件大小限制 (MB)")]
        public int MaxFileSizeInMB { get => _MaxFileSizeInMB; set { _MaxFileSizeInMB = value; OnPropertyChanged(); } }
        private int _MaxFileSizeInMB = 10; // 默认 10MB
    }
}