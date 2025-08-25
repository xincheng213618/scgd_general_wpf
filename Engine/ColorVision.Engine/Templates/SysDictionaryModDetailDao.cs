using ColorVision.Database;
using SqlSugar;
using System;
using System.ComponentModel;

namespace ColorVision.Engine.Templates
{
    public enum SValueType
    {
        [DescriptionAttribute("整数")]
        Integer = 0,  // 整数
        [DescriptionAttribute("浮点")]
        Float = 1,    // 浮点

        [DescriptionAttribute("布尔")]
        Boolean = 2,  // 布尔
        [DescriptionAttribute("字符串")]
        String = 3,   // 字符串
        [DescriptionAttribute("枚举")]
        Enum = 4      // 枚举
    }

    [SugarTable("t_scgd_sys_dictionary_mod_item")]
    public class SysDictionaryModDetaiModel : VPKModel
    {
        [SugarColumn(ColumnName ="pid")]
        public int PId { get => _PId; set { _PId = value; OnPropertyChanged(); } }
        private int _PId;
        [SugarColumn(ColumnName ="address_code")]
        public long AddressCode { get => _AddressCode; set { _AddressCode = value; OnPropertyChanged(); } }
        private long _AddressCode;
        [SugarColumn(ColumnName ="symbol")]
        public string? Symbol { get => _Symbol; set { _Symbol = value; OnPropertyChanged(); } }
        private string? _Symbol;
        [SugarColumn(ColumnName ="name")]
        public string? Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string? _Name;

        [SugarColumn(ColumnName ="default_val")]
        public string? DefaultValue { get => _DefaultValue; set { _DefaultValue = value; OnPropertyChanged(); } }
        private string? _DefaultValue;
        [SugarColumn(ColumnName ="val_type")]
        public SValueType ValueType { get => _ValueType; set { _ValueType = value; OnPropertyChanged(); } }
        private SValueType _ValueType;
        [SugarColumn(ColumnName ="is_enable")]
        public bool IsEnable { get => _IsEnable; set { _IsEnable = value; OnPropertyChanged(); } }
        private bool _IsEnable = true;
        [SugarColumn(ColumnName ="is_delete")]
        public bool IsDelete { get => _IsDelete; set { _IsDelete = value; OnPropertyChanged(); } }
        private bool _IsDelete;
        [SugarColumn(ColumnName ="create_date")]
        public DateTime CreateDate { get => _CreateDate; set { _CreateDate = value; OnPropertyChanged(); } }
        private DateTime _CreateDate;
    }

    public class SysDictionaryModDetailDao : BaseTableDao<SysDictionaryModDetaiModel>
    {
        public static SysDictionaryModDetailDao Instance { get; set; } = new SysDictionaryModDetailDao();
    }
}
