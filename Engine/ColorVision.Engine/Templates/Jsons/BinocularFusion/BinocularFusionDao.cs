using ColorVision.Engine.Abstractions;
using ColorVision.Engine.MySql;
using ColorVision.Engine.MySql.ORM;
using SqlSugar;

namespace ColorVision.Engine.Templates.Jsons.BinocularFusion
{

    [SugarTable("t_scgd_algorithm_result_detail_binocular_fusion")]
    public class BinocularFusionModel : PKModel, IViewResult, IInitTables
    {
        [SugarColumn(ColumnName ="pid")]
        public int PId { get; set; }

        [SugarColumn(ColumnName ="cross_mark_center_x")]
        public float CrossMarkCenterX { get; set; }

        [SugarColumn(ColumnName ="cross_mark_center_y")]
        public float CrossMarkCenterY { get; set; }

        [SugarColumn(ColumnName ="x_degree")]
        public float XDegree { get; set; }
        [SugarColumn(ColumnName ="y_degree")]
        public float YDegree { get; set; }

        [SugarColumn(ColumnName ="z_degree")]
        public float ZDegree { get; set; }
    }


    public class BinocularFusionDao : BaseTableDao<BinocularFusionModel>
    {
        public static BinocularFusionDao Instance { get; set; } = new BinocularFusionDao();
    }



}
