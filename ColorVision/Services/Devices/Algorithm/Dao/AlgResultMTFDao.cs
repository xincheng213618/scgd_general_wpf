﻿using System.Data;
using ColorVision.MySql;

namespace ColorVision.Services.Devices.Algorithm.Dao
{
    public class AlgResultMTFModel : PKModel
    {
        public int? Pid { get; set; }
        public int? PoiId { get; set; }

        public string? Value { get; set; }

        public string? PoiName { get; set; }
        public int? PoiType { get; set; }
        public int? PoiX { get; set; }
        public int? PoiY { get; set; }
        public int? PoiWidth { get; set; }
        public int? PoiHeight { get; set; }
    }
    public class AlgResultMTFDao : BaseTableDao<AlgResultMTFModel>
    {
        public AlgResultMTFDao() : base("t_scgd_algorithm_result_detail_poi_mtf", "id")
        {
        }

        public override AlgResultMTFModel GetModelFromDataRow(DataRow item)
        {
            AlgResultMTFModel model = new AlgResultMTFModel
            {
                Id = item.Field<int>("id"),
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
