using ColorVision.Engine.MySql.ORM;
using CVCommCore.CVAlgorithm;
using System.Data;

namespace ColorVision.Engine.Services.Devices.Algorithm.Dao
{
    public class POIPointResultModel : PKModel
    {
        [Column("pid")]
        public int? Pid { get; set; }
        [Column("poi_id")]
        public int? PoiId { get; set; }
        [Column("value")]
        public string? Value { get; set; }
        [Column("poi_name")]
        public string? PoiName { get; set; }
        [Column("poi_type")]
        public POIPointTypes PoiType { get; set; }
        [Column("poi_x")]
        public int? PoiX { get; set; }
        [Column("poi_y")]
        public int? PoiY { get; set; }
        [Column("poi_width")]
        public int? PoiWidth { get; set; }
        [Column("poi_height")]
        public int? PoiHeight { get; set; }
        [Column("validate_result")]
        public string? ValidateResult { get; set; }
    }

    public class POIPointResultDao : BaseTableDao<POIPointResultModel>
    {
        public static POIPointResultDao Instance { get; set; } = new POIPointResultDao();

        public POIPointResultDao() : base("t_scgd_algorithm_result_detail_poi_mtf", "id")
        {
        }
    }
}
