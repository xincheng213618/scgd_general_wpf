using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Settings;
using ColorVision.UI;
using ColorVision.UI.Configs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.MySql
{
    public delegate void UseMySqlHandler(bool IsUseMySql);

    public class MySqlSettingProvider : IConfigSettingProvider,IStatusBarIconProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata> {
                            new ConfigSettingMetadata
                            {
                                Name = Properties.Resource.EnableDatabase,
                                Description = Properties.Resource.EnableDatabase,
                                Order =1,
                                Type = ConfigSettingType.Bool,
                                BindingName = nameof(MySqlSetting.IsUseMySql),
                                Source = MySqlSetting.Instance
                            }
            };
        }
        public IEnumerable<StatusBarIconMetadata> GetStatusBarIconMetadata()
        {
            Action action = new Action(() =>
            {
                new MySqlConnect() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            });

            return new List<StatusBarIconMetadata>
            {
                new StatusBarIconMetadata()
                {
                    Name = Properties.Resource.EnableDatabase,
                    Description = Properties.Resource.EnableDatabase,
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
        public static MySqlSetting Instance  => ConfigHandler.GetInstance().GetRequiredService<MySqlSetting>();

        public static MySqlControl MySqlControl => MySqlControl.GetInstance();
        public static bool IsConnect => MySqlControl.IsConnect;


        public bool IsUseMySql { get => _IsUseMySql; set { _IsUseMySql = value; NotifyPropertyChanged(); UseMySqlChanged?.Invoke(value); } }
        private bool _IsUseMySql = true;

        public event UseMySqlHandler UseMySqlChanged;


        /// <summary>
        /// MySQL配置
        /// </summary>
        public MySqlConfig MySqlConfig { get; set; } = new MySqlConfig();
        public ObservableCollection<MySqlConfig> MySqlConfigs { get; set; } = new ObservableCollection<MySqlConfig>();
        
        public void Encryption()
        {
            MySqlConfig.UserPwd = Cryptography.AESEncrypt(MySqlConfig.UserPwd, GlobalConst.ConfigAESKey, GlobalConst.ConfigAESVector);
            foreach (var item in MySqlConfigs)
            {
                item.UserPwd = Cryptography.AESEncrypt(item.UserPwd, GlobalConst.ConfigAESKey, GlobalConst.ConfigAESVector);
            }
        }

        public void Decrypt()
        {
            MySqlConfig.UserPwd = Cryptography.AESDecrypt(MySqlConfig.UserPwd, GlobalConst.ConfigAESKey, GlobalConst.ConfigAESVector);
            foreach (var item in MySqlConfigs)
            {
                item.UserPwd = Cryptography.AESDecrypt(item.UserPwd, GlobalConst.ConfigAESKey, GlobalConst.ConfigAESVector);
            }
        }
    }
}
