using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using System.Collections.ObjectModel;

namespace ColorVision.Database
{
    public class MySqlSetting : ViewModelBase , IConfigSecure
    {

        public static MySqlSetting Instance  => ConfigService.Instance.GetRequiredService<MySqlSetting>();

        public static MySqlControl MySqlControl => MySqlControl.GetInstance();
        public static bool IsConnect => MySqlControl.IsConnect;

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
