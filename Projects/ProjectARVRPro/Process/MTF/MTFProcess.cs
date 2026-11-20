#pragma warning disable CS8601
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.MTF2;
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using System.IO;
using System.Text;

namespace ProjectARVRPro.Process.MTF
{
    public class MTFProcess : ProcessBase<MTFProcessConfig>
    {
        internal Func<int, List<AlgResultMasterModel>>? ReadMasters { get; init; }
        internal Func<int, List<DetailCommonModel>>? ReadDetails { get; init; }

        public override IRecipeConfig GetRecipeConfig() => Config.RecipeConfig;

        public override Task<bool> Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Result == null || ctx.ObjectiveTestResult == null)
                return Task.FromResult(false);

            string outputKey = Config.Name;
            try
            {
                if (ctx.Batch == null)
                    throw new InvalidDataException("MTF 缺少测量批次。");

                var values = ctx.GetMeasureResults();
                if (values.Count > 0)
                    ctx.Result.FileName = values[0].FileUrl;

                var testResult = new MTFViewTestResult();
                var recipe = Config.RecipeConfig.UnifiedRecipe;
                bool allPassed = true;
                foreach (var master in (ReadMasters?.Invoke(ctx.Batch.Id) ?? ctx.ReadMasterResults()).Where(m => m.ImgFileType == ViewResultAlgType.MTF && m.version == "2.0"))
                {
                    var details = ReadDetails?.Invoke(master.Id) ?? ctx.ReadDetails(DeatilCommonDao.Instance, master.Id);
                    if (details.Count != 1)
                        continue;

                    var detail = new MTFDetailViewReslut(details[0]);
                    if (detail.MTFResult?.result is not { Count: > 0 } measurements)
                        continue;

                    var correction = new MtfRecipeCorrection(master.Params);
                    foreach (var mtf in measurements)
                    {
                        if (mtf?.mtfValue is not double measured || !double.IsFinite(measured))
                            throw new InvalidDataException($"MTF 结果 ID {master.Id} 的测量点 [{mtf?.name}] 缺少有限的测量值。");

                        double corrected = correction.Apply(measured, recipe, mtf.name);
                        var item = new ObjectiveTestItem
                        {
                            Name = mtf.name,
                            Unit = Config.Unit,
                            Value = corrected,
                            TestValue = corrected.ToString(Config.ShowConfig),
                            LowLimit = recipe.Min,
                            UpLimit = recipe.Max,
                        };
                        allPassed &= item.TestResult;
                        testResult.Items.Add(item);
                    }
                    testResult.MTFDetailViewReslut = detail;
                }

                if (testResult.Items.Count == 0)
                    throw new InvalidDataException($"批次 {ctx.Batch.Id} 没有可判定的 MTF 2.0 测量点。");

                string snapshot = JsonConvert.SerializeObject(testResult);
                (ctx.ObjectiveTestResult.DynamicTestResults ??= new())[outputKey] = testResult.Items;
                ctx.Result.ViewResultJson = snapshot;
                ctx.Result.Result &= allPassed;
                return Task.FromResult(true);
            }
            catch (Exception error)
            {
                string message = $"MTF 解析失败 [{outputKey}]：{error.Message}";
                ctx.Log.Error(message, error);
                Reject(ctx, outputKey, message);
                return Task.FromResult(false);
            }
        }

        public override Task<bool> ExecuteFailure(IProcessExecutionContext ctx)
        {
            if (ctx?.Result != null && ctx.ObjectiveTestResult != null)
                Reject(ctx, Config.Name, string.IsNullOrWhiteSpace(ctx.Result.Msg) ? "MTF 流程未成功完成。" : ctx.Result.Msg);
            return Task.FromResult(false);
        }

        private static void Reject(IProcessExecutionContext ctx, string? outputKey, string message)
        {
            ctx.Result.Result = false;
            ctx.Result.Msg = message;
            ctx.Result.ViewResultJson = string.Empty;
            ctx.ObjectiveTestResult.TotalResult = false;
            if (outputKey != null)
                ctx.ObjectiveTestResult.DynamicTestResults?.Remove(outputKey);
        }

        public override IReadOnlyList<ObjectiveTestCsvRow> GetObjectiveCsvRows(ProjectARVRReuslt result) =>
            GetObjectiveCsvRows<MTFTestResult>(result, string.IsNullOrWhiteSpace(Config.Name) ? "MTF" : Config.Name.Trim());

        public override void Render(IProcessExecutionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return;
            MTFViewTestResult testResult = JsonConvert.DeserializeObject<MTFViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return;

            int id = 0;

            if (testResult.MTFDetailViewReslut?.MTFResult?.result != null && testResult.MTFDetailViewReslut.MTFResult.result.Count != 0)
            {
                foreach (var item in testResult.MTFDetailViewReslut.MTFResult.result)
                {
                    id++;
                    if (!ProcessExtensions.TryCreateMtfOverlay(item, id, Config.ShowConfig, out DVRectangleText Rectangle))
                        continue;

                    ctx.ImageView.AddVisual(Rectangle);
                }
            }
        }

        public override void GenText(IProcessExecutionContext ctx, System.Windows.Documents.Paragraph paragraph, System.Windows.Media.Brush foreground, double fontSize)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"{Config.Name} 画面结果");

            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) { AppendPlainText(paragraph, sb.ToString(), foreground, fontSize); return; }

            MTFTestResult testResult = JsonConvert.DeserializeObject<MTFTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) { AppendPlainText(paragraph, sb.ToString(), foreground, fontSize); return; }

            sb.AppendLine("Name,Value,Unit,LowLimit,UpLimit,Result");

            foreach (var item in testResult.Items)
            {
                sb.AppendLine($"{item.Name},{item.Value},{item.Unit},{item.LowLimit},{item.UpLimit},{item.TestResult}");
            }

            AppendPlainText(paragraph, sb.ToString(), foreground, fontSize); return;
        }
    }
}
