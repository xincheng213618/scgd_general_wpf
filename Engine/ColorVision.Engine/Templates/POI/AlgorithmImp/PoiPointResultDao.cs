using ColorVision.Engine.Abstractions;
using ColorVision.Engine.MySql.ORM;
using CVCommCore.CVAlgorithm;
using SqlSugar;

namespace ColorVision.Engine.Templates.POI.AlgorithmImp
{
    [SugarTable("t_scgd_algorithm_result_detail_poi_mtf")]
    public class PoiPointResultModel : PKModel,IViewResult
    {
        [Column("pid")]
        public int? Pid { get; set; }
        [Column("poi_id")]
        public int? PoiId { get; set; }


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

        [Column("value")]
        public string? Value { get; set; }

    }

    public class PoiPointResultDao : BaseTableDao<PoiPointResultModel>
    {
        public static PoiPointResultDao Instance { get; set; } = new PoiPointResultDao();
    }
}
