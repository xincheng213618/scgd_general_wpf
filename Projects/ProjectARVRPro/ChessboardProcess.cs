using System.Collections.ObjectModel;
using ColorVision.Engine; // DAOs
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.Engine.Templates.Jsons; // DetailCommonModel
using ColorVision.Engine.Templates.Jsons.PoiAnalysis; // PoiAnalysisDetailViewReslut
using ColorVision.Database;

namespace ProjectARVRPro
{
    public class ChessboardProcess : IProcess
    {
        public bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Logger;
            try
            {
                log?.Info("处理 Chessboard 流程结果");
                ctx.ObjectiveTestResult.FlowChessboardTestReslut = true;

                var values = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                if (values.Count > 0)
                    ctx.Result.FileName = values[0].FileUrl;

                var masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                foreach (var master in masters)
                {
                    if (master.ImgFileType == ViewResultAlgType.POI_XYZ)
                    {
                        ctx.Result.ViewReslutCheckerboard.PoiResultCIExyuvDatas = new ObservableCollection<PoiResultCIExyuvData>();
                        var poiPoints = PoiPointResultDao.Instance.GetAllByPid(master.Id);
                        int id = 0;
                        foreach (var item in poiPoints)
                        {
                            var poi = new PoiResultCIExyuvData(item) { Id = id++ };
                            ctx.Result.ViewReslutCheckerboard.PoiResultCIExyuvDatas.Add(poi);
                        }
                    }

                    if (master.ImgFileType == ViewResultAlgType.PoiAnalysis && master.TName.Contains("Chessboard_Contrast"))
                    {
                        var details = DeatilCommonDao.Instance.GetAllByPid(master.Id);
                        if (details.Count == 1)
                        {
                            var view = new PoiAnalysisDetailViewReslut(details[0]);
                            view.PoiAnalysisResult.result.Value *= ctx.ObjectiveTestResultFix.ChessboardContrast;
                            var contrast = new ObjectiveTestItem
                            {
                                Name = "Chessboard_Contrast",
                                LowLimit = ctx.RecipeConfig.ChessboardContrastMin,
                                UpLimit = ctx.RecipeConfig.ChessboardContrastMax,
                                Value = view.PoiAnalysisResult.result.Value,
                                TestValue = view.PoiAnalysisResult.result.Value.ToString("F3")
                            };
                            ctx.ObjectiveTestResult.ChessboardContrast = contrast;
                            ctx.Result.ViewReslutCheckerboard.ChessboardContrast = contrast;
                            ctx.Result.Result &= contrast.TestResult;
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
