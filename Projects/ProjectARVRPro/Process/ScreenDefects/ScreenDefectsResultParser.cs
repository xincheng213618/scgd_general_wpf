using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.DetectScreenDefects;
using Newtonsoft.Json;
using System.IO;

namespace ProjectARVRPro.Process.ScreenDefects
{
    public static class ScreenDefectsResultParser
    {
        public static DetectScreenDefectsResult? Parse(string? resultJson, out string? resultFileName)
        {
            resultFileName = null;
            if (string.IsNullOrWhiteSpace(resultJson))
                return null;

            var resultFile = JsonConvert.DeserializeObject<ResultFile>(resultJson);
            resultFileName = resultFile?.ResultFileName;
            if (!string.IsNullOrWhiteSpace(resultFileName))
            {
                if (!File.Exists(resultFileName))
                    return null;

                resultJson = File.ReadAllText(resultFileName);
            }

            return JsonConvert.DeserializeObject<DetectScreenDefectsResult>(resultJson);
        }
    }
}
