using ColorVision.Engine.MySql.ORM;
using SqlSugar;
using System.Data;

namespace ColorVision.Engine.Templates.Flow
{
    [SugarTable("t_scgd_mod_param_detail")]
    public class ModFlowDetailModel : PKModel
    {

        [SugarColumn(ColumnName ="cc_pid",Length = 11)]
        public int SysPid { get; set; }

        [SugarColumn(ColumnName ="pid",Length =11)]
        public int Pid { get; set; }

        [@SugarColumn(IsIgnore =true)]
        public string? Value { get; set; }

        [SugarColumn(ColumnName ="value_a",IsNullable = true)]
        public string? ValueA { get; set; }

        [SugarColumn(ColumnName ="value_b" ,IsNullable =true)]
        public string? ValueB { get; set; }
        [SugarColumn(ColumnName ="symbol")]
        public string? Symbol { get; set; }


        [SugarColumn(ColumnName ="is_enable")]
        public bool IsEnable { get; set; } = true;
        [SugarColumn(ColumnName ="is_enable")]
        public bool IsDelete { get; set; }
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
