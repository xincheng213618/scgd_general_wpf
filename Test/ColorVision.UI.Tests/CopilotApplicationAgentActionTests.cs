using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.UI.Tests;

[Collection(CopilotSharedStateTestGroup.Name)]
public sealed class CopilotApplicationAgentActionTests : IDisposable
{
    public CopilotApplicationAgentActionTests()
    {
        CopilotMcpConfirmationStore.Instance.ClearForTests();
    }

    [Fact]
    public async Task CreateFlowStagesAndExecutesOnlyAfterApproval()
    {
        var createCount = 0;
        var dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
        {
            CreateFlowHandler = (name, _) =>
            {
                createCount++;
                return Task.FromResult(CopilotMcpToolCallResult.Ok($"created {name}"));
            },
        });
        var tool = new CopilotCreateFlowTool(dispatcher);

        var staged = await tool.ExecuteAsync(CreateRequest("创建新的流程，名为校准流程"), CopilotAgentToolInput.Empty, CancellationToken.None);
        var action = Assert.Single(CopilotMcpConfirmationStore.Instance.GetPendingActions());

        Assert.True(staged.Success);
        Assert.Equal("create_flow", action.ToolName);
        Assert.True(action.ExecuteOnApproval);
        Assert.Contains("校准流程", action.ArgumentsSummary, StringComparison.Ordinal);
        Assert.Equal(0, createCount);

        var created = await CopilotMcpConfirmationStore.Instance.ApproveAndExecuteAsync(action.ActionId, CancellationToken.None);

        Assert.True(created.Success);
        Assert.Equal(1, createCount);
    }

    [Fact]
    public async Task RejectedCreateFlowDoesNotExecute()
    {
        var createCount = 0;
        var dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
        {
            CreateFlowHandler = (_, _) =>
            {
                createCount++;
                return Task.FromResult(CopilotMcpToolCallResult.Ok("created"));
            },
        });
        var tool = new CopilotCreateFlowTool(dispatcher);

        await tool.ExecuteAsync(CreateRequest("创建新的流程"), CopilotAgentToolInput.Empty, CancellationToken.None);
        var action = Assert.Single(CopilotMcpConfirmationStore.Instance.GetPendingActions());

        Assert.True(CopilotMcpConfirmationStore.Instance.Reject(action.ActionId, out _));
        var result = await CopilotMcpConfirmationStore.Instance.ApproveAndExecuteAsync(action.ActionId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(0, createCount);
    }

    [Fact]
    public async Task ConfirmationRequiredMenuStagesBeforeInAppExecution()
    {
        var handlerCalls = 0;
        var executeCount = 0;
        var dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
        {
            ExecuteMenuHandler = (_, dryRun, _) =>
            {
                handlerCalls++;
                if (!dryRun && handlerCalls == 1)
                    return Task.FromResult(CopilotMcpToolCallResult.Fail("confirmation_required", "confirmation_required\nrisk_level: confirmation-required"));

                if (!dryRun)
                    executeCount++;
                return Task.FromResult(CopilotMcpToolCallResult.Ok("executed"));
            },
        });
        var tool = new CopilotExecuteMenuTool(dispatcher);

        var staged = await tool.ExecuteAsync(CreateRequest("执行检查更新菜单"), new CopilotAgentToolInput
        {
            Query = "帮助 > 检查更新",
        }, CancellationToken.None);
        var action = Assert.Single(CopilotMcpConfirmationStore.Instance.GetPendingActions());

        Assert.True(staged.Success);
        Assert.Equal("execute_menu", action.ToolName);
        Assert.True(action.ExecuteOnApproval);
        Assert.Equal(0, executeCount);

        var executed = await CopilotMcpConfirmationStore.Instance.ApproveAndExecuteAsync(action.ActionId, CancellationToken.None);

        Assert.True(executed.Success);
        Assert.Equal(1, executeCount);
    }

    [Fact]
    public async Task ExternalCreateFlowKeepsTwoStageMcpConfirmation()
    {
        var createCount = 0;
        var dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
        {
            CreateFlowHandler = (_, _) =>
            {
                createCount++;
                return Task.FromResult(CopilotMcpToolCallResult.Ok("created"));
            },
        });
        var arguments = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("ExternalFlow"),
        };

        var staged = await dispatcher.CallAsync("create_flow", arguments, CancellationToken.None, "external-mcp-client");
        var action = Assert.Single(CopilotMcpConfirmationStore.Instance.GetPendingActions());

        Assert.False(staged.Success);
        Assert.Equal("confirmation_required", staged.ErrorCode);
        Assert.False(action.ExecuteOnApproval);
        Assert.Equal(0, createCount);

        var directApproval = await CopilotMcpConfirmationStore.Instance.ApproveAndExecuteAsync(action.ActionId, CancellationToken.None);
        Assert.Equal("action_requires_client_confirmation", directApproval.ErrorCode);
        Assert.Equal(0, createCount);

        Assert.True(CopilotMcpConfirmationStore.Instance.Approve(action.ActionId, out _));
        var confirmed = await CopilotMcpConfirmationStore.Instance.ExecuteApprovedAsync(action.ActionId, action.ToolName, action.ArgumentsSummary, CancellationToken.None);
        Assert.True(confirmed.Success);
        Assert.Equal(1, createCount);
    }

    [Fact]
    public void FlowIntentAndNameResolutionAreSpecific()
    {
        Assert.True(CopilotFlowCreationSupport.HasCreateIntent("创建新的流程"));
        Assert.False(CopilotFlowCreationSupport.HasCreateIntent("打开流程模板"));
        Assert.False(CopilotFlowCreationSupport.HasCreateIntent("怎么创建流程？"));
        Assert.False(CopilotFlowCreationSupport.HasCreateIntent("不要创建流程"));
        Assert.Equal("校准流程", CopilotFlowCreationSupport.ResolveFlowName("创建新的流程，名为校准流程", null));
        Assert.Equal("Flow_20260713_123456", CopilotFlowCreationSupport.ResolveFlowName("创建新的流程", null, new DateTime(2026, 7, 13, 12, 34, 56)));
    }

    public void Dispose()
    {
        CopilotMcpConfirmationStore.Instance.ClearForTests();
    }

    private static CopilotAgentRequest CreateRequest(string text)
    {
        return new CopilotAgentRequest
        {
            UserText = text,
            Mode = CopilotAgentMode.Diagnose,
            Profile = new CopilotProfileConfig(),
        };
    }
}
