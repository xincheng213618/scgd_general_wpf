using ColorVision.Engine;
using ColorVision.UI;

namespace ColorVision.UI.Tests;

public sealed class CopilotMeasurementResultContextProviderTests
{
    [Fact]
    public async Task MeasurementResultProvider_CapturesFreshSnapshotForRelevantRequests()
    {
        var batchId = 101;
        var captureCount = 0;
        var provider = new CopilotMeasurementResultContextProvider(_ =>
        {
            captureCount++;
            return Task.FromResult<CopilotMeasurementResultContextSnapshot?>(CreateSnapshot(batchId));
        });

        var first = await provider.CaptureAsync(CreateRequest("分析当前检测结果"), CancellationToken.None);
        batchId = 202;
        var second = await provider.CaptureAsync(CreateRequest("check this batch result"), CancellationToken.None);

        Assert.Equal(2, captureCount);
        Assert.Contains("Internal batch id: 101", Assert.IsType<CopilotContextItem>(first).Content, StringComparison.Ordinal);
        Assert.Contains("Internal batch id: 202", Assert.IsType<CopilotContextItem>(second).Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MeasurementResultProvider_SkipsUnrelatedTurnsButSupportsCurrentSurfaceAndDiagnose()
    {
        var captureCount = 0;
        var isCurrentSurface = false;
        var provider = new CopilotMeasurementResultContextProvider(
            _ =>
            {
                captureCount++;
                return Task.FromResult<CopilotMeasurementResultContextSnapshot?>(CreateSnapshot(101));
            },
            isCurrentSurface: () => isCurrentSurface);

        Assert.Null(await provider.CaptureAsync(CreateRequest("解释这段代码"), CancellationToken.None));
        Assert.Equal(0, captureCount);

        isCurrentSurface = true;
        Assert.NotNull(await provider.CaptureAsync(CreateRequest("这个怎么样？"), CancellationToken.None));
        isCurrentSurface = false;
        Assert.NotNull(await provider.CaptureAsync(CreateRequest("继续排查", CopilotContextScope.Diagnose), CancellationToken.None));
        Assert.Equal(2, captureCount);
    }

    [Fact]
    public async Task MeasurementResultProvider_DropsSnapshotWhenPageBecomesInactiveDuringCapture()
    {
        var active = true;
        var provider = new CopilotMeasurementResultContextProvider(
            _ =>
            {
                active = false;
                return Task.FromResult<CopilotMeasurementResultContextSnapshot?>(CreateSnapshot(101));
            },
            () => active);

        var result = await provider.CaptureAsync(CreateRequest("查看测量结果"), CancellationToken.None);

        Assert.Null(result);
        Assert.False(provider.CanProvide(CopilotContextScope.Agent));
    }

    [Fact]
    public void MeasurementResultExtension_UsesStableReadOnlyContextMetadata()
    {
        var registry = new CopilotAgentExtensionRegistry();
        var provider = new CopilotMeasurementResultContextProvider(_ => Task.FromResult<CopilotMeasurementResultContextSnapshot?>(null));

        using (CopilotMeasurementResultAgentExtension.Register(registry, provider, "3.1.4"))
        {
            var extension = Assert.Single(registry.GetSnapshot().Extensions);
            Assert.Equal(CopilotMeasurementResultAgentExtension.SourceId, extension.SourceId);
            Assert.Equal("Measurement Results", extension.SourceName);
            Assert.Equal("3.1.4", extension.SourceVersion);
            Assert.Same(provider, Assert.Single(extension.ContextProviders));
            Assert.Empty(extension.Tools);
        }

        Assert.Empty(registry.GetSnapshot().Extensions);
    }

    [Fact]
    public void MeasurementResultBuilder_ReportsShapeWithoutIdentifiersPathsPayloadsOrValues()
    {
        var snapshot = CreateSnapshot(101);

        var item = CopilotBusinessContextBuilder.BuildMeasurementResultContextItem(snapshot);

        Assert.Contains("Internal batch id: 101", item.Content, StringComparison.Ordinal);
        Assert.Contains("Batch identifier: Withheld", item.Content, StringComparison.Ordinal);
        Assert.Contains("Result message present: Yes (content withheld)", item.Content, StringComparison.Ordinal);
        Assert.Contains("Image results: 8", item.Content, StringComparison.Ordinal);
        Assert.Contains("Failed image results: 2", item.Content, StringComparison.Ordinal);
        Assert.Contains("Algorithm results: 5", item.Content, StringComparison.Ordinal);
        Assert.Contains("Referenced file available: No", item.Content, StringComparison.Ordinal);
        Assert.Contains("password=<redacted>", item.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("token=<redacted>", item.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("template-secret", item.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("algorithm-secret", item.Content, StringComparison.Ordinal);
        Assert.Contains("file paths, request parameters, raw result messages, payloads, and measured values are withheld", item.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FlowContextBuilder_RedactsKnownBatchSerialNumberField()
    {
        var item = CopilotBusinessContextBuilder.BuildFlowContextItem(new CopilotFlowContextSnapshot
        {
            SourceId = "flow-test",
            FlowName = "Inspection flow",
            BatchSerialNumber = "SN-PRIVATE-001",
            BatchResult = "raw-result-payload-should-not-leak",
        });

        Assert.Contains("Batch serial number: <redacted>", item.Content, StringComparison.Ordinal);
        Assert.Contains("Batch result message: Present (content withheld)", item.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("SN-PRIVATE-001", item.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-result-payload-should-not-leak", item.Content, StringComparison.Ordinal);
    }

    private static CopilotContextRequest CreateRequest(string userText, CopilotContextScope scope = CopilotContextScope.Agent)
    {
        return new CopilotContextRequest { Scope = scope, UserText = userText };
    }

    private static CopilotMeasurementResultContextSnapshot CreateSnapshot(int batchId)
    {
        return new CopilotMeasurementResultContextSnapshot
        {
            Surface = "Measurement batch details",
            LoadedBatchCount = 1,
            BatchId = batchId,
            TemplateId = 17,
            TemplateName = "AOI password=template-secret",
            BatchStatus = "Completed",
            CreatedAt = "2026-07-16T12:00:00.0000000+08:00",
            TotalTimeMilliseconds = 3200,
            ArchiveStatus = "Archived",
            HasResultMessage = true,
            HasLoadedDetails = true,
            ImageResultCount = 8,
            FailedImageResultCount = 2,
            AlgorithmResultCount = 5,
            FailedAlgorithmResultCount = 1,
            UnknownAlgorithmResultCount = 1,
            SelectedResultKind = "Algorithm result",
            SelectedResultId = 88,
            SelectedResultType = "SFR",
            SelectedResultTemplateName = "SFR token=algorithm-secret",
            SelectedResultCode = "0",
            SelectedResultDuration = "120 ms",
            SelectedResultCreatedAt = "2026-07-16T12:00:01.0000000+08:00",
            SelectedResultFileAvailable = false,
        };
    }
}
