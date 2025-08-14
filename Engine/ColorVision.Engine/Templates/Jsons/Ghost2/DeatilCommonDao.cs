using ColorVision.Engine.Abstractions;
using ColorVision.Engine.MySql.ORM;
using Newtonsoft.Json;
using SqlSugar;
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
            Id = detail.Id;
            PId = detail.PId;
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
        [SugarColumn(ColumnName ="id")]
        public int Id { get; set; }
        [SugarColumn(ColumnName ="pid")]
        public int PId { get; set; }

        [SugarColumn(ColumnName ="result")]
        public string Result { get; set; }

        [SugarColumn(IsIgnore = true)]
        public string? ResultFileName { get; set; }
        [SugarColumn(IsIgnore = true)]
        public GhostReslut GhostReslut { get; set; }
    }








}
