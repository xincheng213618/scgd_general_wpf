#pragma warning disable CS8601
using ColorVision.Engine.MySql.ORM;
using SqlSugar;
using System;

namespace ColorVision.Engine.Templates.Validate
{
    [SugarTable("t_scgd_rule_validate_template_master")]
    public class ValidateTemplateMasterModel : PKModel
    {
        [SugarColumn(ColumnName ="dic_pid")]
        public int? DId { get; set; }
        [SugarColumn(ColumnName ="name")]
        public string Name { get; set; }
        [SugarColumn(ColumnName ="code")]
        public string Code { get; set; }
        [SugarColumn(ColumnName ="create_date")]
        public DateTime? CreateDate { get; set; } = DateTime.Now;
        [SugarColumn(ColumnName ="remark")]
        public string Remark { get; set; }
        [SugarColumn(ColumnName ="tenant_id")]
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
