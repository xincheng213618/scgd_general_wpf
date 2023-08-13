using ColorVision.MVVM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.SettingUp
{
    public class UserConfig:ViewModelBase
    {
        /// <summary>
        /// 账号
        /// </summary>
        public string UserName { get => _UserName; set { _UserName = value; NotifyPropertyChanged(); } }
        private string _UserName = string.Empty;

        public Guid UserID { get => _UserID; set { _UserID = value; NotifyPropertyChanged(); } }
        private Guid _UserID = System.Guid.NewGuid();

        /// <summary>
        /// 密码
        /// </summary>
        public string UserPwd { get => _UserPwd; set { _UserPwd = value; NotifyPropertyChanged(); } }
        private string _UserPwd = string.Empty;


        public PerMissionMode PerMissionMode { get => _PerMissionMode; set { _PerMissionMode = value; NotifyPropertyChanged(); } }
        private PerMissionMode _PerMissionMode;

        /// <summary>
        /// 租户ID
        /// </summary>
        public int TenantId { get=>_TenantId; set { _TenantId = value; NotifyPropertyChanged(); } }
        private int _TenantId;
    }

    public enum PerMissionMode
    {
        Administrator,
        User
    }
}

