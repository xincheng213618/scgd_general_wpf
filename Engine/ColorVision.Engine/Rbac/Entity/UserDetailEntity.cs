using ColorVision.Database;
using ColorVision.UI.Authorizations;
using log4net;
using SqlSugar;
using System;

namespace ColorVision.Engine.Rbac
{


    [SugarTable("sys_user_detail")]
    public class UserDetailEntity : ViewEntity 
    {
        [SugarColumn(ColumnName ="user_id")]
        public int UserId { get => _UserId; set { _UserId = value; OnPropertyChanged(); } }
        private int _UserId;

        [SugarColumn(ColumnName ="permission_mode")]
        public PermissionMode PermissionMode { get => _PermissionMode; set { _PermissionMode = value; OnPropertyChanged(); } }
        private PermissionMode _PermissionMode = PermissionMode.SuperAdministrator;

        [SugarColumn(ColumnName ="email")]
        public string Email { get => _Email; set { _Email = value; OnPropertyChanged(); } }
        private string _Email = string.Empty;

        [SugarColumn(ColumnName ="phone")]
        public string Phone { get => _Phone; set { _Phone = value; OnPropertyChanged(); } }
        private string _Phone = string.Empty;

        [SugarColumn(ColumnName ="address")]
        public string Address { get => _Address; set { _Address = value; OnPropertyChanged(); } }
        private string _Address = string.Empty;

        [SugarColumn(ColumnName ="company")]
        public string Company { get => _Company; set { _Company = value; OnPropertyChanged(); } }
        private string _Company = string.Empty;

        [SugarColumn(ColumnName ="department")]
        public string Department { get => _Department; set { _Department = value; OnPropertyChanged(); } }
        private string _Department = string.Empty;

        [SugarColumn(ColumnName ="position")]
        public string Position { get => _Position; set { _Position = value; OnPropertyChanged(); } }
        private string _Position = string.Empty;

        [SugarColumn(ColumnName ="remark")]
        public string Remark { get => _Remark; set { _Remark = value; OnPropertyChanged(); } }
        private string _Remark = string.Empty;

        [SugarColumn(ColumnName ="user_image")]
        public string UserImage { get => _UserImage; set { _UserImage = value; OnPropertyChanged(); } }
        private string _UserImage;

        [SugarColumn(ColumnName = "created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        [SugarColumn(ColumnName = "updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

    };


}
