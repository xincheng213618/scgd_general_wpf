using ColorVision.Engine.Abstractions;
using ColorVision.Database;
using ColorVision.Database;
using Newtonsoft.Json;
using SqlSugar;

namespace ColorVision.Engine.Templates.Jsons.BlackMura
{
    public class BlackMuraView: IViewResult,IInitTables
    {
        public BlackMuraView(BlackMuraModel blackMuraModel)
        {
            Id = blackMuraModel.Id;
            PId = blackMuraModel.PId;
            Name = blackMuraModel.Name;
            Outputfile = JsonConvert.DeserializeObject<Outputfile>(blackMuraModel.OutputFile) ?? new Outputfile();
            ResultJson = JsonConvert.DeserializeObject<ResultJson>(blackMuraModel.ResultJson) ?? new ResultJson();
            LvData = JsonConvert.DeserializeObject<LvData>(blackMuraModel.UniformityJson) ?? new LvData();
            AreaJsonVal = blackMuraModel.AreaJsonVal;
        }
        public int Id { get; set; }
        public int PId { get; set; }

        public string Name { get; set; }

        public ResultJson ResultJson { get; set; }

        public LvData LvData { get; set; }

        public Outputfile Outputfile { get; set; }
        public string AreaJsonVal { get; set; }

    }



    [SugarTable("t_scgd_algorithm_result_detail_blackmura")]
    public class BlackMuraModel : PKModel, IInitTables
    {
        [SugarColumn(ColumnName ="pid")]
        public int PId { get; set; }

        [SugarColumn(ColumnName ="name")]
        public string Name { get; set; }

        [SugarColumn(ColumnName ="result_json_val")]
        public string ResultJson { get; set; }

        [SugarColumn(ColumnName ="uniformity_json_val")]
        public string UniformityJson { get; set; }

        [SugarColumn(ColumnName ="output_file_json_val")]
        public string OutputFile { get; set; }

        [SugarColumn(ColumnName ="area_json_val")]
        public string AreaJsonVal { get; set; }

    }


    public class BlackMuraDao : BaseTableDao<BlackMuraModel>
    {
        public static BlackMuraDao Instance { get; set; } = new BlackMuraDao();
    }



}
