using ColorVision.Engine.MySql.ORM;
using SqlSugar;
using System;

namespace ColorVision.Engine.Rbac
{
    [SugarTable("t_scgd_sys_user2tenant")]
    public class UserTenant : VPKModel
    {
        [Column("name")]
        public int UserId { get; set; }
        [Column("tenant_id")]
        public int TenantId { get; set; }
    }

    public class UserTenantDao : BaseTableDao<Tenant>
    {
        public static UserTenantDao Instance { get; set; } = new UserTenantDao();
    }

    [SugarTable("t_scgd_sys_tenant")]
    public class Tenant: VPKModel
    {
        [Column("name")]
        public string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name = string.Empty;

        [Column("create_date")]
        public DateTime CreateDate { get => _CreateDate; set { _CreateDate = value; NotifyPropertyChanged(); } }
        private DateTime _CreateDate;

        [Column("is_enable")]
        public bool IsEnable { get => _IsEnable; set { _IsEnable = value; NotifyPropertyChanged(); } }
        private bool _IsEnable;

        [Column("is_delete")]
        public bool IsDelete { get => _IsDelete; set { _IsDelete = value; NotifyPropertyChanged(); } }
        private bool _IsDelete;

        [Column("remark")]
        public string Remark { get => _Remark; set { _Remark = value; NotifyPropertyChanged(); } }
        private string _Remark = string.Empty;
    }


    public class TenantDao: BaseTableDao<Tenant>
    {
        public static TenantDao Instance { get; set; } = new TenantDao();

    }
}
