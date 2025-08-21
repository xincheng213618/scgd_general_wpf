using ColorVision.Engine.MySql.ORM;
using SqlSugar;
using System;

namespace ColorVision.Engine.Templates.Jsons
{
    [SugarTable("t_scgd_sys_dictionary_mod_master")]
    public class DicTemplateJsonModel : VPKModel
    {
        [SugarColumn(ColumnName ="code")]
        public string Code { get => _Code; set { _Code = value; NotifyPropertyChanged(); } }
        private string _Code;

        [SugarColumn(ColumnName ="name")]
        public string? Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string? _Name;

        [SugarColumn(ColumnName ="pid")]
        public int Pid { get => _Pid; set { _Pid = value; NotifyPropertyChanged(); } }
        private int _Pid;

        [SugarColumn(ColumnName ="mod_type")]
        public int ModType { get => _ModType; set { _ModType = value; NotifyPropertyChanged(); } }
        private int _ModType;

        [SugarColumn(ColumnName ="cfg_json")]
        public string? JsonVal { get => _JsonVal; set { _JsonVal = value; NotifyPropertyChanged(); } }
        private string? _JsonVal;

        [SugarColumn(ColumnName ="is_enable")]
        public bool IsEnable { get => _IsEnable; set { _IsEnable = value; NotifyPropertyChanged(); } }
        private bool _IsEnable = true;

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
    public class DicTemplateJsonDao : BaseTableDao<DicTemplateJsonModel>
    {

        public static DicTemplateJsonDao Instance { get; set; } = new DicTemplateJsonDao();
    }

}
