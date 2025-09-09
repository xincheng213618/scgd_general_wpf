using ColorVision.Database;
using SqlSugar;
using System;

namespace ColorVision.Engine.Rbac
{
    [SugarTable("sys_user")]
    public class UserEntity  : ViewEntity 
    {
        [SugarColumn(ColumnName = "username")]
        public string Username { get => _Username; set { _Username = value; OnPropertyChanged(); } }
        private string _Username = "admin";

        [SugarColumn(ColumnName = "password")]
        public string Password { get => _Password; set { _Password = value; OnPropertyChanged(); } }
        private string _Password = "admin";

        [SugarColumn(ColumnName ="is_enable")]
        public bool IsEnable { get; set; } = true;
        [SugarColumn(ColumnName ="is_delete")]
        public bool? IsDelete { get; set; } = false;

        [SugarColumn(ColumnName ="remark")]
        public string Remark { get => _Remark; set { _Remark = value; OnPropertyChanged(); } }
        private string _Remark = string.Empty;

        [SugarColumn(ColumnName = "created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        [SugarColumn(ColumnName = "updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }


}
