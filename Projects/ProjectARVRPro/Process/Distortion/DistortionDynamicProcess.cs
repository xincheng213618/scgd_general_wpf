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
        public override bool Execute(IProcessExecutionContext ctx)
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
                            distortion.DistortionReslut.TVDistortion.HorizontalRatio *= recipeConfig.HorizontalTVDistortion.Fix;
                            distortion.DistortionReslut.TVDistortion.VerticalRatio *= recipeConfig.VerticalTVDistortion.Fix;

                            foreach (var pt in distortion.DistortionReslut.TVDistortion.FinalPoints)
                            {
                                distortionResult.Points.Add(new System.Windows.Point(pt.X, pt.Y));
                            }

                            distortionResult.HorizontalTVDistortion = Build(
                                "HorizontalTVDistortion",
                                distortion.DistortionReslut.TVDistortion.HorizontalRatio,
                                recipeConfig.HorizontalTVDistortion.Min,
                                recipeConfig.HorizontalTVDistortion.Max);

                            distortionResult.VerticalTVDistortion = Build(
                                "VerticalTVDistortion",
                                distortion.DistortionReslut.TVDistortion.VerticalRatio,
                                recipeConfig.VerticalTVDistortion.Min,
                                recipeConfig.VerticalTVDistortion.Max);

                            UpdateResult(ctx, distortionResult.HorizontalTVDistortion, distortionResult.VerticalTVDistortion);
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

        public override string GenText(IProcessExecutionContext ctx)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"{GetOutputName()} 画面结果");

            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return sb.ToString();

            DistortionDynamicTestResult? testResult = JsonConvert.DeserializeObject<DistortionDynamicTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return sb.ToString();

            sb.AppendLine("Name,Value,Unit,LowLimit,UpLimit,Result");
            foreach (var item in testResult.Items)
            {
                sb.AppendLine($"{item.Name},{item.Value},{item.Unit},{item.LowLimit},{item.UpLimit},{item.TestResult}");
            }

            return sb.ToString();
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
