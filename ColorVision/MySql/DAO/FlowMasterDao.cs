using OpenCvSharp.Flann;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MySql.DAO
{
    public class FlowMasterModel : IBaseModel
    {
        public FlowMasterModel() : this("",0){ }
        public FlowMasterModel(string text, int tenantId)
        {
            TenantId = tenantId;
        }

        public int Id { get; set; }
        public string? Name { get; set; }
        public int TenantId { get; set; }
        public int Pid { get; set; }
        public int GetPK()
        {
            return Id;
        }

        public void SetPK(int id)
        {
            Id = id;
        }
    }
    public class FlowMasterDao : BaseModMasterDao<FlowMasterModel>
    {
        public FlowMasterDao() : base("flow", "v_scgd_mod_master", "t_scgd_mod_param_master", "id")
        {
        }

        public override FlowMasterModel GetModel(DataRow item)
        {
            FlowMasterModel model = new FlowMasterModel
            {
                Id = item.Field<int>("id"),
                Name = item.Field<string?>("name"),
            };

            return model;
        }

        public override DataRow Model2Row(FlowMasterModel item, DataRow row)
        {
            if (item != null)
            {
                if (item.Id > 0) row["id"] = item.Id;
                if (item.Name != null) row["name"] = item.Name;
                row["tenant_id"] = item.TenantId;
                row["mm_id"] = item.Pid;
            }
            return row;
        }

        public override int Save(FlowMasterModel flowMaster)
        {
            flowMaster.Pid = 11;
            return base.Save(flowMaster);
        }
    }
}
