using ColorVision.Engine; // DAOs
using ColorVision.Engine.Templates.Jsons.FindCross; // FindCrossDetailViewReslut
using ColorVision.Engine.Templates.Jsons; // DetailCommonModel
using ColorVision.Database;

namespace ProjectARVRPro.Process.OpticCenter
{
    public class OpticCenterProcess : IProcess
    {
        public bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Logger;
            OpticCenterRecipeConfig recipeConfig = ctx.RecipeConfig.GetRequiredService<OpticCenterRecipeConfig>();

            try
            {
                log?.Info("处理 OpticCenter 流程结果");

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
                                ctx.ObjectiveTestResult.OptCenterXTilt = Build("OptCenterXTilt", find.FindCrossResult.result[0].tilt.tilt_x, recipeConfig.OptCenterXTiltMin, recipeConfig.OptCenterXTiltMax, "F4");
                                ctx.ObjectiveTestResult.OptCenterYTilt = Build("OptCenterYTilt", find.FindCrossResult.result[0].tilt.tilt_y, recipeConfig.OptCenterYTiltMin, recipeConfig.OptCenterYTiltMax, "F4");
                                ctx.ObjectiveTestResult.OptCenterRotation = Build("OptCenterRotation", find.FindCrossResult.result[0].rotationAngle, recipeConfig.OptCenterRotationMin, recipeConfig.OptCenterRotationMax, "F4");
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
                                ctx.ObjectiveTestResult.ImageCenterXTilt = Build("ImageCenterXTilt", find.FindCrossResult.result[0].tilt.tilt_x, recipeConfig.ImageCenterXTiltMin, recipeConfig.ImageCenterXTiltMax, "F4");
                                ctx.ObjectiveTestResult.ImageCenterYTilt = Build("ImageCenterYTilt", find.FindCrossResult.result[0].tilt.tilt_y, recipeConfig.ImageCenterYTiltMin, recipeConfig.ImageCenterYTiltMax, "F4");
                                ctx.ObjectiveTestResult.ImageCenterRotation = Build("ImageCenterRotation", find.FindCrossResult.result[0].rotationAngle, recipeConfig.ImageCenterRotationMin, recipeConfig.ImageCenterRotationMax, "F4");
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

        public string GenText(IProcessExecutionContext ctx)
        {
            var result = ctx.Result;
            string outtext = string.Empty;
            outtext += $"畸变鬼影 测试项：自动AA区域定位算法+畸变算法+鬼影算法" + Environment.NewLine;
            outtext += $"OpticCenter 测试项：" + Environment.NewLine;

            if (result.ViewResultOpticCenter.FindCrossDetailViewReslut != null)
            {
                outtext += $"Opt中心点x:{result.ViewResultOpticCenter.FindCrossDetailViewReslut.FindCrossResult.result[0].center.x} 中心点y:{result.ViewResultOpticCenter.FindCrossDetailViewReslut.FindCrossResult.result[0].center.y}" + Environment.NewLine;
                if (result.ViewResultOpticCenter.OptCenterXTilt != null)
                    outtext += $"OptCenterXTilt:{result.ViewResultOpticCenter.OptCenterXTilt.TestValue} LowLimit:{result.ViewResultOpticCenter.OptCenterXTilt.LowLimit}  UpLimit:{result.ViewResultOpticCenter.OptCenterXTilt.UpLimit},Rsult{(result.ViewResultOpticCenter.OptCenterXTilt.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
                if (result.ViewResultOpticCenter.OptCenterYTilt != null)
                    outtext += $"OptCenterYTilt:{result.ViewResultOpticCenter.OptCenterYTilt.TestValue} LowLimit:{result.ViewResultOpticCenter.OptCenterYTilt.LowLimit}  UpLimit:{result.ViewResultOpticCenter.OptCenterYTilt.UpLimit},Rsult{(result.ViewResultOpticCenter.OptCenterYTilt.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
                if (result.ViewResultOpticCenter.OptCenterRotation != null)
                    outtext += $"OptCenterRotation:{result.ViewResultOpticCenter.OptCenterRotation.TestValue} LowLimit:{result.ViewResultOpticCenter.OptCenterRotation.LowLimit}  UpLimit:{result.ViewResultOpticCenter.OptCenterRotation.UpLimit},Rsult{(result.ViewResultOpticCenter.OptCenterRotation.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            }
            if (result.ViewResultOpticCenter.FindCrossDetailViewReslut1 != null)
            {
                outtext += $"Image中心点x:{result.ViewResultOpticCenter.FindCrossDetailViewReslut1.FindCrossResult.result[0].center.x} 中心点y:{result.ViewResultOpticCenter.FindCrossDetailViewReslut1.FindCrossResult.result[0].center.y}" + Environment.NewLine;
                if (result.ViewResultOpticCenter.ImageCenterXTilt != null)
                    outtext += $"ImageCenterXTilt:{result.ViewResultOpticCenter.ImageCenterXTilt.TestValue} LowLimit:{result.ViewResultOpticCenter.ImageCenterXTilt.LowLimit}  UpLimit:{result.ViewResultOpticCenter.ImageCenterXTilt.UpLimit},Rsult{(result.ViewResultOpticCenter.ImageCenterXTilt.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
                if (result.ViewResultOpticCenter.ImageCenterYTilt != null)
                    outtext += $"ImageCenterYTilt:{result.ViewResultOpticCenter.ImageCenterYTilt.TestValue} LowLimit:{result.ViewResultOpticCenter.ImageCenterYTilt.LowLimit}  UpLimit:{result.ViewResultOpticCenter.ImageCenterYTilt.UpLimit},Rsult{(result.ViewResultOpticCenter.ImageCenterYTilt.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
                if (result.ViewResultOpticCenter.ImageCenterRotation != null)
                    outtext += $"ImageCenterRotation:{result.ViewResultOpticCenter.ImageCenterRotation.TestValue} LowLimit:{result.ViewResultOpticCenter.ImageCenterRotation.LowLimit}  UpLimit:{result.ViewResultOpticCenter.ImageCenterRotation.UpLimit},Rsult{(result.ViewResultOpticCenter.ImageCenterRotation.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            }

            return outtext;
        }

        public void Render(IProcessExecutionContext ctx)
        {
            
        }
    }
}
