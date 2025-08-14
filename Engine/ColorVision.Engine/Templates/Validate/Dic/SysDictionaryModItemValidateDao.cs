using ColorVision.Engine.MySql.ORM;
using SqlSugar;

namespace ColorVision.Engine.Templates.Validate.Dic
{
    [SugarTable("t_scgd_sys_dictionary_mod_item_validate")]
    public class SysDictionaryModItemValidateModel : VPKModel
    {
        [Column("pid")]
        public int? Pid { get; set; }
        [Column("code")]
        public string Code { get; set; }
        [Column("val_max")]
        public float ValMax { get; set; }
        [Column("val_min")]
        public float ValMin { get; set; }
        [Column("val_equal")]
        public string ValEqual { get; set; }
        [Column("val_radix")]
        public short ValRadix { get; set; }
        [Column("val_type")]
        public short ValType { get; set; }
        [Column("is_enable")]
        public bool IsEnable { get; set; }
    }


    public class SysDictionaryModItemValidateDao : BaseTableDao<SysDictionaryModItemValidateModel>
    {
        public static SysDictionaryModItemValidateDao Instance { get; set; } = new SysDictionaryModItemValidateDao();
    }
}
