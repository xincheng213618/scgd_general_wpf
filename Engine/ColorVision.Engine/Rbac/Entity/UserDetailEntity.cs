using ColorVision.Database;
using ColorVision.UI.Authorizations;
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

        [SugarColumn(ColumnName ="email", IsNullable = true)]
        public string Email { get => _Email; set { _Email = value; OnPropertyChanged(); } }
        private string _Email;

        [SugarColumn(ColumnName ="phone", IsNullable = true)]
        public string Phone { get => _Phone; set { _Phone = value; OnPropertyChanged(); } }
        private string _Phone;

        [SugarColumn(ColumnName ="address", IsNullable = true)]
        public string Address { get => _Address; set { _Address = value; OnPropertyChanged(); } }
        private string _Address;

        [SugarColumn(ColumnName ="company", IsNullable = true)]
        public string Company { get => _Company; set { _Company = value; OnPropertyChanged(); } }
        private string _Company;

        [SugarColumn(ColumnName ="department", IsNullable = true)]
        public string Department { get => _Department; set { _Department = value; OnPropertyChanged(); } }
        private string _Department;

        [SugarColumn(ColumnName ="position", IsNullable = true)]
        public string Position { get => _Position; set { _Position = value; OnPropertyChanged(); } }
        private string _Position;

        [SugarColumn(ColumnName ="remark", IsNullable = true)]
        public string Remark { get => _Remark; set { _Remark = value; OnPropertyChanged(); } }
        private string _Remark;

        [SugarColumn(ColumnName ="user_image",IsNullable =true)]
        public string UserImage { get => _UserImage; set { _UserImage = value; OnPropertyChanged(); } }
        private string _UserImage;

        [SugarColumn(ColumnName = "created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        [SugarColumn(ColumnName = "updated_at")]
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    };


}
