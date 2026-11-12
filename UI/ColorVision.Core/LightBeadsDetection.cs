using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;

namespace ColorVision.Core;

public readonly record struct LightBeadCenter(int X, int Y);

public sealed record LightBeadsDetectionResult(IReadOnlyList<LightBeadCenter> Centers, IReadOnlyList<LightBeadCenter> DarkCandidates);

/// <summary>Local LED localization. Coordinates are absolute source pixels; dark candidates are not verified missing LEDs.</summary>
public static class LightBeadsDetector
{
    public static LightBeadsDetectionResult Detect(HImage image, RoiRect roi, int threshold = 20, int minSize = 2,
        int maxSize = 0, int rows = 0, int columns = 0, CancellationToken cancellationToken = default)
    {
        if (image.pData == IntPtr.Zero || image.cols <= 0 || image.rows <= 0)
            throw new ArgumentException("灯珠定位需要有效图像。", nameof(image));
        if (threshold is < 0 or > 255 || minSize < 0 || maxSize > 0 && maxSize <= minSize || rows < 0 || columns < 0)
            throw new ArgumentException("阈值须为 0–255，最小尺寸须非负，最大尺寸须大于最小尺寸（0 表示不限），行列数须非负。");
        if (roi.Width == 0 && roi.Height == 0) roi = new RoiRect(0, 0, image.cols, image.rows);
        if (roi.X < 0 || roi.Y < 0 || roi.Width <= 0 || roi.Height <= 0
            || (long)roi.X + roi.Width > image.cols || (long)roi.Y + roi.Height > image.rows)
            throw new ArgumentOutOfRangeException(nameof(roi), "灯珠定位 ROI 必须完整位于图像内。");
        cancellationToken.ThrowIfCancellationRequested();
        string parameters = JsonSerializer.Serialize(new { Threshold = threshold, MinSize = minSize, MaxSize = maxSize, Rows = rows, Cols = columns });
        IntPtr pointer = IntPtr.Zero;
        try
        {
            int code = OpenCVMediaHelper.M_FindLightBeads(image, roi, parameters, out pointer);
            if (code <= 0 || pointer == IntPtr.Zero) throw new InvalidOperationException($"灯珠定位失败，错误代码：{code}。");
            IntPtr owned = pointer;
            pointer = IntPtr.Zero;
            string json = OpenCVMediaHelper.PtrToStringUtf8AndFree(owned);
            cancellationToken.ThrowIfCancellationRequested();
            using JsonDocument document = JsonDocument.Parse(json);
            return new LightBeadsDetectionResult(ReadPoints(document.RootElement, "Centers", roi), ReadPoints(document.RootElement, "BlackCenters", roi));
        }
        finally
        {
            if (pointer != IntPtr.Zero) _ = OpenCVMediaHelper.FreeResult(pointer);
        }
    }

    private static LightBeadCenter[] ReadPoints(JsonElement root, string name, RoiRect roi)
    {
        JsonElement points = root.GetProperty(name);
        var output = new LightBeadCenter[points.GetArrayLength()];
        int index = 0;
        foreach (JsonElement point in points.EnumerateArray())
        {
            int x = point[0].GetInt32(), y = point[1].GetInt32();
            if (point.GetArrayLength() != 2 || x < 0 || y < 0 || x >= roi.Width || y >= roi.Height)
                throw new InvalidOperationException("灯珠定位返回了无效或越界的中心坐标。");
            output[index++] = new LightBeadCenter(checked(roi.X + x), checked(roi.Y + y));
        }
        Array.Sort(output, (left, right) => left.Y == right.Y ? left.X.CompareTo(right.X) : left.Y.CompareTo(right.Y));
        return output;
    }
}
