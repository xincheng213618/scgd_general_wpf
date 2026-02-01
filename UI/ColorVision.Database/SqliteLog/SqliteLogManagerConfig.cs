using ColorVision.Common.MVVM;
using ColorVision.UI;
using System;
using System.ComponentModel;

namespace ColorVision.Database.SqliteLog
{
    /// <summary>
    /// 管理 SqliteLogManager 的启用和关闭。
    /// </summary>
    public class SqliteLogManagerConfig:ViewModelBase,IConfig
    {
        public event EventHandler<bool> SqliteLogEnabledChanged;
        [DisplayName("LogDatabase")]
        public bool IsEnabled { get => _IsEnabled; set { _IsEnabled = value; OnPropertyChanged();SqliteLogEnabledChanged?.Invoke(this, value); } }
        private bool _IsEnabled = true;
    }
}
