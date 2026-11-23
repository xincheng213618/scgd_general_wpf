using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.MTF2;
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using ProjectARVRPro.Recipe;
using System.IO;
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
        internal Func<int, List<AlgResultMasterModel>>? ReadMasters { get; init; }
        internal Func<int, List<DetailCommonModel>>? ReadDetails { get; init; }

        protected abstract void WriteObjectiveResult(ObjectiveTestResult destination, string key, TTestResult result);
        protected virtual IDictionary<string, TTestResult>? GetObjectiveResults(ObjectiveTestResult destination) => null;

        public override Task<bool> Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Result == null || ctx.ObjectiveTestResult == null)
                return Task.FromResult(false);

            string outputKey = Config.GetOutputKey();
            try
            {
                if (ctx.Batch == null)
                    throw new InvalidDataException("MTF07 缺少测量批次。");

                var testResult = new TViewResult();
                HashSet<string> measuredItems = new(StringComparer.Ordinal);
                bool allPassed = true;
                var images = ctx.GetMeasureResults();
                if (images.Count > 0)
                    ctx.Result.FileName = images[0].FileUrl;

                foreach (var master in (ReadMasters?.Invoke(ctx.Batch.Id) ?? ctx.ReadMasterResults())
                    .Where(item => item.ImgFileType == ViewResultAlgType.MTF && item.version == "2.0"))
                {
                    var details = ReadDetails?.Invoke(master.Id) ?? ctx.ReadDetails(DeatilCommonDao.Instance, master.Id);
                    if (details.Count != 1)
                        continue;

                    var detail = new MTFDetailViewReslut(details[0]);
                    testResult.MTFDetailViewReslut = detail;
                    if (detail.MTFResult?.result == null)
                        continue;

                    var correction = new MtfRecipeCorrection(master.Params);
                    foreach (var mtf in detail.MTFResult.result)
                    {
                        if (!Config.TryGetItemName(mtf.name, out string itemName) ||
                            !Config.RecipeConfig.TryGetRecipe(itemName, out RecipeBase recipe) ||
                            !testResult.TryGetItem(itemName, out ObjectiveTestItem item))
                        {
                            continue;
                        }

                        MTF07DynamicResultBuilder.PopulateItem(item, mtf, recipe, Config.ShowConfig, Config.Unit, correction);
                        measuredItems.Add(itemName);
                        allPassed &= item.TestResult;
                    }
                }

                string[] missing = testResult.Items.Where(item => !measuredItems.Contains(item.Name)).Select(item => item.Name).ToArray();
                if (missing.Length != 0)
                    throw new InvalidDataException($"MTF07 缺少测量点：{string.Join("、", missing)}。");

                string viewResultJson = JsonConvert.SerializeObject(testResult);
                TTestResult objectiveResult = JsonConvert.DeserializeObject<TTestResult>(viewResultJson)
                    ?? throw new JsonSerializationException($"无法创建{typeof(TTestResult).Name}。");
                WriteObjectiveResult(ctx.ObjectiveTestResult, outputKey, objectiveResult);
                ctx.Result.ViewResultJson = viewResultJson;
                ctx.Result.Result &= allPassed;
                return Task.FromResult(true);
            }
            catch (Exception error)
            {
                string message = $"MTF07 解析失败 [{outputKey}]：{error.Message}";
                ctx.Log.Error(message, error);
                Reject(ctx, outputKey, message);
                return Task.FromResult(false);
            }
        }

        public override Task<bool> ExecuteFailure(IProcessExecutionContext ctx)
        {
            if (ctx?.Result != null && ctx.ObjectiveTestResult != null)
                Reject(ctx, Config.GetOutputKey(), string.IsNullOrWhiteSpace(ctx.Result.Msg) ? "MTF07 流程未成功完成。" : ctx.Result.Msg);
            return Task.FromResult(false);
        }

        private void Reject(IProcessExecutionContext ctx, string outputKey, string message)
        {
            ctx.Result.Result = false;
            ctx.Result.Msg = message;
            ctx.Result.ViewResultJson = string.Empty;
            ctx.ObjectiveTestResult.TotalResult = false;
            var results = GetObjectiveResults(ctx.ObjectiveTestResult);
            if (results == null) return;
            foreach (string key in results.Keys.Where(key => string.Equals(key, outputKey, StringComparison.OrdinalIgnoreCase)).ToArray())
                results.Remove(key);
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
