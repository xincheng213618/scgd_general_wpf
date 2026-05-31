using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Database
{
    public class MySqlSettingProvider : IStatusBarProviderUpdatable
    {
        public event EventHandler StatusBarItemsChanged;

        public MySqlSettingProvider()
        {
            MySqlControl.GetInstance().MySqlConnectChanged += (s, e) =>
                StatusBarItemsChanged?.Invoke(this, EventArgs.Empty);
        }

        public IEnumerable<StatusBarMeta> GetStatusBarIconMetadata()
        {
            bool isConnected = MySqlControl.GetInstance().IsConnect;
            RelayCommand relayCommand = new RelayCommand(a => new MySqlConnect() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
            return new List<StatusBarMeta>
            {
                new StatusBarMeta()
                {
                    Id = "MySQL",
                    Name = Properties.Resources.EnableDatabase,
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

    public class MySqlSetting : ViewModelBase , IConfigSecure
    {

        public static MySqlSetting Instance  => ConfigService.Instance.GetRequiredService<MySqlSetting>();

        public static MySqlControl MySqlControl => MySqlControl.GetInstance();
        public static bool IsConnect => MySqlControl.IsConnect;

        /// <summary>
        /// MySql配置
        /// </summary>
        public MySqlConfig MySqlConfig { get; set; } = new MySqlConfig();
        public ObservableCollection<MySqlConfig> MySqlConfigs { get; set; } = new ObservableCollection<MySqlConfig>();

        public const string ConfigAESKey = "ColorVision";
        public const string ConfigAESVector = "ColorVision";

        public void Encryption()
        {
            MySqlConfig.UserPwd = Cryptography.AESEncrypt(MySqlConfig.UserPwd, ConfigAESKey, ConfigAESVector);
            foreach (var item in MySqlConfigs)
            {
                item.UserPwd = Cryptography.AESEncrypt(item.UserPwd, ConfigAESKey, ConfigAESVector);
            }
        }

        public void Decrypt()
        {
            MySqlConfig.UserPwd = Cryptography.AESDecrypt(MySqlConfig.UserPwd, ConfigAESKey, ConfigAESVector);
            foreach (var item in MySqlConfigs)
            {
                item.UserPwd = Cryptography.AESDecrypt(item.UserPwd, ConfigAESKey, ConfigAESVector);
            }
        }
    }
}
