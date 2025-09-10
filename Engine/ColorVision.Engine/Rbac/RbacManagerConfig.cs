using ColorVision.Common.MVVM;
using ColorVision.Engine.Rbac.Dtos;
using ColorVision.UI;
using ColorVision.UI.Authorizations;

namespace ColorVision.Engine.Rbac
{

    public class RbacManagerConfig : ViewModelBase, IConfig
    {
        public static RbacManagerConfig Instance => ConfigService.Instance.GetRequiredService<RbacManagerConfig>();

        public LoginResultDto LoginResult { get; set; } = new LoginResultDto();
    }



}

