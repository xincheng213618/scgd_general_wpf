using ColorVision.MySql.ORM;
using System;
using System.Data;

namespace ColorVision.Services.Dao
{
    public class SysModMasterModel : PKModel
    {
        public string? Name { get; set; }
        public string? Code { get; set; }
        public DateTime? CreateDate { get; set; } = DateTime.Now;
        public bool IsEnable { get; set; } = true;
        public bool? IsDelete { get; set; } = false;
        public string? Remark { get; set; }
        public int TenantId { get; set; }
    }

    public class SysModMasterDao : BaseTableDao<SysModMasterModel>
    {
        public static SysModMasterDao Instance { get; set; } = new SysModMasterDao();
        public SysModMasterDao() : base("t_scgd_sys_dictionary_mod_master", "id")
        {
        }

        public override SysModMasterModel GetModelFromDataRow(DataRow item)
        {
            SysModMasterModel model = new SysModMasterModel
            {
                Id = item.Field<int>("id"),
                Name = item.Field<string>("name"),
                Code = item.Field<string>("code"),
                CreateDate = item.Field<DateTime?>("create_date"),
                IsEnable = item.Field<bool>("is_enable"),
                IsDelete = item.Field<bool>("is_delete"),
                Remark = item.Field<string?>("remark"),
            };

            return model;
        }
        public override DataRow Model2Row(SysModMasterModel item, DataRow row)
        {
            if (item != null)
            {
                row["id"] = item.Id;
                row["name"] = item.Name;
                row["code"] = item.Code;
                row["create_date"] = item.CreateDate;
                row["is_enable"] = item.IsEnable;
                row["is_delete"] = item.IsDelete;
                row["remark"] = item.Remark;   
            }
            return row;
        }
    }
}
