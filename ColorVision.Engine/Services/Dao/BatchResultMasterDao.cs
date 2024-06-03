using ColorVision.Engine.MySql.ORM;
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
        public short ArchivedFlag { get; set; }
        public int TenantId { get; set; }
    }
    public class BatchResultMasterDao : BaseTableDao<BatchResultMasterModel>
    {
        public static BatchResultMasterDao Instance { get; set; } = new BatchResultMasterDao();

        public BatchResultMasterDao() : base("t_scgd_measure_batch", "id")
        {
        }

        public override BatchResultMasterModel GetModelFromDataRow(DataRow item)
        {
            BatchResultMasterModel model = new()
            {
                Id = item.Field<int>("id"),
                TId = item.Field<int?>("t_id"),
                Name = item.Field<string>("name"),
                Code = item.Field<string>("code"),
                TenantId = item.Field<int>("tenant_id"),
                TotalTime = item.Field<int?>("total_time"),
                Result = item.Field<string>("result"),
                CreateDate = item.Field<DateTime?>("create_date"),
                ArchivedFlag = item.Field<short>("archived_flag")
            };

            return model;
        }

        public override DataRow Model2Row(BatchResultMasterModel item, DataRow row)
        {
            if (item != null)
            {
                row["id"] = item.Id;
                row["name"] = item.Name;
                row["code"] = item.Code;
                row["t_id"] = item.TId;
                row["create_date"] = item.CreateDate;
                row["result"] = item.Result;
                row["total_time"] = item.TotalTime;
                row["tenant_id"] = item.TenantId;
                row["archived_flag"] = DataTableExtension.IsDBNull(item.ArchivedFlag);
            }
            return row;
        }

        public List<BatchResultMasterModel> ConditionalQuery(string batchCode)
        {
            Dictionary<string, object> keyValuePairs = new(0);
            keyValuePairs.Add("code", batchCode);
            return ConditionalQuery(keyValuePairs);
        }

        public BatchResultMasterModel? GetByCode(string code)
        {
            string sql = $"select * from {TableName} where code=@code";
            Dictionary<string, object> param = new()
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
