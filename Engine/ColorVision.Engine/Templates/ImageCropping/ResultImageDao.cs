using ColorVision.Engine.Interfaces;
using ColorVision.Engine.MySql.ORM;

namespace ColorVision.Engine.Templates.ImageCropping
{
    [Table("t_scgd_algorithm_result_detail_image")]
    public class ResultImageModel : VPKModel,IViewResult
    {
        [Column("pid")]
        public int? Pid { get; set; }

        [Column("file_name")]
        public string? FileName { get; set; }

        [Column("order_index")]
        public int? OrderIndex { get; set; }

        [Column("file_info")]
        public string? FileInfo { get; set; }

    }

    public class ResultImageDao : BaseTableDao<ResultImageModel>
    {
        public static ResultImageDao Instance { get; set; } = new ResultImageDao();

        public ResultImageDao() : base("t_scgd_algorithm_result_detail_image", "id")
        {
        }
    }
}
