using ColorVision.Engine.MySql.ORM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Devices.Algorithm.Views
{
    [Table("t_scgd_algorithm_result_detail_compliance_y")]
    public class ComplianceYModel :PKModel
    {
        [Column("pid")]
        public int PId { get; set; }

        [Column("name")]
        public int Name { get; set; }

        [Column("data_type")]
        public int DataType { get; set; }





    }
}
