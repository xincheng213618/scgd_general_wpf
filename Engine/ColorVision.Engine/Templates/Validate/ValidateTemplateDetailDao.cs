#pragma warning disable CS8601
using ColorVision.Engine.MySql.ORM;
using SqlSugar;
using System.Data;

namespace ColorVision.Engine.Templates.Validate
{
    [SugarTable("t_scgd_rule_validate_template_detail")]
    public class ValidateTemplateDetailModel : PKModel
    {
        [SugarColumn(ColumnName ="dic_pid")]
        public int DicPid { get; set; }
        [SugarColumn(ColumnName ="pid")]
        public int Pid { get; set; }

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
    }



    public class ValidateTemplateDetailDao : BaseTableDao<ValidateTemplateDetailModel>
    {
        public static ValidateTemplateDetailDao Instance { get; set; } = new ValidateTemplateDetailDao();

        public ValidateTemplateDetailDao() : base()
        {
        }
    }
}
