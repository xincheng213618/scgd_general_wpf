using ColorVision.Database;
using ColorVision.Engine;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Documents;
using System.Windows.Media;

namespace ProjectARVRPro.Process.DemuraAOI
{
    public class DemuraAoiProcess : ProcessBase<DemuraAoiProcessConfig>
    {
        public override IRecipeConfig GetRecipeConfig()
        {
            return RecipeManager.GetInstance().RecipeConfig.GetRequiredService<DemuraAoiRecipeConfig>();
        }

        public override async Task<bool> Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null || ctx.ObjectiveTestResult == null) return false;

            var testResult = new DemuraAoiViewTestResult();
            try
            {
                SetPreviewFile(ctx);
                DemuraAoiParseResult parseResult = await DemuraAoiParser.ParseAsync(ctx.Batch.Id, Config).ConfigureAwait(false);
                DemuraAoiRecipeConfig recipe = ctx.RecipeConfig.GetRequiredService<DemuraAoiRecipeConfig>();
                DemuraAoiEvaluationResult evaluation = DemuraAoiEvaluator.Evaluate(parseResult, recipe);

                PopulateTestResult(testResult, parseResult, evaluation);
                SaveStructuredResult(ctx, testResult);

                if (Config.EnforceResult && !evaluation.IsPass)
                {
                    ctx.Result.Result = false;
                    ctx.Result.Msg = evaluation.Message;
                    ctx.ObjectiveTestResult.TotalResult = false;
                    if (string.IsNullOrWhiteSpace(ctx.ObjectiveTestResult.Msg))
                        ctx.ObjectiveTestResult.Msg = evaluation.Message;
                }
                else if (evaluation.IsPass)
                {
                    ctx.Result.Msg = evaluation.Message;
                }

                foreach (string warning in parseResult.Warnings)
                    ctx.Log?.Warn(warning);
                if (!evaluation.IsPass)
                    ctx.Log?.Warn(evaluation.Message);

                return true;
            }
            catch (Exception ex)
            {
                ctx.Log?.Error("Demura AOI解析异常", ex);
                testResult.Outcome = DemuraAoiOutcome.DataError.ToString();
                testResult.Message = $"Demura AOI解析异常: {ex.Message}";
                testResult.DataErrors.Add(testResult.Message);
                testResult.Items.Add(CreateFailureItem("DemuraAOIDataError", testResult.Message));
                ctx.Result.Result = false;
                ctx.Result.Msg = testResult.Message;
                ctx.ObjectiveTestResult.TotalResult = false;
                if (string.IsNullOrWhiteSpace(ctx.ObjectiveTestResult.Msg))
                    ctx.ObjectiveTestResult.Msg = testResult.Message;
                SaveStructuredResult(ctx, testResult);
                return true;
            }
        }

        public override Task<bool> ExecuteFailure(IProcessExecutionContext ctx)
        {
            if (ctx?.Result == null || ctx.ObjectiveTestResult == null) return Task.FromResult(false);

            string message = string.IsNullOrWhiteSpace(ctx.Result.Msg) ? "Demura AOI流程失败" : ctx.Result.Msg;
            var testResult = new DemuraAoiViewTestResult
            {
                Outcome = DemuraAoiOutcome.DataError.ToString(),
                Message = message,
                DataErrors = new List<string> { message }
            };
            testResult.Items.Add(CreateFailureItem("DemuraAOIFailure", message));
            SetPreviewFile(ctx);
            ctx.Result.Result = false;
            ctx.Result.Msg = message;
            ctx.ObjectiveTestResult.TotalResult = false;
            ctx.ObjectiveTestResult.Msg = message;
            SaveStructuredResult(ctx, testResult);
            return Task.FromResult(true);
        }

        public override void Render(IProcessExecutionContext ctx)
        {
        }

        public override void GenText(IProcessExecutionContext ctx, Paragraph paragraph, Brush foreground, double fontSize)
        {
            var output = new StringBuilder();
            output.AppendLine($"{Config.Name} 画面结果");
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson))
            {
                AppendPlainText(paragraph, output.ToString(), foreground, fontSize);
                return;
            }

            DemuraAoiTestResult? result = JsonConvert.DeserializeObject<DemuraAoiTestResult>(ctx.Result.ViewResultJson);
            if (result == null)
            {
                AppendPlainText(paragraph, output.ToString(), foreground, fontSize);
                return;
            }

            output.AppendLine($"Outcome:{result.Outcome}");
            output.AppendLine($"Message:{result.Message}");
            if (result.W255 != null)
            {
                output.AppendLine($"W255:{result.W255.FilePath}");
                output.AppendLine($"W255Uniformity:{result.W255.Uniformity:P4}, Min:{result.W255.Minimum:F4}, Max:{result.W255.Maximum:F4}, Radius:{result.W255.Radius}");
            }
            if (result.Grading != null)
                output.AppendLine($"AOIGrade:{result.Grading.GradeLevel}, MaxDefectDensity:{result.Grading.MaxDefectDensity}, DarkTotalDefects:{result.Grading.DarkTotalDefects}, BrightTotalDefects:{result.Grading.BrightTotalDefects}");
            if (result.Black != null)
                output.AppendLine($"BlackGrade:{result.Black.GradeLevel}, BrightCount:{result.Black.BrightCount}");
            foreach (string warning in result.Warnings)
                output.AppendLine($"Warning:{warning}");

            output.AppendLine("Name,Value,Unit,LowLimit,UpLimit,Result");
            foreach (ObjectiveTestItem item in result.Items)
                output.AppendLine($"{item.Name},{item.Value},{item.Unit},{item.LowLimit},{item.UpLimit},{(item.TestResult ? "PASS" : "Fail")}");
            AppendPlainText(paragraph, output.ToString(), foreground, fontSize);
        }

        private static void PopulateTestResult(DemuraAoiViewTestResult target, DemuraAoiParseResult parseResult, DemuraAoiEvaluationResult evaluation)
        {
            target.Outcome = evaluation.Outcome.ToString();
            target.Message = evaluation.Message;
            target.W255 = parseResult.W255;
            target.Grading = parseResult.Grading;
            target.Black = parseResult.Black;
            target.DataErrors = parseResult.DataErrors.ToList();
            target.Warnings = parseResult.Warnings.ToList();
            target.SpecificationFailures = evaluation.SpecificationFailures.ToList();
            target.Items = new ObservableCollection<ObjectiveTestItem>(evaluation.Items);
        }

        private void SaveStructuredResult(IProcessExecutionContext ctx, DemuraAoiViewTestResult testResult)
        {
            ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);
            if (!ctx.ObjectiveTestResult.DynamicTestResults.TryGetValue(Config.Name, out ObservableCollection<ObjectiveTestItem>? items))
            {
                items = new ObservableCollection<ObjectiveTestItem>();
                ctx.ObjectiveTestResult.DynamicTestResults[Config.Name] = items;
            }

            items.Clear();
            foreach (ObjectiveTestItem item in testResult.Items)
                items.Add(item);
        }

        private static ObjectiveTestItem CreateFailureItem(string name, string message)
        {
            return new ObjectiveTestItem
            {
                Name = name,
                Value = 0,
                TestValue = message,
                LowLimit = 1,
                UpLimit = 1,
                Unit = string.Empty
            };
        }

        private static void SetPreviewFile(IProcessExecutionContext ctx)
        {
            if (!string.IsNullOrWhiteSpace(ctx.Result.FileName)) return;
            string? fileName = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id).FirstOrDefault()?.FileUrl;
            if (!string.IsNullOrWhiteSpace(fileName)) ctx.Result.FileName = fileName;
        }
    }
}
