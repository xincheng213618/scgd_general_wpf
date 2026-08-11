using ColorVision.Copilot;
using ColorVision.FileIO;
using OpenCvSharp;
using System.IO;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotBatchImageProcessingTests
{
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
}
