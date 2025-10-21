using ColorVision.Database;
using SqlSugar;

namespace ColorVision.Engine.Templates.ImageCropping
{
    [SugarTable("t_scgd_algorithm_result_detail_image")]
    public class AlgResultImageModel : ViewEntity ,IViewResult
    {
        [SugarColumn(ColumnName ="pid")]
        public int? Pid { get; set; }

        [SugarColumn(ColumnName ="file_name")]
        public string? FileName { get; set; }

        [SugarColumn(ColumnName ="order_index")]
        public int? OrderIndex { get; set; }

        [SugarColumn(ColumnName ="file_info")]
        public string? FileInfo { get; set; }

    }

    public class AlgResultImageDao : BaseTableDao<AlgResultImageModel>
    {
        public static AlgResultImageDao Instance { get; set; } = new AlgResultImageDao();

    }
}
