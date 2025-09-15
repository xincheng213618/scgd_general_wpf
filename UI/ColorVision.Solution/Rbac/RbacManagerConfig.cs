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
    }



}

