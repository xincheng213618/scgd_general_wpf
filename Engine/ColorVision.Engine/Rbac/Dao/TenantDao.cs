using ColorVision.Database;
using SqlSugar;
using System;

namespace ColorVision.Engine.Rbac
{
    [SugarTable("t_scgd_sys_user2tenant")]
    public class UserTenant : ViewEntity 
    {
        [SugarColumn(ColumnName ="name")]
        public int UserId { get; set; }
        [SugarColumn(ColumnName ="tenant_id")]
        public int TenantId { get; set; }
    }

    public class UserTenantDao : BaseTableDao<Tenant>
    {
        public static UserTenantDao Instance { get; set; } = new UserTenantDao();
    }

    [SugarTable("t_scgd_sys_tenant")]
    public class Tenant: ViewEntity 
    {
        [SugarColumn(ColumnName ="name")]
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name = string.Empty;

        [SugarColumn(ColumnName ="create_date")]
        public DateTime CreateDate { get => _CreateDate; set { _CreateDate = value; OnPropertyChanged(); } }
        private DateTime _CreateDate;

        [SugarColumn(ColumnName ="is_enable")]
        public bool IsEnable { get => _IsEnable; set { _IsEnable = value; OnPropertyChanged(); } }
        private bool _IsEnable;

        [SugarColumn(ColumnName ="is_delete")]
        public bool IsDelete { get => _IsDelete; set { _IsDelete = value; OnPropertyChanged(); } }
        private bool _IsDelete;

        [SugarColumn(ColumnName ="remark")]
        public string Remark { get => _Remark; set { _Remark = value; OnPropertyChanged(); } }
        private string _Remark = string.Empty;
    }


    public class TenantDao: BaseTableDao<Tenant>
    {
        public static TenantDao Instance { get; set; } = new TenantDao();

    }
}
