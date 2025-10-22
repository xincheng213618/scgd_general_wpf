using ColorVision.Database;
using ColorVision.Engine; // DAOs
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ProjectARVRPro.Process.W255;

namespace ProjectARVRPro.Process.W25
{
    public class W25Process : IProcess
    {
        public bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Logger;

            W25RecipeConfig recipeConfig = ctx.RecipeConfig.GetRequiredService<W25RecipeConfig>();
            W25FixConfig fixConfig = ctx.FixConfig.GetRequiredService<W25FixConfig>();

            try
            {
                log?.Info("���� White25 ���̽��");

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
                            center.Y *= fixConfig.W25CenterLunimance;
                            center.x *= fixConfig.W25CenterCIE1931ChromaticCoordinatesx;
                            center.y *= fixConfig.W25CenterCIE1931ChromaticCoordinatesy;
                            center.u *= fixConfig.W25CenterCIE1976ChromaticCoordinatesu;
                            center.v *= fixConfig.W25CenterCIE1976ChromaticCoordinatesv;

                            ctx.ObjectiveTestResult.W25CenterLunimance = new ObjectiveTestItem
                            {
                                Name = "W25CenterLunimance",
                                LowLimit = recipeConfig.W25CenterLunimance.Min,
                                UpLimit = recipeConfig.W25CenterLunimance.Max,
                                Value = center.Y,
                                TestValue = center.Y.ToString("F3") + " nit"
                            };
                            ctx.ObjectiveTestResult.W25CenterCIE1931ChromaticCoordinatesx = new ObjectiveTestItem
                            {
                                Name = "W25CenterCIE1931ChromaticCoordinatesx",
                                LowLimit = recipeConfig.W25CenterCIE1931ChromaticCoordinatesx.Min,
                                UpLimit = recipeConfig.W25CenterCIE1931ChromaticCoordinatesx.Max,
                                Value = center.x,
                                TestValue = center.x.ToString("F3")
                            };
                            ctx.ObjectiveTestResult.W25CenterCIE1931ChromaticCoordinatesy = new ObjectiveTestItem
                            {
                                Name = "W25CenterCIE1931ChromaticCoordinatesy",
                                LowLimit = recipeConfig.W25CenterCIE1931ChromaticCoordinatesy.Min,
                                UpLimit = recipeConfig.W25CenterCIE1931ChromaticCoordinatesy.Max,
                                Value = center.y,
                                TestValue = center.y.ToString("F3")
                            };
                            ctx.ObjectiveTestResult.W25CenterCIE1976ChromaticCoordinatesu = new ObjectiveTestItem
                            {
                                Name = "W25CenterCIE1976ChromaticCoordinatesu",
                                LowLimit = recipeConfig.W25CenterCIE1976ChromaticCoordinatesu.Min,
                                UpLimit = recipeConfig.W25CenterCIE1976ChromaticCoordinatesu.Max,
                                Value = center.u,
                                TestValue = center.u.ToString("F3")
                            };
                            ctx.ObjectiveTestResult.W25CenterCIE1976ChromaticCoordinatesv = new ObjectiveTestItem
                            {
                                Name = "W25CenterCIE1976ChromaticCoordinatesv",
                                LowLimit = recipeConfig.W25CenterCIE1976ChromaticCoordinatesv.Min,
                                UpLimit = recipeConfig.W25CenterCIE1976ChromaticCoordinatesv.Max,
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
            outtext += $"W25 ������Զ�AA����λ�㷨+��ע���㷨+���жԱȶ��㷨(�������ȱ�ֵ)" + Environment.NewLine;
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
