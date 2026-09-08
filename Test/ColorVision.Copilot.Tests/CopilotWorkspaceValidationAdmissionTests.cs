using ColorVision.Copilot;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotWorkspaceValidationAdmissionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "CopilotValidationAdmission", Guid.NewGuid().ToString("N"));
    private readonly RecordingRunner _runner = new();
    private readonly CopilotWorkspaceValidationTool _tool;
    private readonly string _workspace;
    private readonly string _project;

    public CopilotWorkspaceValidationAdmissionTests()
    {
        _workspace = Path.Combine(_root, "workspace");
        _project = Path.Combine(_workspace, "Validation.csproj");
        Directory.CreateDirectory(_workspace);
        File.WriteAllText(_project, "<Project />");
        var dotnetPath = Path.Combine(_root, "dotnet.exe");
        File.WriteAllText(dotnetPath, string.Empty);
        _tool = new CopilotWorkspaceValidationTool(new CopilotWorkspaceValidationService(_runner, () => dotnetPath));
    }

    [Theory]
    [InlineData("请把 Foo.cs 中的 A 改成 B")]
    [InlineData("把这个名字换成新名称")]
    [InlineData("Change Foo.cs from A to B and explain why.")]
    public async Task RewordedPatchRequestsCanValidateOnlyAfterApproval(string prompt)
    {
        var request = CreateRequest(prompt);
        var patch = new CopilotApplyWorkspacePatchEnvelopeTool(new CopilotWorkspacePatchStore());
        var tools = new CopilotToolRegistry([patch, _tool]).FindTools(request);
        Assert.Contains(patch, tools);
        Assert.Contains(_tool, tools);

        var denied = await ExecuteAsync(request, _project, approved: false);
        Assert.Equal(CopilotToolExecutionState.Denied, denied.Execution.State);
        Assert.Equal(CopilotToolFailureKind.Authorization, denied.Result.FailureKind);
        Assert.Empty(_runner.Commands);

        var approved = await ExecuteAsync(request, _project, approved: true);
        Assert.True(approved.Result.Success, approved.Result.ErrorMessage);
        var command = Assert.Single(_runner.Commands);
        Assert.Equal(_workspace, command.WorkingDirectory);
        Assert.Equal("build", command.Arguments[0]);
        Assert.Contains(_project, command.Arguments);
    }

    [Fact]
    public void AvailabilityDoesNotRequireAnEditOrCreateACompletionRequirement()
    {
        var request = CreateRequest("你好");
        var tools = new CopilotToolRegistry([_tool]).FindTools(request);
        Assert.Same(_tool, Assert.Single(tools));
        Assert.Equal(CopilotAgentExecutionRequirement.None, CopilotAgentExecutionContract.Create(request, tools).Requirement);
    }

    [Theory]
    [InlineData(CopilotAgentMode.Chat, "运行测试", false)]
    [InlineData(CopilotAgentMode.Plan, "运行测试", false)]
    [InlineData(CopilotAgentMode.Diagnose, "运行测试", false)]
    [InlineData(CopilotAgentMode.Review, "检查代码", false)]
    [InlineData(CopilotAgentMode.Review, "运行测试", true)]
    [InlineData(CopilotAgentMode.Auto, "只读审计，不要修改任何文件", false)]
    [InlineData(CopilotAgentMode.Auto, "不要修改，只解释这个文件", false)]
    public async Task ModeAndExplicitWriteConstraintsStillApply(CopilotAgentMode mode, string prompt, bool available)
    {
        var request = CreateRequest(prompt, mode);
        Assert.Equal(available, new CopilotToolRegistry([_tool]).FindTools(request).Contains(_tool));
        var outcome = await ExecuteAsync(request, _project, approved: true, task: "test");
        Assert.Equal(available, outcome.Result.Success);
        Assert.Equal(available ? 1 : 0, _runner.Commands.Count);
        if (available)
            Assert.Equal("test", Assert.Single(_runner.Commands).Arguments[0]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FileOnlyScopeAndReadOnlySandboxCannotStartValidation(bool readOnlySandbox)
    {
        var request = new CopilotAgentRequest
        {
            Mode = CopilotAgentMode.Auto,
            UserText = "请修改这个文件并运行测试",
            WritableLocalRootPaths = readOnlySandbox ? [_workspace] : [],
            WritableLocalFilePaths = [_project],
            CodexSandboxMode = readOnlySandbox ? CopilotCodexSandboxMode.ReadOnly : CopilotCodexSandboxMode.WorkspaceWrite,
        };
        Assert.Empty(new CopilotToolRegistry([_tool]).FindTools(request));
        var outcome = await ExecuteAsync(request, _project, approved: true);
        Assert.Equal(CopilotToolExecutionState.Denied, outcome.Execution.State);
        Assert.Equal(CopilotToolFailureKind.Authorization, outcome.Result.FailureKind);
        Assert.Empty(_runner.Commands);
    }

    [Fact]
    public async Task ApprovalDoesNotAuthorizeAProjectOutsideTheWritableRoot()
    {
        var outsideProject = Path.Combine(_root, "Outside.csproj");
        File.WriteAllText(outsideProject, "<Project />");
        var request = CreateRequest("把这个名字换成新名称");
        Assert.Same(_tool, Assert.Single(new CopilotToolRegistry([_tool]).FindTools(request)));

        var outcome = await ExecuteAsync(request, outsideProject, approved: true);
        Assert.False(outcome.Result.Success);
        Assert.Equal(CopilotToolFailureKind.Authorization, outcome.Result.FailureKind);
        Assert.Empty(_runner.Commands);
    }

    private CopilotAgentRequest CreateRequest(string prompt, CopilotAgentMode mode = CopilotAgentMode.Auto) => new()
    {
        Mode = mode,
        UserText = prompt,
        WritableLocalRootPaths = [_workspace],
        CodexSandboxMode = CopilotCodexSandboxMode.WorkspaceWrite,
    };

    private Task<CopilotToolExecutionOutcome> ExecuteAsync(CopilotAgentRequest request, string project, bool approved, string task = "build") =>
        new CopilotToolExecutor(Array.Empty<ICopilotToolExecutionHook>()).ExecuteAsync(
            new CopilotToolInvocation
            {
                CallId = Guid.NewGuid().ToString("N"),
                Tool = _tool,
                AgentRequest = request,
                FrameworkApprovalGranted = approved,
                ToolInput = new CopilotAgentToolInput
                {
                    Arguments = new Dictionary<string, object?> { ["task"] = task, ["path"] = project },
                },
            },
            _ => { },
            CancellationToken.None);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class RecordingRunner : ICopilotWorkspaceValidationRunner
    {
        public List<CopilotWorkspaceValidationCommand> Commands { get; } = [];
        public Task<CopilotWorkspaceValidationProcessResult> RunAsync(CopilotWorkspaceValidationCommand command, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commands.Add(command);
            return Task.FromResult(new CopilotWorkspaceValidationProcessResult(0, false, string.Empty, string.Empty, TimeSpan.FromMilliseconds(1)));
        }
    }
}
