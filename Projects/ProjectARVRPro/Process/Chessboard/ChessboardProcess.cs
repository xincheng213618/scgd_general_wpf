#pragma warning disable CS8601,CS8602
using ColorVision.Database;
using ColorVision.Engine; // DAOs
using ColorVision.Engine.Media;
using ColorVision.Engine.Templates.Jsons; // DetailCommonModel
using ColorVision.Engine.Templates.Jsons.PoiAnalysis; // PoiAnalysisDetailViewReslut
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor.Draw;
using CVCommCore.CVAlgorithm;
using Newtonsoft.Json;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ProjectARVRPro.Process.Chessboard
{
    public class ChessboardProcess : ProcessBase<ChessboardProcessConfig, ChessboardRecipeConfig>
    {
        public override async Task<bool> Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Log;
            ChessboardRecipeConfig recipeConfig = ctx.RecipeConfig.GetRequiredService<ChessboardRecipeConfig>();
            ChessboardViewTestResult testResult = new ChessboardViewTestResult();
            string contrastResultName = string.IsNullOrWhiteSpace(Config.ChessboardContrastResultName)
                ? "Chessboard_Contrast"
                : Config.ChessboardContrastResultName.Trim();
            bool databaseContrastFound = false;
            bool databaseContrastLoaded = false;
            ChessboardContrastCalculationResult? calculation = null;


            try
            {
                log?.Info("开始 Chessboard 流程解析");

                var values = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                if (values.Count > 0)
                    ctx.Result.FileName = values[0].FileUrl;

                var masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                foreach (var master in masters)
                {
                    if (master.ImgFileType == ViewResultAlgType.POI_XYZ)
                    {
                        ctx.Result.FileName = master.ImgFile;
                        var poiPoints = PoiPointResultDao.Instance.GetAllByPid(master.Id);
                        int id = 0;
                        foreach (var item in poiPoints)
                        {
                            var poi = new PoiResultCIExyuvData(item) { Id = id++ };
                            testResult.PoixyuvDatas.Add(poi);
                        }
                    }

                    if (master.ImgFileType == ViewResultAlgType.PoiAnalysis && master.TName?.Contains(contrastResultName, StringComparison.Ordinal) == true)
                    {
                        databaseContrastFound = true;
                        var details = DeatilCommonDao.Instance.GetAllByPid(master.Id);
                        if (details.Count == 1)
                        {
                            var view = new PoiAnalysisDetailViewReslut(details[0]);
                            view.PoiAnalysisResult.result.Value = recipeConfig.ChessboardContrast.Apply(view.PoiAnalysisResult.result.Value);
                            var contrast = new ObjectiveTestItem
                            {
                                Name = contrastResultName,
                                LowLimit = recipeConfig.ChessboardContrast.Min,
                                UpLimit = recipeConfig.ChessboardContrast.Max,
                                Value = view.PoiAnalysisResult.result.Value,
                                TestValue = view.PoiAnalysisResult.result.Value.ToString("F3")
                            };
                            testResult.ChessboardContrast = contrast;
                            ctx.Result.Result &= contrast.TestResult;
                            databaseContrastLoaded = true;
                            log?.Info($"Chessboard对比度来源: database, resultName={contrastResultName}");
                        }
                        else
                            log?.Error($"Chessboard数据库结果明细无效: resultName={contrastResultName}, detailCount={details.Count}");
                    }
                }

                if (databaseContrastFound && !databaseContrastLoaded)
                    return false;

                if (!databaseContrastFound || Config.SaveCsv)
                {
                    calculation = ChessboardContrastCalculator.Calculate(
                        testResult.PoixyuvDatas,
                        Config.RowCount,
                        Config.ColumnCount,
                        Config.FirstPointColor,
                        Config.StrayLightCoefficient,
                        Config.AllowNegativeCorrectedDarkLuminance);
                    if (!calculation.Success)
                    {
                        if (!databaseContrastFound)
                        {
                            log?.Error($"Chessboard本地计算失败: resultName={contrastResultName}, message={calculation.ErrorMessage}");
                            return false;
                        }

                        log?.Warn($"Chessboard本地CSV分析失败，保留数据库对比度并导出基础POI数据: resultName={contrastResultName}, message={calculation.ErrorMessage}");
                    }
                }

                if (!databaseContrastFound && calculation?.Success == true)
                {
                    double correctedContrast = recipeConfig.ChessboardContrast.Apply(calculation.CorrectedContrast);
                    testResult.AverageBlackLuminance = new ObjectiveTestItem
                    {
                        Name = "Average_Black_Luminance",
                        Unit = "cd/m^2",
                        Value = calculation.CorrectedDarkLuminance,
                        TestValue = calculation.CorrectedDarkLuminance.ToString("F3")
                    };
                    testResult.ChessboardContrast = new ObjectiveTestItem
                    {
                        Name = contrastResultName,
                        LowLimit = recipeConfig.ChessboardContrast.Min,
                        UpLimit = recipeConfig.ChessboardContrast.Max,
                        Value = correctedContrast,
                        TestValue = correctedContrast.ToString("F3")
                    };
                    ctx.Result.Result &= testResult.ChessboardContrast.TestResult;

                    log?.Info(string.Format(
                        CultureInfo.InvariantCulture,
                        "Chessboard对比度来源: local, resultName={0}, rows={1}, columns={2}, requestedFirstPointColor={3}, resolvedFirstPointColor={4}, a={5}, LB={6}, LD={7}, LD'={8}, CR'={9}",
                        contrastResultName,
                        calculation.RowCount,
                        calculation.ColumnCount,
                        calculation.RequestedFirstPointColor,
                        calculation.ResolvedFirstPointColor,
                        calculation.StrayLightCoefficient,
                        calculation.BrightLuminance,
                        calculation.RawDarkLuminance,
                        calculation.CorrectedDarkLuminance,
                        calculation.CorrectedContrast));
                }

                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);
                ctx.ObjectiveTestResult.ChessboardTestResult = JsonConvert.DeserializeObject<ChessboardTestResult>(ctx.Result.ViewResultJson) ?? new ChessboardTestResult();
                if (Config.SaveCsv)
                {
                    ChessboardCsvExporter.SavePoixyuvDatas(
                        testResult.PoixyuvDatas,
                        ctx,
                        "Chessboard",
                        calculation,
                        testResult.ChessboardContrast?.Value,
                        contrastResultName,
                        databaseContrastFound ? "database" : "local");
                }
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

        public override void GenText(IProcessExecutionContext ctx, System.Windows.Documents.Paragraph paragraph, System.Windows.Media.Brush foreground, double fontSize)
        {
            var result = ctx.Result;
            string outtext = string.Empty;
            outtext += "棋盘格结果数据：" + Environment.NewLine;

            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) { AppendPlainText(paragraph, outtext, foreground, fontSize); return; }
            ChessboardViewTestResult testResult = JsonConvert.DeserializeObject<ChessboardViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) { AppendPlainText(paragraph, outtext, foreground, fontSize); return; }

            foreach (var item in testResult.PoixyuvDatas)
            {
                outtext += $"{item.Name}  Y:{item.Y.ToString("F2")}{Environment.NewLine}";
            }

            if (testResult.AverageBlackLuminance != null)
                outtext += $"AverageBlackLuminance:{testResult.AverageBlackLuminance.TestValue}{testResult.AverageBlackLuminance.Unit}{Environment.NewLine}";

            if (testResult.ChessboardContrast != null)
                outtext += $"ChessboardContrast:{testResult.ChessboardContrast.TestValue} LowLimit:{testResult.ChessboardContrast.LowLimit}  UpLimit:{testResult.ChessboardContrast.UpLimit},Rsult{(testResult.ChessboardContrast.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            AppendPlainText(paragraph, outtext, foreground, fontSize); return;
        }

    }
}
