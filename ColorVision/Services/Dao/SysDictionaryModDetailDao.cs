using ColorVision.MySql.ORM;
using System.Data;

namespace ColorVision.Services.Dao
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
        public static SysDictionaryModDetailDao Instance { get; set; } = new SysDictionaryModDetailDao();

        public SysDictionaryModDetailDao() : base("v_scgd_sys_dictionary_mod_item", "t_scgd_sys_dictionary_mod_item", "id", true)
        {
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
            };

            return model;
        }
    }
}
