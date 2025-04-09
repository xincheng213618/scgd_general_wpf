using ColorVision.Engine.Interfaces;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm;
using CVCommCore;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

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

        [Column("area_json_val")]
        public string AreaJsonVal { get; set; }

    }


    public class BlackMuraDao : BaseTableDao<BlackMuraModel>
    {
        public static BlackMuraDao Instance { get; set; } = new BlackMuraDao();
        public BlackMuraDao() : base("t_scgd_algorithm_result_detail_blackmura")
        {
        }
    }



}
