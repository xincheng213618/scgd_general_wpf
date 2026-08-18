#pragma warning disable CS8601,CS8602
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Media;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor.Draw;
using CVCommCore.CVAlgorithm;
using Newtonsoft.Json;
using ProjectARVRPro.Process.Uniformity;
using ProjectARVRPro.Recipe;
using System.Text;
using System.Windows.Documents;
using System.Windows.Media;

namespace ProjectARVRPro.Process.KeyedResults.LuminanceChromaticity
{
    public class LuminanceChromaticityYWProcess : ProcessBase<LuminanceChromaticityYWProcessConfig>
    {
        internal const int Expected12X7PointCount = 12 * 7;
        internal const int Expected8X7PointCount = 8 * 7;

        private sealed record PoiCandidate(AlgResultMasterModel Master, List<PoiPointResultModel> Points);

        public override IRecipeConfig GetRecipeConfig() => Config.RecipeConfig;

        public override Task<bool> Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null || ctx.ObjectiveTestResult == null)
                return Task.FromResult(false);

            try
            {
                var testResult = new LuminanceChromaticityYWViewTestResult();
                bool found12X7 = false;
                bool found8X7 = false;
                var images = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                if (images.Count > 0)
                    ctx.Result.FileName = images[0].FileUrl;

                List<AlgResultMasterModel> masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                List<PoiCandidate> candidates = new();
                foreach (AlgResultMasterModel master in masters)
                {
                    if (master.ImgFileType != ViewResultAlgType.POI_XYZ)
                        continue;

                    List<PoiPointResultModel> points = PoiPointResultDao.Instance.GetAllByPid(master.Id);
                    candidates.Add(new PoiCandidate(master, points));
                }

                PoiCandidate? candidate12X7 = SelectCandidate(candidates, Expected12X7PointCount, "12X7");
                PoiCandidate? candidate8X7 = SelectCandidate(candidates, Expected8X7PointCount, "8X7", candidate12X7?.Master.Id);
                if (candidate12X7 != null)
                {
                    ReadPoiGroup(candidate12X7.Points, testResult.ViewPoixyuvDatas12X7);
                    ctx.Result.FileName = candidate12X7.Master.ImgFile;
                    found12X7 = true;
                }
                if (candidate8X7 != null)
                {
                    ReadPoiGroup(candidate8X7.Points, testResult.ViewPoixyuvDatas8X7);
                    ctx.Result.FileName = candidate8X7.Master.ImgFile;
                    found8X7 = true;
                }

                if (!found12X7 || !found8X7)
                {
                    ctx.Log?.Error($"YW亮色度POI结果不完整: 12X7(84点)={found12X7}, 8X7(56点)={found8X7}, POI_XYZ候选=[{FormatCandidates(candidates)}]");
                    return Task.FromResult(false);
                }

                if (!TryPopulateCalculatedResults(testResult, Config.RecipeConfig, out string errorMessage))
                {
                    ctx.Log?.Error($"YW亮色度本地计算失败: key={Config.GetOutputKey()}, message={errorMessage}");
                    return Task.FromResult(false);
                }

                foreach (ObjectiveTestItem item in GetCalculatedItems(testResult))
                    ctx.Result.Result &= item.TestResult;

                ctx.Log?.Info($"YW亮色度来源: poi-local, key={Config.GetOutputKey()}, 12X7={testResult.PoixyuvDatas12X7.Count}, 8X7={testResult.PoixyuvDatas8X7.Count}");
                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);
                var objectiveResult = JsonConvert.DeserializeObject<LuminanceChromaticityYWTestResult>(ctx.Result.ViewResultJson) ?? new();
                KeyedTestResultWriter.Write(ctx.ObjectiveTestResult, Config.GetOutputKey(), objectiveResult);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                ctx.Log?.Error(ex);
                return Task.FromResult(false);
            }
        }

        private static PoiCandidate? SelectCandidate(
            IReadOnlyCollection<PoiCandidate> candidates,
            int expectedPointCount,
            string groupName,
            int? excludedMasterId = null)
        {
            List<PoiCandidate> byPointCount = candidates
                .Where(candidate => candidate.Master.Id != excludedMasterId && candidate.Points.Count == expectedPointCount)
                .ToList();
            if (byPointCount.Count == 1)
                return byPointCount[0];
            if (byPointCount.Count > 1)
                throw new InvalidOperationException($"批次中存在多个{expectedPointCount}点的{groupName} POI结果: {FormatCandidates(byPointCount)}");
            return null;
        }

        private static string FormatCandidates(IEnumerable<PoiCandidate> candidates) => string.Join(", ", candidates.Select(candidate =>
            $"Id={candidate.Master.Id},TName={candidate.Master.TName ?? "<null>"},Points={candidate.Points.Count}"));

        private static void ReadPoiGroup(IEnumerable<PoiPointResultModel> points, List<PoiResultCIExyuvData> destination)
        {
            destination.Clear();
            int id = 0;
            foreach (PoiPointResultModel item in points)
                destination.Add(new PoiResultCIExyuvData(item) { Id = id++ });
        }

        internal static bool TryPopulateCalculatedResults(
            LuminanceChromaticityYWViewTestResult testResult,
            LuminanceChromaticityYWRecipeConfig recipe,
            out string errorMessage)
        {
            ArgumentNullException.ThrowIfNull(testResult);
            ArgumentNullException.ThrowIfNull(recipe);

            if (!TryCalculateGroup(testResult.ViewPoixyuvDatas12X7, Expected12X7PointCount, "12X7", out var calculation12X7, out errorMessage))
                return false;
            if (!TryCalculateGroup(testResult.ViewPoixyuvDatas8X7, Expected8X7PointCount, "8X7", out var calculation8X7, out errorMessage))
                return false;

            testResult.PoixyuvDatas12X7 = testResult.ViewPoixyuvDatas12X7.Select(ToOutputPoi).ToList();
            testResult.AverageLuminance12X7 = CreateItem("AverageLuminance_12X7", recipe.AverageLuminance12X7.Apply(calculation12X7.AverageLuminance), recipe.AverageLuminance12X7, "F4", "nit");
            testResult.LuminanceUniformity12X7 = CreateItem("LuminanceUniformity_12X7", recipe.LuminanceUniformity12X7.Apply(calculation12X7.LuminanceUniformity), recipe.LuminanceUniformity12X7, "F4", "%", 100);
            testResult.ColorUniformity12X7 = CreateItem("ColorUniformity_12X7", recipe.ColorUniformity12X7.Apply(calculation12X7.ColorUniformity), recipe.ColorUniformity12X7, "F5");

            testResult.PoixyuvDatas8X7 = testResult.ViewPoixyuvDatas8X7.Select(ToOutputPoi).ToList();
            testResult.AverageLuminance8X7 = CreateItem("AverageLuminance_8X7", recipe.AverageLuminance8X7.Apply(calculation8X7.AverageLuminance), recipe.AverageLuminance8X7, "F4", "nit");
            testResult.LuminanceUniformity8X7 = CreateItem("LuminanceUniformity_8X7", recipe.LuminanceUniformity8X7.Apply(calculation8X7.LuminanceUniformity), recipe.LuminanceUniformity8X7, "F4", "%", 100);
            testResult.ColorUniformity8X7 = CreateItem("ColorUniformity_8X7", recipe.ColorUniformity8X7.Apply(calculation8X7.ColorUniformity), recipe.ColorUniformity8X7, "F5");
            errorMessage = string.Empty;
            return true;
        }

        private static bool TryCalculateGroup(
            List<PoiResultCIExyuvData> points,
            int expectedPointCount,
            string groupName,
            out LuminanceChromaticityUniformityCalculationResult calculation,
            out string errorMessage)
        {
            if (points.Count != expectedPointCount)
            {
                calculation = new();
                errorMessage = $"{groupName} POI数量应为{expectedPointCount}，实际为{points.Count}。";
                return false;
            }

            calculation = LuminanceChromaticityUniformityCalculator.Calculate(points);
            if (!calculation.Success)
            {
                errorMessage = $"{groupName}: {calculation.ErrorMessage}";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private static PoixyuvData ToOutputPoi(PoiResultCIExyuvData poi) => new()
        {
            Id = poi.Id,
            Name = poi.Name,
            X = poi.X,
            Y = poi.Y,
            Z = poi.Z,
            x = poi.x,
            y = poi.y,
            u = poi.u,
            v = poi.v,
            CCT = poi.CCT,
            Wave = poi.Wave
        };

        private static IEnumerable<ObjectiveTestItem> GetCalculatedItems(LuminanceChromaticityYWTestResult result)
        {
            yield return result.AverageLuminance12X7;
            yield return result.LuminanceUniformity12X7;
            yield return result.ColorUniformity12X7;
            yield return result.AverageLuminance8X7;
            yield return result.LuminanceUniformity8X7;
            yield return result.ColorUniformity8X7;
        }

        private static ObjectiveTestItem CreateItem(string name, double value, RecipeBase recipe, string format, string unit = "", double displayScale = 1) => new()
        {
            Name = name,
            Value = value,
            TestValue = (value * displayScale).ToString(format),
            Unit = unit,
            LowLimit = recipe.Min,
            UpLimit = recipe.Max
        };

        public override void Render(IProcessExecutionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson))
                return;

            var testResult = JsonConvert.DeserializeObject<LuminanceChromaticityYWViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null)
                return;

            foreach (PoiResultCIExyuvData poi in testResult.ViewPoixyuvDatas12X7)
                PoiOverlayRenderer.Add(ctx.ImageView, poi.Point, CVRawOpen.FormatMessage("Y:@Y:F2", poi));
        }

        public override void GenText(IProcessExecutionContext ctx, Paragraph paragraph, Brush foreground, double fontSize)
        {
            var output = new StringBuilder().AppendLine($"YW亮色度测试 ({Config.GetOutputKey()})");
            if (!string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson))
            {
                var result = JsonConvert.DeserializeObject<LuminanceChromaticityYWViewTestResult>(ctx.Result.ViewResultJson);
                if (result != null)
                {
                    AppendGroup(output, "12X7", result.ViewPoixyuvDatas12X7);
                    AppendResult(output, result.AverageLuminance12X7);
                    AppendResult(output, result.LuminanceUniformity12X7);
                    AppendResult(output, result.ColorUniformity12X7);
                    AppendGroup(output, "8X7", result.ViewPoixyuvDatas8X7);
                    AppendResult(output, result.AverageLuminance8X7);
                    AppendResult(output, result.LuminanceUniformity8X7);
                    AppendResult(output, result.ColorUniformity8X7);
                }
            }

            AppendPlainText(paragraph, output.ToString(), foreground, fontSize);
        }

        private static void AppendGroup(StringBuilder output, string groupName, IEnumerable<PoiResultCIExyuvData> points)
        {
            output.AppendLine($"[{groupName}]");
            foreach (PoiResultCIExyuvData item in points)
                output.AppendLine($"{item.Name} X:{item.X:F2} Y:{item.Y:F2} Z:{item.Z:F2} x:{item.x:F4} y:{item.y:F4} u:{item.u:F4} v:{item.v:F4} cct:{item.CCT:F2} wave:{item.Wave:F2}");
        }

        private static void AppendResult(StringBuilder output, ObjectiveTestItem item)
        {
            if (!string.IsNullOrWhiteSpace(item?.Name))
                output.AppendLine($"{item.Name}:{item.TestValue}{item.Unit} LowLimit:{item.LowLimit} UpLimit:{item.UpLimit},Result:{(item.TestResult ? "PASS" : "Fail")}");
        }
    }
}
