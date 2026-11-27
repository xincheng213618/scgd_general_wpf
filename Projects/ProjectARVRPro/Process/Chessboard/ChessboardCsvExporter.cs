using ColorVision.Engine.Templates.POI.AlgorithmImp;
using CsvHelper;
using CsvHelper.Configuration;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;

namespace ProjectARVRPro.Process.Chessboard
{
    public static class ChessboardCsvExporter
    {
        public static void SavePoixyuvDatas(
            IEnumerable<PoiResultCIExyuvData>? poixyuvDatas,
            IProcessExecutionContext ctx,
            string outputName,
            ChessboardContrastCalculationResult? calculation,
            double? reportedContrast,
            string contrastResultName,
            string contrastSource)
        {
            try
            {
                string exportDir = GetExportDirectory(ctx);
                string safeOutputName = SanitizeFileName(string.IsNullOrWhiteSpace(outputName) ? "Chessboard" : outputName);
                string filePath = Path.Combine(exportDir, $"{safeOutputName}_PoixyuvDatas_{DateTime.Now:yyyyMMdd_HHmmssfff}.csv");
                WriteCsvFile(filePath, poixyuvDatas, calculation, reportedContrast, contrastResultName, contrastSource);
                ctx.Log.Info($"Chessboard PoixyuvDatas CSV saved: {filePath}");
            }
            catch (Exception ex)
            {
                ctx.Log.Error("Chessboard PoixyuvDatas CSV save failed", ex);
            }
        }

        internal static void WriteCsvFile(
            string filePath,
            IEnumerable<PoiResultCIExyuvData>? poixyuvDatas,
            ChessboardContrastCalculationResult? calculation,
            double? reportedContrast,
            string contrastResultName,
            string contrastSource)
        {
            IReadOnlyList<ChessboardPoiClassification>? classifiedPoints = calculation?.Success == true &&
                calculation.ClassifiedPoints.Count > 0 ? calculation.ClassifiedPoints : null;
            var items = new ObservableCollection<PoiResultCIExyuvData>(classifiedPoints != null
                ? classifiedPoints.Select(point => point.Point)
                : poixyuvDatas ?? []);

            items.SaveCsv(filePath);
            using var csv = new StringWriter(CultureInfo.InvariantCulture);
            if (classifiedPoints != null)
            {
                using var source = new StreamReader(filePath, Encoding.UTF8);
                using var parser = CreateParser(source);
                int recordIndex = 0;
                // The POI exporter emits a header followed by one record per item and its measurement rows.
                while (parser.Read())
                    WriteRecord(csv, parser.RawRecord.AsSpan().TrimEnd("\r\n"), classifiedPoints, recordIndex++);
            }

            WriteSummary(csv, classifiedPoints != null ? 16 : 15, calculation, reportedContrast, contrastResultName, contrastSource);
            // Without colors only the summary is new. For colored output finish parsing before overwriting the base file.
            using var output = new StreamWriter(filePath, append: classifiedPoints == null, Encoding.UTF8);
            foreach (ReadOnlyMemory<char> chunk in csv.GetStringBuilder().GetChunks())
                output.Write(chunk.Span);
        }

        public static string BuildEnhancedCsvContent(
            string baseCsv,
            ChessboardContrastCalculationResult? calculation,
            double? reportedContrast,
            string contrastResultName,
            string contrastSource)
        {
            List<string> records = ReadRecords(baseCsv);
            bool includeCellColor = calculation?.Success == true &&
                calculation.ClassifiedPoints.Count > 0 &&
                records.Count > calculation.ClassifiedPoints.Count;

            IReadOnlyList<ChessboardPoiClassification>? classifiedPoints = includeCellColor ? calculation!.ClassifiedPoints : null;
            using var csv = new StringWriter(CultureInfo.InvariantCulture);
            for (int index = 0; index < records.Count; index++)
                WriteRecord(csv, records[index], classifiedPoints, index);

            WriteSummary(csv, includeCellColor ? 16 : 15, calculation, reportedContrast, contrastResultName, contrastSource);
            return csv.ToString();
        }

        private static void WriteRecord(TextWriter csv, ReadOnlySpan<char> record,
            IReadOnlyList<ChessboardPoiClassification>? classifiedPoints, int index)
        {
            csv.Write(record);
            if (classifiedPoints != null)
            {
                csv.Write(',');
                if (index == 0)
                    csv.Write("cell_color");
                else if (index <= classifiedPoints.Count)
                    csv.Write(classifiedPoints[index - 1].CellColor.ToString());
            }
            csv.WriteLine();
        }

        private static void WriteSummary(
            TextWriter csv,
            int columnCount,
            ChessboardContrastCalculationResult? calculation,
            double? reportedContrast,
            string contrastResultName,
            string contrastSource)
        {
            csv.WriteLine(new string(',', columnCount - 1));
            AppendSummaryRow(csv, columnCount, "Chessboard Measurement Item", "Value", "Unit");
            AppendSummaryRow(csv, columnCount, "Contrast Result Name", contrastResultName);
            AppendSummaryRow(csv, columnCount, "Contrast Source", contrastSource);

            if (calculation?.Success == true)
            {
                AppendSummaryRow(csv, columnCount, "Requested First Point Color", calculation.RequestedFirstPointColor.ToString());
                AppendSummaryRow(csv, columnCount, "Resolved First Point Color", calculation.ResolvedFirstPointColor.ToString());
                AppendSummaryRow(csv, columnCount, "Chessboard Rows", FormatNumber(calculation.RowCount));
                AppendSummaryRow(csv, columnCount, "Chessboard Columns", FormatNumber(calculation.ColumnCount));
                AppendSummaryRow(csv, columnCount, "White Average Luminance", FormatNumber(calculation.BrightLuminance), "cd/m^2");
                AppendSummaryRow(csv, columnCount, "White Min Luminance", FormatNumber(calculation.BrightMinLuminance), "cd/m^2");
                AppendSummaryRow(csv, columnCount, "White Max Luminance", FormatNumber(calculation.BrightMaxLuminance), "cd/m^2");
                AppendSummaryRow(csv, columnCount, "White Luminance Uniformity (Min/Max*100%)", FormatNumber(calculation.BrightUniformity * 100), "%");
                AppendSummaryRow(csv, columnCount, "Black Raw Average Luminance", FormatNumber(calculation.RawDarkLuminance), "cd/m^2");
                AppendSummaryRow(csv, columnCount, "Black Raw Min Luminance", FormatNumber(calculation.DarkMinLuminance), "cd/m^2");
                AppendSummaryRow(csv, columnCount, "Black Raw Max Luminance", FormatNumber(calculation.DarkMaxLuminance), "cd/m^2");
                AppendSummaryRow(csv, columnCount, "Black Raw Luminance Uniformity (Min/Max*100%)", FormatNumber(calculation.DarkUniformity * 100), "%");
                AppendSummaryRow(csv, columnCount, "Stray Light Coefficient", FormatNumber(calculation.StrayLightCoefficient));
                AppendSummaryRow(csv, columnCount, "Stray Light Offset", FormatNumber(calculation.StrayLightOffset), "cd/m^2");
                AppendSummaryRow(csv, columnCount, "Black Corrected Average Luminance", FormatNumber(calculation.CorrectedDarkLuminance), "cd/m^2");
                AppendSummaryRow(csv, columnCount, "Local Corrected Chessboard Contrast", FormatNumber(calculation.CorrectedContrast));
                AppendSummaryRow(csv, columnCount, "Allow Negative Corrected Dark Luminance", calculation.AllowNegativeCorrectedDarkLuminance.ToString());
            }

            if (reportedContrast.HasValue)
                AppendSummaryRow(csv, columnCount, "Reported Chessboard Contrast", FormatNumber(reportedContrast.Value));
        }

        private static List<string> ReadRecords(string value)
        {
            var records = new List<string>();
            using var parser = CreateParser(new StringReader(value ?? string.Empty));
            while (parser.Read())
            {
                // Preserve the original quoting and line breaks inside each field.
                records.Add(parser.RawRecord.TrimEnd('\r', '\n'));
            }
            return records;
        }

        private static CsvParser CreateParser(TextReader reader)
            => new CsvParser(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { IgnoreBlankLines = false });

        private static void AppendSummaryRow(TextWriter csv, int columnCount, string item, string value, string unit = "")
        {
            var cells = new string[columnCount];
            cells[0] = EscapeCsv(item);
            cells[1] = EscapeCsv(value);
            cells[2] = EscapeCsv(unit);
            csv.WriteLine(string.Join(",", cells));
        }

        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            string escaped = value.Replace("\"", "\"\"");
            return escaped.IndexOfAny([',', '\r', '\n', '\"']) >= 0 ? $"\"{escaped}\"" : escaped;
        }

        private static string FormatNumber(double value) => value.ToString("G", CultureInfo.InvariantCulture);

        private static string GetExportDirectory(IProcessExecutionContext ctx)
        {
            var config = ViewResultManager.GetInstance().Config;
            string exportDir = string.IsNullOrWhiteSpace(config.CsvSavePath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ARVR")
                : config.CsvSavePath;

            if (config.SaveByDate)
            {
                exportDir = Path.Combine(exportDir, DateTime.Now.ToString("yyyy-MM-dd"));
            }

            string? batchName = ctx.Batch?.Name;
            if (string.IsNullOrWhiteSpace(batchName))
                batchName = ctx.Result?.SN;
            if (string.IsNullOrWhiteSpace(batchName))
                batchName = ctx.Batch?.Id.ToString();

            batchName = SanitizeFileName(batchName);
            if (!string.IsNullOrWhiteSpace(batchName))
            {
                exportDir = Path.Combine(exportDir, batchName);
            }

            Directory.CreateDirectory(exportDir);
            return exportDir;
        }

        private static string SanitizeFileName(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return string.Empty;

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(invalidChar, '_');
            }

            return fileName.Trim();
        }
    }
}
