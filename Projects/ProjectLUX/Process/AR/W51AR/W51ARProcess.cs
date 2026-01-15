using ColorVision.Database;
using ColorVision.Engine; // AlgResultMasterDao, MeasureImgResultDao, DeatilCommonDao
using ColorVision.Engine.Templates.Jsons; // DetailCommonModel
using ColorVision.Engine.Templates.Jsons.FOV2;
using Newtonsoft.Json;

namespace ProjectLUX.Process.AR.W51AR
{
    public class W51ARProcess : IProcess
    {
        public bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Logger;
            W51ARRecipeConfig recipeConfig = ctx.RecipeConfig.GetRequiredService<W51ARRecipeConfig>();
            W51ARFixConfig fixConfig = ctx.FixConfig.GetRequiredService<W51ARFixConfig>();
            W51ARViewTestResult testResult = new W51ARViewTestResult();

            try
            {
                var values = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                if (values.Count > 0)
                    ctx.Result.FileName = values[0].FileUrl;

                var masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                foreach (var master in masters)
                {
                    if (master.ImgFileType == ViewResultAlgType.FOV)
                    {
                        var algResultModels = DeatilCommonDao.Instance.GetAllByPid(master.Id);
                        if (algResultModels.Count == 1)
                        {
                            DFovView view1 = new DFovView(algResultModels[0]);


                            view1.Result.result.D_Fov = view1.Result.result.D_Fov * fixConfig.W51DiagonalFieldOfViewAngle;
                            testResult.DiagonalFieldOfViewAngle.LowLimit = recipeConfig.DiagonalFieldOfViewAngle.Min;
                            testResult.DiagonalFieldOfViewAngle.UpLimit = recipeConfig.DiagonalFieldOfViewAngle.Max;
                            testResult.DiagonalFieldOfViewAngle.Value = view1.Result.result.D_Fov;
                            testResult.DiagonalFieldOfViewAngle.TestValue = view1.Result.result.D_Fov.ToString("F3");
                            ctx.Result.Result = ctx.Result.Result && testResult.DiagonalFieldOfViewAngle.TestResult;
                            
                            view1.Result.result.ClolorVisionH_Fov = view1.Result.result.ClolorVisionH_Fov * fixConfig.W51HorizontalFieldOfViewAngle;
                            testResult.HorizontalFieldOfViewAngle.LowLimit = recipeConfig.HorizontalFieldOfViewAngle.Min;
                            testResult.HorizontalFieldOfViewAngle.UpLimit = recipeConfig.HorizontalFieldOfViewAngle.Max;
                            testResult.HorizontalFieldOfViewAngle.Value = view1.Result.result.ClolorVisionH_Fov;
                            testResult.HorizontalFieldOfViewAngle.TestValue = view1.Result.result.ClolorVisionH_Fov.ToString("F3");
                            ctx.Result.Result = ctx.Result.Result && testResult.HorizontalFieldOfViewAngle.TestResult;
                            
                            view1.Result.result.ClolorVisionV_Fov = view1.Result.result.ClolorVisionV_Fov * fixConfig.W51VerticalFieldOfViewAngle;
                            testResult.VerticalFieldOfViewAngle.LowLimit = recipeConfig.VerticalFieldOfViewAngle.Min;
                            testResult.VerticalFieldOfViewAngle.UpLimit = recipeConfig.VerticalFieldOfViewAngle.Max;
                            testResult.VerticalFieldOfViewAngle.Value = view1.Result.result.ClolorVisionV_Fov;
                            testResult.VerticalFieldOfViewAngle.TestValue = view1.Result.result.ClolorVisionV_Fov.ToString("F3");
                            ctx.Result.Result = ctx.Result.Result && testResult.VerticalFieldOfViewAngle.TestResult;

                        }

                    }
                }
                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);
                ctx.ObjectiveTestResult.W51ARTestResult = JsonConvert.DeserializeObject<W51ARTestResult>(ctx.Result.ViewResultJson) ?? new W51ARTestResult();

                return true;
            }
            catch (Exception ex)
            {
                log?.Error(ex);
                return false;
            }
        }

        public void Render (IProcessExecutionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return;
            W51ARViewTestResult testResult = JsonConvert.DeserializeObject<W51ARViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return;
        }

        public string GenText(IProcessExecutionContext ctx)
        {
            var result = ctx.Result;
            string outtext = string.Empty;
            outtext += $"W255 画面结果" + Environment.NewLine;

            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return outtext;
            W51ARViewTestResult testResult = JsonConvert.DeserializeObject<W51ARViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return outtext;


            outtext += $"DiagonalFieldOfViewAngle:{testResult.DiagonalFieldOfViewAngle.TestValue}  LowLimit:{testResult.DiagonalFieldOfViewAngle.LowLimit} UpLimit:{testResult.DiagonalFieldOfViewAngle.UpLimit},Rsult{(testResult.DiagonalFieldOfViewAngle.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            outtext += $"HorizontalFieldOfViewAngle:{testResult.HorizontalFieldOfViewAngle.TestValue} LowLimit:{testResult.HorizontalFieldOfViewAngle.LowLimit} UpLimit:{testResult.HorizontalFieldOfViewAngle.UpLimit} ,Rsult{(testResult.HorizontalFieldOfViewAngle.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            outtext += $"VerticalFieldOfViewAngle:{testResult.VerticalFieldOfViewAngle.TestValue} LowLimit:{testResult.VerticalFieldOfViewAngle.LowLimit} UpLimit:{testResult.VerticalFieldOfViewAngle.UpLimit},Rsult{(testResult.VerticalFieldOfViewAngle.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            return outtext;
        }
    }
}
