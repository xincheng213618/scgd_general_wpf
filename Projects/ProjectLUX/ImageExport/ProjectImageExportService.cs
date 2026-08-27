using ColorVision.ImageEditor;
using System.IO;

namespace ProjectLUX.ImageExport;

internal sealed class ProjectImageExportAttemptResult
{
    public string? RenderedFileName { get; internal set; }
    public string? SourceFileName { get; internal set; }
}

internal sealed class ProjectImageExportAttempt : IDisposable
{
    private readonly string? renderedFileName;
    private readonly string? sourceFileName;

    public string? RenderedStagingFileName { get; }
    public string? SourceStagingFileName { get; }

    public ProjectImageExportAttempt(string? renderedFileName, string? sourceFileName)
    {
        this.renderedFileName = NormalizeTargetFileName(renderedFileName);
        this.sourceFileName = NormalizeTargetFileName(sourceFileName);
        RenderedStagingFileName = CreateUniqueStagingFileName(this.renderedFileName);
        SourceStagingFileName = CreateUniqueStagingFileName(this.sourceFileName);
    }

    public ImageViewSnapshotExportOptions CreateOptions(
        ImageViewSnapshotSaveOptions renderedOptions,
        ImageViewSourceSaveOptions sourceOptions)
    {
        return new ImageViewSnapshotExportOptions
        {
            RenderedFileName = RenderedStagingFileName,
            RenderedOptions = renderedOptions,
            SourceFileName = SourceStagingFileName,
            SourceOptions = sourceOptions,
        };
    }

    public ProjectImageExportAttemptResult CommitSuccessfulChannels(
        Action<string, string, Exception>? onFailure = null)
    {
        ProjectImageExportAttemptResult result = new();
        PromoteIfWritten(
            "结果图",
            RenderedStagingFileName,
            renderedFileName,
            fileName => result.RenderedFileName = fileName,
            onFailure);
        PromoteIfWritten(
            "原图",
            SourceStagingFileName,
            sourceFileName,
            fileName => result.SourceFileName = fileName,
            onFailure);
        return result;
    }

    public void Dispose()
    {
        DeleteStagingFile(RenderedStagingFileName);
        DeleteStagingFile(SourceStagingFileName);
    }

    private static string? NormalizeTargetFileName(string? fileName) =>
        string.IsNullOrWhiteSpace(fileName) ? null : Path.GetFullPath(fileName);

    private static string? CreateUniqueStagingFileName(string? targetFileName)
    {
        if (targetFileName == null)
            return null;

        string directory = Path.GetDirectoryName(targetFileName)
            ?? throw new ArgumentException("导出文件必须包含有效目录。", nameof(targetFileName));
        return Path.Combine(
            directory,
            $".{Path.GetFileName(targetFileName)}.{Guid.NewGuid():N}.luxexport.tmp");
    }

    private static void PromoteIfWritten(
        string channelName,
        string? stagingFileName,
        string? targetFileName,
        Action<string> markSaved,
        Action<string, string, Exception>? onFailure)
    {
        if (stagingFileName == null || targetFileName == null || !File.Exists(stagingFileName))
            return;

        try
        {
            File.Move(stagingFileName, targetFileName, overwrite: true);
            markSaved(targetFileName);
        }
        catch (Exception ex)
        {
            onFailure?.Invoke(channelName, targetFileName, ex);
        }
    }

    private static void DeleteStagingFile(string? stagingFileName)
    {
        if (stagingFileName == null)
            return;

        try
        {
            File.Delete(stagingFileName);
        }
        catch
        {
            // Best-effort cleanup; the unique staging name cannot be mistaken for a later attempt.
        }
    }
}

/// <summary>
/// Maps ProjectLUX naming and configuration onto the reusable ImageEditor export API.
/// Pixel capture and encoding remain in ColorVision.ImageEditor.
/// </summary>
internal static class ProjectImageExportService
{
    internal const int ResultJpegQuality = 100;

    internal static string BuildOutputDirectory(
        string rootDirectory,
        bool saveByDate,
        DateTime requestedAt,
        string? serialNumber)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
            throw new ArgumentException("导出根目录不能为空。", nameof(rootDirectory));

        string directory = Path.GetFullPath(rootDirectory);
        if (saveByDate)
            directory = Path.Combine(directory, requestedAt.ToString("yyyy-MM-dd"));

        string safeSerialNumber = SanitizePathSegment(serialNumber);
        if (!string.IsNullOrWhiteSpace(safeSerialNumber))
            directory = Path.Combine(directory, safeSerialNumber);
        return directory;
    }

    internal static string BuildResultFileStem(string sourceFileName, string? model)
    {
        string sourceStem = SanitizePathSegment(Path.GetFileNameWithoutExtension(sourceFileName), "image");
        string modelStem = SanitizePathSegment(model);
        return $"{sourceStem}_{modelStem}result";
    }

    internal static string BuildSourceFileStem(string sourceFileName, string? model)
    {
        string sourceStem = SanitizePathSegment(Path.GetFileNameWithoutExtension(sourceFileName), "image");
        string modelStem = SanitizePathSegment(model);
        return $"{sourceStem}_{modelStem}source";
    }

    internal static string GetResultExtension(ResultImageFormat format) => format switch
    {
        ResultImageFormat.PNG => ".png",
        ResultImageFormat.JPEG => ".jpg",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "不支持的标记图格式。"),
    };

    internal static string GetSourceExtension(SourceImageFormat format) => format switch
    {
        SourceImageFormat.PNG => ".png",
        SourceImageFormat.TIFF => ".tif",
        SourceImageFormat.BMP => ".bmp",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "不支持的原图格式。"),
    };

    internal static int GetScaleDivisor(ImageExportSize size) => size switch
    {
        ImageExportSize.二分之一尺寸 => 2,
        ImageExportSize.四分之一尺寸 => 4,
        _ => 1,
    };

    internal static ImageViewSnapshotSaveOptions CreateRenderedOptions(
        ResultImageFormat format,
        ImageExportSize size)
    {
        return new ImageViewSnapshotSaveOptions
        {
            Format = format == ResultImageFormat.JPEG
                ? ImageViewSnapshotFormat.Jpeg
                : ImageViewSnapshotFormat.Png,
            ScaleDivisor = GetScaleDivisor(size),
            JpegQuality = ResultJpegQuality,
        };
    }

    internal static ImageViewSourceSaveOptions CreateSourceOptions(
        SourceImageFormat format,
        SourceTiffCompression tiffCompression)
    {
        return new ImageViewSourceSaveOptions
        {
            Format = format switch
            {
                SourceImageFormat.TIFF => ImageViewSourceFormat.Tiff,
                SourceImageFormat.BMP => ImageViewSourceFormat.Bmp,
                _ => ImageViewSourceFormat.Png,
            },
            TiffCompression = tiffCompression == SourceTiffCompression.ZIP
                ? ImageViewTiffCompression.Zip
                : ImageViewTiffCompression.Lzw,
        };
    }

    internal static string BuildFilePath(string outputDirectory, string fileStem, string extension)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("导出目录不能为空。", nameof(outputDirectory));

        string directory = Path.GetFullPath(outputDirectory);
        return Path.Combine(directory, SanitizePathSegment(fileStem, "image") + extension);
    }

    private static string SanitizePathSegment(string? value, string fallback = "")
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        string sanitized = value.Trim();
        foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            sanitized = sanitized.Replace(invalidCharacter.ToString(), string.Empty, StringComparison.Ordinal);

        sanitized = sanitized.Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
    }
}
