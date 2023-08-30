namespace ColorVision.MySql.DAO
{
    public class LicenseModel : PKModel
    {
    }
    public class LicenseDao : BaseDaoMaster<LicenseModel>
    {
        public LicenseDao() : base(string.Empty, "t_scgd_license", "id", true)
        {
        }
    }
}
