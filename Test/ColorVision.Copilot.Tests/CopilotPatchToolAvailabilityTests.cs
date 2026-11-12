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
    [InlineData("修复这个 bug，不要改接口", true)]
    [InlineData("不要改接口，修复这个空引用", true)]
    [InlineData("修复这个 bug，接口不要改", true)]
    [InlineData("帮我看下代码，不用改", false)]
    [InlineData("排查一下这个报错，不要动代码", false)]
    public void ClosingOptOutHidesWorkspaceEditsButAConstraintOnOnePartDoesNot(string prompt, bool canEdit)
    {
        var tools = new CopilotToolRegistry(CreateTools()).FindTools(CreateRequest(prompt));

        Assert.Equal(canEdit, tools.Any(tool => tool is CopilotApplyWorkspacePatchEnvelopeTool));
    }

    [Theory]
    [InlineData("帮我执行一下 ipconfig 命令", true)]
    [InlineData("跑一下 build.py 这个脚本", true)]
    [InlineData("这条命令执行失败了", false)]
    [InlineData("程序跑起来很卡", false)]
    [InlineData("什么是后台运行", false)]
    public void ColloquialRequestsReachTheShellButReportsAndQuestionsDoNot(string prompt, bool needsShell)
    {
        Assert.Equal(needsShell, CopilotToolIntentPolicy.NeedsShellExecution(CreateRequest(prompt)));
    }

    [Theory]
    [InlineData("把数据库里 id=3 的记录删除", true)]
    [InlineData("把这张表的状态都改成 NG", true)]
    [InlineData("把数据库表 A 的状态改成 NG，其他字段不要改", true)]
    [InlineData("别删数据库，看下有多少记录", false)]
    [InlineData("数据库里的结果为什么被删除了", false)]
    [InlineData("把配置里的数据库地址改成 127.0.0.1", false)]
    [InlineData("给数据库页面加一个按钮", false)]
    public void DatabaseWriteFollowsARequestedChangeNotAMentionOfOne(string prompt, bool needsWrite)
    {
        Assert.Equal(needsWrite, CopilotToolIntentPolicy.NeedsDatabaseWrite(CreateRequest(prompt)));
    }

    [Theory]
    [InlineData("搜索一下 MQTT 协议的规范", true)]
    [InlineData("上网搜一下 Halcon 的价格", true)]
    [InlineData("搜索一下这个函数的引用", false)]
    [InlineData("不要联网，搜索一下 MQTT 协议", false)]
    public void WebSearchWordsDoNotCaptureLocalSearchOrOverrideAnOptOut(string prompt, bool needsWeb)
    {
        Assert.Equal(needsWeb, CopilotToolIntentPolicy.NeedsPublicWebSearch(CreateRequest(prompt)));
    }

    [Theory]
    [InlineData("搜索一下 LoginManager 在哪", false)]
    [InlineData("上网搜一下 Halcon 的价格", true)]
    public void OnlyAnExplicitWebPhraseCanBecomeARequiredSearch(string prompt, bool required)
    {
        Assert.Equal(required, CopilotToolIntentPolicy.ExplicitlyRequiresPublicWebSearch(CreateRequest(prompt)));
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

    // Wording-independent exposure must stay limited to reads that need no approval and leave the machine only via the model.
    [Theory]
    [InlineData(CopilotAgentMode.Auto, true)]
    [InlineData(CopilotAgentMode.Diagnose, true)]
    [InlineData(CopilotAgentMode.Chat, false)]
    [InlineData(CopilotAgentMode.Web, false)]
    [InlineData(CopilotAgentMode.Review, false)]
    public void LocalReadToolsFollowTheModeAndNotTheWording(CopilotAgentMode mode, bool expected)
    {
        var root = Path.GetTempPath();
        var request = new CopilotAgentRequest
        {
            Mode = mode,
            UserText = "结果表里 NG 有几条",
            WritableLocalRootPaths = [root],
            SearchRootPaths = [root],
        };
        var names = new CopilotToolRegistry(CopilotToolRegistry.CreateDefaultTools()).FindTools(request)
            .Select(tool => tool.Name)
            .ToArray();

        foreach (var name in new[] { "QueryDatabaseSql", "QueryFlowExecutionStats", CopilotSharedAgentToolNames.GetRecentLog, "InspectWindowsServices", "InspectTcpPort" })
            Assert.Equal(expected, names.Contains(name));
        Assert.DoesNotContain("ExecuteDatabaseSql", names);
        if (mode != CopilotAgentMode.Web)
        {
            Assert.DoesNotContain("WebSearch", names);
            Assert.DoesNotContain("FetchUrl", names);
        }
        if (mode != CopilotAgentMode.Review)
            Assert.DoesNotContain("InspectGitDiff", names);
    }

    [Fact]
    public void AWriteOptOutKeepsWorkspaceSearchButRemovesPatchTools()
    {
        var root = Path.GetTempPath();
        var names = new CopilotToolRegistry(CopilotToolRegistry.CreateDefaultTools())
            .FindTools(new CopilotAgentRequest
            {
                Mode = CopilotAgentMode.Auto,
                UserText = "别改代码，只分析原因",
                WritableLocalRootPaths = [root],
                SearchRootPaths = [root],
            })
            .Select(tool => tool.Name)
            .ToArray();

        Assert.Contains(CopilotSharedAgentToolNames.GrepText, names);
        Assert.DoesNotContain("ApplyWorkspacePatchEnvelope", names);
    }

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
