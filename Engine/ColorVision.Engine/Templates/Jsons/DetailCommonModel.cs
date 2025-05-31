using ColorVision.Engine.Abstractions;
using ColorVision.Engine.MySql.ORM;
using Newtonsoft.Json;

namespace ColorVision.Engine.Templates.Jsons
{
    public class ResultFile
    {
        public string ResultFileName { get; set; }
    }

    public class DetailViewReslut : IViewResult
    {
        public DetailCommonModel DetailCommonModel { get; set; }

        public DetailViewReslut(DetailCommonModel detailCommonModel)
        {
            DetailCommonModel = detailCommonModel;

            var restfile = JsonConvert.DeserializeObject<ResultFile>(detailCommonModel.ResultJson);
            ResultFileName = restfile?.ResultFileName;
        }
        [Column("id")]
        public int Id { get; set; }
        [Column("pid")]
        public int PId { get; set; }
        public string? ResultFileName { get; set; }
    }


    [Table("t_scgd_algorithm_result_detail_common")]
    public class DetailCommonModel : PKModel
    {
        [Column("pid")]
        public int PId { get; set; }

        [Column("result")]
        public string ResultJson { get; set; }
    }

    public class DeatilCommonDao : BaseTableDao<DetailCommonModel>
    {
        public static DeatilCommonDao Instance { get; set; } = new DeatilCommonDao();
        public DeatilCommonDao() : base("t_scgd_algorithm_result_detail_common")
        {
        }
    }


}
