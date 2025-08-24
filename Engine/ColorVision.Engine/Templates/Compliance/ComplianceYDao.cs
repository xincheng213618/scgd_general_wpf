using ColorVision.Database;

namespace ColorVision.Engine.Templates.Compliance
{
    public class ComplianceYDao : BaseTableDao<ComplianceYModel>
    {
        public static ComplianceYDao Instance { get; set; } = new ComplianceYDao();
    }



}
