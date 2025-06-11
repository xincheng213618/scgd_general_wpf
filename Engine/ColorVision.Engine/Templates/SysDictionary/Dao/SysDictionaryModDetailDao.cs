using ColorVision.Engine.MySql.ORM;
using System;
using System.Data;

namespace ColorVision.Engine.Templates.SysDictionary
{
    [Table("t_scgd_sys_dictionary_mod_item")]
    public class SysDictionaryModDetaiModel : VPKModel
    {
        [Column("pid")]
        public int PId { get => _PId; set { _PId = value; NotifyPropertyChanged(); } }
        private int _PId;
        [Column("address_code")]
        public long AddressCode { get => _AddressCode; set { _AddressCode = value; NotifyPropertyChanged(); } }
        private long _AddressCode;
        [Column("symbol")]
        public string? Symbol { get => _Symbol; set { _Symbol = value; NotifyPropertyChanged(); } }
        private string? _Symbol;
        [Column("name")]
        public string? Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string? _Name;

        [Column("default_val")]
        public string? DefaultValue { get => _DefaultValue; set { _DefaultValue = value; NotifyPropertyChanged(); } }
        private string? _DefaultValue;
        [Column("val_type")]
        public SValueType ValueType { get => _ValueType; set { _ValueType = value; NotifyPropertyChanged(); } }
        private SValueType _ValueType;
        [Column("is_enable")]
        public bool IsEnable { get => _IsEnable; set { _IsEnable = value; NotifyPropertyChanged(); } }
        private bool _IsEnable = true;
        [Column("is_delete")]
        public bool IsDelete { get => _IsDelete; set { _IsDelete = value; NotifyPropertyChanged(); } }
        private bool _IsDelete;
        [Column("create_date")]
        public DateTime CreateDate { get => _CreateDate; set { _CreateDate = value; NotifyPropertyChanged(); } }
        private DateTime _CreateDate;
    }

    public class SysDictionaryModDetailDao : BaseTableDao<SysDictionaryModDetaiModel>
    {
        public static SysDictionaryModDetailDao Instance { get; set; } = new SysDictionaryModDetailDao();
    }
}
