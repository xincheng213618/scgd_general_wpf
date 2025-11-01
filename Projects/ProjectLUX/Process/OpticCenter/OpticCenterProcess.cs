using ColorVision.Database;
using ColorVision.Engine; // DAOs
using ColorVision.Engine.Templates.Jsons; // DetailCommonModel
using ColorVision.Engine.Templates.Jsons.FindCross; // FindCrossDetailViewReslut
using Newtonsoft.Json;

namespace ProjectLUX.Process.OpticCenter
{
    public class OpticCenterProcess : IProcess
    {
        public bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Logger;
            OpticCenterRecipeConfig recipeConfig = ctx.RecipeConfig.GetRequiredService<OpticCenterRecipeConfig>();
            OpticCenterFixConfig fixConfig = ctx.FixConfig.GetRequiredService<OpticCenterFixConfig>();
            OpticCenterTestResult testResult = new OpticCenterTestResult();

            try
            {
                log?.Info("光轴校准");

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
                                // 先应用修正系数（保留你原有的三行）
                                find.FindCrossResult.result[0].tilt.tilt_x *= fixConfig.OptCenterXTilt;
                                find.FindCrossResult.result[0].tilt.tilt_y *= fixConfig.OptCenterYTilt;
                                find.FindCrossResult.result[0].rotationAngle *= fixConfig.OptCenterRotation;

                                // OptCenterXTilt
                                testResult.OptCenterXTilt.Value = find.FindCrossResult.result[0].tilt.tilt_x;
                                testResult.OptCenterXTilt.LowLimit = recipeConfig.OptCenterXTilt.Min;
                                testResult.OptCenterXTilt.UpLimit = recipeConfig.OptCenterXTilt.Max;
                                testResult.OptCenterXTilt.TestValue = testResult.OptCenterXTilt.Value.ToString("F4");
                                ctx.Result.Result &= testResult.OptCenterXTilt.TestResult;

                                // OptCenterYTilt
                                testResult.OptCenterYTilt.Value = find.FindCrossResult.result[0].tilt.tilt_y;
                                testResult.OptCenterYTilt.LowLimit = recipeConfig.OptCenterYTilt.Min;
                                testResult.OptCenterYTilt.UpLimit = recipeConfig.OptCenterYTilt.Max;
                                testResult.OptCenterYTilt.TestValue = testResult.OptCenterYTilt.Value.ToString("F4");
                                ctx.Result.Result &= testResult.OptCenterYTilt.TestResult;

                                // OptCenterRotation
                                testResult.OptCenterRotation.Value = find.FindCrossResult.result[0].rotationAngle;
                                testResult.OptCenterRotation.LowLimit = recipeConfig.OptCenterRotation.Min;
                                testResult.OptCenterRotation.UpLimit = recipeConfig.OptCenterRotation.Max;
                                testResult.OptCenterRotation.TestValue = testResult.OptCenterRotation.Value.ToString("F4");
                                ctx.Result.Result &= testResult.OptCenterRotation.TestResult;
                            }
                        }
                    }
                }

                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);
                ctx.ObjectiveTestResult.OpticCenterTestResult = testResult;
                return true;
            }
            catch (Exception ex)
            {
                log?.Error(ex);
                return false;
            }
        }


        public string GenText(IProcessExecutionContext ctx)
        {
            var result = ctx.Result;
            string outtext = string.Empty;
            outtext += $"光轴校准 + Environment.NewLine";

            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return outtext;
            OpticCenterTestResult testResult = JsonConvert.DeserializeObject<OpticCenterTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return outtext;

            if (testResult.OptCenterXTilt != null)
                outtext += $"OptCenterXTilt:{testResult.OptCenterXTilt.TestValue} LowLimit:{testResult.OptCenterXTilt.LowLimit}  UpLimit:{testResult.OptCenterXTilt.UpLimit},Rsult{(testResult.OptCenterXTilt.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            if (testResult.OptCenterYTilt != null)
                outtext += $"OptCenterYTilt:{testResult.OptCenterYTilt.TestValue} LowLimit:{testResult.OptCenterYTilt.LowLimit}  UpLimit:{testResult.OptCenterYTilt.UpLimit},Rsult{(testResult.OptCenterYTilt.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            if (testResult.OptCenterRotation != null)
                outtext += $"OptCenterRotation:{testResult.OptCenterRotation.TestValue} LowLimit:{testResult.OptCenterRotation.LowLimit}  UpLimit:{testResult.OptCenterRotation.UpLimit},Rsult{(testResult.OptCenterRotation.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            //if (testResult.ImageCenterXTilt != null)
            //    outtext += $"ImageCenterXTilt:{testResult.ImageCenterXTilt.TestValue} LowLimit:{testResult.ImageCenterXTilt.LowLimit}  UpLimit:{testResult.ImageCenterXTilt.UpLimit},Rsult{(testResult.ImageCenterXTilt.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            //if (testResult.ImageCenterYTilt != null)
            //    outtext += $"ImageCenterYTilt:{testResult.ImageCenterYTilt.TestValue} LowLimit:{testResult.ImageCenterYTilt.LowLimit}  UpLimit:{testResult.ImageCenterYTilt.UpLimit},Rsult{(testResult.ImageCenterYTilt.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            //if (testResult.ImageCenterRotation != null)
            //    outtext += $"ImageCenterRotation:{testResult.ImageCenterRotation.TestValue} LowLimit:{testResult.ImageCenterRotation.LowLimit}  UpLimit:{testResult.ImageCenterRotation.UpLimit},Rsult{(testResult.ImageCenterRotation.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            return outtext;
        }

        public void Render(IProcessExecutionContext ctx)
        {
            
        }
    }
}
