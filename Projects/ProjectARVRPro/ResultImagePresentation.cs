using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.FileIO;
using Newtonsoft.Json.Linq;
using ProjectARVRPro.ImageExport;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ProjectARVRPro;

internal enum ResultImageFileKind
{
    Original,
    SavedSource,
    SavedResult,
}

internal readonly record struct ResultImageFileCandidate(string FilePath, ResultImageFileKind Kind)
{
    public bool RequiresOverlayRendering => Kind != ResultImageFileKind.SavedResult;
}

internal static class ResultImageFileCandidates
{
    public static IReadOnlyList<ResultImageFileCandidate> GetExisting(
        ProjectARVRReuslt result,
        Func<string, bool>? fileExists = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        fileExists ??= File.Exists;

        List<ResultImageFileCandidate> candidates = [];
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        Add(result.FileName, ResultImageFileKind.Original);
        Add(result.SavedSourceImageFileName, ResultImageFileKind.SavedSource);
        Add(result.SavedResultImageFileName, ResultImageFileKind.SavedResult);
        return candidates;

        void Add(string? filePath, ResultImageFileKind kind)
        {
            if (string.IsNullOrWhiteSpace(filePath)
                || !paths.Add(filePath)
                || !fileExists(filePath))
                return;

            candidates.Add(new ResultImageFileCandidate(filePath, kind));
        }
    }

    public static async Task<ResultImageFileCandidate?> OpenFirstAsync(
        IReadOnlyList<ResultImageFileCandidate> candidates,
        Func<ResultImageFileCandidate, CancellationToken, Task<bool>> tryOpen,
        Action<ResultImageFileCandidate, Exception?>? onFailure = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(tryOpen);

        foreach (ResultImageFileCandidate candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (await tryOpen(candidate, cancellationToken))
                    return candidate;

                onFailure?.Invoke(candidate, null);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                onFailure?.Invoke(candidate, ex);
            }
        }

        return null;
    }
}

internal readonly record struct ResultImageExportPathUpdate(
    bool UpdateSavedResultImageFileName,
    string? SavedResultImageFileName,
    bool UpdateSavedSourceImageFileName,
    string? SavedSourceImageFileName)
{
    public static ResultImageExportPathUpdate From(
        ProjectImageExportAttemptResult exportResult,
        bool renderedImageIncludesOverlays,
        string? currentSavedResultImageFileName)
    {
        ArgumentNullException.ThrowIfNull(exportResult);
        bool renderedImageSaved = !string.IsNullOrWhiteSpace(exportResult.RenderedFileName);
        bool sourceImageSaved = !string.IsNullOrWhiteSpace(exportResult.SourceFileName);
        bool clearSavedResultImageFileName = renderedImageSaved
            && !renderedImageIncludesOverlays
            && AreSameWindowsFilePath(currentSavedResultImageFileName, exportResult.RenderedFileName);
        return new ResultImageExportPathUpdate(
            (renderedImageSaved && renderedImageIncludesOverlays) || clearSavedResultImageFileName,
            renderedImageIncludesOverlays ? exportResult.RenderedFileName : null,
            sourceImageSaved,
            sourceImageSaved ? exportResult.SourceFileName : null);
    }

    internal static bool AreSameWindowsFilePath(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        try
        {
            string normalizedLeft = Path.GetFullPath(left)
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            string normalizedRight = Path.GetFullPath(right)
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}

internal sealed class ResultImagePlaceholderCache
{
    private DrawingImage? _source;

    public int Width { get; private set; }
    public int Height { get; private set; }

    public DrawingImage GetOrCreate(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        if (_source == null || Width != width || Height != height)
        {
            _source = ImageUtils.CreateSolidColorDrawing(width, height, Colors.White);
            Width = width;
            Height = height;
        }

        return _source;
    }

    public bool IsCurrent(ImageSource? source, int width, int height)
    {
        return Width == width && Height == height && ReferenceEquals(_source, source);
    }
}

internal static class ResultImageDimensions
{
    public static bool IsValid(int? width, int? height)
    {
        return width > 0 && height > 0;
    }

    public static bool TryPopulateFromFile(ProjectARVRReuslt result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (IsValid(result.ImageWidth, result.ImageHeight))
            return false;

        if (!TryReadFromFile(result.FileName, out int width, out int height))
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
            if (CVFileUtil.IsCIEFile(filePath))
            {
                int headerEnd = CVFileUtil.ReadCIEFileHeader(filePath, out CVCIEFile fileInfo);
                using (fileInfo)
                {
                    width = fileInfo.Cols;
                    height = fileInfo.Rows;
                    return headerEnd > 0 && width > 0 && height > 0;
                }
            }

            using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation | BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
            BitmapFrame frame = decoder.Frames[0];
            width = frame.PixelWidth;
            height = frame.PixelHeight;
            return width > 0 && height > 0;
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
            if (exact != null && TryReadFrameInfo(exact.ImgFrameInfo, out width, out height))
                return true;

            (int Width, int Height)[] sizes = images
                .Select(item => TryReadFrameInfo(item.ImgFrameInfo, out int itemWidth, out int itemHeight)
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

    public static bool TryReadFrameInfo(string? json, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            JObject frameInfo = JObject.Parse(json);
            width = ReadPositiveInt(frameInfo, "width");
            height = ReadPositiveInt(frameInfo, "height");
            return width > 0 && height > 0;
        }
        catch
        {
            width = 0;
            height = 0;
            return false;
        }
    }

    private static int ReadPositiveInt(JObject value, string propertyName)
    {
        JProperty? property = value.Properties().FirstOrDefault(item => string.Equals(item.Name, propertyName, StringComparison.OrdinalIgnoreCase));
        return property?.Value.Type == JTokenType.Integer && property.Value.Value<int>() > 0
            ? property.Value.Value<int>()
            : 0;
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
