using ColorVision.Engine;
using ColorVision.Engine.Media;
using Newtonsoft.Json;
using ProjectARVRPro.Process.KeyedResults;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;

namespace ProjectARVRPro.Process.Chessboard
{
    public class ChessboardDynamicProcess : ProcessWithRecipeBase<ChessboardDynamicProcessConfig, ChessboardRecipeConfig>
    {
        internal ChessboardBatchResults BatchResults { get; init; } = new();

        public override async Task<bool> Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null || ctx.ObjectiveTestResult == null) return false;
            var log = ctx.Log;
            ChessboardRecipeConfig recipeConfig = Config.RecipeConfig;
            ChessboardDynamicViewTestResult testResult = new ChessboardDynamicViewTestResult();
            ChessboardViewTestResult chessboardResult = testResult.ChessboardViewTestResult;
            string contrastResultName = string.IsNullOrWhiteSpace(Config.ChessboardContrastResultName)
                ? "Chessboard_Contrast"
                : Config.ChessboardContrastResultName.Trim();
            bool databaseContrastFound = false;
            string? invalidPoiData = null;
            ChessboardContrastCalculationResult? calculation = null;
            string outputKey = Config.GetOutputKey();

            try
            {
                log?.Info("开始 Chessboard Dynamic 流程解析");

                var values = ctx.GetMeasureResults();
                if (values.Count > 0)
                {
                    string? fileUrl = values[0].FileUrl;
                    if (!string.IsNullOrWhiteSpace(fileUrl))
                        ctx.Result.FileName = fileUrl;
                }

                var masters = BatchResults.ReadMasters?.Invoke(ctx.Batch.Id) ?? ctx.ReadMasterResults();
                foreach (var master in masters)
                {
                    if (master.ImgFileType == ViewResultAlgType.POI_XYZ)
                    {
                        if (File.Exists(master.ImgFile))
                            ctx.Result.FileName = master.ImgFile;
                        string? pointError = BatchResults.AppendMeasuredPoints(ctx, master.Id, chessboardResult.PoixyuvDatas);
                        invalidPoiData ??= pointError;
                    }

                    if (master.ImgFileType == ViewResultAlgType.PoiAnalysis && master.TName?.Contains(contrastResultName, StringComparison.Ordinal) == true)
                    {
                        databaseContrastFound = true;
                        double measured = BatchResults.ReadTemplateContrast(ctx, master.Id, contrastResultName);
                        double corrected = ChessboardBatchResults.ApplyRecipe(measured, recipeConfig.ChessboardContrast);
                        chessboardResult.ChessboardContrast = Build(
                            contrastResultName,
                            corrected,
                            recipeConfig.ChessboardContrast.Min,
                            recipeConfig.ChessboardContrast.Max);
                        ctx.Result.Result &= chessboardResult.ChessboardContrast.TestResult;
                        log?.Info($"Chessboard Dynamic对比度来源: database, resultName={contrastResultName}");
                    }
                }

                if (!databaseContrastFound || Config.SaveCsv)
                {
                    calculation = invalidPoiData == null
                        ? ChessboardContrastCalculator.Calculate(
                            chessboardResult.PoixyuvDatas, Config.RowCount, Config.ColumnCount, Config.FirstPointColor,
                            Config.StrayLightCoefficient, Config.AllowNegativeCorrectedDarkLuminance)
                        : new ChessboardContrastCalculationResult { ErrorMessage = invalidPoiData };
                    if (!calculation.Success)
                    {
                        if (!databaseContrastFound)
                            throw new InvalidDataException(calculation.ErrorMessage);

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
                    double correctedContrast = ChessboardBatchResults.ApplyRecipe(calculation.CorrectedContrast, recipeConfig.ChessboardContrast);
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
                var objectiveResult = JsonConvert.DeserializeObject<ChessboardTestResult>(JsonConvert.SerializeObject(chessboardResult)) ?? new();
                KeyedTestResultWriter.Write(ctx.ObjectiveTestResult, outputKey, objectiveResult);
                if (Config.SaveCsv)
                {
                    ChessboardCsvExporter.SavePoixyuvDatas(
                        chessboardResult.PoixyuvDatas,
                        ctx,
                        outputKey,
                        calculation,
                        chessboardResult.ChessboardContrast?.Value,
                        contrastResultName,
                        databaseContrastFound ? "database" : "local");
                }
                return true;
            }
            catch (Exception ex)
            {
                string message = $"棋盘格解析失败 [{outputKey}/{contrastResultName}]：{ex.Message}";
                log?.Error(message, ex);
                Reject(ctx, outputKey, message);
                return false;
            }
        }

        public override Task<bool> ExecuteFailure(IProcessExecutionContext ctx)
        {
            if (ctx?.Result != null && ctx.ObjectiveTestResult != null)
                Reject(ctx, Config.GetOutputKey(), string.IsNullOrWhiteSpace(ctx.Result.Msg) ? "棋盘格流程未成功完成。" : ctx.Result.Msg);
            return Task.FromResult(false);
        }

        private static void Reject(IProcessExecutionContext ctx, string outputKey, string message)
        {
            ChessboardBatchResults.Reject(ctx, message);
            var objective = ctx.ObjectiveTestResult;
            var results = objective.ChessboardTestResults;
            string? previousKey = results?.Keys.FirstOrDefault(key => KeyedTestResultDictionary.IsKey(key, outputKey));
            if (previousKey != null && results!.Remove(previousKey, out var previous)
                && KeyedTestResultDictionary.IsKey(outputKey, "Chessboard") && ReferenceEquals(objective.ChessboardTestResult, previous))
                objective.ChessboardTestResult = null!;
        }

        public override IReadOnlyList<ObjectiveTestCsvRow> GetObjectiveCsvRows(ProjectARVRReuslt result) =>
            GetObjectiveCsvRows<ChessboardDynamicTestResult>(result, Config.GetOutputKey());

        public override void Render(IProcessExecutionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return;
            ChessboardDynamicViewTestResult? testResult = JsonConvert.DeserializeObject<ChessboardDynamicViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return;

            foreach (var poiResultCIExyuvData in testResult.ChessboardViewTestResult.PoixyuvDatas)
            {
                var item = poiResultCIExyuvData.Point;
                PoiOverlayRenderer.Add(ctx.ImageView, item, CVRawOpen.FormatMessage(Config.DisplayTemplate, poiResultCIExyuvData));
            }
        }

        public override void GenText(IProcessExecutionContext ctx, System.Windows.Documents.Paragraph paragraph, System.Windows.Media.Brush foreground, double fontSize)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"{Config.GetOutputKey()} 画面结果");

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

    }
}
