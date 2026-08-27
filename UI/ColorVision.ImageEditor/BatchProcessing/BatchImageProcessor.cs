using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace ColorVision.ImageEditor.BatchProcessing
{
    public sealed class BatchImageProcessingRequest
    {
        public IReadOnlyList<BatchImageItem> Items { get; init; } = Array.Empty<BatchImageItem>();

        public BatchImageAlgorithmDefinition Algorithm { get; init; } = null!;

        public BatchOutputFormat OutputFormat { get; init; } = BatchOutputFormat.SameAsSource;

        public string? OutputDirectory { get; init; }

        public string Suffix { get; init; } = string.Empty;

        public bool PreserveFolderStructure { get; init; } = true;

        public bool AvoidOverwrite { get; init; } = true;
    }

    public sealed class BatchImageFileResult
    {
        public string SourcePath { get; init; } = string.Empty;

        public string OutputPath { get; init; } = string.Empty;

        public bool SourceRead { get; init; }

        public bool Success { get; init; }

        public bool Cancelled { get; init; }

        public string ErrorMessage { get; init; } = string.Empty;
    }

    public sealed class BatchImageProgress
    {
        public BatchImageItem Item { get; init; } = null!;

        public int Completed { get; init; }

        public int Total { get; init; }

        public string Status { get; init; } = string.Empty;

        public string? OutputPath { get; init; }
    }

    public sealed class BatchImageRunResult
    {
        public IReadOnlyList<BatchImageFileResult> Files { get; init; } = Array.Empty<BatchImageFileResult>();

        public int Succeeded => Files.Count(file => file.Success);

        public int Failed => Files.Count(file => !file.Success && !file.Cancelled);

        public bool Cancelled { get; init; }
    }

    public sealed class BatchImageProcessor
    {
        private readonly IBatchImageLoader[] _loaders;

        public BatchImageProcessor(IEnumerable<IBatchImageLoader> loaders)
        {
            ArgumentNullException.ThrowIfNull(loaders);
            _loaders = loaders
                .Where(loader => loader != null)
                .GroupBy(loader => loader.GetType())
                .Select(group => group.First())
                .ToArray();
            if (_loaders.Length == 0)
            {
                throw new ArgumentException("At least one batch image loader is required.", nameof(loaders));
            }
        }

        public IReadOnlyList<string> SupportedExtensions => _loaders
            .SelectMany(loader => loader.Extensions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(extension => extension, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        public bool IsSupported(string filePath) => GetLoader(filePath) != null;

        public BatchImageRunResult Process(
            BatchImageProcessingRequest request,
            Action<BatchImageProgress>? reportProgress = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.Algorithm);
            if (request.Items.Count == 0)
            {
                throw new ArgumentException("At least one batch image item is required.", nameof(request));
            }
            if (request.Suffix.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException("The output suffix contains invalid file-name characters.", nameof(request));
            }

            var results = new List<BatchImageFileResult>(request.Items.Count);
            var reservedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < request.Items.Count; index++)
            {
                var item = request.Items[index];
                if (cancellationToken.IsCancellationRequested)
                {
                    return new BatchImageRunResult { Files = results, Cancelled = true };
                }

                reportProgress?.Invoke(new BatchImageProgress
                {
                    Item = item,
                    Completed = index,
                    Total = request.Items.Count,
                    Status = "处理中...",
                });

                var sourceRead = false;
                var outputPath = string.Empty;
                var outputExisted = false;
                try
                {
                    var loader = GetLoader(item.FilePath)
                        ?? throw new NotSupportedException($"不支持的图像格式：{Path.GetExtension(item.FilePath)}");
                    outputPath = BatchImageOutput.CreateOutputPath(
                        item,
                        request.OutputDirectory,
                        request.Suffix,
                        request.OutputFormat,
                        request.PreserveFolderStructure,
                        request.AvoidOverwrite,
                        reservedPaths);
                    outputExisted = File.Exists(outputPath);

                    using Mat source = loader.Load(item.FilePath);
                    sourceRead = true;
                    cancellationToken.ThrowIfCancellationRequested();
                    using Mat result = request.Algorithm.Apply(source, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    BatchImageOutput.Save(result, outputPath);

                    results.Add(new BatchImageFileResult
                    {
                        SourcePath = item.FilePath,
                        OutputPath = outputPath,
                        SourceRead = true,
                        Success = true,
                    });
                    reportProgress?.Invoke(new BatchImageProgress
                    {
                        Item = item,
                        Completed = index + 1,
                        Total = request.Items.Count,
                        Status = "完成",
                        OutputPath = outputPath,
                    });
                }
                catch (OperationCanceledException)
                {
                    DeleteIncompleteNewOutput(outputPath, outputExisted);
                    results.Add(new BatchImageFileResult
                    {
                        SourcePath = item.FilePath,
                        OutputPath = outputPath,
                        SourceRead = sourceRead,
                        Cancelled = true,
                        ErrorMessage = "Processing was cancelled.",
                    });
                    reportProgress?.Invoke(new BatchImageProgress
                    {
                        Item = item,
                        Completed = index,
                        Total = request.Items.Count,
                        Status = "已取消",
                    });
                    return new BatchImageRunResult { Files = results, Cancelled = true };
                }
                catch (Exception ex)
                {
                    DeleteIncompleteNewOutput(outputPath, outputExisted);
                    results.Add(new BatchImageFileResult
                    {
                        SourcePath = item.FilePath,
                        OutputPath = outputPath,
                        SourceRead = sourceRead,
                        ErrorMessage = ex.Message,
                    });
                    reportProgress?.Invoke(new BatchImageProgress
                    {
                        Item = item,
                        Completed = index + 1,
                        Total = request.Items.Count,
                        Status = $"失败：{ex.Message}",
                    });
                }
            }

            return new BatchImageRunResult { Files = results };
        }

        private IBatchImageLoader? GetLoader(string filePath)
        {
            var extension = Path.GetExtension(filePath);
            return _loaders.FirstOrDefault(loader => loader.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase));
        }

        private static void DeleteIncompleteNewOutput(string outputPath, bool outputExisted)
        {
            if (outputExisted || string.IsNullOrWhiteSpace(outputPath) || !File.Exists(outputPath))
            {
                return;
            }

            try
            {
                File.Delete(outputPath);
            }
            catch
            {
            }
        }
    }
}
