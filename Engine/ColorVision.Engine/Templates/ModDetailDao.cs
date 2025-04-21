using ColorVision.Engine.MySql.ORM;
using System.Data;

namespace ColorVision.Engine.Templates
{
    public class ModDetailModel : VPKModel
    {
        public ModDetailModel()
        { 
        }

        public ModDetailModel(int sysPid, int pid, string? val)
        {
            SysPid = sysPid;
            Pid = pid;
            ValueA = val;
        }
        public int SysPid { get => _SysPid; set { _SysPid = value; NotifyPropertyChanged(); } }
        private int _SysPid;
        public int Pid { get => _Pid; set { _Pid = value; NotifyPropertyChanged(); } }
        private int _Pid;
          
        public string? ValueA { get => _ValueA; set { _ValueA = value; NotifyPropertyChanged(); } }
        private string? _ValueA;
        public string? ValueB { get => _ValueB; set { _ValueB = value; NotifyPropertyChanged(); } }
        private string? _ValueB;
        public string? Symbol { get => _Symbol; set { _Symbol = value; NotifyPropertyChanged(); } }
        private string? _Symbol;
        public string? SymbolName { get => _SymbolName; set { _SymbolName = value; NotifyPropertyChanged(); } }
        private string? _SymbolName;

        public bool? IsEnable { get; set; } = true;
        public bool? IsDelete { get; set; } = false;
    }


    public class ModDetailDao : BaseViewDao<ModDetailModel>
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
                SymbolName = item.Field<string>("name")
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