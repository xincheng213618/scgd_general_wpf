using ColorVision.Common.MVVM;
using ColorVision.Rbac.Dtos;
using ColorVision.UI;

namespace ColorVision.Rbac
{

    public class RbacManagerConfig : ViewModelBase, IConfig
    {
        public static RbacManagerConfig Instance => ConfigService.Instance.GetRequiredService<RbacManagerConfig>();

        public LoginResultDto LoginResult { get => _LoginResult; set { _LoginResult = value; OnPropertyChanged(); } }
        private LoginResultDto _LoginResult = new LoginResultDto();

        /// <summary>
        /// 当前会话Token
        /// </summary>
        public string SessionToken { get => _SessionToken; set { _SessionToken = value; OnPropertyChanged(); } }
        private string _SessionToken = string.Empty;

        /// <summary>
        /// 是否记住登录状态
        /// </summary>
        public bool RememberMe { get => _RememberMe; set { _RememberMe = value; OnPropertyChanged(); } }
        private bool _RememberMe = false;

        /// <summary>
        /// 保存的用户名（用于自动登录）
        /// </summary>
        public string SavedUsername { get => _SavedUsername; set { _SavedUsername = value; OnPropertyChanged(); } }
        private string _SavedUsername = string.Empty;

        /// <summary>
        /// 保存的密码Hash（用于自动登录）
        /// </summary>
        public string SavedPasswordHash { get => _SavedPasswordHash; set { _SavedPasswordHash = value; OnPropertyChanged(); } }
        private string _SavedPasswordHash = string.Empty;
    }



}

