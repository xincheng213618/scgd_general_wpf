using ColorVision.Common.Algorithms;
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace ProjectARVRPro.Process.KeyedResults.FieldOfView
{
    public class FieldOfViewProcess : ProcessBase<FieldOfViewProcessConfig>
    {
        internal FovBatchResults BatchResults { get; init; } = new();

        public override IRecipeConfig GetRecipeConfig() => Config.RecipeConfig;

        public override Task<bool> Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Result == null || ctx.ObjectiveTestResult == null)
                return Task.FromResult(false);

            string outputKey = Config.GetOutputKey();
            try
            {
                if (ctx.Batch == null)
                    throw new InvalidDataException("视场角缺少测量批次。");
                var images = ctx.GetMeasureResults();
                string? fileUrl = images.FirstOrDefault()?.FileUrl;
                if (!string.IsNullOrWhiteSpace(fileUrl))
                    ctx.Result.FileName = fileUrl;

                var (angles, corners) = BatchResults.Load(ctx);
                var testResult = new FieldOfViewViewTestResult { AlgResultLightAreaModels = corners };
                var recipe = Config.RecipeConfig;
                bool allPassed = true;
                foreach (var measurement in angles)
                {
                    allPassed &= FovBatchResults.ApplyRecipe(testResult.DiagonalFieldOfViewAngle, measurement.Diagonal, recipe.DiagonalFieldOfViewAngle);
                    allPassed &= FovBatchResults.ApplyRecipe(testResult.HorizontalFieldOfViewAngle, measurement.Horizontal, recipe.HorizontalFieldOfViewAngle);
                    allPassed &= FovBatchResults.ApplyRecipe(testResult.VerticalFieldOfViewAngle, measurement.Vertical, recipe.VerticalFieldOfViewAngle);
                }

                string snapshot = JsonConvert.SerializeObject(testResult);
                var objectiveResult = JsonConvert.DeserializeObject<FieldOfViewTestResult>(snapshot)
                    ?? throw new JsonSerializationException("无法生成视场角测量结果。");
                KeyedTestResultWriter.Write(ctx.ObjectiveTestResult, outputKey, objectiveResult);
                ctx.Result.ViewResultJson = snapshot;
                ctx.Result.Result &= allPassed;
                return Task.FromResult(true);
            }
            catch (Exception error)
            {
                string message = $"视场角解析失败 [{outputKey}]：{error.Message}";
                ctx.Log.Error(message, error);
                Reject(ctx, outputKey, message);
                return Task.FromResult(false);
            }
        }

        public override Task<bool> ExecuteFailure(IProcessExecutionContext ctx)
        {
            if (ctx?.Result != null && ctx.ObjectiveTestResult != null)
                Reject(ctx, Config.GetOutputKey(), string.IsNullOrWhiteSpace(ctx.Result.Msg) ? "视场角流程未成功完成。" : ctx.Result.Msg);
            return Task.FromResult(false);
        }

        private static void Reject(IProcessExecutionContext ctx, string outputKey, string message)
        {
            FovBatchResults.Reject(ctx, message);
            var objective = ctx.ObjectiveTestResult;
            var results = objective.FieldOfViewTestResults;
            if (results == null) return;

            foreach (string key in results.Keys.Where(key => KeyedTestResultDictionary.IsKey(key, outputKey)).ToArray())
            {
                if (KeyedTestResultDictionary.IsKey(outputKey, "White") && FieldOfViewCompatibility.IsProjectionOf(objective.W51TestResult, results[key]))
                    objective.W51TestResult = null!;
                results.Remove(key);
            }
        }

        public override IReadOnlyList<ObjectiveTestCsvRow> GetObjectiveCsvRows(ProjectARVRReuslt result) =>
            GetObjectiveCsvRows<FieldOfViewTestResult>(result, Config.GetOutputKey());

        public override void Render(IProcessExecutionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson))
                return;

            var testResult = JsonConvert.DeserializeObject<FieldOfViewViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null || testResult.AlgResultLightAreaModels.Count == 0)
                return;

            var points = testResult.AlgResultLightAreaModels
                .Select(item => new Point((int)item.PosX, (int)item.PosY))
                .ToList();
            var polygon = new DVPolygon();
            foreach (Point point in GrahamScan.ComputeConvexHull(points))
                polygon.Attribute.Points.Add(point);

            polygon.Attribute.Brush = Brushes.Transparent;
            polygon.Attribute.Pen = new Pen(Brushes.Blue, 1);
            polygon.Attribute.Id = -1;
            polygon.IsComple = true;
            polygon.Render();
            ctx.ImageView.AddVisual(polygon);
        }

        public override void GenText(IProcessExecutionContext ctx, Paragraph paragraph, Brush foreground, double fontSize)
        {
            var output = new StringBuilder().AppendLine($"视场角测试 ({Config.GetOutputKey()})");
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson))
            {
                AppendPlainText(paragraph, output.ToString(), foreground, fontSize);
                return;
            }

            var testResult = JsonConvert.DeserializeObject<FieldOfViewViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult != null)
            {
                AppendResult(output, testResult.HorizontalFieldOfViewAngle);
                AppendResult(output, testResult.VerticalFieldOfViewAngle);
                AppendResult(output, testResult.DiagonalFieldOfViewAngle);
            }

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
