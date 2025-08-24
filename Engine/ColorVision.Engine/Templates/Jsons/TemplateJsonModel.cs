using ColorVision.Database;
using SqlSugar;
using System;

namespace ColorVision.Engine.Templates.Jsons
{
    [SugarTable("t_scgd_mod_param_master")]
    public class TemplateJsonModel: VPKModel
    {
        [SugarColumn(ColumnName ="name")]
        public string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name;

        [SugarColumn(ColumnName ="mm_id")]
        public int? DicId  { get => _DicId; set { _DicId = value; NotifyPropertyChanged(); } }
        private int? _DicId;

        [SugarColumn(ColumnName ="code")]
        public string? DicCode { get => _DicCode; set { _DicCode = value; NotifyPropertyChanged(); } }
        private string? _DicCode;

        [SugarColumn(ColumnName ="res_pid")]
        public int? ResourceId { get => _ResourceId; set { _ResourceId = value; NotifyPropertyChanged(); } }
        private int? _ResourceId;

        [SugarColumn(ColumnName ="cfg_json")]
        public string? JsonVal { get => _JsonVal; set { _JsonVal = value; NotifyPropertyChanged(); } }
        private string? _JsonVal;

        [SugarColumn(ColumnName ="is_enable")]
        public bool IsEnable { get => _IsEnable; set { _IsEnable = value; NotifyPropertyChanged(); } }
        private bool _IsEnable =true;

        [SugarColumn(ColumnName ="is_delete")]
        public bool IsDelete { get => _IsDelete; set { _IsDelete = value; NotifyPropertyChanged(); } }
        private bool _IsDelete;

        [SugarColumn(ColumnName ="tenant_id")]
        public int TenantId { get => _TenantId; set { _TenantId = value; NotifyPropertyChanged(); } }
        private int _TenantId;

        [SugarColumn(ColumnName ="create_date")]
        public DateTime CreateDate { get => _CreateDate; set { _CreateDate = value; NotifyPropertyChanged(); } }
        private DateTime _CreateDate = DateTime.Now;

        [SugarColumn(ColumnName ="remark")]
        public string? Remark { get => _Remark; set { _Remark = value; NotifyPropertyChanged(); } }
        private string? _Remark;
    }

    public class TemplateJsonDao : BaseTableDao<TemplateJsonModel>
    {

        public static TemplateJsonDao Instance { get; set; } = new TemplateJsonDao();

    }
}
