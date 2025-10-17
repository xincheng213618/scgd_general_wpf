using ColorVision.Database;
using SqlSugar;

namespace ColorVision.Engine.Templates.Jsons
{
    public class ResultFile
    {
        public string ResultFileName { get; set; }
    }

    [SugarTable("t_scgd_algorithm_result_detail_common")]
    public class DetailCommonModel : EntityBase, IInitTables
    {
        [SugarColumn(ColumnName ="pid", IsNullable = true)]
        public int PId { get; set; }

        [SugarColumn(ColumnName ="result",ColumnDataType ="json",IsNullable =true)]
        public string ResultJson { get; set; }
    }

    public class DeatilCommonDao : BaseTableDao<DetailCommonModel>
    {
        public static DeatilCommonDao Instance { get; set; } = new DeatilCommonDao();
    }


}
