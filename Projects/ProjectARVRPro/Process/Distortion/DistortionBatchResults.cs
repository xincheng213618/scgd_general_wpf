using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.Distortion2;
using Newtonsoft.Json.Linq;
using ProjectARVRPro.Recipe;
using System.Globalization;
using System.IO;

namespace ProjectARVRPro.Process.Distortion;

internal sealed class DistortionBatchResults
{
    internal Func<int, List<AlgResultMasterModel>>? ReadMasters { get; init; }
    internal Func<int, List<DetailCommonModel>>? ReadDetails { get; init; }

    internal bool Apply(IProcessExecutionContext ctx, DistortionViewTestResult target, DistortionPointSource pointSource, DistortionRecipeConfig recipe,
        string showConfig, string? ratioUnit, string ratioSuffix)
    {
        int batchId = ctx.Batch.Id;
        bool allPassed = true;
        foreach (var master in (ReadMasters?.Invoke(batchId) ?? ctx.ReadMasterResults()).Where(master => master.ImgFileType == ViewResultAlgType.Distortion && master.version == "2.0"))
        {
            var details = ReadDetails?.Invoke(master.Id) ?? ctx.ReadDetails(DeatilCommonDao.Instance, master.Id);
            if (details.Count != 1)
                continue;

            var detail = new Distortion2View(details[0]);
            if (detail.DistortionReslut is not { } source)
                continue;

            target.Points.Clear();
            if (DistortionPointSourceHelper.GetPoints(source, pointSource) is { } points)
                target.Points.AddRange(points.Select(point => new System.Windows.Point(point.X, point.Y)));

            DistortionGeometryResultBuilder.Apply(source, recipe, target, showConfig);
            var document = JObject.Parse(detail.Result);
            if (document.GetValue("Optic_Distortion", StringComparison.OrdinalIgnoreCase) is JObject optic)
            {
                target.OpticDistortion = Build(optic, "opticRatio", nameof(target.OpticDistortion), recipe.OpticDistortion) ?? target.OpticDistortion;
            }

            if (document.GetValue("TV_distortion", StringComparison.OrdinalIgnoreCase) is JObject tv)
            {
                var horizontal = Build(tv, "horizontalRatio", nameof(target.HorizontalTVDistortion), recipe.HorizontalTVDistortion);
                var vertical = Build(tv, "verticalRatio", nameof(target.VerticalTVDistortion), recipe.VerticalTVDistortion);
                if ((horizontal == null) != (vertical == null))
                    throw new InvalidDataException($"畸变结果 ID {master.Id} 的 TV 指标缺少水平或垂直实测值。");
                target.HorizontalTVDistortion = horizontal ?? target.HorizontalTVDistortion;
                target.VerticalTVDistortion = vertical ?? target.VerticalTVDistortion;
            }

            if (document.GetValue("Point9_distortion", StringComparison.OrdinalIgnoreCase) is JObject point9)
            {
                target.DistortionTop = Build(point9, "topRatio", nameof(target.DistortionTop), recipe.DistortionTop) ?? target.DistortionTop;
                target.DistortionBottom = Build(point9, "bottomRatio", nameof(target.DistortionBottom), recipe.DistortionBottom) ?? target.DistortionBottom;
                target.DistortionLeft = Build(point9, "leftRatio", nameof(target.DistortionLeft), recipe.DistortionLeft) ?? target.DistortionLeft;
                target.DistortionRight = Build(point9, "rightRatio", nameof(target.DistortionRight), recipe.DistortionRight) ?? target.DistortionRight;
                target.KeystoneHoriz = Build(point9, "keyStoneHoriRatio", nameof(target.KeystoneHoriz), recipe.KeystoneHoriz) ?? target.KeystoneHoriz;
                target.KeystoneVert = Build(point9, "keyStoneVercRatio", nameof(target.KeystoneVert), recipe.KeystoneVert) ?? target.KeystoneVert;
            }

            allPassed &= GetItems(target).All(item => item.TestResult);
        }

        if (!GetItems(target).Any())
            throw new InvalidDataException($"批次 {batchId} 没有可判定的畸变实测值。");
        return allPassed;

        ObjectiveTestItem? Build(JObject group, string field, string itemName, RecipeBase metricRecipe)
        {
            var token = group.GetValue(field, StringComparison.OrdinalIgnoreCase);
            if (token == null) return null;

            double rawValue = token.Type switch
            {
                JTokenType.Integer or JTokenType.Float => token.Value<double>(),
                JTokenType.String when double.TryParse(token.Value<string>(), NumberStyles.Float | NumberStyles.AllowThousands,
                    CultureInfo.InvariantCulture, out double value) => value,
                _ => double.NaN,
            };
            if (!double.IsFinite(rawValue))
                throw new InvalidDataException($"畸变实测字段 {field} 缺少有限数值。");

            double corrected = metricRecipe.Apply(rawValue);
            if (!double.IsFinite(corrected))
                throw new InvalidDataException($"畸变指标 {itemName} 修正后的数值无效。");
            return new ObjectiveTestItem
            {
                Name = itemName, Value = corrected, LowLimit = metricRecipe.Min, UpLimit = metricRecipe.Max,
                TestValue = corrected.ToString(showConfig) + ratioSuffix, Unit = ratioUnit!,
            };
        }
    }

    internal static IEnumerable<ObjectiveTestItem> GetItems(DistortionTestResult result)
    {
        ObjectiveTestItem?[] items =
        [
            result.HorizontalTVDistortion, result.VerticalTVDistortion, result.OpticDistortion,
            result.DistortionTop, result.DistortionBottom, result.DistortionLeft, result.DistortionRight,
            result.KeystoneHoriz, result.KeystoneVert, result.MaximumTiltDegrees, result.MaximumEdgeLengthDifferencePercent,
        ];
        return items.OfType<ObjectiveTestItem>();
    }

    internal static void Reject(IProcessExecutionContext context, string message)
    {
        context.Result.Result = false;
        context.Result.Msg = message;
        context.Result.ViewResultJson = string.Empty;
        context.ObjectiveTestResult.TotalResult = false;
    }
}
