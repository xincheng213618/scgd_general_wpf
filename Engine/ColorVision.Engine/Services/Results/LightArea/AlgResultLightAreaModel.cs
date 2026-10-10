using ColorVision.Database;
using SqlSugar;

namespace ColorVision.Engine.Services.Results.LightArea
{
    /// <summary>Shared luminous-area corner detail for AA and local detection results.</summary>
    [SugarTable("t_scgd_algorithm_result_detail_light_area")]
    public class AlgResultLightAreaModel : EntityBase, IViewResult
    {
        [SugarColumn(ColumnName = "pid")]
        public int Pid { get; set; }

        [SugarColumn(ColumnName = "pos_x")]
        public float PosX { get; set; }

        [SugarColumn(ColumnName = "pos_y")]
        public float PosY { get; set; }
    }
}
