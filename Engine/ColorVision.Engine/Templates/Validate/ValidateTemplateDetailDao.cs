#pragma warning disable CS8601
using ColorVision.Engine.MySql.ORM;
using System.Data;

namespace ColorVision.Engine.Templates.Validate
{
    [Table("t_scgd_rule_validate_template_detail")]
    public class ValidateTemplateDetailModel : PKModel
    {
        [Column("dic_pid")]
        public int DicPid { get; set; }
        [Column("pid")]
        public int Pid { get; set; }

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
    }



    public class ValidateTemplateDetailDao : BaseTableDao<ValidateTemplateDetailModel>
    {
        public static ValidateTemplateDetailDao Instance { get; set; } = new ValidateTemplateDetailDao();

        public ValidateTemplateDetailDao() : base()
        {
        }
    }
}
