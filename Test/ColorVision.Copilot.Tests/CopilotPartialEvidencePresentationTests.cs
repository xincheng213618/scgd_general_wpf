using ColorVision.Copilot.Mcp;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.FlowProcessing.PostProcess;
using ColorVision.Engine.Services.FlowDiagnostics;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotPartialEvidencePresentationTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("CopilotPartialEvidence-");

    [Fact]
    public async Task PartialCoverageSurvivesSnapshotsAndConversationRecovery()
    {
        var result = await CreatePartialResult("mixed-read");
        var captured = CopilotToolResultContract.Capture(result.ToolName, result);
        var observation = CopilotToolObservation.FromResult(captured);
        var run = new CopilotAgentRunResult
        {
            StepRecords = [new() { Observation = observation, ModelObservation = observation }],
        };
        Assert.Equal(result.PartialResultMessage, Assert.Single(run.StepRecords).Observation.PartialResultMessage);
        Assert.Equal(result.PartialResultMessage, run.StepRecords[0].ModelObservation!.PartialResultMessage);
        var entry = CreateEntry(captured);
        var restored = JsonConvert.DeserializeObject<CopilotAgentTraceEntry>(JsonConvert.SerializeObject(entry))!;
        restored.EnsureValid(DateTimeOffset.UtcNow);
        Assert.Equal(entry.PartialResultMessage, restored.PartialResultMessage);
        Assert.Equal(entry.ActivityLabel, restored.ActivityLabel);
        Assert.Contains(entry.PartialResultMessage, restored.DiagnosticDetails);
        Assert.False(restored.EnsureValid(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void AdvisoryIsRedactedAndBoundedOnRecoveryAndDoesNotOverrideFailure()
    {
        var secret = Guid.NewGuid().ToString("N");
        var captured = CopilotToolResultContract.Capture("ReadLocalFile", new()
        {
            ToolName = "ReadLocalFile", Success = true,
            PartialResultMessage = $"api_key={secret} " + new string('x', 1200),
        });
        Assert.DoesNotContain(secret, captured.PartialResultMessage);
        var restored = new CopilotAgentTraceEntry
        {
            ToolName = "ReadLocalFile", State = CopilotToolExecutionState.Completed,
            PartialResultMessage = $"api_key={secret} " + new string('x', 1200),
        };
        Assert.True(restored.EnsureValid(DateTimeOffset.UtcNow));
        Assert.DoesNotContain(secret, restored.PartialResultMessage);
        // Trace summaries retain an 800-character prefix followed by the existing ellipsis marker.
        Assert.Equal(803, restored.PartialResultMessage.Length);
        Assert.EndsWith("...", restored.PartialResultMessage);
        restored.State = CopilotToolExecutionState.Failed;
        restored.EnsureValid(DateTimeOffset.UtcNow);
        Assert.False(restored.HasPartialResult);
        Assert.Empty(restored.PartialResultMessage);
        Assert.Contains("失败", restored.ActivityLabel);
    }

    [Fact]
    public async Task AttachmentBatchKeepsOmittedFilesAndReadScopesVisible()
    {
        var attachments = Enumerable.Range(0, 4).Select(index =>
        {
            var path = Path.Combine(_directory.FullName, $"attachment-{index}.log");
            File.WriteAllText(path, new string('x', 21_000));
            return new CopilotAttachmentItem { Type = CopilotAttachmentType.File, Value = path };
        }).ToArray();
        var tool = new CopilotReadAttachedFileTool();
        var request = new CopilotAgentRequest { Mode = CopilotAgentMode.Auto, Attachments = attachments };
        var result = CopilotToolResultContract.Capture(tool.Name, await tool.ExecuteAsync(request, new(), CancellationToken.None));
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(3, result.LocalFileReadScopes.Count);
        Assert.All(result.LocalFileReadScopes, scope => Assert.True(scope.WasTruncated));
        Assert.Contains("1 个附件未读取", CreateEntry(result).ActivityDescription);
        var selected = await tool.ExecuteAsync(request, new() { Path = attachments[3].Value, StartLine = 1, StartColumn = 20_001 }, CancellationToken.None);
        Assert.True(selected.Success);
        Assert.Empty(selected.PartialResultMessage);
        Assert.Single(selected.LocalFileReadScopes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AttachmentFailuresRetainTheirCauseInsteadOfInvalidOutput(bool empty)
    {
        var path = Path.Combine(_directory.FullName, "binary.log");
        File.WriteAllText(path, "bad\0data");
        var tool = new CopilotReadAttachedFileTool();
        var result = CopilotToolResultContract.Capture(tool.Name, await tool.ExecuteAsync(new()
        {
            Mode = CopilotAgentMode.Auto,
            Attachments = empty ? [] : [new() { Type = CopilotAttachmentType.File, Value = path }],
        }, new(), CancellationToken.None));
        Assert.False(result.Success);
        Assert.NotEqual(CopilotToolResultContract.InvalidOutputFailureCode, result.FailureCode);
        Assert.NotEmpty(result.ErrorMessage);
        Assert.Empty(result.PartialResultMessage);
    }

    private async Task<CopilotToolResult> CreatePartialResult(string kind)
    {
        var path = Path.Combine(_directory.FullName, "capture.log");
        if (kind == "unreadable-search")
        {
            File.WriteAllText(path, "SUMMARY\0binary");
            return CopilotGrepTextCapability.Search([_directory.FullName], "SUMMARY", null, CancellationToken.None).ToCapabilityResult().ToToolResult("GrepText");
        }
        if (kind == "search-page")
        {
            File.WriteAllLines(path, Enumerable.Repeat("SUMMARY result=OK", 45));
            return CopilotGrepTextCapability.Search([_directory.FullName], "SUMMARY", null, CancellationToken.None).ToCapabilityResult().ToToolResult("GrepText");
        }
        if (kind is "directory-page" or "file-search-page")
        {
            for (var index = 0; index < 65; index++)
                File.WriteAllText(Path.Combine(_directory.FullName, $"record-{index:D2}.log"), "record");
            return kind == "directory-page"
                ? CopilotListDirectoryCapability.List([_directory.FullName], null, CancellationToken.None).ToToolResult("ListDirectory")
                : CopilotSearchFilesCapability.Search([_directory.FullName], "record-", null, true, CancellationToken.None).ToCapabilityResult().ToToolResult("SearchFiles");
        }
        File.WriteAllText(path, kind == "truncated-read" ? new string('x', 21_000) : "SUMMARY result=OK");
        var files = new List<string> { path };
        if (kind == "mixed-read")
        {
            var bad = Path.Combine(_directory.FullName, "damaged.log");
            File.WriteAllText(bad, "bad\0data");
            files.Add(bad);
        }
        return await new CopilotReadLocalFileTool().ExecuteAsync(new()
        {
            Mode = CopilotAgentMode.Auto, UserText = "读取日志", ReadableLocalFilePaths = files,
            SearchRootPaths = [_directory.FullName],
        }, new(), CancellationToken.None);
    }

    private static CopilotAgentTraceEntry CreateEntry(CopilotToolResult result) => CopilotAgentTraceEntry.FromResult(new()
    {
        ToolName = result.ToolName, CallId = Guid.NewGuid().ToString("N"), State = CopilotToolExecutionState.Completed,
        Access = CopilotToolAccess.ReadOnly, StartedAtUtc = DateTimeOffset.UtcNow, CompletedAtUtc = DateTimeOffset.UtcNow,
    }, CopilotToolResultContract.Capture(result.ToolName, result));

    public void Dispose()
    {
        var resolved = Path.GetFullPath(_directory.FullName);
        if (!string.Equals(Path.GetDirectoryName(resolved), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(resolved).StartsWith("CopilotPartialEvidence-", StringComparison.Ordinal))
            throw new InvalidOperationException("The fixture directory is outside its temporary root.");
        Directory.Delete(resolved, recursive: true);
    }
}
