#pragma warning disable CS8601
using ColorVision.Engine.MySql.ORM;
using SqlSugar;
using System;

namespace ColorVision.Engine.Templates.Validate
{
    [SugarTable("t_scgd_rule_validate_template_master")]
    public class ValidateTemplateMasterModel : PKModel
    {
        [Column("dic_pid")]
        public int? DId { get; set; }
        [Column("name")]
        public string Name { get; set; }
        [Column("code")]
        public string Code { get; set; }
        [Column("create_date")]
        public DateTime? CreateDate { get; set; } = DateTime.Now;
        [Column("remark")]
        public string Remark { get; set; }
        [Column("tenant_id")]
        public int TenantId { get; set; }
    }


    public class ValidateTemplateMasterDao : BaseTableDao<ValidateTemplateMasterModel>
    {
        public static ValidateTemplateMasterDao Instance { get; set; } = new ValidateTemplateMasterDao();

        public ValidateTemplateMasterDao() : base()
        {

        }
    }
}
