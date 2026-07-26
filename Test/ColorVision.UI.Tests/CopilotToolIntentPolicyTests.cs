using ColorVision.Copilot;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
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

    [Fact]
    public void ExplicitFileRequestLoadsMatchingClaudePathRule()
    {
        var root = Path.Combine(Path.GetTempPath(), $"copilot-rule-intent-{Guid.NewGuid():N}");
        var sourceDirectory = Path.Combine(root, "src");
        var rulesDirectory = Path.Combine(root, ".claude", "rules");
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(rulesDirectory);
        var sourcePath = Path.Combine(sourceDirectory, "Target.cs");
        var rulePath = Path.Combine(rulesDirectory, "source.md");
        File.WriteAllText(
            rulePath,
            """
            ---
            paths:
              - "src/**/*.cs"
            ---
            # Source rule
            """);
        try
        {
            var hostContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                solutionDirectoryPath: root,
                attachments: null);

            var plan = CopilotAgentRequestFactory.Prepare(
                $"查看文件 {sourcePath}",
                CopilotAgentMode.Auto,
                hostContext);

            CopilotProjectInstructionDocument instruction = Assert.Single(plan.ProjectInstructions);
            Assert.Equal(rulePath, instruction.Path, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FileAttachmentLoadsMatchingClaudePathRule()
    {
        var root = Path.Combine(Path.GetTempPath(), $"copilot-rule-attachment-{Guid.NewGuid():N}");
        var sourceDirectory = Path.Combine(root, "src");
        var rulesDirectory = Path.Combine(root, ".claude", "rules");
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(rulesDirectory);
        var sourcePath = Path.Combine(sourceDirectory, "Target.cs");
        var rulePath = Path.Combine(rulesDirectory, "source.md");
        File.WriteAllText(sourcePath, "namespace Target;");
        File.WriteAllText(
            rulePath,
            """
            ---
            paths:
              - "src/**/*.cs"
            ---
            # Source rule
            """);
        try
        {
            var hostContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                solutionDirectoryPath: root,
                attachments: [CopilotAttachmentItem.CreateFile(sourcePath)]);

            var plan = CopilotAgentRequestFactory.Prepare(
                "查看这个文件",
                CopilotAgentMode.Auto,
                hostContext);

            CopilotProjectInstructionDocument instruction = Assert.Single(plan.ProjectInstructions);
            Assert.Equal(rulePath, instruction.Path, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExplicitExternalFileRemainsSearchableWithoutLoadingAdjacentProjectInstructions()
    {
        var root = Path.Combine(Path.GetTempPath(), $"copilot-trusted-roots-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var externalRoot = Path.Combine(root, "external");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(externalRoot);
        var workspaceInstructions = Path.Combine(workspaceRoot, "AGENTS.md");
        var externalInstructions = Path.Combine(externalRoot, "AGENTS.md");
        var externalFile = Path.Combine(externalRoot, "Target.cs");
        File.WriteAllText(workspaceInstructions, "# Trusted workspace");
        File.WriteAllText(externalInstructions, "# Untrusted adjacent instructions");
        File.WriteAllText(externalFile, "namespace External;");
        try
        {
            var hostContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                solutionDirectoryPath: workspaceRoot,
                attachments: null);

            var plan = CopilotAgentRequestFactory.Prepare(
                $"查看文件 {externalFile}",
                CopilotAgentMode.Auto,
                hostContext);

            Assert.Contains(externalRoot, plan.SearchRootPaths, StringComparer.OrdinalIgnoreCase);
            Assert.Equal([workspaceRoot], plan.TrustedProjectRootPaths, StringComparer.OrdinalIgnoreCase);
            CopilotProjectInstructionDocument instruction = Assert.Single(plan.ProjectInstructions);
            Assert.Equal(workspaceInstructions, instruction.Path, ignoreCase: true);
            Assert.DoesNotContain(plan.ProjectInstructions, document =>
                string.Equals(document.Path, externalInstructions, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExternalFileAttachmentDoesNotBecomeAProjectInstructionRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"copilot-trusted-attachment-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var externalRoot = Path.Combine(root, "external");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(externalRoot);
        var workspaceInstructions = Path.Combine(workspaceRoot, "AGENTS.md");
        var externalInstructions = Path.Combine(externalRoot, "CLAUDE.md");
        var externalFile = Path.Combine(externalRoot, "Target.cs");
        File.WriteAllText(workspaceInstructions, "# Trusted workspace");
        File.WriteAllText(externalInstructions, "# Untrusted adjacent instructions");
        File.WriteAllText(externalFile, "namespace External;");
        try
        {
            var hostContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                solutionDirectoryPath: workspaceRoot,
                attachments: [CopilotAttachmentItem.CreateFile(externalFile)]);

            var plan = CopilotAgentRequestFactory.Prepare(
                "查看这个文件",
                CopilotAgentMode.Auto,
                hostContext);

            Assert.Contains(externalRoot, plan.SearchRootPaths, StringComparer.OrdinalIgnoreCase);
            Assert.Equal([workspaceRoot], plan.TrustedProjectRootPaths, StringComparer.OrdinalIgnoreCase);
            CopilotProjectInstructionDocument instruction = Assert.Single(plan.ProjectInstructions);
            Assert.Equal(workspaceInstructions, instruction.Path, ignoreCase: true);
            Assert.DoesNotContain(plan.ProjectInstructions, document =>
                string.Equals(document.Path, externalInstructions, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ActiveDocumentDirectoryBecomesTheProjectRootWhenNoSolutionIsOpen()
    {
        var root = Path.Combine(Path.GetTempPath(), $"copilot-active-root-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var activeDocument = Path.Combine(root, "Target.cs");
        File.WriteAllText(activeDocument, "namespace Active;");
        try
        {
            var hostContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                solutionDirectoryPath: null,
                attachments: null);

            var trustedRoots = CopilotAgentRequestFactory.BuildTrustedProjectRootPaths(hostContext);

            Assert.Equal([root], trustedRoots, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AgentSkillsUseTrustedProjectRootsInsteadOfAllSearchRoots()
    {
        var root = Path.Combine(Path.GetTempPath(), $"copilot-trusted-skills-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var externalRoot = Path.Combine(root, "external");
        var applicationRoot = Path.Combine(root, "application");
        var workspaceSkills = Path.Combine(workspaceRoot, ".agents", "skills");
        var externalSkills = Path.Combine(externalRoot, ".agents", "skills");
        Directory.CreateDirectory(workspaceSkills);
        Directory.CreateDirectory(externalSkills);
        Directory.CreateDirectory(applicationRoot);
        try
        {
            var request = new CopilotAgentRequest
            {
                SearchRootPaths = [workspaceRoot, externalRoot],
                TrustedProjectRootPaths = [workspaceRoot],
            };

            var skillRoots = CopilotAgentSkills.ResolveSearchPaths(request, applicationRoot);

            Assert.Contains(workspaceSkills, skillRoots, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(externalSkills, skillRoots, StringComparer.OrdinalIgnoreCase);
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
    [InlineData(@"C:\Users\17917\Desktop\work 里面的 cvraw 文件，写一个 python 脚本，运行批量转换成 tif")]
    [InlineData("用 Node.js 批量处理这些文件")]
    [InlineData("run npm test")]
    public void ExecutableAutomationRequestsExposeShell(string userText)
    {
        Assert.True(CopilotToolIntentPolicy.NeedsShellExecution(Request(userText)));
    }

    [Fact]
    public void ShortExecuteFollowUpRetainsShellIntentFromThePreviousScriptRequest()
    {
        var request = new CopilotAgentRequest
        {
            UserText = "执行",
            Mode = CopilotAgentMode.Auto,
            History =
            [
                new CopilotRequestMessage(
                    "user",
                    @"C:\Users\17917\Desktop\work 里面的 cvraw 文件，写一个 python 脚本，运行批量转换成 tif"),
                new CopilotRequestMessage(
                    "assistant",
                    "脚本已创建为 convert_cvraw_to_tiff.py。"),
            ],
        };

        Assert.True(CopilotToolIntentPolicy.NeedsShellExecution(request));
        Assert.True(new CopilotShellCommandTool().IsAvailable(request));
    }

    [Fact]
    public void ExplicitPythonBatchConversionDoesNotExposeTheNativeConversionDetour()
    {
        var request = Request(
            @"C:\Users\17917\Desktop\work 里面的 cvraw 文件，写一个 python 脚本，运行批量转换成 tif");

        Assert.True(CopilotToolIntentPolicy.NeedsShellExecution(request));
        Assert.False(CopilotToolIntentPolicy.NeedsBatchImageProcessing(request));
        Assert.False(CopilotToolIntentPolicy.NeedsBatchImageConversionExecution(request));
        Assert.False(new CopilotConvertBatchImagesTool().IsAvailable(request));
    }

    [Fact]
    public void ExplicitScriptRequestIgnoresUnrelatedAttachedFlowContext()
    {
        var request = Request(
            @"C:\Users\17917\Desktop\work 里面的 cvraw 文件，写一个 python 脚本，运行批量转换成 tif",
            contextItems:
            [
                new CopilotContextItem
                {
                    Id = "workspace:flow",
                    Title = "Flow context · AOI",
                    Content = "Attached automatically by the current page.",
                },
            ]);

        Assert.True(CopilotToolIntentPolicy.NeedsShellExecution(request));
        Assert.False(CopilotToolIntentPolicy.NeedsFlowGraph(request));
        Assert.False(new CopilotSearchFlowNodeCatalogTool().IsAvailable(request));
    }

    [Fact]
    public void ExplicitExternalDirectoryBecomesWritableForRequestedScriptCreation()
    {
        var root = Path.Combine(Path.GetTempPath(), $"copilot-script-scope-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var externalRoot = Path.Combine(root, "work");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(externalRoot);
        try
        {
            var hostContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                solutionDirectoryPath: workspaceRoot,
                attachments: null);

            var plan = CopilotAgentRequestFactory.Prepare(
                $"{externalRoot} 里面的 CVRAW 文件，写一个 Python 脚本并运行批量转换成 TIFF",
                CopilotAgentMode.Auto,
                hostContext);

            Assert.Contains(externalRoot, plan.WritableLocalRootPaths, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ReadOnlyExternalDirectoryDoesNotBecomeWritable()
    {
        var root = Path.Combine(Path.GetTempPath(), $"copilot-read-scope-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var externalRoot = Path.Combine(root, "external");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(externalRoot);
        try
        {
            var hostContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                solutionDirectoryPath: workspaceRoot,
                attachments: null);

            var plan = CopilotAgentRequestFactory.Prepare(
                $"查看 {externalRoot} 里面有哪些 CVRAW 文件",
                CopilotAgentMode.Auto,
                hostContext);

            Assert.DoesNotContain(externalRoot, plan.WritableLocalRootPaths, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EnvironmentContextPublishesCurrentColorVisionExecutable()
    {
        var context = CopilotAgentEnvironmentContext.Capture(Request(
            "创建一个 Python 脚本并执行它",
            writableRoots: [Environment.CurrentDirectory],
            searchRoots: [Environment.CurrentDirectory]));

        Assert.Equal(Environment.ProcessPath, context.ApplicationExecutablePath, ignoreCase: true);
        Assert.Contains("\"application_executable\"", context.BuildPromptDataBlock(), StringComparison.Ordinal);
        Assert.True(context.IsStructurallyValid());
    }

    [Fact]
    public void DirectBatchScriptOutcomeDoesNotCreatePlanOnlyProviderTurns()
    {
        var request = Request(
            @"C:\Users\17917\Desktop\work 里面的 cvraw 文件，写一个 python 脚本，运行批量转换成 tif",
            writableRoots: [@"C:\Users\17917\Desktop\work"],
            searchRoots: [@"C:\Users\17917\Desktop\work"]);

        Assert.False(CopilotToolIntentPolicy.NeedsTaskLedger(request));
    }

    [Theory]
    [InlineData("先制定计划，再检查当前项目并修复编译错误")]
    [InlineData("Make a plan for fixing and validating this project")]
    public void ExplicitPlanningRequestsEnableTaskLedger(string userText)
    {
        Assert.True(CopilotToolIntentPolicy.NeedsTaskLedger(Request(
            userText,
            writableRoots: [@"C:\workspace"],
            searchRoots: [@"C:\workspace"])));
    }

    [Fact]
    public void RuntimeEnvironmentIsTheVariablePromptSuffix()
    {
        var request = Request("检查当前项目的实现", searchRoots: [Environment.CurrentDirectory]);
        var environment = CopilotAgentEnvironmentContext.Capture(request);

        var instructions = CopilotMicrosoftAgentFrameworkRuntime.BuildHarnessInstructions(
            request,
            [new NamedTool("SearchFiles")],
            environment,
            taskLedgerEnabled: false,
            agentModeEnabled: false);

        Assert.True(
            instructions.IndexOf("SearchFiles and GrepText", StringComparison.Ordinal)
            < instructions.IndexOf("<runtime_environment>", StringComparison.Ordinal));
        Assert.EndsWith("</runtime_environment>", instructions, StringComparison.Ordinal);
    }

    [Fact]
    public void TokenUsageAggregatesCacheReadsWithoutDoubleCountingTotals()
    {
        var usage = new CopilotTokenUsage(100, 20, 120, 80)
            .Add(new CopilotTokenUsage(50, 10, 60, 40));

        Assert.Equal(150, usage.InputTokens);
        Assert.Equal(30, usage.OutputTokens);
        Assert.Equal(180, usage.EffectiveTotalTokens);
        Assert.Equal(120, usage.EffectiveCachedInputTokens);
        Assert.Equal(80d, usage.CachedInputPercentage);
    }

    [Fact]
    public void OpenAiUsageReadsCachedTokensAsInputSubset()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "usage": {
                "prompt_tokens": 100,
                "completion_tokens": 20,
                "total_tokens": 120,
                "prompt_tokens_details": { "cached_tokens": 80 }
              }
            }
            """);

        var usage = CopilotChatService.ExtractOpenAiUsage(document.RootElement);

        Assert.Equal(new CopilotTokenUsage(100, 20, 120, 80), usage);
    }

    [Fact]
    public void AnthropicUsageSeparatesCacheReadsFromLogicalInput()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "usage": {
                "input_tokens": 10,
                "output_tokens": 5,
                "cache_creation_input_tokens": 20,
                "cache_read_input_tokens": 70
              }
            }
            """);

        var usage = CopilotChatService.ExtractAnthropicUsage(document.RootElement);

        Assert.Equal(new CopilotTokenUsage(100, 5, 105, 70), usage);
    }

    [Fact]
    public void NativeCvrawBatchConversionDoesNotRequireShell()
    {
        var request = Request("批量转换 cvraw 文件为 TIFF");

        Assert.True(CopilotToolIntentPolicy.NeedsBatchImageProcessing(request));
        Assert.False(CopilotToolIntentPolicy.NeedsShellExecution(request));
    }

    [Fact]
    public void NativeCvrawBatchConversionRequiresRealConversionEvidence()
    {
        var contract = CopilotAgentExecutionContract.Create(
            Request("批量转换 cvraw 文件为 TIFF"),
            [
                new NamedTool("ConvertBatchImages"),
                new NamedTool("OpenBatchImageProcessing"),
            ]);

        Assert.Equal(CopilotAgentExecutionRequirement.BatchImageConversion, contract.Requirement);
        Assert.Equal(["ConvertBatchImages"], contract.AcceptedToolNames);
    }

    [Fact]
    public void NativeCvrawConversionDoesNotRequireTextReadEvidenceForTheBinarySource()
    {
        var root = Path.Combine(Path.GetTempPath(), $"copilot-cvraw-contract-{Guid.NewGuid():N}");
        var source = Path.Combine(root, "sample.cvraw");
        Directory.CreateDirectory(root);
        File.WriteAllBytes(source, [0x43, 0x56, 0x52, 0x41, 0x57]);
        try
        {
            var request = Request($"把 {source} 转换为 TIFF", readableFiles: [source]);

            var contract = CopilotAgentExecutionContract.Create(
                request,
                [
                    new NamedTool("ReadLocalFile"),
                    new NamedTool("ConvertBatchImages"),
                ]);

            Assert.Equal(CopilotAgentExecutionRequirement.BatchImageConversion, contract.Requirement);
            Assert.Equal(["ConvertBatchImages"], contract.AcceptedToolNames);
            Assert.DoesNotContain("ReadLocalFile", contract.BuildInitialInstruction(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void NativeCvrawConversionStillRequiresTextReadEvidenceForAManifest()
    {
        var root = Path.Combine(Path.GetTempPath(), $"copilot-cvraw-contract-{Guid.NewGuid():N}");
        var manifest = Path.Combine(root, "manifest.txt");
        Directory.CreateDirectory(root);
        File.WriteAllText(manifest, "sample.cvraw");
        try
        {
            var request = Request($"按照 {manifest} 批量转换 CVRAW 为 TIFF", readableFiles: [manifest]);

            var contract = CopilotAgentExecutionContract.Create(
                request,
                [
                    new NamedTool("ReadLocalFile"),
                    new NamedTool("ConvertBatchImages"),
                ]);

            Assert.Equal(CopilotAgentExecutionRequirement.BatchImageConversion, contract.Requirement);
            Assert.Equal(["ReadLocalFile", "ConvertBatchImages"], contract.AcceptedToolNames);
            Assert.Contains("ReadLocalFile", contract.BuildInitialInstruction(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void OpeningTheInteractiveBatchProcessorDoesNotRequireConversionEvidence()
    {
        var contract = CopilotAgentExecutionContract.Create(
            Request("打开批量执行算法"),
            [
                new NamedTool("ConvertBatchImages"),
                new NamedTool("OpenBatchImageProcessing"),
            ]);

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
    [InlineData("CVRAW转TIFF", true)]
    [InlineData("转换这个 CVRAW 文件", true)]
    [InlineData("打开批量执行算法", true)]
    [InlineData("CVRAW 是什么？", false)]
    [InlineData("检查这个 CVRAW 文件的格式", false)]
    public void BatchImageIntentDistinguishesActionsFromConcepts(string userText, bool expected)
    {
        Assert.Equal(expected, CopilotToolIntentPolicy.NeedsBatchImageProcessing(Request(userText)));
    }

    [Theory]
    [InlineData("创建一个 Python 脚本并执行它")]
    [InlineData(@"C:\Users\17917\Desktop\work 里面的 cvraw 文件，写一个 python 脚本，运行批量转换成 tif")]
    public void ScriptCreationExecutionContractRequiresWriteThenProcess(string userText)
    {
        var request = Request(
            userText,
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
        IReadOnlyList<string>? searchRoots = null,
        IReadOnlyList<string>? readableFiles = null,
        IReadOnlyList<CopilotContextItem>? contextItems = null)
    {
        return new CopilotAgentRequest
        {
            UserText = userText,
            Mode = CopilotAgentMode.Auto,
            WritableLocalRootPaths = writableRoots ?? Array.Empty<string>(),
            SearchRootPaths = searchRoots ?? Array.Empty<string>(),
            ReadableLocalFilePaths = readableFiles ?? Array.Empty<string>(),
            ContextItems = contextItems ?? Array.Empty<CopilotContextItem>(),
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
