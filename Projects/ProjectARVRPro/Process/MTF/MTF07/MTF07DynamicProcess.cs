using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.MTF2;
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using ProjectARVRPro.Recipe;
using System.Text;
using System.Windows.Media;

namespace ProjectARVRPro.Process.MTF.MTF07
{
    public interface IMTF07DynamicProcessConfig
    {
        string ShowConfig { get; }
        string Unit { get; }
        string GetOutputKey();
        bool TryGetItemName(string? sourceName, out string itemName);
    }

    public interface IMTF07DynamicRecipeConfig
    {
        bool TryGetRecipe(string itemName, out RecipeBase recipe);
    }

    public interface IMTF07TestResult
    {
        IReadOnlyList<ObjectiveTestItem> Items { get; }
        bool TryGetItem(string itemName, out ObjectiveTestItem item);
    }

    public interface IMTF07ViewTestResult : IMTF07TestResult
    {
        MTFDetailViewReslut? MTFDetailViewReslut { get; set; }
    }

    /// <summary>
    /// MTF07条纹图案共享解析基类。H横条纹、V竖条纹均读取MTFResult.result[].mtfValue；
    /// 中心0F和四角0.7F分别配置解析Key与Recipe，HV特殊图案读取resultChild，不属于此流程。
    /// </summary>
    public abstract class MTF07DynamicProcess<TConfig, TRecipe, TViewResult, TTestResult> : ProcessWithRecipeBase<TConfig, TRecipe>
        where TConfig : ProcessConfigBase<TRecipe>, IMTF07DynamicProcessConfig, new()
        where TRecipe : class, IRecipeConfig, IMTF07DynamicRecipeConfig, new()
        where TTestResult : class, IMTF07TestResult
        where TViewResult : TTestResult, IMTF07ViewTestResult, new()
    {
        protected abstract void WriteObjectiveResult(ObjectiveTestResult destination, string key, TTestResult result);

        public override Task<bool> Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null || ctx.ObjectiveTestResult == null)
                return Task.FromResult(false);

            try
            {
                var testResult = new TViewResult();
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
                        if (!Config.TryGetItemName(mtf.name, out string itemName) ||
                            !Config.RecipeConfig.TryGetRecipe(itemName, out RecipeBase recipe) ||
                            !testResult.TryGetItem(itemName, out ObjectiveTestItem item))
                        {
                            continue;
                        }

                        MTF07DynamicResultBuilder.PopulateItem(item, mtf, recipe, Config.ShowConfig, Config.Unit);
                        ctx.Result.Result &= item.TestResult;
                    }
                }

                string viewResultJson = JsonConvert.SerializeObject(testResult);
                ctx.Result.ViewResultJson = viewResultJson;
                TTestResult objectiveResult = JsonConvert.DeserializeObject<TTestResult>(viewResultJson)
                    ?? throw new JsonSerializationException($"无法创建{typeof(TTestResult).Name}。");
                string outputKey = Config.GetOutputKey();
                WriteObjectiveResult(ctx.ObjectiveTestResult, outputKey, objectiveResult);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                ctx.Log?.Error(ex);
                return Task.FromResult(false);
            }
        }

        public override IReadOnlyList<ObjectiveTestCsvRow> GetObjectiveCsvRows(ProjectARVRReuslt result) =>
            GetObjectiveCsvRows<TTestResult>(result, Config.GetOutputKey());

        public override void Render(IProcessExecutionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson))
                return;

            var testResult = JsonConvert.DeserializeObject<TViewResult>(ctx.Result.ViewResultJson);
            if (testResult?.MTFDetailViewReslut?.MTFResult?.result == null)
                return;

            int id = 0;
            foreach (var item in testResult.MTFDetailViewReslut.MTFResult.result)
            {
                if (!Config.TryGetItemName(item.name, out _))
                    continue;

                id++;
                if (!ProcessExtensions.TryCreateMtfOverlay(item, id, Config.ShowConfig, out DVRectangleText rectangle))
                    continue;

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

            var testResult = JsonConvert.DeserializeObject<TTestResult>(ctx.Result.ViewResultJson);
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
