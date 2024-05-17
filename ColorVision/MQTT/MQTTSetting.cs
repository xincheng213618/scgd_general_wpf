using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Settings;
using ColorVision.UI;
using System.Collections.ObjectModel;

namespace ColorVision.MQTT
{
    public delegate void UseMQTTHandler(bool IsUseMQTT);

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
