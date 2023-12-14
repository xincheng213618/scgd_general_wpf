using System.Data;

namespace ColorVision.MySql.DAO
{
    public class POIPointResultModel : PKModel
    {
        //public int? BatchId { get; set; }
        //public string? BatchCode { get; set; }
        public int? Pid { get; set; }
        public int? PoiId { get; set; }

        public string? Value { get; set; }

        public string? PoiName { get; set; }
        public int? PoiType { get; set; }
        public int? PoiX { get; set; }
        public int? PoiY { get; set; }
        public int? PoiWidth { get; set; }
        public int? PoiHeight { get; set; }
        //public DateTime? CreateDate { get; set; } = DateTime.Now;
    }
    public class POIPointResultDao : BaseDaoMaster<POIPointResultModel>
    {
        public POIPointResultDao() : base(string.Empty, "t_scgd_algorithm_result_detail_poi_mtf", "id", false)
        {
        }

        public override POIPointResultModel GetModel(DataRow item)
        {
            POIPointResultModel model = new POIPointResultModel
            {
                Id = item.Field<int>("id"),
                //BatchId = item.Field<int?>("batch_id"),
                //BatchCode = item.Field<string>("batch_code"),
                Pid = item.Field<int?>("pid"),
                PoiId = item.Field<int?>("poi_id"),
                Value = item.Field<string>("value"),
                PoiName = item.Field<string>("poi_name"),
                PoiType = item.Field<sbyte>("poi_type"),
                PoiWidth = item.Field<int>("poi_width"),
                PoiHeight = item.Field<int>("poi_height"),
                PoiX = item.Field<int>("poi_x"),
                PoiY = item.Field<int>("poi_y"),
            };

            return model;
        }
    }
}
