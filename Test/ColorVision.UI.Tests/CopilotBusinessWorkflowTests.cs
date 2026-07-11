using ColorVision.UI;

namespace ColorVision.UI.Tests;

public sealed class CopilotBusinessWorkflowTests : IDisposable
{
    [Fact]
    public void FlowDiagnosisPromptRequiresEvidenceRankingAndGuardedTemplateWorkflow()
    {
        var snapshot = new CopilotFlowContextSnapshot
        {
            FlowName = "Camera inspection",
            FocusedNodeSummary = "Capture / CameraNode",
            RecentFailureSummary = "Camera timeout",
            FailureEvidence = new[] { "Camera timeout after 30000ms" },
        };

        var prompt = CopilotBusinessContextCoordinator.BuildFlowDiagnosisPrompt(snapshot, "Analyze this flow.");

        Assert.Contains("Current state and focused node", prompt, StringComparison.Ordinal);
        Assert.Contains("Ranked probable causes", prompt, StringComparison.Ordinal);
        Assert.Contains("suggest_template_patch -> preview_template_patch -> apply_template_patch", prompt, StringComparison.Ordinal);
        Assert.Contains("Capture / CameraNode", prompt, StringComparison.Ordinal);
        Assert.Contains("requires ColorVision user approval", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void FlowContextIncludesFocusedNodeAndFailureEvidence()
    {
        var item = CopilotBusinessContextBuilder.BuildFlowContextItem(new CopilotFlowContextSnapshot
        {
            FlowName = "Inspection",
            Status = "Failed",
            FocusedNodeSummary = "Camera",
            FailureEvidence = new[] { "timeout", "response failed" },
        });

        Assert.Contains("Focused node: Camera", item.Content, StringComparison.Ordinal);
        Assert.Contains("Failure evidence:", item.Content, StringComparison.Ordinal);
        Assert.Contains("- timeout", item.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void CoordinatorPublishesAndDispatchesTheSameSnapshotBundle()
    {
        var item = new CopilotContextItem
        {
            Id = "device:test",
            Title = "Test device",
            Summary = "Offline",
            Content = "Device status: Offline",
        };
        var bundle = CopilotBusinessContextBundle.FromItem("device:test", item);
        var service = new RecordingCopilotService();

        var result = CopilotBusinessContextCoordinator.DispatchDiagnosis(bundle, "Diagnose device", service: service);

        Assert.True(result.WasSent);
        Assert.Equal("device:test", CopilotLiveContextRegistry.Current?.SourceId);
        Assert.NotNull(service.LastRequest);
        Assert.True(service.LastRequest!.AttachContextSnapshot);
        Assert.Same(item, Assert.Single(service.LastRequest.ContextItems));
    }

    [Fact]
    public void DeviceDiagnosisPromptKeepsOperationReadOnlyAndSensitiveDataOut()
    {
        var snapshot = new CopilotDeviceContextSnapshot
        {
            ServiceName = "Camera",
            ServiceCode = "CAM-1",
            DeviceStatus = "Offline",
        };

        var prompt = CopilotBusinessContextCoordinator.BuildDeviceDiagnosisPrompt(snapshot);

        Assert.Contains("read-only verification steps", prompt, StringComparison.Ordinal);
        Assert.Contains("do not claim", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("passwords, tokens, serial numbers", prompt, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        CopilotLiveContextRegistry.Clear("device:test");
    }

    private sealed class RecordingCopilotService : ICopilotService
    {
        public bool IsAvailable => true;

        public CopilotPromptRequest? LastRequest { get; private set; }

        public void ShowPanel()
        {
        }

        public bool Ask(CopilotPromptRequest request)
        {
            LastRequest = request;
            return true;
        }
    }
}
