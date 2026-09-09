using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ColorVision.Engine.Services.PhyCameras.Calibration
{
    internal static class LumFourColorMeasurementClipboard
    {
        internal static readonly string[] Headers = { "目标", "相机 Y", "相机 x", "相机 y", "光谱 Y", "光谱 x", "光谱 y" };

        internal static void Paste(IReadOnlyList<CorrectionMeasurementRow> rows, string text, int startRow, int startColumn)
        {
            string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').TrimEnd('\n').Split('\n');
            if (lines.Length == 1 && string.IsNullOrWhiteSpace(lines[0])) throw new InvalidOperationException("剪贴板没有测量数据。");
            var cells = lines.Select(line => line.Split('\t').Select(value => value.Trim()).ToArray()).ToList();
            int columns = cells[0].Length;
            if (cells.Any(row => row.Length != columns)) throw new InvalidOperationException("粘贴区域的列数不一致，请在 Excel 中选择连续的矩形区域。");
            bool hasTargets = cells[0][0] == "目标" || cells[0][0] is "R" or "G" or "B" or "W" or "单点";
            int column = hasTargets ? 0 : Math.Max(1, startColumn);
            if (startRow < 0 || column + columns > Headers.Length) throw new InvalidOperationException("粘贴列数超出表格：数值按相机 Y / x / y、光谱 Y / x / y 排列，可在首列带目标名称。");
            if (cells[0].SequenceEqual(Headers.Skip(column).Take(columns))) cells.RemoveAt(0);
            if (cells.Count == 0 || startRow + cells.Count > rows.Count) throw new InvalidOperationException("粘贴行数超出当前模式，请核对单点或 RGBW 模式及起始单元格。");
            // Validate the complete rectangle before changing any existing measurement.
            for (int row = 0; row < cells.Count; row++)
                for (int col = 0; col < columns; col++)
                {
                    string value = cells[row][col];
                    int targetColumn = column + col;
                    if (targetColumn == 0)
                    {
                        if (value != rows[startRow + row].Target) throw new InvalidOperationException($"第 {row + 1} 行目标为 {value}，应为 {rows[startRow + row].Target}；请按表格中的色块顺序粘贴。");
                    }
                    else if (value.Length > 0 && !IsFiniteNumber(value))
                        throw new InvalidOperationException($"{rows[startRow + row].Target} 的 {Headers[targetColumn]} 不是有限数值：{value}。本次未粘贴。");
                }
            for (int row = 0; row < cells.Count; row++)
                for (int col = 0; col < columns; col++)
                    if (column + col > 0) SetCell(rows[startRow + row], column + col, cells[row][col]);
        }

        private static bool IsFiniteNumber(string text) =>
            (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value)
                || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) && double.IsFinite(value);

        internal static string CopyAll(IEnumerable<CorrectionMeasurementRow> rows) =>
            string.Join(Environment.NewLine, new[] { string.Join('\t', Headers) }.Concat(rows.Select(row =>
                string.Join('\t', row.Target, row.CameraY, row.CameraX, row.CameraYChromaticity, row.ReferenceY, row.ReferenceX, row.ReferenceYChromaticity))));

        private static void SetCell(CorrectionMeasurementRow row, int column, string value)
        {
            switch (column)
            {
                case 1: row.CameraY = value; break;
                case 2: row.CameraX = value; break;
                case 3: row.CameraYChromaticity = value; break;
                case 4: row.ReferenceY = value; break;
                case 5: row.ReferenceX = value; break;
                case 6: row.ReferenceYChromaticity = value; break;
            }
        }
    }
}
