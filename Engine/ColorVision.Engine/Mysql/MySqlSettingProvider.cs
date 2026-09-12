using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Engine
{
    /// <summary>
    /// 主程序 MySQL 状态及默认连接配置入口。
    /// </summary>
    public class MySqlSettingProvider : IStatusBarProviderUpdatable
    {
        public event EventHandler? StatusBarItemsChanged;

        public MySqlSettingProvider()
        {
            MySqlControl.GetInstance().MySqlConnectChanged += (s, e) =>
                StatusBarItemsChanged?.Invoke(this, EventArgs.Empty);
        }

        public IEnumerable<StatusBarMeta> GetStatusBarIconMetadata()
        {
            bool isConnected = MySqlControl.GetInstance().IsConnect;
            RelayCommand relayCommand = new(a => new MySqlConnect
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.ShowDialog());

            return new List<StatusBarMeta>
            {
                new()
                {
                    Id = "MySQL",
                    Name = ColorVision.Database.Properties.Resources.EnableDatabase,
                    Description = isConnected ? "MySQL Connected" : "MySQL Disconnected",
                    Order = 999,
                    Type = StatusBarType.Icon,
                    IconResourceKey = isConnected ? "DrawingImageMysql" : "DrawingImageMysqlRed",
                    Source = MySqlSetting.Instance,
                    Command = relayCommand
                }
            };
        }
    }
}
