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
                var images = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                if (images.Count > 0)
                    ctx.Result.FileName = images[0].FileUrl;

                foreach (var master in AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id))
                {
                    if (master.ImgFileType == ViewResultAlgType.POI_XYZ)
                    {
                        ReadPoiResults(ctx, master, testResult);
                    }
                    else if (master.ImgFileType == ViewResultAlgType.PoiAnalysis)
                        ReadUniformityResult(ctx, master, testResult);
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

        private void ReadUniformityResult(IProcessExecutionContext ctx, AlgResultMasterModel master, LuminanceChromaticityTestResult testResult)
        {
            var details = DeatilCommonDao.Instance.GetAllByPid(master.Id);
            if (details.Count != 1)
                return;

            var value = new PoiAnalysisDetailViewReslut(details[0]).PoiAnalysisResult.result.Value;
            if (master.TName.Contains("Luminance_uniformity", StringComparison.OrdinalIgnoreCase))
            {
                value = Config.RecipeConfig.LuminanceUniformity.Apply(value);
                testResult.LuminanceUniformity = CreateItem("Luminance_uniformity(%)", value, Config.RecipeConfig.LuminanceUniformity, "F4", "%", 100);
                ctx.Result.Result &= testResult.LuminanceUniformity.TestResult;
            }
            else if (master.TName.Contains("Color_uniformity", StringComparison.OrdinalIgnoreCase))
            {
                value = Config.RecipeConfig.ColorUniformity.Apply(value);
                testResult.ColorUniformity = CreateItem("Color_uniformity", value, Config.RecipeConfig.ColorUniformity, "F5");
                ctx.Result.Result &= testResult.ColorUniformity.TestResult;
            }
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

        public override void Render(IProcessExecutionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson))
                return;

            var testResult = JsonConvert.DeserializeObject<LuminanceChromaticityViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null)
                return;

            foreach (var poi in testResult.ViewPoixyuvDatas)
            {
                var point = poi.Point;
                switch (point.PointType)
                {
                    case POIPointTypes.Circle:
                        var circle = CreateCircle(point, poi);
                        circle.Render();
                        ctx.ImageView.AddVisual(circle);
                        break;
                    case POIPointTypes.Rect:
                        var rectangle = CreateRectangle(point, poi);
                        rectangle.Render();
                        ctx.ImageView.AddVisual(rectangle);
                        break;
                }
            }
        }

        private static DVCircleText CreateCircle(POIPoint point, PoiResultCIExyuvData poi)
        {
            var circle = new DVCircleText();
            circle.Attribute.Center = new Point(point.PixelX, point.PixelY);
            circle.Attribute.Radius = point.Radius;
            circle.Attribute.Brush = Brushes.Transparent;
            circle.Attribute.Pen = new Pen(Brushes.Red, 1);
            circle.Attribute.Id = point.Id ?? -1;
            circle.Attribute.Text = point.Name;
            circle.Attribute.Msg = CVRawOpen.FormatMessage(CVCIEShowConfig.Instance.Template, poi);
            return circle;
        }

        private static DVRectangleText CreateRectangle(POIPoint point, PoiResultCIExyuvData poi)
        {
            var rectangle = new DVRectangleText();
            rectangle.Attribute.Rect = new Rect(point.PixelX - point.Width / 2, point.PixelY - point.Height / 2, point.Width, point.Height);
            rectangle.Attribute.Brush = Brushes.Transparent;
            rectangle.Attribute.Pen = new Pen(Brushes.Red, 1);
            rectangle.Attribute.Id = point.Id ?? -1;
            rectangle.Attribute.Text = point.Name;
            rectangle.Attribute.Msg = CVRawOpen.FormatMessage(CVCIEShowConfig.Instance.Template, poi);
            return rectangle;
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
