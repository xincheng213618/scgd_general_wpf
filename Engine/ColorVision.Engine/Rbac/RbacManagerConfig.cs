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
        /// <summary>
        /// 租户ID
        /// </summary>
        public int TenantId { get => _TenantId; set { _TenantId = value; OnPropertyChanged(); } }
        private int _TenantId;
    }



}

