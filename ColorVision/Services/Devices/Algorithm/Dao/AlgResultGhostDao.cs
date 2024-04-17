using System.Data;
using ColorVision.MySql;
using ColorVision.MySql.ORM;

namespace ColorVision.Services.Devices.Algorithm.Dao
{
    public class AlgResultGhostModel : PKModel
    {
        public int Pid { get; set; }
        public int Radius { get; set; }
        public int Rows { get; set; }
        public int Cols { get; set; }
        public float RatioH { get; set; }
        public float RatioL { get; set; }

        public string LEDCenters { get; set; }
        public string LEDPixels { get; set; }
        public string LEDBlobGray { get; set; }
        public string GhostAverageGray { get; set; }
        public string SingleLedPixelNum { get; set; }
        public string SingleGhostPixelNum { get; set; }
        public string GhostPixels { get; set; }
    }
    public class AlgResultGhostDao : BaseTableDao<AlgResultGhostModel>
    {
        public static AlgResultGhostDao Instance { get; set; }= new AlgResultGhostDao();

        public AlgResultGhostDao() : base("t_scgd_algorithm_result_detail_ghost", "id")
        {
        }

        public override AlgResultGhostModel GetModelFromDataRow(DataRow item)
        {
            AlgResultGhostModel model = new AlgResultGhostModel
            {
                Id = item.Field<int>("id"),
                Pid = item.Field<int?>("pid") ?? -1,
                Radius = item.Field<int?>("radius") ?? -1,
                Rows = item.Field<int?>("rows") ?? -1,
                Cols = item.Field<int?>("cols") ?? -1,
                RatioH = item.Field<float?>("ratio_h") ?? -1,
                RatioL = item.Field<float?>("ratio_l") ?? -1,
                LEDCenters = item.Field<string>("led_centers") ?? string.Empty,
                LEDPixels = item.Field<string>("led_pixels") ?? string.Empty,
                LEDBlobGray = item.Field<string>("led_blob_gray") ?? string.Empty,
                GhostAverageGray = item.Field<string>("ghost_average_gray") ?? string.Empty,
                SingleLedPixelNum = item.Field<string>("single_led_pixel_num") ?? string.Empty,
                SingleGhostPixelNum = item.Field<string>("single_ghost_pixel_num") ?? string.Empty,
                GhostPixels = item.Field<string>("ghost_pixels") ?? string.Empty,
            };
            return model;
        }

        public override DataRow Model2Row(AlgResultGhostModel item, DataRow row)
        {
            if (item != null)
            {
                if (item.Id > 0) row["id"] = item.Id;
                row["pid"] = item.Pid;
                row["radius"] = item.Radius;
                row["rows"] = item.Rows;
                row["cols"] = item.Cols;
                row["ratio_h"] = item.RatioH;
                row["ratio_l"] = item.RatioL;
                row["led_centers"] = item.LEDCenters;
                row["led_pixels"] = item.LEDPixels;
                row["led_blob_gray"] = item.LEDBlobGray;
                row["ghost_average_gray"] = item.GhostAverageGray;
                row["single_led_pixel_num"] = item.SingleLedPixelNum;
                row["single_ghost_pixel_num"] = item.SingleGhostPixelNum;
                row["ghost_pixels"] = item.GhostPixels;
            }
            return row;
        }
    }
}
