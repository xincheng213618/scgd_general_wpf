using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotConversationTitleCoordinatorTests
{
    [Fact]
    public async Task ReplacementCancelsThePreviousGenerationAndAppliesOnlyTheLatestTitle()
    {
        var firstGenerationEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondGenerationEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstProviderReply = new TaskCompletionSource<CopilotCompletedReplyResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondProviderReply = new TaskCompletionSource<CopilotCompletedReplyResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var generationCount = 0;
        var generator = new CopilotConversationTitleGenerator(async (_, _, _) =>
        {
            var generation = Interlocked.Increment(ref generationCount);
            if (generation == 1)
            {
                firstGenerationEntered.TrySetResult();
                return await firstProviderReply.Task;
            }

            secondGenerationEntered.TrySetResult();
            return await secondProviderReply.Task;
        });
        var conversation = CreateConversation();
        using var coordinator = new CopilotConversationTitleCoordinator(generator, ApplyGeneratedTitleAsync);

        var first = coordinator.QueueAsync(conversation, new CopilotProfileConfig());
        await firstGenerationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = coordinator.QueueAsync(conversation, new CopilotProfileConfig());
        await secondGenerationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        firstProviderReply.TrySetResult(CreateCompletion("Stale title"));
        await first.WaitAsync(TimeSpan.FromSeconds(5));
        secondProviderReply.TrySetResult(CreateCompletion("Generated 2"));

        await second.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, generationCount);
        Assert.Equal("Generated 2", conversation.Title);
        Assert.True(conversation.HasCustomTitle);
    }

    [Fact]
    public async Task IneligibleRescheduleDoesNotCancelTheExistingGeneration()
    {
        var generationEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var providerReply = new TaskCompletionSource<CopilotCompletedReplyResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var generationCount = 0;
        var generator = new CopilotConversationTitleGenerator(async (_, _, _) =>
        {
            Interlocked.Increment(ref generationCount);
            generationEntered.TrySetResult();
            return await providerReply.Task;
        });
        var conversation = CreateConversation();
        using var coordinator = new CopilotConversationTitleCoordinator(generator, ApplyGeneratedTitleAsync);

        var generation = coordinator.QueueAsync(conversation, new CopilotProfileConfig());
        await generationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Continue"));
        await coordinator.QueueAsync(conversation, new CopilotProfileConfig());
        providerReply.TrySetResult(CreateCompletion("Original generation"));

        await generation.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, generationCount);
        Assert.Equal("Original generation", conversation.Title);
    }

    [Fact]
    public async Task CancelPreventsALateProviderReplyFromReachingTheApplication()
    {
        var generationEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var providerReply = new TaskCompletionSource<CopilotCompletedReplyResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var applicationCount = 0;
        var generator = new CopilotConversationTitleGenerator(async (_, _, _) =>
        {
            generationEntered.TrySetResult();
            return await providerReply.Task;
        });
        var conversation = CreateConversation();
        using var coordinator = new CopilotConversationTitleCoordinator(
            generator,
            (_, _, _, _) =>
            {
                Interlocked.Increment(ref applicationCount);
                return Task.CompletedTask;
            });

        var generation = coordinator.QueueAsync(conversation, new CopilotProfileConfig());
        await generationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        coordinator.Cancel(conversation.Id);
        providerReply.TrySetResult(CreateCompletion("Late title"));

        await generation.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(0, applicationCount);
        Assert.Equal(CopilotUiText.NewConversationTitle, conversation.Title);
    }

    [Fact]
    public async Task CurrentGenerationIsRecheckedAfterApplicationDispatchDelay()
    {
        var applicationEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseApplication = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var generator = CreateGenerator("Generated title");
        var conversation = CreateConversation();
        using var coordinator = new CopilotConversationTitleCoordinator(
            generator,
            async (target, title, isCurrentGeneration, cancellationToken) =>
            {
                applicationEntered.TrySetResult();
                await releaseApplication.Task;
                if (!cancellationToken.IsCancellationRequested && isCurrentGeneration())
                    target.SetGeneratedTitle(title);
            });

        var generation = coordinator.QueueAsync(conversation, new CopilotProfileConfig());
        await applicationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        coordinator.Cancel(conversation.Id);
        conversation.SetCustomTitle("Manual title");
        releaseApplication.TrySetResult();

        await generation.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("Manual title", conversation.Title);
        Assert.True(conversation.HasCustomTitle);
    }

    [Fact]
    public async Task DisposeCancelsActiveWorkAndRejectsNewGenerations()
    {
        var generationEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var generationCount = 0;
        var generator = new CopilotConversationTitleGenerator(async (_, _, cancellationToken) =>
        {
            Interlocked.Increment(ref generationCount);
            generationEntered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return CreateCompletion("Unused");
        });
        var conversation = CreateConversation();
        var coordinator = new CopilotConversationTitleCoordinator(generator, ApplyGeneratedTitleAsync);

        var activeGeneration = coordinator.QueueAsync(conversation, new CopilotProfileConfig());
        await generationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        coordinator.Dispose();
        await activeGeneration.WaitAsync(TimeSpan.FromSeconds(5));
        await coordinator.QueueAsync(conversation, new CopilotProfileConfig());

        Assert.Equal(1, generationCount);
        Assert.Equal(CopilotUiText.NewConversationTitle, conversation.Title);
    }

    [Fact]
    public async Task ProviderFailureReleasesTheLeaseAndAllowsARetry()
    {
        var generationCount = 0;
        var generator = new CopilotConversationTitleGenerator((_, _, _) =>
        {
            var generation = Interlocked.Increment(ref generationCount);
            return generation == 1
                ? Task.FromException<CopilotCompletedReplyResult>(new InvalidOperationException("Provider unavailable"))
                : Task.FromResult(CreateCompletion("Recovered title"));
        });
        var conversation = CreateConversation();
        using var coordinator = new CopilotConversationTitleCoordinator(generator, ApplyGeneratedTitleAsync);

        await coordinator.QueueAsync(conversation, new CopilotProfileConfig());
        await coordinator.QueueAsync(conversation, new CopilotProfileConfig());

        Assert.Equal(2, generationCount);
        Assert.Equal("Recovered title", conversation.Title);
    }

    private static Task ApplyGeneratedTitleAsync(
        CopilotConversationRecord conversation,
        string generatedTitle,
        Func<bool> isCurrentGeneration,
        CancellationToken cancellationToken)
    {
        if (!cancellationToken.IsCancellationRequested && isCurrentGeneration())
            conversation.SetGeneratedTitle(generatedTitle);
        return Task.CompletedTask;
    }

    private static CopilotConversationTitleGenerator CreateGenerator(string title) =>
        new((_, _, _) => Task.FromResult(CreateCompletion(title)));

    private static CopilotConversationRecord CreateConversation()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Calibrate the camera"));
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Open calibration settings."));
        return conversation;
    }

    private static CopilotCompletedReplyResult CreateCompletion(string content) =>
        new(
            new CopilotChatReply(
                new CopilotStreamDelta(string.Empty, content),
                CopilotTokenUsage.Empty),
            new CopilotChatStreamResult(
                CopilotTokenUsage.Empty,
                CopilotChatFinishKind.Complete,
                CopilotChatFinishKind.Complete.ToString()),
            IsContentTruncated: false);
}
