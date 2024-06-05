using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql.ORM;
using System.Data;

namespace ColorVision.Services.Dao
{
    public class ModDetailModel : ViewModelBase,IPKModel
    {
        public int Id { get; set; }
        public ModDetailModel() : this(-1, -1, null) { }
        public ModDetailModel(int sysPid, int pid, string? val)
        {
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

        public string GetValueMD5()
        {
            string txt = ValueA + Id;
            string code = Cryptography.GetMd5Hash(txt);
            return code;
        }
    }

    public class ModDetailDao : BaseDaoMaster<ModDetailModel>
    {
        public static ModDetailDao Instance { get; set; } = new ModDetailDao();

        public ModDetailDao() : base("v_scgd_mod_detail", "t_scgd_mod_param_detail", "id", true)
        {
        }

        public override ModDetailModel GetModelFromDataRow(DataRow item)
        {
            ModDetailModel model = new()
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