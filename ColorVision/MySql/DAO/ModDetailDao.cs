using System.Data;

namespace ColorVision.MySql.DAO
{
    public class ModDetailModel : PKModel
    {
        public ModDetailModel() :this(-1,-1,null){ }
        public ModDetailModel(int sysPid,int pid,string? val) {
            SysPid = sysPid;
            Pid = pid;
            ValueA = val;
        }
        public int SysPid { get; set; }
        public int Pid { get; set; }
        public string? ValueA { get; set; }
        public string? ValueB { get; set; }
        public string? Symbol { get; set; }
        public bool? IsEnable { get; set; } = true;
        public bool? IsDelete { get; set; } = false;
    }

    public class ModDetailDao : BaseDaoMaster<ModDetailModel>
    {
        public ModDetailDao() : base("v_scgd_mod_detail", "t_scgd_mod_param_detail", "id", true)
        {
        }

        public override ModDetailModel GetModel(DataRow item)
        {
            ModDetailModel model = new ModDetailModel
            {
                Id = item.Field<int>("id"),
                SysPid = item.Field<int>("cc_pid"),
                Pid = item.Field<int>("pid"),
                ValueA = item.Field<string>("value_a"),
                ValueB = item.Field<string>("value_b"),
                Symbol = item.Field<string>("symbol"),
                IsEnable = item.Field<bool>("is_enable"),
                IsDelete = item.Field<bool>("is_delete"),
            };

            return model;
        }

        public override DataRow Model2Row(ModDetailModel item, DataRow row)
        {
            if (item != null)
            {
                if (item.Id > 0) row["id"] = item.Id;
                row["pid"] = item.Pid;
                row["cc_pid"] = item.SysPid;
                if (item.ValueA != null) row["value_a"] = item.ValueA;
                if (item.ValueB != null) row["value_b"] = item.ValueB;
                row["is_enable"] = item.IsEnable;
                row["is_delete"] = item.IsDelete;
            }
            return row;
        }

        public override DataTable CreateColumns(DataTable dInfo)
        {
            dInfo.Columns.Add("id", typeof(int));
            dInfo.Columns.Add("cc_pid", typeof(int));
            dInfo.Columns.Add("pid", typeof(int));
            dInfo.Columns.Add("value_a", typeof(string));
            dInfo.Columns.Add("value_b", typeof(string));
            dInfo.Columns.Add("is_enable", typeof(bool));
            dInfo.Columns.Add("is_delete", typeof(bool));
            return dInfo;
        }
    }
}