using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MySql.DAO
{
    public class SpectumResultModel : PKModel
    {

    }
    public class SpectumResultDao : BaseDaoMaster<SpectumResultModel>
    {
        public SpectumResultDao() : base(string.Empty, "t_scgd_measure_result_spectrometer", "id", false)
        {
        }
    }
}
