using ColorVision.Database;
using SqlSugar;

namespace ColorVision.Engine.Rbac
{
    [SugarTable("sys_user2tenant")]
    public class UserTenantEntity  : ViewEntity 
    {
        [SugarColumn(ColumnName = "user_id")]
        public int UserId { get; set; }
        [SugarColumn(ColumnName ="tenant_id")]
        public int TenantId { get; set; }
    }
}
