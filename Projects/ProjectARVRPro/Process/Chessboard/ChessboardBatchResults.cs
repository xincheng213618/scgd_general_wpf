using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.PoiAnalysis;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectARVRPro.Recipe;
using System.Globalization;
using System.IO;

namespace ProjectARVRPro.Process.Chessboard;

internal sealed class ChessboardBatchResults
{
    internal Func<int, List<AlgResultMasterModel>>? ReadMasters { get; init; }
    internal Func<int, List<PoiPointResultModel>?>? ReadPoints { get; init; }
    internal Func<int, List<DetailCommonModel>>? ReadDetails { get; init; }

    internal string? AppendMeasuredPoints(IProcessExecutionContext ctx, int masterId, List<PoiResultCIExyuvData> destination)
    {
        var points = ReadPoints != null ? ReadPoints(masterId) : ctx.TryReadDetails(PoiPointResultDao.Instance, masterId);
        if (points == null)
            return $"无法读取棋盘格 POI 结果 {masterId}。";

        string? error = null;
        for (int id = 0; id < points.Count; id++)
        {
            var point = points[id];
            try
            {
                // Uppercase Y is luminance. Lowercase y is a separate chromaticity field.
                if (!string.IsNullOrWhiteSpace(point.Value) &&
                    TryReadFinite(JObject.Parse(point.Value), "Y", StringComparison.Ordinal, out _))
                {
                    var measured = new PoiResultCIExyuvData(point) { Id = id };
                    if (double.IsFinite(measured.Y))
                    {
                        destination.Add(measured);
                        continue;
                    }
                }
            }
            catch (JsonException)
            {
                // An unreadable point cannot be used for local contrast or exported as a measurement.
            }

            error ??= $"棋盘格 POI 结果 {masterId} 点位 [{point.PoiName ?? point.Id.ToString()}] 缺少有效实测亮度 Y。";
        }

        return error;
    }

    internal double ReadTemplateContrast(IProcessExecutionContext ctx, int masterId, string resultName)
    {
        var details = ReadDetails?.Invoke(masterId) ?? ctx.ReadDetails(DeatilCommonDao.Instance, masterId);
        if (details.Count != 1)
            throw new InvalidDataException($"棋盘格模板结果 [{resultName}] 明细数量无效：{details.Count}。" +
                $" 结果 ID：{masterId}。");

        string? path = JsonConvert.DeserializeObject<ResultFile>(details[0].ResultJson)?.ResultFileName;
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidDataException($"棋盘格模板结果 [{resultName}] 缺少结果文件路径。 结果 ID：{masterId}。");

        var document = JObject.Parse(File.ReadAllText(path));
        if (!document.TryGetValue(nameof(PoiAnalysisResult.result), StringComparison.OrdinalIgnoreCase, out var payload) ||
            payload is not JObject result ||
            !TryReadFinite(result, nameof(PoiAnalysisItem.Value), StringComparison.OrdinalIgnoreCase, out double measured))
            throw new InvalidDataException($"棋盘格模板结果 [{resultName}] 缺少有效实测字段 Value。 结果 ID：{masterId}。");

        return measured;
    }

    internal static double ApplyRecipe(double measured, RecipeBase recipe)
    {
        double corrected = recipe.Apply(measured);
        if (!double.IsFinite(corrected))
            throw new InvalidDataException("棋盘格对比度修正值无效。");
        return corrected;
    }

    private static bool TryReadFinite(JObject result, string field, StringComparison comparison, out double number)
    {
        number = default;
        if (!result.TryGetValue(field, comparison, out var token))
            return false;
        if (token.Type is JTokenType.Integer or JTokenType.Float)
        {
            number = token.Value<double>();
            return double.IsFinite(number);
        }
        return token.Type == JTokenType.String && double.TryParse(token.Value<string>(), NumberStyles.Float | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture, out number) && double.IsFinite(number);
    }

    internal static void Reject(IProcessExecutionContext ctx, string message)
    {
        ctx.Result.Result = false;
        ctx.Result.Msg = message;
        ctx.Result.ViewResultJson = string.Empty;
        ctx.ObjectiveTestResult.TotalResult = false;
    }
}
