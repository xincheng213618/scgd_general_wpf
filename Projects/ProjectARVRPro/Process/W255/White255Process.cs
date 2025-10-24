using ColorVision.Database;
using ColorVision.Engine; // AlgResultMasterDao, MeasureImgResultDao, DeatilCommonDao
using ColorVision.Engine.Media;
using ColorVision.Engine.Templates.Jsons; // DetailCommonModel
using ColorVision.Engine.Templates.Jsons.FOV2;
using ColorVision.Engine.Templates.Jsons.PoiAnalysis; // PoiAnalysisDetailViewReslut
using ColorVision.Engine.Templates.POI.AlgorithmImp; // PoiPointResultModel
using ColorVision.ImageEditor.Draw;
using CVCommCore.CVAlgorithm;
using Newtonsoft.Json;
using System.Windows;
using System.Windows.Media;

namespace ProjectARVRPro.Process.W255
{
    public class White255Process : IProcess
    {
        public bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Logger;
            W255RecipeConfig recipeConfig = ctx.RecipeConfig.GetRequiredService<W255RecipeConfig>();
            W255FixConfig fixConfig = ctx.FixConfig.GetRequiredService<W255FixConfig>();
            W255ViewTestResult testResult = new W255ViewTestResult();

            try
            {
                var values = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                if (values.Count > 0)
                    ctx.Result.FileName = values[0].FileUrl;

                var masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                foreach (var master in masters)
                {
                    if (master.ImgFileType == ViewResultAlgType.POI_XYZ)
                    {
                        var poiPoints = PoiPointResultDao.Instance.GetAllByPid(master.Id);
                        int id = 0;
                        testResult.PoixyuvDatas.Clear();
                        foreach (var item in poiPoints)
                        {
                            var poi = new PoiResultCIExyuvData(item) { Id = id++ };
                            poi.CCT *= fixConfig.CenterCorrelatedColorTemperature;
                            poi.Y *= fixConfig.CenterLunimance;
                            poi.x *= fixConfig.CenterCIE1931ChromaticCoordinatesx;
                            poi.y *= fixConfig.CenterCIE1931ChromaticCoordinatesy;
                            poi.u *= fixConfig.CenterCIE1976ChromaticCoordinatesu;
                            poi.v *= fixConfig.CenterCIE1976ChromaticCoordinatesv;
                            testResult.PoixyuvDatas.Add(poi);

                            if (item.PoiName == "P_9")
                            {
                                testResult.CenterLunimance = new ObjectiveTestItem
                                {
                                    Name = "CenterLunimance",
                                    LowLimit = recipeConfig.CenterLunimance.Min,
                                    UpLimit = recipeConfig.CenterLunimance.Max,
                                    Value = poi.Y,
                                    TestValue = poi.Y.ToString("F3") + " nit"
                                };
                                testResult.CenterCIE1931ChromaticCoordinatesx = new ObjectiveTestItem
                                {
                                    Name = "CenterCIE1931ChromaticCoordinatesx",
                                    LowLimit = recipeConfig.CenterCIE1931ChromaticCoordinatesx.Min,
                                    UpLimit = recipeConfig.CenterCIE1931ChromaticCoordinatesx.Max,
                                    Value = poi.x,
                                    TestValue = poi.x.ToString("F3")
                                };
                                testResult.CenterCIE1931ChromaticCoordinatesy = new ObjectiveTestItem
                                {
                                    Name = "CenterCIE1931ChromaticCoordinatesy",
                                    LowLimit = recipeConfig.CenterCIE1931ChromaticCoordinatesy.Min,
                                    UpLimit = recipeConfig.CenterCIE1931ChromaticCoordinatesy.Max,
                                    Value = poi.y,
                                    TestValue = poi.y.ToString("F3")
                                };
                                testResult.CenterCIE1976ChromaticCoordinatesu = new ObjectiveTestItem
                                {
                                    Name = "CenterCIE1976ChromaticCoordinatesu",
                                    LowLimit = recipeConfig.CenterCIE1976ChromaticCoordinatesu.Min,
                                    UpLimit = recipeConfig.CenterCIE1976ChromaticCoordinatesu.Max,
                                    Value = poi.u,
                                    TestValue = poi.u.ToString("F3")
                                };
                                testResult.CenterCIE1976ChromaticCoordinatesv = new ObjectiveTestItem
                                {
                                    Name = "CenterCIE1976ChromaticCoordinatesv",
                                    LowLimit = recipeConfig.CenterCIE1976ChromaticCoordinatesv.Min,
                                    UpLimit = recipeConfig.CenterCIE1976ChromaticCoordinatesv.Max,
                                    Value = poi.v,
                                    TestValue = poi.v.ToString("F3")
                                };

                                ctx.Result.Result &= testResult.CenterLunimance.TestResult;
                                ctx.Result.Result &= testResult.CenterCIE1931ChromaticCoordinatesx.TestResult;
                                ctx.Result.Result &= testResult.CenterCIE1931ChromaticCoordinatesy.TestResult;
                                ctx.Result.Result &= testResult.CenterCIE1976ChromaticCoordinatesu.TestResult;
                                ctx.Result.Result &= testResult.CenterCIE1976ChromaticCoordinatesv.TestResult;
                            }
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
                                view.PoiAnalysisResult.result.Value *= fixConfig.LuminanceUniformity;
                                var uniform = new ObjectiveTestItem
                                {
                                    Name = "Luminance_uniformity(%)",
                                    TestValue = (view.PoiAnalysisResult.result.Value * 100).ToString("F3") + "%",
                                    Value = view.PoiAnalysisResult.result.Value,
                                    LowLimit = recipeConfig.LuminanceUniformity.Min,
                                    UpLimit = recipeConfig.LuminanceUniformity.Max
                                };
                                testResult.LuminanceUniformity = uniform;
                                ctx.Result.Result &= uniform.TestResult;
                            }
                        }
                        if (master.TName.Contains("Color_uniformity"))
                        {
                            var details = DeatilCommonDao.Instance.GetAllByPid(master.Id);
                            if (details.Count == 1)
                            {
                                var view = new PoiAnalysisDetailViewReslut(details[0]);
                                view.PoiAnalysisResult.result.Value *= fixConfig.ColorUniformity;
                                var colorUniform = new ObjectiveTestItem
                                {
                                    Name = "Color_uniformity",
                                    TestValue = view.PoiAnalysisResult.result.Value.ToString("F5"),
                                    Value = view.PoiAnalysisResult.result.Value,
                                    LowLimit = recipeConfig.ColorUniformity.Min,
                                    UpLimit = recipeConfig.ColorUniformity.Max
                                };
                                testResult.ColorUniformity = colorUniform;
                                ctx.Result.Result &= colorUniform.TestResult;
                            }
                        }
                    }

                    if (master.ImgFileType == ViewResultAlgType.FOV)
                    {
                        var algResultModels = DeatilCommonDao.Instance.GetAllByPid(master.Id);
                        if (algResultModels.Count == 1)
                        {
                            DFovView view1 = new DFovView(algResultModels[0]);


                            view1.Result.result.D_Fov = view1.Result.result.D_Fov * fixConfig.W51DiagonalFieldOfViewAngle;
                            view1.Result.result.ClolorVisionH_Fov = view1.Result.result.ClolorVisionH_Fov * fixConfig.W51HorizontalFieldOfViewAngle;
                            view1.Result.result.ClolorVisionV_Fov = view1.Result.result.ClolorVisionV_Fov * fixConfig.W51VerticalFieldOfViewAngle;


                            testResult.DiagonalFieldOfViewAngle.LowLimit = recipeConfig.DiagonalFieldOfViewAngle.Min;
                            testResult.DiagonalFieldOfViewAngle.UpLimit = recipeConfig.DiagonalFieldOfViewAngle.Max;
                            testResult.DiagonalFieldOfViewAngle.Value = view1.Result.result.D_Fov;
                            testResult.DiagonalFieldOfViewAngle.TestValue = view1.Result.result.D_Fov.ToString("F3");

                            testResult.HorizontalFieldOfViewAngle.LowLimit = recipeConfig.HorizontalFieldOfViewAngle.Min;
                            testResult.HorizontalFieldOfViewAngle.UpLimit = recipeConfig.HorizontalFieldOfViewAngle.Max;
                            testResult.HorizontalFieldOfViewAngle.Value = view1.Result.result.ClolorVisionH_Fov;
                            testResult.HorizontalFieldOfViewAngle.TestValue = view1.Result.result.ClolorVisionH_Fov.ToString("F3");

                            testResult.VerticalFieldOfViewAngle.LowLimit = recipeConfig.VerticalFieldOfViewAngle.Min;
                            testResult.VerticalFieldOfViewAngle.UpLimit = recipeConfig.VerticalFieldOfViewAngle.Max;
                            testResult.VerticalFieldOfViewAngle.Value = view1.Result.result.ClolorVisionV_Fov;
                            testResult.VerticalFieldOfViewAngle.TestValue = view1.Result.result.ClolorVisionV_Fov.ToString("F3");

                            ctx.Result.Result = ctx.Result.Result && testResult.DiagonalFieldOfViewAngle.TestResult;
                            ctx.Result.Result = ctx.Result.Result && testResult.HorizontalFieldOfViewAngle.TestResult;
                            ctx.Result.Result = ctx.Result.Result && testResult.VerticalFieldOfViewAngle.TestResult;

                        }

                    }
                }
                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);
                ctx.ObjectiveTestResult.W255TestResult = JsonConvert.DeserializeObject<W255TestResult>(ctx.Result.ViewResultJson) ?? new W255TestResult();

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
            W255ViewTestResult testResult = JsonConvert.DeserializeObject<W255ViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return;

            foreach (var poiResultCIExyuvData in testResult.PoixyuvDatas)
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
            outtext += $"W255 画面结果" + Environment.NewLine;

            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return outtext;
            W255ViewTestResult testResult = JsonConvert.DeserializeObject<W255ViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return outtext;

            foreach (var item in testResult.PoixyuvDatas)
            {
                outtext += $"X:{item.X.ToString("F2")} Y:{item.Y.ToString("F2")} Z:{item.Z.ToString("F2")} x:{item.x.ToString("F2")} y:{item.y.ToString("F2")} u:{item.u.ToString("F2")} v:{item.v.ToString("F2")} cct:{item.CCT.ToString("F2")} wave:{item.Wave.ToString("F2")}{Environment.NewLine}";
            }

            outtext += $"Luminance_uniformity:{testResult.LuminanceUniformity.TestValue} LowLimit:{testResult.LuminanceUniformity.LowLimit}  UpLimit:{testResult.LuminanceUniformity.UpLimit},Rsult{(testResult.LuminanceUniformity.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            outtext += $"Color_uniformity:{testResult.ColorUniformity.TestValue} LowLimit:{testResult.ColorUniformity.LowLimit} UpLimit:{testResult.ColorUniformity.UpLimit},Rsult{(testResult.ColorUniformity.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";


            outtext += $"DiagonalFieldOfViewAngle:{testResult.DiagonalFieldOfViewAngle.TestValue}  LowLimit:{testResult.DiagonalFieldOfViewAngle.LowLimit} UpLimit:{testResult.DiagonalFieldOfViewAngle.UpLimit},Rsult{(testResult.DiagonalFieldOfViewAngle.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            outtext += $"HorizontalFieldOfViewAngle:{testResult.HorizontalFieldOfViewAngle.TestValue} LowLimit:{testResult.HorizontalFieldOfViewAngle.LowLimit} UpLimit:{testResult.HorizontalFieldOfViewAngle.UpLimit} ,Rsult{(testResult.HorizontalFieldOfViewAngle.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            outtext += $"VerticalFieldOfViewAngle:{testResult.VerticalFieldOfViewAngle.TestValue} LowLimit:{testResult.VerticalFieldOfViewAngle.LowLimit} UpLimit:{testResult.VerticalFieldOfViewAngle.UpLimit},Rsult{(testResult.VerticalFieldOfViewAngle.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            return outtext;
        }
    }
}
