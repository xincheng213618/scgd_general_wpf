using ColorVision.MySql;
using ColorVision.Services.Dao;
using System;
using System.Collections.Generic;
using System.Data;

namespace ColorVision.Services.DAO
{
    public class BatchResultMasterModel : PKModel
    {
        public BatchResultMasterModel() : this(null,-1)
        {

        }
        public BatchResultMasterModel(string? sn, int tenantId)
        {
            Name = sn;
            Code = sn;
            TenantId = tenantId;
            TId = -1;
            CreateDate = DateTime.Now;
            TotalTime = 0;
        }

        public int? TId { get; set; }
        public string? Name { get; set; }
        public string? Code { get; set; }
        public DateTime? CreateDate { get; set; }
        public int? TotalTime { get; set; }
        public string? Result { get; set; }
        public int TenantId { get; set; }
    }
    public class BatchResultMasterDao : BaseDaoMaster<BatchResultMasterModel>
    {
        public BatchResultMasterDao() : base(string.Empty, "t_scgd_measure_batch", "id", false)
        {
        }

        public override BatchResultMasterModel GetModelFromDataRow(DataRow item)
        {
            BatchResultMasterModel model = new BatchResultMasterModel
            {
                Id = item.Field<int>("id"),
                TId = item.Field<int?>("t_id"),
                Name = item.Field<string>("name"),
                Code = item.Field<string>("code"),
                TenantId = item.Field<int>("tenant_id"),
                TotalTime = item.Field<int?>("total_time"),
                Result = item.Field<string>("result"),
                CreateDate = item.Field<DateTime?>("create_date"),
            };

            return model;
        }

        public override DataRow Model2Row(BatchResultMasterModel item, DataRow row)
        {
            if (item != null)
            {
                row["id"] = item.Id;
                row["name"] = item.Name;
                row["t_id"] = item.TId;
                row["create_date"] = item.CreateDate;
                row["result"] = item.Result;
                row["total_time"] = item.TotalTime;
                row["tenant_id"] = item.TenantId;
            }
            return row;
        }

        public List<BatchResultMasterModel> ConditionalQuery(string batchCode)
        {
            Dictionary<string, object> keyValuePairs = new Dictionary<string, object>(0);
            keyValuePairs.Add("code", batchCode);
            return ConditionalQuery(keyValuePairs);
        }
        public BatchResultMasterModel? GetByCode(string code)
        {
            string sql = $"select * from {GetTableName()} where code=@code";
            Dictionary<string, object> param = new Dictionary<string, object>
            {
                { "code", code }
            };
            DataTable d_info = GetData(sql, param);
            return (d_info !=null && d_info.Rows.Count == 1) ? GetModelFromDataRow(d_info.Rows[0]) : default;
        }

        public int UpdateEnd(string bid, int totalTime, string result)
        {
            int result_code = (result == "Completed") ? 0 : -1;
            string sql = $"update {TableName} set result='{result}',result_code={result_code},total_time={totalTime} where code='{bid}'";
            return ExecuteNonQuery(sql);
        }
    }
}
