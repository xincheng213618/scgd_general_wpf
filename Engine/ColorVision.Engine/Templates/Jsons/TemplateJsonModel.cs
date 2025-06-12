using ColorVision.Engine.MySql.ORM;
using System;

namespace ColorVision.Engine.Templates.Jsons
{
    [Table("t_scgd_mod_param_master")]
    public class TemplateJsonModel: VPKModel
    {
        [Column("name")]
        public string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name;

        [Column("mm_id")]
        public int? DicId  { get => _DicId; set { _DicId = value; NotifyPropertyChanged(); } }
        private int? _DicId;

        [Column("code")]
        public string? DicCode { get => _DicCode; set { _DicCode = value; NotifyPropertyChanged(); } }
        private string? _DicCode;

        [Column("res_pid")]
        public int? ResourceId { get => _ResourceId; set { _ResourceId = value; NotifyPropertyChanged(); } }
        private int? _ResourceId;

        [Column("cfg_json")]
        public string? JsonVal { get => _JsonVal; set { _JsonVal = value; NotifyPropertyChanged(); } }
        private string? _JsonVal;

        [Column("is_enable")]
        public bool IsEnable { get => _IsEnable; set { _IsEnable = value; NotifyPropertyChanged(); } }
        private bool _IsEnable =true;

        [Column("is_delete")]
        public bool IsDelete { get => _IsDelete; set { _IsDelete = value; NotifyPropertyChanged(); } }
        private bool _IsDelete;

        [Column("tenant_id")]
        public int TenantId { get => _TenantId; set { _TenantId = value; NotifyPropertyChanged(); } }
        private int _TenantId;

        [Column("create_date")]
        public DateTime CreateDate { get => _CreateDate; set { _CreateDate = value; NotifyPropertyChanged(); } }
        private DateTime _CreateDate = DateTime.Now;

        [Column("remark")]
        public string? Remark { get => _Remark; set { _Remark = value; NotifyPropertyChanged(); } }
        private string? _Remark;
    }

    public class TemplateJsonDao : BaseTableDao<TemplateJsonModel>
    {

        public static TemplateJsonDao Instance { get; set; } = new TemplateJsonDao();

    }
}
