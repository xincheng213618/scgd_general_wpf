using ColorVision.Copilot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            Success("diff --git a/a.cs b/a.cs"),
            Success("a.cs\0"));
        var result = await ExecuteAsync(runner, new Dictionary<string, object?>
        {
            ["target"] = "base_branch",
            ["revision"] = "origin/develop",
        });

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(4, runner.Commands.Count);
        Assert.Contains("--end-of-options", runner.Commands[0].Arguments);
        Assert.Contains("origin/develop^{commit}", runner.Commands[0].Arguments);
        Assert.Contains(mergeBase, runner.Commands[2].Arguments);
        Assert.Contains("HEAD", runner.Commands[2].Arguments);
        Assert.DoesNotContain(runner.Commands[2].Arguments, argument => argument.Contains("origin/develop", StringComparison.Ordinal));
        Assert.Contains("--name-only", runner.Commands[3].Arguments);
        Assert.Contains("\"target\":\"base_branch\"", result.Content, StringComparison.Ordinal);
        Assert.Contains("\"resolved_revision\":\"" + resolvedBase + "\"", result.Content, StringComparison.Ordinal);
        Assert.Contains("\"changed_paths\":[\"a.cs\"]", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CommitUsesResolvedFullObjectIdForShow()
    {
        var resolvedCommit = new string('d', 40);
        var runner = new RecordingRunner(
            Success(resolvedCommit + Environment.NewLine),
            Success("diff --git a/b.cs b/b.cs"),
            Success("b.cs\0"));
        var result = await ExecuteAsync(runner, new Dictionary<string, object?>
        {
            ["target"] = "commit",
            ["revision"] = "abcdef1",
        });

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(3, runner.Commands.Count);
        Assert.Contains("show", runner.Commands[1].Arguments);
        Assert.Contains(resolvedCommit, runner.Commands[1].Arguments);
        Assert.DoesNotContain("abcdef1", runner.Commands[1].Arguments);
        Assert.Contains("--name-only", runner.Commands[2].Arguments);
        Assert.Contains("\"target\":\"commit\"", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WorkingTreeBothIncludesBoundedUntrackedTextAndCanonicalPaths()
    {
        var untrackedPath = Path.Combine(_root, "nested", "new file.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(untrackedPath)!);
        File.WriteAllText(untrackedPath, "first line\nsecond line");
        var runner = new RecordingRunner(
            Success("diff --git a/tracked.cs b/tracked.cs"),
            Success("tracked.cs\0"),
            Success(string.Empty),
            Success(string.Empty),
            Success("nested/new file.cs\0"));

        var result = await ExecuteAsync(runner, new Dictionary<string, object?>
        {
            ["scope"] = "both",
        });

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(5, runner.Commands.Count);
        Assert.True(CopilotGitDiffResultProtocol.TryParse(result.Content, out var snapshot, out var error), error);
        Assert.Equal(["nested/new file.cs", "tracked.cs"], snapshot.ChangedPaths);
        Assert.True(snapshot.ChangedPathsComplete);
        var untracked = Assert.Single(snapshot.Sections, section => section.Scope == "untracked");
        Assert.True(untracked.OutputComplete);
        Assert.Contains("+++ \"b/nested/new file.cs\"", untracked.Patch, StringComparison.Ordinal);
        Assert.Contains("+first line", untracked.Patch, StringComparison.Ordinal);
        Assert.Contains("+second line", untracked.Patch, StringComparison.Ordinal);
        Assert.Contains("\\ No newline at end of file", untracked.Patch, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidChangedPathFailsClosedWithoutEscapingTheRepository()
    {
        var runner = new RecordingRunner(
            Success("diff --git a/safe.cs b/safe.cs"),
            Success("../outside.cs\0"));

        var result = await ExecuteAsync(runner, new Dictionary<string, object?>
        {
            ["scope"] = "staged",
        });

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(CopilotGitDiffResultProtocol.TryParse(result.Content, out var snapshot, out var error), error);
        Assert.Empty(snapshot.ChangedPaths);
        Assert.False(snapshot.ChangedPathsComplete);
    }

    [Fact]
    public async Task LargeUntrackedFileIsExplicitlyBounded()
    {
        var untrackedPath = Path.Combine(_root, "large.txt");
        File.WriteAllText(
            untrackedPath,
            string.Join('\n', Enumerable.Repeat(new string('x', 200), 200)));
        var runner = new RecordingRunner(
            Success(string.Empty),
            Success(string.Empty),
            Success("large.txt\0"));

        var result = await ExecuteAsync(runner, new Dictionary<string, object?>
        {
            ["scope"] = "unstaged",
        });

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(CopilotGitDiffResultProtocol.TryParse(result.Content, out var snapshot, out var error), error);
        var untracked = Assert.Single(snapshot.Sections, section => section.Scope == "untracked");
        Assert.False(untracked.OutputComplete);
        Assert.True(untracked.PatchTruncated);
        Assert.True(snapshot.PatchTruncated);
        Assert.True(untracked.Patch.Length <= CopilotGitDiffInspectionService.MaxPatchCharactersPerSection);
        Assert.Contains("truncated", untracked.Patch, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["large.txt"], snapshot.ChangedPaths);
        Assert.True(snapshot.ChangedPathsComplete);
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
