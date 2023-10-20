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

        public string? Name { get; set; }
        public int? Type { get; set; }
        public int? PixX { get; set; }
        public int? PixY { get; set; }
        public int? PixWidth { get; set; }
        public int? PixHeight { get; set; }
        public DateTime? CreateDate { get; set; } = DateTime.Now;
    }
    public class POIPointResultDao : BaseDaoMaster<POIPointResultModel>
    {
        public POIPointResultDao() : base(string.Empty, "v_scgd_poi_result", "id", false)
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
                Name = item.Field<string>("name"),
                Type = item.Field<sbyte>("pt_type"),
                PixWidth = item.Field<int>("pix_width"),
                PixHeight = item.Field<int>("pix_height"),
                PixX = item.Field<int>("pix_x"),
                PixY = item.Field<int>("pix_y"),
            };

            return model;
        }
    }
}
