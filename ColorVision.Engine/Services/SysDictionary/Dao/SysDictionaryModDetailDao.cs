using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql.ORM;
using System;
using System.Data;

namespace ColorVision.Engine.Services.SysDictionary
{
    public class SysDictionaryModDetaiModel : ViewModelBase,IPKModel
    {
        public int Id { get; set; }
        public long AddressCode { get; set; }
        public string? Symbol { get; set; }
        public string? Name { get; set; }
        public string? DefaultValue { get; set; }
        public short ValueType { get; set; }
    }
    public class SysDictionaryModDetailDao : BaseDaoMaster<SysDictionaryModDetaiModel>
    {
        public static SysDictionaryModDetailDao Instance { get; set; } = new SysDictionaryModDetailDao();

        public SysDictionaryModDetailDao() : base("v_scgd_sys_dictionary_mod_item", "t_scgd_sys_dictionary_mod_item", "id", true)
        {
        }
        public override DataTable CreateColumns(DataTable dInfo)
        {
            dInfo.Columns.Add("id", typeof(int));
            dInfo.Columns.Add("symbol", typeof(int));
            dInfo.Columns.Add("name", typeof(int));
            dInfo.Columns.Add("default_val", typeof(string));
            dInfo.Columns.Add("val_type", typeof(string));
            dInfo.Columns.Add("is_enable", typeof(bool));
            dInfo.Columns.Add("is_delete", typeof(bool));
            dInfo.Columns.Add("address_code", typeof(long));
            return dInfo;
        }

        public override SysDictionaryModDetaiModel GetModelFromDataRow(DataRow item)
        {
            SysDictionaryModDetaiModel model = new()
            {
                Id = item.Field<int>("id"),
                Symbol = item.Field<string>("symbol"),
                Name = item.Field<string>("name"),
                DefaultValue = item.Field<string>("default_val"),
                ValueType = item.Field<sbyte>("val_type"),
                AddressCode = item.Field<long>("address_code")
            };

            return model;
        }
    }
}
