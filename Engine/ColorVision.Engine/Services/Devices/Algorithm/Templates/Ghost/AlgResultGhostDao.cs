using ColorVision.Engine.MySql.ORM;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.Ghost
{
    [Table("t_scgd_algorithm_result_detail_ghost", PrimaryKey ="id")]
    public class AlgResultGhostModel : PKModel
    {
        [Column("pid")]
        public int Pid { get; set; }
        [Column("rows")]
        public int Rows { get; set; }
        [Column("cols")]
        public int Cols { get; set; }
        [Column("radius")]
        public int Radius { get; set; }
        [Column("ratio_h")]
        public float RatioH { get; set; }
        [Column("ratio_l")]
        public float RatioL { get; set; }

        [Column("led_centers", Comment = "检出的鬼影点阵质心坐标集")]
        public string LEDCenters { get; set; }
        [Column("led_pixels", Comment = "所有点阵轮廓的坐标集")]
        public string LEDPixels { get; set; }
        [Column("led_blob_gray", Comment = "检出光斑的灰度均值集")]
        public string LEDBlobGray { get; set; }
        [Column("ghost_average_gray", Comment = "检出鬼影区域的灰度均值集")]
        public string GhostAverageGray { get; set; }
        [Column("single_led_pixel_num", Comment = "包含的鬼影集数量")]
        public string SingleLedPixelNum { get; set; }
        [Column("single_ghost_pixel_num", Comment = "包含的点阵数量")]
        public string SingleGhostPixelNum { get; set; }
        [Column("ghost_pixels", Comment = "所有鬼影轮廓的坐标集")]
        public string GhostPixels { get; set; }
    }


    public class AlgResultGhostDao : BaseTableDao<AlgResultGhostModel>
    {
        public static AlgResultGhostDao Instance { get; set; } = new AlgResultGhostDao();

        public AlgResultGhostDao() : base("t_scgd_algorithm_result_detail_ghost", "id")
        {
        }
    }
}
