using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Authorizations;

namespace ColorVision.Engine.Rbac
{

    public class UserConfig : ViewModelBase, IConfig
    {
        public static UserConfig Instance => ConfigService.Instance.GetRequiredService<UserConfig>();

        public string Account { get => _Account; set { _Account = value; NotifyPropertyChanged(); } }
        private string _Account = "admin";

        public string UserPwd { get => _UserPwd; set { _UserPwd = value; NotifyPropertyChanged(); } }
        private string _UserPwd = "admin";

        public PermissionMode PermissionMode { get => _PermissionMode; set { _PermissionMode = value; NotifyPropertyChanged(); } }
        private PermissionMode _PermissionMode = PermissionMode.Administrator;

        public string UserName { get => _UserName; set { _UserName = value; NotifyPropertyChanged(); } }
        private string _UserName = string.Empty;

        /// <summary>
        /// 租户ID
        /// </summary>
        public int TenantId { get => _TenantId; set { _TenantId = value; NotifyPropertyChanged(); } }
        private int _TenantId;
    }



}

