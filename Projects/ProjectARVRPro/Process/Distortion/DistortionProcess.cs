using ColorVision.Database;
using ColorVision.Engine; // DAOs
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.Distortion2;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using System.Windows.Media;

namespace ProjectARVRPro.Process.Distortion
{
    public class DistortionProcess : IProcess
    {
        public bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Logger;
            DistortionRecipeConfig recipeConfig = ctx.RecipeConfig.GetRequiredService<DistortionRecipeConfig>();


            try
            {
                log?.Info("处理 Distortion 流程结果");

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
                            distortion.DistortionReslut.TVDistortion.HorizontalRatio *= ctx.ObjectiveTestResultFix.HorizontalTVDistortion;
                            distortion.DistortionReslut.TVDistortion.VerticalRatio *= ctx.ObjectiveTestResultFix.VerticalTVDistortion;

                            ctx.Result.ViewReslutDistortionGhost.Distortion2View = distortion;

                            ctx.ObjectiveTestResult.HorizontalTVDistortion = new ObjectiveTestItem
                            {
                                Name = "HorizontalTVDistortion",
                                LowLimit = recipeConfig.HorizontalTVDistortionMin,
                                UpLimit = recipeConfig.HorizontalTVDistortionMax,
                                Value = distortion.DistortionReslut.TVDistortion.HorizontalRatio,
                                TestValue = distortion.DistortionReslut.TVDistortion.HorizontalRatio.ToString("F5") + "%"
                            };
                            ctx.ObjectiveTestResult.VerticalTVDistortion = new ObjectiveTestItem
                            {
                                Name = "VerticalTVDistortion",
                                LowLimit = recipeConfig.VerticalTVDistortionMin,
                                UpLimit = recipeConfig.VerticalTVDistortionMax,
                                Value = distortion.DistortionReslut.TVDistortion.VerticalRatio,
                                TestValue = distortion.DistortionReslut.TVDistortion.VerticalRatio.ToString("F5") + "%"
                            };
                            ctx.Result.ViewReslutDistortionGhost.HorizontalTVDistortion = ctx.ObjectiveTestResult.HorizontalTVDistortion;
                            ctx.Result.ViewReslutDistortionGhost.VerticalTVDistortion = ctx.ObjectiveTestResult.VerticalTVDistortion;

                            ctx.Result.Result &= ctx.ObjectiveTestResult.HorizontalTVDistortion.TestResult;
                            ctx.Result.Result &= ctx.ObjectiveTestResult.VerticalTVDistortion.TestResult;
                        }
                    }
                }
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
            if (ctx.Result.ViewReslutDistortionGhost.Distortion2View.DistortionReslut.TVDistortion != null)
            {
                if (ctx.Result.ViewReslutDistortionGhost.Distortion2View.DistortionReslut.TVDistortion.FinalPoints != null)
                {
                    foreach (var points in ctx.Result.ViewReslutDistortionGhost.Distortion2View.DistortionReslut.TVDistortion.FinalPoints)
                    {
                        DVCircleText Circle = new();
                        Circle.Attribute.Center = new System.Windows.Point(points.X, points.Y);
                        Circle.Attribute.Radius = 20 / ctx.ImageView.Zoombox1.ContentMatrix.M11;
                        Circle.Attribute.Brush = Brushes.Transparent;
                        Circle.Attribute.Pen = new Pen(Brushes.Red, 1 / ctx.ImageView.Zoombox1.ContentMatrix.M11);
                        Circle.Attribute.Text = $"id:{points.Id}{Environment.NewLine} X:{points.X.ToString("F0")}{Environment.NewLine}Y:{points.Y.ToString("F0")}";
                        Circle.Attribute.Id = points.Id;
                        Circle.Render();
                        ctx.ImageView.AddVisual(Circle);
                    }
                }
            }
        }

        public string GenText(IProcessExecutionContext ctx)
        {
            var result = ctx.Result;
            string outtext = string.Empty;
            outtext += $"畸变鬼影 测试项：自动AA区域定位算法+畸变算法+鬼影算法" + Environment.NewLine;

            foreach (var item in result.ViewReslutDistortionGhost.Distortion2View.DistortionReslut.TVDistortion.FinalPoints)
            {
                outtext += $"id:{item.Id} X:{item.X} Y:{item.Y}" + Environment.NewLine;
            }
            outtext += $"HorizontalTVDistortion:{result.ViewReslutDistortionGhost.HorizontalTVDistortion.TestValue} LowLimit:{result.ViewReslutDistortionGhost.HorizontalTVDistortion.LowLimit}  UpLimit:{result.ViewReslutDistortionGhost.HorizontalTVDistortion.UpLimit},Rsult{(result.ViewReslutDistortionGhost.HorizontalTVDistortion.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            outtext += $"VerticalTVDistortion:{result.ViewReslutDistortionGhost.VerticalTVDistortion.TestValue} LowLimit:{result.ViewReslutDistortionGhost.VerticalTVDistortion.LowLimit}  UpLimit:{result.ViewReslutDistortionGhost.VerticalTVDistortion.UpLimit},Rsult{(result.ViewReslutDistortionGhost.VerticalTVDistortion.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            return outtext;
        }
    }
}
