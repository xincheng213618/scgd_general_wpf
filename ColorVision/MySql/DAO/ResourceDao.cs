using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MySql.DAO
{
    public class ResourceModel : PKModel
    {
        public string? Name { get; set; }
        public string? Code { get; set; }
        public int Type { get; set; }
        public string? Value { get; set; }
        public int TenantId { get; set; }
    }
    public class ResourceDao : BaseDaoMaster<ResourceModel>
    {
        public ResourceDao() : base(string.Empty, "t_scgd_sys_resource", "id", true)
        {
        }

        public DataTable GetTableAllByType(int type, int tenantId)
        {
            string sql = $"select * from {GetTableName()} where type={type} and tenant_id={tenantId}" + GetDelSQL(true);
            DataTable d_info = GetData(sql);
            return d_info;
        }

        internal List<ResourceModel> GetAllByType(int type, int tenantId)
        {
            List<ResourceModel> list = new List<ResourceModel>();
            DataTable d_info = GetTableAllByType(type, tenantId);
            foreach (var item in d_info.AsEnumerable())
            {
                ResourceModel? model = GetModel(item);
                if (model != null)
                {
                    list.Add(model);
                }
            }
            return list;
        }

        public override ResourceModel GetModel(DataRow item)
        {
            ResourceModel model = new ResourceModel
            {
                Id = item.Field<int>("id"),
                Name = item.Field<string>("name"),
                Code = item.Field<string>("code"),
                Type = item.Field<int>("type"),
                Value = item.Field<string>("txt_value"),
                TenantId = item.Field<int>("tenant_id"),
            };
            return model;
        }
    }
}
