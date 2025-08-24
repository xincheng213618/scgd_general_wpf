using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Authorizations;

namespace ColorVision.Engine.Rbac
{

    public class UserConfig : ViewModelBase, IConfig
    {
        public static UserConfig Instance => ConfigService.Instance.GetRequiredService<UserConfig>();

        public string Account { get => _Account; set { _Account = value; OnPropertyChanged(); } }
        private string _Account = "admin";

        public string UserPwd { get => _UserPwd; set { _UserPwd = value; OnPropertyChanged(); } }
        private string _UserPwd = "admin";

        public PermissionMode PermissionMode { get => _PermissionMode; set { _PermissionMode = value; OnPropertyChanged(); } }
        private PermissionMode _PermissionMode = PermissionMode.Administrator;

        public string UserName { get => _UserName; set { _UserName = value; OnPropertyChanged(); } }
        private string _UserName = string.Empty;

        /// <summary>
        /// 租户ID
        /// </summary>
        public int TenantId { get => _TenantId; set { _TenantId = value; OnPropertyChanged(); } }
        private int _TenantId;
    }



}

