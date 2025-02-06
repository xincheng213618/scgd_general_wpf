using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using ColorVision.UI.Configs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Engine.MySql
{
    public delegate void UseMySqlHandler(bool IsUseMySql);

    public class MySqlSettingProvider : IConfigSettingProvider,IStatusBarProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata> {
                            new ConfigSettingMetadata
                            {
                                Name = Properties.Resources.EnableDatabase,
                                Description = Properties.Resources.EnableDatabase,
                                Order =1,
                                Type = ConfigSettingType.Bool,
                                BindingName = nameof(MySqlSetting.IsUseMySql),
                                Source = MySqlSetting.Instance
                            },       
                          new ConfigSettingMetadata
                            {
                                Name = "数据库超时重连检测",
                                Description = "数据库超时重连检测",
                                Order =1,
                                Type = ConfigSettingType.Text,
                                BindingName = nameof(MySqlSetting.ReConnectTime),
                                Source = MySqlSetting.Instance
                            }
            };
        }
        public IEnumerable<StatusBarMeta> GetStatusBarIconMetadata()
        {
            Action action = new Action(() =>
            {
                new MySqlConnect() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            });

            return new List<StatusBarMeta>
            {
                new StatusBarMeta()
                {
                    Name = Properties.Resources.EnableDatabase,
                    Description = Properties.Resources.EnableDatabase,
                    Order =0,
                    BindingName = "MySqlControl.IsConnect",
                    VisibilityBindingName = nameof(MySqlSetting.IsUseMySql),
                    ButtonStyleName ="ButtonDrawingImageMysql",
                    Source = MySqlSetting.Instance,
                    Action =action
                }
            };
        }

    }



    public class MySqlSetting : ViewModelBase , IConfigSecure
    {
        public static MySqlSetting Instance  => ConfigService.Instance.GetRequiredService<MySqlSetting>();

        public static MySqlControl MySqlControl => MySqlControl.GetInstance();
        public static bool IsConnect => MySqlControl.IsConnect;

        public  int ReConnectTime { get => _ReConnectTime; set { _ReConnectTime = value; NotifyPropertyChanged(); ReConnectTimeChanged?.Invoke(this, new EventArgs()); } }
        private int _ReConnectTime = 3600000;

        public event  EventHandler ReConnectTimeChanged;


        public bool IsUseMySql { get => _IsUseMySql; set { _IsUseMySql = value; NotifyPropertyChanged(); UseMySqlChanged?.Invoke(value); } }
        private bool _IsUseMySql = true;

        public event UseMySqlHandler UseMySqlChanged;


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
