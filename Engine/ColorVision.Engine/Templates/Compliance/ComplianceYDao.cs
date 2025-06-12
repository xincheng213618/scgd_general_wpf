using ColorVision.Engine.MySql.ORM;

namespace ColorVision.Engine.Templates.Compliance
{
    public class ComplianceYDao : BaseTableDao<ComplianceYModel>
    {
        public static ComplianceYDao Instance { get; set; } = new ComplianceYDao();
    }



}
