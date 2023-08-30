using ColorVision.MVVM;
using System;

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


        public string Email { get => _Email; set { _Email = value; NotifyPropertyChanged(); } }
        private string _Email = string.Empty;

        public string Phone { get => _Phone; set { _Phone = value; NotifyPropertyChanged(); } } 
        private string _Phone = string.Empty;

        public string Address { get => _Address; set { _Address = value; NotifyPropertyChanged(); } }
        private string _Address = string.Empty;

        public string Company { get => _Company; set { _Company = value; NotifyPropertyChanged(); } }
        private string _Company = string.Empty;

        public string Department { get => _Department; set { _Department = value; NotifyPropertyChanged(); } }
        private string _Department = string.Empty;

        public string Position { get => _Position; set { _Position = value; NotifyPropertyChanged(); } }
        private string _Position = string.Empty;

        public string Remark { get => _Remark; set { _Remark = value; NotifyPropertyChanged(); } }
        private string _Remark = string.Empty;

        public string UserImage { get => _UserImage; set { _UserImage = value; NotifyPropertyChanged(); } }
        private string _UserImage = "Config\\user.jpg";


    
    
    }

    public enum PerMissionMode
    {
        Administrator,
        User
    }
}

