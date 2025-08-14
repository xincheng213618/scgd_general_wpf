using ColorVision.Engine.MySql.ORM;
using System;
using System.Collections.Generic;
using System.Data;

namespace ColorVision.Engine.Services.Dao
{
    [Table("t_scgd_measure_batch")]
    public class BatchResultMasterModel : PKModel
    {
        public BatchResultMasterModel() 
        {
            Name = null;
            Code = null;
            TenantId = -1;
            TId = -1;
            CreateDate = DateTime.Now;
            TotalTime = 0;
        }

        [Column("t_id")]
        public int? TId { get; set; }
        [Column("name")]
        public string? Name { get; set; }
        [Column("code")]
        public string? Code { get; set; }
        [Column("create_date")]
        public DateTime? CreateDate { get; set; }
        [Column("total_time")]
        public int? TotalTime { get; set; }
        [Column("result")]
        public string? Result { get; set; }
        [Column("archived_flag")]
        public short ArchivedFlag { get; set; }
        [Column("tenant_id")]
        public int TenantId { get; set; }
    }



    public class BatchResultMasterDao : BaseTableDao<BatchResultMasterModel>
    {
        public static BatchResultMasterDao Instance { get; set; } = new BatchResultMasterDao();

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

    }
}
