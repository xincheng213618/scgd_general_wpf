using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MySql.DAO
{
    public class FlowDetailModel : IBaseModel
    {
        public FlowDetailModel() :this(-1,-1){ }
        public FlowDetailModel(int sysPid,int pid) {
            SysPid = sysPid;
            Pid = pid;
        }
        public int Id { get; set; }
        public int SysPid { get; set; }
        public int Pid { get; set; }
        public string? ValueA { get; set; }
        public string? ValueB { get; set; }
        public string? Symbol { get; set; }

        public int GetPK()
        {
            return Id;
        }

        public void SetPK(int id)
        {
            Id = id;
        }
    }

    public class FlowDetailDao : BaseModDetailDao<FlowDetailModel>
    {
        public FlowDetailDao() : base("flow", "v_scgd_mod_detail", "t_scgd_mod_param_detail", "id")
        {
        }

        public override FlowDetailModel GetModel(DataRow item)
        {
            FlowDetailModel model = new FlowDetailModel
            {
                Id = item.Field<int>("id"),
                SysPid = item.Field<int>("cc_pid"),
                Pid = item.Field<int>("pid"),
                ValueA = item.Field<string>("value_a"),
                ValueB = item.Field<string>("value_b"),
                Symbol = item.Field<string>("symbol"),
            };

            return model;
        }

        public override DataRow Model2Row(FlowDetailModel item, DataRow row)
        {
            if (item != null)
            {
                if (item.Id > 0) row["id"] = item.Id;
                row["pid"] = item.Pid;
                row["cc_pid"] = item.SysPid;
                if (item.ValueA != null) row["value_a"] = item.ValueA;
                if (item.ValueB != null) row["value_b"] = item.ValueB;
            }
            return row;
        }
    }
}