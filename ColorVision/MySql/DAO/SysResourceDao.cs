using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MySql.DAO
{
    public class SysResourceModel : PKModel
    {
        public string? Name { get; set; }
        public string? Code { get; set; }
        public int Type { get; set; }
        public int? Pid { get; set; }
        public string? Value { get; set; }
        public int TenantId { get; set; }
    }
    public class SysResourceDao : BaseDaoMaster<SysResourceModel>
    {
        public SysResourceDao() : base(string.Empty, "t_scgd_sys_resource", "id", true)
        {
        }

        public override SysResourceModel GetModel(DataRow item)
        {
            SysResourceModel model = new SysResourceModel
            {
                Id = item.Field<int>("id"),
                Name = item.Field<string>("name"),
                Code = item.Field<string>("code"),
                Type = item.Field<int>("type"),
                Pid = item.Field<int?>("pid"),
                Value = item.Field<string>("txt_value"),
                TenantId = item.Field<int>("tenant_id"),
            };
            return model;
        }
    }
}
