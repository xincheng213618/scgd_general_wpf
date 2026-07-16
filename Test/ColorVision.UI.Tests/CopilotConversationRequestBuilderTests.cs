using ColorVision.Copilot;
using ColorVision.UI;
using System.IO;
using System.Threading;

namespace ColorVision.UI.Tests;

public sealed class CopilotConversationRequestBuilderTests : IDisposable
{
    private static readonly CopilotConversationHistoryLimits GenerousLimits = new(64, 100_000, 24_000);
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "ColorVision-Conversation-Request-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void BuildChatHistoryUsesProvidedConversationModelContentAndAttachmentSnapshot()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Model");
        var earlierUser = new CopilotChatMessage(CopilotChatRole.User, "raw earlier request")
        {
            RequestContent = "prepared earlier request",
        };
        var currentUser = new CopilotChatMessage(CopilotChatRole.User, "raw current request")
        {
            RequestContent = "prepared current request",
        };
        conversation.Messages.Add(earlierUser);
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "earlier answer"));
        conversation.Messages.Add(currentUser);
        var attachment = CopilotAttachmentItem.CreateContext("SNAPSHOT-CONTENT", "Business snapshot", "test");
        var historySnapshot = CopilotConversationRequestBuilder.CaptureHistorySnapshot(conversation, currentUser);
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "LATE-MUTATION"));

        var history = CopilotConversationRequestBuilder.BuildChatHistory(
            historySnapshot,
            currentUser.RequestContent,
            [attachment],
            GenerousLimits,
            includeAttachmentContext: true);

        Assert.Equal(4, history.Count);
        Assert.Contains("SNAPSHOT-CONTENT", history[0].Content, StringComparison.Ordinal);
        Assert.Equal("prepared earlier request", history[1].Content);
        Assert.Equal("earlier answer", history[2].Content);
        Assert.Equal("prepared current request", history[3].Content);
        Assert.DoesNotContain(history, message => message.Content == "LATE-MUTATION");
    }

    [Fact]
    public void BuildVisibleHistoryStopsBeforeCurrentTurnAndNeverReplaysInjectedModelContent()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Model");
        var earlierUser = new CopilotChatMessage(CopilotChatRole.User, "visible earlier request")
        {
            RequestContent = "INJECTED-EARLIER-CONTEXT",
        };
        var currentUser = new CopilotChatMessage(CopilotChatRole.User, "visible current request")
        {
            RequestContent = "INJECTED-CURRENT-CONTEXT",
        };
        conversation.Messages.Add(earlierUser);
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "earlier answer"));
        conversation.Messages.Add(currentUser);
        var historySnapshot = CopilotConversationRequestBuilder.CaptureHistorySnapshot(conversation, currentUser);

        var history = CopilotConversationRequestBuilder.BuildVisibleHistory(historySnapshot, GenerousLimits);

        Assert.Equal(2, history.Count);
        Assert.Equal("visible earlier request", history[0].Content);
        Assert.Equal("earlier answer", history[1].Content);
        Assert.DoesNotContain(history, message => message.Content.Contains("INJECTED-", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildAttachmentContextReadsOnlyBoundedFilePrefix()
    {
        Directory.CreateDirectory(_tempRoot);
        var filePath = Path.Combine(_tempRoot, "large.txt");
        File.WriteAllText(filePath, new string('a', CopilotConversationRequestBuilder.AttachmentContentLimit) + "SENTINEL-AFTER-LIMIT");

        var context = CopilotConversationRequestBuilder.BuildAttachmentContextBlock(
            [CopilotAttachmentItem.CreateFile(filePath)]);

        Assert.Contains("...<truncated>", context, StringComparison.Ordinal);
        Assert.DoesNotContain("SENTINEL-AFTER-LIMIT", context, StringComparison.Ordinal);
        Assert.Contains("~~~txt", context, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildUserRequestContentUsesCapturedLiveContextAndFetchedPages()
    {
        var fetchedUrls = new List<string>();
        var builder = new CopilotConversationRequestBuilder((url, _) =>
        {
            fetchedUrls.Add(url);
            return Task.FromResult(new CopilotFetchedWebPageContent(url, "Fetched title", "Fetched description", "FETCHED-BODY"));
        });
        var liveContext = new CopilotLiveContext
        {
            SourceId = "flow-editor",
            Title = "Flow editor · Camera calibration",
            Summary = "A camera node is selected.",
        };

        var content = await builder.BuildUserRequestContentAsync(
            "Inspect https://example.test/page and explain the selected node.",
            liveContext,
            CancellationToken.None);

        Assert.Equal(["https://example.test/page"], fetchedUrls);
        Assert.Contains("[Current Window Context]", content, StringComparison.Ordinal);
        Assert.Contains("Flow editor · Camera calibration", content, StringComparison.Ordinal);
        Assert.Contains("[Local Web Context Injection]", content, StringComparison.Ordinal);
        Assert.Contains("FETCHED-BODY", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildUserRequestContentContainsFetchFailureAndHonorsCancellation()
    {
        var builder = new CopilotConversationRequestBuilder((_, _) => throw new InvalidOperationException("token=web-secret\r\noffline"));

        var failed = await builder.BuildUserRequestContentAsync("Read https://example.test/failure", null, CancellationToken.None);

        Assert.Contains("[Web Page Fetch Failed]", failed, StringComparison.Ordinal);
        Assert.Contains("offline", failed, StringComparison.Ordinal);
        Assert.Contains("token=<redacted> offline", failed, StringComparison.Ordinal);
        Assert.DoesNotContain("web-secret", failed, StringComparison.Ordinal);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            builder.BuildUserRequestContentAsync("Read https://example.test/cancel", null, cancellation.Token));
    }

    [Fact]
    public async Task BuildUserRequestContentBoundsInjectedWebContext()
    {
        var builder = new CopilotConversationRequestBuilder((url, _) => Task.FromResult(
            new CopilotFetchedWebPageContent(url, "Large", string.Empty, new string('w', CopilotConversationRequestBuilder.MaximumWebContextCharacters + 1_000))));

        var content = await builder.BuildUserRequestContentAsync("Read https://example.test/large", null, CancellationToken.None);

        Assert.Contains("...<web context truncated>", content, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('w', CopilotConversationRequestBuilder.MaximumWebContextCharacters + 1), content, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, true);
    }
}
