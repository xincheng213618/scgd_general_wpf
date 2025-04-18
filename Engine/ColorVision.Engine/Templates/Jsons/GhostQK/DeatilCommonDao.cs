using ColorVision.Engine.Interfaces;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm;
using CVCommCore;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Engine.Templates.Jsons.GhostQK
{

    public class GhostView: IViewResult
    {
        public GhostView(DetailCommonModel detail)
        {
            Id = detail.Id;
            PId = detail.PId;
            Result = detail.ResultJson;
        }
        [Column("id")]
        public int Id { get; set; }
        [Column("pid")]
        public int PId { get; set; }

        [Column("result")]
        public string Result { get; set; }
    }



    [Table("t_scgd_algorithm_result_detail_common")]
    public class DetailCommonModel : PKModel
    {
        [Column("pid")]
        public int PId { get; set; }

        [Column("result_json")]
        public string ResultJson { get; set; }
    }


    public class DeatilCommonDao : BaseTableDao<DetailCommonModel>
    {
        public static DeatilCommonDao Instance { get; set; } = new DeatilCommonDao();
        public DeatilCommonDao() : base("ot_scgd_algorithm_result_detail_common")
        {
        }
    }



}
