using ColorVision.Engine.MySql.ORM;
using SqlSugar;
using System.Data;

namespace ColorVision.Engine.Templates.Flow
{
    [SugarTable("t_scgd_mod_param_detail")]
    public class ModFlowDetailModel : PKModel
    {
        [Column("cc_pid")]
        public int SysPid { get; set; }
        [Column("pid")]
        public int Pid { get; set; }
        [Column("value"), ColumnIgnoreAttribute()]
        public string? Value { get; set; }
        [Column("value_a")]
        public string? ValueA { get; set; }
        [Column("value_b")]
        public string? ValueB { get; set; }
        [Column("symbol")]
        public string? Symbol { get; set; }
        [Column("is_enable")]
        public bool? IsEnable { get; set; } = true;
        [Column("is_enable")]
        public bool? IsDelete { get; set; } = false;
    }

    public class ModFlowDetailDao : BaseViewDao<ModFlowDetailModel>
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
