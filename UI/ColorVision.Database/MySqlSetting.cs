using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Database
{
    public class MySqlSettingProvider : IConfigSettingProvider,IStatusBarProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata> {
                            new ConfigSettingMetadata
                            {
                                Group ="Engine",
                                Order =1,
                                BindingName = nameof(MySqlSetting.IsUseMySql),
                                Source = MySqlSetting.Instance
                            },
            };
        }
        public IEnumerable<StatusBarMeta> GetStatusBarIconMetadata()
        {
            RelayCommand relayCommand = new RelayCommand(a => new MySqlConnect() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
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
                    Command =relayCommand
                }
            };
        }

    }



    public class MySqlSetting : ViewModelBase , IConfigSecure
    {
        public static MySqlSetting Instance  => ConfigService.Instance.GetRequiredService<MySqlSetting>();

        public static MySqlControl MySqlControl => MySqlControl.GetInstance();
        public static bool IsConnect => MySqlControl.IsConnect;

        public bool IsUseMySql { get => _IsUseMySql; set { _IsUseMySql = value; OnPropertyChanged(); UseMySqlChanged?.Invoke(this,value); } }
        private bool _IsUseMySql = true;

        public event EventHandler<bool> UseMySqlChanged;


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
