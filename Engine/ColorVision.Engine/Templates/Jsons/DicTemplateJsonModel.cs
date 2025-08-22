using ColorVision.Engine.MySql;
using ColorVision.Engine.MySql.ORM;
using SqlSugar;
using System;

namespace ColorVision.Engine.Templates.Jsons
{
    [SugarTable("t_scgd_sys_dictionary_mod_master")]
    public class DicTemplateJsonModel : VPKModel, IInitTables
    {
        [SugarColumn(ColumnName ="code", Length =32)]
        public string Code { get => _Code; set { _Code = value; NotifyPropertyChanged(); } }
        private string _Code;

        [SugarColumn(ColumnName ="name",Length =64)]
        public string? Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string? _Name;

        [SugarColumn(ColumnName ="pid")]
        public int Pid { get => _Pid; set { _Pid = value; NotifyPropertyChanged(); } }
        private int _Pid;

        [SugarColumn(ColumnName = "p_type",ColumnDataType = "tinyint")]
        public int Ptype { get => _Ptype; set { _Ptype = value; NotifyPropertyChanged(); } }
        private int _Ptype;

        [SugarColumn(ColumnName ="mod_type")]
        public int ModType { get => _ModType; set { _ModType = value; NotifyPropertyChanged(); } }
        private int _ModType;

        [SugarColumn(ColumnName ="cfg_json",ColumnDataType ="json")]
        public string? JsonVal { get => _JsonVal; set { _JsonVal = value; NotifyPropertyChanged(); } }
        private string? _JsonVal;

        [SugarColumn(ColumnName = "version", Length = 16)]
        public string? Version { get => _Version; set { _Version = value; NotifyPropertyChanged(); } }
        private string? _Version;

        [SugarColumn(ColumnName ="create_date")]
        public DateTime CreateDate { get => _CreateDate; set { _CreateDate = value; NotifyPropertyChanged(); } }
        private DateTime _CreateDate = DateTime.Now;

        [SugarColumn(ColumnName = "is_enable")]
        public bool IsEnable { get => _IsEnable; set { _IsEnable = value; NotifyPropertyChanged(); } }
        private bool _IsEnable = true;

        [SugarColumn(ColumnName = "is_delete")]
        public bool IsDelete { get => _IsDelete; set { _IsDelete = value; NotifyPropertyChanged(); } }
        private bool _IsDelete;

        [SugarColumn(ColumnName ="remark",Length =256)]
        public string? Remark { get => _Remark; set { _Remark = value; NotifyPropertyChanged(); } }
        private string? _Remark;

        [SugarColumn(ColumnName = "tenant_id")]
        public int TenantId { get => _TenantId; set { _TenantId = value; NotifyPropertyChanged(); } }
        private int _TenantId;



    }
    public class DicTemplateJsonDao : BaseTableDao<DicTemplateJsonModel>
    {

        public static DicTemplateJsonDao Instance { get; set; } = new DicTemplateJsonDao();
    }

}
