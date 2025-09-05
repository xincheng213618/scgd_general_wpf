using ColorVision.Database;
using SqlSugar;

namespace ColorVision.Engine.Templates.SFR
{
    [SugarTable("t_scgd_algorithm_result_detail_sfr")]
    public class AlgResultSFRModel : EntityBase,IViewResult, IInitTables
    {
        [SugarColumn(ColumnName ="pid")]
        public int? Pid { get; set; }
        [SugarColumn(ColumnName ="roi_x")]
        public int? RoiX { get; set; }
        [SugarColumn(ColumnName ="roi_y")]
        public int? RoiY { get; set; }
        [SugarColumn(ColumnName ="roi_width")]
        public int? RoiWidth { get; set; }
        [SugarColumn(ColumnName ="roi_height")]
        public int? RoiHeight { get; set; }
        [SugarColumn(ColumnName ="gamma")]
        public double? Gamma { get; set; }
        [SugarColumn(ColumnName ="pdfrequency")]
        public string? Pdfrequency { get; set; }
        [SugarColumn(ColumnName ="pdomain_sampling_data")]
        public string? PdomainSamplingData { get; set; }
    }


    public class AlgResultSFRDao : BaseTableDao<AlgResultSFRModel>
    {
        public static AlgResultSFRDao Instance { get; set; } = new AlgResultSFRDao();
    }
}
