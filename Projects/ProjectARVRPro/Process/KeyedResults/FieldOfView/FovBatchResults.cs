using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Services.Results.LightArea;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.FOV2;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectARVRPro.Recipe;
using System.Globalization;
using System.IO;

namespace ProjectARVRPro.Process.KeyedResults.FieldOfView;

internal readonly record struct FovAngles(double Horizontal, double Vertical, double Diagonal);

internal sealed class FovBatchResults
{
    internal Func<int, List<AlgResultMasterModel>>? ReadMasters { get; init; }
    internal Func<int, List<DetailCommonModel>>? ReadDetails { get; init; }
    internal Func<int, List<AlgResultLightAreaModel>>? ReadLightAreas { get; init; }

    internal (List<FovAngles> Angles, List<AlgResultLightAreaModel> Corners) Load(IProcessExecutionContext ctx)
    {
        int batchId = ctx.Batch.Id;
        List<FovAngles> angles = [];
        List<AlgResultLightAreaModel> corners = [];
        foreach (var master in (ReadMasters?.Invoke(batchId) ?? ctx.ReadMasterResults()))
        {
            if (master.ImgFileType == ViewResultAlgType.FindLightArea)
                corners = ReadLightAreas?.Invoke(master.Id) ?? ctx.ReadDetails(AlgResultLightAreaDao.Instance, master.Id);
            else if (master.ImgFileType == ViewResultAlgType.FOV)
            {
                var details = ReadDetails?.Invoke(master.Id) ?? ctx.ReadDetails(DeatilCommonDao.Instance, master.Id);
                if (details.Count == 1)
                    angles.Add(ReadAngles(details[0]));
            }
        }

        if (angles.Count == 0)
            throw new InvalidDataException($"批次 {batchId} 缺少水平、垂直和对角视场角测量结果。");
        return (angles, corners);
    }

    private static FovAngles ReadAngles(DetailCommonModel detail)
    {
        string? path = JsonConvert.DeserializeObject<ResultFile>(detail.ResultJson)?.ResultFileName;
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidDataException($"视场角明细 {detail.Id} 缺少结果文件路径。");

        var document = JObject.Parse(File.ReadAllText(path));
        if (!document.TryGetValue(nameof(ResDFov.result), StringComparison.OrdinalIgnoreCase, out var payload) || payload is not JObject result)
            throw new InvalidDataException($"视场角明细 {detail.Id} 缺少 result 对象。");

        return new FovAngles(
            ReadAngle(result, nameof(CameraFOV.ClolorVisionH_Fov)),
            ReadAngle(result, nameof(CameraFOV.ClolorVisionV_Fov)),
            ReadAngle(result, nameof(CameraFOV.D_Fov)));
    }

    private static double ReadAngle(JObject result, string field)
    {
        if (result.TryGetValue(field, StringComparison.OrdinalIgnoreCase, out var token))
        {
            if (token.Type is JTokenType.Float or JTokenType.Integer && token.Value<double>() is double numeric && double.IsFinite(numeric))
                return numeric;
            if (token.Type == JTokenType.String && double.TryParse(token.Value<string>(), NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out double text) && double.IsFinite(text))
                return text;
        }
        throw new InvalidDataException($"视场角缺少有限的实测字段 {field}。");
    }

    internal static bool ApplyRecipe(ObjectiveTestItem item, double measured, RecipeBase recipe)
    {
        double value = recipe.Apply(measured);
        if (!double.IsFinite(value))
            throw new InvalidDataException($"视场角 {item.Name} 修正后的数值无效。");

        item.Value = value;
        item.TestValue = value.ToString("F4");
        item.LowLimit = recipe.Min;
        item.UpLimit = recipe.Max;
        return item.TestResult;
    }

    internal static void Reject(IProcessExecutionContext context, string message)
    {
        context.Result.Result = false;
        context.Result.Msg = message;
        context.Result.ViewResultJson = string.Empty;
        context.ObjectiveTestResult.TotalResult = false;
    }
}
