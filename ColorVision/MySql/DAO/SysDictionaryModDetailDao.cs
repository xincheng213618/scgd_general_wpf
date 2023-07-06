using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MySql.DAO
{
    public class SysDictionaryModDetaiModel : IBaseModel
    {
        public int Id { get; set; }
        public string Symbol { get; set; }
        public string Name { get; set; }
        public string DefaultValue { get; set; }
        public short ValueType { get; set; }

        public int GetPK()
        {
            return Id;
        }

        public void SetPK(int id)
        {
            Id = id;
        }
    }
    public class SysDictionaryModDetailDao : BaseServiceMaster<SysDictionaryModDetaiModel>
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
