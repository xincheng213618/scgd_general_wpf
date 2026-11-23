#pragma warning disable CS8601,CS8602
using ColorVision.Engine;
using ColorVision.Engine.Media;
using Newtonsoft.Json;
using ProjectARVRPro.Process.KeyedResults;
using System.Globalization;
using System.IO;

namespace ProjectARVRPro.Process.Chessboard
{
    public class ChessboardProcess : ProcessWithRecipeBase<ChessboardProcessConfig, ChessboardRecipeConfig>
    {
        internal ChessboardBatchResults BatchResults { get; init; } = new();

        public override async Task<bool> Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null || ctx.ObjectiveTestResult == null) return false;
            var log = ctx.Log;
            ChessboardRecipeConfig recipeConfig = Config.RecipeConfig;
            ChessboardViewTestResult testResult = new ChessboardViewTestResult();
            string contrastResultName = string.IsNullOrWhiteSpace(Config.ChessboardContrastResultName)
                ? "Chessboard_Contrast"
                : Config.ChessboardContrastResultName.Trim();
            bool databaseContrastFound = false;
            string? invalidPoiData = null;
            ChessboardContrastCalculationResult? calculation = null;

            try
            {
                log?.Info("开始 Chessboard 流程解析");

                var values = ctx.GetMeasureResults();
                if (values.Count > 0)
                    ctx.Result.FileName = values[0].FileUrl;

                var masters = BatchResults.ReadMasters?.Invoke(ctx.Batch.Id) ?? ctx.ReadMasterResults();
                foreach (var master in masters)
                {
                    if (master.ImgFileType == ViewResultAlgType.POI_XYZ)
                    {
                        if (File.Exists(master.ImgFile))
                            ctx.Result.FileName = master.ImgFile;
                        string? pointError = BatchResults.AppendMeasuredPoints(ctx, master.Id, testResult.PoixyuvDatas);
                        invalidPoiData ??= pointError;
                    }

                    if (master.ImgFileType == ViewResultAlgType.PoiAnalysis && master.TName?.Contains(contrastResultName, StringComparison.Ordinal) == true)
                    {
                        databaseContrastFound = true;
                        double measured = BatchResults.ReadTemplateContrast(ctx, master.Id, contrastResultName);
                        double corrected = ChessboardBatchResults.ApplyRecipe(measured, recipeConfig.ChessboardContrast);
                        var contrast = new ObjectiveTestItem
                        {
                            Name = contrastResultName,
                            LowLimit = recipeConfig.ChessboardContrast.Min,
                            UpLimit = recipeConfig.ChessboardContrast.Max,
                            Value = corrected,
                            TestValue = corrected.ToString("F3")
                        };
                        testResult.ChessboardContrast = contrast;
                        ctx.Result.Result &= contrast.TestResult;
                        log?.Info($"Chessboard对比度来源: database, resultName={contrastResultName}");
                    }
                }

                if (!databaseContrastFound || Config.SaveCsv)
                {
                    calculation = invalidPoiData == null
                        ? ChessboardContrastCalculator.Calculate(
                            testResult.PoixyuvDatas, Config.RowCount, Config.ColumnCount, Config.FirstPointColor,
                            Config.StrayLightCoefficient, Config.AllowNegativeCorrectedDarkLuminance)
                        : new ChessboardContrastCalculationResult { ErrorMessage = invalidPoiData };
                    if (!calculation.Success)
                    {
                        if (!databaseContrastFound)
                            throw new InvalidDataException(calculation.ErrorMessage);

                        log?.Warn($"Chessboard本地CSV分析失败，保留数据库对比度并导出基础POI数据: resultName={contrastResultName}, message={calculation.ErrorMessage}");
                    }
                }

                if (!databaseContrastFound && calculation?.Success == true)
                {
                    double correctedContrast = ChessboardBatchResults.ApplyRecipe(calculation.CorrectedContrast, recipeConfig.ChessboardContrast);
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
                string message = $"棋盘格解析失败 [{contrastResultName}]：{ex.Message}";
                log?.Error(message, ex);
                Reject(ctx, message);
                return false;
            }
        }

        public override Task<bool> ExecuteFailure(IProcessExecutionContext ctx)
        {
            if (ctx?.Result != null && ctx.ObjectiveTestResult != null)
                Reject(ctx, string.IsNullOrWhiteSpace(ctx.Result.Msg) ? "棋盘格流程未成功完成。" : ctx.Result.Msg);
            return Task.FromResult(false);
        }

        private static void Reject(IProcessExecutionContext ctx, string message)
        {
            ChessboardBatchResults.Reject(ctx, message);
            var objective = ctx.ObjectiveTestResult;
            objective.ChessboardTestResult = KeyedTestResultDictionary.TryGetValue(objective.ChessboardTestResults, "Chessboard", out var available)
                ? available! : null!;
        }

        public override IReadOnlyList<ObjectiveTestCsvRow> GetObjectiveCsvRows(ProjectARVRReuslt result) =>
            GetObjectiveCsvRows<ChessboardTestResult>(result, "Chessborad");

        public override void Render(IProcessExecutionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return;
            ChessboardViewTestResult testResult = JsonConvert.DeserializeObject<ChessboardViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return;

            foreach (var poiResultCIExyuvData in testResult.PoixyuvDatas)
            {
                var item = poiResultCIExyuvData.Point;
                PoiOverlayRenderer.Add(ctx.ImageView, item, CVRawOpen.FormatMessage(Config.DisplayTemplate, poiResultCIExyuvData));
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
