using System;
using System.Globalization;
using System.IO;
using System.Text;
using ColorVision.FlowPoi.Client;

namespace ColorVision.FlowPoi.Demo
{
    internal static class DemoResultFiles
    {
        public static void SaveJson(FlowPoiResponse response, string path)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));
            File.WriteAllText(PreparePath(path), response.ResponseJson, new UTF8Encoding(false));
        }

        public static void SaveCsv(FlowPoiResponse response, string path)
        {
            if (response == null || response.Data == null || response.Data.Poi == null || response.Data.Poi.Count == 0)
                throw new InvalidOperationException("没有可导出的 POI 结果。");

            using (var writer = new StreamWriter(PreparePath(path), false, new UTF8Encoding(true)))
            {
                writer.WriteLine("Code,FlowId,FlowName,BatchId,ExecutionSerialNumber,Status,TotalTime,AlgorithmResultId,AlgorithmName,PoiId,Name,PixelX,PixelY,Width,Height,X,Y,Z,x,y,u,v,CCT,Wave");
                foreach (FlowPoiPoint point in response.Data.Poi)
                {
                    object[] values =
                    {
                        response.Code, response.Data.FlowId, response.Data.FlowName, response.Data.BatchId,
                        response.Data.ExecutionSerialNumber, response.Data.Status, response.Data.TotalTime,
                        point.AlgorithmResultId, point.AlgorithmName, point.PoiId, point.Name,
                        point.PixelX, point.PixelY, point.Width, point.Height,
                        point.X, point.Y, point.Z, point.ChromaticityX, point.ChromaticityY,
                        point.U, point.V, point.CCT, point.Wave
                    };
                    string[] cells = new string[values.Length];
                    for (int i = 0; i < values.Length; i++)
                        cells[i] = CsvCell(values[i]);
                    writer.WriteLine(string.Join(",", cells));
                }
            }
        }

        private static string CsvCell(object value)
        {
            string text = value is double
                ? ((double)value).ToString("G17", CultureInfo.InvariantCulture)
                : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            // Preserve names as text when the CSV is opened in a spreadsheet.
            if (value is string && text.Length > 0 && "=+-@\t\r".IndexOf(text[0]) >= 0)
                text = "'" + text;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        private static string PreparePath(string path)
        {
            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            return fullPath;
        }
    }
}
