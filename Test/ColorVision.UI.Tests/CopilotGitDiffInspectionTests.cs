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

public sealed class CopilotGitDiffInspectionTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "ColorVision-Git-Diff-" + Guid.NewGuid().ToString("N"));
    private readonly string _gitExecutable;

    public CopilotGitDiffInspectionTests()
    {
        Directory.CreateDirectory(_tempRoot);
        _gitExecutable = Path.Combine(_tempRoot, "git.exe");
        File.WriteAllBytes(_gitExecutable, []);
    }

    [Fact]
    public async Task BothScopeReturnsStructuredBoundedSectionsUsingFixedArguments()
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, ".git"));
        var selectedFile = Path.Combine(_tempRoot, "src", "feature.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(selectedFile)!);
        File.WriteAllText(selectedFile, "changed");
        var runner = new QueueRunner(
            Result("diff --git a/src/feature.cs b/src/feature.cs\n+unstaged\n"),
            Result("diff --git a/src/feature.cs b/src/feature.cs\n+staged\n"));
        var input = new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?> { ["path"] = selectedFile, ["scope"] = "both" },
            Path = selectedFile,
        };

        var result = await CreateService(runner).ExecuteAsync(Request(), input, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("Git returned staged and unstaged changes.", result.Summary);
        var json = ReadResultJson(result.Content);
        Assert.Equal("both", json.RootElement.GetProperty("scope").GetString());
        Assert.Equal("src/feature.cs", json.RootElement.GetProperty("path_filter").GetString());
        Assert.True(json.RootElement.GetProperty("has_changes").GetBoolean());
        Assert.True(json.RootElement.GetProperty("output_complete").GetBoolean());
        Assert.False(json.RootElement.GetProperty("patch_truncated").GetBoolean());
        Assert.Equal(2, json.RootElement.GetProperty("sections").GetArrayLength());

        Assert.Equal(2, runner.Commands.Count);
        var unstaged = runner.Commands[0];
        var staged = runner.Commands[1];
        Assert.Equal(_gitExecutable, unstaged.ExecutablePath);
        Assert.Equal(_tempRoot, unstaged.WorkingDirectory);
        Assert.Contains("--no-ext-diff", unstaged.Arguments);
        Assert.Contains("--no-textconv", unstaged.Arguments);
        Assert.Contains("--no-renames", unstaged.Arguments);
        Assert.Contains("--ignore-submodules=all", unstaged.Arguments);
        Assert.Contains("--no-color", unstaged.Arguments);
        Assert.DoesNotContain("--cached", unstaged.Arguments);
        Assert.Contains("--cached", staged.Arguments);
        AssertPathFilterFollowsSeparator(unstaged, "src/feature.cs");
        AssertPathFilterFollowsSeparator(staged, "src/feature.cs");
        Assert.NotNull(unstaged.EnvironmentOverrides);
        Assert.Null(unstaged.EnvironmentOverrides!["GIT_DIR"]);
        Assert.Null(unstaged.EnvironmentOverrides["GIT_WORK_TREE"]);
        Assert.Null(unstaged.EnvironmentOverrides["GIT_CONFIG_PARAMETERS"]);
        Assert.Equal("0", unstaged.EnvironmentOverrides["GIT_OPTIONAL_LOCKS"]);
    }

    [Fact]
    public async Task EmptyCompleteDiffDoesNotInventChanges()
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, ".git"));
        var result = await CreateService(new QueueRunner(Result(string.Empty))).ExecuteAsync(
            Request(),
            CopilotAgentToolInput.Empty,
            CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("Git found no unstaged changes.", result.Summary);
        var json = ReadResultJson(result.Content);
        Assert.False(json.RootElement.GetProperty("has_changes").GetBoolean());
        Assert.True(json.RootElement.GetProperty("output_complete").GetBoolean());
    }

    [Fact]
    public async Task ServiceAndProcessTruncationAreBothReportedAsIncomplete()
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, ".git"));
        var oversized = "diff --git a/a b/a\n" + new string('+', CopilotGitDiffInspectionService.MaxPatchCharactersPerSection + 100);
        var serviceResult = await CreateService(new QueueRunner(Result(oversized))).ExecuteAsync(
            Request(),
            CopilotAgentToolInput.Empty,
            CancellationToken.None);

        Assert.True(serviceResult.Success, serviceResult.ErrorMessage);
        Assert.Contains("incomplete excerpt", serviceResult.Summary, StringComparison.Ordinal);
        var serviceJson = ReadResultJson(serviceResult.Content);
        Assert.False(serviceJson.RootElement.GetProperty("output_complete").GetBoolean());
        Assert.True(serviceJson.RootElement.GetProperty("patch_truncated").GetBoolean());
        Assert.Contains("<Git diff excerpt truncated>", serviceJson.RootElement.GetProperty("sections")[0].GetProperty("patch").GetString(), StringComparison.Ordinal);

        var runnerResult = await CreateService(new QueueRunner(Result("diff --git a/a b/a\n...<shell output truncated>...\n+tail"))).ExecuteAsync(
            Request(),
            CopilotAgentToolInput.Empty,
            CancellationToken.None);
        var runnerJson = ReadResultJson(runnerResult.Content);
        Assert.False(runnerJson.RootElement.GetProperty("output_complete").GetBoolean());
        Assert.True(runnerJson.RootElement.GetProperty("patch_truncated").GetBoolean());
    }

    [Fact]
    public async Task InvalidScopeAndOutsidePathNeverStartGit()
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, ".git"));
        var runner = new QueueRunner(Result(string.Empty));
        var invalidScope = await CreateService(runner).ExecuteAsync(
            Request(),
            new CopilotAgentToolInput { Arguments = new Dictionary<string, object?> { ["scope"] = "everything" } },
            CancellationToken.None);

        Assert.False(invalidScope.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, invalidScope.FailureKind);
        Assert.Empty(runner.Commands);

        var outside = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ColorVision-Git-Diff-Outside-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var outsideResult = await CreateService(runner).ExecuteAsync(
                Request(),
                new CopilotAgentToolInput { Arguments = new Dictionary<string, object?> { ["path"] = outside }, Path = outside },
                CancellationToken.None);
            Assert.False(outsideResult.Success);
            Assert.Equal(CopilotToolFailureKind.Validation, outsideResult.FailureKind);
            Assert.Empty(runner.Commands);
        }
        finally
        {
            Directory.Delete(outside, true);
        }
    }

    [Fact]
    public async Task TimeoutNonzeroAndUntrustedExecutableRemainHonestFailures()
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, ".git"));
        var timeout = await CreateService(new QueueRunner(new CopilotShellProcessResult(-1, true, string.Empty, string.Empty, TimeSpan.FromSeconds(20)))).ExecuteAsync(
            Request(), CopilotAgentToolInput.Empty, CancellationToken.None);
        Assert.False(timeout.Success);
        Assert.Equal(CopilotToolFailureKind.Transient, timeout.FailureKind);

        var failed = await CreateService(new QueueRunner(new CopilotShellProcessResult(128, false, string.Empty, "fatal: unsafe repository", TimeSpan.Zero))).ExecuteAsync(
            Request(), CopilotAgentToolInput.Empty, CancellationToken.None);
        Assert.False(failed.Success);
        Assert.Equal(CopilotToolFailureKind.Internal, failed.FailureKind);
        Assert.Contains("unsafe repository", failed.ErrorMessage, StringComparison.Ordinal);

        var runner = new QueueRunner(Result(string.Empty));
        var untrusted = new CopilotGitDiffInspectionService(runner, () => "git.exe");
        var untrustedResult = await untrusted.ExecuteAsync(Request(), CopilotAgentToolInput.Empty, CancellationToken.None);
        Assert.False(untrustedResult.Success);
        Assert.Equal(CopilotToolFailureKind.NotFound, untrustedResult.FailureKind);
        Assert.Empty(runner.Commands);
    }

    [Fact]
    public async Task RegistryPublishesStrictProtectedDiffSchemaAndApproval()
    {
        var registry = CopilotToolRegistry.CreateDefault();
        var tool = Assert.Single(registry.FindTools(Request()), candidate => candidate.Name == "InspectGitDiff");

        Assert.Equal(CopilotToolAccess.ReadOnly, tool.Capability.Access);
        Assert.Equal(CopilotToolRiskLevel.Medium, tool.Capability.RiskLevel);
        Assert.Equal(CopilotToolApprovalMode.Always, tool.Capability.ApprovalMode);
        Assert.Equal(CopilotToolConcurrencyMode.Exclusive, tool.Capability.EffectiveConcurrencyMode);
        Assert.True(tool.InputSchema.TryBind(new Dictionary<string, object?>(), out _, out var emptyError), emptyError);
        Assert.True(tool.InputSchema.TryBind(new Dictionary<string, object?> { ["path"] = _tempRoot, ["scope"] = "both" }, out var bound, out var validError), validError);
        Assert.Equal(_tempRoot, bound.Path);
        Assert.False(tool.InputSchema.TryBind(new Dictionary<string, object?> { ["scope"] = "all" }, out _, out var scopeError));
        Assert.Contains("must be one of", scopeError, StringComparison.Ordinal);
        Assert.False(tool.InputSchema.TryBind(new Dictionary<string, object?> { ["command"] = "git diff" }, out _, out var commandError));
        Assert.Contains("Unknown argument 'command'", commandError, StringComparison.Ordinal);

        var protectedTool = Assert.IsType<CopilotInspectGitDiffTool>(tool);
        var direct = await protectedTool.ExecuteAsync(Request(), CopilotAgentToolInput.Empty, CancellationToken.None);
        Assert.False(direct.Success);
        Assert.Equal(CopilotToolFailureKind.Authorization, direct.FailureKind);
        var approval = protectedTool.CreateApprovalPresentation(bound);
        Assert.Contains("Git diff", approval.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("both", approval.Description, StringComparison.Ordinal);
        Assert.Contains("No command text", approval.Description, StringComparison.Ordinal);
        Assert.DoesNotContain(
            registry.FindTools(Request(CopilotAgentMode.Chat)),
            candidate => candidate.Name == "InspectGitDiff");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, true);
    }

    private CopilotGitDiffInspectionService CreateService(ICopilotShellProcessRunner runner)
    {
        return new CopilotGitDiffInspectionService(runner, () => _gitExecutable);
    }

    private CopilotAgentRequest Request(CopilotAgentMode mode = CopilotAgentMode.Auto)
    {
        return new CopilotAgentRequest
        {
            UserText = "查看当前改了什么",
            Mode = mode,
            SearchRootPaths = [_tempRoot],
            History = [new CopilotRequestMessage("assistant", "previous answer")],
        };
    }

    private static CopilotShellProcessResult Result(string output)
    {
        return new CopilotShellProcessResult(0, false, output, string.Empty, TimeSpan.FromMilliseconds(10));
    }

    private static JsonDocument ReadResultJson(string content)
    {
        const string marker = "result_json: ";
        return JsonDocument.Parse(content[(content.IndexOf(marker, StringComparison.Ordinal) + marker.Length)..]);
    }

    private static void AssertPathFilterFollowsSeparator(CopilotShellProcessCommand command, string expectedPath)
    {
        var separatorIndex = command.Arguments.ToList().IndexOf("--");
        Assert.True(separatorIndex >= 0);
        Assert.Equal(expectedPath, command.Arguments[separatorIndex + 1]);
        Assert.Equal(separatorIndex + 2, command.Arguments.Count);
    }

    private sealed class QueueRunner(params CopilotShellProcessResult[] results) : ICopilotShellProcessRunner
    {
        private readonly Queue<CopilotShellProcessResult> _results = new(results);

        public List<CopilotShellProcessCommand> Commands { get; } = [];

        public Task<CopilotShellProcessResult> RunAsync(CopilotShellProcessCommand command, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commands.Add(command);
            return Task.FromResult(_results.Dequeue());
        }
    }
}
