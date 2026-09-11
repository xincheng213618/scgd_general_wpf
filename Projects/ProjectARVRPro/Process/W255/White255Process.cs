#pragma warning disable CS8601,CS8602
using ColorVision.Common.Algorithms;
using ColorVision.Database;
using ColorVision.Engine; // AlgResultMasterDao, MeasureImgResultDao, DeatilCommonDao
using ColorVision.Engine.Media;
using ColorVision.Engine.Templates.FindLightArea;
using ColorVision.Engine.Templates.Jsons; // DetailCommonModel
using ColorVision.Engine.Templates.Jsons.FOV2;
using ColorVision.Engine.Templates.Jsons.PoiAnalysis; // PoiAnalysisDetailViewReslut
using ColorVision.Engine.Templates.POI.AlgorithmImp; // PoiPointResultModel
using ColorVision.ImageEditor.Draw;
using CVCommCore.CVAlgorithm;
using Newtonsoft.Json;
using ProjectARVRPro.Process.Uniformity;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace ProjectARVRPro.Process.W255
{
    public class White255Process : ProcessWithRecipeBase<W255ProcessConfig, W255RecipeConfig>
    {
        public override async Task<bool> Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Log;
            W255RecipeConfig recipeConfig = Config.RecipeConfig;
            W255ViewTestResult testResult = new W255ViewTestResult();

            try
            {
                var values = ctx.GetMeasureResults();
                bool calculateUniformityFromCorrectedPoi = Config.CalculateUniformityFromCorrectedPoi;
                string luminanceUniformityResultName = Config.GetLuminanceUniformityResultName();
                string colorUniformityResultName = Config.GetColorUniformityResultName();
                if (values.Count > 0)
                    ctx.Result.FileName = values[0].FileUrl;

                var masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                foreach (var master in masters)
                {
                    if (master.ImgFileType == ColorVision.Engine.ViewResultAlgType.FindLightArea)
                    {
                        testResult.AlgResultLightAreaModels = AlgResultLightAreaDao.Instance.GetAllByPid(master.Id);
                    }

                    if (master.ImgFileType == ViewResultAlgType.POI_XYZ)
                    {
                        if (File.Exists(master.ImgFile))
                        {
                            ctx.Result.FileName = master.ImgFile;
                        }
                        var poiPoints = PoiPointResultDao.Instance.GetAllByPid(master.Id);
                        if (poiPoints.Count == 0) continue;

                        int id = 0;
                        testResult.ViewPoixyuvDatas.Clear();
                        foreach (var item in poiPoints)
                        {
                            var poi = new PoiResultCIExyuvData(item) { Id = id++ };
                            poi.CCT = recipeConfig.CenterCorrelatedColorTemperature.Apply(poi.CCT);
                            poi.Y = recipeConfig.CenterLunimance.Apply(poi.Y);
                            poi.x = recipeConfig.CenterCIE1931ChromaticCoordinatesx.Apply(poi.x);
                            poi.y = recipeConfig.CenterCIE1931ChromaticCoordinatesy.Apply(poi.y);
                            poi.u = recipeConfig.CenterCIE1976ChromaticCoordinatesu.Apply(poi.u);
                            poi.v = recipeConfig.CenterCIE1976ChromaticCoordinatesv.Apply(poi.v);
                            testResult.ViewPoixyuvDatas.Add(poi);
                            testResult.PoixyuvDatas.Add(new PoixyuvData() { Id =poi.Id,Name =poi.Name,X =poi.X,Y=poi.Y,Z=poi.Z,x =poi.x,y =poi.y,u =poi.u,v =poi.v,CCT =poi.CCT,Wave =poi.Wave});
                            
                            if (item.PoiName == Config.Key_Center)
                            {
                                testResult.CenterLunimance = new ObjectiveTestItem
                                {
                                    Name = "CenterLunimance",  
                                    LowLimit = recipeConfig.CenterLunimance.Min,
                                    UpLimit = recipeConfig.CenterLunimance.Max,
                                    Value = poi.Y,
                                    TestValue = poi.Y.ToString("F4") + " nit"
                                };
                                testResult.CenterCIE1931ChromaticCoordinatesx = new ObjectiveTestItem
                                {
                                    Name = "CenterCIE1931ChromaticCoordinatesx",
                                    LowLimit = recipeConfig.CenterCIE1931ChromaticCoordinatesx.Min,
                                    UpLimit = recipeConfig.CenterCIE1931ChromaticCoordinatesx.Max,
                                    Value = poi.x,
                                    TestValue = poi.x.ToString("F4")
                                };
                                testResult.CenterCIE1931ChromaticCoordinatesy = new ObjectiveTestItem
                                {
                                    Name = "CenterCIE1931ChromaticCoordinatesy",
                                    LowLimit = recipeConfig.CenterCIE1931ChromaticCoordinatesy.Min,
                                    UpLimit = recipeConfig.CenterCIE1931ChromaticCoordinatesy.Max,
                                    Value = poi.y,
                                    TestValue = poi.y.ToString("F4")
                                };
                                testResult.CenterCIE1976ChromaticCoordinatesu = new ObjectiveTestItem
                                {
                                    Name = "CenterCIE1976ChromaticCoordinatesu",
                                    LowLimit = recipeConfig.CenterCIE1976ChromaticCoordinatesu.Min,
                                    UpLimit = recipeConfig.CenterCIE1976ChromaticCoordinatesu.Max,
                                    Value = poi.u,
                                    TestValue = poi.u.ToString("F4")
                                };
                                testResult.CenterCIE1976ChromaticCoordinatesv = new ObjectiveTestItem
                                {
                                    Name = "CenterCIE1976ChromaticCoordinatesv",
                                    LowLimit = recipeConfig.CenterCIE1976ChromaticCoordinatesv.Min,
                                    UpLimit = recipeConfig.CenterCIE1976ChromaticCoordinatesv.Max,
                                    Value = poi.v,
                                    TestValue = poi.v.ToString("F4")
                                };
                                testResult.CenterCorrelatedColorTemperature.Value = poi.CCT;
                                testResult.CenterCorrelatedColorTemperature.LowLimit = recipeConfig.CenterCorrelatedColorTemperature.Min;
                                testResult.CenterCorrelatedColorTemperature.UpLimit = recipeConfig.CenterCorrelatedColorTemperature.Max;

                                testResult.CenterCorrelatedColorTemperature.TestValue = testResult.CenterCorrelatedColorTemperature.Value.ToString("F4") + " K";

                                ctx.Result.Result &= testResult.CenterLunimance.TestResult;
                                ctx.Result.Result &= testResult.CenterCIE1931ChromaticCoordinatesx.TestResult;
                                ctx.Result.Result &= testResult.CenterCIE1931ChromaticCoordinatesy.TestResult;
                                ctx.Result.Result &= testResult.CenterCIE1976ChromaticCoordinatesu.TestResult;
                                ctx.Result.Result &= testResult.CenterCIE1976ChromaticCoordinatesv.TestResult;
                            }
                        }
                    }
                    if (master.ImgFileType == ViewResultAlgType.PoiAnalysis && !calculateUniformityFromCorrectedPoi)
                    {
                        bool isLuminanceUniformity = LuminanceChromaticityUniformityCalculator.MatchesResultName(master.TName, luminanceUniformityResultName);
                        bool isColorUniformity = LuminanceChromaticityUniformityCalculator.MatchesResultName(master.TName, colorUniformityResultName);
                        if (isLuminanceUniformity && isColorUniformity)
                            throw new InvalidOperationException($"同一个PoiAnalysis结果同时匹配亮度和色度均匀性TName: {master.TName}");

                        if (isLuminanceUniformity)
                        {
                            var details = DeatilCommonDao.Instance.GetAllByPid(master.Id);
                            if (details.Count == 1)
                            {
                                var view = new PoiAnalysisDetailViewReslut(details[0]);
                                SetLuminanceUniformity(ctx, testResult, recipeConfig, view.PoiAnalysisResult.result.Value);
                            }
                        }
                        else if (isColorUniformity)
                        {
                            var details = DeatilCommonDao.Instance.GetAllByPid(master.Id);
                            if (details.Count == 1)
                            {
                                var view = new PoiAnalysisDetailViewReslut(details[0]);
                                SetColorUniformity(ctx, testResult, recipeConfig, view.PoiAnalysisResult.result.Value);
                            }
                        }
                    }

                    if (master.ImgFileType == ViewResultAlgType.FOV)
                    {
                        var algResultModels = DeatilCommonDao.Instance.GetAllByPid(master.Id);
                        if (algResultModels.Count == 1)
                        {
                            DFovView view1 = new DFovView(algResultModels[0]);


                            view1.Result.result.D_Fov = recipeConfig.DiagonalFieldOfViewAngle.Apply(view1.Result.result.D_Fov);
                            view1.Result.result.ClolorVisionH_Fov = recipeConfig.HorizontalFieldOfViewAngle.Apply(view1.Result.result.ClolorVisionH_Fov);
                            view1.Result.result.ClolorVisionV_Fov = recipeConfig.VerticalFieldOfViewAngle.Apply(view1.Result.result.ClolorVisionV_Fov);


                            testResult.DiagonalFieldOfViewAngle.LowLimit = recipeConfig.DiagonalFieldOfViewAngle.Min;
                            testResult.DiagonalFieldOfViewAngle.UpLimit = recipeConfig.DiagonalFieldOfViewAngle.Max;
                            testResult.DiagonalFieldOfViewAngle.Value = view1.Result.result.D_Fov;
                            testResult.DiagonalFieldOfViewAngle.TestValue = view1.Result.result.D_Fov.ToString("F4");

                            testResult.HorizontalFieldOfViewAngle.LowLimit = recipeConfig.HorizontalFieldOfViewAngle.Min;
                            testResult.HorizontalFieldOfViewAngle.UpLimit = recipeConfig.HorizontalFieldOfViewAngle.Max;
                            testResult.HorizontalFieldOfViewAngle.Value = view1.Result.result.ClolorVisionH_Fov;
                            testResult.HorizontalFieldOfViewAngle.TestValue = view1.Result.result.ClolorVisionH_Fov.ToString("F4");

                            testResult.VerticalFieldOfViewAngle.LowLimit = recipeConfig.VerticalFieldOfViewAngle.Min;
                            testResult.VerticalFieldOfViewAngle.UpLimit = recipeConfig.VerticalFieldOfViewAngle.Max;
                            testResult.VerticalFieldOfViewAngle.Value = view1.Result.result.ClolorVisionV_Fov;
                            testResult.VerticalFieldOfViewAngle.TestValue = view1.Result.result.ClolorVisionV_Fov.ToString("F4");

                            ctx.Result.Result = ctx.Result.Result && testResult.DiagonalFieldOfViewAngle.TestResult;
                            ctx.Result.Result = ctx.Result.Result && testResult.HorizontalFieldOfViewAngle.TestResult;
                            ctx.Result.Result = ctx.Result.Result && testResult.VerticalFieldOfViewAngle.TestResult;

                        }

                    }
                }

                if (calculateUniformityFromCorrectedPoi)
                {
                    var calculation = LuminanceChromaticityUniformityCalculator.Calculate(testResult.ViewPoixyuvDatas);
                    if (!calculation.Success)
                    {
                        log?.Error($"W255本地均匀性计算失败: message={calculation.ErrorMessage}");
                        return false;
                    }

                    SetLuminanceUniformity(ctx, testResult, recipeConfig, calculation.LuminanceUniformity);
                    SetColorUniformity(ctx, testResult, recipeConfig, calculation.ColorUniformity);
                    log?.Info($"W255均匀性来源: corrected-poi, pointCount={calculation.PointCount}, luminance={testResult.LuminanceUniformity.Value:R}, color={testResult.ColorUniformity.Value:R}");
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

        private static void SetLuminanceUniformity(IProcessExecutionContext ctx, W255TestResult testResult, W255RecipeConfig recipeConfig, double value)
        {
            value = recipeConfig.LuminanceUniformity.Apply(value);
            testResult.LuminanceUniformity.LowLimit = recipeConfig.LuminanceUniformity.Min;
            testResult.LuminanceUniformity.UpLimit = recipeConfig.LuminanceUniformity.Max;
            testResult.LuminanceUniformity.Value = value;
            testResult.LuminanceUniformity.TestValue = (value * 100).ToString("F4") + "%";
            ctx.Result.Result &= testResult.LuminanceUniformity.TestResult;
        }

        private static void SetColorUniformity(IProcessExecutionContext ctx, W255TestResult testResult, W255RecipeConfig recipeConfig, double value)
        {
            value = recipeConfig.ColorUniformity.Apply(value);
            testResult.ColorUniformity.LowLimit = recipeConfig.ColorUniformity.Min;
            testResult.ColorUniformity.UpLimit = recipeConfig.ColorUniformity.Max;
            testResult.ColorUniformity.Value = value;
            testResult.ColorUniformity.TestValue = value.ToString("F5");
            ctx.Result.Result &= testResult.ColorUniformity.TestResult;
        }

        public override IReadOnlyList<ObjectiveTestCsvRow> GetObjectiveCsvRows(ProjectARVRReuslt result) =>
            GetObjectiveCsvRows<W255TestResult>(result, "W255");

        public override void Render (IProcessExecutionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return;
            W255ViewTestResult testResult = JsonConvert.DeserializeObject<W255ViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return;
            if (testResult.AlgResultLightAreaModels.Count > 0)
            {
                DVPolygon polygon = new DVPolygon();
                List<System.Windows.Point> point1s = new List<System.Windows.Point>();

                foreach (var item in testResult.AlgResultLightAreaModels)
                {
                    point1s.Add(new System.Windows.Point((int)item.PosX, (int)item.PosY));
                }
                foreach (var item in GrahamScan.ComputeConvexHull(point1s))
                {
                    polygon.Attribute.Points.Add(new Point(item.X, item.Y));
                }
                polygon.Attribute.Brush = Brushes.Transparent;
                polygon.Attribute.Pen = new Pen(Brushes.Blue, 1);
                polygon.Attribute.Id = -1;
                polygon.IsComple = true;
                polygon.Render();
                ctx.ImageView.AddVisual(polygon);
            }

            foreach (var poiResultCIExyuvData in testResult.ViewPoixyuvDatas)
            {
                var item = poiResultCIExyuvData.Point;
                PoiOverlayRenderer.Add(ctx.ImageView, item, CVRawOpen.FormatMessage(CVCIEShowConfig.Instance.Template, poiResultCIExyuvData));
            }

        }

        public override void GenText(IProcessExecutionContext ctx, System.Windows.Documents.Paragraph paragraph, System.Windows.Media.Brush foreground, double fontSize)
        {
            var result = ctx.Result;
            string outtext = string.Empty;
            outtext += $"W255 画面结果" + Environment.NewLine;

            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) { AppendPlainText(paragraph, outtext, foreground, fontSize); return; }
            W255ViewTestResult testResult = JsonConvert.DeserializeObject<W255ViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) { AppendPlainText(paragraph, outtext, foreground, fontSize); return; }



            foreach (var item in testResult.ViewPoixyuvDatas)
            {
                outtext += $"X:{item.X.ToString("F2")} Y:{item.Y.ToString("F2")} Z:{item.Z.ToString("F2")} x:{item.x.ToString("F2")} y:{item.y.ToString("F2")} u:{item.u.ToString("F2")} v:{item.v.ToString("F2")} cct:{item.CCT.ToString("F2")} wave:{item.Wave.ToString("F2")}{Environment.NewLine}";
            }

            outtext += $"Luminance_uniformity:{testResult.LuminanceUniformity.TestValue} LowLimit:{testResult.LuminanceUniformity.LowLimit}  UpLimit:{testResult.LuminanceUniformity.UpLimit},Rsult{(testResult.LuminanceUniformity.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            outtext += $"Color_uniformity:{testResult.ColorUniformity.TestValue} LowLimit:{testResult.ColorUniformity.LowLimit} UpLimit:{testResult.ColorUniformity.UpLimit},Rsult{(testResult.ColorUniformity.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            outtext += $"CenterCorrelatedColorTemperature:{testResult.CenterCorrelatedColorTemperature.TestValue} LowLimit:{testResult.CenterCorrelatedColorTemperature.LowLimit} UpLimit:{testResult.CenterCorrelatedColorTemperature.UpLimit},Rsult{(testResult.CenterCorrelatedColorTemperature.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";

            outtext += $"HorizontalFieldOfViewAngle:{testResult.HorizontalFieldOfViewAngle.TestValue} LowLimit:{testResult.HorizontalFieldOfViewAngle.LowLimit} UpLimit:{testResult.HorizontalFieldOfViewAngle.UpLimit} ,Rsult{(testResult.HorizontalFieldOfViewAngle.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            outtext += $"VerticalFieldOfViewAngle:{testResult.VerticalFieldOfViewAngle.TestValue} LowLimit:{testResult.VerticalFieldOfViewAngle.LowLimit} UpLimit:{testResult.VerticalFieldOfViewAngle.UpLimit},Rsult{(testResult.VerticalFieldOfViewAngle.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            outtext += $"DiagonalFieldOfViewAngle:{testResult.DiagonalFieldOfViewAngle.TestValue}  LowLimit:{testResult.DiagonalFieldOfViewAngle.LowLimit} UpLimit:{testResult.DiagonalFieldOfViewAngle.UpLimit},Rsult{(testResult.DiagonalFieldOfViewAngle.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            AppendPlainText(paragraph, outtext, foreground, fontSize); return;
        }

    }
}
