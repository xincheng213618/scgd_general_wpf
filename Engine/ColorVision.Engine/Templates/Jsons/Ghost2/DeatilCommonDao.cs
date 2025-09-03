using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace ColorVision.Engine.Templates.Jsons.Ghost2
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
            var restfile = JsonConvert.DeserializeObject<ResultFile>(detail.ResultJson);
            if (restfile != null)
            {
                ResultFileName = restfile.ResultFileName;
                if (File.Exists(restfile.ResultFileName))
                {

                    string json = File.ReadAllText(restfile.ResultFileName);
                    Result = json;
                    GhostReslut = JsonConvert.DeserializeObject<GhostReslut>(json);
                }
            }
            else
            {
                Result = detail.ResultJson;
            }


        }
        public string Result { get; set; }
        public string? ResultFileName { get; set; }
        public GhostReslut GhostReslut { get; set; }
    }








}
