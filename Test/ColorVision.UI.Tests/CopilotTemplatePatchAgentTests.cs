using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.UI.Tests;

[Collection(CopilotSharedStateTestGroup.Name)]
public sealed class CopilotTemplatePatchAgentTests : IDisposable
{
    public CopilotTemplatePatchAgentTests()
    {
        CopilotMcpConfirmationStore.Instance.ClearForTests();
        CopilotMcpTemplatePatchPreviewStore.Instance.ClearForTests();
    }

    [Fact]
    public async Task InAppToolPreviewsStagesAndExecutesTemplatePatchAfterApproval()
    {
        var currentJson = "{\"Exposure\":10,\"Gain\":2}";
        var applyCount = 0;
        var context = CreateTemplateContext(() => currentJson);
        CopilotLiveContextRegistry.Publish(context);
        var dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
        {
            LiveContextProvider = () => context,
            ApplyTemplatePatchHandler = (request, _) =>
            {
                currentJson = request.PatchedJson;
                applyCount++;
                return Task.FromResult(CopilotMcpToolCallResult.Ok("applied"));
            },
        });
        var tool = new CopilotTemplatePatchTool(dispatcher);
        var request = CreateRequest("把曝光调整到 12");

        var preview = await tool.ExecuteAsync(request, new CopilotAgentToolInput
        {
            Query = "{\"proposed_changes\":{\"Exposure\":12}}",
        }, CancellationToken.None);
        var previewId = ExtractField(preview.Content, "preview_id");

        Assert.True(preview.Success);
        Assert.Contains("Exposure: 10 -> 12", preview.Content, StringComparison.Ordinal);

        var staged = await tool.ExecuteAsync(CreateRequest("应用这个预览"), new CopilotAgentToolInput
        {
            Query = $"{{\"preview_id\":\"{previewId}\",\"apply\":true}}",
        }, CancellationToken.None);
        var action = Assert.Single(CopilotMcpConfirmationStore.Instance.GetPendingActions());

        Assert.True(staged.Success);
        Assert.True(action.ExecuteOnApproval);
        Assert.Equal(0, applyCount);

        var applied = await CopilotMcpConfirmationStore.Instance.ApproveAndExecuteAsync(action.ActionId, CancellationToken.None);

        Assert.True(applied.Success);
        Assert.Equal(1, applyCount);
        using var document = JsonDocument.Parse(currentJson);
        Assert.Equal(12, document.RootElement.GetProperty("Exposure").GetInt32());
    }

    [Fact]
    public async Task InAppToolRejectsSensitiveTemplateFields()
    {
        const string currentJson = "{\"Exposure\":10}";
        var context = CreateTemplateContext(() => currentJson);
        CopilotLiveContextRegistry.Publish(context);
        var tool = new CopilotTemplatePatchTool(new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
        {
            LiveContextProvider = () => context,
        }));

        var result = await tool.ExecuteAsync(CreateRequest("修改 token"), new CopilotAgentToolInput
        {
            Query = "{\"proposed_changes\":{\"api_token\":\"secret\"}}",
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("sensitive", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(CopilotMcpConfirmationStore.Instance.GetPendingActions());
    }

    [Fact]
    public async Task ExistingPreviewCannotBeStagedWithoutExplicitApplyIntent()
    {
        var tool = new CopilotTemplatePatchTool();
        CopilotLiveContextRegistry.Publish(CreateTemplateContext(() => "{\"Exposure\":10}"));

        var result = await tool.ExecuteAsync(CreateRequest("查看这个预览"), new CopilotAgentToolInput
        {
            Query = "{\"preview_id\":\"abcdef123456\",\"apply\":true}",
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("explicitly apply", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(CopilotMcpConfirmationStore.Instance.GetPendingActions());
    }

    [Fact]
    public async Task ExternalMcpActionStillRequiresClientConfirmAction()
    {
        var action = CopilotMcpConfirmationStore.Instance.Create(
            "External action",
            "External two-stage confirmation",
            "confirmation-required",
            "external_tool",
            "value=1",
            _ => Task.FromResult(CopilotMcpToolCallResult.Ok("executed")));

        var result = await CopilotMcpConfirmationStore.Instance.ApproveAndExecuteAsync(action.ActionId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("action_requires_client_confirmation", result.ErrorCode);
        Assert.True(CopilotMcpConfirmationStore.Instance.Approve(action.ActionId, out _));
        var confirmed = await CopilotMcpConfirmationStore.Instance.ExecuteApprovedAsync(action.ActionId, action.ToolName, action.ArgumentsSummary, CancellationToken.None);
        Assert.True(confirmed.Success);
    }

    [Fact]
    public async Task ApprovedInAppActionConvertsExecutorExceptionToAuditedFailure()
    {
        var action = CopilotMcpConfirmationStore.Instance.Create(
            "Failing in-app action",
            "Exercise executor failure handling",
            "confirmation-required",
            "apply_template_patch",
            "preview_id=test",
            _ => throw new InvalidOperationException("token=secret-value device failed"),
            executeOnApproval: true);

        var result = await CopilotMcpConfirmationStore.Instance.ApproveAndExecuteAsync(action.ActionId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("action_execution_failed", result.ErrorCode);
        Assert.DoesNotContain("secret-value", result.Text, StringComparison.Ordinal);
        Assert.Equal(ConfirmableActionStatus.Executed, action.Status);
    }

    [Fact]
    public void ToolIsAvailableOnlyForTemplateChangeIntent()
    {
        var tool = new CopilotTemplatePatchTool();
        CopilotLiveContextRegistry.Publish(CreateTemplateContext(() => "{\"Exposure\":10}"));

        Assert.True(tool.CanHandle(CreateRequest("把曝光调整到 12")));
        Assert.False(tool.CanHandle(CreateRequest("解释这个模板")));
        Assert.False(tool.CanHandle(new CopilotAgentRequest
        {
            UserText = "把曝光调整到 12",
            Mode = CopilotAgentMode.Chat,
            Profile = new CopilotProfileConfig(),
        }));
    }

    [Fact]
    public void PlannerPromptShowsTemplatePatchInputAsValidJson()
    {
        var messages = new CopilotAgentContextBuilder().BuildPlannerMessages(
            CreateRequest("把曝光调整到 12"),
            new ICopilotTool[] { new CopilotTemplatePatchTool() },
            Array.Empty<CopilotAgentStepRecord>(),
            Array.Empty<string>());
        var prompt = messages[^1].Content;

        Assert.Contains("{\"proposed_changes\":{\"FieldName\":newValue}}", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("{\\\"proposed_changes", prompt, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        CopilotLiveContextRegistry.Clear("template-json-editor:agent-test");
        CopilotMcpConfirmationStore.Instance.ClearForTests();
        CopilotMcpTemplatePatchPreviewStore.Instance.ClearForTests();
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

    private static CopilotLiveContext CreateTemplateContext(Func<string> currentJsonProvider)
    {
        return new CopilotLiveContext
        {
            SourceId = "template-json-editor:agent-test",
            Title = "Template JSON editor · CameraTemplate",
            SnapshotItems = new[]
            {
                new CopilotContextItem
                {
                    Title = "CameraTemplate",
                    Content = "Surface: Template JSON editor\nTemplate name: CameraTemplate\nCurrent JSON:\n```json\n" + currentJsonProvider() + "\n```",
                },
            },
        };
    }

    private static string ExtractField(string text, string fieldName)
    {
        var line = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .First(item => item.TrimStart().StartsWith(fieldName + ":", StringComparison.OrdinalIgnoreCase));
        return line[(line.IndexOf(':') + 1)..].Trim();
    }
}
