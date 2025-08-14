using ColorVision.Engine.MySql.ORM;
using SqlSugar;

namespace ColorVision.Engine.Templates.Validate.Dic
{
    [SugarTable("t_scgd_sys_dictionary_mod_item_validate")]
    public class SysDictionaryModItemValidateModel : VPKModel
    {
        [SugarColumn(ColumnName ="pid")]
        public int? Pid { get; set; }
        [SugarColumn(ColumnName ="code")]
        public string Code { get; set; }
        [SugarColumn(ColumnName ="val_max")]
        public float ValMax { get; set; }
        [SugarColumn(ColumnName ="val_min")]
        public float ValMin { get; set; }
        [SugarColumn(ColumnName ="val_equal")]
        public string ValEqual { get; set; }
        [SugarColumn(ColumnName ="val_radix")]
        public short ValRadix { get; set; }
        [SugarColumn(ColumnName ="val_type")]
        public short ValType { get; set; }
        [SugarColumn(ColumnName ="is_enable")]
        public bool IsEnable { get; set; }
    }


    public class SysDictionaryModItemValidateDao : BaseTableDao<SysDictionaryModItemValidateModel>
    {
        public static SysDictionaryModItemValidateDao Instance { get; set; } = new SysDictionaryModItemValidateDao();
    }
}
