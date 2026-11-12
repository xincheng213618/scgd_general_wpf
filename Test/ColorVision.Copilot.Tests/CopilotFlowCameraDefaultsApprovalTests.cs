using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotApprovalReviewTestGroup.CollectionName)]
public sealed class CopilotFlowCameraDefaultsApprovalTests
{
    private static readonly string Fingerprint = new('a', 64);

    [Fact]
    public async Task InheritedCameraApprovalReviewsTheCompletePreviewAndForwardsItsFingerprint()
    {
        var preview = JsonSerializer.Serialize(new
        {
            format = "colorvision.flow-patch-preview.v1",
            change = new
            {
                operation = "add_node",
                node = new
                {
                    deviceCode = "camera-test",
                    parameters = new[]
                    {
                        new { name = "Gain", value = "7" },
                        new { name = "Calibration", value = new string('x', 2400) + "review-tail" },
                    },
                },
            },
        }).Replace("review-tail", "\u202Ereview-tail", StringComparison.Ordinal);
        var invoker = new PreviewInvoker(new CopilotApplicationCapabilityCallResult { Success = true, Content = preview });
        var tool = new CopilotApplyFlowPatchTool(invoker);
        var coordinator = new CopilotFrameworkApprovalCoordinator();
        var handle = await coordinator.RequestApprovalAsync(
            tool, CreateRequest(), CreateInput(tool, inherit: true), $"call-{Guid.NewGuid():N}", CancellationToken.None);
        try
        {
            Assert.Equal(CopilotSharedCapabilityCatalog.PreviewFlowPatch.McpToolName, invoker.CapabilityName);
            Assert.Equal(1, invoker.CallCount);
            Assert.Equal("add_node", invoker.Arguments["operation"].GetString());
            Assert.Equal("rev-camera-test", invoker.Arguments["expected_revision"].GetString());
            Assert.Equal("FlowEngineLib.dll|LVCameraNode", invoker.Arguments["type_key"].GetString());
            Assert.Equal(100, invoker.Arguments["left"].GetInt32());
            Assert.Equal(200, invoker.Arguments["top"].GetInt32());
            Assert.True(invoker.Arguments["inherit_camera_defaults"].GetBoolean());
            Assert.Equal(Fingerprint, invoker.Arguments["camera_defaults_fingerprint"].GetString());
            Assert.Contains(CopilotApprovalReviewTextEncoder.Encode(preview), handle.Action.ReviewDetails, StringComparison.Ordinal);
            Assert.Contains(@"\u202Ereview-tail", handle.Action.ReviewDetails, StringComparison.Ordinal);
            Assert.DoesNotContain("\u202E", handle.Action.ReviewDetails, StringComparison.Ordinal);
        }
        finally
        {
            coordinator.Cancel(handle);
            await handle.Decision.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingFingerprintOrStalePreviewCannotCreateApproval(bool missingFingerprint)
    {
        var invoker = new PreviewInvoker(new CopilotApplicationCapabilityCallResult
        {
            Success = false,
            ErrorCode = "camera_defaults_changed",
            Content = "Camera defaults changed after preview; preview the addition again.",
        });
        var tool = new CopilotApplyFlowPatchTool(invoker);
        var callId = $"call-{Guid.NewGuid():N}";
        var createdActions = 0;
        EventHandler<ConfirmableActionChangedEventArgs> changed = (_, args) =>
        {
            if (args.Action.AgentCallId == callId)
                Interlocked.Increment(ref createdActions);
        };
        CopilotMcpConfirmationStore.Instance.ActionStatusChanged += changed;
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => new CopilotFrameworkApprovalCoordinator().RequestApprovalAsync(
                tool, CreateRequest(), CreateInput(tool, inherit: true, includeFingerprint: !missingFingerprint), callId, CancellationToken.None));

            Assert.Equal(missingFingerprint ? 0 : 1, invoker.CallCount);
            Assert.Equal(0, Volatile.Read(ref createdActions));
            Assert.DoesNotContain(CopilotMcpConfirmationStore.Instance.GetPendingActions(), action => action.AgentCallId == callId);
        }
        finally
        {
            CopilotMcpConfirmationStore.Instance.ActionStatusChanged -= changed;
        }
    }

    [Fact]
    public void SynchronousPresentationKeepsLegacyDefaultsAndRejectsInheritedDefaults()
    {
        var invoker = new PreviewInvoker(new CopilotApplicationCapabilityCallResult { Success = true, Content = "unused" });
        var tool = new CopilotApplyFlowPatchTool(invoker);
        var legacy = tool.CreateApprovalPresentation(CreateInput(tool, inherit: false));

        Assert.Contains("FlowEngineLib.dll|LVCameraNode", legacy.Description, StringComparison.Ordinal);
        Assert.Empty(legacy.ReviewDetails);
        Assert.Throws<InvalidOperationException>(() => tool.CreateApprovalPresentation(CreateInput(tool, inherit: true)));
        Assert.Equal(0, invoker.CallCount);
    }

    private static CopilotAgentRequest CreateRequest() => new()
    {
        ConversationId = "camera-defaults-approval-conversation",
        TaskId = "camera-defaults-approval-task",
        UserText = "添加相机节点并使用当前相机参数",
        Mode = CopilotAgentMode.Auto,
    };

    private static CopilotAgentToolInput CreateInput(CopilotApplyFlowPatchTool tool, bool inherit, bool includeFingerprint = true)
    {
        var arguments = new Dictionary<string, object?>
        {
            ["operation"] = "add_node",
            ["expected_revision"] = "rev-camera-test",
            ["type_key"] = "FlowEngineLib.dll|LVCameraNode",
            ["left"] = 100,
            ["top"] = 200,
            ["inherit_camera_defaults"] = inherit,
        };
        if (inherit && includeFingerprint)
            arguments["camera_defaults_fingerprint"] = Fingerprint;
        Assert.True(tool.InputSchema.TryBind(arguments, out var input, out var error), error);
        return input;
    }

    private sealed class PreviewInvoker(CopilotApplicationCapabilityCallResult result) : ICopilotApplicationCapabilityInvoker
    {
        public string CapabilityName { get; private set; } = string.Empty;
        public int CallCount { get; private set; }
        public Dictionary<string, JsonElement> Arguments { get; private set; } = new();

        public Task<CopilotApplicationCapabilityCallResult> InvokeAsync(
            string capabilityName, IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotApplicationCapabilityCaller caller, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CapabilityName = capabilityName;
            CallCount++;
            Arguments = (arguments ?? new Dictionary<string, JsonElement>())
                .ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(result);
        }
    }
}
