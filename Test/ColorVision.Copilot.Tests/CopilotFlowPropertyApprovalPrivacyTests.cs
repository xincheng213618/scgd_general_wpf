using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotApprovalReviewTestGroup.CollectionName)]
public sealed class CopilotFlowPropertyApprovalPrivacyTests
{
    [Fact]
    public async Task ASourceRejectedSensitiveDisplayPropertyCannotPublishApprovalOrExposeTheProposedValue()
    {
        const string proposedSecret = "ordinary-proposed-credential-7319";
        var previews = 0;
        var applies = 0;
        var dispatcher = Dispatcher((request, _) =>
        {
            previews++;
            Assert.Equal("set_property", request.Operation);
            Assert.Equal("Setting", request.PropertyName);
            Assert.Equal(proposedSecret, request.Value);
            return Task.FromResult(CopilotMcpToolCallResult.Fail("flow_patch_failed", "The source refuses to read or modify this sensitive property.", CopilotToolFailureKind.Validation));
        }, (_, _) => { applies++; throw new InvalidOperationException("Rejected property must not be applied."); });
        var tool = new CopilotApplyFlowPatchTool(dispatcher);
        var input = Input("Setting", proposedSecret);
        Assert.Throws<InvalidOperationException>(() => tool.CreateApprovalPresentation(input));
        var callId = "sensitive-property-approval-" + Guid.NewGuid().ToString("N");

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => new CopilotFrameworkApprovalCoordinator().RequestApprovalAsync(
            tool, Request(), input, callId, CancellationToken.None));

        Assert.DoesNotContain(proposedSecret, failure.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, previews);
        Assert.Equal(0, applies);
        Assert.DoesNotContain(CopilotMcpConfirmationStore.Instance.GetPendingActions(), action => action.AgentCallId == callId);
    }

    [Fact]
    public async Task NormalPropertyApprovalReviewsTheCompleteConvertedPreviewAndNeverAppliesAutomatically()
    {
        var previews = 0;
        var applies = 0;
        var preview = JsonSerializer.Serialize(new
        {
            format = "colorvision.flow-patch-preview.v1", expectedRevision = new string('a', 64), change = new
            {
                operation = "set_property", nodeId = "12345678-1234-1234-1234-123456789abc", nodeTitle = "Descriptor node",
                propertyName = "Value", oldValue = "4", newValue = "7",
            },
        });
        var dispatcher = Dispatcher((request, _) =>
        {
            previews++;
            Assert.Equal("units:7", request.Value);
            return Task.FromResult(CopilotMcpToolCallResult.Ok(preview));
        }, (_, _) => { applies++; return Task.FromResult(CopilotMcpToolCallResult.Ok("must remain unused")); });
        var tool = new CopilotApplyFlowPatchTool(dispatcher);
        var input = Input("Value", "units:7");
        Assert.Throws<InvalidOperationException>(() => tool.CreateApprovalPresentation(input));
        var coordinator = new CopilotFrameworkApprovalCoordinator();
        var handle = await coordinator.RequestApprovalAsync(tool, Request(), input, "normal-property-approval-" + Guid.NewGuid().ToString("N"), CancellationToken.None);
        try
        {
            Assert.Equal(1, previews);
            Assert.Equal(0, applies);
            Assert.Equal(ConfirmableActionStatus.Pending, handle.Action.Status);
            Assert.Equal(CopilotApprovalReviewTextEncoder.Encode(preview), handle.Action.ReviewDetails);
            Assert.Contains("Value", handle.Action.Description, StringComparison.Ordinal);
            Assert.Contains("units:7", handle.Action.Description, StringComparison.Ordinal);
        }
        finally
        {
            coordinator.Cancel(handle);
            await handle.Decision.WaitAsync(TimeSpan.FromSeconds(2));
        }
        Assert.Equal(0, applies);
    }

    private static CopilotAgentToolInput Input(string propertyName, string value) => new()
    {
        Arguments = new Dictionary<string, object?>
        {
            ["operation"] = "set_property", ["expected_revision"] = new string('a', 64),
            ["node_id"] = "12345678-1234-1234-1234-123456789abc", ["property_name"] = propertyName, ["value"] = value,
        },
    };
    private static CopilotAgentRequest Request() => new()
    {
        ConversationId = "property-approval-conversation", TaskId = "property-approval-task", Mode = CopilotAgentMode.Auto,
        UserText = "Preview the exact property change before applying it.",
    };
    private static CopilotMcpToolDispatcher Dispatcher(
        Func<CopilotFlowPatchRequest, CancellationToken, Task<CopilotMcpToolCallResult>> preview,
        Func<CopilotFlowPatchRequest, CancellationToken, Task<CopilotMcpToolCallResult>> apply) =>
        new(new CopilotMcpToolEnvironment
        {
            WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot(), PreviewFlowPatchHandler = preview, ApplyFlowPatchHandler = apply,
        });
}
