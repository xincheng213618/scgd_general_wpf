#pragma warning disable CS8601
using ColorVision.Common.Algorithms;
using ColorVision.Core;
using ColorVision.Engine.Services.Results.LightArea;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate;
using Newtonsoft.Json;
using ProjectARVRPro.Process.KeyedResults;
using ProjectARVRPro.Process.KeyedResults.FieldOfView;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace ProjectARVRPro.Process.W51
{
    public class White51Process : ProcessWithRecipeBase<W51ProcessConfig, W51RecipeConfig>
    {
        internal FovBatchResults BatchResults { get; init; } = new();

        public override Task<bool> Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Result == null || ctx.ObjectiveTestResult == null)
                return Task.FromResult(false);

            try
            {
                if (ctx.Batch == null)
                    throw new InvalidDataException("W51 缺少测量批次。");
                var images = ctx.GetMeasureResults();
                if (images.Count > 0)
                    ctx.Result.FileName = images[0].FileUrl;

                var (angles, corners) = BatchResults.Load(ctx);
                var testResult = new W51ViewTestResult { AlgResultLightAreaModels = corners };
                var recipe = Config.RecipeConfig;
                bool allPassed = true;
                foreach (var measurement in angles)
                {
                    allPassed &= FovBatchResults.ApplyRecipe(testResult.DiagonalFieldOfViewAngle, measurement.Diagonal, recipe.DiagonalFieldOfViewAngle);
                    allPassed &= FovBatchResults.ApplyRecipe(testResult.HorizontalFieldOfViewAngle, measurement.Horizontal, recipe.HorizontalFieldOfViewAngle);
                    allPassed &= FovBatchResults.ApplyRecipe(testResult.VerticalFieldOfViewAngle, measurement.Vertical, recipe.VerticalFieldOfViewAngle);
                }

                string snapshot = JsonConvert.SerializeObject(testResult);
                ctx.ObjectiveTestResult.W51TestResult = JsonConvert.DeserializeObject<W51TestResult>(snapshot)
                    ?? throw new JsonSerializationException("无法生成 W51 测量结果。");
                ctx.Result.ViewResultJson = snapshot;
                ctx.Result.Result &= allPassed;
                return Task.FromResult(true);
            }
            catch (Exception error)
            {
                string message = $"W51 视场角解析失败：{error.Message}";
                ctx.Log.Error(message, error);
                Reject(ctx, message);
                return Task.FromResult(false);
            }
        }

        public override Task<bool> ExecuteFailure(IProcessExecutionContext ctx)
        {
            if (ctx?.Result != null && ctx.ObjectiveTestResult != null)
                Reject(ctx, string.IsNullOrWhiteSpace(ctx.Result.Msg) ? "W51 流程未成功完成。" : ctx.Result.Msg);
            return Task.FromResult(false);
        }

        private static void Reject(IProcessExecutionContext ctx, string message)
        {
            FovBatchResults.Reject(ctx, message);
            var objective = ctx.ObjectiveTestResult;
            objective.W51TestResult = KeyedTestResultDictionary.TryGetValue(objective.FieldOfViewTestResults, "White", out var white) && white != null
                ? FieldOfViewCompatibility.ToW51TestResult(white) : null!;
        }

        public override IReadOnlyList<ObjectiveTestCsvRow> GetObjectiveCsvRows(ProjectARVRReuslt result) =>
            GetObjectiveCsvRows<W51TestResult>(result, "W51");

        public override void Render (IProcessExecutionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return;
            W51ViewTestResult testResult = JsonConvert.DeserializeObject<W51ViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return;

            if (Config.DrawFovOverlay && TryCreateFovMeasurement(testResult, out FovMeasurement measurement))
            {
                FovOverlayRenderer.Render(
                    ctx.ImageView.EditorContext.ProcessingContext,
                    ctx.ImageView.EditorContext.DrawEditorContext,
                    measurement);
                return;
            }

            AlgorithmResultOverlay.ClearTagged(
                ctx.ImageView.EditorContext.DrawEditorContext,
                AlgorithmResultOverlay.FovTag);
            RenderLuminousArea(ctx, testResult.AlgResultLightAreaModels);
        }

        internal static bool TryCreateFovMeasurement(W51ViewTestResult testResult, out FovMeasurement measurement)
        {
            measurement = null!;
            if (testResult.AlgResultLightAreaModels == null || testResult.AlgResultLightAreaModels.Count != 4
                || testResult.HorizontalFieldOfViewAngle == null
                || testResult.VerticalFieldOfViewAngle == null
                || testResult.DiagonalFieldOfViewAngle == null)
                return false;

            LuminousAreaPoint[] points = testResult.AlgResultLightAreaModels
                .Select(item => new LuminousAreaPoint(item.PosX, item.PosY))
                .ToArray();
            double centerX = points.Average(point => point.X);
            double centerY = points.Average(point => point.Y);
            LuminousAreaPoint[] aroundCenter = points
                .OrderBy(point => Math.Atan2(point.Y - centerY, point.X - centerX))
                .ToArray();
            int leftTopIndex = Enumerable.Range(0, aroundCenter.Length)
                .OrderBy(index => aroundCenter[index].X + aroundCenter[index].Y)
                .ThenBy(index => aroundCenter[index].Y)
                .First();
            LuminousAreaPoint[] corners = aroundCenter
                .Skip(leftTopIndex)
                .Concat(aroundCenter.Take(leftTopIndex))
                .ToArray();
            if (!LuminousAreaResultParser.TryValidateOrderedCorners(corners, out _))
                return false;

            double horizontal = testResult.HorizontalFieldOfViewAngle.Value;
            double vertical = testResult.VerticalFieldOfViewAngle.Value;
            double diagonal = testResult.DiagonalFieldOfViewAngle.Value;
            if (!double.IsFinite(horizontal) || !double.IsFinite(vertical) || !double.IsFinite(diagonal))
                return false;

            measurement = new FovMeasurement
            {
                Corners = corners,
                HorizontalFovDegrees = horizontal,
                VerticalFovDegrees = vertical,
                DiagonalFovDegrees = diagonal,
                DirectionalHorizontalFovDegrees = horizontal,
                DirectionalVerticalFovDegrees = vertical
            };
            return true;
        }

        private static void RenderLuminousArea(IProcessExecutionContext ctx, List<AlgResultLightAreaModel>? models)
        {
            if (models == null || models.Count == 0) return;

            DVPolygon polygon = new();
            List<Point> points = models.Select(item => new Point((int)item.PosX, (int)item.PosY)).ToList();
            foreach (Point point in GrahamScan.ComputeConvexHull(points))
                polygon.Attribute.Points.Add(point);
            polygon.Attribute.Brush = Brushes.Transparent;
            polygon.Attribute.Pen = new Pen(Brushes.Blue, 1);
            polygon.Attribute.Id = -1;
            polygon.IsComple = true;
            polygon.Render();
            ctx.ImageView.AddVisual(polygon);
        }

        public override void GenText(IProcessExecutionContext ctx, System.Windows.Documents.Paragraph paragraph, System.Windows.Media.Brush foreground, double fontSize)
        {
            var result = ctx.Result;
            string outtext = string.Empty;
            outtext += $"W51 画面结果" + Environment.NewLine;

            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) { AppendPlainText(paragraph, outtext, foreground, fontSize); return; }
            W51ViewTestResult testResult = JsonConvert.DeserializeObject<W51ViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) { AppendPlainText(paragraph, outtext, foreground, fontSize); return; }




            outtext += $"HorizontalFieldOfViewAngle:{testResult.HorizontalFieldOfViewAngle.TestValue} LowLimit:{testResult.HorizontalFieldOfViewAngle.LowLimit} UpLimit:{testResult.HorizontalFieldOfViewAngle.UpLimit} ,Rsult{(testResult.HorizontalFieldOfViewAngle.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            outtext += $"VerticalFieldOfViewAngle:{testResult.VerticalFieldOfViewAngle.TestValue} LowLimit:{testResult.VerticalFieldOfViewAngle.LowLimit} UpLimit:{testResult.VerticalFieldOfViewAngle.UpLimit},Rsult{(testResult.VerticalFieldOfViewAngle.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            outtext += $"DiagonalFieldOfViewAngle:{testResult.DiagonalFieldOfViewAngle.TestValue}  LowLimit:{testResult.DiagonalFieldOfViewAngle.LowLimit} UpLimit:{testResult.DiagonalFieldOfViewAngle.UpLimit},Rsult{(testResult.DiagonalFieldOfViewAngle.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            AppendPlainText(paragraph, outtext, foreground, fontSize); return;
        }

    }
}
