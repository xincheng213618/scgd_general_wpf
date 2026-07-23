using ColorVision.Database;
using SqlSugar;

namespace ColorVision.Rbac.Entity
{
    [SugarTable("sys_user")]
    [SugarIndex("uidx_sys_user_username", nameof(Username), OrderByType.Asc, true)] // 唯一用户名索引
    public class UserEntity  : EntityBase
    {
        [SugarColumn(ColumnName = "username")]
        public string Username { get => _Username; set { _Username = value;  } }
        private string _Username = string.Empty;

        [SugarColumn(ColumnName = "password")]
        public string Password { get => _Password; set { _Password = value;  } }
        private string _Password = string.Empty; // 不再提供默认明文

        [SugarColumn(ColumnName ="is_enable")]
        public bool IsEnable { get; set; } = true;
        [SugarColumn(ColumnName ="is_delete")]
        public bool? IsDelete { get; set; } = false;

        [SugarColumn(ColumnName ="remark")]
        public string Remark { get => _Remark; set { _Remark = value; } }
        private string _Remark = string.Empty;

        [SugarColumn(ColumnName = "created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
        [SugarColumn(ColumnName = "updated_at")]
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
    }


}
