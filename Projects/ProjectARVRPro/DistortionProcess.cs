using System;
using ColorVision.Engine; // DAOs
using ColorVision.Engine.Templates.Jsons.Distortion2;
using ColorVision.Database;
using ColorVision.Engine.Templates.Jsons;

namespace ProjectARVRPro
{
    public class DistortionProcess : IProcess
    {
        public bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Logger;
            try
            {
                log?.Info("处理 Distortion 流程结果");
                ctx.ObjectiveTestResult.FlowDistortionTestReslut = true;

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
                                LowLimit = ctx.RecipeConfig.HorizontalTVDistortionMin,
                                UpLimit = ctx.RecipeConfig.HorizontalTVDistortionMax,
                                Value = distortion.DistortionReslut.TVDistortion.HorizontalRatio,
                                TestValue = distortion.DistortionReslut.TVDistortion.HorizontalRatio.ToString("F5") + "%"
                            };
                            ctx.ObjectiveTestResult.VerticalTVDistortion = new ObjectiveTestItem
                            {
                                Name = "VerticalTVDistortion",
                                LowLimit = ctx.RecipeConfig.VerticalTVDistortionMin,
                                UpLimit = ctx.RecipeConfig.VerticalTVDistortionMax,
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
    }
}
