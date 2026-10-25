using ColorVision.Copilot.Mcp;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotApprovalReviewTestGroup.CollectionName)]
public sealed class CopilotFlowCameraDefaultsMcpTests
{
    private const string Fingerprint = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string TypeKey = "FlowEngineLib.dll|LVCameraNode";
    private const string WorkspacePath = @"C:\ColorVision\CameraDefaultsMcp";

    [Fact]
    public async Task PreviewAllowsInheritedDefaultsWithoutAFingerprintAndForwardsTheRequest()
    {
        var previewText = JsonSerializer.Serialize(new { cameraDefaultsFingerprint = Fingerprint, gain = 7 });
        using var session = new TestSession(CopilotMcpToolCallResult.Ok(previewText));

        var result = await session.CallAsync("preview_flow_patch", CreateArguments("add_node", true, null));

        Assert.True(result.Success);
        Assert.Equal(previewText, result.Text);
        Assert.False(result.RequiresApproval);
        var request = Assert.Single(session.PreviewRequests);
        Assert.Equal("add_node", request.Operation);
        Assert.Equal("rev-camera-test", request.ExpectedRevision);
        Assert.Equal(TypeKey, request.TypeKey);
        Assert.Equal(100, request.Left);
        Assert.Equal(200, request.Top);
        Assert.True(request.InheritCameraDefaults);
        Assert.Empty(request.CameraDefaultsFingerprint);
        Assert.Empty(session.ApplyRequests);
        Assert.Empty(session.PendingActions);
    }

    [Theory]
    [InlineData("add_node", true, null, "requires camera_defaults_fingerprint")]
    [InlineData("add_node", false, Fingerprint, "requires inherit_camera_defaults=true")]
    [InlineData("add_node", true, "ab", "64-character hexadecimal")]
    [InlineData("add_node", true, "gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg", "64-character hexadecimal")]
    [InlineData("set_property", false, null, "only supported by add_node")]
    [InlineData("connect", null, Fingerprint, "only supported by add_node")]
    public async Task InvalidCameraDefaultsArgumentsCannotReachHandlersOrCreatePendingActions(
        string operation, bool? inherit, string? fingerprint, string expectedError)
    {
        using var session = new TestSession(CopilotMcpToolCallResult.Ok("unused preview"));

        var result = await session.CallAsync("apply_flow_patch", CreateArguments(operation, inherit, fingerprint));

        Assert.False(result.Success);
        Assert.Equal("invalid_flow_patch", result.ErrorCode);
        Assert.Contains(expectedError, result.Text, StringComparison.Ordinal);
        Assert.False(result.RequiresApproval);
        Assert.Empty(result.ApprovalActionId);
        Assert.Empty(session.PreviewRequests);
        Assert.Empty(session.ApplyRequests);
        Assert.Empty(session.PendingActions);
    }

    [Fact]
    public async Task ExternalApprovalBindsTheCompleteReviewAndNormalizedFingerprintToTheFrozenRequest()
    {
        var previewText = "camera settings\nCalibration=" + new string('x', 9000) + "\u202Ecomplete-review-tail";
        using var session = new TestSession(CopilotMcpToolCallResult.Ok(previewText));
        var arguments = CreateArguments("add_node", true, " " + Fingerprint.ToUpperInvariant() + " ");
        arguments["expected_revision"] = JsonSerializer.SerializeToElement(" rev-camera-test ");
        arguments["type_key"] = JsonSerializer.SerializeToElement(" " + TypeKey + " ");

        var result = await session.CallAsync("apply_flow_patch", arguments);
        var action = session.GetAction(result);

        Assert.True(result.RequiresApproval);
        Assert.False(result.ExecuteOnApproval);
        Assert.Equal(ConfirmableActionStatus.Pending, action.Status);
        Assert.Equal(CopilotApprovalSourceKind.ExternalMcp, action.RequestContext.SourceKind);
        Assert.Equal(CopilotApprovalReviewTextEncoder.Encode(previewText), action.ReviewDetails);
        Assert.Contains(@"\u202Ecomplete-review-tail", action.ReviewDetails, StringComparison.Ordinal);
        Assert.DoesNotContain("\u202E", action.ReviewDetails, StringComparison.Ordinal);
        Assert.Equal(ExpectedArgumentsDigest(Fingerprint), action.ArgumentsDigest);
        Assert.NotEqual(ExpectedArgumentsDigest(new string('b', 64)), action.ArgumentsDigest);
        var reviewedRequest = Assert.Single(session.PreviewRequests);
        Assert.True(reviewedRequest.InheritCameraDefaults);
        Assert.Equal(Fingerprint, reviewedRequest.CameraDefaultsFingerprint);
        Assert.Empty(session.ApplyRequests);

        arguments["inherit_camera_defaults"] = JsonSerializer.SerializeToElement(false);
        arguments["camera_defaults_fingerprint"] = JsonSerializer.SerializeToElement(new string('b', 64));
        Assert.True(CopilotMcpConfirmationStore.Instance.Approve(
            action.ActionId, new CopilotConfirmationReviewContext(string.Empty, string.Empty, WorkspacePath), out _));

        var mismatchedConfirmation = await session.CallAsync("confirm_action", ConfirmArguments(action, ExpectedArgumentsDigest(new string('b', 64))));
        Assert.False(mismatchedConfirmation.Success);
        Assert.Equal("action_arguments_mismatch", mismatchedConfirmation.ErrorCode);
        Assert.Equal(ConfirmableActionStatus.Approved, action.Status);
        Assert.Empty(session.ApplyRequests);

        var confirmation = await session.CallAsync("confirm_action", ConfirmArguments(action, action.ArgumentsDigest));

        Assert.True(confirmation.Success);
        Assert.Equal(ConfirmableActionStatus.Executed, action.Status);
        Assert.Same(reviewedRequest, Assert.Single(session.ApplyRequests));
        Assert.Equal(Fingerprint, session.ApplyRequests[0].CameraDefaultsFingerprint);
        Assert.True(session.ApplyRequests[0].InheritCameraDefaults);
        Assert.Single(session.PreviewRequests);
    }

    [Fact]
    public async Task ChangedCameraDefaultsDuringApplyPreviewCannotCreatePendingAction()
    {
        using var session = new TestSession(CopilotMcpToolCallResult.Fail(
            "camera_defaults_changed", "Camera defaults changed after preview; preview the addition again."));

        var result = await session.CallAsync("apply_flow_patch", CreateArguments("add_node", true, Fingerprint));

        Assert.False(result.Success);
        Assert.Equal("camera_defaults_changed", result.ErrorCode);
        Assert.False(result.RequiresApproval);
        Assert.Empty(result.ApprovalActionId);
        var request = Assert.Single(session.PreviewRequests);
        Assert.True(request.InheritCameraDefaults);
        Assert.Equal(Fingerprint, request.CameraDefaultsFingerprint);
        Assert.Empty(session.ApplyRequests);
        Assert.Empty(session.PendingActions);
    }

    [Fact]
    public async Task CancelledApplyCannotPublishPendingActionAfterNonCooperativePreviewReturns()
    {
        var typeKey = TypeKey + "-cancelled-" + Guid.NewGuid().ToString("N");
        var releasePreview = new TaskCompletionSource<CopilotMcpToolCallResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var applyCalls = 0;
        var dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
        {
            PreviewFlowPatchHandler = (_, _) => releasePreview.Task,
            ApplyFlowPatchHandler = (_, _) =>
            {
                Interlocked.Increment(ref applyCalls);
                return Task.FromResult(CopilotMcpToolCallResult.Ok("applied"));
            },
        });
        using var cancellation = new CancellationTokenSource();
        var arguments = CreateArguments("add_node", true, Fingerprint);
        arguments["type_key"] = JsonSerializer.SerializeToElement(typeKey);
        var apply = dispatcher.ApplyFlowPatchAsync(
            arguments, CopilotExecutionScope.ForExternalMcpSession(typeKey, typeKey, WorkspacePath), cancellation.Token);
        try
        {
            Assert.False(apply.IsCompleted);
            cancellation.Cancel();
            releasePreview.SetResult(CopilotMcpToolCallResult.Ok("late successful camera preview"));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => apply.WaitAsync(TimeSpan.FromSeconds(2)));

            Assert.Equal(0, Volatile.Read(ref applyCalls));
            Assert.DoesNotContain(CopilotMcpConfirmationStore.Instance.GetPendingActions(),
                action => action.Description.Contains(typeKey, StringComparison.Ordinal));
        }
        finally
        {
            releasePreview.TrySetResult(CopilotMcpToolCallResult.Ok("cleanup"));
            foreach (var action in CopilotMcpConfirmationStore.Instance.GetPendingActions()
                .Where(action => action.Description.Contains(typeKey, StringComparison.Ordinal)))
            {
                CopilotMcpConfirmationStore.Instance.Cancel(action.ActionId, out _, "Cancelled camera preview test cleanup.");
            }
        }
    }

    private static Dictionary<string, JsonElement> CreateArguments(string operation, bool? inherit, string? fingerprint)
    {
        var arguments = new Dictionary<string, JsonElement>
        {
            ["operation"] = JsonSerializer.SerializeToElement(operation),
            ["expected_revision"] = JsonSerializer.SerializeToElement("rev-camera-test"),
        };
        var operationArguments = operation switch
        {
            "add_node" => new Dictionary<string, object?> { ["type_key"] = TypeKey, ["left"] = 100, ["top"] = 200 },
            "set_property" => new Dictionary<string, object?> { ["node_id"] = "node-test", ["property_name"] = "Gain", ["value"] = "7" },
            _ => new Dictionary<string, object?> { ["source_node_id"] = "source-test", ["source_port_id"] = "out:0", ["target_node_id"] = "target-test", ["target_port_id"] = "in:0" },
        };
        foreach (var pair in operationArguments)
            arguments[pair.Key] = JsonSerializer.SerializeToElement(pair.Value);
        if (inherit.HasValue)
            arguments["inherit_camera_defaults"] = JsonSerializer.SerializeToElement(inherit.Value);
        if (fingerprint != null)
            arguments["camera_defaults_fingerprint"] = JsonSerializer.SerializeToElement(fingerprint);
        return arguments;
    }

    private static Dictionary<string, JsonElement> ConfirmArguments(ConfirmableAction action, string digest) => new()
    {
        ["action_id"] = JsonSerializer.SerializeToElement(action.ActionId),
        ["tool_name"] = JsonSerializer.SerializeToElement(action.ToolName),
        ["arguments_digest"] = JsonSerializer.SerializeToElement(digest),
    };

    private static string ExpectedArgumentsDigest(string fingerprint)
    {
        var binding = JsonSerializer.Serialize(new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["operation"] = "add_node",
            ["expected_revision"] = "rev-camera-test",
            ["type_key"] = TypeKey,
            ["left"] = 100,
            ["top"] = 200,
            ["inherit_camera_defaults"] = true,
            ["camera_defaults_fingerprint"] = fingerprint,
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(binding))).ToLowerInvariant();
    }

    private sealed class TestSession : IDisposable
    {
        private readonly string _callerSource = "camera-defaults-mcp-" + Guid.NewGuid().ToString("N");
        private readonly CopilotMcpToolDispatcher _dispatcher;
        private readonly List<ConfirmableAction> _createdActions = [];

        public TestSession(CopilotMcpToolCallResult previewResult)
        {
            _dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
            {
                WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot { SolutionDirectoryPath = WorkspacePath },
                PreviewFlowPatchHandler = (request, _) =>
                {
                    PreviewRequests.Add(request);
                    return Task.FromResult(previewResult);
                },
                ApplyFlowPatchHandler = (request, _) =>
                {
                    ApplyRequests.Add(request);
                    return Task.FromResult(CopilotMcpToolCallResult.Ok("applied"));
                },
            });
        }

        public List<CopilotFlowPatchRequest> PreviewRequests { get; } = [];
        public List<CopilotFlowPatchRequest> ApplyRequests { get; } = [];
        public IReadOnlyList<ConfirmableAction> PendingActions => CopilotMcpConfirmationStore.Instance.GetPendingActions()
            .Where(action => action.RequestContext.RequestSource == _callerSource).ToArray();

        public Task<CopilotMcpToolCallResult> CallAsync(string toolName, IReadOnlyDictionary<string, JsonElement> arguments) =>
            _dispatcher.CallExternalAsync(toolName, arguments, _callerSource, CancellationToken.None);

        public ConfirmableAction GetAction(CopilotMcpToolCallResult result)
        {
            var action = Assert.Single(PendingActions, item => item.ActionId == result.ApprovalActionId);
            _createdActions.Add(action);
            return action;
        }

        public void Dispose()
        {
            foreach (var action in _createdActions.Concat(PendingActions).DistinctBy(item => item.ActionId))
                CopilotMcpConfirmationStore.Instance.Cancel(action.ActionId, out _, "Camera defaults MCP test cleanup.");
        }
    }
}
