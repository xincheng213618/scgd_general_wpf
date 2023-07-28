using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MySql.DAO
{
    public class MeasureMasterModel : PKModel
    {
        public string? Name { get; set; }
        public bool? IsEnable { get; set; } = true;
        public bool? IsDelete { get; set; } = false;
    }
    public class MeasureMasterDao : BaseDaoMaster<MeasureMasterModel>
    {
        public MeasureMasterDao() : base(string.Empty, "t_scgd_measure_template_master", "id", true)
        {
        }
    }
}
