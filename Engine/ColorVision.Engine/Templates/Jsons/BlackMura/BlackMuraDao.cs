using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm;
using CVCommCore;
using Newtonsoft.Json;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.Jsons.BlackMura
{

    [Table("t_scgd_algorithm_result_detail_compliance_jnd")]
    public class BlackMuraModel : PKModel, IViewResult
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


    public class BlackMuraDao : BaseTableDao<BlackMuraModel>
    {
        public static BlackMuraDao Instance { get; set; } = new BlackMuraDao();
        public BlackMuraDao() : base("t_scgd_algorithm_result_detail_binocular_fusion")
        {
        }
    }



}
