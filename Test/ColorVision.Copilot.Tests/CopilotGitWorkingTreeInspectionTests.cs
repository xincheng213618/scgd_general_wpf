using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotGitWorkingTreeInspectionTests : IDisposable
{
    private readonly string _root;
    private readonly string _gitExecutable;

    public CopilotGitWorkingTreeInspectionTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ColorVisionCopilotGitStatus-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, ".git"));
        _gitExecutable = Path.Combine(_root, "git.exe");
        File.WriteAllText(_gitExecutable, string.Empty);
    }

    [Fact]
    public async Task ConcurrentServicesShareOnlyTheActiveStatusRun()
    {
        var runner = new BlockingRunner();
        var firstService = new CopilotGitWorkingTreeInspectionService(runner, () => _gitExecutable);
        var secondService = new CopilotGitWorkingTreeInspectionService(runner, () => _gitExecutable);

        var first = firstService.ExecuteAsync(CreateRequest("conversation-1"), CopilotAgentToolInput.Empty, CancellationToken.None);
        var second = secondService.ExecuteAsync(CreateRequest("conversation-2"), CopilotAgentToolInput.Empty, CancellationToken.None);

        Assert.Equal(1, runner.CallCount);
        runner.CompleteNext(Success("# branch.oid abcdef\n# branch.head develop\n"));
        var sharedResults = await Task.WhenAll(first, second);
        Assert.All(sharedResults, result => Assert.True(result.Success));

        var later = firstService.ExecuteAsync(CreateRequest("conversation-3"), CopilotAgentToolInput.Empty, CancellationToken.None);

        Assert.Equal(2, runner.CallCount);
        runner.CompleteNext(Success("# branch.oid abcdef\n# branch.head develop\n? later.txt\n"));
        var laterResult = await later;
        Assert.True(laterResult.Success);
        Assert.Contains("1 changed path(s)", laterResult.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancellingOneWaiterPreservesTheSharedStatusRun()
    {
        var runner = new BlockingRunner();
        var firstService = new CopilotGitWorkingTreeInspectionService(runner, () => _gitExecutable);
        var secondService = new CopilotGitWorkingTreeInspectionService(runner, () => _gitExecutable);
        using var cancellation = new CancellationTokenSource();

        var cancelledWaiter = firstService.ExecuteAsync(CreateRequest("conversation-1"), CopilotAgentToolInput.Empty, cancellation.Token);
        var survivingWaiter = secondService.ExecuteAsync(CreateRequest("conversation-2"), CopilotAgentToolInput.Empty, CancellationToken.None);

        Assert.Equal(1, runner.CallCount);
        Assert.False(Assert.Single(runner.RunCancellationTokens).CanBeCanceled);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledWaiter);
        Assert.False(survivingWaiter.IsCompleted);

        runner.CompleteNext(Success("# branch.oid abcdef\n# branch.head develop\n"));
        Assert.True((await survivingWaiter).Success);
    }

    [Fact]
    public async Task DifferentGitEnvironmentsDoNotShareStatusRun()
    {
        var runner = new BlockingRunner();
        var firstService = new CopilotGitWorkingTreeInspectionService(runner, () => _gitExecutable);
        var secondService = new CopilotGitWorkingTreeInspectionService(runner, () => _gitExecutable);

        var first = firstService.ExecuteAsync(CreateRequest("conversation-1", "first.gitconfig"), CopilotAgentToolInput.Empty, CancellationToken.None);
        var second = secondService.ExecuteAsync(CreateRequest("conversation-2", "second.gitconfig"), CopilotAgentToolInput.Empty, CancellationToken.None);

        Assert.Equal(2, runner.CallCount);
        runner.CompleteNext(Success("# branch.oid abcdef\n# branch.head develop\n"));
        runner.CompleteNext(Success("# branch.oid abcdef\n# branch.head develop\n"));
        Assert.All(await Task.WhenAll(first, second), result => Assert.True(result.Success));
    }

    public void Dispose()
    {
        var tempRoot = Path.GetFullPath(Path.GetTempPath());
        if (_root.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private CopilotAgentRequest CreateRequest(string conversationId, string? gitConfigPath = null) => new()
    {
        ConversationId = conversationId,
        WorkspacePath = _root,
        SearchRootPaths = [_root],
        WritableLocalRootPaths = [_root],
        Mode = CopilotAgentMode.Review,
        CodexShellEnvironmentPolicy = gitConfigPath == null
            ? CopilotCodexShellEnvironmentPolicy.Default
            : new CopilotCodexShellEnvironmentPolicy
            {
                Inherit = CopilotCodexShellEnvironmentInherit.None,
                Set = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["GIT_CONFIG_GLOBAL"] = gitConfigPath,
                },
            },
    };

    private static CopilotShellProcessResult Success(string output) =>
        new(0, false, output, string.Empty, TimeSpan.FromMilliseconds(1));

    private sealed class BlockingRunner : ICopilotShellProcessRunner
    {
        private readonly ConcurrentQueue<TaskCompletionSource<CopilotShellProcessResult>> _pending = new();
        private readonly ConcurrentQueue<CancellationToken> _runCancellationTokens = new();
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public IReadOnlyCollection<CancellationToken> RunCancellationTokens => _runCancellationTokens.ToArray();

        public Task<CopilotShellProcessResult> RunAsync(
            CopilotShellProcessCommand command,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            _runCancellationTokens.Enqueue(cancellationToken);
            var completion = new TaskCompletionSource<CopilotShellProcessResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending.Enqueue(completion);
            return completion.Task;
        }

        public void CompleteNext(CopilotShellProcessResult result)
        {
            Assert.True(_pending.TryDequeue(out var completion));
            completion.SetResult(result);
        }
    }
}
