#pragma warning disable CS8601
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using System.IO;
using System.Windows.Media;

namespace ProjectARVRPro.Process.Distortion
{
    public class DistortionProcess : ProcessWithRecipeBase<DistortionProcessConfig, DistortionRecipeConfig>
    {
        internal DistortionBatchResults BatchResults { get; init; } = new();

        public override Task<bool> Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Result == null || ctx.ObjectiveTestResult == null)
                return Task.FromResult(false);

            try
            {
                if (ctx.Batch == null)
                    throw new InvalidDataException("畸变缺少测量批次。");

                var values = ctx.GetMeasureResults();
                if (values.Count > 0)
                    ctx.Result.FileName = values[0].FileUrl;

                var testResult = new DistortionViewTestResult();
                bool passed = BatchResults.Apply(ctx, testResult, Config.PointSource, Config.RecipeConfig, "F5", null, "%");
                string snapshot = JsonConvert.SerializeObject(testResult);
                ctx.ObjectiveTestResult.DistortionTestResult = JsonConvert.DeserializeObject<DistortionTestResult>(snapshot)
                    ?? throw new JsonSerializationException("无法生成畸变测量结果。");
                ctx.Result.ViewResultJson = snapshot;
                ctx.Result.Result &= passed;
                return Task.FromResult(true);
            }
            catch (Exception error)
            {
                string message = $"畸变解析失败：{error.Message}";
                ctx.Log.Error(message, error);
                Reject(ctx, message);
                return Task.FromResult(false);
            }
        }

        public override Task<bool> ExecuteFailure(IProcessExecutionContext ctx)
        {
            if (ctx?.Result != null && ctx.ObjectiveTestResult != null)
                Reject(ctx, string.IsNullOrWhiteSpace(ctx.Result.Msg) ? "畸变流程未成功完成。" : ctx.Result.Msg);
            return Task.FromResult(false);
        }

        private static void Reject(IProcessExecutionContext ctx, string message)
        {
            DistortionBatchResults.Reject(ctx, message);
            ctx.ObjectiveTestResult.DistortionTestResult = null!;
        }

        public override IReadOnlyList<ObjectiveTestCsvRow> GetObjectiveCsvRows(ProjectARVRReuslt result) =>
            GetObjectiveCsvRows<DistortionTestResult>(result, "Distortion");

        public override void Render(IProcessExecutionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return;
            DistortionViewTestResult testResult = JsonConvert.DeserializeObject<DistortionViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return;

            foreach (var points in testResult.Points)
            {
                if (!ProcessExtensions.TryCreateOverlayPoint(points.X, points.Y, out System.Windows.Point center))
                    continue;

                CircleTextProperties properties = new()
                {
                    Center = center,
                    Radius = 200,
                    Brush = Brushes.Transparent,
                    Pen = new Pen(Brushes.Red, 1),
                    Text = $"{Environment.NewLine} X:{points.X.ToString("F0")}{Environment.NewLine}Y:{points.Y.ToString("F0")}",
                };
                DVCircleText Circle = new(properties);
                Circle.TextAttribute.FontSize = 20;
                ctx.ImageView.AddVisual(Circle);
            }
            DistortionResultOverlay.AppendGrid(ctx.ImageView, testResult.Points, Config.DrawGridOverlay);
        }

        public override void GenText(IProcessExecutionContext ctx, System.Windows.Documents.Paragraph paragraph, System.Windows.Media.Brush foreground, double fontSize)
        {
            var result = ctx.Result;
            string outtext = string.Empty;
            outtext += $"Distortion" + Environment.NewLine;

            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) { AppendPlainText(paragraph, outtext, foreground, fontSize); return; }
            DistortionViewTestResult testResult = JsonConvert.DeserializeObject<DistortionViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) { AppendPlainText(paragraph, outtext, foreground, fontSize); return; }

            AppendItemText(ref outtext, testResult.HorizontalTVDistortion);
            AppendItemText(ref outtext, testResult.VerticalTVDistortion);
            AppendItemText(ref outtext, testResult.OpticDistortion);
            AppendItemText(ref outtext, testResult.DistortionTop);
            AppendItemText(ref outtext, testResult.DistortionBottom);
            AppendItemText(ref outtext, testResult.DistortionLeft);
            AppendItemText(ref outtext, testResult.DistortionRight);
            AppendItemText(ref outtext, testResult.KeystoneHoriz);
            AppendItemText(ref outtext, testResult.KeystoneVert);
            AppendItemText(ref outtext, testResult.MaximumTiltDegrees);
            AppendItemText(ref outtext, testResult.MaximumEdgeLengthDifferencePercent);
            AppendPlainText(paragraph, outtext, foreground, fontSize); return;
        }


        private static void AppendItemText(ref string outtext, ObjectiveTestItem? item)
        {
            if (item == null)
                return;

            outtext += $"{item.Name}:{item.TestValue} LowLimit:{item.LowLimit}  UpLimit:{item.UpLimit},Rsult{(item.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
        }
    }
}
