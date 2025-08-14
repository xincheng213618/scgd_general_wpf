using ColorVision.Engine.MySql.ORM;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace ColorVision.Engine.Templates.Jsons.BlackMura
{
    public class LvData
    {
        public double AvgLv { get; set; }
        public double MaxLv { get; set; }
        public double MinLv { get; set; }
        public int BlockNumX { get; set; }
        public int BlockNumY { get; set; }
        public double LvUniformity { get; set; }
        public List<double> LocalLvUniformity { get; set; }
    }

    public class ResultJson
    {
        [JsonProperty("Nle")]
        public int Nle { get; set; }
        [JsonProperty("lv_avg")]
        public double LvAvg { get; set; }
        [JsonProperty("lv_max")]
        public double LvMax { get; set; }
        [JsonProperty("lv_min")]
        public double LvMin { get; set; }
        [JsonProperty("max_pt_x")]
        public int MaxPtX { get; set; }
        [JsonProperty("max_pt_y")]
        public int MaxPtY { get; set; }
        [JsonProperty("min_pt_x")]
        public int MinPtX { get; set; }
        [JsonProperty("min_pt_y")]
        public int MinPtY { get; set; }

        [JsonProperty("uniformity")]
        public double Uniformity { get; set; }
        [JsonProperty("za_rel_max")]
        public double ZaRelMax { get; set; }
    }

    public class Outputfile
    {

        [JsonProperty("aa_path")]
        public string AAPath { get; set; }

        [JsonProperty("rst_path")]
        public string RstPath { get; set; }

        [JsonProperty("mask_path")]
        public string MaskPath { get; set; }
    }
}
