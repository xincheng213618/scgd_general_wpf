using SqlSugar;

namespace ColorVision.Rbac
{
    [SugarTable("sys_user_role")]
    public class UserRoleEntity
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "id")]
        public int Id { get; set; }

        [SugarColumn(ColumnName = "user_id")]
        public int UserId { get; set; }

        [SugarColumn(ColumnName = "role_id")]
        public int RoleId { get; set; }


    }

}
