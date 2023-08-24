using NPOI.SS.Formula.Functions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MySql.DAO
{
    public class BatchResultMasterModel : PKModel
    {
        public BatchResultMasterModel()
        {

        }
        public BatchResultMasterModel(string sn, int tenantId)
        {
            Name = sn;
            Code = sn;
            TenantId = tenantId;
            TId = -1;
        }

        public int? TId { get; set; }
        public string? Name { get; set; }
        public string? Code { get; set; }
        public DateTime? CreateDate { get; set; } = DateTime.Now;
        public int TenantId { get; set; }
    }
    public class BatchResultMasterDao : BaseDaoMaster<BatchResultMasterModel>
    {
        public BatchResultMasterDao() : base(string.Empty, "t_scgd_measure_batch", "id", false)
        {
        }

        public override BatchResultMasterModel GetModel(DataRow item)
        {
            BatchResultMasterModel model = new BatchResultMasterModel
            {
                Id = item.Field<int>("id"),
                TId = item.Field<int?>("t_id"),
                Name = item.Field<string>("name"),
                Code = item.Field<string>("code"),
                TenantId = item.Field<int>("tenant_id"),
                CreateDate = item.Field<DateTime?>("create_date"),
            };

            return model;
        }

        public override DataRow Model2Row(BatchResultMasterModel item, DataRow row)
        {
            if (item != null)
            {
                if (item.Id > 0) row["id"] = item.Id;
                if (item.Name != null) row["name"] = item.Name;
                if (item.Code != null) row["code"] = item.Code;
                if (item.TId >= 0) row["t_id"] = item.TId;
                row["create_date"] = item.CreateDate;
                row["tenant_id"] = item.TenantId;
            }
            return row;
        }

        public BatchResultMasterModel? GetByCode(string code)
        {
            string sql = $"select * from {GetTableName()} where code=@code";
            Dictionary<string, object> param = new Dictionary<string, object>
            {
                { "code", code }
            };
            DataTable d_info = GetData(sql, param);
            return (d_info !=null && d_info.Rows.Count == 1) ? GetModel(d_info.Rows[0]) : default;
        }
    }
}
