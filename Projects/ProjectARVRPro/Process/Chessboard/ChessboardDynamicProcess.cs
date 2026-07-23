using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Media;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.PoiAnalysis;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor.Draw;
using CVCommCore.CVAlgorithm;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace ProjectARVRPro.Process.Chessboard
{
    public class ChessboardDynamicProcess : ProcessBase<ChessboardDynamicProcessConfig, ChessboardRecipeConfig>
    {
        public override async Task<bool> Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Log;
            ChessboardRecipeConfig recipeConfig = ctx.RecipeConfig.GetRequiredService<ChessboardRecipeConfig>();
            ChessboardDynamicViewTestResult testResult = new ChessboardDynamicViewTestResult();
            ChessboardViewTestResult chessboardResult = testResult.ChessboardViewTestResult;
            string contrastResultName = string.IsNullOrWhiteSpace(Config.ChessboardContrastResultName)
                ? "Chessboard_Contrast"
                : Config.ChessboardContrastResultName.Trim();
            bool databaseContrastFound = false;
            bool databaseContrastLoaded = false;
            ChessboardContrastCalculationResult? calculation = null;

            try
            {
                log?.Info("开始 Chessboard Dynamic 流程解析");

                var values = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                if (values.Count > 0)
                {
                    string? fileUrl = values[0].FileUrl;
                    if (!string.IsNullOrWhiteSpace(fileUrl))
                        ctx.Result.FileName = fileUrl;
                }

                var masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                foreach (var master in masters)
                {
                    if (master.ImgFileType == ViewResultAlgType.POI_XYZ)
                    {
                        var poiPoints = PoiPointResultDao.Instance.GetAllByPid(master.Id);
                        int id = 0;
                        foreach (var item in poiPoints)
                        {
                            var poi = new PoiResultCIExyuvData(item) { Id = id++ };
                            chessboardResult.PoixyuvDatas.Add(poi);
                        }
                    }

                    if (master.ImgFileType == ViewResultAlgType.BuildPOI)
                    {
                        var poiPoints = PoiPointResultDao.Instance.GetAllByPid(master.Id);
                        int id = 0;
                        foreach (var item in poiPoints)
                        {
                            var poi = new PoiResultCIExyuvData(item) { Id = id++ };
                            chessboardResult.PoixyuvDatas.Add(poi);
                        }
                    }

                    if (master.ImgFileType == ViewResultAlgType.PoiAnalysis && master.TName?.Contains(contrastResultName, StringComparison.Ordinal) == true)
                    {
                        databaseContrastFound = true;
                        var details = DeatilCommonDao.Instance.GetAllByPid(master.Id);
                        if (details.Count == 1)
                        {
                            var view = new PoiAnalysisDetailViewReslut(details[0]);
                            if (view.PoiAnalysisResult?.result == null)
                            {
                                log?.Error($"Chessboard Dynamic数据库结果内容无效: resultName={contrastResultName}");
                                continue;
                            }

                            view.PoiAnalysisResult.result.Value = recipeConfig.ChessboardContrast.Apply(view.PoiAnalysisResult.result.Value);
                            chessboardResult.ChessboardContrast = Build(
                                contrastResultName,
                                view.PoiAnalysisResult.result.Value,
                                recipeConfig.ChessboardContrast.Min,
                                recipeConfig.ChessboardContrast.Max);
                            ctx.Result.Result &= chessboardResult.ChessboardContrast.TestResult;
                            databaseContrastLoaded = true;
                            log?.Info($"Chessboard Dynamic对比度来源: database, resultName={contrastResultName}");
                        }
                        else
                            log?.Error($"Chessboard Dynamic数据库结果明细无效: resultName={contrastResultName}, detailCount={details.Count}");
                    }
                }

                if (databaseContrastFound && !databaseContrastLoaded)
                    return false;

                if (!databaseContrastFound || Config.SaveCsv)
                {
                    calculation = ChessboardContrastCalculator.Calculate(
                        chessboardResult.PoixyuvDatas,
                        Config.RowCount,
                        Config.ColumnCount,
                        Config.FirstPointColor,
                        Config.StrayLightCoefficient,
                        Config.AllowNegativeCorrectedDarkLuminance);
                    if (!calculation.Success)
                    {
                        if (!databaseContrastFound)
                        {
                            log?.Error($"Chessboard Dynamic本地计算失败: resultName={contrastResultName}, message={calculation.ErrorMessage}");
                            return false;
                        }

                        log?.Warn($"Chessboard Dynamic本地CSV分析失败，保留数据库对比度并导出基础POI数据: resultName={contrastResultName}, message={calculation.ErrorMessage}");
                    }
                }

                if (!databaseContrastFound && calculation?.Success == true)
                {
                    chessboardResult.AverageBlackLuminance = new ObjectiveTestItem
                    {
                        Name = "Average_Black_Luminance",
                        Unit = "cd/m^2",
                        Value = calculation.CorrectedDarkLuminance,
                        TestValue = calculation.CorrectedDarkLuminance.ToString(Config.ShowConfig)
                    };
                    double correctedContrast = recipeConfig.ChessboardContrast.Apply(calculation.CorrectedContrast);
                    chessboardResult.ChessboardContrast = Build(
                        contrastResultName,
                        correctedContrast,
                        recipeConfig.ChessboardContrast.Min,
                        recipeConfig.ChessboardContrast.Max);
                    ctx.Result.Result &= chessboardResult.ChessboardContrast.TestResult;

                    log?.Info(string.Format(
                        CultureInfo.InvariantCulture,
                        "Chessboard Dynamic对比度来源: local, resultName={0}, rows={1}, columns={2}, requestedFirstPointColor={3}, resolvedFirstPointColor={4}, a={5}, LB={6}, LD={7}, LD'={8}, CR'={9}",
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

                testResult.Items = CollectItems(chessboardResult);
                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);
                ctx.ObjectiveTestResult.DynamicTestResults[GetOutputName()] = testResult.Items;
                if (Config.SaveCsv)
                {
                    ChessboardCsvExporter.SavePoixyuvDatas(
                        chessboardResult.PoixyuvDatas,
                        ctx,
                        GetOutputName(),
                        calculation,
                        chessboardResult.ChessboardContrast?.Value,
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
            ChessboardDynamicViewTestResult? testResult = JsonConvert.DeserializeObject<ChessboardDynamicViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return;

            foreach (var poiResultCIExyuvData in testResult.ChessboardViewTestResult.PoixyuvDatas)
            {
                var item = poiResultCIExyuvData.Point;
                switch (item.PointType)
                {
                    case POIPointTypes.Circle:
                        DVCircleText circle = new DVCircleText();
                        circle.Attribute.Center = new Point(item.PixelX, item.PixelY);
                        circle.Attribute.Radius = item.Radius;
                        circle.Attribute.Brush = Brushes.Transparent;
                        circle.Attribute.Pen = new Pen(Brushes.Red, 1);
                        circle.Attribute.Id = item.Id ?? -1;
                        circle.Attribute.Text = item.Name;
                        circle.Attribute.Msg = CVRawOpen.FormatMessage("Y:@Y:F2", poiResultCIExyuvData);
                        circle.Render();
                        ctx.ImageView.AddVisual(circle);
                        break;
                    case POIPointTypes.Rect:
                        DVRectangleText rectangle = new DVRectangleText();
                        rectangle.Attribute.Rect = new Rect(item.PixelX - item.Width / 2, item.PixelY - item.Height / 2, item.Width, item.Height);
                        rectangle.Attribute.Brush = Brushes.Transparent;
                        rectangle.Attribute.Pen = new Pen(Brushes.Red, 1);
                        rectangle.Attribute.Id = item.Id ?? -1;
                        rectangle.Attribute.Text = item.Name;
                        rectangle.Attribute.Msg = CVRawOpen.FormatMessage("Y:@Y:F2", poiResultCIExyuvData);
                        rectangle.Render();
                        ctx.ImageView.AddVisual(rectangle);
                        break;
                    default:
                        break;
                }
            }
        }

        public override void GenText(IProcessExecutionContext ctx, System.Windows.Documents.Paragraph paragraph, System.Windows.Media.Brush foreground, double fontSize)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"{GetOutputName()} 画面结果");

            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) { AppendPlainText(paragraph, sb.ToString(), foreground, fontSize); return; }

            ChessboardDynamicTestResult? testResult = JsonConvert.DeserializeObject<ChessboardDynamicTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) { AppendPlainText(paragraph, sb.ToString(), foreground, fontSize); return; }

            sb.AppendLine("Name,Value,Unit,LowLimit,UpLimit,Result");
            foreach (var item in testResult.Items)
            {
                sb.AppendLine($"{item.Name},{item.Value},{item.Unit},{item.LowLimit},{item.UpLimit},{item.TestResult}");
            }

            AppendPlainText(paragraph, sb.ToString(), foreground, fontSize); return;
        }


        private ObjectiveTestItem Build(string name, double value, double low, double up) => new ObjectiveTestItem
        {
            Name = name,
            LowLimit = low,
            UpLimit = up,
            Value = value,
            TestValue = value.ToString(Config.ShowConfig),
            Unit = Config.Unit
        };

        private static ObservableCollection<ObjectiveTestItem> CollectItems(ChessboardTestResult result)
        {
            ObservableCollection<ObjectiveTestItem> items = new ObservableCollection<ObjectiveTestItem>();
            if (result.AverageBlackLuminance != null)
                items.Add(result.AverageBlackLuminance);
            if (result.ChessboardContrast != null)
                items.Add(result.ChessboardContrast);
            return items;
        }

        private string GetOutputName()
        {
            return string.IsNullOrWhiteSpace(Config.Name) ? "Chessboard" : Config.Name.Trim();
        }
    }
}
