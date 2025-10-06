using ColorVision.Engine; // AlgResultMasterDao, MeasureImgResultDao, DeatilCommonDao
using ColorVision.Engine.Templates.Jsons; // DetailCommonModel
using ColorVision.Engine.Templates.Jsons.PoiAnalysis; // PoiAnalysisDetailViewReslut
using ColorVision.Engine.Templates.POI.AlgorithmImp; // PoiPointResultModel
using ColorVision.Database;

namespace ProjectARVRPro
{
    public class White255Process : IProcess
    {
        public bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Logger;
            try
            {
                log?.Info("处理 White255 流程结果");
                ctx.ObjectiveTestResult.FlowWhiteTestReslut = true;

                var values = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                if (values.Count > 0)
                    ctx.Result.FileName = values[0].FileUrl;

                var masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                foreach (var master in masters)
                {
                    if (master.ImgFileType == ViewResultAlgType.POI_XYZ)
                    {
                        ctx.Result.ViewResultWhite.PoiResultCIExyuvDatas = new List<PoiResultCIExyuvData>();
                        var poiPoints = PoiPointResultDao.Instance.GetAllByPid(master.Id);
                        int id = 0;
                        ctx.ObjectiveTestResult.W255PoixyuvDatas.Clear();
                        foreach (var item in poiPoints)
                        {
                            var poi = new PoiResultCIExyuvData(item) { Id = id++ };
                            ctx.ObjectiveTestResult.W255PoixyuvDatas.Add(new PoixyuvData()
                            {
                                Id = poi.Id,
                                Name = poi.Name,
                                CCT = poi.CCT * ctx.ObjectiveTestResultFix.BlackCenterCorrelatedColorTemperature,
                                X = poi.X,
                                Y = poi.Y * ctx.ObjectiveTestResultFix.W255CenterLunimance,
                                Z = poi.Z,
                                Wave = poi.Wave,
                                x = poi.x * ctx.ObjectiveTestResultFix.W255CenterCIE1931ChromaticCoordinatesx,
                                y = poi.y * ctx.ObjectiveTestResultFix.W255CenterCIE1931ChromaticCoordinatesy,
                                u = poi.u * ctx.ObjectiveTestResultFix.W255CenterCIE1976ChromaticCoordinatesu,
                                v = poi.v * ctx.ObjectiveTestResultFix.W255CenterCIE1976ChromaticCoordinatesv
                            });
                            if (item.PoiName == "POI_5")
                            {
                                poi.CCT *= ctx.ObjectiveTestResultFix.BlackCenterCorrelatedColorTemperature;
                                poi.Y *= ctx.ObjectiveTestResultFix.W255CenterLunimance;
                                poi.x *= ctx.ObjectiveTestResultFix.W255CenterCIE1931ChromaticCoordinatesx;
                                poi.y *= ctx.ObjectiveTestResultFix.W255CenterCIE1931ChromaticCoordinatesy;
                                poi.u *= ctx.ObjectiveTestResultFix.W255CenterCIE1976ChromaticCoordinatesu;
                                poi.v *= ctx.ObjectiveTestResultFix.W255CenterCIE1976ChromaticCoordinatesv;

                                var centerCCT = new ObjectiveTestItem
                                {
                                    Name = "CenterCorrelatedColorTemperature",
                                    TestValue = poi.CCT.ToString(),
                                    Value = poi.CCT,
                                    LowLimit = ctx.RecipeConfig.CenterCorrelatedColorTemperatureMin,
                                    UpLimit = ctx.RecipeConfig.CenterCorrelatedColorTemperatureMax
                                };
                                ctx.ObjectiveTestResult.BlackCenterCorrelatedColorTemperature = centerCCT;
                                ctx.Result.ViewResultWhite.CenterCorrelatedColorTemperature = centerCCT;
                                ctx.Result.Result &= centerCCT.TestResult;

                                ctx.ObjectiveTestResult.W255CenterLunimance = new ObjectiveTestItem
                                {
                                    Name = "W255CenterLunimance",
                                    LowLimit = ctx.RecipeConfig.W255CenterLunimanceMin,
                                    UpLimit = ctx.RecipeConfig.W255CenterLunimanceMax,
                                    Value = poi.Y,
                                    TestValue = poi.Y.ToString("F3") + " nit"
                                };
                                ctx.ObjectiveTestResult.W255CenterCIE1931ChromaticCoordinatesx = new ObjectiveTestItem
                                {
                                    Name = "W255CenterCIE1931ChromaticCoordinatesx",
                                    LowLimit = ctx.RecipeConfig.W255CenterCIE1931ChromaticCoordinatesxMin,
                                    UpLimit = ctx.RecipeConfig.W255CenterCIE1931ChromaticCoordinatesxMax,
                                    Value = poi.x,
                                    TestValue = poi.x.ToString("F3")
                                };
                                ctx.ObjectiveTestResult.W255CenterCIE1931ChromaticCoordinatesy = new ObjectiveTestItem
                                {
                                    Name = "W255CenterCIE1931ChromaticCoordinatesy",
                                    LowLimit = ctx.RecipeConfig.W255CenterCIE1931ChromaticCoordinatesyMin,
                                    UpLimit = ctx.RecipeConfig.W255CenterCIE1931ChromaticCoordinatesyMax,
                                    Value = poi.y,
                                    TestValue = poi.y.ToString("F3")
                                };
                                ctx.ObjectiveTestResult.W255CenterCIE1976ChromaticCoordinatesu = new ObjectiveTestItem
                                {
                                    Name = "W255CenterCIE1976ChromaticCoordinatesu",
                                    LowLimit = ctx.RecipeConfig.W255CenterCIE1976ChromaticCoordinatesuMin,
                                    UpLimit = ctx.RecipeConfig.W255CenterCIE1976ChromaticCoordinatesuMax,
                                    Value = poi.u,
                                    TestValue = poi.u.ToString("F3")
                                };
                                ctx.ObjectiveTestResult.W255CenterCIE1976ChromaticCoordinatesv = new ObjectiveTestItem
                                {
                                    Name = "W255CenterCIE1976ChromaticCoordinatesv",
                                    LowLimit = ctx.RecipeConfig.W255CenterCIE1976ChromaticCoordinatesvMin,
                                    UpLimit = ctx.RecipeConfig.W255CenterCIE1976ChromaticCoordinatesvMax,
                                    Value = poi.v,
                                    TestValue = poi.v.ToString("F3")
                                };

                                ctx.Result.Result &= ctx.ObjectiveTestResult.W255CenterLunimance.TestResult;
                                ctx.Result.Result &= ctx.ObjectiveTestResult.W255CenterCIE1931ChromaticCoordinatesx.TestResult;
                                ctx.Result.Result &= ctx.ObjectiveTestResult.W255CenterCIE1931ChromaticCoordinatesy.TestResult;
                                ctx.Result.Result &= ctx.ObjectiveTestResult.W255CenterCIE1976ChromaticCoordinatesu.TestResult;
                                ctx.Result.Result &= ctx.ObjectiveTestResult.W255CenterCIE1976ChromaticCoordinatesv.TestResult;
                            }
                            ctx.Result.ViewResultWhite.PoiResultCIExyuvDatas.Add(poi);
                        }
                    }
                    if (master.ImgFileType == ViewResultAlgType.PoiAnalysis)
                    {
                        if (master.TName.Contains("Luminance_uniformity"))
                        {
                            var details = DeatilCommonDao.Instance.GetAllByPid(master.Id);
                            if (details.Count == 1)
                            {
                                var view = new PoiAnalysisDetailViewReslut(details[0]);
                                view.PoiAnalysisResult.result.Value *= ctx.ObjectiveTestResultFix.W255LuminanceUniformity;
                                var uniform = new ObjectiveTestItem
                                {
                                    Name = "Luminance_uniformity(%)",
                                    TestValue = (view.PoiAnalysisResult.result.Value * 100).ToString("F3") + "%",
                                    Value = view.PoiAnalysisResult.result.Value,
                                    LowLimit = ctx.RecipeConfig.W255LuminanceUniformityMin,
                                    UpLimit = ctx.RecipeConfig.W255LuminanceUniformityMax
                                };
                                ctx.ObjectiveTestResult.W255LuminanceUniformity = uniform;
                                ctx.Result.ViewResultWhite.W255LuminanceUniformity = uniform;
                                ctx.Result.Result &= uniform.TestResult;
                            }
                        }
                        if (master.TName.Contains("Color_uniformity"))
                        {
                            var details = DeatilCommonDao.Instance.GetAllByPid(master.Id);
                            if (details.Count == 1)
                            {
                                var view = new PoiAnalysisDetailViewReslut(details[0]);
                                view.PoiAnalysisResult.result.Value *= ctx.ObjectiveTestResultFix.W255ColorUniformity;
                                var colorUniform = new ObjectiveTestItem
                                {
                                    Name = "Color_uniformity",
                                    TestValue = view.PoiAnalysisResult.result.Value.ToString("F5"),
                                    Value = view.PoiAnalysisResult.result.Value,
                                    LowLimit = ctx.RecipeConfig.W255ColorUniformityMin,
                                    UpLimit = ctx.RecipeConfig.W255ColorUniformityMax
                                };
                                ctx.ObjectiveTestResult.W255ColorUniformity = colorUniform;
                                ctx.Result.ViewResultWhite.W255ColorUniformity = colorUniform;
                                ctx.Result.Result &= colorUniform.TestResult;
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
    }
}
