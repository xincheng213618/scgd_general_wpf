using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotConversationTitleUsageTests
{
    [Fact]
    public async Task GeneratorReturnsNormalizedTitleAndProviderUsage()
    {
        var usage = new CopilotTokenUsage(40, 6, 46, 20);
        var generator = new CopilotConversationTitleGenerator((_, _, _) =>
            Task.FromResult(CreateReply("\"测试标题\"", usage)));

        var result = await generator.GenerateAsync(
            new CopilotConversationTitleRequest(CreateProfile(), "Create a title."),
            CancellationToken.None);

        Assert.Equal("测试标题", result.Title);
        Assert.Equal(usage, result.Usage);
        Assert.Equal(TimeSpan.Zero, result.CompletedAtUtc.Offset);
    }

    [Fact]
    public async Task GeneratorPreservesUsageWhenTitleResponseIsIncomplete()
    {
        var usage = new CopilotTokenUsage(40, 6, 46);
        var generator = new CopilotConversationTitleGenerator((_, _, _) =>
            Task.FromResult(CreateReply(
                "Partial title",
                usage,
                CopilotChatFinishKind.LengthLimit)));

        var result = await generator.GenerateAsync(
            new CopilotConversationTitleRequest(CreateProfile(), "Create a title."),
            CancellationToken.None);

        Assert.Null(result.Title);
        Assert.Equal(usage, result.Usage);
    }

    [Theory]
    [InlineData("official_failure")]
    [InlineData("cancel_billed_backoff")]
    [InlineData("unknown_usage")]
    public async Task CoordinatorRecordsSettledFailureUsageForItsCapturedConversation(string outcome)
    {
        var conversation = CreateTitleCandidateConversation();
        var otherConversation = CreateTitleCandidateConversation();
        var originalTitle = conversation.Title;
        var originalMessages = conversation.Messages.ToArray();
        var profile = CreateProfile();
        profile.BaseUrl = "https://example.test/v1/responses";
        var canceled = outcome == "cancel_billed_backoff";
        var hasReportedUsage = outcome != "unknown_usage";
        using var handler = new TitleFailureHandler(canceled ? "server_error" : "insufficient_quota", hasReportedUsage);
        using var httpClient = new HttpClient(handler);
        CopilotConversationTitleCoordinator? coordinator = null;
        var retryDelays = 0;
        var service = new CopilotChatService(httpClient, 3, _ => TimeSpan.Zero, (_, token) =>
        {
            retryDelays++;
            coordinator!.Cancel(conversation.Id);
            Assert.True(token.IsCancellationRequested);
            return Task.FromCanceled(token);
        });
        CopilotConversationRecord? deliveredConversation = null;
        CopilotConversationTitleGenerationResult? delivered = null;
        var deliveredAsCurrent = false;
        var deliveredAfterCancellation = false;
        var applicationCalls = 0;
        using var titleCoordinator = new CopilotConversationTitleCoordinator(
            new CopilotConversationTitleGenerator(service),
            (target, result, isCurrentGeneration, cancellationToken) =>
            {
                applicationCalls++;
                deliveredConversation = target;
                delivered = result;
                deliveredAsCurrent = isCurrentGeneration();
                deliveredAfterCancellation = cancellationToken.IsCancellationRequested;
                target.RecordTitleGenerationUsage(result.Usage, result.CompletedAtUtc);
                return Task.CompletedTask;
            });
        coordinator = titleCoordinator;

        await titleCoordinator.QueueAsync(conversation, profile);

        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(canceled ? 1 : 0, retryDelays);
        Assert.Equal(originalTitle, conversation.Title);
        Assert.False(conversation.HasCustomTitle);
        Assert.Equal(originalMessages, conversation.Messages.ToArray());
        Assert.Null(otherConversation.TitleGenerationUsage);
        Assert.Equal(hasReportedUsage ? 1 : 0, applicationCalls);
        if (hasReportedUsage)
        {
            Assert.Same(conversation, deliveredConversation);
            Assert.NotNull(delivered);
            Assert.Null(delivered.Title);
            Assert.Equal(new CopilotTokenUsage(12, 8, 20, 3), delivered.Usage);
            Assert.Equal(TimeSpan.Zero, delivered.CompletedAtUtc.Offset);
            Assert.Equal(!canceled, deliveredAsCurrent);
            Assert.Equal(canceled, deliveredAfterCancellation);
            Assert.Equal(1, conversation.TitleGenerationUsage?.RequestCount);
            Assert.Equal(new CopilotTokenUsage(12, 8, 20, 3), conversation.TitleGenerationUsage?.Usage);
        }
        else
        {
            Assert.Null(delivered);
            Assert.Null(conversation.TitleGenerationUsage);
        }
    }

    [Fact]
    public async Task CoordinatorDeliversUsageWhenGenerationWasCanceledAfterProviderResponse()
    {
        var conversation = CreateTitleCandidateConversation();
        var usage = new CopilotTokenUsage(32, 4, 36, 16);
        CopilotConversationTitleCoordinator? coordinator = null;
        var generator = new CopilotConversationTitleGenerator((_, _, _) =>
        {
            coordinator!.Cancel(conversation.Id);
            return Task.FromResult(CreateReply("Canceled title", usage));
        });
        CopilotConversationTitleGenerationResult? delivered = null;
        coordinator = new CopilotConversationTitleCoordinator(
            generator,
            (target, result, isCurrentGeneration, cancellationToken) =>
            {
                Assert.Same(conversation, target);
                Assert.False(isCurrentGeneration());
                Assert.True(cancellationToken.IsCancellationRequested);
                delivered = result;
                target.RecordTitleGenerationUsage(result.Usage, result.CompletedAtUtc);
                return Task.CompletedTask;
            });

        await coordinator.QueueAsync(conversation, CreateProfile());

        Assert.NotNull(delivered);
        Assert.Equal(usage, delivered.Usage);
        Assert.Equal(1, conversation.TitleGenerationUsage?.RequestCount);
        Assert.Equal(usage, conversation.TitleGenerationUsage?.Usage);
    }

    [Fact]
    public async Task CoordinatorDropsLateResultAfterItIsDisposed()
    {
        var conversation = CreateTitleCandidateConversation();
        var completion = new TaskCompletionSource<CopilotCompletedReplyResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var generator = new CopilotConversationTitleGenerator((_, _, _) => completion.Task);
        var applicationCalls = 0;
        var coordinator = new CopilotConversationTitleCoordinator(
            generator,
            (_, _, _, _) =>
            {
                applicationCalls++;
                return Task.CompletedTask;
            });

        var queued = coordinator.QueueAsync(conversation, CreateProfile());
        coordinator.Dispose();
        completion.SetResult(CreateReply(
            "Late title",
            new CopilotTokenUsage(20, 4, 24)));
        await queued;

        Assert.Equal(0, applicationCalls);
        Assert.Null(conversation.TitleGenerationUsage);
    }

    [Fact]
    public void SessionUsageIncludesTitleGenerationWithoutReplacingLastAnswer()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "Answer");
        assistant.SetReportedUsage(new CopilotTokenUsage(100, 25, 125, 40));
        conversation.Messages.Add(assistant);
        conversation.RecordTitleGenerationUsage(
            new CopilotTokenUsage(12, 3, 15, 8),
            DateTimeOffset.UtcNow);

        var snapshot = CopilotConversationUsageDiagnostics.Capture(conversation);
        var report = CopilotConversationUsageDiagnostics.Format(conversation);

        Assert.Equal(new CopilotTokenUsage(112, 28, 140, 48), snapshot.TotalUsage);
        Assert.Equal(new CopilotTokenUsage(100, 25, 125, 40), snapshot.LastUsage);
        Assert.Equal(new CopilotTokenUsage(12, 3, 15, 8), snapshot.TitleGenerationUsage);
        Assert.Equal(1, snapshot.TitleGenerationRequests);
        Assert.Contains("标题模型调用：1 次", report, StringComparison.Ordinal);
    }

    [Fact]
    public void BranchCopiesTitleUsageWithoutRequiringAnActiveCompaction()
    {
        var conversation = CreateTitleCandidateConversation();
        conversation.RecordTitleGenerationUsage(
            new CopilotTokenUsage(24, 4, 28),
            DateTimeOffset.UtcNow);

        var branch = CopilotConversationBranchService.CreateBranch(
            conversation,
            conversation.Messages[1]);

        Assert.Null(branch.Compaction);
        Assert.NotSame(conversation.TitleGenerationUsage, branch.TitleGenerationUsage);
        Assert.Equal(1, branch.TitleGenerationUsage?.RequestCount);
        Assert.Equal(new CopilotTokenUsage(24, 4, 28), branch.TitleGenerationUsage?.Usage);
    }

    private static CopilotConversationRecord CreateTitleCandidateConversation()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Question"));
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Answer"));
        return conversation;
    }

    private static CopilotProfileConfig CreateProfile()
    {
        return new CopilotProfileConfig
        {
            VendorType = CopilotVendorType.Custom,
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "test-key",
            BaseUrl = "https://example.test/v1",
            Model = "test-model",
            MaxTokens = 4_096,
        };
    }

    private static CopilotCompletedReplyResult CreateReply(
        string content,
        CopilotTokenUsage usage,
        CopilotChatFinishKind finishKind = CopilotChatFinishKind.Complete)
    {
        return new CopilotCompletedReplyResult(
            new CopilotChatReply(new CopilotStreamDelta(string.Empty, content), usage),
            new CopilotChatStreamResult(
                usage,
                finishKind,
                finishKind == CopilotChatFinishKind.Complete ? "stop" : "length"),
            IsContentTruncated: false);
    }

    private sealed class TitleFailureHandler(string errorCode, bool hasReportedUsage) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var body = JsonSerializer.Serialize(new
            {
                id = "resp_title_failed", @object = "response", status = "failed", output = Array.Empty<object>(),
                error = new { code = errorCode, type = errorCode, message = "Controlled title generation failure." },
                usage = hasReportedUsage
                    ? new { input_tokens = 12, output_tokens = 8, total_tokens = 20, input_tokens_details = new { cached_tokens = 3 } }
                    : null,
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
