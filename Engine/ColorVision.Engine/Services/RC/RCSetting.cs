using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Engine.Services.RC
{
    public class RCSettingProvider : IConfigSettingProvider, IStatusBarProviderUpdatable
    {
        public event EventHandler StatusBarItemsChanged;

        public RCSettingProvider()
        {
            MqttRCService.GetInstance().RCServiceConnectChanged += (s, e) =>
                StatusBarItemsChanged?.Invoke(this, EventArgs.Empty);
        }

        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata>
            {
                new ConfigSettingMetadata
                {
                    Group ="Engine",
                    BindingName = nameof(RCSetting.IsUseRCService),
                    Source = RCSetting.Instance
                },
            };
        }

        public IEnumerable<StatusBarMeta> GetStatusBarIconMetadata()
        {
            bool isConnected = MqttRCService.GetInstance().IsConnect;
            RelayCommand relayCommand = new RelayCommand(a => new RCServiceConnect() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
            return new List<StatusBarMeta>
            {
                new StatusBarMeta()
                {
                    Id = "RC",
                    Name = "RC",
                    Description = isConnected ? "RC Service Connected" : "RC Service Disconnected",
                    Order = 999,
                    Type = StatusBarType.Icon,
                    IconResourceKey = isConnected ? "DrawingImageRCService" : "DrawingImageRCServiceRed",
                    Source = RCSetting.Instance,
                    Command = relayCommand
                }
            };
        }
    }


    public class RCSetting : ViewModelBase, IConfigSecure
    {
        public static RCSetting Instance => ConfigService.Instance.GetRequiredService<RCSetting>();

        public static MqttRCService MQTTRCService => MqttRCService.GetInstance();

        public RCServiceConfig Config { get; set; } = new RCServiceConfig();
        public bool IsUseRCService { get => _IsUseRCService; set { _IsUseRCService = value; OnPropertyChanged(); } }
        private bool _IsUseRCService = true;

        public ObservableCollection<RCServiceConfig> RCServiceConfigs { get; set; } = new ObservableCollection<RCServiceConfig>();
        public const string ConfigAESKey = "ColorVision";
        public const string ConfigAESVector = "ColorVision";

        public void Decrypt()
        {
            ///如果是初始值直接跳过
            if (Config.AppSecret != "123456")
            {
                Config.AppSecret = Cryptography.AESDecrypt(Config.AppSecret, ConfigAESKey, ConfigAESVector);
            }
        }

        public void Encryption()
        {
            Config.AppSecret = Cryptography.AESEncrypt(Config.AppSecret, ConfigAESKey, ConfigAESVector);
        }
    }
}
