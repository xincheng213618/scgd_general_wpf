using ColorVision.Engine.Templates.POI.AlgorithmImp;
using System.Collections.ObjectModel;
using System.IO;

namespace ProjectARVRPro.Process.Chessboard
{
    internal static class ChessboardCsvExporter
    {
        public static void SavePoixyuvDatas(IEnumerable<PoiResultCIExyuvData>? poixyuvDatas, IProcessExecutionContext ctx, string outputName)
        {
            try
            {
                ObservableCollection<PoiResultCIExyuvData> items = new ObservableCollection<PoiResultCIExyuvData>();
                if (poixyuvDatas != null)
                {
                    foreach (var item in poixyuvDatas)
                    {
                        items.Add(item);
                    }
                }

                string exportDir = GetExportDirectory(ctx);
                string safeOutputName = SanitizeFileName(string.IsNullOrWhiteSpace(outputName) ? "Chessboard" : outputName);
                string filePath = Path.Combine(exportDir, $"{safeOutputName}_PoixyuvDatas_{DateTime.Now:yyyyMMdd_HHmmssfff}.csv");
                items.SaveCsv(filePath);
                ctx.Log.Info($"Chessboard PoixyuvDatas CSV saved: {filePath}");
            }
            catch (Exception ex)
            {
                ctx.Log.Error("Chessboard PoixyuvDatas CSV save failed", ex);
            }
        }

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
