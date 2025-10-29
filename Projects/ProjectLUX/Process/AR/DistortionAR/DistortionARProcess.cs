using ColorVision.Database;
using ColorVision.Engine; // DAOs
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.Distortion2;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using ProjectLUX.Process.Chessboard;
using ProjectLUX.Process.Green;
using System.Windows.Media;

namespace ProjectLUX.Process.DistortionAR
{
    public class DistortionARProcess : IProcess
    {
        public bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Logger;
            DistortionRecipeConfig recipeConfig = ctx.RecipeConfig.GetRequiredService<DistortionRecipeConfig>();
            DistortionARFixConfig fixConfig = ctx.FixConfig.GetRequiredService<DistortionARFixConfig>();
            DistortionARViewTestResult testResult = new DistortionARViewTestResult();

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
                            distortion.DistortionReslut.TVDistortion.HorizontalRatio *= fixConfig.HorizontalTVDistortion;
                            distortion.DistortionReslut.TVDistortion.VerticalRatio *= fixConfig.VerticalTVDistortion;


                            foreach (var pt in distortion.DistortionReslut.TVDistortion.FinalPoints)
                            {
                                testResult.Points.Add(new System.Windows.Point(pt.X, pt.Y));
                            }

                            testResult.HorizontalTVDistortion = new ObjectiveTestItem
                            {
                                Name = "HorizontalTVDistortion",
                                LowLimit = recipeConfig.HorizontalTVDistortion.Min,
                                UpLimit = recipeConfig.HorizontalTVDistortion.Max,
                                Value = distortion.DistortionReslut.TVDistortion.HorizontalRatio,
                                TestValue = distortion.DistortionReslut.TVDistortion.HorizontalRatio.ToString("F5") + "%"
                            };
                            testResult.VerticalTVDistortion = new ObjectiveTestItem
                            {
                                Name = "VerticalTVDistortion",
                                LowLimit = recipeConfig.VerticalTVDistortion.Min,
                                UpLimit = recipeConfig.VerticalTVDistortion.Max,
                                Value = distortion.DistortionReslut.TVDistortion.VerticalRatio,
                                TestValue = distortion.DistortionReslut.TVDistortion.VerticalRatio.ToString("F5") + "%"
                            };

                            ctx.Result.Result &= testResult.HorizontalTVDistortion.TestResult;
                            ctx.Result.Result &= testResult.VerticalTVDistortion.TestResult;
                        }
                    }
                }

                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);
                ctx.ObjectiveTestResult.DistortionARTestResult = JsonConvert.DeserializeObject<DistortionARTestResult>(ctx.Result.ViewResultJson) ?? new DistortionARTestResult();
                return true;
            }
            catch (Exception ex)
            {
                log?.Error(ex);
                return false;
            }
        }

        public void Render(IProcessExecutionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return;
            DistortionARViewTestResult testResult = JsonConvert.DeserializeObject<DistortionARViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return;

            foreach (var points in testResult.Points)
            {
                DVCircleText Circle = new();
                Circle.Attribute.Center = new System.Windows.Point(points.X, points.Y);
                Circle.Attribute.Radius = 20 / ctx.ImageView.Zoombox1.ContentMatrix.M11;
                Circle.Attribute.Brush = Brushes.Transparent;
                Circle.Attribute.Pen = new Pen(Brushes.Red, 1 / ctx.ImageView.Zoombox1.ContentMatrix.M11);
                Circle.Attribute.Text = $"{Environment.NewLine} X:{points.X.ToString("F0")}{Environment.NewLine}Y:{points.Y.ToString("F0")}";
                Circle.Render();
                ctx.ImageView.AddVisual(Circle);
            }

        }

        public string GenText(IProcessExecutionContext ctx)
        {
            var result = ctx.Result;
            string outtext = string.Empty;
            outtext += $"Distortion" + Environment.NewLine;

            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return outtext;
            DistortionARViewTestResult testResult = JsonConvert.DeserializeObject<DistortionARViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return outtext;

            outtext += $"SMIA_TV_Distortion_Horizontal:{testResult.HorizontalTVDistortion.TestValue} LowLimit:{testResult.HorizontalTVDistortion.LowLimit}  UpLimit:{testResult.HorizontalTVDistortion.UpLimit},Rsult{(testResult.HorizontalTVDistortion.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            outtext += $"SMIA_TV_Distortion_Vertical:{testResult.VerticalTVDistortion.TestValue} LowLimit:{testResult.VerticalTVDistortion.LowLimit}  UpLimit:{testResult.VerticalTVDistortion.UpLimit},Rsult{(testResult.VerticalTVDistortion.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            return outtext;
        }
    }
}
