using ColorVision.Database;
using ColorVision.Engine; // AlgResultMasterDao, MeasureImgResultDao, DeatilCommonDao
using ColorVision.Engine.Media;
using ColorVision.Engine.Templates.Jsons; // DetailCommonModel
using ColorVision.Engine.Templates.Jsons.PoiAnalysis; // PoiAnalysisDetailViewReslut
using ColorVision.Engine.Templates.POI.AlgorithmImp; // PoiPointResultModel
using ColorVision.ImageEditor.Draw;
using CVCommCore.CVAlgorithm;
using Newtonsoft.Json;
using ProjectARVRPro.Fix;
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
            RedRecipeConfig recipeConfig = ctx.RecipeConfig.GetRequiredService<RedRecipeConfig>();
            RedFixConfig fixConfig = ctx.FixConfig.GetRequiredService<RedFixConfig>();
            RedViewTestResult redTestResult = new RedViewTestResult();

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
                        redTestResult.PoixyuvDatas.Clear();
                        foreach (var item in poiPoints)
                        {
                            var poi = new PoiResultCIExyuvData(item) { Id = id++ };
                            poi.CCT *= fixConfig.BlackCenterCorrelatedColorTemperature;
                            poi.Y *= fixConfig.CenterLunimance;
                            poi.x *= fixConfig.CenterCIE1931ChromaticCoordinatesx;
                            poi.y *= fixConfig.CenterCIE1931ChromaticCoordinatesy;
                            poi.u *= fixConfig.CenterCIE1976ChromaticCoordinatesu;
                            poi.v *= fixConfig.CenterCIE1976ChromaticCoordinatesv;

                            redTestResult.PoixyuvDatas.Add(poi);

                            if (item.PoiName == "P_9")
                            {
                                redTestResult.CenterLunimance = new ObjectiveTestItem
                                {
                                    Name = "CenterLunimance",
                                    LowLimit = recipeConfig.CenterLunimance.Min,
                                    UpLimit = recipeConfig.CenterLunimance.Max,
                                    Value = poi.Y,
                                    TestValue = poi.Y.ToString("F4") + " nit"
                                };
                                redTestResult.CenterCIE1931ChromaticCoordinatesx = new ObjectiveTestItem
                                {
                                    Name = "CenterCIE1931ChromaticCoordinatesx",
                                    LowLimit = recipeConfig.CenterCIE1931ChromaticCoordinatesx.Min,
                                    UpLimit = recipeConfig.CenterCIE1931ChromaticCoordinatesx.Max,
                                    Value = poi.x,
                                    TestValue = poi.x.ToString("F4")
                                };
                                redTestResult.CenterCIE1931ChromaticCoordinatesy = new ObjectiveTestItem
                                {
                                    Name = "CenterCIE1931ChromaticCoordinatesy",
                                    LowLimit = recipeConfig.CenterCIE1931ChromaticCoordinatesy.Min,
                                    UpLimit = recipeConfig.CenterCIE1931ChromaticCoordinatesy.Max,
                                    Value = poi.y,
                                    TestValue = poi.y.ToString("F4")
                                };
                                redTestResult.CenterCIE1976ChromaticCoordinatesu = new ObjectiveTestItem
                                {
                                    Name = "CenterCIE1976ChromaticCoordinatesu",
                                    LowLimit = recipeConfig.CenterCIE1976ChromaticCoordinatesu.Min,
                                    UpLimit = recipeConfig.CenterCIE1976ChromaticCoordinatesu.Max,
                                    Value = poi.u,
                                    TestValue = poi.u.ToString("F4")
                                };
                                redTestResult.CenterCIE1976ChromaticCoordinatesv = new ObjectiveTestItem
                                {
                                    Name = "CenterCIE1976ChromaticCoordinatesv",
                                    LowLimit = recipeConfig.CenterCIE1976ChromaticCoordinatesv.Min,
                                    UpLimit = recipeConfig.CenterCIE1976ChromaticCoordinatesv.Max,
                                    Value = poi.v,
                                    TestValue = poi.v.ToString("F4")
                                };

                                ctx.Result.Result &= redTestResult.CenterLunimance.TestResult;
                                ctx.Result.Result &= redTestResult.CenterCIE1931ChromaticCoordinatesx.TestResult;
                                ctx.Result.Result &= redTestResult.CenterCIE1931ChromaticCoordinatesy.TestResult;
                                ctx.Result.Result &= redTestResult.CenterCIE1976ChromaticCoordinatesu.TestResult;
                                ctx.Result.Result &= redTestResult.CenterCIE1976ChromaticCoordinatesv.TestResult;
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
                                    TestValue = (view.PoiAnalysisResult.result.Value * 100).ToString("F4") + "%",
                                    Value = view.PoiAnalysisResult.result.Value,
                                    LowLimit = recipeConfig.LuminanceUniformity.Min,
                                    UpLimit = recipeConfig.LuminanceUniformity.Max
                                };
                                redTestResult.LuminanceUniformity = uniform;
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
                                redTestResult.ColorUniformity = colorUniform;
                                ctx.Result.Result &= colorUniform.TestResult;
                            }
                        }
                    }
                }

                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(redTestResult);
                ctx.ObjectiveTestResult.RedTestResult = JsonConvert.DeserializeObject<RedTestResult>(ctx.Result.ViewResultJson) ?? new RedTestResult();
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
            RedViewTestResult redTestResult = JsonConvert.DeserializeObject<RedViewTestResult>(ctx.Result.ViewResultJson);
            if (redTestResult == null) return;
            foreach (var poiResultCIExyuvData in redTestResult.PoixyuvDatas)
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

            string outtext = string.Empty;
            outtext += $"Red ������" + Environment.NewLine;

            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return outtext;
            RedViewTestResult redTestResult = JsonConvert.DeserializeObject<RedViewTestResult>(ctx.Result.ViewResultJson);
            if (redTestResult == null) return outtext;

            foreach (var item in redTestResult.PoixyuvDatas)
            {
                outtext += $"X:{item.X.ToString("F2")} Y:{item.Y.ToString("F2")} Z:{item.Z.ToString("F2")} x:{item.x.ToString("F2")} y:{item.y.ToString("F2")} u:{item.u.ToString("F2")} v:{item.v.ToString("F2")} cct:{item.CCT.ToString("F2")} wave:{item.Wave.ToString("F2")}{Environment.NewLine}";
            }

            outtext += $"Luminance_uniformity:{redTestResult.LuminanceUniformity.TestValue} LowLimit:{redTestResult.LuminanceUniformity.LowLimit}  UpLimit:{redTestResult.LuminanceUniformity.UpLimit},Rsult{(redTestResult.LuminanceUniformity.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            outtext += $"Color_uniformity:{redTestResult.ColorUniformity.TestValue} LowLimit:{redTestResult.ColorUniformity.LowLimit} UpLimit:{redTestResult.ColorUniformity.UpLimit},Rsult{(redTestResult.ColorUniformity.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";

            return outtext;
        }

        public IRecipeConfig GetRecipeConfig()
        {
            return RecipeManager.GetInstance().RecipeConfig.GetRequiredService<RedRecipeConfig>();
        }

        public IFixConfig GetFixConfig()
        {
            return FixManager.GetInstance().FixConfig.GetRequiredService<RedFixConfig>();
        }
    }
}
