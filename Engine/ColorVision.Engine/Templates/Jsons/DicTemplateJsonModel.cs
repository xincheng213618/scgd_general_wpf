using ColorVision.Engine.MySql.ORM;
using System;

namespace ColorVision.Engine.Templates.Jsons
{
    [Table("t_scgd_sys_dictionary_mod_master")]
    public class DicTemplateJsonModel : VPKModel
    {
        [Column("code")]
        public string Code { get => _Code; set { _Code = value; NotifyPropertyChanged(); } }
        private string _Code;

        [Column("name")]
        public string? Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string? _Name;

        [Column("pid")]
        public int Pid { get => _Pid; set { _Pid = value; NotifyPropertyChanged(); } }
        private int _Pid;

        [Column("mod_type")]
        public int ModType { get => _ModType; set { _ModType = value; NotifyPropertyChanged(); } }
        private int _ModType;

        [Column("cfg_json")]
        public string? JsonVal { get => _JsonVal; set { _JsonVal = value; NotifyPropertyChanged(); } }
        private string? _JsonVal;

        [Column("is_enable")]
        public bool IsEnable { get => _IsEnable; set { _IsEnable = value; NotifyPropertyChanged(); } }
        private bool _IsEnable = true;

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
    public class DicTemplateJsonDao : BaseTableDao<DicTemplateJsonModel>
    {

        public static DicTemplateJsonDao Instance { get; set; } = new DicTemplateJsonDao();
    }

}
