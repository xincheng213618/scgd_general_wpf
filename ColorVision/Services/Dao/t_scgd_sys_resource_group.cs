using System.Data;
using ColorVision.MySql;

namespace ColorVision.Services.Dao
{
    public class ResourceGroupAssociation:PKModel
    {
        public int ResourceId { get; set; }
        public int GroupId { get; set; }
    }


    public class ResourceGroupEqo : BaseDaoMaster<ResourceGroupAssociation>
    {

        public ResourceGroupEqo() : base("t_scgd_sys_resource_group", "id", true)
        {

        }

        public override ResourceGroupAssociation GetModel(DataRow item)
        {
            ResourceGroupAssociation model = new ResourceGroupAssociation
            {
                Id = item.Field<int>("id"),
                GroupId = item.Field<int>("group_id"),
                ResourceId =item.Field<int>("resource_id")
            };
            return model;
        }

        public override DataRow Model2Row(ResourceGroupAssociation item, DataRow row)
        {
            if (item != null)
            {
                row["id"] = item.Id;
                row["group_id"] = item.GroupId;
                row["resource_id"] = item.ResourceId;
            }
            return row;
        }
    }
}
