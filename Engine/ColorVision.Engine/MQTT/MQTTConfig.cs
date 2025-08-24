using ColorVision.Common.MVVM;

namespace ColorVision.Engine.MQTT
{

    /// <summary>
    /// MQTT配置
    /// </summary>
    public class MQTTConfig : ViewModelBase
    {
        /// <summary>
        /// 连接名称
        /// </summary>
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name;


        /// <summary>
        /// IP地址
        /// </summary>
        public string Host { get => _Host; set { _Host = value; OnPropertyChanged(); } }
        private string _Host = "127.0.0.1";

        /// <summary>
        /// 端口地址
        /// </summary>
        public int Port
        {
            get => _Port; set
            {
                _Port = value <= 0 ? 0 : value >= 65535 ? 65535 : value;
                OnPropertyChanged();
            }
        }
        private int _Port = 1883;

        /// <summary>
        /// 账号
        /// </summary>
        public string UserName { get => _UserName; set { _UserName = value; OnPropertyChanged(); } }
        private string _UserName = string.Empty;

        /// <summary>
        /// 密码
        /// </summary>
        public string UserPwd { get => _UserPwd; set { _UserPwd = value; OnPropertyChanged(); } }
        private string _UserPwd = string.Empty;


        public override string ToString()
        {
            return $"Host={Host};Port={Port};UserName={UserName};UserPwd={UserPwd}";
        }

    }
}
