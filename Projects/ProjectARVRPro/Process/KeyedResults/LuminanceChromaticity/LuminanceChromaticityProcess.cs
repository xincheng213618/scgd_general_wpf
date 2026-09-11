#pragma warning disable CS8601,CS8602
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Media;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.PoiAnalysis;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor.Draw;
using CVCommCore.CVAlgorithm;
using Newtonsoft.Json;
using ProjectARVRPro.Process.Uniformity;
using ProjectARVRPro.Recipe;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace ProjectARVRPro.Process.KeyedResults.LuminanceChromaticity
{
    public class LuminanceChromaticityProcess : ProcessBase<LuminanceChromaticityProcessConfig>
    {
        public override IRecipeConfig GetRecipeConfig() => Config.RecipeConfig;

        public override Task<bool> Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null || ctx.ObjectiveTestResult == null)
                return Task.FromResult(false);

            try
            {
                var testResult = new LuminanceChromaticityViewTestResult();
                bool calculateUniformityFromCorrectedPoi = Config.CalculateUniformityFromCorrectedPoi;
                string luminanceUniformityResultName = Config.GetLuminanceUniformityResultName();
                string colorUniformityResultName = Config.GetColorUniformityResultName();
                var images = ctx.GetMeasureResults();
                if (images.Count > 0)
                    ctx.Result.FileName = images[0].FileUrl;

                foreach (var master in AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id))
                {
                    if (master.ImgFileType == ViewResultAlgType.POI_XYZ)
                    {
                        ReadPoiResults(ctx, master, testResult);
                    }
                    else if (master.ImgFileType == ViewResultAlgType.PoiAnalysis && !calculateUniformityFromCorrectedPoi)
                        ReadUniformityResult(ctx, master, testResult, luminanceUniformityResultName, colorUniformityResultName);
                }

                if (calculateUniformityFromCorrectedPoi)
                {
                    var calculation = LuminanceChromaticityUniformityCalculator.Calculate(testResult.ViewPoixyuvDatas);
                    if (!calculation.Success)
                    {
                        ctx.Log?.Error($"亮色度本地均匀性计算失败: key={Config.GetOutputKey()}, message={calculation.ErrorMessage}");
                        return Task.FromResult(false);
                    }

                    SetLuminanceUniformity(ctx, testResult, calculation.LuminanceUniformity);
                    SetColorUniformity(ctx, testResult, calculation.ColorUniformity);
                    ctx.Log?.Info($"亮色度均匀性来源: corrected-poi, key={Config.GetOutputKey()}, pointCount={calculation.PointCount}, luminance={testResult.LuminanceUniformity.Value:R}, color={testResult.ColorUniformity.Value:R}");
                }

                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);
                var objectiveResult = JsonConvert.DeserializeObject<LuminanceChromaticityTestResult>(ctx.Result.ViewResultJson) ?? new();
                KeyedTestResultWriter.Write(ctx.ObjectiveTestResult, Config.GetOutputKey(), objectiveResult);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                ctx.Log?.Error(ex);
                return Task.FromResult(false);
            }
        }

        private void ReadPoiResults(IProcessExecutionContext ctx, AlgResultMasterModel master, LuminanceChromaticityViewTestResult testResult)
        {
            ctx.Result.FileName = master.ImgFile;
            testResult.ViewPoixyuvDatas.Clear();
            testResult.PoixyuvDatas.Clear();

            int id = 0;
            foreach (var item in PoiPointResultDao.Instance.GetAllByPid(master.Id))
            {
                var poi = new PoiResultCIExyuvData(item) { Id = id++ };
                ApplyRecipe(poi, Config.RecipeConfig);
                testResult.ViewPoixyuvDatas.Add(poi);
                testResult.PoixyuvDatas.Add(new PoixyuvData
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
                });

                if (string.Equals(item.PoiName, Config.CenterKey, StringComparison.OrdinalIgnoreCase))
                    SetCenterResults(ctx, testResult, poi);
            }
        }

        private static void ApplyRecipe(PoiResultCIExyuvData poi, LuminanceChromaticityRecipeConfig recipe)
        {
            poi.CCT = recipe.CenterCorrelatedColorTemperature.Apply(poi.CCT);
            poi.Y = recipe.CenterLuminance.Apply(poi.Y);
            poi.x = recipe.CenterCIE1931ChromaticCoordinatesx.Apply(poi.x);
            poi.y = recipe.CenterCIE1931ChromaticCoordinatesy.Apply(poi.y);
            poi.u = recipe.CenterCIE1976ChromaticCoordinatesu.Apply(poi.u);
            poi.v = recipe.CenterCIE1976ChromaticCoordinatesv.Apply(poi.v);
        }

        private void SetCenterResults(IProcessExecutionContext ctx, LuminanceChromaticityTestResult testResult, PoiResultCIExyuvData poi)
        {
            var recipe = Config.RecipeConfig;
            testResult.CenterCorrelatedColorTemperature = CreateItem("CenterCorrelatedColorTemperature", poi.CCT, recipe.CenterCorrelatedColorTemperature, "F4", "K");
            testResult.CenterLuminance = CreateItem("CenterLuminance", poi.Y, recipe.CenterLuminance, "F4", "nit");
            testResult.CenterCIE1931ChromaticCoordinatesx = CreateItem("CenterCIE1931ChromaticCoordinatesx", poi.x, recipe.CenterCIE1931ChromaticCoordinatesx, "F4");
            testResult.CenterCIE1931ChromaticCoordinatesy = CreateItem("CenterCIE1931ChromaticCoordinatesy", poi.y, recipe.CenterCIE1931ChromaticCoordinatesy, "F4");
            testResult.CenterCIE1976ChromaticCoordinatesu = CreateItem("CenterCIE1976ChromaticCoordinatesu", poi.u, recipe.CenterCIE1976ChromaticCoordinatesu, "F4");
            testResult.CenterCIE1976ChromaticCoordinatesv = CreateItem("CenterCIE1976ChromaticCoordinatesv", poi.v, recipe.CenterCIE1976ChromaticCoordinatesv, "F4");

            ctx.Result.Result &= testResult.CenterCorrelatedColorTemperature.TestResult;
            ctx.Result.Result &= testResult.CenterLuminance.TestResult;
            ctx.Result.Result &= testResult.CenterCIE1931ChromaticCoordinatesx.TestResult;
            ctx.Result.Result &= testResult.CenterCIE1931ChromaticCoordinatesy.TestResult;
            ctx.Result.Result &= testResult.CenterCIE1976ChromaticCoordinatesu.TestResult;
            ctx.Result.Result &= testResult.CenterCIE1976ChromaticCoordinatesv.TestResult;
        }

        private void ReadUniformityResult(
            IProcessExecutionContext ctx,
            AlgResultMasterModel master,
            LuminanceChromaticityTestResult testResult,
            string luminanceUniformityResultName,
            string colorUniformityResultName)
        {
            bool isLuminanceUniformity = LuminanceChromaticityUniformityCalculator.MatchesResultName(master.TName, luminanceUniformityResultName);
            bool isColorUniformity = LuminanceChromaticityUniformityCalculator.MatchesResultName(master.TName, colorUniformityResultName);
            if (isLuminanceUniformity && isColorUniformity)
                throw new InvalidOperationException($"同一个PoiAnalysis结果同时匹配亮度和色度均匀性TName: {master.TName}");
            if (!isLuminanceUniformity && !isColorUniformity)
                return;

            var details = DeatilCommonDao.Instance.GetAllByPid(master.Id);
            if (details.Count != 1)
                return;

            var value = new PoiAnalysisDetailViewReslut(details[0]).PoiAnalysisResult.result.Value;
            if (isLuminanceUniformity)
                SetLuminanceUniformity(ctx, testResult, value);
            else
                SetColorUniformity(ctx, testResult, value);
        }

        private void SetLuminanceUniformity(IProcessExecutionContext ctx, LuminanceChromaticityTestResult testResult, double value)
        {
            value = Config.RecipeConfig.LuminanceUniformity.Apply(value);
            testResult.LuminanceUniformity = CreateItem("Luminance_uniformity(%)", value, Config.RecipeConfig.LuminanceUniformity, "F4", "%", 100);
            ctx.Result.Result &= testResult.LuminanceUniformity.TestResult;
        }

        private void SetColorUniformity(IProcessExecutionContext ctx, LuminanceChromaticityTestResult testResult, double value)
        {
            value = Config.RecipeConfig.ColorUniformity.Apply(value);
            testResult.ColorUniformity = CreateItem("Color_uniformity", value, Config.RecipeConfig.ColorUniformity, "F5");
            ctx.Result.Result &= testResult.ColorUniformity.TestResult;
        }

        private static ObjectiveTestItem CreateItem(string name, double value, RecipeBase recipe, string format, string unit = "", double displayScale = 1)
        {
            return new ObjectiveTestItem
            {
                Name = name,
                Value = value,
                TestValue = (value * displayScale).ToString(format),
                Unit = unit,
                LowLimit = recipe.Min,
                UpLimit = recipe.Max
            };
        }

        public override IReadOnlyList<ObjectiveTestCsvRow> GetObjectiveCsvRows(ProjectARVRReuslt result) =>
            GetObjectiveCsvRows<LuminanceChromaticityTestResult>(result, Config.GetOutputKey());

        public override void Render(IProcessExecutionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson))
                return;

            var testResult = JsonConvert.DeserializeObject<LuminanceChromaticityViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null)
                return;

            foreach (var poi in testResult.ViewPoixyuvDatas)
            {
                PoiOverlayRenderer.Add(ctx.ImageView, poi.Point, CVRawOpen.FormatMessage(CVCIEShowConfig.Instance.Template, poi));
            }
        }

        public override void GenText(IProcessExecutionContext ctx, Paragraph paragraph, Brush foreground, double fontSize)
        {
            var output = new StringBuilder().AppendLine($"亮色度测试 ({Config.GetOutputKey()})");
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson))
            {
                AppendPlainText(paragraph, output.ToString(), foreground, fontSize);
                return;
            }

            var testResult = JsonConvert.DeserializeObject<LuminanceChromaticityViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null)
            {
                AppendPlainText(paragraph, output.ToString(), foreground, fontSize);
                return;
            }

            foreach (var item in testResult.ViewPoixyuvDatas)
                output.AppendLine($"X:{item.X:F2} Y:{item.Y:F2} Z:{item.Z:F2} x:{item.x:F2} y:{item.y:F2} u:{item.u:F2} v:{item.v:F2} cct:{item.CCT:F2} wave:{item.Wave:F2}");

            AppendResult(output, testResult.LuminanceUniformity);
            AppendResult(output, testResult.ColorUniformity);
            AppendPlainText(paragraph, output.ToString(), foreground, fontSize);
        }

        private static void AppendResult(StringBuilder output, ObjectiveTestItem item)
        {
            if (string.IsNullOrWhiteSpace(item?.Name))
                return;

            output.AppendLine($"{item.Name}:{item.TestValue}{item.Unit} LowLimit:{item.LowLimit} UpLimit:{item.UpLimit},Result:{(item.TestResult ? "PASS" : "Fail")}");
        }
    }
}
