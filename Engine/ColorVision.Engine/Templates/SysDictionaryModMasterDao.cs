using ColorVision.Common.MVVM;
using ColorVision.Database;
using SqlSugar;
using System;
using System.Collections.Generic;

namespace ColorVision.Engine.Templates
{
    [SugarTable("t_scgd_sys_dictionary_mod_master")]
    public class SysDictionaryModModel : VPKModel, IInitTables
    {
        [SugarColumn(ColumnName = "code", Length = 32)]
        public string Code { get => _Code; set { _Code = value; OnPropertyChanged(); } }
        private string _Code;

        [SugarColumn(ColumnName = "name", Length = 64)]
        public string? Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string? _Name;

        [SugarColumn(ColumnName = "pid")]
        public int Pid { get => _Pid; set { _Pid = value; OnPropertyChanged(); } }
        private int _Pid;

        [SugarColumn(ColumnName = "p_type", ColumnDataType = "tinyint")]
        public int Ptype { get => _Ptype; set { _Ptype = value; OnPropertyChanged(); } }
        private int _Ptype;

        [SugarColumn(ColumnName = "mod_type")]
        public int ModType { get => _ModType; set { _ModType = value; OnPropertyChanged(); } }
        private int _ModType;

        [SugarColumn(ColumnName = "cfg_json", ColumnDataType = "json")]
        public string? JsonVal { get => _JsonVal; set { _JsonVal = value; OnPropertyChanged(); } }
        private string? _JsonVal;

        [SugarColumn(ColumnName = "version", Length = 16)]
        public string? Version { get => _Version; set { _Version = value; OnPropertyChanged(); } }
        private string? _Version;

        [SugarColumn(ColumnName = "create_date")]
        public DateTime CreateDate { get => _CreateDate; set { _CreateDate = value; OnPropertyChanged(); } }
        private DateTime _CreateDate = DateTime.Now;

        [SugarColumn(ColumnName = "is_enable")]
        public bool IsEnable { get => _IsEnable; set { _IsEnable = value; OnPropertyChanged(); } }
        private bool _IsEnable = true;

        [SugarColumn(ColumnName = "is_delete")]
        public bool IsDelete { get => _IsDelete; set { _IsDelete = value; OnPropertyChanged(); } }
        private bool _IsDelete;

        [SugarColumn(ColumnName = "remark", Length = 256)]
        public string? Remark { get => _Remark; set { _Remark = value; OnPropertyChanged(); } }
        private string? _Remark;

        [SugarColumn(ColumnName = "tenant_id")]
        public int TenantId { get => _TenantId; set { _TenantId = value; OnPropertyChanged(); } }
        private int _TenantId;
    }

    public class SysDictionaryModMasterDao : BaseTableDao<SysDictionaryModModel>
    {
        public static SysDictionaryModMasterDao Instance { get; set; } = new SysDictionaryModMasterDao();
        public SysDictionaryModModel? GetByCode(string code, int tenantId) => Db.Queryable<SysDictionaryModModel>().Where(x => x.Code == code && x.IsDelete == false && x.TenantId == tenantId).First();
    }
}
