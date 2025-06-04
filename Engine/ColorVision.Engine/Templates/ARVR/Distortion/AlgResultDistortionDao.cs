using ColorVision.Engine.MySql.ORM;
using CVCommCore.CVAlgorithm;

namespace ColorVision.Engine.Templates.Distortion
{
    [Table("t_scgd_algorithm_result_detail_distortion")]
    public class AlgResultDistortionModel : PKModel
    {
        [Column("pid")]
        public int? Pid { get; set; }
        [Column("type")]
        public DistortionType Type { get; set; }
        [Column("layout_type")]
        public DisLayoutType LayoutType { get; set; }
        [Column("slope_type")]
        public DisSlopeType SlopeType { get; set; }
        [Column("corner_type")]
        public DisCornerType CornerType { get; set; }
        [Column("point_x")]
        public double PointX { get; set; }
        [Column("point_y")]
        public double PointY { get; set; }
        [Column("max_ratio")]
        public double MaxRatio { get; set; }
        [Column("rotation_angle")]
        public double RotationAngle { get; set; }
        [Column("final_points")]
        public string? FinalPoints { get; set; }
    }


    public class AlgResultDistortionDao : BaseTableDao<AlgResultDistortionModel>
    {
        public static AlgResultDistortionDao Instance { get; set; } = new AlgResultDistortionDao();
    }
}
