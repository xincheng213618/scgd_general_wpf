using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.FileIO;
using ColorVision.ImageEditor;
using Newtonsoft.Json.Linq;
using ProjectARVRPro.ImageExport;
using System.Diagnostics;
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

internal readonly record struct ResultImageFilePaths(string? Original, string? SavedSource, string? SavedResult)
{
    public static ResultImageFilePaths Capture(ProjectARVRReuslt result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new(result.FileName, result.SavedSourceImageFileName, result.SavedResultImageFileName);
    }
}

internal static class ResultImageFileCandidates
{
    public static IReadOnlyList<ResultImageFileCandidate> GetExisting(
        ProjectARVRReuslt result,
        Func<string, bool>? fileExists = null) => GetExisting(ResultImageFilePaths.Capture(result), fileExists, default);

    public static Task<IReadOnlyList<ResultImageFileCandidate>> GetExistingAsync(
        ResultImageFilePaths paths,
        CancellationToken cancellationToken,
        Func<string, bool>? fileExists = null) => Task.Run(() => GetExisting(paths, fileExists, cancellationToken), cancellationToken);

    private static IReadOnlyList<ResultImageFileCandidate> GetExisting(
        ResultImageFilePaths filePaths,
        Func<string, bool>? fileExists,
        CancellationToken cancellationToken)
    {
        fileExists ??= path => CVFileReadCache.GetCachedLength(path).HasValue || File.Exists(path);

        List<ResultImageFileCandidate> candidates = [];
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        Add(filePaths.Original, ResultImageFileKind.Original);
        Add(filePaths.SavedSource, ResultImageFileKind.SavedSource);
        Add(filePaths.SavedResult, ResultImageFileKind.SavedResult);
        cancellationToken.ThrowIfCancellationRequested();
        return candidates;

        void Add(string? filePath, ResultImageFileKind kind)
        {
            cancellationToken.ThrowIfCancellationRequested();
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

internal static class ResultImageLoader
{
    private static readonly TimeSpan OpenTimeout = TimeSpan.FromSeconds(30);

    public static BitmapSource? GetCurrentBitmap(ImageView imageView) =>
        imageView.ViewBitmapSource as BitmapSource ?? imageView.ImageShow.Source as BitmapSource;

    public static async Task<BitmapSource?> OpenAsync(ImageView imageView, string filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool samePath = IsRequestedPath();
        Task openCompletion = imageView.ImageOpenCompletion;
        if (samePath && openCompletion.IsCompletedSuccessfully && GetCurrentBitmap(imageView) is BitmapSource currentSource)
            return currentSource;

        TaskCompletionSource<ImageViewImageSourceLoadedEventArgs> imageLoaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<ImageViewImageSourceLoadedEventArgs> onImageLoaded = (_, e) => imageLoaded.TrySetResult(e);
        imageView.ImageSourceLoaded += onImageLoaded;
        try
        {
            if (!samePath || openCompletion.IsCompleted)
            {
                imageView.OpenImage(filePath);
                openCompletion = imageView.ImageOpenCompletion;
            }

            if (!IsRequestedPath())
                return null;

            long waitStarted = Stopwatch.GetTimestamp();
            // Older openers can return from OpenImage before posting their pixels.
            if (!openCompletion.IsCompletedSuccessfully
                && await Task.WhenAny(openCompletion, imageLoaded.Task).WaitAsync(OpenTimeout, cancellationToken) == openCompletion)
            {
                await openCompletion;
            }

            TimeSpan remaining = OpenTimeout - Stopwatch.GetElapsedTime(waitStarted);
            ImageViewImageSourceLoadedEventArgs loaded = await imageLoaded.Task.WaitAsync(
                remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return IsRequestedPath() && imageView.IsCurrentImageRevision(loaded.ImageRevision)
                ? loaded.Source as BitmapSource
                : null;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new IOException("The image opener canceled before publishing the result image.", ex);
        }
        finally
        {
            imageView.ImageSourceLoaded -= onImageLoaded;
        }

        bool IsRequestedPath() => string.Equals(filePath, imageView.Config.GetProperties<string>(ImageViewPropertyKeys.FilePath), StringComparison.OrdinalIgnoreCase);
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
    internal static bool TryPopulate(
        ProjectARVRReuslt result,
        Func<int, List<MeasureResultImgModel>>? measureResultLoader = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.ImageWidth is > 0 && result.ImageHeight is > 0)
            return true;
        if (result.BatchId <= 0)
            return false;

        try
        {
            measureResultLoader ??= batchId => MeasureImgResultDao.Instance.GetAllByBatchId(batchId);
            List<MeasureResultImgModel> images = measureResultLoader(result.BatchId) ?? [];
            if (!TryReadFromMeasureResults(images, result.FileName, out int width, out int height))
                return false;

            result.ImageWidth = width;
            result.ImageHeight = height;
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static bool TryReadFromMeasureResults(
        IReadOnlyList<MeasureResultImgModel> images,
        string? expectedFilePath,
        out int width,
        out int height)
    {
        width = 0;
        height = 0;
        if (images == null || images.Count == 0)
            return false;

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
