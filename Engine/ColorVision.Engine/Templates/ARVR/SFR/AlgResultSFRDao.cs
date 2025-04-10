using ColorVision.Engine.Interfaces;
using ColorVision.Engine.MySql.ORM;

namespace ColorVision.Engine.Templates.SFR
{
    [Table("t_scgd_algorithm_result_detail_sfr")]
    public class AlgResultSFRModel : PKModel,IViewResult
    {
        [Column("pid")]
        public int? Pid { get; set; }
        [Column("roi_x")]
        public int? RoiX { get; set; }
        [Column("roi_y")]
        public int? RoiY { get; set; }
        [Column("roi_width")]
        public int? RoiWidth { get; set; }
        [Column("roi_height")]
        public int? RoiHeight { get; set; }
        [Column("gamma")]
        public double? Gamma { get; set; }
        [Column("pdfrequency")]
        public string? Pdfrequency { get; set; }
        [Column("pdomain_sampling_data")]
        public string? PdomainSamplingData { get; set; }
    }


    public class AlgResultSFRDao : BaseTableDao<AlgResultSFRModel>
    {
        public static AlgResultSFRDao Instance { get; set; } = new AlgResultSFRDao();

        public AlgResultSFRDao() : base("t_scgd_algorithm_result_detail_sfr", "id")
        {
        }
    }
}
