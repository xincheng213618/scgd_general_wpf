using ColorVision.Database;
using SqlSugar;

namespace ColorVision.Engine.Templates.FOV
{
    [SugarTable("t_scgd_algorithm_result_detail_fov")]
    public class AlgResultFOVModel : EntityBase, IInitTables
    {
        [SugarColumn(ColumnName ="pid")]
        public int? Pid { get; set; }
        [SugarColumn(ColumnName ="pattern")]
        public int? Pattern { get; set; }
        [SugarColumn(ColumnName ="type")]
        public int? Type { get; set; }
        [SugarColumn(ColumnName ="radio")]
        public double? Radio { get; set; }
        [SugarColumn(ColumnName ="camera_degrees")]
        public double? CameraDegrees { get; set; }
        [SugarColumn(ColumnName ="dist")]
        public double? Dist { get; set; }
        [SugarColumn(ColumnName ="threshold")]
        public int? Threshold { get; set; }
        [SugarColumn(ColumnName ="degrees")]
        public double? Degrees { get; set; }
    }


    public class AlgResultFOVDao : BaseTableDao<AlgResultFOVModel>
    {
        public static AlgResultFOVDao Instance { get; set; } = new AlgResultFOVDao();
    }
}
