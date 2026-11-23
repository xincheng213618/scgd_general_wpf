using ColorVision.ImageEditor;
using System.IO;
using System.Text.RegularExpressions;

namespace ProjectARVRPro.ImageExport;

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

    public ProjectImageExportAttempt(string? renderedFileName, string? sourceFileName, string? inputFileName = null)
    {
        string? inputPath = NormalizeTargetFileName(inputFileName);
        this.renderedFileName = ResolveExportPath(NormalizeTargetFileName(renderedFileName), "_result", inputPath, otherExportPath: null);
        this.sourceFileName = ResolveExportPath(NormalizeTargetFileName(sourceFileName), "_source", inputPath, this.renderedFileName);
        RenderedStagingFileName = CreateUniqueStagingFileName(this.renderedFileName);
        SourceStagingFileName = CreateUniqueStagingFileName(this.sourceFileName);
    }

    public ImageViewSnapshotExportOptions CreateOptions(
        ImageViewSnapshotSaveOptions renderedOptions,
        ImageViewSourceSaveOptions sourceOptions,
        string? diagnosticContext = null)
    {
        return new ImageViewSnapshotExportOptions
        {
            RenderedFileName = RenderedStagingFileName,
            RenderedOptions = renderedOptions,
            SourceFileName = SourceStagingFileName,
            SourceOptions = sourceOptions,
            DiagnosticContext = diagnosticContext,
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

    private static string? ResolveExportPath(string? path, string suffix, string? inputPath, string? otherExportPath)
    {
        if (path == null) return null;
        while (MatchesInputFile(path, inputPath) || IsSameFileEntry(path, otherExportPath))
        {
            string extension = Path.GetExtension(path);
            path = path[..(path.Length - extension.Length)] + suffix + extension;
        }
        return path;
    }

    private static bool MatchesInputFile(string path, string? inputPath)
    {
        if (inputPath == null) return false;
        if (IsSameFileEntry(path, inputPath)) return true;
        if (!File.Exists(path)) return false;

        // Replacing a file link's target also replaces the pixels reachable from the input path.
        var input = new FileInfo(inputPath);
        return input.Exists && (input.Attributes & FileAttributes.ReparsePoint) != 0
            && input.ResolveLinkTarget(returnFinalTarget: true) is FileSystemInfo target
            && IsSameFileEntry(path, target.FullName);
    }

    private static bool IsSameFileEntry(string path, string? otherPath)
    {
        if (otherPath == null) return false;
        var comparer = StringComparer.OrdinalIgnoreCase;
        return comparer.Equals(path, otherPath)
            || (comparer.Equals(Path.GetFileName(path), Path.GetFileName(otherPath))
                && comparer.Equals(ResolveDirectory(Path.GetDirectoryName(path)!), ResolveDirectory(Path.GetDirectoryName(otherPath)!)));
    }

    private static string ResolveDirectory(string directory)
    {
        if (Path.GetDirectoryName(directory) is not string parent) return directory;
        // Resolve ancestors before the last segment, including ordinary directories beneath a junction.
        string resolved = Path.Combine(ResolveDirectory(parent), Path.GetFileName(directory));
        var info = new DirectoryInfo(resolved);
        return info.Exists && (info.Attributes & FileAttributes.ReparsePoint) != 0
            && info.ResolveLinkTarget(returnFinalTarget: true) is FileSystemInfo target
            ? ResolveDirectory(Path.TrimEndingDirectorySeparator(target.FullName))
            : resolved;
    }

    private static string? CreateUniqueStagingFileName(string? targetFileName)
    {
        if (targetFileName == null)
            return null;

        string directory = Path.GetDirectoryName(targetFileName)
            ?? throw new ArgumentException("导出文件必须包含有效目录。", nameof(targetFileName));
        return Path.Combine(
            directory,
            $".{Path.GetFileName(targetFileName)}.{Guid.NewGuid():N}.arvrexport.tmp");
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
            // Best-effort cleanup; the unique staging name can never be mistaken for a later attempt.
        }
    }
}

/// <summary>
/// Maps ProjectARVRPro naming and configuration onto the reusable ImageEditor export API.
/// Pixel capture and encoding intentionally remain in ColorVision.ImageEditor.
/// </summary>
internal static class ProjectImageExportService
{
    internal const int ResultJpegQuality = 100;
    internal const string DefaultResultFileNameTemplate = "{FlowName}";
    internal const string DefaultSourceFileNameTemplate = "{FlowName}_source";
    private static readonly Regex FileNameToken = new(@"\{(?:FlowName|SN)\}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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

    internal static (string Rendered, string Source) BuildFileStems(
        string sourceFileName, string? model, string? serialNumber, string? resultTemplate, string? sourceTemplate)
    {
        string sourceStem = SanitizePathSegment(Path.GetFileNameWithoutExtension(sourceFileName), "image");
        string modelStem = SanitizePathSegment(model);
        string flowName = modelStem.Length == 0 ? "image" : modelStem;
        string sn = SanitizePathSegment(serialNumber);
        string rendered = resultTemplate == null
            ? $"{sourceStem}_{modelStem}result"
            : ExpandFileNameTemplate(resultTemplate, DefaultResultFileNameTemplate, flowName, sn);
        string source = sourceTemplate == null
            ? $"{sourceStem}_{modelStem}source"
            : ExpandFileNameTemplate(sourceTemplate, DefaultSourceFileNameTemplate, flowName, sn);
        return (rendered, source);
    }

    internal static bool IsValidFileNameTemplate(string? template)
    {
        if (string.IsNullOrWhiteSpace(template))
            return false;

        string sample = FileNameToken.Replace(template.Trim(), "image");
        return sample.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && !sample.Contains('{') && !sample.Contains('}')
            && !sample.EndsWith('.') && Path.GetExtension(sample).ToLowerInvariant() is not (".png" or ".jpg" or ".jpeg" or ".tif" or ".tiff" or ".bmp");
    }

    private static string ExpandFileNameTemplate(string template, string defaultTemplate, string flowName, string serialNumber)
    {
        if (!IsValidFileNameTemplate(template))
            template = defaultTemplate;

        string expanded = FileNameToken.Replace(template,
            match => string.Equals(match.Value, "{SN}", StringComparison.OrdinalIgnoreCase) ? serialNumber : flowName);
        return SanitizePathSegment(expanded, "image");
    }

    internal static string BuildSourceFilePath(string outputDirectory, string fileStem, string extension, string? renderedFilePath)
    {
        string sourcePath = BuildFilePath(outputDirectory, fileStem, extension);
        return string.Equals(sourcePath, renderedFilePath, StringComparison.OrdinalIgnoreCase)
            ? BuildFilePath(outputDirectory, $"{fileStem}_source", extension)
            : sourcePath;
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
        ImageExportSize size,
        bool useHeatmap = false)
    {
        return new ImageViewSnapshotSaveOptions
        {
            Format = format == ResultImageFormat.JPEG
                ? ImageViewSnapshotFormat.Jpeg
                : ImageViewSnapshotFormat.Png,
            ScaleDivisor = GetScaleDivisor(size),
            JpegQuality = ResultJpegQuality,
            UseHeatmap = useHeatmap,
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

    internal static string BuildDiagnosticContext(
        string? serialNumber,
        int resultId,
        int batchId,
        string? model)
    {
        return $"Task=ProjectARVRPro.ImageExport SN={NormalizeDiagnosticValue(serialNumber)} "
            + $"ResultId={resultId} BatchId={batchId} Model={NormalizeDiagnosticValue(model)}";
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

    private static string NormalizeDiagnosticValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "-";
        return value.Replace('\r', ' ').Replace('\n', ' ').Trim();
    }
}
