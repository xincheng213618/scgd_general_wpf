using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.DetectScreenDefects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
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

            using var text = new StringReader(resultJson);
            using var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None };
            var payload = JToken.ReadFrom(reader);
            while (reader.Read())
            {
                // Read through the end so extra JSON content is reported as malformed.
            }
            if (payload.Type == JTokenType.Null)
                return null;

            const string countField = nameof(DetectScreenDefectsResult.DefectCount);
            if (payload is not JObject result ||
                !result.TryGetValue(countField, StringComparison.OrdinalIgnoreCase, out var countToken) ||
                countToken.Type is not (JTokenType.Integer or JTokenType.String) ||
                !int.TryParse(countToken.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) ||
                count < 0)
                throw new InvalidDataException("屏幕缺陷检测结果缺少有效的非负整数 DefectCount。");

            var inspection = result.ToObject<DetectScreenDefectsResult>()
                ?? throw new InvalidDataException("屏幕缺陷检测结果格式无效。");
            if (inspection.DefectCount != count || (count == 0 && inspection.Defects is { Count: > 0 }))
                throw new InvalidDataException("屏幕缺陷检测结果的 DefectCount 与明细矛盾。");

            return inspection;
        }
    }
}
