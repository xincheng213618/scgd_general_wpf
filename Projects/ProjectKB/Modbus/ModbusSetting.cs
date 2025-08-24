using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using System.Collections.ObjectModel;

namespace ProjectKB.Modbus
{
    public class ModbusSetting : ViewModelBase, IConfigSecure
    {
        public static ModbusSetting Instance => ConfigService.Instance.GetRequiredService<ModbusSetting>();

        public event EventHandler<bool> MobusEnableChanged;
        public bool MobusEnable { get => _MobusEnable; set { _MobusEnable = value; OnPropertyChanged(); MobusEnableChanged?.Invoke(this, _MobusEnable); } }
        private bool _MobusEnable = true;

        /// <summary>
        /// MySql配置
        /// </summary>
        public ModbusConfig ModbusConfig { get; set; } = new ModbusConfig();
        public ObservableCollection<ModbusConfig> ModbusConfigs { get; set; } = new ObservableCollection<ModbusConfig>();


        public const string ConfigAESKey = "ColorVision";
        public const string ConfigAESVector = "ColorVision";

        public void Encryption()
        {
            ModbusConfig.UserPwd = Cryptography.AESEncrypt(ModbusConfig.UserPwd, ConfigAESKey, ConfigAESVector);
            foreach (var item in ModbusConfigs)
            {
                item.UserPwd = Cryptography.AESEncrypt(item.UserPwd, ConfigAESKey, ConfigAESVector);
            }
        }

        public void Decrypt()
        {
            ModbusConfig.UserPwd = Cryptography.AESDecrypt(ModbusConfig.UserPwd, ConfigAESKey, ConfigAESVector);
            foreach (var item in ModbusConfigs)
            {
                item.UserPwd = Cryptography.AESDecrypt(item.UserPwd, ConfigAESKey, ConfigAESVector);
            }
        }
    }
}
