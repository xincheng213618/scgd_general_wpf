using ColorVision.Engine; // DAOs
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.Database;

namespace ProjectARVRPro
{
    public class BlackProcess : IProcess
    {
        public bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Logger;
            try
            {
                log?.Info("处理 Black 流程结果");
                ctx.ObjectiveTestResult.FlowBlackTestReslut = true;

                var values = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                if (values.Count > 0)
                    ctx.Result.FileName = values[0].FileUrl;

                var masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                foreach (var master in masters)
                {
                    if (master.ImgFileType == ViewResultAlgType.POI_XYZ)
                    {
                        ctx.Result.ViewResultBlack.PoiResultCIExyuvDatas = new List<PoiResultCIExyuvData>();
                        var poiPoints = PoiPointResultDao.Instance.GetAllByPid(master.Id);
                        int id = 0;
                        foreach (var item in poiPoints)
                        {
                            var poi = new PoiResultCIExyuvData(item) { Id = id++ };
                            ctx.Result.ViewResultBlack.PoiResultCIExyuvDatas.Add(poi);
                        }
                        // 需要白画面的亮度才能计算对比度
                        if (ctx.Result.ViewResultWhite != null && ctx.Result.ViewResultWhite.PoiResultCIExyuvDatas != null && ctx.Result.ViewResultWhite.PoiResultCIExyuvDatas.Count == 9 && ctx.Result.ViewResultBlack.PoiResultCIExyuvDatas.Count == 1)
                        {
                            double contrast = ctx.Result.ViewResultWhite.PoiResultCIExyuvDatas[5].Y / ctx.Result.ViewResultBlack.PoiResultCIExyuvDatas[0].Y;
                            contrast *= ctx.ObjectiveTestResultFix.FOFOContrast;
                            var fofo = new ObjectiveTestItem
                            {
                                Name = "FOFOContrast",
                                LowLimit = ctx.RecipeConfig.FOFOContrastMin,
                                UpLimit = ctx.RecipeConfig.FOFOContrastMax,
                                Value = contrast,
                                TestValue = contrast.ToString("F2")
                            };
                            ctx.ObjectiveTestResult.FOFOContrast = fofo;
                            ctx.Result.ViewResultBlack.FOFOContrast = fofo;
                            ctx.Result.Result &= fofo.TestResult;
                        }
                        else
                        {
                            log?.Info("计算对比度前需要白画面数据");
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
