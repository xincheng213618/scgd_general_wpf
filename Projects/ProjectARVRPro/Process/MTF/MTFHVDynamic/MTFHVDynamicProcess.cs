using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.MTF2;
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using ProjectARVRPro.Recipe;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace ProjectARVRPro.Process.MTF.MTFHVDynamic
{
    public class MTFHVDynamicProcess : ProcessBase<MTFHVDynamicProcessConfig>
    {
        public override IRecipeConfig GetRecipeConfig() => Config.RecipeConfig;

        public override bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Log;
            MTFHVDynamicViewTestResult testResult = new MTFHVDynamicViewTestResult();

            try
            {
                string outputName = GetOutputName();
                var values = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                if (values.Count > 0)
                {
                    string? fileUrl = values[0].FileUrl;
                    if (!string.IsNullOrWhiteSpace(fileUrl))
                        ctx.Result.FileName = fileUrl;
                }

                Dictionary<string, ObjectiveTestItem> itemMap = new Dictionary<string, ObjectiveTestItem>(StringComparer.OrdinalIgnoreCase);

                var masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                foreach (var master in masters.Where(m => m.ImgFileType == ViewResultAlgType.MTF && m.version == "2.0"))
                {
                    var details = DeatilCommonDao.Instance.GetAllByPid(master.Id);
                    if (details.Count != 1)
                        continue;

                    var mtfDetail = new MTFDetailViewReslut(details[0]);
                    testResult.MTFDetailViewReslut = mtfDetail;
                    if (mtfDetail.MTFResult?.resultChild == null)
                        continue;

                    foreach (var mtf in mtfDetail.MTFResult.resultChild)
                    {
                        if (string.IsNullOrWhiteSpace(mtf.name))
                            continue;

                        AddItem(ctx, itemMap, BuildItem("H", mtf.name, mtf.horizontalAverage, Config.RecipeConfig.HRecipe));
                        AddItem(ctx, itemMap, BuildItem("V", mtf.name, mtf.verticalAverage, Config.RecipeConfig.VRecipe));
                    }
                }

                testResult.Items = new ObservableCollection<ObjectiveTestItem>(itemMap.Values);
                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);
                ctx.ObjectiveTestResult.DynamicTestResults[outputName] = testResult.Items;  
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
            MTFHVDynamicViewTestResult? testResult = JsonConvert.DeserializeObject<MTFHVDynamicViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult?.MTFDetailViewReslut?.MTFResult?.result == null)
                return;

            int id = 0;
            foreach (var item in testResult.MTFDetailViewReslut.MTFResult.result)
            {
                id++;
                DVRectangleText rectangle = new();
                rectangle.Attribute.Rect = new Rect(item.x, item.y, item.w, item.h);
                rectangle.Attribute.Brush = Brushes.Transparent;
                rectangle.Attribute.Pen = new Pen(Brushes.Red, 1);
                rectangle.Attribute.Id = id;
                rectangle.Attribute.Msg = item.mtfValue?.ToString(Config.ShowConfig);
                rectangle.Render();
                ctx.ImageView.AddVisual(rectangle);
            }
        }

        public override void GenText(IProcessExecutionContext ctx, System.Windows.Documents.Paragraph paragraph, System.Windows.Media.Brush foreground, double fontSize)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"{GetOutputName()} 画面结果");

            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) { AppendPlainText(paragraph, sb.ToString(), foreground, fontSize); return; }

            MTFHVDynamicTestResult? testResult = JsonConvert.DeserializeObject<MTFHVDynamicTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) { AppendPlainText(paragraph, sb.ToString(), foreground, fontSize); return; }

            sb.AppendLine("Name,Value,Unit,LowLimit,UpLimit,Result");
            foreach (var item in testResult.Items)
            {
                sb.AppendLine($"{item.Name},{item.Value},{item.Unit},{item.LowLimit},{item.UpLimit},{item.TestResult}");
            }

            AppendPlainText(paragraph, sb.ToString(), foreground, fontSize); return;
        }

        private ObjectiveTestItem BuildItem(string axis, string pointName, double value, RecipeBase recipe)
        {
            string itemName = $"MTF_HV_{axis}_{NormalizePointName(pointName)}";
            double fixedValue = recipe.Apply(value);
            return new ObjectiveTestItem
            {
                Name = itemName,
                Unit = Config.Unit,
                Value = fixedValue,
                TestValue = fixedValue.ToString(Config.ShowConfig),
                LowLimit = recipe.Min,
                UpLimit = recipe.Max
            };
        }

        private static void AddItem(IProcessExecutionContext ctx, Dictionary<string, ObjectiveTestItem> itemMap, ObjectiveTestItem item)
        {
            itemMap[item.Name] = item;
            ctx.Result.Result &= item.TestResult;
        }

        private string GetOutputName()
        {
            return string.IsNullOrWhiteSpace(Config.Name) ? "MTFHV" : Config.Name.Trim();
        }

        private static string NormalizePointName(string pointName)
        {
            string name = pointName.Trim();
            const string marker = "_MTF_HV_";
            int markerIndex = name.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex > 0 && markerIndex + marker.Length < name.Length)
            {
                string frequency = name[..markerIndex];
                string position = name[(markerIndex + marker.Length)..];
                name = $"{position}_{frequency}";
            }

            return name.Replace('.', '_');
        }
    }
}
