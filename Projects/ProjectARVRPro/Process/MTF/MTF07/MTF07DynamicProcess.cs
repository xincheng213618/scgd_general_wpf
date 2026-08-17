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

namespace ProjectARVRPro.Process.MTF.MTF07
{
    public interface IMTF07DynamicProcessConfig
    {
        string ShowConfig { get; }
        string Unit { get; }
        string GetOutputKey();
    }

    public interface IMTF07DynamicRecipeConfig
    {
        RecipeBase UnifiedRecipe { get; }
    }

    public abstract class MTF07DynamicProcess<TConfig, TRecipe> : ProcessWithRecipeBase<TConfig, TRecipe>
        where TConfig : ProcessConfigBase<TRecipe>, IMTF07DynamicProcessConfig, new()
        where TRecipe : class, IRecipeConfig, IMTF07DynamicRecipeConfig, new()
    {
        protected abstract string Axis { get; }

        public override Task<bool> Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null || ctx.ObjectiveTestResult == null)
                return Task.FromResult(false);

            try
            {
                var testResult = new MTF07DynamicViewTestResult();
                var items = new Dictionary<string, ObjectiveTestItem>(StringComparer.OrdinalIgnoreCase);
                var images = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                if (images.Count > 0)
                    ctx.Result.FileName = images[0].FileUrl;

                foreach (var master in AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id)
                    .Where(item => item.ImgFileType == ViewResultAlgType.MTF && item.version == "2.0"))
                {
                    var details = DeatilCommonDao.Instance.GetAllByPid(master.Id);
                    if (details.Count != 1)
                        continue;

                    var detail = new MTFDetailViewReslut(details[0]);
                    testResult.MTFDetailViewReslut = detail;
                    if (detail.MTFResult?.result == null)
                        continue;

                    foreach (var mtf in detail.MTFResult.result)
                    {
                        ObjectiveTestItem? item = MTF07DynamicResultBuilder.CreateItem(
                            Axis,
                            mtf,
                            Config.RecipeConfig.UnifiedRecipe,
                            Config.ShowConfig,
                            Config.Unit);
                        if (item != null)
                            items[item.Name] = item;
                    }
                }

                testResult.Items = new ObservableCollection<ObjectiveTestItem>(items.Values);
                foreach (ObjectiveTestItem item in testResult.Items)
                    ctx.Result.Result &= item.TestResult;

                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);
                ctx.ObjectiveTestResult.DynamicTestResults ??= new();
                ctx.ObjectiveTestResult.DynamicTestResults[Config.GetOutputKey()] = testResult.Items;
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                ctx.Log?.Error(ex);
                return Task.FromResult(false);
            }
        }

        public override void Render(IProcessExecutionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson))
                return;

            var testResult = JsonConvert.DeserializeObject<MTF07DynamicViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult?.MTFDetailViewReslut?.MTFResult?.result == null)
                return;

            int id = 0;
            foreach (var item in testResult.MTFDetailViewReslut.MTFResult.result)
            {
                if (!MTF07DynamicResultBuilder.MatchesAxis(Axis, item.name))
                    continue;

                DVRectangleText rectangle = new();
                rectangle.Attribute.Rect = new Rect(item.x, item.y, item.w, item.h);
                rectangle.Attribute.Brush = Brushes.Transparent;
                rectangle.Attribute.Pen = new Pen(Brushes.Red, 1);
                rectangle.Attribute.Id = ++id;
                rectangle.Attribute.Msg = item.mtfValue?.ToString(Config.ShowConfig);
                rectangle.Render();
                ctx.ImageView.AddVisual(rectangle);
            }
        }

        public override void GenText(IProcessExecutionContext ctx, System.Windows.Documents.Paragraph paragraph, Brush foreground, double fontSize)
        {
            var output = new StringBuilder().AppendLine($"{Config.GetOutputKey()} 画面结果");
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson))
            {
                AppendPlainText(paragraph, output.ToString(), foreground, fontSize);
                return;
            }

            var testResult = JsonConvert.DeserializeObject<MTF07DynamicTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null)
            {
                AppendPlainText(paragraph, output.ToString(), foreground, fontSize);
                return;
            }

            output.AppendLine("Name,Value,Unit,LowLimit,UpLimit,Result");
            foreach (ObjectiveTestItem item in testResult.Items)
                output.AppendLine($"{item.Name},{item.Value},{item.Unit},{item.LowLimit},{item.UpLimit},{item.TestResult}");

            AppendPlainText(paragraph, output.ToString(), foreground, fontSize);
        }
    }
}
