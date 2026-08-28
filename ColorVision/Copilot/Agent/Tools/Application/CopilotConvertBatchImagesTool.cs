using ColorVision.Algorithms;
using ColorVision.Engine.Media;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.BatchProcessing;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotConvertBatchImagesTool :
        ICopilotFrameworkApprovedTool,
        ICopilotFrameworkApprovedProgressReportingTool,
        ICopilotFrameworkApprovalPresentation,
        ICopilotAgentDrivenTool
    {
        private const int MaximumFiles = 500;
        private const int MaximumResultRows = 100;
        private static readonly JsonSerializerOptions ResultJsonOptions = new() { WriteIndented = true };
        private static readonly EnumerationOptions DirectoryEnumerationOptions = new()
        {
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false,
        };
        private static readonly CopilotToolInputSchema Schema = CopilotToolInputSchema.FromJsonSchema(
            JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["sources"] = new
                    {
                        type = "array",
                        minItems = 1,
                        maxItems = 32,
                        description = "Absolute or workspace-relative source image files or directories explicitly in the current local scope.",
                        items = new { type = "string", minLength = 1, maxLength = 4096 },
                    },
                    ["outputDirectory"] = new { type = "string", maxLength = 4096, description = "Optional output directory within the approved local scope. Omit to write beside each source." },
                    ["format"] = new { type = "string", @enum = new[] { "same-as-source", "tiff", "png", "jpeg", "bmp", "webp" }, description = "Output format. Defaults to same-as-source; CVRAW and CVCIE then become TIFF." },
                    ["algorithm"] = new { type = "string", maxLength = 128, description = "Optional stable Catalog algorithm ID or compatibility alias. Only the explicit local, headless, deterministic Copilot whitelist is accepted." },
                    ["parameters"] = CreateAlgorithmParametersSchema(),
                    ["recursive"] = new { type = "boolean", description = "Recursively enumerate source directories. Defaults to false." },
                    ["preserveFolderStructure"] = new { type = "boolean", description = "Preserve subdirectories below each source root when outputDirectory is set. Defaults to true." },
                    ["suffix"] = new { type = "string", maxLength = 64, description = "Optional suffix inserted before the output extension." },
                },
                ["required"] = new[] { "sources" },
                ["additionalProperties"] = false,
            }));

        private static object CreateAlgorithmParametersSchema()
        {
            string[] operations =
            [
                "Erode", "Dilate", "Open", "Close", "Gradient", "TopHat", "BlackHat",
                "Bilateral", "MeanBlur",
            ];
            string[] colorMaps =
            [
                "Autumn", "Bone", "Jet", "Winter", "Rainbow", "Ocean", "Summer", "Spring", "Cool", "Hsv", "Pink", "Hot",
                "Parula", "Magma", "Inferno", "Plasma", "Viridis", "Cividis", "Twilight", "TwilightShifted", "Turbo", "DeepGreen",
            ];
            return new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["description"] = "Optional parameters for the selected algorithm. Unknown or unrelated fields are rejected after Catalog resolution.",
                ["$comment"] = "The current ColorVision tool-schema dialect does not support JSON Schema if/then/else. Threshold's conditional useNominalRange contract is documented on both fields and enforced by the Catalog/runtime validator.",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["lowThreshold"] = new { type = "number", minimum = 0, maximum = 255 },
                    ["highThreshold"] = new { type = "number", minimum = 0, maximum = 255 },
                    ["apertureSize"] = new { type = "integer", @enum = new[] { 3, 5, 7 } },
                    ["l2Gradient"] = new { type = "boolean" },
                    ["exposure"] = new { type = "number", minimum = -5, maximum = 5 },
                    ["brightness"] = new { type = "number", minimum = -100, maximum = 100 },
                    ["contrast"] = new { type = "number", minimum = -100, maximum = 100 },
                    ["gamma"] = new { type = "number", minimum = 0.1, maximum = 5 },
                    ["threshold"] = new
                    {
                        type = "number",
                        minimum = 0,
                        maximum = ThresholdParameters.MaximumAbsoluteThreshold,
                        description = "Threshold mode is selected by useNominalRange. When true, use the 0..255 nominal scale and ColorVision scales it to the input format. When false, use raw 0..65535 DN, but the value must still fit every input format (for example Gray8 255, Gray16 65535, Gray32Float 1). The Catalog/runtime performs the final format-aware validation.",
                    },
                    ["kernelSize"] = new { type = "integer", minimum = 1, maximum = 255 },
                    ["sigma"] = new { type = "number", minimum = 0, maximum = 1000 },
                    ["operation"] = new { type = "string", @enum = operations },
                    ["iterations"] = new { type = "integer", minimum = 1, maximum = 100 },
                    ["useNominalColorSigma"] = new { type = "boolean" },
                    ["sigmaColor"] = new { type = "number", minimum = 0, maximum = 10000 },
                    ["sigmaSpace"] = new { type = "number", minimum = 0, maximum = 10000 },
                    ["redScale"] = new { type = "number", minimum = 0, maximum = 16 },
                    ["greenScale"] = new { type = "number", minimum = 0, maximum = 16 },
                    ["blueScale"] = new { type = "number", minimum = 0, maximum = 16 },
                    ["useNominalRange"] = new
                    {
                        type = "boolean",
                        description = "Defaults to true. True selects the 0..255 nominal scale; false selects raw DN up to 65535. The runtime rejects values outside the selected mode and actual input-format range.",
                    },
                    ["colormap"] = new { type = "string", @enum = colorMaps },
                    ["minimum"] = new { type = "integer", minimum = 0, maximum = uint.MaxValue },
                    ["maximum"] = new { type = "integer", minimum = 0, maximum = uint.MaxValue },
                    ["channel"] = new { type = "integer", minimum = -1, maximum = 3 },
                    ["autoRange"] = new { type = "boolean" },
                    ["dataMinimum"] = new { type = "integer", minimum = 0, maximum = uint.MaxValue },
                    ["dataMaximum"] = new { type = "integer", minimum = 0, maximum = uint.MaxValue },
                },
                ["additionalProperties"] = false,
            };
        }

        private readonly BatchImageProcessor _processor;
        private readonly AlgorithmRuntime _algorithmRuntime;

        public CopilotConvertBatchImagesTool()
            : this(new BatchImageProcessor([
                new StandardBatchImageLoader(),
                new CVRawBatchImageLoader(),
            ]), ImageAlgorithmPlatform.Runtime)
        {
        }

        public CopilotConvertBatchImagesTool(BatchImageProcessor processor)
            : this(processor, ImageAlgorithmPlatform.Runtime)
        {
        }

        public CopilotConvertBatchImagesTool(BatchImageProcessor processor, AlgorithmRuntime algorithmRuntime)
        {
            _processor = processor ?? throw new ArgumentNullException(nameof(processor));
            _algorithmRuntime = algorithmRuntime ?? throw new ArgumentNullException(nameof(algorithmRuntime));
        }

        public string Name => "ConvertBatchImages";

        public string Description => "Convert or run one explicitly whitelisted local, headless, deterministic Catalog algorithm on up to 500 approved local images. This never overwrites an existing file and returns per-file output or failure evidence.";

        public CopilotToolCapabilityDescriptor Capability { get; } = CopilotToolCapabilityDescriptor.ProtectedWrite(
            CopilotToolIdempotency.NonIdempotent,
            executionTimeout: TimeSpan.FromMinutes(10),
            auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
            allowsTemporaryFullAccess: false);

        public CopilotToolInputSchema InputSchema => Schema;

        public bool CanHandle(CopilotAgentRequest request) => IsAvailable(request);

        public bool IsAvailable(CopilotAgentRequest request)
        {
            return CopilotToolIntentPolicy.NeedsBatchImageConversionExecution(request);
        }

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Failure(
                "Batch image conversion requires Microsoft Agent Framework approval.",
                "The conversion was requested without approval for the exact sources, output directory, and format.",
                CopilotToolFailureKind.Authorization));
        }

        Task<CopilotToolResult> ICopilotFrameworkApprovedTool.ExecuteApprovedAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return ExecuteApprovedCoreAsync(request, toolInput, progress: null, cancellationToken);
        }

        Task<CopilotToolResult> ICopilotFrameworkApprovedProgressReportingTool.ExecuteApprovedWithProgressAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CopilotToolProgressContext progress,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(progress);
            return ExecuteApprovedCoreAsync(request, toolInput, progress, cancellationToken);
        }

        private async Task<CopilotToolResult> ExecuteApprovedCoreAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CopilotToolProgressContext? progress,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            progress?.Report("正在检查批量图像输入");
            if (!TryParseOptions(toolInput, out var options, out var parseError))
            {
                return Failure("Batch image conversion arguments are invalid.", parseError, CopilotToolFailureKind.Validation);
            }
            if (!TryResolveItems(request, options, cancellationToken, out var items, out var skippedIdentityPaths, out var resolveError))
            {
                return Failure("Batch image conversion inputs are invalid.", resolveError, CopilotToolFailureKind.Validation);
            }
            if (!TryResolveOutputDirectory(request, options.OutputDirectory, items, out var outputDirectory, out var outputError))
            {
                return Failure("Batch image conversion output is outside the approved local scope.", outputError, CopilotToolFailureKind.Authorization);
            }
            BatchImageAlgorithmDefinition algorithm;
            if (string.IsNullOrWhiteSpace(options.Algorithm))
            {
                algorithm = BatchImageAlgorithms.CreateFormatOnly();
            }
            else if (!BatchImageAlgorithms.TryCreateForCopilot(_algorithmRuntime, options.Algorithm, options.Parameters, out BatchImageAlgorithmDefinition? selected, out string algorithmError)
                     || selected == null)
            {
                return Failure("The requested image algorithm is unavailable to Copilot.", algorithmError, CopilotToolFailureKind.Authorization);
            }
            else
            {
                algorithm = selected;
            }

            progress?.Report(
                $"已准备 {items.Length} 个待转换图像文件",
                completed: 0,
                total: items.Length,
                unit: "files");
            BatchImageRunResult result;
            try
            {
                result = items.Length == 0
                    ? new BatchImageRunResult()
                    : await Task.Run(() => _processor.Process(
                        new BatchImageProcessingRequest
                        {
                            Items = items,
                            Algorithm = algorithm,
                            OutputFormat = options.Format,
                            OutputDirectory = outputDirectory,
                            Suffix = string.IsNullOrWhiteSpace(options.Suffix) && algorithm.Descriptor != null
                                ? algorithm.Suffix
                                : options.Suffix,
                            PreserveFolderStructure = options.PreserveFolderStructure,
                            AvoidOverwrite = true,
                        },
                        reportProgress: batchProgress => progress?.Report(CreateProgressUpdate(batchProgress)),
                        cancellationToken: cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return Failure("Batch image conversion was cancelled.", "The approved conversion was cancelled before it completed.", CopilotToolFailureKind.Cancelled);
            }
            catch (Exception ex)
            {
                return Failure("Batch image conversion failed before producing a complete result.", ex.Message, CopilotToolFailureKind.Internal);
            }

            var rows = result.Files.Take(MaximumResultRows).Select(file => new
            {
                source = file.SourcePath,
                output = string.IsNullOrWhiteSpace(file.OutputPath) ? null : file.OutputPath,
                source_read = file.SourceRead,
                success = file.Success,
                cancelled = file.Cancelled,
                error = string.IsNullOrWhiteSpace(file.ErrorMessage) ? null : file.ErrorMessage,
            });
            var content = JsonSerializer.Serialize(new
            {
                algorithm = algorithm.Descriptor?.Id.Value ?? "format-only",
                algorithm_version = algorithm.Descriptor?.Version.ToString(),
                parameter_schema_version = algorithm.Descriptor?.ParameterSchema.Version,
                requested = items.Length + skippedIdentityPaths.Length,
                processed = result.Files.Count,
                total = result.Files.Count,
                succeeded = result.Succeeded,
                failed = result.Failed,
                skipped_identity = skippedIdentityPaths.Length,
                skipped_identity_sources = skippedIdentityPaths,
                cancelled = result.Cancelled,
                results = rows,
                results_truncated = result.Files.Count > MaximumResultRows,
            }, ResultJsonOptions);
            var success = !result.Cancelled && result.Failed == 0 && result.Succeeded == items.Length;
            return new CopilotToolResult
            {
                ToolName = Name,
                Success = success,
                Summary = success
                    ? skippedIdentityPaths.Length == 0
                        ? $"Processed {result.Succeeded} image file(s) without overwriting existing outputs."
                        : $"Processed {result.Succeeded} image file(s) and skipped {skippedIdentityPaths.Length} source file(s) already in the requested format."
                    : $"Batch conversion completed with {result.Succeeded} succeeded, {result.Failed} failed, cancelled={result.Cancelled}.",
                Content = content,
                ErrorMessage = success ? string.Empty : FirstFailure(result),
                FailureKind = success ? CopilotToolFailureKind.None : result.Cancelled ? CopilotToolFailureKind.Cancelled : CopilotToolFailureKind.Unspecified,
                FailureCode = success ? string.Empty : result.Cancelled ? "batch_image_conversion_cancelled" : "batch_image_conversion_partial_failure",
                AttemptedLocalFilePaths = result.Files.Select(file => file.SourcePath).ToArray(),
                SuccessfullyReadLocalFilePaths = result.Files.Where(file => file.SourceRead).Select(file => file.SourcePath).ToArray(),
            };
        }

        private static CopilotToolProgressUpdate CreateProgressUpdate(BatchImageProgress progress)
        {
            ArgumentNullException.ThrowIfNull(progress);
            var fileName = Path.GetFileName(progress.Item?.FilePath ?? string.Empty);
            var status = progress.Status ?? string.Empty;
            var message = status switch
            {
                "处理中..." when !string.IsNullOrWhiteSpace(fileName) => $"正在转换 {fileName}",
                "完成" when !string.IsNullOrWhiteSpace(fileName) => $"已转换 {fileName}",
                "已取消" => "批量图像转换正在取消",
                _ when status.StartsWith("失败", StringComparison.Ordinal) => "一个图像转换失败，正在继续处理剩余文件",
                _ => "批量图像转换进行中",
            };
            return new CopilotToolProgressUpdate
            {
                Message = message,
                Completed = progress.Completed,
                Total = progress.Total,
                Unit = "files",
            };
        }

        public CopilotToolApprovalPresentation CreateApprovalPresentation(CopilotAgentToolInput toolInput)
        {
            if (!TryParseOptions(toolInput, out var options, out var error))
            {
                return new CopilotToolApprovalPresentation("执行批量图像转换", $"参数无效：{error}");
            }

            var sources = options.Sources.Take(5).ToArray();
            var sourceText = string.Join(Environment.NewLine, sources.Select(source => $"• {source}"));
            if (options.Sources.Count > sources.Length)
            {
                sourceText += $"{Environment.NewLine}• 另有 {options.Sources.Count - sources.Length} 个输入";
            }
            var destination = string.IsNullOrWhiteSpace(options.OutputDirectory) ? "源文件所在目录" : options.OutputDirectory;
            var algorithm = string.IsNullOrWhiteSpace(options.Algorithm) ? "仅格式转换" : options.Algorithm;
            return new CopilotToolApprovalPresentation(
                "执行批量图像处理",
                $"输入：{Environment.NewLine}{sourceText}{Environment.NewLine}算法：{algorithm}{Environment.NewLine}输出：{destination}{Environment.NewLine}格式：{FormatName(options.Format)}{Environment.NewLine}递归：{options.Recursive}{Environment.NewLine}最多处理 {MaximumFiles} 个文件；已有文件不会被覆盖。");
        }

        public string GetConcurrencyKey(CopilotAgentRequest request, CopilotAgentToolInput toolInput)
        {
            return TryParseOptions(toolInput, out var options, out _)
                ? "batch-image:" + (string.IsNullOrWhiteSpace(options.OutputDirectory) ? string.Join("|", options.Sources) : options.OutputDirectory)
                : "batch-image:invalid";
        }

        private bool TryResolveItems(
            CopilotAgentRequest request,
            ConversionOptions options,
            CancellationToken cancellationToken,
            out BatchImageItem[] items,
            out string[] skippedIdentityPaths,
            out string error)
        {
            var allowedRoots = CopilotWorkspaceSearchSupport.NormalizeSearchRoots(
                request.SearchRootPaths
                    .Concat(request.ReadableLocalDirectoryPaths)
                    .Concat(request.ReadableLocalFilePaths.Select(Path.GetDirectoryName).Where(path => !string.IsNullOrWhiteSpace(path))!));
            var resolved = new List<BatchImageItem>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in options.Sources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!CopilotWorkspaceSearchSupport.TryResolveExistingPathWithinRoots(source, allowedRoots, out var fullPath, out error))
                {
                    items = Array.Empty<BatchImageItem>();
                    skippedIdentityPaths = Array.Empty<string>();
                    return false;
                }

                if (File.Exists(fullPath))
                {
                    if (!_processor.IsSupported(fullPath))
                    {
                        items = Array.Empty<BatchImageItem>();
                        skippedIdentityPaths = Array.Empty<string>();
                        error = $"Unsupported image extension: {fullPath}";
                        return false;
                    }
                    if (seen.Add(fullPath))
                    {
                        resolved.Add(new BatchImageItem(fullPath, Path.GetDirectoryName(fullPath)));
                    }
                }
                else
                {
                    var sourceRoot = fullPath;
                    var enumerationOptions = new EnumerationOptions
                    {
                        AttributesToSkip = DirectoryEnumerationOptions.AttributesToSkip,
                        IgnoreInaccessible = DirectoryEnumerationOptions.IgnoreInaccessible,
                        RecurseSubdirectories = options.Recursive,
                        ReturnSpecialDirectories = DirectoryEnumerationOptions.ReturnSpecialDirectories,
                    };
                    foreach (var file in Directory.EnumerateFiles(fullPath, "*", enumerationOptions)
                                 .Where(_processor.IsSupported)
                                 .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (seen.Add(file))
                        {
                            resolved.Add(new BatchImageItem(Path.GetFullPath(file), sourceRoot));
                        }
                        if (resolved.Count > MaximumFiles)
                        {
                            items = Array.Empty<BatchImageItem>();
                            skippedIdentityPaths = Array.Empty<string>();
                            error = $"The batch exceeds the {MaximumFiles}-file safety limit. Narrow the sources and retry.";
                            return false;
                        }
                    }
                }
            }

            if (resolved.Count == 0)
            {
                items = Array.Empty<BatchImageItem>();
                skippedIdentityPaths = Array.Empty<string>();
                error = "No supported image files were found in the approved sources.";
                return false;
            }
            if (resolved.Count > MaximumFiles)
            {
                items = Array.Empty<BatchImageItem>();
                skippedIdentityPaths = Array.Empty<string>();
                error = $"The batch exceeds the {MaximumFiles}-file safety limit. Narrow the sources and retry.";
                return false;
            }

            skippedIdentityPaths = resolved
                .Where(item => HasIdentityOutputBesideSource(item.FilePath, options))
                .Select(item => item.FilePath)
                .ToArray();
            items = resolved
                .Where(item => !HasIdentityOutputBesideSource(item.FilePath, options))
                .ToArray();
            error = string.Empty;
            return true;
        }

        private static bool HasIdentityOutputBesideSource(string sourcePath, ConversionOptions options)
        {
            return string.IsNullOrWhiteSpace(options.Algorithm)
                && string.IsNullOrWhiteSpace(options.OutputDirectory)
                && string.IsNullOrWhiteSpace(options.Suffix)
                && string.Equals(
                    Path.GetExtension(sourcePath),
                    BatchImageOutput.ResolveExtension(sourcePath, options.Format),
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryResolveOutputDirectory(
            CopilotAgentRequest request,
            string requestedOutputDirectory,
            IReadOnlyList<BatchImageItem> items,
            out string? outputDirectory,
            out string error)
        {
            outputDirectory = null;
            error = string.Empty;
            var writableRoots = CopilotWorkspaceSearchSupport.NormalizeSearchRoots(
                request.WritableLocalRootPaths);
            if (string.IsNullOrWhiteSpace(requestedOutputDirectory))
            {
                foreach (var item in items)
                {
                    var sourceDirectory = Path.GetDirectoryName(item.FilePath) ?? string.Empty;
                    if (IsPotentialOutputPathWithinRoots(sourceDirectory, writableRoots))
                        continue;

                    error = $"The source directory is read-only for this request: {sourceDirectory}";
                    return false;
                }
                return true;
            }

            try
            {
                outputDirectory = Path.GetFullPath(requestedOutputDirectory);
            }
            catch (Exception ex)
            {
                error = $"The output directory is invalid: {ex.Message}";
                return false;
            }

            if (!IsPotentialOutputPathWithinRoots(outputDirectory, writableRoots))
            {
                error = $"The output directory is outside the writable roots: {outputDirectory}";
                outputDirectory = null;
                return false;
            }
            return true;
        }

        private static bool IsPotentialOutputPathWithinRoots(string outputPath, IEnumerable<string> roots)
        {
            var normalizedRoots = CopilotWorkspaceSearchSupport.NormalizeSearchRoots(roots);
            foreach (var root in normalizedRoots)
            {
                var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
                    ? root
                    : root + Path.DirectorySeparatorChar;
                if (!string.Equals(outputPath, root, StringComparison.OrdinalIgnoreCase)
                    && !outputPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var existingAncestor = outputPath;
                while (!Directory.Exists(existingAncestor))
                {
                    existingAncestor = Path.GetDirectoryName(existingAncestor) ?? string.Empty;
                    if (existingAncestor.Length == 0)
                    {
                        break;
                    }
                }
                if (existingAncestor.Length > 0
                    && CopilotWorkspaceSearchSupport.IsPathWithinRoots(existingAncestor, [root]))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool TryParseOptions(CopilotAgentToolInput? input, out ConversionOptions options, out string error)
        {
            options = new ConversionOptions();
            error = string.Empty;
            var arguments = input?.Arguments ?? new Dictionary<string, object?>();
            if (!TryReadStringArray(arguments, "sources", out var sources) || sources.Count == 0)
            {
                error = "sources must contain at least one path.";
                return false;
            }
            if (!TryReadString(arguments, "outputDirectory", out var outputDirectory)
                || !TryReadString(arguments, "format", out var formatText)
                || !TryReadString(arguments, "algorithm", out var algorithm)
                || !TryReadString(arguments, "suffix", out var suffix)
                || !TryReadBoolean(arguments, "recursive", out var recursive)
                || !TryReadBoolean(arguments, "preserveFolderStructure", out var preserveFolderStructure)
                || !TryReadObject(arguments, "parameters", out JsonElement? parameters))
            {
                error = "One or more arguments have the wrong JSON type.";
                return false;
            }
            if (suffix.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                error = "suffix contains invalid file-name characters.";
                return false;
            }
            if (!TryParseFormat(formatText, out var format))
            {
                error = $"Unsupported output format: {formatText}";
                return false;
            }

            options = new ConversionOptions
            {
                Sources = sources,
                OutputDirectory = outputDirectory,
                Format = format,
                Recursive = recursive ?? false,
                PreserveFolderStructure = preserveFolderStructure ?? true,
                Suffix = suffix,
                Algorithm = algorithm,
                Parameters = parameters,
            };
            return true;
        }

        private static bool TryReadStringArray(IReadOnlyDictionary<string, object?> arguments, string name, out IReadOnlyList<string> values)
        {
            var pair = arguments.FirstOrDefault(argument => string.Equals(argument.Key, name, StringComparison.OrdinalIgnoreCase));
            IEnumerable? enumerable = pair.Value switch
            {
                JsonElement { ValueKind: JsonValueKind.Array } element => element.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : null).ToArray(),
                IEnumerable items and not string => items,
                _ => null,
            };
            if (enumerable == null)
            {
                values = Array.Empty<string>();
                return false;
            }

            var parsed = new List<string>();
            foreach (var value in enumerable)
            {
                var text = value switch
                {
                    string stringValue => stringValue,
                    JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
                    _ => null,
                };
                if (string.IsNullOrWhiteSpace(text))
                {
                    values = Array.Empty<string>();
                    return false;
                }
                parsed.Add(text.Trim());
            }
            values = parsed.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            return true;
        }

        private static bool TryReadString(IReadOnlyDictionary<string, object?> arguments, string name, out string value)
        {
            var pair = arguments.FirstOrDefault(argument => string.Equals(argument.Key, name, StringComparison.OrdinalIgnoreCase));
            if (pair.Key == null || pair.Value == null)
            {
                value = string.Empty;
                return true;
            }
            if (pair.Value is string text)
            {
                value = text.Trim();
                return true;
            }
            if (pair.Value is JsonElement { ValueKind: JsonValueKind.String } element)
            {
                value = element.GetString()?.Trim() ?? string.Empty;
                return true;
            }
            value = string.Empty;
            return false;
        }

        private static bool TryReadBoolean(IReadOnlyDictionary<string, object?> arguments, string name, out bool? value)
        {
            var pair = arguments.FirstOrDefault(argument => string.Equals(argument.Key, name, StringComparison.OrdinalIgnoreCase));
            if (pair.Key == null || pair.Value == null)
            {
                value = null;
                return true;
            }
            if (pair.Value is bool boolean)
            {
                value = boolean;
                return true;
            }
            if (pair.Value is JsonElement { ValueKind: JsonValueKind.True or JsonValueKind.False } element)
            {
                value = element.GetBoolean();
                return true;
            }
            value = null;
            return false;
        }

        private static bool TryReadObject(IReadOnlyDictionary<string, object?> arguments, string name, out JsonElement? value)
        {
            var pair = arguments.FirstOrDefault(argument => string.Equals(argument.Key, name, StringComparison.OrdinalIgnoreCase));
            if (pair.Key == null || pair.Value == null)
            {
                value = null;
                return true;
            }
            if (pair.Value is JsonElement { ValueKind: JsonValueKind.Object } element)
            {
                value = element.Clone();
                return true;
            }
            try
            {
                JsonElement serialized = JsonSerializer.SerializeToElement(pair.Value);
                if (serialized.ValueKind == JsonValueKind.Object)
                {
                    value = serialized;
                    return true;
                }
            }
            catch (NotSupportedException)
            {
            }
            value = null;
            return false;
        }

        private static bool TryParseFormat(string value, out BatchOutputFormat format)
        {
            format = (value ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "" or "same-as-source" => BatchOutputFormat.SameAsSource,
                "tiff" => BatchOutputFormat.Tiff,
                "png" => BatchOutputFormat.Png,
                "jpeg" => BatchOutputFormat.Jpeg,
                "bmp" => BatchOutputFormat.Bmp,
                "webp" => BatchOutputFormat.WebP,
                _ => (BatchOutputFormat)(-1),
            };
            return Enum.IsDefined(format);
        }

        private static string FormatName(BatchOutputFormat format)
        {
            return format == BatchOutputFormat.SameAsSource ? "与源格式相同（CVRAW/CVCIE 输出 TIFF）" : format.ToString();
        }

        private static string FirstFailure(BatchImageRunResult result)
        {
            return result.Files.FirstOrDefault(file => !file.Success)?.ErrorMessage
                ?? (result.Cancelled ? "The conversion was cancelled." : "One or more files failed to convert.");
        }

        private CopilotToolResult Failure(string summary, string error, CopilotToolFailureKind failureKind)
        {
            return new CopilotToolResult
            {
                ToolName = Name,
                Success = false,
                Summary = summary,
                ErrorMessage = error,
                FailureKind = failureKind,
            };
        }

        private sealed class ConversionOptions
        {
            public IReadOnlyList<string> Sources { get; init; } = Array.Empty<string>();

            public string OutputDirectory { get; init; } = string.Empty;

            public BatchOutputFormat Format { get; init; } = BatchOutputFormat.SameAsSource;

            public bool Recursive { get; init; }

            public bool PreserveFolderStructure { get; init; } = true;

            public string Suffix { get; init; } = string.Empty;

            public string Algorithm { get; init; } = string.Empty;

            public JsonElement? Parameters { get; init; }
        }
    }
}
