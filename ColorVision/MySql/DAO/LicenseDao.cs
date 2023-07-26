using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
