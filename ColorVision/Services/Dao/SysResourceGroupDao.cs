using System.Collections.Generic;
using System.Data;
using ColorVision.MySql;

namespace ColorVision.Services.Dao
{
    public class SysResourceGroupModel : PKModel
    {
        public int ResourceId { get; set; }
        public int GroupId { get; set; }
    }


    public class ResourceGroupDao : BaseDaoMaster<SysResourceGroupModel>
    {

        public ResourceGroupDao() : base("t_scgd_sys_resource_group", "id", true)
        {

        }



        public override SysResourceGroupModel GetModelFromDataRow(DataRow item)
        {
            SysResourceGroupModel model = new SysResourceGroupModel
            {
                Id = item.Field<int>("id"),
                GroupId = item.Field<int>("group_id"),
                ResourceId =item.Field<int>("resource_id")
            };
            return model;
        }

        public override DataRow Model2Row(SysResourceGroupModel item, DataRow row)
        {
            if (item != null)
            {
                row["id"] = item.Id;
                row["group_id"] = item.GroupId;
                row["resource_id"] = item.ResourceId;
            }
            return row;
        }



        public List<SysResourceModel> GetResourceItems(int pid, int tenantId = -1)
        {
            List<SysResourceModel> list = new List<SysResourceModel>();

        }

    }
}
