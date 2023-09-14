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
        public PoiResultDao() : base(string.Empty, "t_scgd_cfg_poi_reslut_detail", "id", true)
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

        public override DataRow Model2Row(PoiDetailModel item, DataRow row)
        {
            if (item != null)
            {
                if (item.Id > 0) row["id"] = item.Id;
                if (item.Name != null) row["name"] = item.Name;
                if (item.Type >= 0) row["pt_type"] = item.Type;
                if (item.Pid > 0) row["pid"] = item.Pid;
                if (item.PixWidth > 0) row["pix_width"] = item.PixWidth;
                if (item.PixHeight > 0) row["pix_height"] = item.PixHeight;
                if (item.PixX >= 0) row["pix_x"] = item.PixX;
                if (item.PixY >= 0) row["pix_y"] = item.PixY;
                //row["create_date"] = item.CreateDate;
                //row["is_enable"] = item.IsEnable;
                //row["is_delete"] = item.IsDelete;
                if (item.Remark != null) row["remark"] = item.Remark;
            }
            return row;
        }
    }
}
