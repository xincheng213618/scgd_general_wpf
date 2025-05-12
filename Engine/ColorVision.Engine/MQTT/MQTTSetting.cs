using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Engine.MQTT
{
    public delegate void UseMQTTHandler(bool IsUseMQTT);

    public class MQTTSettingProvider : IConfigSettingProvider, IStatusBarProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata>
            {
                new ConfigSettingMetadata
                {
                    Name = "启用MQTT",
                    Description = "启用MQTT",
                    Order =1,
                    Type = ConfigSettingType.Bool,
                    Group ="Engine",
                    BindingName = nameof(MQTTSetting.IsUseMQTT),
                    Source = MQTTSetting.Instance
                },
                new ConfigSettingMetadata
                {
                    Name = "MQTT超时",
                    Description = "超时时间",
                    Group ="Engine",
                    Order =1,
                    Type = ConfigSettingType.Text,
                    BindingName = nameof(MQTTSetting.DefaultTimeout),
                    Source = MQTTSetting.Instance
                }
            };
        }
        public IEnumerable<StatusBarMeta> GetStatusBarIconMetadata()
        {
            Action action = new Action(() =>
            {
                new MQTTConnect() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            });

            return new List<StatusBarMeta>
            {
                new StatusBarMeta()
                {
                    Name = "启用MQTT",
                    Description = "启用MQTT",
                    Order =2,
                    BindingName ="MQTTControl.IsConnect",
                    VisibilityBindingName = nameof(MQTTSetting.IsUseMQTT),
                    ButtonStyleName ="ButtonDrawingImageMQTT",
                    Source = MQTTSetting.Instance,
                    Action =action
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
        public bool IsUseMQTT { get => _IsUseMQTT; set { _IsUseMQTT = value; NotifyPropertyChanged(); } }
        private bool _IsUseMQTT = true;

        public double DefaultTimeout { get => _DefaultTimeout; set { _DefaultTimeout = value; NotifyPropertyChanged(); } }
        private double _DefaultTimeout = 30000;


        public MQTTConfig MQTTConfig { get; set; } = new MQTTConfig();

        public ObservableCollection<MQTTConfig> MQTTConfigs { get; set; } = new ObservableCollection<MQTTConfig>();

        /// <summary>
        /// 是否显示心跳
        /// </summary>
        public bool IsShieldHeartbeat { get => _IsShieldHeartbeat; set { _IsShieldHeartbeat = value; NotifyPropertyChanged(); } }
        private bool _IsShieldHeartbeat;

        /// <summary>
        /// 只显示选中的
        /// </summary>
        public bool ShowSelect { get => _ShowSelect; set { _ShowSelect = value; NotifyPropertyChanged(); } }
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
