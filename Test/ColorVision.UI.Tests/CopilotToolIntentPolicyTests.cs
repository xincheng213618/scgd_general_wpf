using ColorVision.Copilot;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

public sealed class CopilotToolIntentPolicyTests
{
    [Theory]
    [InlineData("代码是什么？")]
    [InlineData("文件和目录有什么区别？")]
    [InlineData("Python 脚本是什么？")]
    [InlineData("如何创建文件？")]
    [InlineData("What is source code?")]
    [InlineData("How to implement a class?")]
    public void ConceptualQuestionsDoNotRequireLocalEvidence(string userText)
    {
        Assert.False(CopilotToolIntentPolicy.NeedsLocalEvidence(Request(userText)));
    }

    [Fact]
    public void ConceptualQuestionDoesNotExposeWorkspaceSearchSurface()
    {
        var request = Request(
            "代码是什么？",
            searchRoots: [@"C:\workspace"]);

        Assert.False(new CopilotSearchFilesTool().IsAvailable(request));
        Assert.False(new CopilotGrepTextTool().IsAvailable(request));
        Assert.False(new CopilotReadLocalFileTool().IsAvailable(request));
        Assert.False(new CopilotListDirectoryTool().IsAvailable(request));
        Assert.False(ExploreRole().IsAvailable(request));
    }

    [Fact]
    public void ExplicitWorkspaceQuestionExposesWorkspaceSearchSurface()
    {
        var request = Request(
            "在当前项目里搜索这个实现",
            searchRoots: [@"C:\workspace"]);

        Assert.True(new CopilotSearchFilesTool().IsAvailable(request));
        Assert.True(new CopilotGrepTextTool().IsAvailable(request));
        Assert.True(new CopilotReadLocalFileTool().IsAvailable(request));
        Assert.True(new CopilotListDirectoryTool().IsAvailable(request));
        Assert.True(ExploreRole().IsAvailable(request));
    }

    [Fact]
    public void RuntimeCatalogCanBeCreatedAndOmitsLocalToolsForAConceptualQuestion()
    {
        var registry = new CopilotToolRegistry(CopilotToolRegistry.CreateBuiltInCatalogTools());
        var toolNames = registry.FindTools(Request(
                "代码是什么？",
                searchRoots: [@"C:\workspace"]))
            .Select(tool => tool.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("SearchFiles", toolNames);
        Assert.DoesNotContain("GrepText", toolNames);
        Assert.DoesNotContain("ReadLocalFile", toolNames);
        Assert.DoesNotContain("ListDirectory", toolNames);
        Assert.DoesNotContain("DelegateExplore", toolNames);
    }

    [Fact]
    public void ConceptualRequestSkipsProjectInstructionDiscovery()
    {
        var root = Path.Combine(Path.GetTempPath(), $"copilot-intent-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "AGENTS.md"), "# Workspace instructions");
        try
        {
            var hostContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                solutionDirectoryPath: root,
                attachments: null);

            var conceptual = CopilotAgentRequestFactory.Prepare("代码是什么？", CopilotAgentMode.Auto, hostContext);
            var workspace = CopilotAgentRequestFactory.Prepare("查看当前项目代码", CopilotAgentMode.Auto, hostContext);

            Assert.Empty(conceptual.ProjectInstructions);
            Assert.Single(workspace.ProjectInstructions);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("当前项目中的批量转换代码在哪里？")]
    [InlineData("查看这个文件")]
    [InlineData("Search the workspace for this implementation")]
    public void ExplicitWorkspaceQuestionsRequireLocalEvidence(string userText)
    {
        Assert.True(CopilotToolIntentPolicy.NeedsLocalEvidence(Request(userText)));
    }

    [Theory]
    [InlineData("创建一个 Python 脚本并执行它")]
    [InlineData("用 Node.js 批量处理这些文件")]
    [InlineData("run npm test")]
    public void ExecutableAutomationRequestsExposeShell(string userText)
    {
        Assert.True(CopilotToolIntentPolicy.NeedsShellExecution(Request(userText)));
    }

    [Fact]
    public void NativeCvrawBatchConversionDoesNotRequireShell()
    {
        var request = Request("批量转换 cvraw 文件为 TIFF");

        Assert.True(CopilotToolIntentPolicy.NeedsBatchImageProcessing(request));
        Assert.False(CopilotToolIntentPolicy.NeedsShellExecution(request));
    }

    [Fact]
    public void NativeCvrawBatchConversionRequiresOpeningTheProductWorkflow()
    {
        var contract = CopilotAgentExecutionContract.Create(
            Request("批量转换 cvraw 文件为 TIFF"),
            [new NamedTool("OpenBatchImageProcessing")]);

        Assert.Equal(CopilotAgentExecutionRequirement.BatchImageProcessing, contract.Requirement);
        Assert.Equal(["OpenBatchImageProcessing"], contract.AcceptedToolNames);
    }

    [Theory]
    [InlineData("Python 脚本是什么？")]
    [InlineData("如何运行 Python 脚本？")]
    [InlineData("Explain how to run a Node.js script")]
    public void ScriptExplanationsDoNotExecuteShell(string userText)
    {
        Assert.False(CopilotToolIntentPolicy.NeedsShellExecution(Request(userText)));
    }

    [Fact]
    public void ScriptCreationIntentExposesCreateAndShellTools()
    {
        var request = Request(
            "创建一个 Python 脚本并执行它",
            writableRoots: [@"C:\workspace"]);

        Assert.True(CopilotToolIntentPolicy.NeedsWorkspaceCreate(request));
        Assert.True(CopilotToolIntentPolicy.NeedsShellExecution(request));
    }

    [Fact]
    public void ScriptCreationExplanationDoesNotRequestAWrite()
    {
        var request = Request(
            "如何创建并运行 Python 脚本？",
            writableRoots: [@"C:\workspace"]);

        Assert.False(CopilotToolIntentPolicy.NeedsWorkspaceCreate(request));
        Assert.False(CopilotToolIntentPolicy.NeedsShellExecution(request));
    }

    [Theory]
    [InlineData("批量转换 cvraw 文件为 TIFF", true)]
    [InlineData("打开批量执行算法", true)]
    [InlineData("CVRAW 是什么？", false)]
    public void BatchImageIntentDistinguishesActionsFromConcepts(string userText, bool expected)
    {
        Assert.Equal(expected, CopilotToolIntentPolicy.NeedsBatchImageProcessing(Request(userText)));
    }

    [Fact]
    public void ScriptCreationExecutionContractRequiresWriteThenProcess()
    {
        var request = Request(
            "创建一个 Python 脚本并执行它",
            writableRoots: [@"C:\workspace"]);
        var contract = CopilotAgentExecutionContract.Create(
            request,
            [
                new NamedTool("ApplyWorkspacePatchEnvelope"),
                new NamedTool("RunShellCommand"),
            ]);

        Assert.Equal(CopilotAgentExecutionRequirement.WorkspaceCreateAndShellExecution, contract.Requirement);
        Assert.Equal(
            ["ApplyWorkspacePatchEnvelope", "RunShellCommand"],
            contract.AcceptedToolNames);
        Assert.True(
            contract.BuildInitialInstruction().IndexOf("ApplyWorkspacePatchEnvelope", StringComparison.Ordinal)
            < contract.BuildInitialInstruction().IndexOf("RunShellCommand", StringComparison.Ordinal));
    }

    [Fact]
    public void DirectCommandExecutionContractRequiresRealProcessEvidence()
    {
        var contract = CopilotAgentExecutionContract.Create(
            Request("用 CMD 执行 dir"),
            [new NamedTool("RunShellCommand")]);

        Assert.Equal(CopilotAgentExecutionRequirement.ShellExecution, contract.Requirement);
        Assert.Equal(["RunShellCommand"], contract.AcceptedToolNames);
        Assert.Contains("RunShellCommand", contract.BuildInitialInstruction(), StringComparison.Ordinal);
    }

    private static CopilotAgentRequest Request(
        string userText,
        IReadOnlyList<string>? writableRoots = null,
        IReadOnlyList<string>? searchRoots = null)
    {
        return new CopilotAgentRequest
        {
            UserText = userText,
            Mode = CopilotAgentMode.Auto,
            WritableLocalRootPaths = writableRoots ?? Array.Empty<string>(),
            SearchRootPaths = searchRoots ?? Array.Empty<string>(),
        };
    }

    private static CopilotSubagentRoleDescriptor ExploreRole()
    {
        return CopilotSubagentRoleCatalog.CreateBuiltInRoles()
            .Single(role => string.Equals(role.ToolName, "DelegateExplore", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class NamedTool(string name) : ICopilotTool
    {
        public string Name { get; } = name;

        public string Description => Name;

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new CopilotToolResult { ToolName = Name, Success = true });
        }
    }
}
