using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql.ORM;
using System;
using System.Data;

namespace ColorVision.Engine.Services.SysDictionary
{
    public class SysDictionaryModDetaiModel : ViewModelBase,IPKModel
    {
        public int Id { get; set; }
        public int PId { get => _PId; set { _PId = value; NotifyPropertyChanged(); } }
        private int _PId;

        public long AddressCode { get => _AddressCode; set { _AddressCode = value; NotifyPropertyChanged(); } }
        private long _AddressCode;
        public string? Symbol { get => _Symbol; set { _Symbol = value; NotifyPropertyChanged(); } }
        private string _Symbol;
        public string? Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string? _Name;
        public string? DefaultValue { get => _DefaultValue; set { _DefaultValue = value; NotifyPropertyChanged(); } }
        private string? _DefaultValue;
        public short ValueType { get => _ValueType; set { _ValueType = value; NotifyPropertyChanged(); } }
        private short _ValueType;

        public bool IsEnable { get => _IsEnable; set { _IsEnable = value; NotifyPropertyChanged(); } }
        private bool _IsEnable;

        public bool IsDelete { get => _IsDelete; set { _IsDelete = value; NotifyPropertyChanged(); } }
        private bool _IsDelete;

        public DateTime CreateDate { get => _CreateDate; set { _CreateDate = value; NotifyPropertyChanged(); } }
        private DateTime _CreateDate;
    }
    public class SysDictionaryModDetailDao : BaseTableDao<SysDictionaryModDetaiModel>
    {
        public static SysDictionaryModDetailDao Instance { get; set; } = new SysDictionaryModDetailDao();

        public SysDictionaryModDetailDao() : base("t_scgd_sys_dictionary_mod_item", "id")
        {
        }
        public override DataTable CreateColumns(DataTable dataTable)
        {
            dataTable.Columns.Add("id", typeof(int));
            dataTable.Columns.Add("Pid", typeof(int));
            dataTable.Columns.Add("symbol", typeof(int));
            dataTable.Columns.Add("name", typeof(int));
            dataTable.Columns.Add("default_val", typeof(string));
            dataTable.Columns.Add("val_type", typeof(string));
            dataTable.Columns.Add("is_enable", typeof(bool));
            dataTable.Columns.Add("is_delete", typeof(bool));
            dataTable.Columns.Add("address_code", typeof(long));
            dataTable.Columns.Add("create_date", typeof(DateTime));
            return dataTable;
        }

        public override DataRow Model2Row(SysDictionaryModDetaiModel item, DataRow row)
        {
            if (item.Id > 0) row["id"] = item.Id;
            row["name"] = DataTableExtension.IsDBNull(item.Name);
            row["pid"] = DataTableExtension.IsDBNull(item.PId);
            row["symbol"] = DataTableExtension.IsDBNull(item.Symbol);
            row["default_val"] = DataTableExtension.IsDBNull(item.DefaultValue);
            row["val_type"] = DataTableExtension.IsDBNull(item.ValueType);
            row["is_enable"] = DataTableExtension.IsDBNull(item.IsEnable);
            row["is_delete"] = DataTableExtension.IsDBNull(item.IsDelete);
            row["address_code"] = DataTableExtension.IsDBNull(item.AddressCode);
            row["create_date"] = DataTableExtension.IsDBNull(item.CreateDate);
            return row;
        }

        public override SysDictionaryModDetaiModel GetModelFromDataRow(DataRow item)
        {
            SysDictionaryModDetaiModel model = new()
            {
                Id = item.Field<int>("id"),
                Symbol = item.Field<string>("symbol"),
                Name = item.Field<string>("name"),
                PId = item.Field<int>("pid"),
                DefaultValue = item.Field<string>("default_val"),
                ValueType = item.Field<sbyte>("val_type"),
                AddressCode = item.Field<long>("address_code"),
                IsEnable = item.Field<bool>("is_enable"),
                IsDelete = item.Field<bool>("is_delete"),
                CreateDate = item.Field<DateTime>("create_date"),
            };
            return model;
        }
    }
}
