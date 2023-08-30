using System;
using System.Data;

namespace ColorVision.MySql.DAO
{
    public class SysDictionaryModDetaiModel : PKModel
    {
        public string? Symbol { get; set; }
        public string? Name { get; set; }
        public string? DefaultValue { get; set; }
        public short ValueType { get; set; }
    }
    public class SysDictionaryModDetailDao : BaseDaoMaster<SysDictionaryModDetaiModel>
    {
        public SysDictionaryModDetailDao() : base("v_scgd_sys_dictionary_mod_item", "t_scgd_sys_dictionary_mod_item", "id", true)
        {
        }

        public override SysDictionaryModDetaiModel GetModel(DataRow item)
        {
            SysDictionaryModDetaiModel model = new SysDictionaryModDetaiModel
            {
                Id = item.Field<int>("id"),
                Symbol = item.Field<string>("symbol"),
                Name = item.Field<string>("name"),
                DefaultValue = item.Field<string>("default_val"),
                ValueType = item.Field<SByte>("val_type"),
            };

            return model;
        }
    }
}
