using ColorVision.Common.MVVM;

namespace ColorVision.Database
{
    /// <summary>
    /// MySql配置
    /// </summary>
    public class MySqlConfig : ViewModelBase
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
        private int _Port = 3306;

        /// <summary>
        /// 账号
        /// </summary>
        public string UserName { get => _UserName; set { _UserName = value; OnPropertyChanged(); } }
        private string _UserName = "root";

        /// <summary>
        /// 密码
        /// </summary>
        public string UserPwd { get => _UserPwd; set { _UserPwd = value; OnPropertyChanged(); } }
        private string _UserPwd = string.Empty;

        /// <summary>
        /// 数据库
        /// </summary>
        public string Database { get => _Database; set { _Database = value; OnPropertyChanged(); } }
        private string _Database = string.Empty;

    }
}
