using ColorVision.Engine.Interfaces;
using ColorVision.Engine.MySql.ORM;

namespace ColorVision.Engine.Templates.Jsons.BlackMura
{

    [Table("t_scgd_algorithm_result_detail_blackmura")]
    public class BlackMuraModel : PKModel, IViewResult
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
    }


    public class BlackMuraDao : BaseTableDao<BlackMuraModel>
    {
        public static BlackMuraDao Instance { get; set; } = new BlackMuraDao();
        public BlackMuraDao() : base("t_scgd_algorithm_result_detail_blackmura")
        {
        }
    }



}
