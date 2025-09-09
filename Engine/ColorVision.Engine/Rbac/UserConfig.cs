using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Authorizations;

namespace ColorVision.Engine.Rbac
{

    public class UserConfig : ViewModelBase, IConfig
    {
        public static UserConfig Instance => ConfigService.Instance.GetRequiredService<UserConfig>();

        public UserLoginResult UserLoginResult { get; set; } = new UserLoginResult();
        /// <summary>
        /// 租户ID
        /// </summary>
        public int TenantId { get => _TenantId; set { _TenantId = value; OnPropertyChanged(); } }
        private int _TenantId;
    }



}

