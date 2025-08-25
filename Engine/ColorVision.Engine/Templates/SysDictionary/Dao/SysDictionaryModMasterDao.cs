using ColorVision.Common.MVVM;
using ColorVision.Database;
using SqlSugar;
using System;
using System.Collections.Generic;

namespace ColorVision.Engine.Templates.SysDictionary
{
    [SugarTable("t_scgd_sys_dictionary_mod_master")]
    public class SysDictionaryModModel : ViewModelBase,IPKModel
    {
        [SugarColumn(ColumnName ="id")]
        public int Id { get; set; } 
        [SugarColumn(ColumnName ="pid")]
        public int? PId { get => _PId; set { _PId = value; OnPropertyChanged(); } }
        private int? _PId;
        [SugarColumn(ColumnName ="mod_type")]
        public short ModType { get => _ModType; set { _ModType = value; OnPropertyChanged(); } }
        private short _ModType;

        [SugarColumn(ColumnName ="p_type")]
        public short Ptype { get => _Ptype; set { _Ptype = value; OnPropertyChanged(); } }
        private short _Ptype;

        [SugarColumn(ColumnName ="code")]
        public string Code { get => _Code; set { _Code = value; OnPropertyChanged(); } }
        private string _Code;

        [SugarColumn(ColumnName ="name")]
        public string? Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string? _Name;


        [SugarColumn(ColumnName ="cfg_json")]
        public string? CfgJson { get => _CfgJson; set { _CfgJson = value; OnPropertyChanged(); } }
        private string? _CfgJson;


        [SugarColumn(ColumnName ="tenant_id")]
        public int TenantId { get => _TenantId; set { _TenantId = value; OnPropertyChanged(); } }
        private int _TenantId;

        [SugarColumn(ColumnName ="create_date")]
        public DateTime CreateDate { get => _CreateDate; set { _CreateDate = value; OnPropertyChanged(); } }
        private DateTime _CreateDate;


        [SugarColumn(ColumnName = "is_enable")]
        public bool IsEnable { get => _IsEnable; set { _IsEnable = value; OnPropertyChanged(); } }
        private bool _IsEnable = true;


        [SugarColumn(ColumnName = "is_delete")]
        public bool IsDelete { get => _IsDelete; set { _IsDelete = value; OnPropertyChanged(); } }
        private bool _IsDelete;


        [SugarColumn(ColumnName ="remark")]
        public string? Remark { get => _Remark; set { _Remark = value; OnPropertyChanged(); } }
        private string? _Remark;
    }

    public class SysDictionaryModMasterDao : BaseTableDao<SysDictionaryModModel>
    {
        public static SysDictionaryModMasterDao Instance { get; set; } = new SysDictionaryModMasterDao();
        public SysDictionaryModModel? GetByCode(string code, int tenantId) => this.GetByParam( new Dictionary<string, object>() { { "is_delete",0 }, { "code", code }, { "tenant_id", tenantId } });
    }
}
