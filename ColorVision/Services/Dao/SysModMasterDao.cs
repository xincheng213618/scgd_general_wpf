using System;
using System.Data;
using ColorVision.MySql;

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
    public class SysModMasterDao : BaseDaoMaster<SysModMasterModel>
    {
        public SysModMasterDao() : base(string.Empty, "t_scgd_sys_dictionary_mod_master", "id", true)
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
    }
}
