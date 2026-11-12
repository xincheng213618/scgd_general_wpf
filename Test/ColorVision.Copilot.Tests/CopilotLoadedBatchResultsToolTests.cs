using ColorVision.Copilot.Mcp;
using ColorVision.Engine;
using System.Text.Json;
using static ColorVision.Copilot.Tests.CopilotLoadedBatchResultsServiceTests;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotLoadedBatchResultsToolTests
{
    [Theory]
    [InlineData("directory")]
    [InlineData("results")]
    [InlineData("focus")]
    [InlineData("continuation")]
    public async Task NativeAndMcpReadDirectoryRowsAndExactFocusAsTheSameWholeJson(string mode)
    {
        var snapshot = Snapshot([Source(10, [Row(10), Row(11) with { Kind = "algorithm", ResultCode = null, DurationMs = -17 }]), Source(20, [])]);
        var dispatcher = Dispatcher(_ => Task.FromResult(snapshot));
        var arguments = new Dictionary<string, object?>();
        if (mode != "directory")
        {
            arguments["source_ref"] = Ref(10).ToUpperInvariant();
            arguments["expected_source_instance_id"] = Registry.ToUpperInvariant();
        }
        if (mode == "focus") arguments["result_ref"] = Ref(11).ToUpperInvariant();
        if (mode == "continuation")
        {
            var firstArguments = arguments.ToDictionary(pair => pair.Key, pair => JsonSerializer.SerializeToElement(pair.Value));
            firstArguments["max_items"] = JsonSerializer.SerializeToElement(1);
            var first = await dispatcher.CallAsync("inspect_loaded_batch_results", firstArguments, CancellationToken.None);
            Assert.True(first.Success, first.Text);
            using var firstJson = JsonDocument.Parse(first.Text);
            arguments["cursor"] = firstJson.RootElement.GetProperty("next_cursor").GetString();
            arguments["max_items"] = 100;
        }
        var tool = new CopilotInspectLoadedBatchResultsTool(dispatcher);
        Assert.True(tool.InputSchema.TryBind(arguments, out var input, out var error), error);
        var agent = await tool.ExecuteAsync(Request(), input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("inspect_loaded_batch_results", CopilotFlowReadToolBase.ToJsonArguments(input), CancellationToken.None);
        Assert.True(agent.Success, agent.ErrorMessage);
        Assert.True(mcp.Success, mcp.Text);
        Assert.Equal(agent.Content, mcp.Text);
        Assert.Equal(agent.PartialResultMessage, mcp.PartialResultMessage);
        using var document = JsonDocument.Parse(agent.Content);
        var root = document.RootElement;
        Assert.Equal(Registry, root.GetProperty("source_instance_id").GetString());
        Assert.Equal(mode == "continuation" ? "results" : mode, root.GetProperty("mode").GetString());
        Assert.Equal(mode == "directory" ? 2 : 0, root.GetProperty("sources").GetArrayLength());
        Assert.Equal(mode == "directory" ? 0 : mode == "results" ? 2 : 1, root.GetProperty("results").GetArrayLength());
        if (mode != "directory")
        {
            Assert.Equal(Ref(10), root.GetProperty("source_ref").GetString());
            Assert.NotEmpty(agent.PartialResultMessage);
        }
        Assert.Null(agent.Approval);
        Assert.False(mcp.RequiresApproval);
    }

    [Fact]
    public async Task UnavailableRegistryKeepsItsClassificationWithoutLeakingRawExceptionText()
    {
        var dispatcher = Dispatcher(_ => throw new CopilotLoadedBatchResultsSourceUnavailableException("PRIVATE_DETAIL"));
        var agent = await new CopilotInspectLoadedBatchResultsTool(dispatcher).ExecuteAsync(Request(), CopilotAgentToolInput.Empty, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("inspect_loaded_batch_results", null, CancellationToken.None);
        Assert.False(agent.Success);
        Assert.False(mcp.Success);
        Assert.Equal(CopilotToolFailureKind.NotFound, agent.FailureKind);
        Assert.Equal(agent.FailureKind, mcp.FailureKind);
        Assert.DoesNotContain("PRIVATE_DETAIL", agent.Content, StringComparison.Ordinal);
    }

    private static CopilotAgentRequest Request(CopilotAgentMode mode = CopilotAgentMode.Diagnose) => new()
    {
        Mode = mode, UserText = "Read registered loaded batch details.", ConversationId = "loaded-batches-test", TaskId = "loaded-batches-task",
    };
    private static CopilotMcpToolDispatcher Dispatcher(Func<CancellationToken, Task<CopilotLoadedBatchResultsSnapshot>> provider) => new(new CopilotMcpToolEnvironment
    {
        WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot(), LoadedBatchResultsSnapshotProvider = provider,
    });
}
