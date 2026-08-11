using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Services;
using System.IO;
using System.Windows.Media.Imaging;

namespace ProjectKB;

internal static class KBResultImageDimensions
{
    public static bool TryPopulate(KBItemMaster result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (ResultImageDimensions.IsValid(result.ImageWidth, result.ImageHeight))
            return false;

        bool found = TryReadFromFile(result.ResultImagFile, out int width, out int height)
            || TryReadFromMeasureResults(result.BatchId, result.ResultImagFile, out width, out height);
        if (!found)
            return false;

        result.ImageWidth = width;
        result.ImageHeight = height;
        return true;
    }

    public static bool TryReadFromFile(string? filePath, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return false;

        try
        {
            using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            BitmapDecoder decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.DelayCreation | BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.None);
            if (decoder.Frames.Count == 0)
                return false;

            BitmapFrame frame = decoder.Frames[0];
            width = frame.PixelWidth;
            height = frame.PixelHeight;
            return ResultImageDimensions.IsValid(width, height);
        }
        catch
        {
            width = 0;
            height = 0;
            return false;
        }
    }

    public static bool TryReadFromMeasureResults(int batchId, string? expectedFilePath, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (batchId <= 0)
            return false;

        try
        {
            List<MeasureResultImgModel> images = MeasureImgResultDao.Instance.GetAllByBatchId(batchId);
            MeasureResultImgModel? exact = images.FirstOrDefault(item => PathsEqual(item.FileUrl, expectedFilePath));
            if (exact != null && ResultImageDimensions.TryReadFrameInfo(exact.ImgFrameInfo, out width, out height))
                return true;

            (int Width, int Height)[] sizes = images
                .Select(item => ResultImageDimensions.TryReadFrameInfo(item.ImgFrameInfo, out int itemWidth, out int itemHeight)
                    ? (Width: itemWidth, Height: itemHeight)
                    : (Width: 0, Height: 0))
                .Where(size => size.Width > 0 && size.Height > 0)
                .Distinct()
                .ToArray();

            if (sizes.Length != 1)
                return false;

            width = sizes[0].Width;
            height = sizes[0].Height;
            return true;
        }
        catch
        {
            width = 0;
            height = 0;
            return false;
        }
    }

    private static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        try
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
