#pragma warning disable CA1707
using ColorVision.Copilot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

public sealed class CopilotGitWorkingTreeInspectionTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "ColorVision-Git-Inspection-" + Guid.NewGuid().ToString("N"));
    private readonly string _gitExecutable;

    public CopilotGitWorkingTreeInspectionTests()
    {
        Directory.CreateDirectory(_tempRoot);
        _gitExecutable = Path.Combine(_tempRoot, "git.exe");
        File.WriteAllBytes(_gitExecutable, []);
    }

    [Fact]
    public async Task FixedDiagnosticReturnsStructuredDirtyStatusWithoutCommandInput()
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, ".git"));
        var hash = new string('a', 40);
        var output = string.Join('\n',
        [
            $"# branch.oid {hash}",
            "# branch.head develop",
            "# branch.upstream origin/develop",
            "# branch.ab +2 -1",
            $"1 M. N... {hash} {hash} {hash} {hash} {hash} Directory.Build.props",
            $"1 .M N... {hash} {hash} {hash} {hash} {hash} ColorVision/Copilot/file.cs",
            "? new-file.txt",
            $"u UU N... {hash} {hash} {hash} {hash} {hash} {hash} {hash} conflicted.cs",
        ]);
        var runner = new RecordingRunner(new CopilotShellProcessResult(0, false, output, string.Empty, TimeSpan.FromMilliseconds(20)));

        var result = await CreateService(runner).ExecuteAsync(Request(_tempRoot), CopilotAgentToolInput.Empty, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains("4 changed path(s)", result.Summary, StringComparison.Ordinal);
        Assert.Contains("1 staged, 1 unstaged, 1 untracked, 1 conflicted", result.Summary, StringComparison.Ordinal);
        Assert.Contains("\"branch\":\"develop\"", result.Content, StringComparison.Ordinal);
        Assert.Contains("\"ahead\":2", result.Content, StringComparison.Ordinal);
        Assert.Contains("\"behind\":1", result.Content, StringComparison.Ordinal);
        Assert.Contains("\"is_clean\":false", result.Content, StringComparison.Ordinal);
        Assert.Contains("\"path\":\"conflicted.cs\"", result.Content, StringComparison.Ordinal);

        var command = Assert.Single(runner.Commands);
        Assert.Equal(_gitExecutable, command.ExecutablePath);
        Assert.Equal(_tempRoot, command.WorkingDirectory);
        Assert.Contains("--no-optional-locks", command.Arguments);
        Assert.Contains("core.fsmonitor=false", command.Arguments);
        Assert.Contains("core.untrackedCache=false", command.Arguments);
        Assert.Contains("core.worktree=" + _tempRoot, command.Arguments);
        Assert.Contains("status.relativePaths=false", command.Arguments);
        Assert.Contains("--porcelain=v2", command.Arguments);
        Assert.Contains("--no-renames", command.Arguments);
        Assert.Contains("--ignore-submodules=all", command.Arguments);
        Assert.DoesNotContain(command.Arguments, argument => argument.Contains("检查", StringComparison.Ordinal));
        Assert.NotNull(command.EnvironmentOverrides);
        Assert.Null(command.EnvironmentOverrides!["GIT_DIR"]);
        Assert.Null(command.EnvironmentOverrides["GIT_WORK_TREE"]);
        Assert.Null(command.EnvironmentOverrides["GIT_INDEX_FILE"]);
        Assert.Null(command.EnvironmentOverrides["GIT_CONFIG_PARAMETERS"]);
        Assert.Equal("0", command.EnvironmentOverrides["GIT_OPTIONAL_LOCKS"]);
    }

    [Fact]
    public async Task CleanStatusKeepsBranchAndReportsCompleteCleanObservation()
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, ".git"));
        var hash = new string('b', 40);
        var runner = new RecordingRunner(new CopilotShellProcessResult(
            0,
            false,
            $"# branch.oid {hash}\n# branch.head main\n# branch.ab +0 -0\n",
            string.Empty,
            TimeSpan.Zero));

        var result = await CreateService(runner).ExecuteAsync(Request(_tempRoot), CopilotAgentToolInput.Empty, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("Git working tree is clean on branch main.", result.Summary);
        Assert.Contains("\"is_clean\":true", result.Content, StringComparison.Ordinal);
        Assert.Contains("\"status_complete\":true", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TruncatedProcessOutputNeverClaimsWorkingTreeIsClean()
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, ".git"));
        var runner = new RecordingRunner(new CopilotShellProcessResult(
            0,
            false,
            "# branch.head develop\n...<shell output truncated>...\n",
            string.Empty,
            TimeSpan.Zero));

        var result = await CreateService(runner).ExecuteAsync(Request(_tempRoot), CopilotAgentToolInput.Empty, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.DoesNotContain("is clean", result.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"is_clean\":false", result.Content, StringComparison.Ordinal);
        Assert.Contains("\"status_complete\":false", result.Content, StringComparison.Ordinal);
        Assert.Contains("\"entries_truncated\":true", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EntryListIsBoundedWhileCompleteCountsCoverAllObservedPaths()
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, ".git"));
        var output = "# branch.head develop\n" + string.Join('\n', Enumerable.Range(1, 105).Select(index => $"? file-{index}.txt"));
        var runner = new RecordingRunner(new CopilotShellProcessResult(0, false, output, string.Empty, TimeSpan.Zero));

        var result = await CreateService(runner).ExecuteAsync(Request(_tempRoot), CopilotAgentToolInput.Empty, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        var json = result.Content[(result.Content.IndexOf("result_json: ", StringComparison.Ordinal) + "result_json: ".Length)..];
        using var document = JsonDocument.Parse(json);
        Assert.Equal(105, document.RootElement.GetProperty("changed_path_count").GetInt32());
        Assert.Equal(105, document.RootElement.GetProperty("untracked_count").GetInt32());
        Assert.True(document.RootElement.GetProperty("status_complete").GetBoolean());
        Assert.True(document.RootElement.GetProperty("entries_truncated").GetBoolean());
        Assert.Equal(CopilotGitWorkingTreeInspectionService.MaxEntries, document.RootElement.GetProperty("entries").GetArrayLength());
    }

    [Fact]
    public async Task SelectedChildPathUsesRepositoryRootButCannotEscapeAllowedRoot()
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, ".git"));
        var child = Directory.CreateDirectory(Path.Combine(_tempRoot, "src", "feature")).FullName;
        var runner = new RecordingRunner(new CopilotShellProcessResult(0, false, "# branch.head develop\n", string.Empty, TimeSpan.Zero));

        var result = await CreateService(runner).ExecuteAsync(
            Request(_tempRoot),
            new CopilotAgentToolInput { Arguments = new Dictionary<string, object?> { ["path"] = child }, Path = child },
            CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(_tempRoot, Assert.Single(runner.Commands).WorkingDirectory);

        var outside = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ColorVision-Git-Outside-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var denied = await CreateService(runner).ExecuteAsync(
                Request(_tempRoot),
                new CopilotAgentToolInput { Arguments = new Dictionary<string, object?> { ["path"] = outside }, Path = outside },
                CancellationToken.None);

            Assert.False(denied.Success);
            Assert.Equal(CopilotToolFailureKind.Validation, denied.FailureKind);
            Assert.Single(runner.Commands);
        }
        finally
        {
            Directory.Delete(outside, true);
        }
    }

    [Fact]
    public async Task AllowedSubdirectoryCannotExposeRepositoryAboveItsRoot()
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, ".git"));
        var allowedChild = Directory.CreateDirectory(Path.Combine(_tempRoot, "allowed-child")).FullName;
        var runner = new RecordingRunner(new CopilotShellProcessResult(0, false, "# branch.head develop\n", string.Empty, TimeSpan.Zero));

        var result = await CreateService(runner).ExecuteAsync(Request(allowedChild), CopilotAgentToolInput.Empty, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.NotFound, result.FailureKind);
        Assert.Empty(runner.Commands);
    }

    [Fact]
    public async Task TimeoutIsRetryEligibleAndMissingRepositoryDoesNotStartGit()
    {
        var runner = new RecordingRunner(new CopilotShellProcessResult(0, true, string.Empty, string.Empty, TimeSpan.FromSeconds(15)));
        var missingRepository = await CreateService(runner).ExecuteAsync(Request(_tempRoot), CopilotAgentToolInput.Empty, CancellationToken.None);

        Assert.False(missingRepository.Success);
        Assert.Equal(CopilotToolFailureKind.NotFound, missingRepository.FailureKind);
        Assert.Empty(runner.Commands);

        Directory.CreateDirectory(Path.Combine(_tempRoot, ".git"));
        var timedOut = await CreateService(runner).ExecuteAsync(Request(_tempRoot), CopilotAgentToolInput.Empty, CancellationToken.None);

        Assert.False(timedOut.Success);
        Assert.Equal(CopilotToolFailureKind.Transient, timedOut.FailureKind);
        Assert.Single(runner.Commands);
    }

    [Fact]
    public async Task RelativeExecutablePathIsNeverResolvedAgainstTheWorkspace()
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, ".git"));
        File.WriteAllBytes(Path.Combine(_tempRoot, "untrusted-git.exe"), []);
        var runner = new RecordingRunner(new CopilotShellProcessResult(0, false, "# branch.head develop\n", string.Empty, TimeSpan.Zero));
        var service = new CopilotGitWorkingTreeInspectionService(runner, () => "untrusted-git.exe");

        var result = await service.ExecuteAsync(Request(_tempRoot), CopilotAgentToolInput.Empty, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.NotFound, result.FailureKind);
        Assert.Empty(runner.Commands);
    }

    [Fact]
    public async Task RegistryPublishesProtectedStructuredGitInspectionSchema()
    {
        var registry = CopilotToolRegistry.CreateDefault();
        var tools = registry.FindTools(Request(_tempRoot, "解释一下版本控制"));
        var tool = Assert.Single(tools, candidate => candidate.Name == "InspectGitWorkingTree");

        Assert.Equal(CopilotToolAccess.ReadOnly, tool.Capability.Access);
        Assert.Equal(CopilotToolRiskLevel.Medium, tool.Capability.RiskLevel);
        Assert.Equal(CopilotToolApprovalMode.Always, tool.Capability.ApprovalMode);
        Assert.Equal(CopilotToolIdempotency.Unknown, tool.Capability.Idempotency);
        Assert.Equal(CopilotToolConcurrencyMode.Exclusive, tool.Capability.EffectiveConcurrencyMode);
        Assert.True(tool.InputSchema.TryBind(new Dictionary<string, object?>(), out _, out var emptyError), emptyError);
        Assert.True(tool.InputSchema.TryBind(new Dictionary<string, object?> { ["path"] = _tempRoot }, out _, out var pathError), pathError);
        Assert.False(tool.InputSchema.TryBind(new Dictionary<string, object?> { ["command"] = "git status" }, out _, out var commandError));
        Assert.Contains("Unknown argument 'command'", commandError, StringComparison.Ordinal);
        var protectedTool = Assert.IsType<CopilotInspectGitWorkingTreeTool>(tool);
        var direct = await protectedTool.ExecuteAsync(Request(_tempRoot), CopilotAgentToolInput.Empty, CancellationToken.None);
        Assert.False(direct.Success);
        Assert.Equal(CopilotToolFailureKind.Authorization, direct.FailureKind);
        var approval = protectedTool.CreateApprovalPresentation(CopilotAgentToolInput.Empty);
        Assert.Contains("Git working tree", approval.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("external filters", approval.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            registry.FindTools(Request(_tempRoot, "检查 Git", CopilotAgentMode.Chat)),
            candidate => candidate.Name == "InspectGitWorkingTree");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, true);
    }

    private CopilotGitWorkingTreeInspectionService CreateService(ICopilotShellProcessRunner runner)
    {
        return new CopilotGitWorkingTreeInspectionService(runner, () => _gitExecutable);
    }

    private static CopilotAgentRequest Request(
        string root,
        string userText = "检查当前 Git 工作树",
        CopilotAgentMode mode = CopilotAgentMode.Auto)
    {
        return new CopilotAgentRequest
        {
            UserText = userText,
            Mode = mode,
            SearchRootPaths = [root],
            History = [new CopilotRequestMessage("assistant", "previous answer")],
        };
    }

    private sealed class RecordingRunner(CopilotShellProcessResult result) : ICopilotShellProcessRunner
    {
        public List<CopilotShellProcessCommand> Commands { get; } = [];

        public Task<CopilotShellProcessResult> RunAsync(CopilotShellProcessCommand command, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commands.Add(command);
            return Task.FromResult(result);
        }
    }
}
