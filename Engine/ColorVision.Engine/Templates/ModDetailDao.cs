using ColorVision.Engine.MySql.ORM;
using SqlSugar;
using System.Data;

namespace ColorVision.Engine.Templates
{
    [SugarTable("t_scgd_mod_param_detail")]
    public class ModDetailModel : PKModel
    {

        [SugarColumn(ColumnName = "cc_pid", Length = 11)]
        public int SysPid { get; set; }

        [SugarColumn(ColumnName = "pid", Length = 11)]
        public int Pid { get; set; }

        [@SugarColumn(IsIgnore = true)]
        public string? Value { get; set; }

        [SugarColumn(ColumnName = "value_a", IsNullable = true)]
        public string? ValueA { get; set; }

        [SugarColumn(ColumnName = "value_b", IsNullable = true)]
        public string? ValueB { get; set; }

        [SugarColumn(ColumnName = "is_enable")]
        public bool IsEnable { get; set; } = true;

        [SugarColumn(ColumnName = "is_delete")]
        public bool IsDelete { get; set; }
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