using ColorVision.Engine.Abstractions;
using ColorVision.Database;
using Newtonsoft.Json;
using SqlSugar;

namespace ColorVision.Engine.Templates.Jsons
{
    public class ResultFile
    {
        public string ResultFileName { get; set; }
    }

    [SugarTable("t_scgd_algorithm_result_detail_common")]
    public class DetailCommonModel : PKModel, IInitTables
    {
        [SugarColumn(ColumnName ="pid")]
        public int PId { get; set; }

        [SugarColumn(ColumnName ="result",ColumnDataType ="json")]
        public string ResultJson { get; set; }
    }

    public class DeatilCommonDao : BaseTableDao<DetailCommonModel>
    {
        public static DeatilCommonDao Instance { get; set; } = new DeatilCommonDao();
    }


}
