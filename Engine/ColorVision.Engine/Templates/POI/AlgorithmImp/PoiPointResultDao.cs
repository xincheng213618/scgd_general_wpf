using ColorVision.Database;
using CVCommCore.CVAlgorithm;
using SqlSugar;

namespace ColorVision.Engine.Templates.POI.AlgorithmImp
{
    [SugarTable("t_scgd_algorithm_result_detail_poi_mtf")]
    public class PoiPointResultModel : EntityBase,IViewResult
    {
        [SugarColumn(ColumnName ="pid")]
        public int? Pid { get; set; }
        [SugarColumn(ColumnName ="poi_id")]
        public int? PoiId { get; set; }


        [SugarColumn(ColumnName ="poi_name")]
        public string? PoiName { get; set; }
        [SugarColumn(ColumnName ="poi_type")]
        public POIPointTypes PoiType { get; set; }
        [SugarColumn(ColumnName ="poi_x")]
        public int? PoiX { get; set; }
        [SugarColumn(ColumnName ="poi_y")]
        public int? PoiY { get; set; }
        [SugarColumn(ColumnName ="poi_width")]
        public int? PoiWidth { get; set; }
        [SugarColumn(ColumnName ="poi_height")]
        public int? PoiHeight { get; set; }

        [SugarColumn(ColumnName ="value")]
        public string? Value { get; set; }

    }

    public class PoiPointResultDao : BaseTableDao<PoiPointResultModel>
    {
        public static PoiPointResultDao Instance { get; set; } = new PoiPointResultDao();
    }
}
