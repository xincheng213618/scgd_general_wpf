using ColorVision.Engine.Abstractions;
using ColorVision.Engine.MySql.ORM;
using Newtonsoft.Json;
using SqlSugar;

namespace ColorVision.Engine.Templates.Jsons.BlackMura
{
    [SugarTable("t_scgd_algorithm_result_detail_blackmura")]
    public class BlackMuraView: IViewResult
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
        [Column("id")]
        public int Id { get; set; }
        [Column("pid")]
        public int PId { get; set; }

        [Column("name")]
        public string Name { get; set; }

        public ResultJson ResultJson { get; set; }

        public LvData LvData { get; set; }

        public Outputfile Outputfile { get; set; }

        [Column("area_json_val")]
        public string AreaJsonVal { get; set; }

    }



    [SugarTable("t_scgd_algorithm_result_detail_blackmura")]
    public class BlackMuraModel : PKModel
    {
        [Column("pid")]
        public int PId { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("result_json_val")]
        public string ResultJson { get; set; }

        [Column("uniformity_json_val")]
        public string UniformityJson { get; set; }

        [Column("output_file_json_val")]
        public string OutputFile { get; set; }

        [Column("area_json_val")]
        public string AreaJsonVal { get; set; }

    }


    public class BlackMuraDao : BaseTableDao<BlackMuraModel>
    {
        public static BlackMuraDao Instance { get; set; } = new BlackMuraDao();
    }



}
