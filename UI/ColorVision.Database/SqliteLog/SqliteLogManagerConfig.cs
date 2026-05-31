using ColorVision.Common.MVVM;
using ColorVision.UI;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ColorVision.Database.SqliteLog
{
    public class SqliteLogManagerConfig : ViewModelBase, IConfig
    {
        public event EventHandler<bool> SqliteLogEnabledChanged;

        [ConfigSetting(Order = 1, Section = ConfigSettingConstants.SectionBasic, Description = "DB_EnableLoggingDesc")]
        [Display(Name = "DB_EnableLogging", ResourceType = typeof(Properties.Resources))]
        public bool IsEnabled { get => _IsEnabled; set { _IsEnabled = value; OnPropertyChanged(); SqliteLogEnabledChanged?.Invoke(this, value); } }
        private bool _IsEnabled;

        [ConfigSetting(Order = 2, Section = ConfigSettingConstants.SectionFileArchive)]
        [Display(Name = "DB_EnableCompress", Description = "DB_EnableCompressDesc", ResourceType = typeof(Properties.Resources))]
        public bool IsCompressionEnabled { get => _IsCompressionEnabled; set { _IsCompressionEnabled = value; OnPropertyChanged(); } }
        private bool _IsCompressionEnabled = true;

        [ConfigSetting(Order = 3, Section = ConfigSettingConstants.SectionFileArchive, Description = "DB_FileSizeLimitDesc")]
        [Display(Name = "DB_FileSizeLimit", ResourceType = typeof(Properties.Resources))]
        public int MaxFileSizeInMB { get => _MaxFileSizeInMB; set { _MaxFileSizeInMB = Math.Max(10, value); OnPropertyChanged(); } }
        private int _MaxFileSizeInMB = 1024; // 默认 1024MB

        [ConfigSetting(Order = 4, Section = ConfigSettingConstants.SectionFileArchive)]
        [Display(Name = "DB_ArchiveCount", Description = "DB_ArchiveCountDesc", ResourceType = typeof(Properties.Resources))]
        public int MaxArchiveFiles { get => _MaxArchiveFiles; set { _MaxArchiveFiles = Math.Max(1, value); OnPropertyChanged(); } }
        private int _MaxArchiveFiles = 30;
    }
}