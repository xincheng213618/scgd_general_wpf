using ColorVision.Engine.MySql.ORM;

namespace ColorVision.Engine.Templates.Jsons
{
    public class ResultFile
    {
        public string ResultFileName { get; set; }
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
