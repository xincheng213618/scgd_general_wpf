#pragma warning disable CS8601
using ColorVision.Engine.MySql.ORM;
using System;
using System.Data;

namespace ColorVision.Services.Templates.POI.Validate.Dao
{
    public class ValidateTemplateMasterModel : PKModel
    {
        public int? DId { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public DateTime? CreateDate { get; set; } = DateTime.Now;
        public string Remark { get; set; }
        public int TenantId { get; set; }
    }

    public class ValidateTemplateMasterDao : BaseTableDao<ValidateTemplateMasterModel>
    {
        public static ValidateTemplateMasterDao Instance { get; set; } = new ValidateTemplateMasterDao();

        public ValidateTemplateMasterDao() : base("t_scgd_rule_validate_template_master", "id")
        {
        }
        public override ValidateTemplateMasterModel GetModelFromDataRow(DataRow item)
        {
            ValidateTemplateMasterModel model = new()
            {
                Id = item.Field<int>("id"),
                DId = item.Field<int?>("dic_pid"),
                Name = item.Field<string>("name"),
                Code = item.Field<string>("code"),
                TenantId = item.Field<int>("tenant_id"),
                Remark = item.Field<string>("remark"),
                CreateDate = item.Field<DateTime?>("create_date"),
            };

            return model;
        }

        public override DataRow Model2Row(ValidateTemplateMasterModel item, DataRow row)
        {
            if (item != null)
            {
                if (item.Id > 0) row["id"] = item.Id;
                row["name"] = DataTableExtension.IsDBNull(item.Name);
                row["dic_pid"] = item.DId;
                row["code"] = item.Code;
                row["create_date"] = item.CreateDate;
                row["tenant_id"] = item.TenantId;
                row["remark"] = DataTableExtension.IsDBNull(item.Remark);
            }
            return row;
        }
    }
}
