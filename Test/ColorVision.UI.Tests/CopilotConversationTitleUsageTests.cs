using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

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
}
