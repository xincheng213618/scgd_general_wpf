using ColorVision.Common.MVVM;
using ColorVision.UI;

namespace ColorVision.Engine.Services.RC
{

    public class RCServiceConfig : ViewModelBase,IConfig
    {
        /// <summary>
        /// 连接名称
        /// </summary>
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name;

        /// <summary>
        /// 注册中心名称
        /// </summary>
        public string RCName { get => _RCName; set { _RCName = value; OnPropertyChanged(); } }
        private string _RCName = "RC_local";

        /// <summary>
        /// AppId App Id
        /// </summary>
        public string AppId { get => _AppId; set { _AppId = value; OnPropertyChanged(); } }
        private string _AppId = "app1";

        /// <summary>
        /// AppSecret App 密钥
        /// </summary>
        public string AppSecret { get => _AppSecret; set { _AppSecret = value; OnPropertyChanged(); } }
        private string _AppSecret = "123456";
    }
}
