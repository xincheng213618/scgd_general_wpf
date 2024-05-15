using ColorVision.MySql.ORM;
using System;
using System.Data;

namespace ColorVision.Services.Dao
{
    public class MeasureMasterModel : PKModel
    {
        public MeasureMasterModel()
        {

        }
        public MeasureMasterModel(string name, int tenantId)
        {
            Name = name;
            TenantId = tenantId;
        }

        public string? Name { get; set; }
        public DateTime? CreateDate { get; set; } = DateTime.Now;
        public bool IsEnable { get; set; } = true;
        public bool? IsDelete { get; set; } = false;
        public string? Remark { get; set; }
        public int TenantId { get; set; }
    }
    public class MeasureMasterDao : BaseTableDao<MeasureMasterModel>
    {
        public static MeasureMasterDao Instance { get; set; } = new MeasureMasterDao();

        public MeasureMasterDao() : base("t_scgd_measure_template_master", "id")
        {
        }

        public override MeasureMasterModel GetModelFromDataRow(DataRow item)
        {
            MeasureMasterModel model = new()
            {
                Id = item.Field<int>("id"),
                Name = item.Field<string>("name"),
                CreateDate = item.Field<DateTime?>("create_date"),
                IsEnable = item.Field<bool>("is_enable"),
                IsDelete = item.Field<bool>("is_delete"),
            };

            return model;
        }

        public override DataRow Model2Row(MeasureMasterModel item, DataRow row)
        {
            if (item != null)
            {
                if (item.Id > 0) row["id"] = item.Id;
                if (item.Name != null) row["name"] = item.Name;
                row["create_date"] = item.CreateDate;
                row["remark"] = DataTableExtension.IsDBNull(item.Remark);
                row["tenant_id"] = item.TenantId;
            }
            return row;
        }
    }
}
