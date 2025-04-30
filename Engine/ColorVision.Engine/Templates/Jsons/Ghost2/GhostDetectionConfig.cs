using Newtonsoft.Json;
using System.Collections.Generic;

namespace ColorVision.Engine.Templates.Jsons.Ghost2
{
    public class GhostDetectionConfig
    {
        public VOtherConfig vOther { get; set; }
        public BrightConfig Bright { get; set; }
        public GhostConfig Ghost { get; set; }

        static public string getDefaultJson()
        {
            var config = new GhostDetectionConfig
            {
                vOther = new VOtherConfig
                {
                    Debug = false,
                    debugPath = "Result\\",
                    showMinGain = 60,
                    showMaxGain = 0.8f
                },
                Bright = new BrightConfig
                {
                    thresholdMin = 30000,
                    thresholdMax = 40000,
                    thresholdStep = 1000,
                    brightNumX = 3,
                    brightNumY = 3,
                    patternType = 0,
                    outRectSizeMin = 60,
                    outRectSizeRate = 5,
                    erodeKernel = 3
                },
                Ghost = new GhostConfig
                {
                    ingoreCheckMixBright = new List<bool> { false, false, false, false, true, false, false, false, false },
                    thresholdMin = 50,
                    thresholdMax = 500,
                    thresholdStep = 10,
                    outRectSizeMin = 100,
                    outRectSizeRate = 7.3f,
                    minGary = -1,
                    garyRate = 1,
                    erodeKernel = 3,
                    erodeTime = 5,
                    distanceToBright = 100
                }

            };

            return JsonConvert.SerializeObject(config);
        }
    }
}
