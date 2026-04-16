using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace ColorVision.Database
{
    public class MySqlSettingProvider : IConfigSettingProvider, IStatusBarProviderUpdatable
    {
        public event EventHandler StatusBarItemsChanged;

        public MySqlSettingProvider()
        {
            MySqlControl.GetInstance().MySqlConnectChanged += (s, e) =>
                StatusBarItemsChanged?.Invoke(this, EventArgs.Empty);
        }

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
        public const string RootProfileName = "RootPath";
        public const string BusinessProfileName = "CVPath";

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

        public MySqlConfig? FindProfile(string profileName)
        {
            return MySqlConfigs.FirstOrDefault(a => a.Name == profileName);
        }

        public MySqlConfig GetOrCreateProfile(string profileName)
        {
            var profile = FindProfile(profileName);
            if (profile != null)
            {
                return profile;
            }

            profile = new MySqlConfig { Name = profileName };
            MySqlConfigs.Add(profile);
            return profile;
        }

        public void ApplyBusinessConfig(string host, int port, string userName, string userPwd, string database)
        {
            MySqlConfig.Name = BusinessProfileName;
            MySqlConfig.Host = host;
            MySqlConfig.Port = port;
            MySqlConfig.UserName = userName;
            MySqlConfig.UserPwd = userPwd;
            MySqlConfig.Database = database;
        }

        public void ApplyRootConfig(string host, int port, string rootPwd, string database)
        {
            var rootProfile = GetOrCreateProfile(RootProfileName);
            rootProfile.Host = host;
            rootProfile.Port = port;
            rootProfile.UserName = "root";
            rootProfile.UserPwd = rootPwd;
            rootProfile.Database = database;
        }


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
