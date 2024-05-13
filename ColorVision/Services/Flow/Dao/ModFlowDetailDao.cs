using ColorVision.MySql;
using ColorVision.MySql.ORM;
using System.Data;

namespace ColorVision.Services.Flow.Dao
{
    public class ModFlowDetailModel : PKModel
    {
        public ModFlowDetailModel() : this(-1, -1, null) { }
        public ModFlowDetailModel(int sysPid, int pid, string? val)
        {
            SysPid = sysPid;
            Pid = pid;
            ValueA = val;
        }
        public int SysPid { get; set; }
        public int Pid { get; set; }
        public string? ValueA { get; set; }
        public string? Value { get; set; }
        public string? ValueB { get; set; }
        public string? Symbol { get; set; }
        public bool? IsEnable { get; set; } = true;
        public bool? IsDelete { get; set; } = false;
    }



    public class ModFlowDetailDao : BaseDaoMaster<ModFlowDetailModel>
    {
        public static ModFlowDetailDao Instance { get; set; } = new ModFlowDetailDao();

        public ModFlowDetailDao() : base("v_scgd_mod_detail_flow", "t_scgd_mod_param_detail", "id", true)
        {

        }

        public override ModFlowDetailModel GetModelFromDataRow(DataRow item)
        {
            ModFlowDetailModel model = new()
            {
                Id = item.Field<int>("id"),
                SysPid = item.Field<int>("cc_pid"),
                Pid = item.Field<int>("pid"),
                Value = item.Field<string>("value"),
                ValueA = item.Field<string>("value_a"),
                ValueB = item.Field<string>("value_b"),
                Symbol = item.Field<string>("symbol"),
                IsEnable = item.Field<bool>("is_enable"),
                IsDelete = item.Field<bool>("is_delete"),
            };

            return model;
        }
    }
}
