using ColorVision.Database;
using CVCommCore.CVAlgorithm;
using SqlSugar;

namespace ColorVision.Engine.Templates.Distortion
{
    [SugarTable("t_scgd_algorithm_result_detail_distortion")]
    public class AlgResultDistortionModel : PKModel, IInitTables
    {
        [SugarColumn(ColumnName ="pid")]
        public int? Pid { get; set; }
        [SugarColumn(ColumnName ="type")]
        public DistortionType Type { get; set; }
        [SugarColumn(ColumnName ="layout_type")]
        public DisLayoutType LayoutType { get; set; }
        [SugarColumn(ColumnName ="slope_type")]
        public DisSlopeType SlopeType { get; set; }
        [SugarColumn(ColumnName ="corner_type")]
        public DisCornerType CornerType { get; set; }
        [SugarColumn(ColumnName ="point_x")]
        public double PointX { get; set; }
        [SugarColumn(ColumnName ="point_y")]
        public double PointY { get; set; }
        [SugarColumn(ColumnName ="max_ratio")]
        public double MaxRatio { get; set; }
        [SugarColumn(ColumnName ="rotation_angle")]
        public double RotationAngle { get; set; }
        [SugarColumn(ColumnName ="final_points")]
        public string? FinalPoints { get; set; }
    }


    public class AlgResultDistortionDao : BaseTableDao<AlgResultDistortionModel>
    {
        public static AlgResultDistortionDao Instance { get; set; } = new AlgResultDistortionDao();
    }
}
