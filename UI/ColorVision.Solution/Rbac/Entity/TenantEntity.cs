using ColorVision.Database;
using SqlSugar;

namespace ColorVision.Rbac.Entity
{
    [SugarTable("sys_tenant")]
    public class TenantEntity: ViewEntity 
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
}
