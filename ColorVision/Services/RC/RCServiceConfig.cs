using ColorVision.MVVM;

namespace ColorVision.RC
{
    public class RCServiceConfig : ViewModelBase
    {
        /// <summary>
        /// 连接名称
        /// </summary>
        public string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name;
        /// <summary>
        /// AppId App ID
        /// </summary>
        public string AppId { get => _AppId; set { _AppId = value; NotifyPropertyChanged(); } }
        private string _AppId;

        /// <summary>
        /// AppSecret App 密钥
        /// </summary>
        public string AppSecret { get => _AppSecret; set { _AppSecret = value; NotifyPropertyChanged(); } }
        private string _AppSecret;

        /// <summary>
        /// 注册中心名称
        /// </summary>
        public string RCName { get => _RCName; set { _RCName = value; NotifyPropertyChanged(); } }
        private string _RCName;

        public RCServiceConfig()
        {
            _AppId = "app1";
            _AppSecret = "123456";
            _RCName = "RC_local";
        }
    }
}
