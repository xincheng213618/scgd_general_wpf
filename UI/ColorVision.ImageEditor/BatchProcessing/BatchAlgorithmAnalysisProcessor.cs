using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ImageEditor.BatchProcessing
{
    public enum BatchAnalysisExportFormat
    {
        Json,
        CsvBundle,
    }

    public sealed class BatchAlgorithmAnalysisRequest
    {
        public IReadOnlyList<BatchImageItem> Items { get; init; } = Array.Empty<BatchImageItem>();
        public required AlgorithmInvocation Invocation { get; init; }
        public string? OutputDirectory { get; init; }
        public string Suffix { get; init; } = "_analysis";
        public bool PreserveFolderStructure { get; init; } = true;
        public bool AvoidOverwrite { get; init; } = true;
        public BatchAnalysisExportFormat ExportFormat { get; init; } = BatchAnalysisExportFormat.Json;
    }

    public sealed record BatchAlgorithmAnalysisFileResult(
        string SourcePath,
        IReadOnlyList<string> OutputPaths,
        AlgorithmResultStatus Status,
        string ErrorMessage = "");

    public sealed class BatchAlgorithmAnalysisResult
    {
        public IReadOnlyList<BatchAlgorithmAnalysisFileResult> Files { get; init; } = Array.Empty<BatchAlgorithmAnalysisFileResult>();
        public bool Cancelled { get; init; }
        public int Succeeded => Files.Count(file => file.Status == AlgorithmResultStatus.Succeeded);
        public int Failed => Files.Count(file => file.Status == AlgorithmResultStatus.Failed);
    }

    /// <summary>Catalog/Runner batch adapter for structured analysis results; image-format conversion stays in the pixel batch processor.</summary>
    public sealed class BatchAlgorithmAnalysisProcessor
    {
        private readonly IBatchImageLoader[] _loaders;

        public BatchAlgorithmAnalysisProcessor(IEnumerable<IBatchImageLoader> loaders)
        {
            ArgumentNullException.ThrowIfNull(loaders);
            _loaders = loaders.Where(loader => loader != null).GroupBy(loader => loader.GetType()).Select(group => group.First()).ToArray();
            if (_loaders.Length == 0) throw new ArgumentException("At least one batch image loader is required.", nameof(loaders));
        }

        public async Task<BatchAlgorithmAnalysisResult> ProcessAsync(
            BatchAlgorithmAnalysisRequest request,
            IProgress<BatchImageProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.Items.Count == 0) throw new ArgumentException("At least one batch image item is required.", nameof(request));
            if (request.Suffix.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("The output suffix contains invalid file-name characters.", nameof(request));

            List<BatchAlgorithmAnalysisFileResult> files = new(request.Items.Count);
            for (int index = 0; index < request.Items.Count; index++)
            {
                BatchImageItem item = request.Items[index];
                if (cancellationToken.IsCancellationRequested)
                    return new BatchAlgorithmAnalysisResult { Files = files, Cancelled = true };
                progress?.Report(new BatchImageProgress { Item = item, Completed = index, Total = request.Items.Count, Status = "分析中..." });
                try
                {
                    IBatchImageLoader loader = GetLoader(item.FilePath)
                        ?? throw new NotSupportedException($"不支持的图像格式：{Path.GetExtension(item.FilePath)}");
                    using Mat source = loader.Load(item.FilePath);
                    AlgorithmInvocation invocation = CloneInvocation(request.Invocation, item.FilePath);
                    AlgorithmImageBuffer input = AlgorithmImageInterop.FromMat(source);
                    using AlgorithmResult result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
                    {
                        Invocation = invocation,
                        Inputs =
                        [
                            new AlgorithmInput
                            {
                                Name = "source",
                                Image = input,
                                Ownership = AlgorithmInputOwnership.Transferred,
                                SourceUri = item.FilePath,
                            },
                        ],
                        RequiredCapabilities = AlgorithmHostCapabilities.Batch | AlgorithmHostCapabilities.Headless
                            | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Roi,
                    }, cancellationToken);
                    if (result.Status == AlgorithmResultStatus.Cancelled)
                    {
                        files.Add(new BatchAlgorithmAnalysisFileResult(item.FilePath, Array.Empty<string>(), result.Status, "Processing was cancelled."));
                        return new BatchAlgorithmAnalysisResult { Files = files, Cancelled = true };
                    }
                    if (result.Status != AlgorithmResultStatus.Succeeded)
                    {
                        string failure = string.Join("; ", result.Failures.Select(item => $"{item.Code}: {item.Message}"));
                        files.Add(new BatchAlgorithmAnalysisFileResult(item.FilePath, Array.Empty<string>(), result.Status, failure));
                        continue;
                    }

                    string outputPath = CreateOutputPath(item, request);
                    IReadOnlyList<string> outputPaths = request.ExportFormat == BatchAnalysisExportFormat.Json
                        ? [AlgorithmResultExporter.ExportJson(result, outputPath, overwrite: !request.AvoidOverwrite)]
                        : AlgorithmResultExporter.ExportCsvBundle(result, outputPath, overwrite: !request.AvoidOverwrite);
                    files.Add(new BatchAlgorithmAnalysisFileResult(item.FilePath, outputPaths, result.Status));
                    progress?.Report(new BatchImageProgress
                    {
                        Item = item,
                        Completed = index + 1,
                        Total = request.Items.Count,
                        Status = "完成",
                        OutputPath = outputPaths[0],
                    });
                }
                catch (OperationCanceledException)
                {
                    files.Add(new BatchAlgorithmAnalysisFileResult(item.FilePath, Array.Empty<string>(), AlgorithmResultStatus.Cancelled, "Processing was cancelled."));
                    return new BatchAlgorithmAnalysisResult { Files = files, Cancelled = true };
                }
                catch (Exception exception)
                {
                    files.Add(new BatchAlgorithmAnalysisFileResult(item.FilePath, Array.Empty<string>(), AlgorithmResultStatus.Failed, exception.Message));
                }
            }
            return new BatchAlgorithmAnalysisResult { Files = files };
        }

        private IBatchImageLoader? GetLoader(string filePath)
        {
            string extension = Path.GetExtension(filePath);
            return _loaders.FirstOrDefault(loader => loader.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase));
        }

        private static AlgorithmInvocation CloneInvocation(AlgorithmInvocation source, string inputPath)
            => new()
            {
                InvocationId = Guid.NewGuid(),
                AlgorithmId = source.AlgorithmId,
                AlgorithmVersion = source.AlgorithmVersion,
                ParameterSchemaVersion = source.ParameterSchemaVersion,
                Parameters = source.Parameters.ValueKind is JsonValueKind.Undefined ? default : source.Parameters.Clone(),
                Inputs = [new AlgorithmInputReference("source", inputPath)],
                Roi = source.Roi,
                PresetId = source.PresetId,
                Metadata = source.Metadata,
            };

        private static string CreateOutputPath(BatchImageItem item, BatchAlgorithmAnalysisRequest request)
        {
            string directory = request.OutputDirectory ?? Path.GetDirectoryName(item.FilePath)!;
            if (request.PreserveFolderStructure
                && !string.IsNullOrWhiteSpace(request.OutputDirectory)
                && !string.IsNullOrWhiteSpace(item.SourceRoot))
            {
                string relative = Path.GetRelativePath(Path.GetFullPath(item.SourceRoot), Path.GetDirectoryName(Path.GetFullPath(item.FilePath))!);
                if (relative != "." && !relative.StartsWith("..", StringComparison.Ordinal)) directory = Path.Combine(directory, relative);
            }
            Directory.CreateDirectory(directory);
            string extension = request.ExportFormat == BatchAnalysisExportFormat.Json ? ".json" : ".csv";
            return Path.Combine(directory, Path.GetFileNameWithoutExtension(item.FilePath) + request.Suffix + extension);
        }
    }
}
