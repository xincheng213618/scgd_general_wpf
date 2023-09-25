using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ColorVision.MySql.DAO
{
    public class PoiResultModel : PKModel
    {
        public int? BatchId { get; set; }
        public int? Pid { get; set; }
        public int? PoiId { get; set; }

        public string? Value { get; set; }
        public DateTime? CreateDate { get; set; } = DateTime.Now;
    }
    public class PoiResultDao : BaseDaoMaster<PoiResultModel>
    {
        public PoiResultDao() : base(string.Empty, "t_scgd_cfg_poi_result_detail", "id", false)
        {
        }

        public List<PoiResultModel> selectBySN(string sn)
        {
            List<PoiResultModel> list = new List<PoiResultModel>();
            DataTable d_info = GetTableAllBySN(sn);
            foreach (var item in d_info.AsEnumerable())
            {
                PoiResultModel? model = GetModel(item);
                if (model != null)
                {
                    list.Add(model);
                }
            }
            return list;
        }


        private DataTable GetTableAllBySN(string bid)
        {
            string sql = $"select * from {GetTableName()} where batch_id='{bid}'";
            DataTable d_info = GetData(sql);
            return d_info;
        }

        public override PoiResultModel GetModel(DataRow item)
        {
            PoiResultModel model = new PoiResultModel
            {
                Id = item.Field<int>("id"),
                BatchId = item.Field<int?>("batch_id"),
                Pid = item.Field<int?>("pid"),
                PoiId = item.Field<int?>("poi_id"),
                Value = item.Field<string>("value"),
            };

            return model;
        }
    }
}
