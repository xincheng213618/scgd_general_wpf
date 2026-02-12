using ColorVision.Database;
using ColorVision.Engine; // DAOs
using ColorVision.Engine.Media;
using ColorVision.Engine.Templates.Jsons; // DetailCommonModel
using ColorVision.Engine.Templates.Jsons.PoiAnalysis; // PoiAnalysisDetailViewReslut
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor.Draw;
using CVCommCore.CVAlgorithm;
using Newtonsoft.Json;
using ProjectLUX.Fix;
using ProjectLUX.Process.W255;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ProjectLUX.Process.Chessboard55
{
    public class Chessboard55ProcessConfig : ProcessConfigBase
    {
        [Category("解析配置")]
        [DisplayName("ChessboardContrastTempName")]
        [Description("Chessboard_Contrast")]
        public string ChessboardContrastTempName { get => _ChessboardContrastTempName; set { _ChessboardContrastTempName = value; OnPropertyChanged(); } }
        private string _ChessboardContrastTempName = "Chessboard_Contrast";
    }

    public class Chessboard55Process : ProcessBase<Chessboard55ProcessConfig>
    {
        public override bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Logger;
            Chessboard55RecipeConfig recipeConfig = ctx.RecipeConfig.GetRequiredService<Chessboard55RecipeConfig>();
            Chessboard55FixConfig fixConfig = ctx.FixConfig.GetRequiredService<Chessboard55FixConfig>();
            ChessboardViewTestResult testResult = new ChessboardViewTestResult();


            try
            {
                log?.Info("处理 Chessboard 流程结果");

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

                        if (poiPoints != null && poiPoints.Count >= 25)
                        {
                            var lvItems = new[]
                            {
        testResult.P1Lv,  testResult.P2Lv,  testResult.P3Lv,  testResult.P4Lv,  testResult.P5Lv,
        testResult.P6Lv,  testResult.P7Lv,  testResult.P8Lv,  testResult.P9Lv,  testResult.P10Lv,
        testResult.P11Lv, testResult.P12Lv, testResult.P13Lv, testResult.P14Lv, testResult.P15Lv,
        testResult.P16Lv, testResult.P17Lv, testResult.P18Lv, testResult.P19Lv, testResult.P20Lv,
        testResult.P21Lv, testResult.P22Lv, testResult.P23Lv, testResult.P24Lv, testResult.P25Lv
    };

                            var lvFixes = new[]
                            {
        fixConfig.P1Lv,  fixConfig.P2Lv,  fixConfig.P3Lv,  fixConfig.P4Lv,  fixConfig.P5Lv,
        fixConfig.P6Lv,  fixConfig.P7Lv,  fixConfig.P8Lv,  fixConfig.P9Lv,  fixConfig.P10Lv,
        fixConfig.P11Lv, fixConfig.P12Lv, fixConfig.P13Lv, fixConfig.P14Lv, fixConfig.P15Lv,
        fixConfig.P16Lv, fixConfig.P17Lv, fixConfig.P18Lv, fixConfig.P19Lv, fixConfig.P20Lv,
        fixConfig.P21Lv, fixConfig.P22Lv, fixConfig.P23Lv, fixConfig.P24Lv, fixConfig.P25Lv
    };

                            var lvRecipes = new[]
                            {
        recipeConfig.P1Lv,  recipeConfig.P2Lv,  recipeConfig.P3Lv,  recipeConfig.P4Lv,  recipeConfig.P5Lv,
        recipeConfig.P6Lv,  recipeConfig.P7Lv,  recipeConfig.P8Lv,  recipeConfig.P9Lv,  recipeConfig.P10Lv,
        recipeConfig.P11Lv, recipeConfig.P12Lv, recipeConfig.P13Lv, recipeConfig.P14Lv, recipeConfig.P15Lv,
        recipeConfig.P16Lv, recipeConfig.P17Lv, recipeConfig.P18Lv, recipeConfig.P19Lv, recipeConfig.P20Lv,
        recipeConfig.P21Lv, recipeConfig.P22Lv, recipeConfig.P23Lv, recipeConfig.P24Lv, recipeConfig.P25Lv
    };

                            for (int i = 0; i < 25; i++)
                            {
                                var poi = new PoiResultCIExyuvData(poiPoints[i]);
                                var item = lvItems[i];
                                var fix = lvFixes[i];
                                var rc = lvRecipes[i];

                                item.Value = poi.Y;
                                item.Value *= fix;
                                item.LowLimit = rc.Min;
                                item.UpLimit = rc.Max;
                                item.TestValue = item.Value.ToString();

                                ctx.Result.Result &= item.TestResult;
                            }
                        }

                        var luminanceValues = new List<double>();
                        for (int i = 0; i < 25; i++)
                        {
                            var poi = new PoiResultCIExyuvData(poiPoints[i]);
                            luminanceValues.Add(poi.Y);
                        }
                        // Sort the luminance values in ascending order
                        luminanceValues.Sort();

                        // The first 8 values are the dimmest (black)
                        double averageBlackLuminance = luminanceValues.Take(12).Average();

                        // The last 8 values are the brightest (white)
                        double averageWhiteLuminance = luminanceValues.Skip(13).Take(12).Average();

                        testResult.AverageWhiteLunimance.Value = averageWhiteLuminance;
                        testResult.AverageWhiteLunimance.Value *= fixConfig.AverageWhiteLunimance;
                        testResult.AverageWhiteLunimance.TestValue = testResult.AverageWhiteLunimance.Value.ToString("F3");
                        testResult.AverageWhiteLunimance.LowLimit *= recipeConfig.AverageWhiteLunimance.Min;
                        testResult.AverageWhiteLunimance.UpLimit *= recipeConfig.AverageWhiteLunimance.Max;
                        ctx.Result.Result &= testResult.AverageWhiteLunimance.TestResult;

                        testResult.AverageBlackLunimance.Value = averageBlackLuminance;
                        testResult.AverageBlackLunimance.Value *= fixConfig.AverageBlackLunimance;
                        testResult.AverageBlackLunimance.TestValue = testResult.AverageBlackLunimance.Value.ToString("F3");
                        testResult.AverageBlackLunimance.LowLimit *= recipeConfig.AverageBlackLunimance.Min;
                        testResult.AverageBlackLunimance.UpLimit *= recipeConfig.AverageBlackLunimance.Max;
                        ctx.Result.Result &= testResult.AverageBlackLunimance.TestResult;

                    }

                    if (master.ImgFileType == ViewResultAlgType.PoiAnalysis && master.TName.Contains(Config.ChessboardContrastTempName))
                    {
                        var details = DeatilCommonDao.Instance.GetAllByPid(master.Id);
                        if (details.Count == 1)
                        {
                            var view = new PoiAnalysisDetailViewReslut(details[0]);

                            testResult.ChessboardContrast.Value = view.PoiAnalysisResult.result.Value;
                            testResult.ChessboardContrast.Value *= fixConfig.ChessboardContrast;
                            testResult.ChessboardContrast.TestValue = testResult.ChessboardContrast.Value.ToString("F3");
                            testResult.ChessboardContrast.LowLimit *= recipeConfig.ChessboardContrast.Min;
                            testResult.ChessboardContrast.UpLimit *= recipeConfig.ChessboardContrast.Max;
                            ctx.Result.Result &= testResult.ChessboardContrast.TestResult;
                        }
                    }
                }

                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);
                ctx.ObjectiveTestResult.Chessboard55TestResult = JsonConvert.DeserializeObject<Chessboard55TestResult>(ctx.Result.ViewResultJson) ?? new Chessboard55TestResult();
                return true;
            }
            catch (Exception ex)
            {
                log?.Error(ex);
                return false;
            }
        }

        public override void Render(IProcessExecutionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return;
            ChessboardViewTestResult testResult = JsonConvert.DeserializeObject<ChessboardViewTestResult>(ctx.Result.ViewResultJson);
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
                        Circle.Attribute.Msg = CVRawOpen.FormatMessage("Y:@Y:F2", poiResultCIExyuvData);
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
                        Rectangle.Attribute.Msg = CVRawOpen.FormatMessage("Y:@Y:F2", poiResultCIExyuvData);
                        Rectangle.Render();
                        ctx.ImageView.AddVisual(Rectangle);
                        break;
                    default:
                        break;
                }
            }
        }

        public override string GenText(IProcessExecutionContext ctx)
        {
            var result = ctx.Result;
            string outtext = string.Empty;
            outtext += $"棋盘格 测试项：" + Environment.NewLine;

            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return outtext;
            ChessboardViewTestResult testResult = JsonConvert.DeserializeObject<ChessboardViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return outtext;

            foreach (var item in testResult.PoixyuvDatas)
            {
                outtext += $"{item.Name}  Y:{item.Y.ToString("F2")}{Environment.NewLine}";
            }

            outtext += $"ChessboardContrast:{testResult.ChessboardContrast.TestValue} LowLimit:{testResult.ChessboardContrast.LowLimit}  UpLimit:{testResult.ChessboardContrast.UpLimit},Rsult{(testResult.ChessboardContrast.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            return outtext;
        }


        public override IRecipeConfig GetRecipeConfig()
        {
            return RecipeManager.GetInstance().RecipeConfig.GetRequiredService<Chessboard55RecipeConfig>();
        }

        public override IFixConfig GetFixConfig()
        {
            return FixManager.GetInstance().FixConfig.GetRequiredService<Chessboard55FixConfig>();
        }
    }
}
