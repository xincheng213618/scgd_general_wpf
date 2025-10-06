using ColorVision.Engine; // DAOs
using ColorVision.Engine.Templates.Jsons.FindCross; // FindCrossDetailViewReslut
using ColorVision.Engine.Templates.Jsons; // DetailCommonModel
using ColorVision.Database;

namespace ProjectARVRPro
{
    public class OpticCenterProcess : IProcess
    {
        public bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Logger;
            try
            {
                log?.Info("处理 OpticCenter 流程结果");
                ctx.ObjectiveTestResult.FlowOpticCenterTestReslut = true;

                var values = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                if (values.Count > 0)
                    ctx.Result.FileName = values[0].FileUrl;

                var masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                foreach (var master in masters)
                {
                    if (master.ImgFileType == ViewResultAlgType.FindCross)
                    {
                        var details = DeatilCommonDao.Instance.GetAllByPid(master.Id);
                        if (details.Count == 1)
                        {
                            var find = new FindCrossDetailViewReslut(details[0]);
                            if (master.TName == "optCenter")
                            {
                                find.FindCrossResult.result[0].tilt.tilt_x *= ctx.ObjectiveTestResultFix.OptCenterXTilt;
                                find.FindCrossResult.result[0].tilt.tilt_y *= ctx.ObjectiveTestResultFix.OptCenterYTilt;
                                find.FindCrossResult.result[0].rotationAngle *= ctx.ObjectiveTestResultFix.OptCenterRotation;
                                ctx.Result.ViewResultOpticCenter.FindCrossDetailViewReslut = find;
                                ctx.ObjectiveTestResult.OptCenterXTilt = Build("OptCenterXTilt", find.FindCrossResult.result[0].tilt.tilt_x, ctx.RecipeConfig.OptCenterXTiltMin, ctx.RecipeConfig.OptCenterXTiltMax, "F4");
                                ctx.ObjectiveTestResult.OptCenterYTilt = Build("OptCenterYTilt", find.FindCrossResult.result[0].tilt.tilt_y, ctx.RecipeConfig.OptCenterYTiltMin, ctx.RecipeConfig.OptCenterYTiltMax, "F4");
                                ctx.ObjectiveTestResult.OptCenterRotation = Build("OptCenterRotation", find.FindCrossResult.result[0].rotationAngle, ctx.RecipeConfig.OptCenterRotationMin, ctx.RecipeConfig.OptCenterRotationMax, "F4");
                                ctx.Result.ViewResultOpticCenter.OptCenterXTilt = ctx.ObjectiveTestResult.OptCenterXTilt;
                                ctx.Result.ViewResultOpticCenter.OptCenterYTilt = ctx.ObjectiveTestResult.OptCenterYTilt;
                                ctx.Result.ViewResultOpticCenter.OptCenterRotation = ctx.ObjectiveTestResult.OptCenterRotation;
                                ctx.Result.Result &= ctx.ObjectiveTestResult.OptCenterXTilt.TestResult;
                                ctx.Result.Result &= ctx.ObjectiveTestResult.OptCenterYTilt.TestResult;
                                ctx.Result.Result &= ctx.ObjectiveTestResult.OptCenterRotation.TestResult;
                            }
                            else if (master.TName == "ImageCenter")
                            {
                                find.FindCrossResult.result[0].tilt.tilt_x *= ctx.ObjectiveTestResultFix.ImageCenterXTilt;
                                find.FindCrossResult.result[0].tilt.tilt_y *= ctx.ObjectiveTestResultFix.ImageCenterYTilt;
                                find.FindCrossResult.result[0].rotationAngle *= ctx.ObjectiveTestResultFix.ImageCenterRotation;
                                ctx.Result.ViewResultOpticCenter.FindCrossDetailViewReslut1 = find;
                                ctx.ObjectiveTestResult.ImageCenterXTilt = Build("ImageCenterXTilt", find.FindCrossResult.result[0].tilt.tilt_x, ctx.RecipeConfig.ImageCenterXTiltMin, ctx.RecipeConfig.ImageCenterXTiltMax, "F4");
                                ctx.ObjectiveTestResult.ImageCenterYTilt = Build("ImageCenterYTilt", find.FindCrossResult.result[0].tilt.tilt_y, ctx.RecipeConfig.ImageCenterYTiltMin, ctx.RecipeConfig.ImageCenterYTiltMax, "F4");
                                ctx.ObjectiveTestResult.ImageCenterRotation = Build("ImageCenterRotation", find.FindCrossResult.result[0].rotationAngle, ctx.RecipeConfig.ImageCenterRotationMin, ctx.RecipeConfig.ImageCenterRotationMax, "F4");
                                ctx.Result.ViewResultOpticCenter.ImageCenterXTilt = ctx.ObjectiveTestResult.ImageCenterXTilt;
                                ctx.Result.ViewResultOpticCenter.ImageCenterYTilt = ctx.ObjectiveTestResult.ImageCenterYTilt;
                                ctx.Result.ViewResultOpticCenter.ImageCenterRotation = ctx.ObjectiveTestResult.ImageCenterRotation;
                                ctx.Result.Result &= ctx.ObjectiveTestResult.ImageCenterXTilt.TestResult;
                                ctx.Result.Result &= ctx.ObjectiveTestResult.ImageCenterYTilt.TestResult;
                                ctx.Result.Result &= ctx.ObjectiveTestResult.ImageCenterRotation.TestResult;
                            }
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

        private ObjectiveTestItem Build(string name, double value, double low, double up, string fmt) => new ObjectiveTestItem
        {
            Name = name,
            LowLimit = low,
            UpLimit = up,
            Value = value,
            TestValue = value.ToString(fmt)
        };
    }
}
