using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;

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

        public const string ConfigAESKey = "ColorVision";
        public const string ConfigAESVector = "ColorVision";

        public void Encryption()
        {
            MySqlConfig.UserPwd = Cryptography.AESEncrypt(MySqlConfig.UserPwd, ConfigAESKey, ConfigAESVector);
        }

        public void Decrypt()
        {
            MySqlConfig.UserPwd = Cryptography.AESDecrypt(MySqlConfig.UserPwd, ConfigAESKey, ConfigAESVector);
        }
    }
}
