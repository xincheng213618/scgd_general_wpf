using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.Distortion2;
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Media;

namespace ProjectARVRPro.Process.Distortion
{
    public class DistortionDynamicProcess : ProcessBase<DistortionDynamicProcessConfig>
    {
        public override async Task<bool> Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Log;
            DistortionRecipeConfig recipeConfig = ctx.RecipeConfig.GetRequiredService<DistortionRecipeConfig>();
            DistortionDynamicViewTestResult testResult = new DistortionDynamicViewTestResult();
            DistortionViewTestResult distortionResult = testResult.DistortionViewTestResult;

            try
            {
                var values = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                if (values.Count > 0)
                {
                    string? fileUrl = values[0].FileUrl;
                    if (!string.IsNullOrWhiteSpace(fileUrl))
                        ctx.Result.FileName = fileUrl;
                }

                var masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                foreach (var master in masters)
                {
                    if (master.ImgFileType == ViewResultAlgType.Distortion && master.version == "2.0")
                    {
                        var details = DeatilCommonDao.Instance.GetAllByPid(master.Id);
                        if (details.Count == 1)
                        {
                            var distortion = new Distortion2View(details[0]);
                            var distortionData = distortion.DistortionReslut;
                            if (distortionData == null)
                                continue;

                            ApplySelectedPoints(distortionResult, distortionData, Config.PointSource);

                            if (distortionData.OpticDistortion != null)
                            {
                                distortionData.OpticDistortion.OpticRatio = recipeConfig.OpticDistortion.Apply(distortionData.OpticDistortion.OpticRatio);

                                distortionResult.OpticDistortion = Build(
                                    "Optic_Distortion",
                                    distortionData.OpticDistortion.OpticRatio,
                                    recipeConfig.OpticDistortion.Min,
                                    recipeConfig.OpticDistortion.Max);
                            }

                            if (distortionData.TVDistortion != null)
                            {
                                distortionData.TVDistortion.HorizontalRatio = recipeConfig.HorizontalTVDistortion.Apply(distortionData.TVDistortion.HorizontalRatio);
                                distortionData.TVDistortion.VerticalRatio = recipeConfig.VerticalTVDistortion.Apply(distortionData.TVDistortion.VerticalRatio);

                                distortionResult.HorizontalTVDistortion = Build(
                                    "HorizontalTVDistortion",
                                    distortionData.TVDistortion.HorizontalRatio,
                                    recipeConfig.HorizontalTVDistortion.Min,
                                    recipeConfig.HorizontalTVDistortion.Max);

                                distortionResult.VerticalTVDistortion = Build(
                                    "VerticalTVDistortion",
                                    distortionData.TVDistortion.VerticalRatio,
                                    recipeConfig.VerticalTVDistortion.Min,
                                    recipeConfig.VerticalTVDistortion.Max);
                            }

                            if (distortionData.Point9Distortion != null)
                            {
                                distortionData.Point9Distortion.TopRatio = recipeConfig.DistortionTop.Apply(distortionData.Point9Distortion.TopRatio);
                                distortionData.Point9Distortion.BottomRatio = recipeConfig.DistortionBottom.Apply(distortionData.Point9Distortion.BottomRatio);
                                distortionData.Point9Distortion.LeftRatio = recipeConfig.DistortionLeft.Apply(distortionData.Point9Distortion.LeftRatio);
                                distortionData.Point9Distortion.RightRatio = recipeConfig.DistortionRight.Apply(distortionData.Point9Distortion.RightRatio);
                                distortionData.Point9Distortion.KeyStoneHoriRatio = recipeConfig.KeystoneHoriz.Apply(distortionData.Point9Distortion.KeyStoneHoriRatio);
                                distortionData.Point9Distortion.KeyStoneVercRatio = recipeConfig.KeystoneVert.Apply(distortionData.Point9Distortion.KeyStoneVercRatio);

                                distortionResult.DistortionTop = Build(
                                    "DistortionTop",
                                    distortionData.Point9Distortion.TopRatio,
                                    recipeConfig.DistortionTop.Min,
                                    recipeConfig.DistortionTop.Max);
                                distortionResult.DistortionBottom = Build(
                                    "DistortionBottom",
                                    distortionData.Point9Distortion.BottomRatio,
                                    recipeConfig.DistortionBottom.Min,
                                    recipeConfig.DistortionBottom.Max);
                                distortionResult.DistortionLeft = Build(
                                    "DistortionLeft",
                                    distortionData.Point9Distortion.LeftRatio,
                                    recipeConfig.DistortionLeft.Min,
                                    recipeConfig.DistortionLeft.Max);
                                distortionResult.DistortionRight = Build(
                                    "DistortionRight",
                                    distortionData.Point9Distortion.RightRatio,
                                    recipeConfig.DistortionRight.Min,
                                    recipeConfig.DistortionRight.Max);
                                distortionResult.KeystoneHoriz = Build(
                                    "KeystoneHoriz",
                                    distortionData.Point9Distortion.KeyStoneHoriRatio,
                                    recipeConfig.KeystoneHoriz.Min,
                                    recipeConfig.KeystoneHoriz.Max);
                                distortionResult.KeystoneVert = Build(
                                    "KeystoneVert",
                                    distortionData.Point9Distortion.KeyStoneVercRatio,
                                    recipeConfig.KeystoneVert.Min,
                                    recipeConfig.KeystoneVert.Max);
                            }

                            UpdateResult(
                                ctx,
                                distortionResult.HorizontalTVDistortion,
                                distortionResult.VerticalTVDistortion,
                                distortionResult.OpticDistortion,
                                distortionResult.DistortionTop,
                                distortionResult.DistortionBottom,
                                distortionResult.DistortionLeft,
                                distortionResult.DistortionRight,
                                distortionResult.KeystoneHoriz,
                                distortionResult.KeystoneVert);
                        }
                    }
                }

                testResult.Items = CollectItems(distortionResult);
                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);
                ctx.ObjectiveTestResult.DynamicTestResults[GetOutputName()] = testResult.Items;
                return true;
            }
            catch (Exception ex)
            {
                log?.Error(ex);
                return false;
            }
        }

        public override void Render(IProcessExecutionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return;
            DistortionDynamicViewTestResult? testResult = JsonConvert.DeserializeObject<DistortionDynamicViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return;

            foreach (var points in testResult.DistortionViewTestResult.Points)
            {
                DVCircleText circle = new();
                circle.Attribute.Center = new System.Windows.Point(points.X, points.Y);
                circle.Attribute.Radius = 200;
                circle.Attribute.Brush = Brushes.Transparent;
                circle.Attribute.Pen = new Pen(Brushes.Red, 1);
                circle.Attribute.Text = $"{Environment.NewLine} X:{points.X:F0}{Environment.NewLine}Y:{points.Y:F0}";
                circle.Render();
                ctx.ImageView.AddVisual(circle);
            }
        }

        public override void GenText(IProcessExecutionContext ctx, System.Windows.Documents.Paragraph paragraph, System.Windows.Media.Brush foreground, double fontSize)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"{GetOutputName()} 画面结果");

            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) { AppendPlainText(paragraph, sb.ToString(), foreground, fontSize); return; }

            DistortionDynamicTestResult? testResult = JsonConvert.DeserializeObject<DistortionDynamicTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) { AppendPlainText(paragraph, sb.ToString(), foreground, fontSize); return; }

            sb.AppendLine("Name,Value,Unit,LowLimit,UpLimit,Result");
            foreach (var item in testResult.Items)
            {
                sb.AppendLine($"{item.Name},{item.Value},{item.Unit},{item.LowLimit},{item.UpLimit},{item.TestResult}");
            }

            AppendPlainText(paragraph, sb.ToString(), foreground, fontSize); return;
        }

        public override IRecipeConfig GetRecipeConfig()
        {
            return RecipeManager.GetInstance().RecipeConfig.GetRequiredService<DistortionRecipeConfig>();
        }

        private ObjectiveTestItem Build(string name, double value, double low, double up) => new ObjectiveTestItem
        {
            Name = name,
            LowLimit = low,
            UpLimit = up,
            Value = value,
            TestValue = value.ToString(Config.ShowConfig),
            Unit = Config.Unit
        };

        private static void ApplySelectedPoints(DistortionViewTestResult distortionResult, DistortionReslut distortionData, DistortionPointSource pointSource)
        {
            distortionResult.Points.Clear();
            var points = DistortionPointSourceHelper.GetPoints(distortionData, pointSource);
            if (points == null)
                return;

            foreach (var pt in points)
            {
                distortionResult.Points.Add(new System.Windows.Point(pt.X, pt.Y));
            }
        }

        private static void UpdateResult(IProcessExecutionContext ctx, params ObjectiveTestItem[] items)
        {
            foreach (var item in items)
            {
                if (item != null)
                    ctx.Result.Result &= item.TestResult;
            }
        }

        private static ObservableCollection<ObjectiveTestItem> CollectItems(DistortionTestResult result)
        {
            ObservableCollection<ObjectiveTestItem> items = new ObservableCollection<ObjectiveTestItem>();
            AddIfNotNull(items, result.HorizontalTVDistortion);
            AddIfNotNull(items, result.VerticalTVDistortion);
            AddIfNotNull(items, result.OpticDistortion);
            AddIfNotNull(items, result.DistortionTop);
            AddIfNotNull(items, result.DistortionBottom);
            AddIfNotNull(items, result.DistortionLeft);
            AddIfNotNull(items, result.DistortionRight);
            AddIfNotNull(items, result.KeystoneHoriz);
            AddIfNotNull(items, result.KeystoneVert);
            return items;
        }

        private static void AddIfNotNull(ObservableCollection<ObjectiveTestItem> items, ObjectiveTestItem? item)
        {
            if (item != null)
                items.Add(item);
        }

        private string GetOutputName()
        {
            return string.IsNullOrWhiteSpace(Config.Name) ? "Distortion" : Config.Name.Trim();
        }
    }
}
