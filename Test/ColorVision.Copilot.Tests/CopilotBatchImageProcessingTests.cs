using ColorVision.Copilot;
using ColorVision.Algorithms;
using ColorVision.FileIO;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.BatchProcessing;
using OpenCvSharp;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotBatchImageProcessingTests
{
    [Fact]
    public async Task CopilotToolExecutesTheInjectedRuntimeInsteadOfTheGlobalProvider()
    {
        AlgorithmDescriptor invert = StandardAlgorithmCatalog.Create().Descriptors.Single(
            descriptor => descriptor.Id == StandardAlgorithmIds.Invert);
        AlgorithmCatalog catalog = new();
        catalog.Register(invert, "InvertImage");
        int executions = 0;
        InjectedProvider provider = new(() => Interlocked.Increment(ref executions));
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [provider], scheduler);
        CopilotConvertBatchImagesTool tool = new(
            new BatchImageProcessor([new StandardBatchImageLoader()]),
            runtime);
        string directory = Path.Combine(Path.GetTempPath(), $"colorvision-copilot-runtime-{Guid.NewGuid():N}");
        string sourcePath = Path.Combine(directory, "sample.png");
        Directory.CreateDirectory(directory);
        try
        {
            using (Mat source = new(2, 3, MatType.CV_8UC1, Scalar.All(7))) Assert.True(Cv2.ImWrite(sourcePath, source));
            CopilotAgentRequest request = new()
            {
                UserText = "对图片执行反相",
                Mode = CopilotAgentMode.Auto,
                SearchRootPaths = [directory],
                ReadableLocalDirectoryPaths = [directory],
                WritableLocalRootPaths = [directory],
            };
            Assert.True(tool.InputSchema.TryBind(
                new Dictionary<string, object?>
                {
                    ["sources"] = new[] { sourcePath },
                    ["format"] = "png",
                    ["algorithm"] = "InvertImage",
                    ["parameters"] = JsonSerializer.SerializeToElement(new { }),
                },
                out CopilotAgentToolInput input,
                out string bindError), bindError);

            CopilotToolResult result = await ((ICopilotFrameworkApprovedTool)tool).ExecuteApprovedAsync(request, input, CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(1, executions);
            using Mat output = Cv2.ImRead(Path.Combine(directory, "sample_invert.png"), ImreadModes.Grayscale);
            Assert.Equal(33, output.At<byte>(0, 0));
            Assert.Equal(33, output.At<byte>(1, 2));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CopilotCreationRejectsAWhitelistedIdWhoseRuntimeContractIsAnalysis()
    {
        AlgorithmDescriptor invert = StandardAlgorithmCatalog.Create().Descriptors.Single(
            descriptor => descriptor.Id == StandardAlgorithmIds.Invert) with
        {
            ResultSemantics = AlgorithmResultSemantics.Analysis,
        };
        AlgorithmCatalog catalog = new();
        catalog.Register(invert, "InvertImage");
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [new InjectedProvider(() => { })], scheduler);

        bool created = BatchImageAlgorithms.TryCreateForCopilot(
            runtime,
            "InvertImage",
            null,
            out BatchImageAlgorithmDefinition? algorithm,
            out string error);

        Assert.False(created);
        Assert.Null(algorithm);
        Assert.Contains("whitelist", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("对这些图片执行 Canny 边缘检测")]
    [InlineData("把这些图像做白平衡")]
    [InlineData("Run Canny on these images")]
    [InlineData("对图片执行反相")]
    public void NaturalLanguageAlgorithmIntentReachesTheApprovedWhitelistedExecutionTool(string userText)
    {
        CopilotAgentRequest request = new()
        {
            UserText = userText,
            Mode = CopilotAgentMode.Auto,
        };
        CopilotToolRegistry registry = new(CopilotToolRegistry.CreateCoreDefaultTools());

        IReadOnlyList<ICopilotTool> available = registry.FindTools(request);
        ICopilotTool tool = Assert.Single(available, item => item.Name == "ConvertBatchImages");
        Assert.IsType<CopilotConvertBatchImagesTool>(tool);
        Assert.DoesNotContain(available, item => item.Name == "OpenBatchImageProcessing");
        Assert.Equal(CopilotToolAccess.Write, tool.Capability.Access);
        Assert.Equal(CopilotToolApprovalMode.Always, tool.Capability.ApprovalMode);

        CopilotAgentExecutionContract contract = CopilotAgentExecutionContract.Create(request, available);
        Assert.Equal(CopilotAgentExecutionRequirement.BatchImageConversion, contract.Requirement);
        Assert.Equal(["ConvertBatchImages"], contract.AcceptedToolNames);
        Assert.Contains("ConvertBatchImages", contract.BuildInitialInstruction(), StringComparison.Ordinal);
    }

    [Fact]
    public void ConceptualAlgorithmQuestionDoesNotExposeTheWriteTool()
    {
        CopilotAgentRequest request = new()
        {
            UserText = "Canny 图像边缘检测是什么？",
            Mode = CopilotAgentMode.Auto,
        };

        Assert.False(new CopilotConvertBatchImagesTool().IsAvailable(request));
    }

    [Theory]
    [InlineData("Analyze this image's white balance")]
    [InlineData("Check whether these images need histogram equalization")]
    [InlineData("Evaluate the white balance of this image")]
    [InlineData("看看这张图片的白平衡是否正常")]
    [InlineData("分析这些图像是否需要直方图均衡")]
    [InlineData("Should I apply white balance to these images?")]
    [InlineData("Should we apply histogram equalization to these images?")]
    [InlineData("我该不该对这些图片应用白平衡？")]
    [InlineData("要不要给这些图像做直方图均衡？")]
    [InlineData("是否应该对这些图片应用白平衡？")]
    [InlineData("Batch image processing best practices for white balance")]
    [InlineData("这张图像白平衡处理结果不对")]
    [InlineData("Do not apply white balance to these images")]
    [InlineData("I applied white balance to these images yesterday")]
    [InlineData("White balance was applied to these images")]
    [InlineData("Apply white balance to this image")]
    [InlineData("不要对这些图片应用白平衡")]
    [InlineData("这些图片已经做过白平衡")]
    [InlineData("请对这张图片降噪")]
    public void ReadOnlyAlgorithmAssessmentDoesNotCreateABatchImageWriteContract(string userText)
    {
        CopilotAgentRequest request = new() { UserText = userText, Mode = CopilotAgentMode.Auto };
        CopilotToolRegistry registry = new(CopilotToolRegistry.CreateCoreDefaultTools());

        IReadOnlyList<ICopilotTool> available = registry.FindTools(request);

        Assert.DoesNotContain(available, tool => tool.Name == "ConvertBatchImages");
        Assert.False(new CopilotConvertBatchImagesTool().IsAvailable(request));
        Assert.NotEqual(
            CopilotAgentExecutionRequirement.BatchImageConversion,
            CopilotAgentExecutionContract.Create(request, available).Requirement);
    }

    [Theory]
    [InlineData("Apply white balance to these images and save the outputs")]
    [InlineData("Convert these images with histogram equalization")]
    [InlineData("把这些图片应用白平衡并保存输出")]
    [InlineData("批量处理这些图像：应用直方图均衡并导出")]
    [InlineData("Please invert these images")]
    [InlineData("请把这些图片反相")]
    [InlineData("White-balance these images")]
    [InlineData("Use histogram equalization on these images")]
    [InlineData("请对这些图片降噪")]
    [InlineData("Run denoise on all images")]
    [InlineData("对多张图像应用阈值处理")]
    public void ExplicitAlgorithmMutationStillRequiresTheAlwaysApprovedBatchTool(string userText)
    {
        CopilotAgentRequest request = new() { UserText = userText, Mode = CopilotAgentMode.Auto };
        CopilotToolRegistry registry = new(CopilotToolRegistry.CreateCoreDefaultTools());

        IReadOnlyList<ICopilotTool> available = registry.FindTools(request);
        ICopilotTool tool = Assert.Single(available, item => item.Name == "ConvertBatchImages");
        Assert.Equal(CopilotToolAccess.Write, tool.Capability.Access);
        Assert.Equal(CopilotToolApprovalMode.Always, tool.Capability.ApprovalMode);
        Assert.Equal(
            CopilotAgentExecutionRequirement.BatchImageConversion,
            CopilotAgentExecutionContract.Create(request, available).Requirement);
    }

    [Fact]
    public void CopilotParameterSchemaUsesTheCatalogNominalIntensityContract()
    {
        JsonElement parameterSchema = new CopilotConvertBatchImagesTool()
            .InputSchema.JsonSchema
            .GetProperty("properties")
            .GetProperty("parameters");
        JsonElement properties = parameterSchema.GetProperty("properties");

        Assert.Equal(ushort.MaxValue, properties.GetProperty("threshold").GetProperty("maximum").GetInt32());
        Assert.Equal("boolean", properties.GetProperty("useNominalColorSigma").GetProperty("type").GetString());
        string thresholdDescription = properties.GetProperty("threshold").GetProperty("description").GetString()!;
        string modeDescription = properties.GetProperty("useNominalRange").GetProperty("description").GetString()!;
        Assert.Contains("useNominalRange", thresholdDescription, StringComparison.Ordinal);
        Assert.Contains("0..255", thresholdDescription, StringComparison.Ordinal);
        Assert.Contains("0..65535", thresholdDescription, StringComparison.Ordinal);
        Assert.Contains("input format", thresholdDescription, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("runtime", modeDescription, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("conditional", parameterSchema.GetProperty("$comment").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CopilotThresholdSchemaAllowsAbsolute16BitValuesButUnifiedValidationRejectsNominalOverflow()
    {
        CopilotConvertBatchImagesTool tool = new();
        JsonElement absolute = JsonSerializer.SerializeToElement(new { threshold = ushort.MaxValue, useNominalRange = false });
        Assert.True(tool.InputSchema.TryBind(
            new Dictionary<string, object?>
            {
                ["sources"] = new[] { "sample.png" },
                ["algorithm"] = "colorvision.image.threshold",
                ["parameters"] = absolute,
            },
            out _,
            out string bindError), bindError);
        Assert.True(BatchImageAlgorithms.TryCreateForCopilot(
            "colorvision.image.threshold",
            absolute,
            out BatchImageAlgorithmDefinition? algorithm,
            out string createError), createError);
        Assert.NotNull(algorithm);

        JsonElement invalidNominal = JsonSerializer.SerializeToElement(new { threshold = 256, useNominalRange = true });
        Assert.False(BatchImageAlgorithms.TryCreateForCopilot(
            "colorvision.image.threshold",
            invalidNominal,
            out _,
            out string validationError));
        Assert.Contains(nameof(ThresholdParameters.Threshold), validationError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CopilotRunsOnlyApprovedWhitelistedCatalogAlgorithmAndReturnsEvidence()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"colorvision-copilot-algorithm-{Guid.NewGuid():N}");
        string sourcePath = Path.Combine(directory, "sample.png");
        Directory.CreateDirectory(directory);
        try
        {
            byte[] pixels = [0, 1, 127, 255, 16, 240];
            using (Mat source = new(2, 3, MatType.CV_8UC1))
            {
                Marshal.Copy(pixels, 0, source.Data, pixels.Length);
                Assert.True(Cv2.ImWrite(sourcePath, source));
            }

            CopilotConvertBatchImagesTool tool = new();
            CopilotAgentRequest request = new()
            {
                UserText = "对图片执行反相",
                Mode = CopilotAgentMode.Auto,
                SearchRootPaths = [directory],
                ReadableLocalDirectoryPaths = [directory],
                WritableLocalRootPaths = [directory],
            };
            Assert.True(tool.IsAvailable(request));
            Assert.True(tool.InputSchema.TryBind(
                new Dictionary<string, object?>
                {
                    ["sources"] = new[] { sourcePath },
                    ["format"] = "png",
                    ["algorithm"] = "colorvision.image.invert",
                    ["parameters"] = JsonSerializer.SerializeToElement(new { }),
                },
                out CopilotAgentToolInput input,
                out string bindError), bindError);

            CopilotToolResult unapproved = await tool.ExecuteAsync(request, input, CancellationToken.None);
            Assert.False(unapproved.Success);
            Assert.Equal(CopilotToolFailureKind.Authorization, unapproved.FailureKind);

            CopilotToolResult approved = await ((ICopilotFrameworkApprovedTool)tool).ExecuteApprovedAsync(request, input, CancellationToken.None);
            Assert.True(approved.Success, approved.ErrorMessage);
            string outputPath = Path.Combine(directory, "sample_invert.png");
            Assert.True(File.Exists(outputPath));
            using Mat output = Cv2.ImRead(outputPath, ImreadModes.Grayscale);
            byte[] actual = new byte[pixels.Length];
            Marshal.Copy(output.Data, actual, 0, actual.Length);
            Assert.Equal(pixels.Select(value => (byte)~value), actual);
            using JsonDocument evidence = JsonDocument.Parse(approved.Content);
            Assert.Equal("colorvision.image.invert", evidence.RootElement.GetProperty("algorithm").GetString());
            Assert.Equal("1.0.0", evidence.RootElement.GetProperty("algorithm_version").GetString());
            Assert.Equal(1, evidence.RootElement.GetProperty("parameter_schema_version").GetInt32());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CopilotRejectsCatalogAlgorithmOutsideExplicitWhitelist()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"colorvision-copilot-algorithm-denied-{Guid.NewGuid():N}");
        string sourcePath = Path.Combine(directory, "sample.png");
        Directory.CreateDirectory(directory);
        try
        {
            using (Mat source = new(2, 2, MatType.CV_8UC1, Scalar.All(42))) Assert.True(Cv2.ImWrite(sourcePath, source));
            CopilotConvertBatchImagesTool tool = new();
            CopilotAgentRequest request = new()
            {
                UserText = "执行未列入白名单的算法",
                Mode = CopilotAgentMode.Auto,
                SearchRootPaths = [directory],
                ReadableLocalDirectoryPaths = [directory],
                WritableLocalRootPaths = [directory],
            };
            Assert.True(tool.InputSchema.TryBind(
                new Dictionary<string, object?>
                {
                    ["sources"] = new[] { sourcePath },
                    ["format"] = "png",
                    ["algorithm"] = "colorvision.image.remove-moire",
                },
                out CopilotAgentToolInput input,
                out string bindError), bindError);

            CopilotToolResult result = await ((ICopilotFrameworkApprovedTool)tool).ExecuteApprovedAsync(request, input, CancellationToken.None);
            Assert.False(result.Success);
            Assert.Equal(CopilotToolFailureKind.Authorization, result.FailureKind);
            Assert.Contains("whitelist", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(Path.Combine(directory, "sample_demoire.png")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CopilotRejectsParametersOutsideTheSelectedAlgorithmContract()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"colorvision-copilot-parameter-denied-{Guid.NewGuid():N}");
        string sourcePath = Path.Combine(directory, "sample.png");
        Directory.CreateDirectory(directory);
        try
        {
            using (Mat source = new(2, 2, MatType.CV_8UC1, Scalar.All(42))) Assert.True(Cv2.ImWrite(sourcePath, source));
            CopilotConvertBatchImagesTool tool = new();
            CopilotAgentRequest request = new()
            {
                UserText = "执行反相并传入无关参数",
                Mode = CopilotAgentMode.Auto,
                SearchRootPaths = [directory],
                ReadableLocalDirectoryPaths = [directory],
                WritableLocalRootPaths = [directory],
            };
            Assert.True(tool.InputSchema.TryBind(
                new Dictionary<string, object?>
                {
                    ["sources"] = new[] { sourcePath },
                    ["format"] = "png",
                    ["algorithm"] = "colorvision.image.invert",
                    ["parameters"] = JsonSerializer.SerializeToElement(new { threshold = 10 }),
                },
                out CopilotAgentToolInput input,
                out string bindError), bindError);

            CopilotToolResult result = await ((ICopilotFrameworkApprovedTool)tool).ExecuteApprovedAsync(request, input, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(CopilotToolFailureKind.Authorization, result.FailureKind);
            Assert.Contains("not defined", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(Path.Combine(directory, "sample_invert.png")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ApprovedCopilotToolConvertsARealCvrawAndNeverOverwrites()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"colorvision-copilot-batch-{Guid.NewGuid():N}");
        string sourcePath = Path.Combine(directory, "sample.cvraw");
        string outputDirectory = Path.Combine(directory, "output");
        Directory.CreateDirectory(directory);
        try
        {
            using (CVCIEFile file = new()
            {
                Version = 1,
                FileExtType = CVType.Raw,
                Rows = 2,
                Cols = 3,
                Bpp = 16,
                Channels = 3,
                Gain = 1,
                Exp = new[] { 1f, 1f, 1f },
                Data = Enumerable.Range(0, 18).SelectMany(value => BitConverter.GetBytes((ushort)(value * 100))).ToArray(),
            })
            {
                Assert.True(CVFileUtil.WriteCIEFile(sourcePath, file));
            }

            CopilotConvertBatchImagesTool tool = new();
            CopilotAgentRequest request = new()
            {
                UserText = "批量转换 cvraw 文件为 TIFF",
                Mode = CopilotAgentMode.Auto,
                SearchRootPaths = [directory],
                ReadableLocalDirectoryPaths = [directory],
                WritableLocalRootPaths = [directory],
            };
            var arguments = new Dictionary<string, object?>
            {
                ["sources"] = new[] { sourcePath },
                ["outputDirectory"] = outputDirectory,
                ["format"] = "tiff",
            };
            Assert.True(tool.InputSchema.TryBind(arguments, out CopilotAgentToolInput input, out string bindError), bindError);
            Assert.Equal(CopilotToolApprovalMode.Always, tool.Capability.ApprovalMode);

            CopilotToolResult denied = await tool.ExecuteAsync(request, input, CancellationToken.None);
            Assert.False(denied.Success);
            Assert.Equal(CopilotToolFailureKind.Authorization, denied.FailureKind);

            var approvedTool = (ICopilotFrameworkApprovedTool)tool;
            CopilotToolResult first = await approvedTool.ExecuteApprovedAsync(request, input, CancellationToken.None);
            var progress = new CopilotToolProgressContext();
            CopilotToolResult second = await ((ICopilotFrameworkApprovedProgressReportingTool)tool).ExecuteApprovedWithProgressAsync(
                request,
                input,
                progress,
                CancellationToken.None);

            Assert.True(first.Success, first.ErrorMessage);
            Assert.True(second.Success, second.ErrorMessage);
            string firstOutput = Path.Combine(outputDirectory, "sample.tiff");
            string secondOutput = Path.Combine(outputDirectory, "sample_2.tiff");
            Assert.True(File.Exists(firstOutput));
            Assert.True(File.Exists(secondOutput));
            using Mat converted = Cv2.ImRead(firstOutput, ImreadModes.Unchanged);
            Assert.False(converted.Empty());
            Assert.Equal(2, converted.Rows);
            Assert.Equal(3, converted.Cols);
            Assert.Contains(sourcePath, first.SuccessfullyReadLocalFilePaths, StringComparer.OrdinalIgnoreCase);
            using JsonDocument evidence = JsonDocument.Parse(first.Content);
            Assert.Equal(1, evidence.RootElement.GetProperty("requested").GetInt32());
            Assert.Equal(1, evidence.RootElement.GetProperty("processed").GetInt32());
            Assert.True(evidence.RootElement.GetProperty("results")[0].GetProperty("source_read").GetBoolean());
            Assert.Equal(firstOutput, evidence.RootElement.GetProperty("results")[0].GetProperty("output").GetString());
            Assert.Equal(1, progress.LatestSnapshot?.Completed);
            Assert.Equal(1, progress.LatestSnapshot?.Total);
            Assert.Equal("files", progress.LatestSnapshot?.Unit);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ApprovedCopilotToolSkipsIdentityOutputsInsteadOfFailingTheBatch()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"colorvision-copilot-batch-identity-{Guid.NewGuid():N}");
        string existingTiff = Path.Combine(directory, "already-converted.tiff");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(existingTiff, [0x49, 0x49, 0x2A, 0x00]);
        try
        {
            CopilotConvertBatchImagesTool tool = new();
            CopilotAgentRequest request = new()
            {
                UserText = "批量转换图片为 TIFF",
                Mode = CopilotAgentMode.Auto,
                SearchRootPaths = [directory],
                ReadableLocalDirectoryPaths = [directory],
                WritableLocalRootPaths = [directory],
            };
            Assert.True(tool.InputSchema.TryBind(
                new Dictionary<string, object?>
                {
                    ["sources"] = new[] { directory },
                    ["format"] = "tiff",
                },
                out CopilotAgentToolInput input,
                out string bindError), bindError);

            CopilotToolResult result = await ((ICopilotFrameworkApprovedTool)tool).ExecuteApprovedAsync(
                request,
                input,
                CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Contains("skipped 1", result.Summary, StringComparison.OrdinalIgnoreCase);
            using JsonDocument evidence = JsonDocument.Parse(result.Content);
            Assert.Equal(1, evidence.RootElement.GetProperty("requested").GetInt32());
            Assert.Equal(0, evidence.RootElement.GetProperty("processed").GetInt32());
            Assert.Equal(1, evidence.RootElement.GetProperty("skipped_identity").GetInt32());
            Assert.Equal(existingTiff, evidence.RootElement.GetProperty("skipped_identity_sources")[0].GetString());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CopilotBatchConversionRejectsAnOutputOutsideApprovedRoots()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"colorvision-copilot-scope-{Guid.NewGuid():N}");
        string outsideDirectory = Path.Combine(Path.GetTempPath(), $"colorvision-copilot-outside-{Guid.NewGuid():N}");
        string sourcePath = Path.Combine(directory, "sample.png");
        Directory.CreateDirectory(directory);
        try
        {
            using (Mat source = new(2, 2, MatType.CV_8UC1, Scalar.All(42)))
            {
                Assert.True(Cv2.ImWrite(sourcePath, source));
            }

            CopilotConvertBatchImagesTool tool = new();
            CopilotAgentRequest request = new()
            {
                UserText = "批量转换图片为 TIFF",
                Mode = CopilotAgentMode.Auto,
                SearchRootPaths = [directory],
                WritableLocalRootPaths = [directory],
            };
            Assert.True(tool.InputSchema.TryBind(
                new Dictionary<string, object?>
                {
                    ["sources"] = new[] { sourcePath },
                    ["outputDirectory"] = outsideDirectory,
                    ["format"] = "tiff",
                },
                out CopilotAgentToolInput input,
                out string bindError), bindError);

            CopilotToolResult result = await ((ICopilotFrameworkApprovedTool)tool).ExecuteApprovedAsync(
                request,
                input,
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(CopilotToolFailureKind.Authorization, result.FailureKind);
            Assert.False(Directory.Exists(outsideDirectory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
            if (Directory.Exists(outsideDirectory))
            {
                Directory.Delete(outsideDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CopilotBatchConversionCannotWriteBesideAReadOnlyAddedDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"colorvision-copilot-read-only-{Guid.NewGuid():N}");
        string sourcePath = Path.Combine(directory, "sample.png");
        string outputPath = Path.Combine(directory, "sample.tiff");
        Directory.CreateDirectory(directory);
        try
        {
            using (Mat source = new(2, 2, MatType.CV_8UC1, Scalar.All(42)))
            {
                Assert.True(Cv2.ImWrite(sourcePath, source));
            }

            CopilotConvertBatchImagesTool tool = new();
            CopilotAgentRequest request = new()
            {
                UserText = "把参考目录中的图片转换为 TIFF",
                Mode = CopilotAgentMode.Auto,
                SearchRootPaths = [directory],
                ReadableLocalDirectoryPaths = [directory],
                WritableLocalRootPaths = [],
            };
            Assert.True(tool.InputSchema.TryBind(
                new Dictionary<string, object?>
                {
                    ["sources"] = new[] { sourcePath },
                    ["format"] = "tiff",
                },
                out CopilotAgentToolInput input,
                out string bindError), bindError);

            CopilotToolResult result = await ((ICopilotFrameworkApprovedTool)tool).ExecuteApprovedAsync(
                request,
                input,
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(CopilotToolFailureKind.Authorization, result.FailureKind);
            Assert.Contains("read-only", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(outputPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class InjectedProvider(Action onExecute) : IImageAlgorithmProvider
    {
        public AlgorithmProviderMetadata Metadata { get; } = new(
            "copilot-injected-provider",
            "Copilot injected provider",
            AlgorithmProviderKind.Cpu,
            AlgorithmExecutionPlane.Local,
            100,
            AlgorithmHostCapabilities.Copilot | AlgorithmHostCapabilities.Batch | AlgorithmHostCapabilities.Headless
                | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Deterministic,
            new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 });

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            reason = descriptor.Id == StandardAlgorithmIds.Invert ? null : "wrong algorithm";
            return reason == null;
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            onExecute();
            AlgorithmImageBuffer source = context.Inputs.Single().Image;
            byte[] pixels = Enumerable.Repeat((byte)33, source.Stride * source.Height).ToArray();
            return ValueTask.FromResult(new AlgorithmResult
            {
                Status = AlgorithmResultStatus.Succeeded,
                Artifacts =
                [
                    new AlgorithmImageArtifact(
                        "output",
                        "primary",
                        new AlgorithmImageBuffer(source.Width, source.Height, source.Stride, source.Format, pixels, source.DpiX, source.DpiY)),
                ],
            });
        }
    }
}
