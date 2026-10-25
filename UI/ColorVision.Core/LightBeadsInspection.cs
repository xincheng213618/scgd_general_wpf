using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace ColorVision.Core;

public enum LightBeadStatus { Normal, Dark, Dim, Unknown }
public sealed record LightBeadInspectionPoint(int Row, int Column, double X, double Y, LightBeadStatus Status, double Brightness, bool Detected);
public sealed record LightBeadsGrid(int Rows, int Columns, double OriginX, double OriginY, double ColumnX, double ColumnY, double RowX, double RowY);

public sealed record LightBeadsInspectionResult(LightBeadsGrid Grid, IReadOnlyList<LightBeadInspectionPoint> Points,
    IReadOnlyList<LightBeadCenter> UnmatchedCenters, double ReferenceBrightness, double DimThreshold, int DarkThreshold, bool InferredSize)
{
    public int TotalCount => Points.Count;
    public int BadCount => Points.Count(point => point.Status is LightBeadStatus.Dark or LightBeadStatus.Dim);
    public int NormalCount => Points.Count(point => point.Status == LightBeadStatus.Normal);
    public int UnknownCount => Points.Count(point => point.Status == LightBeadStatus.Unknown);

    public void WriteCsv(TextWriter writer, bool defectsOnly, CancellationToken cancellationToken = default)
    {
        writer.WriteLine("Row,Column,X,Y,Status,Brightness,Detected");
        foreach (LightBeadInspectionPoint point in Points)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (defectsOnly && point.Status == LightBeadStatus.Normal) continue;
            writer.WriteLine(string.Join(",", point.Row, point.Column, point.X.ToString("F3", CultureInfo.InvariantCulture),
                point.Y.ToString("F3", CultureInfo.InvariantCulture), point.Status, point.Brightness.ToString("F3", CultureInfo.InvariantCulture), point.Detected));
        }
    }
}

/// <summary>Fits a regular near-axis-aligned lattice, then inspects every cell rather than enumerating dark contours.</summary>
public static class LightBeadsInspector
{
    private readonly record struct GridPoint(int Column, int Row, LightBeadCenter Center, double Error);

    public static LightBeadsInspectionResult Inspect(HImage image, IReadOnlyList<LightBeadCenter> centers,
        int expectedRows = 0, int expectedColumns = 0, double dimRatio = 0.5, int darkThreshold = 20, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(centers);
        if (image.pData == IntPtr.Zero || image.cols <= 0 || image.rows <= 0 || image.channels is not (1 or 3 or 4)
            || image.depth is not (8 or 16 or 32) || image.stride < checked(image.cols * image.channels * (image.depth / 8)))
            throw new ArgumentException("灯珠检测需要有效的 1/3/4 通道 8/16/32 位图像。", nameof(image));
        if (centers.Count < 9) throw new InvalidOperationException("有效亮点不足，无法可靠建立规则网格；请调整阈值或搜索区域。");
        if (expectedRows < 0 || expectedColumns < 0 || !double.IsFinite(dimRatio) || dimRatio <= 0 || dimRatio >= 1)
            throw new ArgumentException("行列数须非负，偏暗比例须大于 0 且小于 1。");
        cancellationToken.ThrowIfCancellationRequested();
        var grid = Fit(centers, cancellationToken);
        Dictionary<(int, int), GridPoint> matched = Match(centers, grid);
        int[] rows = SupportedIndices(matched.Values.Select(point => point.Row));
        int[] columns = SupportedIndices(matched.Values.Select(point => point.Column));
        int firstRow = rows.Min(), firstColumn = columns.Min();
        int rowCount = rows.Max() - firstRow + 1, columnCount = columns.Max() - firstColumn + 1;
        if (expectedRows > 0 && expectedRows != rowCount || expectedColumns > 0 && expectedColumns != columnCount)
            throw new InvalidOperationException($"拟合网格为 {rowCount} 行 × {columnCount} 列，与指定行列不符；可能存在整行/整列不亮或错误网格，不能可靠推断边界坐标。");
        int count = checked(rowCount * columnCount);
        if (rowCount < 2 || columnCount < 2 || count > (long)image.cols * image.rows)
            throw new InvalidOperationException("无法建立有效的二维灯珠网格。");
        var samples = new (double X, double Y, double Brightness, bool Detected)[count];
        var reference = new List<double>();
        var used = new HashSet<LightBeadCenter>();
        int searchX = Math.Max(1, (int)Math.Floor(Math.Sqrt(grid.ColumnX * grid.ColumnX + grid.ColumnY * grid.ColumnY) * 0.25));
        int searchY = Math.Max(1, (int)Math.Floor(Math.Sqrt(grid.RowX * grid.RowX + grid.RowY * grid.RowY) * 0.25));
        for (int row = 0, index = 0; row < rowCount; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int column = 0; column < columnCount; column++, index++)
            {
                double x = grid.OriginX + grid.ColumnX * (column + firstColumn) + grid.RowX * (row + firstRow);
                double y = grid.OriginY + grid.ColumnY * (column + firstColumn) + grid.RowY * (row + firstRow);
                bool detected = matched.TryGetValue((column + firstColumn, row + firstRow), out GridPoint match);
                if (detected) used.Add(match.Center);
                var peak = Sample(image, x, y, searchX, searchY);
                double brightness = peak.Brightness;
                if (brightness > darkThreshold) { x = peak.X; y = peak.Y; }
                samples[index] = (x, y, brightness, detected);
                if (detected && brightness > darkThreshold) reference.Add(brightness);
            }
        }
        if (reference.Count < 4) throw new InvalidOperationException("可用亮点不足，无法建立偏暗判定参考。");
        double median = Median(reference);
        var points = new LightBeadInspectionPoint[count];
        for (int index = 0; index < count; index++)
        {
            var sample = samples[index];
            LightBeadStatus status = !double.IsFinite(sample.Brightness) ? LightBeadStatus.Unknown
                : sample.Brightness <= darkThreshold ? LightBeadStatus.Dark
                : sample.Brightness < median * dimRatio ? LightBeadStatus.Dim : LightBeadStatus.Normal;
            points[index] = new(index / columnCount + 1, index % columnCount + 1, sample.X, sample.Y, status, sample.Brightness, sample.Detected);
        }
        return new LightBeadsInspectionResult(new(rowCount, columnCount,
            grid.OriginX + firstColumn * grid.ColumnX + firstRow * grid.RowX,
            grid.OriginY + firstColumn * grid.ColumnY + firstRow * grid.RowY,
            grid.ColumnX, grid.ColumnY, grid.RowX, grid.RowY), points, centers.Where(point => !used.Contains(point)).ToArray(),
            median, median * dimRatio, darkThreshold, expectedRows == 0 || expectedColumns == 0);
    }

    private static LightBeadsGrid Fit(IReadOnlyList<LightBeadCenter> centers, CancellationToken cancellation)
    {
        double minX = centers.Min(point => point.X), minY = centers.Min(point => point.Y);
        double cell = Math.Max(2, Math.Sqrt((centers.Max(point => point.X) - minX) * (centers.Max(point => point.Y) - minY) / centers.Count));
        var buckets = new Dictionary<(int, int), List<LightBeadCenter>>();
        foreach (var point in centers)
        {
            var key = ((int)Math.Floor((point.X - minX) / cell), (int)Math.Floor((point.Y - minY) / cell));
            if (!buckets.TryGetValue(key, out var points)) buckets[key] = points = new();
            points.Add(point);
        }
        var horizontal = new List<(double X, double Y)>();
        var vertical = new List<(double X, double Y)>();
        foreach (var point in centers)
        {
            cancellation.ThrowIfCancellationRequested();
            int column = (int)Math.Floor((point.X - minX) / cell), row = (int)Math.Floor((point.Y - minY) / cell);
            double hDistance = double.PositiveInfinity, vDistance = double.PositiveInfinity;
            (double X, double Y) h = default, v = default;
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    if (!buckets.TryGetValue((column + dx, row + dy), out var nearby)) continue;
                    foreach (var candidate in nearby)
                    {
                        double x = candidate.X - point.X, y = candidate.Y - point.Y, distance = x * x + y * y;
                        // Use one direction. Picking the shorter of opposite integer-pixel
                        // neighbours systematically underestimates a fractional pitch.
                        if (x > 2 * Math.Abs(y) && distance < hDistance)
                        { hDistance = distance; h = (x, y); }
                        if (y > 2 * Math.Abs(x) && distance < vDistance)
                        { vDistance = distance; v = (x, y); }
                    }
                }
            if (double.IsFinite(hDistance)) horizontal.Add(h);
            if (double.IsFinite(vDistance)) vertical.Add(v);
        }
        var u = TypicalVector(horizontal, true);
        var w = TypicalVector(vertical, false);
        double determinant = u.X * w.Y - w.X * u.Y;
        if (determinant <= 1) throw new InvalidOperationException("灯珠网格方向或间距无效。");
        double sumUX = 0, sumUY = 0, sumVX = 0, sumVY = 0;
        foreach (var point in centers)
        {
            double x = (point.X * w.Y - point.Y * w.X) / determinant;
            double y = (point.Y * u.X - point.X * u.Y) / determinant;
            sumUX += Math.Cos(2 * Math.PI * x); sumUY += Math.Sin(2 * Math.PI * x);
            sumVX += Math.Cos(2 * Math.PI * y); sumVY += Math.Sin(2 * Math.PI * y);
        }
        if (Math.Sqrt(sumUX * sumUX + sumUY * sumUY) < centers.Count * 0.4
            || Math.Sqrt(sumVX * sumVX + sumVY * sumVY) < centers.Count * 0.4)
            throw new InvalidOperationException("亮点不能稳定配准到规则网格；请缩小搜索区域，避免混入其它区域。");
        double phaseU = Math.Atan2(sumUY, sumUX) / (2 * Math.PI), phaseV = Math.Atan2(sumVY, sumVX) / (2 * Math.PI);
        var grid = new LightBeadsGrid(0, 0, phaseU * u.X + phaseV * w.X, phaseU * u.Y + phaseV * w.Y, u.X, u.Y, w.X, w.Y);
        for (int iteration = 0; iteration < 3; iteration++)
        {
            GridPoint[] matched = Match(centers, grid).Values.ToArray();
            if (matched.Length < centers.Count / 2) throw new InvalidOperationException("网格匹配亮点比例过低，无法可靠给出坏点。");
            double c = matched.Average(point => point.Column), r = matched.Average(point => point.Row);
            double x = matched.Average(point => point.Center.X), y = matched.Average(point => point.Center.Y);
            double cc = 0, rr = 0, cr = 0, xc = 0, xr = 0, yc = 0, yr = 0;
            foreach (var point in matched)
            {
                double dc = point.Column - c, dr = point.Row - r;
                cc += dc * dc; rr += dr * dr; cr += dc * dr;
                xc += (point.Center.X - x) * dc; xr += (point.Center.X - x) * dr;
                yc += (point.Center.Y - y) * dc; yr += (point.Center.Y - y) * dr;
            }
            double det = cc * rr - cr * cr;
            if (det <= 0) throw new InvalidOperationException("灯珠网格无法拟合为二维阵列。");
            double ux = (xc * rr - xr * cr) / det, vx = (xr * cc - xc * cr) / det;
            double uy = (yc * rr - yr * cr) / det, vy = (yr * cc - yc * cr) / det;
            grid = new(0, 0, x - c * ux - r * vx, y - c * uy - r * vy, ux, uy, vx, vy);
        }
        return grid;
    }

    private static Dictionary<(int, int), GridPoint> Match(IReadOnlyList<LightBeadCenter> centers, LightBeadsGrid grid)
    {
        double det = grid.ColumnX * grid.RowY - grid.RowX * grid.ColumnY;
        var matched = new Dictionary<(int, int), GridPoint>();
        foreach (var point in centers)
        {
            double x = point.X - grid.OriginX, y = point.Y - grid.OriginY;
            double c = (x * grid.RowY - y * grid.RowX) / det, r = (y * grid.ColumnX - x * grid.ColumnY) / det;
            int column = checked((int)Math.Round(c)), row = checked((int)Math.Round(r));
            double error = (c - column) * (c - column) + (r - row) * (r - row);
            if (error > 0.3 * 0.3) continue;
            var key = (column, row);
            if (!matched.TryGetValue(key, out GridPoint previous) || error < previous.Error)
                matched[key] = new GridPoint(column, row, point, error);
        }
        return matched;
    }

    private static int[] SupportedIndices(IEnumerable<int> indices)
    {
        var counts = indices.GroupBy(index => index).ToDictionary(group => group.Key, group => group.Count());
        double minimum = Math.Max(2, Median(counts.Values.Select(value => (double)value).ToList()) * 0.5);
        int[] output = counts.Where(pair => pair.Value >= minimum).Select(pair => pair.Key).ToArray();
        return output.Length >= 2 ? output : throw new InvalidOperationException("可用网格行列不足。");
    }

    private static (double X, double Y) TypicalVector(List<(double X, double Y)> vectors, bool horizontal)
    {
        if (vectors.Count < 4) throw new InvalidOperationException("未找到规则灯珠间距。");
        double median = Median(vectors.Select(vector => horizontal ? vector.X : vector.Y).ToList());
        var regular = vectors.Where(vector => Math.Abs((horizontal ? vector.X : vector.Y) / median - 1) < 0.2).ToArray();
        return (regular.Average(vector => vector.X), regular.Average(vector => vector.Y));
    }

    private static double Median(List<double> values)
    {
        values.Sort();
        return values.Count % 2 == 0 ? (values[values.Count / 2 - 1] + values[values.Count / 2]) / 2 : values[values.Count / 2];
    }

    private static unsafe (double Brightness, int X, int Y) Sample(HImage image, double x, double y, int searchX, int searchY)
    {
        int cx = checked((int)Math.Round(x)), cy = checked((int)Math.Round(y));
        if (cx < 1 || cy < 1 || cx >= image.cols - 1 || cy >= image.rows - 1) return (double.NaN, cx, cy);
        int left = Math.Max(0, cx - searchX - 1), top = Math.Max(0, cy - searchY - 1);
        int width = Math.Min(image.cols - 1, cx + searchX + 1) - left + 1;
        int height = Math.Min(image.rows - 1, cy + searchY + 1) - top + 1;
        int stride = checked(width + 1), size = checked(stride * (height + 1));
        Span<double> sums = size <= 4096 ? stackalloc double[size] : new double[size];
        sums.Clear();
        for (int row = 0; row < height; row++)
        {
            byte* pixels = (byte*)image.pData + (top + row) * image.stride;
            double running = 0;
            for (int column = 0; column < width; column++)
            {
                int offset = (left + column) * image.channels;
                double Channel(int channel) => image.depth switch
                {
                    8 => pixels[offset + channel],
                    16 => ((ushort*)pixels)[offset + channel] * (255.0 / 65535),
                    32 => Math.Max(((float*)pixels)[offset + channel], 0),
                    _ => throw new NotSupportedException("灯珠采样仅支持 8/16 位整数或 32 位浮点图像。")
                };
                running += image.channels == 1 ? Channel(0) : 0.114 * Channel(0) + 0.587 * Channel(1) + 0.299 * Channel(2);
                if (!double.IsFinite(running)) return (double.NaN, cx, cy);
                sums[(row + 1) * stride + column + 1] = sums[row * stride + column + 1] + running;
            }
        }
        double peak = -1, bestDistance = double.PositiveInfinity;
        int peakX = cx, peakY = cy;
        for (int row = 1; row < height - 1; row++)
            for (int column = 1; column < width - 1; column++)
            {
                double mean = (sums[(row + 2) * stride + column + 2] - sums[(row - 1) * stride + column + 2]
                    - sums[(row + 2) * stride + column - 1] + sums[(row - 1) * stride + column - 1]) / 9;
                double distance = (left + column - x) * (left + column - x) + (top + row - y) * (top + row - y);
                if (mean > peak || mean == peak && distance < bestDistance)
                { peak = mean; peakX = left + column; peakY = top + row; bestDistance = distance; }
            }
        return (peak, peakX, peakY);
    }
}
