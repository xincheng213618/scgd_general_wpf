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
    }



}

