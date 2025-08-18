using ColorVision.Engine.Abstractions;
using ColorVision.Engine.MySql;
using ColorVision.Engine.MySql.ORM;
using Newtonsoft.Json;
using SqlSugar;
using System.Collections.Generic;

namespace ColorVision.Engine.Templates.Ghost
{
    /// <summary>
    /// 专门位鬼影设计的类
    /// </summary>
    public sealed class Point1
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    [SugarTable("t_scgd_algorithm_result_detail_ghost")]
    public class AlgResultGhostModel : VPKModel, IViewResult, IInitTables
    {
        [SugarColumn(ColumnName ="pid")]
        public int Pid { get; set; }
        [SugarColumn(ColumnName ="rows")]
        public int Rows { get; set; }
        [SugarColumn(ColumnName ="cols")]
        public int Cols { get; set; }
        [SugarColumn(ColumnName ="radius")]
        public int Radius { get; set; }
        [SugarColumn(ColumnName ="ratio_h")]
        public float RatioH { get; set; }
        [SugarColumn(ColumnName ="ratio_l")]
        public float RatioL { get; set; }

        [SugarColumn(ColumnName ="led_centers", ColumnDescription = "检出的鬼影点阵质心坐标集")]
        public string LEDCenters { get; set; }
        [SugarColumn(ColumnName ="led_pixels", ColumnDescription = "所有点阵轮廓的坐标集")]
        public string LEDPixels { get; set; }
        [SugarColumn(ColumnName ="led_blob_gray", ColumnDescription = "检出光斑的灰度均值集")]
        public string LEDBlobGray { get; set; }
        [SugarColumn(ColumnName ="ghost_average_gray", ColumnDescription = "检出鬼影区域的灰度均值集")]
        public string GhostAverageGray { get; set; }
        [SugarColumn(ColumnName ="single_led_pixel_num", ColumnDescription = "包含的鬼影集数量")]
        public string SingleLedPixelNum { get; set; }
        [SugarColumn(ColumnName ="single_ghost_pixel_num", ColumnDescription = "包含的点阵数量")]
        public string SingleGhostPixelNum { get; set; }
        [SugarColumn(ColumnName ="ghost_pixels", ColumnDescription = "所有鬼影轮廓的坐标集")]
        public string GhostPixels { get; set; }

        public List<List<Point1>>? GhostPixel => JsonConvert.DeserializeObject<List<List<Point1>>>(GhostPixels);

        public List<List<Point1>>? LedPixel => JsonConvert.DeserializeObject<List<List<Point1>>>(LEDPixels);


    }


    public class AlgResultGhostDao : BaseTableDao<AlgResultGhostModel>
    {
        public static AlgResultGhostDao Instance { get; set; } = new AlgResultGhostDao();

    }
}
