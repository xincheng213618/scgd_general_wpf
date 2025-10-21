using ColorVision.Database;
using ColorVision.Engine; // AlgResultMasterDao, MeasureImgResultDao, DeatilCommonDao
using ColorVision.Engine.Media;
using ColorVision.Engine.Templates.Jsons; // DetailCommonModel
using ColorVision.Engine.Templates.Jsons.PoiAnalysis; // PoiAnalysisDetailViewReslut
using ColorVision.Engine.Templates.POI.AlgorithmImp; // PoiPointResultModel
using ColorVision.ImageEditor.Draw;
using CVCommCore.CVAlgorithm;
using ProjectARVRPro.Process.W255;
using System.Windows;
using System.Windows.Media;

namespace ProjectARVRPro.Process.Red
{
    public class RedProcess : IProcess
    {
        public bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Logger;
            W255RecipeConfig recipeConfig = ctx.RecipeConfig.GetRequiredService<W255RecipeConfig>();
            try
            {
                log?.Info("处理 Red 流程结果");
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
                                    LowLimit = recipeConfig.CenterCorrelatedColorTemperatureMin,
                                    UpLimit = recipeConfig.CenterCorrelatedColorTemperatureMax
                                };
                                ctx.ObjectiveTestResult.BlackCenterCorrelatedColorTemperature = centerCCT;
                                ctx.Result.ViewResultWhite.CenterCorrelatedColorTemperature = centerCCT;
                                ctx.Result.Result &= centerCCT.TestResult;

                                ctx.ObjectiveTestResult.W255CenterLunimance = new ObjectiveTestItem
                                {
                                    Name = "W255CenterLunimance",
                                    LowLimit = recipeConfig.W255CenterLunimanceMin,
                                    UpLimit = recipeConfig.W255CenterLunimanceMax,
                                    Value = poi.Y,
                                    TestValue = poi.Y.ToString("F3") + " nit"
                                };
                                ctx.ObjectiveTestResult.W255CenterCIE1931ChromaticCoordinatesx = new ObjectiveTestItem
                                {
                                    Name = "W255CenterCIE1931ChromaticCoordinatesx",
                                    LowLimit = recipeConfig.W255CenterCIE1931ChromaticCoordinatesxMin,
                                    UpLimit = recipeConfig.W255CenterCIE1931ChromaticCoordinatesxMax,
                                    Value = poi.x,
                                    TestValue = poi.x.ToString("F3")
                                };
                                ctx.ObjectiveTestResult.W255CenterCIE1931ChromaticCoordinatesy = new ObjectiveTestItem
                                {
                                    Name = "W255CenterCIE1931ChromaticCoordinatesy",
                                    LowLimit = recipeConfig.W255CenterCIE1931ChromaticCoordinatesyMin,
                                    UpLimit = recipeConfig.W255CenterCIE1931ChromaticCoordinatesyMax,
                                    Value = poi.y,
                                    TestValue = poi.y.ToString("F3")
                                };
                                ctx.ObjectiveTestResult.W255CenterCIE1976ChromaticCoordinatesu = new ObjectiveTestItem
                                {
                                    Name = "W255CenterCIE1976ChromaticCoordinatesu",
                                    LowLimit = recipeConfig.W255CenterCIE1976ChromaticCoordinatesuMin,
                                    UpLimit = recipeConfig.W255CenterCIE1976ChromaticCoordinatesuMax,
                                    Value = poi.u,
                                    TestValue = poi.u.ToString("F3")
                                };
                                ctx.ObjectiveTestResult.W255CenterCIE1976ChromaticCoordinatesv = new ObjectiveTestItem
                                {
                                    Name = "W255CenterCIE1976ChromaticCoordinatesv",
                                    LowLimit = recipeConfig.W255CenterCIE1976ChromaticCoordinatesvMin,
                                    UpLimit = recipeConfig.W255CenterCIE1976ChromaticCoordinatesvMax,
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
                                    LowLimit = recipeConfig.W255LuminanceUniformityMin,
                                    UpLimit = recipeConfig.W255LuminanceUniformityMax
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
                                    LowLimit = recipeConfig.W255ColorUniformityMin,
                                    UpLimit = recipeConfig.W255ColorUniformityMax
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

        public void Render (IProcessExecutionContext ctx)
        {
            foreach (var poiResultCIExyuvData in ctx.Result.ViewResultW25.PoiResultCIExyuvDatas)
            {
                var item = poiResultCIExyuvData.Point;
                switch (item.PointType)
                {
                    case POIPointTypes.Circle:
                        DVCircleText Circle = new DVCircleText();
                        Circle.Attribute.Center = new Point(item.PixelX, item.PixelY);
                        Circle.Attribute.Radius = item.Radius;
                        Circle.Attribute.Brush = Brushes.Transparent;
                        Circle.Attribute.Pen = new Pen(Brushes.Red, 1);
                        Circle.Attribute.Id = item.Id ?? -1;
                        Circle.Attribute.Text = item.Name;
                        Circle.Attribute.Msg = CVRawOpen.FormatMessage(CVCIEShowConfig.Instance.Template, poiResultCIExyuvData);
                        Circle.Render();
                        ctx.ImageView.AddVisual(Circle);
                        break;
                    case POIPointTypes.Rect:
                        DVRectangleText Rectangle = new DVRectangleText();
                        Rectangle.Attribute.Rect = new Rect(item.PixelX - item.Width / 2, item.PixelY - item.Height / 2, item.Width, item.Height);
                        Rectangle.Attribute.Brush = Brushes.Transparent;
                        Rectangle.Attribute.Pen = new Pen(Brushes.Red, 1);
                        Rectangle.Attribute.Id = item.Id ?? -1;
                        Rectangle.Attribute.Text = item.Name;
                        Rectangle.Attribute.Msg = CVRawOpen.FormatMessage(CVCIEShowConfig.Instance.Template, poiResultCIExyuvData);
                        Rectangle.Render();
                        ctx.ImageView.AddVisual(Rectangle);
                        break;
                    default:
                        break;
                }
            }

        }

        public string GenText(IProcessExecutionContext ctx)
        {
            var result = ctx.Result;
            string outtext = string.Empty;
            outtext += $"白画面九点圆 测试项：关注点算法+亮度均匀性+颜色均匀性算法+" + Environment.NewLine;

            if (result.ViewResultWhite.PoiResultCIExyuvDatas != null)
            {
                foreach (var item in result.ViewResultWhite.PoiResultCIExyuvDatas)
                {
                    outtext += $"X:{item.X.ToString("F2")} Y:{item.Y.ToString("F2")} Z:{item.Z.ToString("F2")} x:{item.x.ToString("F2")} y:{item.y.ToString("F2")} u:{item.u.ToString("F2")} v:{item.v.ToString("F2")} cct:{item.CCT.ToString("F2")} wave:{item.Wave.ToString("F2")}{Environment.NewLine}";
                }
            }

            outtext += $"CenterCorrelatedColorTemperature:{result.ViewResultWhite.CenterCorrelatedColorTemperature.TestValue}  LowLimit:{result.ViewResultWhite.CenterCorrelatedColorTemperature.LowLimit} UpLimit:{result.ViewResultWhite.CenterCorrelatedColorTemperature.UpLimit},Rsult{(result.ViewResultWhite.CenterCorrelatedColorTemperature.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            outtext += $"Luminance_uniformity:{result.ViewResultWhite.W255LuminanceUniformity.TestValue} LowLimit:{result.ViewResultWhite.W255LuminanceUniformity.LowLimit}  UpLimit:{result.ViewResultWhite.W255LuminanceUniformity.UpLimit},Rsult{(result.ViewResultWhite.W255LuminanceUniformity.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            outtext += $"Color_uniformity:{result.ViewResultWhite.W255ColorUniformity.TestValue} LowLimit:{result.ViewResultWhite.W255ColorUniformity.LowLimit} UpLimit:{result.ViewResultWhite.W255ColorUniformity.UpLimit},Rsult{(result.ViewResultWhite.W255ColorUniformity.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";

            return outtext;
        }
    }
}
