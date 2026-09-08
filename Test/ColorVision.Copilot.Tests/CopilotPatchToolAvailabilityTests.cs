using ColorVision.Copilot;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotPatchToolAvailabilityTests
{
    [Theory]
    [InlineData("请把 Foo.cs 中的 A 改成 B")]
    [InlineData("把这个名字换成新名称")]
    [InlineData("Change Foo.cs from A to B and explain why.")]
    public void WorkspacePatchAvailabilityIncludesItsReadingToolsForRewordedRequests(string prompt)
    {
        var request = CreateRequest(prompt);
        var tools = new CopilotToolRegistry(CreateTools()).FindTools(request);

        Assert.Contains(tools, tool => tool is CopilotPreviewWorkspacePatchEnvelopeTool);
        Assert.Contains(tools, tool => tool is CopilotApplyWorkspacePatchEnvelopeTool);
        Assert.Contains(tools, tool => tool is CopilotRollbackWorkspacePatchEnvelopeTool);
        Assert.Contains(tools, tool => tool is CopilotReadLocalFileTool);
        Assert.Contains(tools, tool => tool is CopilotGrepTextTool);
        Assert.Contains(tools, tool => tool is CopilotSearchFilesTool);
        Assert.Contains(tools, tool => tool is CopilotListDirectoryTool);
    }

    [Theory]
    [InlineData("请修改流程节点的超时为 5000，并解释为什么")]
    [InlineData("把流程节点的超时改成 5000")]
    [InlineData("Update node timeout in the flow graph to 5000 and explain why.")]
    public void FlowPatchAvailabilityDoesNotRequireMutationPhrasesOrRejectAnExplanation(string prompt)
    {
        var tools = new CopilotToolRegistry(CreateTools()).FindTools(CreateRequest(prompt));

        Assert.Contains(tools, tool => tool is CopilotPreviewFlowPatchTool);
        Assert.Contains(tools, tool => tool is CopilotApplyFlowPatchTool);
    }

    [Fact]
    public void AvailablePatchToolsDoNotTurnAnOrdinaryRequestIntoARequiredWrite()
    {
        var request = CreateRequest("你好");
        var tools = new CopilotToolRegistry(CreateTools()).FindTools(request);

        Assert.Contains(tools, tool => tool is CopilotApplyWorkspacePatchEnvelopeTool);
        Assert.Equal(CopilotAgentExecutionRequirement.None,
            CopilotAgentExecutionContract.Create(request, tools).Requirement);
    }

    [Theory]
    [InlineData(CopilotAgentMode.Chat)]
    [InlineData(CopilotAgentMode.Plan)]
    [InlineData(CopilotAgentMode.Review)]
    [InlineData(CopilotAgentMode.Diagnose)]
    public async Task ReadOnlyModesHidePatchesAndRejectInjectedWriteCalls(CopilotAgentMode mode)
    {
        await AssertPatchesUnavailableAsync(CreateRequest("请修改流程节点和 Foo.cs，并解释为什么", mode));
    }

    [Theory]
    [InlineData("请分析流程节点，不要修改")]
    [InlineData("只解释流程节点和文件的作用")]
    [InlineData("Explain the flow graph and workspace; do not modify anything.")]
    [InlineData(" ")]
    public async Task ExplicitOptOutAndEmptyRequestsStillRejectPatches(string prompt)
    {
        await AssertPatchesUnavailableAsync(CreateRequest(prompt));
    }

    [Fact]
    public async Task ReadOnlySandboxStillRejectsPatches()
    {
        await AssertPatchesUnavailableAsync(CreateRequest("请修改流程节点和 Foo.cs", readOnlySandbox: true));
    }

    [Fact]
    public void NoWritableScopeDoesNotExposeWorkspacePatches()
    {
        var tools = new CopilotToolRegistry(CreateTools()).FindTools(
            CreateRequest("请把 Foo.cs 中的 A 改成 B", writable: false));

        Assert.DoesNotContain(tools, IsWorkspacePatch);
    }

    [Fact]
    public async Task VisibleWorkspaceWritesStillRequireNativeApproval()
    {
        var request = CreateRequest("请把 Foo.cs 中的 A 改成 B");
        var tools = new CopilotToolRegistry(CreateTools()).FindTools(request);
        foreach (var tool in tools.Where(tool => IsWorkspacePatch(tool) && tool.Capability.Access == CopilotToolAccess.Write))
        {
            var result = await tool.ExecuteAsync(request, CopilotAgentToolInput.Empty, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(CopilotToolFailureKind.Authorization, result.FailureKind);
        }
    }

    [Fact]
    public void PatchScopeLoadsInstructionsWithoutReclassifyingTheApplicationContextOrWideningReadRoots()
    {
        var root = Path.Combine(Path.GetTempPath(), $"copilot-patch-availability-{Guid.NewGuid():N}");
        var workspace = Directory.CreateDirectory(Path.Combine(root, "workspace")).FullName;
        var globalRoot = Directory.CreateDirectory(Path.Combine(root, "global")).FullName;
        var readRoot = Directory.CreateDirectory(Path.Combine(root, "read-only")).FullName;
        Directory.CreateDirectory(Path.Combine(workspace, ".git"));
        var instructionsPath = Path.Combine(workspace, "AGENTS.md");
        File.WriteAllText(instructionsPath, "Keep the project naming conventions.");
        File.WriteAllText(Path.Combine(readRoot, "AGENTS.md"), "Additional read access is not project trust.");
        try
        {
            var host = new CopilotAgentHostContextSnapshot(null, workspace, null, null, null,
                additionalReadRootPaths: null, globalInstructionRootPath: globalRoot);
            var plan = CopilotAgentRequestFactory.Prepare("把这个名字换成新名称", CopilotAgentMode.Auto, host);

            Assert.False(plan.ContextRequest.RequiresWorkspaceEvidence);
            Assert.Contains(plan.ProjectInstructions, document => document.Path == instructionsPath);
            Assert.Equal([workspace], plan.WritableLocalRootPaths);

            var hostWithReadRoot = new CopilotAgentHostContextSnapshot(null, workspace, null, null, null,
                additionalReadRootPaths: [readRoot], globalInstructionRootPath: globalRoot);
            var readPlan = CopilotAgentRequestFactory.Prepare("把这个名字换成新名称", CopilotAgentMode.Auto, hostWithReadRoot);

            Assert.Contains(readRoot, readPlan.SearchRootPaths);
            Assert.DoesNotContain(readRoot, readPlan.WritableLocalRootPaths);
            Assert.DoesNotContain(readRoot, readPlan.TrustedProjectRootPaths);
            Assert.DoesNotContain(readPlan.ProjectInstructions, document => document.Path.StartsWith(readRoot, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task AssertPatchesUnavailableAsync(CopilotAgentRequest request)
    {
        var patchTools = CreateTools().Where(IsPatch).ToArray();
        Assert.Empty(new CopilotToolRegistry(patchTools).FindTools(request));
        foreach (var tool in patchTools.Where(tool => tool.Capability.Access == CopilotToolAccess.Write))
        {
            var decision = await new CopilotWriteToolPolicyHook().BeforeExecuteAsync(new CopilotToolExecutionHookContext
            {
                Invocation = new CopilotToolInvocation
                {
                    AgentRequest = request,
                    Tool = tool,
                    ToolInput = CopilotAgentToolInput.Empty,
                },
            }, CancellationToken.None);

            Assert.False(decision.ShouldProceed);
        }
    }

    private static CopilotAgentRequest CreateRequest(string prompt, CopilotAgentMode mode = CopilotAgentMode.Auto,
        bool writable = true, bool readOnlySandbox = false) => new()
    {
        UserText = prompt,
        Mode = mode,
        SearchRootPaths = [Path.GetTempPath()],
        WritableLocalRootPaths = writable ? [Path.GetTempPath()] : [],
        CodexSandboxMode = readOnlySandbox ? CopilotCodexSandboxMode.ReadOnly : CopilotCodexSandboxMode.WorkspaceWrite,
    };

    private static ICopilotTool[] CreateTools()
    {
        var store = new CopilotWorkspacePatchStore();
        return
        [
            new CopilotPreviewWorkspacePatchEnvelopeTool(store),
            new CopilotApplyWorkspacePatchEnvelopeTool(store),
            new CopilotRollbackWorkspacePatchEnvelopeTool(store),
            new CopilotPreviewFlowPatchTool(),
            new CopilotApplyFlowPatchTool(),
            new CopilotReadLocalFileTool(),
            new CopilotGrepTextTool(),
            new CopilotSearchFilesTool(),
            new CopilotListDirectoryTool(),
        ];
    }

    private static bool IsPatch(ICopilotTool tool) => IsWorkspacePatch(tool)
        || tool is CopilotPreviewFlowPatchTool or CopilotApplyFlowPatchTool;

    private static bool IsWorkspacePatch(ICopilotTool tool) => tool is CopilotPreviewWorkspacePatchEnvelopeTool
        or CopilotApplyWorkspacePatchEnvelopeTool or CopilotRollbackWorkspacePatchEnvelopeTool;
}
