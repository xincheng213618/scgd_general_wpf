#pragma warning disable CS8601
using ColorVision.Common.Algorithms;
using ColorVision.Core;
using ColorVision.Database;
using ColorVision.Engine; // AlgResultMasterDao, MeasureImgResultDao, DeatilCommonDao
using ColorVision.Engine.Templates.FindLightArea;
using ColorVision.Engine.Templates.Jsons; // DetailCommonModel
using ColorVision.Engine.Templates.Jsons.FOV2;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate;
using Newtonsoft.Json;
using System.Windows;
using System.Windows.Media;

namespace ProjectARVRPro.Process.W51
{
    public class White51Process : ProcessWithRecipeBase<W51ProcessConfig, W51RecipeConfig>
    {
        public override async Task<bool> Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Log;
            W51RecipeConfig recipeConfig = Config.RecipeConfig;
            W51ViewTestResult testResult = new W51ViewTestResult();

            try
            {
                var values = ctx.GetMeasureResults();
                if (values.Count > 0)
                    ctx.Result.FileName = values[0].FileUrl;

                var masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                foreach (var master in masters)
                {
                    if (master.ImgFileType == ColorVision.Engine.ViewResultAlgType.FindLightArea)
                    {  
                        testResult.AlgResultLightAreaModels = AlgResultLightAreaDao.Instance.GetAllByPid(master.Id);
                    }


                    if (master.ImgFileType == ViewResultAlgType.FOV)
                    {
                        var algResultModels = DeatilCommonDao.Instance.GetAllByPid(master.Id);
                        if (algResultModels.Count == 1)
                        {
                            DFovView view1 = new DFovView(algResultModels[0]);


                            view1.Result.result.D_Fov = recipeConfig.DiagonalFieldOfViewAngle.Apply(view1.Result.result.D_Fov);
                            view1.Result.result.ClolorVisionH_Fov = recipeConfig.HorizontalFieldOfViewAngle.Apply(view1.Result.result.ClolorVisionH_Fov);
                            view1.Result.result.ClolorVisionV_Fov = recipeConfig.VerticalFieldOfViewAngle.Apply(view1.Result.result.ClolorVisionV_Fov);


                            testResult.DiagonalFieldOfViewAngle.LowLimit = recipeConfig.DiagonalFieldOfViewAngle.Min;
                            testResult.DiagonalFieldOfViewAngle.UpLimit = recipeConfig.DiagonalFieldOfViewAngle.Max;
                            testResult.DiagonalFieldOfViewAngle.Value = view1.Result.result.D_Fov;
                            testResult.DiagonalFieldOfViewAngle.TestValue = view1.Result.result.D_Fov.ToString("F4");

                            testResult.HorizontalFieldOfViewAngle.LowLimit = recipeConfig.HorizontalFieldOfViewAngle.Min;
                            testResult.HorizontalFieldOfViewAngle.UpLimit = recipeConfig.HorizontalFieldOfViewAngle.Max;
                            testResult.HorizontalFieldOfViewAngle.Value = view1.Result.result.ClolorVisionH_Fov;
                            testResult.HorizontalFieldOfViewAngle.TestValue = view1.Result.result.ClolorVisionH_Fov.ToString("F4");

                            testResult.VerticalFieldOfViewAngle.LowLimit = recipeConfig.VerticalFieldOfViewAngle.Min;
                            testResult.VerticalFieldOfViewAngle.UpLimit = recipeConfig.VerticalFieldOfViewAngle.Max;
                            testResult.VerticalFieldOfViewAngle.Value = view1.Result.result.ClolorVisionV_Fov;
                            testResult.VerticalFieldOfViewAngle.TestValue = view1.Result.result.ClolorVisionV_Fov.ToString("F4");

                            ctx.Result.Result = ctx.Result.Result && testResult.DiagonalFieldOfViewAngle.TestResult;
                            ctx.Result.Result = ctx.Result.Result && testResult.HorizontalFieldOfViewAngle.TestResult;
                            ctx.Result.Result = ctx.Result.Result && testResult.VerticalFieldOfViewAngle.TestResult;

                        }

                    }
                }
                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);
                ctx.ObjectiveTestResult.W51TestResult = JsonConvert.DeserializeObject<W51TestResult>(ctx.Result.ViewResultJson) ?? new W51TestResult();

                return true;
            }
            catch (Exception ex)
            {
                log?.Error(ex);
                return false;
            }
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
