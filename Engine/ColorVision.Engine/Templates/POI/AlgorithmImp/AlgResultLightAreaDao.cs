using ColorVision.Engine.Interfaces;
using ColorVision.Engine.MySql.ORM;

namespace ColorVision.Engine.Templates.POI.AlgorithmImp
{
    [Table("t_scgd_algorithm_result_detail_light_area", PrimaryKey = "id")]
    public class AlgResultLightAreaModel : PKModel, IViewResult
    {
        [Column("pid")]
        public int Pid { get; set; }

        [Column("pos_x")]
        public float PosX { get; set; }

        [Column("pos_y")]
        public float PosY { get; set; }

    }
    public class AlgResultLightAreaDao : BaseTableDao<AlgResultLightAreaModel>
    {
        public static AlgResultLightAreaDao Instance { get; set; } = new AlgResultLightAreaDao();

        public AlgResultLightAreaDao() : base("t_scgd_algorithm_result_detail_light_area")
        {
        }
    }
}
