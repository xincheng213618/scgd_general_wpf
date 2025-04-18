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
    public class VOtherConfig
    {
        public bool Debug { get; set; }
        public string debugPath { get; set; }
        public float showMinGain { get; set; }
        public float showMaxGain { get; set; }
    }

    public class BrightConfig
    {
        public int thresholdMin { get; set; }
        public int thresholdMax { get; set; }
        public int thresholdStep { get; set; }
        public int brightNumX { get; set; }
        public int brightNumY { get; set; }
        public int patternType { get; set; }
        public int outRectSizeMin { get; set; }
        public float outRectSizeRate { get; set; }
        public int erodeKernel { get; set; }
    }

    public class GhostConfig
    {
        public List<bool> ingoreCheckMixBright { get; set; }
        public int thresholdMin { get; set; }
        public int thresholdMax { get; set; }
        public int thresholdStep { get; set; }
        public int outRectSizeMin { get; set; }
        public float outRectSizeRate { get; set; }
        public int minGary { get; set; }
        public float garyRate { get; set; }
        public int erodeKernel { get; set; }
        public int erodeTime { get; set; }
        public int distanceToBright { get; set; }
    }


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
