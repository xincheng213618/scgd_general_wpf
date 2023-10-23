using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ColorVision.MySql.DAO
{
    public class POIPointResultModel : PKModel
    {
        public int? BatchId { get; set; }
        public string? BatchCode { get; set; }
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
        public POIPointResultDao() : base(string.Empty, "v_scgd_algorithm_poi_detail_result", "id", false)
        {
        }

        public List<POIPointResultModel> selectBySN(string sn)
        {
            List<POIPointResultModel> list = new List<POIPointResultModel>();
            DataTable d_info = GetTableAllBySN(sn);
            foreach (var item in d_info.AsEnumerable())
            {
                POIPointResultModel? model = GetModel(item);
                if (model != null)
                {
                    list.Add(model);
                }
            }
            return list;
        }


        private DataTable GetTableAllBySN(string sn)
        {
            string sql = $"select * from {GetTableName()} where batch_code='{sn}'";
            DataTable d_info = GetData(sql);
            return d_info;
        }

        public override POIPointResultModel GetModel(DataRow item)
        {
            POIPointResultModel model = new POIPointResultModel
            {
                Id = item.Field<int>("id"),
                BatchId = item.Field<int?>("batch_id"),
                BatchCode = item.Field<string>("batch_code"),
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
