using ColorVision.Engine.MySql.ORM;

namespace ColorVision.Engine.Templates.Compliance
{
    public class ComplianceYDao : BaseTableDao<ComplianceYModel>
    {
        public static ComplianceYDao Instance { get; set; } = new ComplianceYDao();
        public ComplianceYDao() : base("t_scgd_algorithm_result_detail_compliance_y")
        {
        }
    }



}
