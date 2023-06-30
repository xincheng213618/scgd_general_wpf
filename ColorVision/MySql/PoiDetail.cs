using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MySql
{
    public class PoiDetail
    {
        string Tables = "t_scgd_cfg_poi_master";

        public MySqlControl MySqlControl { get; set; }

        public PoiDetail()
        {
            MySqlControl = MySqlControl.GetInstance();
        }
        
    }
}
