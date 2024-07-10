using ColorVision.Engine.MySql.ORM;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.Compliance
{
    public class ComplianceXYZDao : BaseTableDao<ComplianceXYZModel>
    {
        public static ComplianceXYZDao Instance { get; set; } = new ComplianceXYZDao();
        public ComplianceXYZDao() : base("t_scgd_algorithm_result_detail_compliance_xyz")
        {
        }
    }



}
