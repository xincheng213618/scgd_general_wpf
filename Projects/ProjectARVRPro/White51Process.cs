using System;
using ColorVision.Engine; // AlgResultMasterDao, MeasureImgResultDao
using ColorVision.Engine.Templates.FindLightArea;
using ColorVision.Engine.Templates.Jsons; // DetailCommonModel
using ColorVision.Engine.Templates.Jsons.FOV2; // DFovView
using ColorVision.Database;

namespace ProjectARVRPro
{
    /// <summary>
    /// Extracted processing logic for White51 (W51) test type.
    /// </summary>
    public class White51Process : IProcess
    {
        public bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx == null || ctx.Batch == null || ctx.Result == null)
                return false;
            var log = ctx.Logger;

            try
            {
                log?.Info("处理 White51 流程结果");
                ctx.ObjectiveTestResult.FlowW51TestReslut = true;

                // 图像
                var values = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                if (values.Count > 0)
                {
                    ctx.Result.FileName = values[0].FileUrl;
                }

                var algResultMasterlists = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                log?.Info($"AlgResultMasterlists count {algResultMasterlists.Count}");
                foreach (var algResultMaster in algResultMasterlists)
                {
                    if (algResultMaster.ImgFileType == ViewResultAlgType.FindLightArea)
                    {
                        ctx.Result.ViewReslutW51.AlgResultLightAreaModels = AlgResultLightAreaDao.Instance.GetAllByPid(algResultMaster.Id);
                    }

                    if (algResultMaster.ImgFileType == ViewResultAlgType.FOV)
                    {
                        var algResultModels = DeatilCommonDao.Instance.GetAllByPid(algResultMaster.Id);
                        if (algResultModels.Count == 1)
                        {
                            DFovView view1 = new DFovView(algResultModels[0]);

                            view1.Result.result.D_Fov = view1.Result.result.D_Fov * ctx.ObjectiveTestResultFix.W51DiagonalFieldOfViewAngle;
                            view1.Result.result.ClolorVisionH_Fov = view1.Result.result.ClolorVisionH_Fov * ctx.ObjectiveTestResultFix.W51HorizontalFieldOfViewAngle;
                            view1.Result.result.ClolorVisionV_Fov = view1.Result.result.ClolorVisionV_Fov * ctx.ObjectiveTestResultFix.W51VerticalFieldOfViewAngle;

                            ctx.Result.ViewResultWhite.DFovView = view1;
                            ctx.ObjectiveTestResult.W51DiagonalFieldOfViewAngle = new ObjectiveTestItem()
                            {
                                Name = "DiagonalFieldOfViewAngle",
                                LowLimit = ctx.RecipeConfig.DiagonalFieldOfViewAngleMin,
                                UpLimit = ctx.RecipeConfig.DiagonalFieldOfViewAngleMax,
                                Value = view1.Result.result.D_Fov,
                                TestValue = view1.Result.result.D_Fov.ToString("F3")
                            };

                            ctx.ObjectiveTestResult.W51HorizontalFieldOfViewAngle = new ObjectiveTestItem()
                            {
                                Name = "HorizontalFieldOfViewAngle",
                                LowLimit = ctx.RecipeConfig.HorizontalFieldOfViewAngleMin,
                                UpLimit = ctx.RecipeConfig.HorizontalFieldOfViewAngleMax,
                                Value = view1.Result.result.ClolorVisionH_Fov,
                                TestValue = view1.Result.result.ClolorVisionH_Fov.ToString("F3")
                            };
                            ctx.ObjectiveTestResult.W51VerticalFieldOfViewAngle = new ObjectiveTestItem()
                            {
                                Name = "VerticalFieldOfViewAngle",
                                LowLimit = ctx.RecipeConfig.VerticalFieldOfViewAngleMin,
                                UpLimit = ctx.RecipeConfig.VerticalFieldOfViewAngleMax,
                                Value = view1.Result.result.ClolorVisionV_Fov,
                                TestValue = view1.Result.result.ClolorVisionV_Fov.ToString("F3")
                            };
                            ctx.Result.ViewReslutW51.DiagonalFieldOfViewAngle = ctx.ObjectiveTestResult.W51DiagonalFieldOfViewAngle;
                            ctx.Result.ViewReslutW51.HorizontalFieldOfViewAngle = ctx.ObjectiveTestResult.W51HorizontalFieldOfViewAngle;
                            ctx.Result.ViewReslutW51.VerticalFieldOfViewAngle = ctx.ObjectiveTestResult.W51VerticalFieldOfViewAngle;


                            ctx.Result.Result = ctx.Result.Result && ctx.ObjectiveTestResult.W51DiagonalFieldOfViewAngle.TestResult;
                            ctx.Result.Result = ctx.Result.Result && ctx.ObjectiveTestResult.W51HorizontalFieldOfViewAngle.TestResult;
                            ctx.Result.Result = ctx.Result.Result && ctx.ObjectiveTestResult.W51VerticalFieldOfViewAngle.TestResult;
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
