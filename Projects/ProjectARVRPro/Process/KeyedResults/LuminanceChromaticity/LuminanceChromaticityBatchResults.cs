using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Services.Results.LightArea;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using MQTTMessageLib.Algorithm;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.IO;

namespace ProjectARVRPro.Process.KeyedResults.LuminanceChromaticity;

internal sealed class LuminanceChromaticityBatchResults
{
    private static readonly string[] CenterFields =
    [
        nameof(POIResultDataCIExyuv.Y), nameof(POIResultDataCIExyuv.x), nameof(POIResultDataCIExyuv.y),
        nameof(POIResultDataCIExyuv.u), nameof(POIResultDataCIExyuv.v), nameof(POIResultDataCIExyuv.CCT),
    ];

    internal Func<int, List<AlgResultMasterModel>>? ReadMasters { get; init; }
    internal Func<int, List<PoiPointResultModel>>? ReadPoints { get; init; }
    internal Func<int, List<DetailCommonModel>>? ReadDetails { get; init; }
    internal Func<int, List<AlgResultLightAreaModel>>? ReadLightAreas { get; init; }

    internal static void RequireCenterValue(PoiPointResultModel point)
    {
        if (string.IsNullOrWhiteSpace(point.Value))
            throw new InvalidDataException($"中心点 [{point.PoiName}] 缺少实测数据。");

        var json = JObject.Parse(point.Value);
        foreach (string field in CenterFields)
        {
            // X/Y and x/y represent different quantities in the same POI payload.
            StringComparison comparison = field is "Y" or "x" or "y" ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            if (!json.TryGetValue(field, comparison, out var token) || !IsFiniteMeasurement(token))
                throw new InvalidDataException($"中心点 [{point.PoiName}] 缺少有限的实测字段 {field}。");
        }
    }

    private static bool IsFiniteMeasurement(JToken token)
    {
        if (token.Type is JTokenType.Integer or JTokenType.Float)
            return double.IsFinite(token.Value<double>());

        return token.Type == JTokenType.String && double.TryParse(token.Value<string>(), NumberStyles.Float | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture, out double numeric) && double.IsFinite(numeric);
    }

    internal static void RequireFiniteCenter(PoiResultCIExyuvData center)
    {
        if (!double.IsFinite(center.Y) || !double.IsFinite(center.x) || !double.IsFinite(center.y)
            || !double.IsFinite(center.u) || !double.IsFinite(center.v) || !double.IsFinite(center.CCT))
            throw new InvalidDataException($"中心点 [{center.Name}] 修正后的测量值无效。");
    }

    internal static void RequireMeasuredCenter(bool hasCenter, string? centerKey)
    {
        if (!hasCenter)
            throw new InvalidDataException($"缺少已配置中心点 [{centerKey}] 的实测数据。");
    }

    internal static void Reject(IProcessExecutionContext context, string message)
    {
        context.Result.Result = false;
        context.Result.Msg = message;
        context.Result.ViewResultJson = string.Empty;
        context.ObjectiveTestResult.TotalResult = false;
    }
}
