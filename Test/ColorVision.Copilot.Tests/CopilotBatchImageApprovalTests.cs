using ColorVision.Copilot.Mcp;
using ColorVision.ImageEditor.Algorithms;
using System.IO;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotApprovalReviewTestGroup.CollectionName)]
public sealed class CopilotBatchImageApprovalTests
{
    [Fact]
    public async Task ApprovalPreservesAllSourcesAndCompleteParametersBeyondTheShortDescription()
    {
        var sources = Enumerable.Range(0, 6).Select(index => $"images/source-{index}.png").ToArray();
        var parametersJson = "{\"lowThreshold\":20,\"highThreshold\":200." + new string('0', 1500) + ",\"apertureSize\":3,\"l2Gradient\":true}";
        using var parameters = JsonDocument.Parse(parametersJson);
        var suffix = "_human-review-" + new string('x', 35) + "-FINAL-TAIL";
        var input = new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?>
            {
                ["sources"] = sources,
                ["outputDirectory"] = "images/output",
                ["format"] = "png",
                ["algorithm"] = StandardAlgorithmIds.Canny.Value,
                ["parameters"] = parameters.RootElement.Clone(),
                ["recursive"] = true,
                ["preserveFolderStructure"] = false,
                ["suffix"] = suffix,
            },
        };
        var coordinator = new CopilotFrameworkApprovalCoordinator();
        var handle = RequestApproval(coordinator, input);
        try
        {
            Assert.True(handle.Action.HasReviewDetails);
            Assert.DoesNotContain(sources[5], handle.Action.Description, StringComparison.Ordinal);
            Assert.All(sources, source => Assert.Contains(source, handle.Action.ReviewDetails, StringComparison.Ordinal));
            Assert.Contains(parametersJson, handle.Action.ReviewDetails, StringComparison.Ordinal);
            Assert.Contains("recursive: true", handle.Action.ReviewDetails, StringComparison.Ordinal);
            Assert.Contains("preserveFolderStructure: false", handle.Action.ReviewDetails, StringComparison.Ordinal);
            Assert.Contains("suffix (effective): " + suffix, handle.Action.ReviewDetails, StringComparison.Ordinal);
            Assert.Contains("maximumFiles: 500", handle.Action.ReviewDetails, StringComparison.Ordinal);
            Assert.Contains("avoidOverwrite: true", handle.Action.ReviewDetails, StringComparison.Ordinal);
        }
        finally
        {
            await CancelAsync(coordinator, handle);
        }
    }

    [Fact]
    public async Task ApprovalExplainsDefaultsEscapesInvisibleSourceCharactersAndClearsDetailsOnCancellation()
    {
        var input = new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?>
            {
                ["sources"] = new[] { "images/review\u202Ecod\u200B.png" },
                ["algorithm"] = StandardAlgorithmIds.Canny.Value,
            },
        };
        var coordinator = new CopilotFrameworkApprovalCoordinator();
        var handle = RequestApproval(coordinator, input);
        try
        {
            Assert.Contains(@"images/review\u202Ecod\u200B.png", handle.Action.ReviewDetails, StringComparison.Ordinal);
            Assert.DoesNotContain("\u202E", handle.Action.ReviewDetails, StringComparison.Ordinal);
            Assert.DoesNotContain("\u200B", handle.Action.ReviewDetails, StringComparison.Ordinal);
            Assert.Contains("outputDirectory: (beside each source)", handle.Action.ReviewDetails, StringComparison.Ordinal);
            Assert.Contains("parameters (provided JSON): (Catalog defaults)", handle.Action.ReviewDetails, StringComparison.Ordinal);
            Assert.Contains("suffix (requested): (default)", handle.Action.ReviewDetails, StringComparison.Ordinal);
            Assert.Contains("suffix (effective): _canny", handle.Action.ReviewDetails, StringComparison.Ordinal);
        }
        finally
        {
            await CancelAsync(coordinator, handle);
        }
        Assert.Empty(handle.Action.ReviewDetails);
        Assert.False(handle.Action.HasReviewDetails);
    }

    [Fact]
    public async Task EncodedCompleteReviewOverTheLimitIsRejectedBeforePublishingPendingAction()
    {
        var sources = Enumerable.Range(0, 8)
            .Select(index => "images/" + new string('\u200B', 3000) + $"-{index}.png")
            .ToArray();
        Assert.True(sources.Sum(source => source.Length) < CopilotMcpConfirmationStore.MaximumReviewDetailsCharacters);
        var input = new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?> { ["sources"] = sources },
        };

        var exception = await AssertRejectedBeforePendingAsync<ArgumentException>(input);

        Assert.Contains(CopilotMcpConfirmationStore.MaximumReviewDetailsCharacters.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"not-an-object\"")]
    [InlineData("{\"highThreshold\":-1}")]
    public async Task InvalidParameterJsonIsRejectedBeforePublishingPendingAction(string parametersJson)
    {
        using var parameters = JsonDocument.Parse(parametersJson);
        var input = new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?>
            {
                ["sources"] = new[] { "images/source.png" },
                ["algorithm"] = StandardAlgorithmIds.Canny.Value,
                ["parameters"] = parameters.RootElement.Clone(),
            },
        };

        await AssertRejectedBeforePendingAsync<InvalidOperationException>(input);
    }

    private static async Task<TException> AssertRejectedBeforePendingAsync<TException>(CopilotAgentToolInput input)
        where TException : Exception
    {
        var coordinator = new CopilotFrameworkApprovalCoordinator();
        var callId = "batch-image-review-" + Guid.NewGuid().ToString("N");
        var publishedPending = 0;
        CopilotFrameworkApprovalHandle? handle = null;
        EventHandler<ConfirmableActionChangedEventArgs> changed = (_, args) =>
        {
            if (args.Action.AgentCallId == callId && args.Action.Status == ConfirmableActionStatus.Pending)
                Interlocked.Increment(ref publishedPending);
        };
        CopilotMcpConfirmationStore.Instance.ActionStatusChanged += changed;
        try
        {
            var exception = Record.Exception(() => { handle = RequestApproval(coordinator, input, callId); });
            var rejection = Assert.IsType<TException>(exception);
            Assert.Equal(0, Volatile.Read(ref publishedPending));
            Assert.DoesNotContain(CopilotMcpConfirmationStore.Instance.GetPendingActions(), action => action.AgentCallId == callId);
            return rejection;
        }
        finally
        {
            CopilotMcpConfirmationStore.Instance.ActionStatusChanged -= changed;
            if (handle != null)
                await CancelAsync(coordinator, handle);
        }
    }

    private static CopilotFrameworkApprovalHandle RequestApproval(
        CopilotFrameworkApprovalCoordinator coordinator,
        CopilotAgentToolInput input,
        string? callId = null) => coordinator.RequestApproval(
            new CopilotConvertBatchImagesTool(),
            new CopilotAgentRequest
            {
                ConversationId = "batch-image-review-conversation",
                TaskId = "batch-image-review-task-" + Guid.NewGuid().ToString("N"),
                WorkspacePath = Path.GetFullPath(Path.GetTempPath()),
                UserText = "Apply Canny to the requested local images.",
            },
            input,
            callId ?? "batch-image-review-" + Guid.NewGuid().ToString("N"),
            CancellationToken.None);

    private static async Task CancelAsync(CopilotFrameworkApprovalCoordinator coordinator, CopilotFrameworkApprovalHandle handle)
    {
        coordinator.Cancel(handle);
        var decision = await handle.Decision.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(CopilotFrameworkApprovalDecisionKind.Cancelled, decision.Kind);
    }
}
