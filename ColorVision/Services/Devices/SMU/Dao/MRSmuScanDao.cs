using ColorVision.MySql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ColorVision.Services.Devices.SMU.Dao
{
    public class SmuScanModel : PKModel
    {
        public string? DeviceCode { get; set; }

        /// <summary>
        /// SN
        /// </summary>
        public int? BatchId { get; set; }
        /// <summary>
        /// 是否电压
        /// </summary>
        public bool IsSourceV { get; set; }
        /// <summary>
        /// 源值
        /// </summary>
        public float SrcBegin { get; set; }
        /// <summary>
        /// 限值
        /// </summary>
        public float SrcEnd { get; set; }
        /// <summary>
        /// 电压结果
        /// </summary>
        public string? VResult { get; set; }
        /// <summary>
        /// 电流结果
        /// </summary>
        public string? IResult { get; set; }

        /// <summary>
        /// 点数
        /// </summary>
        public int Points { get; set; }


        public DateTime? CreateDate { get; set; }
    }

    public class MRSmuScanDao : BaseDaoMaster<SmuScanModel>
    {
        public MRSmuScanDao() : base(string.Empty, "t_scgd_measure_result_smu_scan", "id", false)
        {

        }

        public List<SmuScanModel> ConditionalQuery(string id, string batchid)
        {
            Dictionary<string, object> keyValuePairs = new Dictionary<string, object>();
            keyValuePairs.Add("id", id);
            keyValuePairs.Add("batch_id", batchid);
            return ConditionalQuery(keyValuePairs);
        }

        public override DataRow Model2Row(SmuScanModel item, DataRow row)
        {
            if (item != null)
            {
                row["device_code"] = item.DeviceCode;
                row["batch_id"] = item.BatchId;
                row["is_source_v"] = item.IsSourceV;
                row["src_begin"] = item.SrcBegin;
                row["src_end"] = item.SrcEnd;
                row["v_result"] = item.VResult;
                row["i_result"] = item.IResult;
                row["points"] = item.Points;
                row["create_date"] = item.CreateDate;
            }
            return row;
        }

        public List<SmuScanModel> selectBySN(string sn)
        {
            List<SmuScanModel> list = new List<SmuScanModel>();
            DataTable d_info = GetTableAllBySN(sn);
            foreach (var item in d_info.AsEnumerable())
            {
                SmuScanModel? model = GetModel(item);
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

        public override SmuScanModel GetModel(DataRow item)
        {
            SmuScanModel model = new SmuScanModel
            {
                Id = item.Field<int>("id"),
                DeviceCode = item.Field<string?>("device_code"),
                BatchId = item.Field<int?>("batch_id"),
                IsSourceV = item.Field<bool>("is_source_v"),
                SrcEnd = item.Field<float>("src_end"),
                SrcBegin = item.Field<float>("src_begin"),
                VResult = item.Field<string?>("v_result"),
                IResult = item.Field<string?>("i_result"),
                Points = item.Field<int>("points"),
                CreateDate = item.Field<DateTime?>("create_date"),
            };

            return model;
        }
    }
}
