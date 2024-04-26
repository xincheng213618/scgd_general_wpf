using ColorVision.MySql;
using ColorVision.MySql.ORM;
using ColorVision.Services.Dao;
using System;
using System.Collections.Generic;
using System.Data;

namespace ColorVision.Services.DAO.Validate
{
    public class ValidateTemplateMasterModel : PKModel
    {
        public ValidateTemplateMasterModel() 
        {

        }

        public int? TId { get; set; }
        public string? Name { get; set; }
        public string? Code { get; set; }
        public DateTime? CreateDate { get; set; }
        public int? TotalTime { get; set; }
        public string? Result { get; set; }
        public int TenantId { get; set; }
    }

    public class ValidateTemplateMasterDao : BaseTableDao<BatchResultMasterModel>
    {
        public static ValidateTemplateMasterDao Instance { get; set; } = new ValidateTemplateMasterDao();

        public ValidateTemplateMasterDao() : base("t_scgd_measure_batch", "id")
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
    }
}
