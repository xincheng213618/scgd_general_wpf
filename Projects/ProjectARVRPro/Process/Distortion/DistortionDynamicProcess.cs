using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using System.IO;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Media;

namespace ProjectARVRPro.Process.Distortion
{
    public class DistortionDynamicProcess : ProcessWithRecipeBase<DistortionDynamicProcessConfig, DistortionRecipeConfig>
    {
        internal DistortionBatchResults BatchResults { get; init; } = new();

        public override Task<bool> Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Result == null || ctx.ObjectiveTestResult == null)
                return Task.FromResult(false);

            string outputName = GetOutputName();
            try
            {
                if (ctx.Batch == null)
                    throw new InvalidDataException("畸变缺少测量批次。");

                var images = ctx.GetMeasureResults();
                string? fileUrl = images.FirstOrDefault()?.FileUrl;
                if (!string.IsNullOrWhiteSpace(fileUrl))
                    ctx.Result.FileName = fileUrl;

                var testResult = new DistortionDynamicViewTestResult();
                var view = testResult.DistortionViewTestResult;
                bool passed = BatchResults.Apply(ctx, view, Config.PointSource, Config.RecipeConfig, Config.ShowConfig, Config.Unit, string.Empty);
                testResult.Items = CollectItems(view);
                string snapshot = JsonConvert.SerializeObject(testResult);
                (ctx.ObjectiveTestResult.DynamicTestResults ??= new())[outputName] = testResult.Items;
                ctx.Result.ViewResultJson = snapshot;
                ctx.Result.Result &= passed;
                return Task.FromResult(true);
            }
            catch (Exception error)
            {
                string message = $"畸变解析失败 [{outputName}]：{error.Message}";
                ctx.Log.Error(message, error);
                Reject(ctx, outputName, message);
                return Task.FromResult(false);
            }
        }

        public override Task<bool> ExecuteFailure(IProcessExecutionContext ctx)
        {
            if (ctx?.Result != null && ctx.ObjectiveTestResult != null)
                Reject(ctx, GetOutputName(), string.IsNullOrWhiteSpace(ctx.Result.Msg) ? "畸变流程未成功完成。" : ctx.Result.Msg);
            return Task.FromResult(false);
        }

        private static void Reject(IProcessExecutionContext ctx, string outputName, string message)
        {
            DistortionBatchResults.Reject(ctx, message);
            ctx.ObjectiveTestResult.DynamicTestResults?.Remove(outputName);
        }

        public override IReadOnlyList<ObjectiveTestCsvRow> GetObjectiveCsvRows(ProjectARVRReuslt result) =>
            GetObjectiveCsvRows<DistortionDynamicTestResult>(result, string.IsNullOrWhiteSpace(Config.Name) ? "Distortion" : Config.Name.Trim());

        public override void Render(IProcessExecutionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return;
            DistortionDynamicViewTestResult? testResult = JsonConvert.DeserializeObject<DistortionDynamicViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return;

            foreach (var points in testResult.DistortionViewTestResult.Points)
            {
                if (!ProcessExtensions.TryCreateOverlayPoint(points.X, points.Y, out System.Windows.Point center))
                    continue;

                CircleTextProperties properties = new()
                {
                    Center = center,
                    Radius = 200,
                    Brush = Brushes.Transparent,
                    Pen = new Pen(Brushes.Red, 1),
                    Text = $"{Environment.NewLine} X:{points.X:F0}{Environment.NewLine}Y:{points.Y:F0}",
                };
                DVCircleText circle = new(properties);
                circle.TextAttribute.FontSize = 20;
                ctx.ImageView.AddVisual(circle);
            }
            DistortionResultOverlay.AppendGrid(ctx.ImageView, testResult.DistortionViewTestResult.Points, Config.DrawGridOverlay);
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


        internal static ObservableCollection<ObjectiveTestItem> CollectItems(DistortionTestResult result)
            => new(DistortionBatchResults.GetItems(result));

        private string GetOutputName()
        {
            return string.IsNullOrWhiteSpace(Config.Name) ? "Distortion" : Config.Name.Trim();
        }
    }
}
