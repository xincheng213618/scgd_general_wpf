#pragma warning disable CS8601
using ColorVision.Database;
using ColorVision.Engine; // DAOs
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.Distortion2;
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using System.Windows.Media;

namespace ProjectARVRPro.Process.Distortion
{
    public class DistortionProcess : ProcessBase<DistortionProcessConfig>
    {
        public override bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Log;
            DistortionRecipeConfig recipeConfig = ctx.RecipeConfig.GetRequiredService<DistortionRecipeConfig>();
            DistortionViewTestResult testResult = new DistortionViewTestResult();

            try
            {
                var values = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                if (values.Count > 0)
                    ctx.Result.FileName = values[0].FileUrl;

                var masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                foreach (var master in masters)
                {
                    if (master.ImgFileType == ViewResultAlgType.Distortion && master.version == "2.0")
                    {
                        var details = DeatilCommonDao.Instance.GetAllByPid(master.Id);
                        if (details.Count == 1)
                        {
                            var distortion = new Distortion2View(details[0]);
                            var distortionResult = distortion.DistortionReslut;
                            if (distortionResult == null)
                                continue;

                            ApplySelectedPoints(testResult, distortionResult, Config.PointSource);

                            if (distortionResult.OpticDistortion != null)
                            {
                                distortionResult.OpticDistortion.OpticRatio = recipeConfig.OpticDistortion.Apply(distortionResult.OpticDistortion.OpticRatio);

                                testResult.OpticDistortion = Build(
                                    "Optic_Distortion",
                                    distortionResult.OpticDistortion.OpticRatio,
                                    recipeConfig.OpticDistortion.Min,
                                    recipeConfig.OpticDistortion.Max);
                            }

                            if (distortionResult.TVDistortion != null)
                            {
                                distortionResult.TVDistortion.HorizontalRatio = recipeConfig.HorizontalTVDistortion.Apply(distortionResult.TVDistortion.HorizontalRatio);
                                distortionResult.TVDistortion.VerticalRatio = recipeConfig.VerticalTVDistortion.Apply(distortionResult.TVDistortion.VerticalRatio);

                                testResult.HorizontalTVDistortion = Build(
                                    "HorizontalTVDistortion",
                                    distortionResult.TVDistortion.HorizontalRatio,
                                    recipeConfig.HorizontalTVDistortion.Min,
                                    recipeConfig.HorizontalTVDistortion.Max);
                                testResult.VerticalTVDistortion = Build(
                                    "VerticalTVDistortion",
                                    distortionResult.TVDistortion.VerticalRatio,
                                    recipeConfig.VerticalTVDistortion.Min,
                                    recipeConfig.VerticalTVDistortion.Max);
                            }

                            if (distortionResult.Point9Distortion != null)
                            {
                                distortionResult.Point9Distortion.TopRatio = recipeConfig.DistortionTop.Apply(distortionResult.Point9Distortion.TopRatio);
                                distortionResult.Point9Distortion.BottomRatio = recipeConfig.DistortionBottom.Apply(distortionResult.Point9Distortion.BottomRatio);
                                distortionResult.Point9Distortion.LeftRatio = recipeConfig.DistortionLeft.Apply(distortionResult.Point9Distortion.LeftRatio);
                                distortionResult.Point9Distortion.RightRatio = recipeConfig.DistortionRight.Apply(distortionResult.Point9Distortion.RightRatio);
                                distortionResult.Point9Distortion.KeyStoneHoriRatio = recipeConfig.KeystoneHoriz.Apply(distortionResult.Point9Distortion.KeyStoneHoriRatio);
                                distortionResult.Point9Distortion.KeyStoneVercRatio = recipeConfig.KeystoneVert.Apply(distortionResult.Point9Distortion.KeyStoneVercRatio);

                                testResult.DistortionTop = Build(
                                    "DistortionTop",
                                    distortionResult.Point9Distortion.TopRatio,
                                    recipeConfig.DistortionTop.Min,
                                    recipeConfig.DistortionTop.Max);
                                testResult.DistortionBottom = Build(
                                    "DistortionBottom",
                                    distortionResult.Point9Distortion.BottomRatio,
                                    recipeConfig.DistortionBottom.Min,
                                    recipeConfig.DistortionBottom.Max);
                                testResult.DistortionLeft = Build(
                                    "DistortionLeft",
                                    distortionResult.Point9Distortion.LeftRatio,
                                    recipeConfig.DistortionLeft.Min,
                                    recipeConfig.DistortionLeft.Max);
                                testResult.DistortionRight = Build(
                                    "DistortionRight",
                                    distortionResult.Point9Distortion.RightRatio,
                                    recipeConfig.DistortionRight.Min,
                                    recipeConfig.DistortionRight.Max);
                                testResult.KeystoneHoriz = Build(
                                    "KeystoneHoriz",
                                    distortionResult.Point9Distortion.KeyStoneHoriRatio,
                                    recipeConfig.KeystoneHoriz.Min,
                                    recipeConfig.KeystoneHoriz.Max);
                                testResult.KeystoneVert = Build(
                                    "KeystoneVert",
                                    distortionResult.Point9Distortion.KeyStoneVercRatio,
                                    recipeConfig.KeystoneVert.Min,
                                    recipeConfig.KeystoneVert.Max);
                            }

                            UpdateResult(
                                ctx,
                                testResult.HorizontalTVDistortion,
                                testResult.VerticalTVDistortion,
                                testResult.OpticDistortion,
                                testResult.DistortionTop,
                                testResult.DistortionBottom,
                                testResult.DistortionLeft,
                                testResult.DistortionRight,
                                testResult.KeystoneHoriz,
                                testResult.KeystoneVert);
                        }
                    }
                }

                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);
                ctx.ObjectiveTestResult.DistortionTestResult = JsonConvert.DeserializeObject<DistortionTestResult>(ctx.Result.ViewResultJson) ?? new DistortionTestResult();
                return true;
            }
            catch (Exception ex)
            {
                log?.Error(ex);
                return false;
            }
        }

        public override void Render(IProcessExecutionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return;
            DistortionViewTestResult testResult = JsonConvert.DeserializeObject<DistortionViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return;

            foreach (var points in testResult.Points)
            {
                DVCircleText Circle = new();
                Circle.Attribute.Center = new System.Windows.Point(points.X, points.Y);
                Circle.Attribute.Radius = 200;
                Circle.Attribute.Brush = Brushes.Transparent;
                Circle.Attribute.Pen = new Pen(Brushes.Red, 1 );
                Circle.Attribute.Text = $"{Environment.NewLine} X:{points.X.ToString("F0")}{Environment.NewLine}Y:{points.Y.ToString("F0")}";
                Circle.Render();
                ctx.ImageView.AddVisual(Circle);
            }

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
            AppendPlainText(paragraph, outtext, foreground, fontSize); return;
        }

        public override IRecipeConfig GetRecipeConfig()
        {
            return RecipeManager.GetInstance().RecipeConfig.GetRequiredService<DistortionRecipeConfig>();
        }

        private static ObjectiveTestItem Build(string name, double value, double low, double up) => new ObjectiveTestItem
        {
            Name = name,
            LowLimit = low,
            UpLimit = up,
            Value = value,
            TestValue = value.ToString("F5") + "%"
        };

        private static void ApplySelectedPoints(DistortionViewTestResult testResult, DistortionReslut distortionResult, DistortionPointSource pointSource)
        {
            testResult.Points.Clear();
            var points = DistortionPointSourceHelper.GetPoints(distortionResult, pointSource);
            if (points == null)
                return;

            foreach (var pt in points)
            {
                testResult.Points.Add(new System.Windows.Point(pt.X, pt.Y));
            }
        }

        private static void UpdateResult(IProcessExecutionContext ctx, params ObjectiveTestItem[] items)
        {
            foreach (var item in items)
            {
                if (item != null)
                    ctx.Result.Result &= item.TestResult;
            }
        }

        private static void AppendItemText(ref string outtext, ObjectiveTestItem item)
        {
            if (item == null)
                return;

            outtext += $"{item.Name}:{item.TestValue} LowLimit:{item.LowLimit}  UpLimit:{item.UpLimit},Rsult{(item.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
        }
    }
}
