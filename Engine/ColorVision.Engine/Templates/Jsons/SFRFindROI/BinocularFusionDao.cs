using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm;
using CVCommCore;
using Newtonsoft.Json;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.Jsons.SFRFindROI
{

    [Table("t_scgd_algorithm_result_detail_compliance_jnd")]
    public class BinocularFusionModel : PKModel, IViewResult
    {
        [Column("pid")]
        public int PId { get; set; }

        [Column("cross_mark_center_x")]
        public float CrossMarkCenterX { get; set; }

        [Column("cross_mark_center_y")]
        public float CrossMarkCenterY { get; set; }

        [Column("x_degree")]
        public float XDegree { get; set; }
        [Column("y_degree")]
        public float YDegree { get; set; }

        [Column("z_degree")]
        public float ZDegree { get; set; }
    }


    public class BinocularFusionDao : BaseTableDao<BinocularFusionModel>
    {
        public static BinocularFusionDao Instance { get; set; } = new BinocularFusionDao();
        public BinocularFusionDao() : base("t_scgd_algorithm_result_detail_binocular_fusion")
        {
        }
    }



}
