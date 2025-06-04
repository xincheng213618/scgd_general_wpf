using ColorVision.Engine.MySql.ORM;

namespace ColorVision.Engine.Templates.FOV
{
    [Table("t_scgd_algorithm_result_detail_fov")]
    public class AlgResultFOVModel : PKModel
    {
        [Column("pid")]
        public int? Pid { get; set; }
        [Column("pattern")]
        public int? Pattern { get; set; }
        [Column("type")]
        public int? Type { get; set; }
        [Column("radio")]
        public double? Radio { get; set; }
        [Column("camera_degrees")]
        public double? CameraDegrees { get; set; }
        [Column("dist")]
        public double? Dist { get; set; }
        [Column("threshold")]
        public int? Threshold { get; set; }
        [Column("degrees")]
        public double? Degrees { get; set; }
    }


    public class AlgResultFOVDao : BaseTableDao<AlgResultFOVModel>
    {
        public static AlgResultFOVDao Instance { get; set; } = new AlgResultFOVDao();
    }
}
