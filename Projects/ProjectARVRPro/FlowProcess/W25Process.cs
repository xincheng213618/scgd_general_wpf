using ColorVision.Engine; // DAOs
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.Database;

namespace ProjectARVRPro.FlowProcess
{
    public class W25Process : IProcess
    {
        public bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Logger;
            try
            {
                log?.Info("处理 White25 流程结果");

                var values = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                if (values.Count > 0)
                    ctx.Result.FileName = values[0].FileUrl;

                var masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                foreach (var master in masters)
                {
                    if (master.ImgFileType == ViewResultAlgType.POI_XYZ)
                    {
                        ctx.Result.ViewResultW25.PoiResultCIExyuvDatas = new List<PoiResultCIExyuvData>();
                        var poiPoints = PoiPointResultDao.Instance.GetAllByPid(master.Id);
                        int id = 0;
                        foreach (var item in poiPoints)
                        {
                            var poi = new PoiResultCIExyuvData(item) { Id = id++ };
                            ctx.Result.ViewResultW25.PoiResultCIExyuvDatas.Add(poi);
                        }
                        if (ctx.Result.ViewResultW25.PoiResultCIExyuvDatas.Count == 1)
                        {
                            var center = ctx.Result.ViewResultW25.PoiResultCIExyuvDatas[0];
                            center.Y *= ctx.ObjectiveTestResultFix.W25CenterLunimance;
                            center.x *= ctx.ObjectiveTestResultFix.W25CenterCIE1931ChromaticCoordinatesx;
                            center.y *= ctx.ObjectiveTestResultFix.W25CenterCIE1931ChromaticCoordinatesy;
                            center.u *= ctx.ObjectiveTestResultFix.W25CenterCIE1976ChromaticCoordinatesu;
                            center.v *= ctx.ObjectiveTestResultFix.W25CenterCIE1976ChromaticCoordinatesv;

                            ctx.ObjectiveTestResult.W25CenterLunimance = new ObjectiveTestItem
                            {
                                Name = "W25CenterLunimance",
                                LowLimit = ctx.RecipeConfig.W25CenterLunimanceMin,
                                UpLimit = ctx.RecipeConfig.W25CenterLunimanceMax,
                                Value = center.Y,
                                TestValue = center.Y.ToString("F3") + " nit"
                            };
                            ctx.ObjectiveTestResult.W25CenterCIE1931ChromaticCoordinatesx = new ObjectiveTestItem
                            {
                                Name = "W25CenterCIE1931ChromaticCoordinatesx",
                                LowLimit = ctx.RecipeConfig.W25CenterCIE1931ChromaticCoordinatesxMin,
                                UpLimit = ctx.RecipeConfig.W25CenterCIE1931ChromaticCoordinatesxMax,
                                Value = center.x,
                                TestValue = center.x.ToString("F3")
                            };
                            ctx.ObjectiveTestResult.W25CenterCIE1931ChromaticCoordinatesy = new ObjectiveTestItem
                            {
                                Name = "W25CenterCIE1931ChromaticCoordinatesy",
                                LowLimit = ctx.RecipeConfig.W25CenterCIE1931ChromaticCoordinatesyMin,
                                UpLimit = ctx.RecipeConfig.W25CenterCIE1931ChromaticCoordinatesyMax,
                                Value = center.y,
                                TestValue = center.y.ToString("F3")
                            };
                            ctx.ObjectiveTestResult.W25CenterCIE1976ChromaticCoordinatesu = new ObjectiveTestItem
                            {
                                Name = "W25CenterCIE1976ChromaticCoordinatesu",
                                LowLimit = ctx.RecipeConfig.W25CenterCIE1976ChromaticCoordinatesuMin,
                                UpLimit = ctx.RecipeConfig.W25CenterCIE1976ChromaticCoordinatesuMax,
                                Value = center.u,
                                TestValue = center.u.ToString("F3")
                            };
                            ctx.ObjectiveTestResult.W25CenterCIE1976ChromaticCoordinatesv = new ObjectiveTestItem
                            {
                                Name = "W25CenterCIE1976ChromaticCoordinatesv",
                                LowLimit = ctx.RecipeConfig.W25CenterCIE1976ChromaticCoordinatesvMin,
                                UpLimit = ctx.RecipeConfig.W25CenterCIE1976ChromaticCoordinatesvMax,
                                Value = center.v,
                                TestValue = center.v.ToString("F3")
                            };

                            ctx.Result.Result &= ctx.ObjectiveTestResult.W25CenterLunimance.TestResult;
                            ctx.Result.Result &= ctx.ObjectiveTestResult.W25CenterCIE1931ChromaticCoordinatesx.TestResult;
                            ctx.Result.Result &= ctx.ObjectiveTestResult.W25CenterCIE1931ChromaticCoordinatesy.TestResult;
                            ctx.Result.Result &= ctx.ObjectiveTestResult.W25CenterCIE1976ChromaticCoordinatesu.TestResult;
                            ctx.Result.Result &= ctx.ObjectiveTestResult.W25CenterCIE1976ChromaticCoordinatesv.TestResult;
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
           
        }

        public string GenText(IProcessExecutionContext ctx)
        {
            var result = ctx.Result;
            string outtext = string.Empty;
            outtext += $"W25 测试项：自动AA区域定位算法+关注点算法+序列对比度算法(中心亮度比值)" + Environment.NewLine;
            if (result.ViewResultW25.PoiResultCIExyuvDatas != null)
            {
                foreach (var item in result.ViewResultW25.PoiResultCIExyuvDatas)
                {
                    outtext += $"{item.Name}  X:{item.X.ToString("F2")} Y:{item.Y.ToString("F2")} Z:{item.Z.ToString("F2")} x:{item.x.ToString("F2")} y:{item.y.ToString("F2")} u:{item.u.ToString("F2")} v:{item.v.ToString("F2")} cct:{item.CCT.ToString("F2")} wave:{item.Wave.ToString("F2")}{Environment.NewLine}";
                }
            }
            return outtext;
        }
    }
}
