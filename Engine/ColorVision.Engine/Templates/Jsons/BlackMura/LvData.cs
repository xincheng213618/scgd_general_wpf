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

    public class LvDetails
    {
        public int Nle { get; set; }
        public double LvAvg { get; set; }
        public double LvMax { get; set; }
        public double LvMin { get; set; }
        public int MaxPtX { get; set; }
        public int MaxPtY { get; set; }
        public int MinPtX { get; set; }
        public int MinPtY { get; set; }
        public double Uniformity { get; set; }
        public double ZaRelMax { get; set; }
    }



}
