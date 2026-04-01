using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Engine.MQTT
{
    public class MQTTSettingProvider : IConfigSettingProvider, IStatusBarProviderUpdatable
    {
        public event EventHandler StatusBarItemsChanged;

        public MQTTSettingProvider()
        {
            MQTTControl.GetInstance().MQTTConnectChanged += (s, e) =>
                StatusBarItemsChanged?.Invoke(this, EventArgs.Empty);
        }

        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata>
            {
                new ConfigSettingMetadata
                {
                    Group ="Engine",
                    BindingName = nameof(MQTTSetting.IsUseMQTT),
                    Source = MQTTSetting.Instance
                },
                new ConfigSettingMetadata
                {
                    BindingName = nameof(MQTTSetting.DefaultTimeout),
                    Source = MQTTSetting.Instance
                }
            };
        }
        public IEnumerable<StatusBarMeta> GetStatusBarIconMetadata()
        {
            bool isConnected = MQTTControl.GetInstance().IsConnect;
            RelayCommand relayCommand = new RelayCommand(a => new MQTTConnect() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
            return new List<StatusBarMeta>
            {
                new StatusBarMeta()
                {
                    Id = "MQTT",
                    Name = "MQTT",
                    Description = isConnected ? "MQTT Connected" : "MQTT Disconnected",
                    Order = 999,
                    Type = StatusBarType.Icon,
                    IconResourceKey = isConnected ? "DrawingImageMQTT" : "DrawingImageMQTTRed",
                    Source = MQTTSetting.Instance,
                    Command = relayCommand
                }
            };
        }

    }



    public class MQTTSetting : ViewModelBase ,IConfigSecure
    {
        public static MQTTSetting Instance { get; set; } = ConfigService.Instance.GetRequiredService<MQTTSetting>();

        public static MQTTControl MQTTControl => MQTTControl.GetInstance();
        public MQTTSetting()
        {

        }

        /// <summary>
        /// MQTT
        /// </summary>
        public bool IsUseMQTT { get => _IsUseMQTT; set { _IsUseMQTT = value; OnPropertyChanged(); } }
        private bool _IsUseMQTT = true;

        public double DefaultTimeout { get => _DefaultTimeout; set { _DefaultTimeout = value; OnPropertyChanged(); } }
        private double _DefaultTimeout = 30000;


        public MQTTConfig MQTTConfig { get; set; } = new MQTTConfig();

        public ObservableCollection<MQTTConfig> MQTTConfigs { get; set; } = new ObservableCollection<MQTTConfig>();

        /// <summary>
        /// 是否显示心跳
        /// </summary>
        public bool IsShieldHeartbeat { get => _IsShieldHeartbeat; set { _IsShieldHeartbeat = value; OnPropertyChanged(); } }
        private bool _IsShieldHeartbeat;

        /// <summary>
        /// 只显示选中的
        /// </summary>
        public bool ShowSelect { get => _ShowSelect; set { _ShowSelect = value; OnPropertyChanged(); } }
        private bool _ShowSelect;
        public const string ConfigAESKey = "ColorVision";
        public const string ConfigAESVector = "ColorVision";

        public void Encryption()
        {
            MQTTConfig.UserPwd = Cryptography.AESEncrypt(MQTTConfig.UserPwd, ConfigAESKey,ConfigAESVector);
        }

        public void Decrypt()
        {
            MQTTConfig.UserPwd = Cryptography.AESDecrypt(MQTTConfig.UserPwd, ConfigAESKey, ConfigAESVector);
        }
    }
}
