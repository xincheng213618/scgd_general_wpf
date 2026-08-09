using ColorVision.Copilot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotGitDiffInspectionTests : IDisposable
{
    private readonly string _root;
    private readonly string _gitExecutable;

    public CopilotGitDiffInspectionTests()
    {
        _root = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "ColorVisionCopilotGitDiffTests",
            Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(Path.Combine(_root, ".git"));
        _gitExecutable = Path.Combine(_root, "git.exe");
        File.WriteAllText(_gitExecutable, string.Empty);
    }

    [Fact]
    public async Task BaseBranchUsesResolvedObjectIdsForFinalDiff()
    {
        var resolvedBase = new string('a', 40);
        var mergeBase = new string('b', 40);
        var runner = new RecordingRunner(
            Success(resolvedBase + Environment.NewLine),
            Success(mergeBase + Environment.NewLine),
            Success("diff --git a/a.cs b/a.cs"));
        var result = await ExecuteAsync(runner, new Dictionary<string, object?>
        {
            ["target"] = "base_branch",
            ["revision"] = "origin/develop",
        });

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(3, runner.Commands.Count);
        Assert.Contains("--end-of-options", runner.Commands[0].Arguments);
        Assert.Contains("origin/develop^{commit}", runner.Commands[0].Arguments);
        Assert.Contains(mergeBase, runner.Commands[2].Arguments);
        Assert.Contains("HEAD", runner.Commands[2].Arguments);
        Assert.DoesNotContain(runner.Commands[2].Arguments, argument => argument.Contains("origin/develop", StringComparison.Ordinal));
        Assert.Contains("\"target\":\"base_branch\"", result.Content, StringComparison.Ordinal);
        Assert.Contains("\"resolved_revision\":\"" + resolvedBase + "\"", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CommitUsesResolvedFullObjectIdForShow()
    {
        var resolvedCommit = new string('d', 40);
        var runner = new RecordingRunner(
            Success(resolvedCommit + Environment.NewLine),
            Success("diff --git a/b.cs b/b.cs"));
        var result = await ExecuteAsync(runner, new Dictionary<string, object?>
        {
            ["target"] = "commit",
            ["revision"] = "abcdef1",
        });

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(2, runner.Commands.Count);
        Assert.Contains("show", runner.Commands[1].Arguments);
        Assert.Contains(resolvedCommit, runner.Commands[1].Arguments);
        Assert.DoesNotContain("abcdef1", runner.Commands[1].Arguments);
        Assert.Contains("\"target\":\"commit\"", result.Content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("base_branch", "-dangerous")]
    [InlineData("base_branch", "main..feature")]
    [InlineData("base_branch", "topic.lock/child")]
    [InlineData("commit", "not-a-sha")]
    public async Task InvalidRevisionIsRejectedBeforeGitStarts(string target, string revision)
    {
        var runner = new RecordingRunner();
        var result = await ExecuteAsync(runner, new Dictionary<string, object?>
        {
            ["target"] = target,
            ["revision"] = revision,
        });

        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, result.FailureKind);
        Assert.Empty(runner.Commands);
    }

    [Fact]
    public async Task RevisionTargetRejectsWorkingTreeScope()
    {
        var runner = new RecordingRunner();
        var result = await ExecuteAsync(runner, new Dictionary<string, object?>
        {
            ["target"] = "commit",
            ["revision"] = "abcdef1",
            ["Scope"] = "both",
        });

        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, result.FailureKind);
        Assert.Empty(runner.Commands);
    }

    public void Dispose()
    {
        var tempRoot = Path.GetFullPath(Path.GetTempPath());
        if (_root.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase)
            && Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<CopilotToolResult> ExecuteAsync(
        RecordingRunner runner,
        IReadOnlyDictionary<string, object?> arguments)
    {
        var service = new CopilotGitDiffInspectionService(runner, () => _gitExecutable);
        return await service.ExecuteAsync(
            new CopilotAgentRequest
            {
                WorkspacePath = _root,
                SearchRootPaths = [_root],
                WritableLocalRootPaths = [_root],
                Mode = CopilotAgentMode.Review,
            },
            new CopilotAgentToolInput { Arguments = arguments },
            CancellationToken.None);
    }

    private static CopilotShellProcessResult Success(string output) =>
        new(0, false, output, string.Empty, TimeSpan.FromMilliseconds(1));

    private sealed class RecordingRunner(params CopilotShellProcessResult[] results) : ICopilotShellProcessRunner
    {
        private readonly Queue<CopilotShellProcessResult> _results = new(results);

        public List<CopilotShellProcessCommand> Commands { get; } = [];

        public Task<CopilotShellProcessResult> RunAsync(
            CopilotShellProcessCommand command,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commands.Add(command);
            return Task.FromResult(_results.Dequeue());
        }
    }
}
