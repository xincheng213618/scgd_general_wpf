using ColorVision.Engine.MySql.ORM;

namespace ColorVision.Engine.Templates.Compliance
{
    public class ComplianceXYZDao : BaseTableDao<ComplianceXYZModel>
    {
        public static ComplianceXYZDao Instance { get; set; } = new ComplianceXYZDao();
    }



}
