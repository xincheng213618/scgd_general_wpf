using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.MySql;
using ColorVision.Settings;
using ColorVision.UI;
using ColorVision.UI.Configs;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ColorVision.MQTT
{
    public delegate void UseMQTTHandler(bool IsUseMQTT);

    public class MQTTSettingProvider : IConfigSettingProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata> {
                            new ConfigSettingMetadata
                            {
                                Name = "启用MQTT",
                                Description = "启用MQTT",
                                Type = ConfigSettingType.Bool,
                                BindingName = nameof(MQTTSetting.IsUseMQTT),
                                Source = MQTTSetting.Instance
                            }
            };
        }
    }

    public class MQTTSetting : ViewModelBase ,IConfigSecure
    {
        public static MQTTSetting Instance { get; set; } = ConfigHandler.GetInstance().GetRequiredService<MQTTSetting>();

        public static MQTTControl MQTTControl => MQTTControl.GetInstance();
        public static bool IsConnect => MQTTControl.IsConnect;

        public MQTTSetting()
        {

        }

        /// <summary>
        /// MQTT
        /// </summary>
        public bool IsUseMQTT { get => _IsUseMQTT; set { _IsUseMQTT = value; NotifyPropertyChanged(); } }
        private bool _IsUseMQTT = true;


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

        public void Encryption()
        {
            MQTTConfig.UserPwd = Cryptography.AESEncrypt(MQTTConfig.UserPwd, GlobalConst.ConfigAESKey, GlobalConst.ConfigAESVector);
        }

        public void Decrypt()
        {
            MQTTConfig.UserPwd = Cryptography.AESDecrypt(MQTTConfig.UserPwd, GlobalConst.ConfigAESKey, GlobalConst.ConfigAESVector);
        }
    }
}
