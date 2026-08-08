using ColorVision.Copilot;
using System.IO;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotCodexCustomSubagentsTests
{
    [Fact]
    public void TrustedProjectAgentsOverrideCodexHomeWhileUntrustedProjectsAreIgnored()
    {
        var globalRoot = CreateTemporaryDirectory();
        var projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(globalRoot, "agents"));
            File.WriteAllText(
                Path.Combine(globalRoot, "agents", "reviewer.toml"),
                CreateAgentConfig(
                    "reviewer",
                    "Global review agent",
                    "Use the global review checklist.",
                    model: "global-model",
                    effort: "low") + "\n[mcp_servers.extra]\ncommand = \"ignored\"");
            File.WriteAllText(
                Path.Combine(globalRoot, "agents", "docs.toml"),
                CreateAgentConfig("docs", "Docs research", "Find exact public documentation."));

            var globalConfigPath = Path.Combine(globalRoot, "config.toml");
            File.WriteAllText(
                globalConfigPath,
                $"[projects.'{projectRoot}']\ntrust_level = \"trusted\"");
            var projectAgentsDirectory = Path.Combine(projectRoot, ".codex", "agents");
            Directory.CreateDirectory(projectAgentsDirectory);
            File.WriteAllText(
                Path.Combine(projectAgentsDirectory, "reviewer.toml"),
                CreateAgentConfig(
                    "reviewer",
                    "Project review agent",
                    "Use the trusted project review checklist.",
                    model: "project-model",
                    effort: "high"));

            var trusted = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.Equal(2, trusted.CustomSubagents.Count);
            var reviewer = Assert.Single(trusted.CustomSubagents, definition => definition.Name == "reviewer");
            Assert.Equal("Project review agent", reviewer.Description);
            Assert.Equal("Use the trusted project review checklist.", reviewer.DeveloperInstructions);
            Assert.Equal("project-model", reviewer.Model);
            Assert.Equal(CopilotCodexReasoningEffort.High, reviewer.ReasoningEffort);
            Assert.Equal(CopilotProjectInstructionConfigSources.TrustedProject, reviewer.Source);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.CodexHome,
                Assert.Single(trusted.CustomSubagents, definition => definition.Name == "docs").Source);

            File.WriteAllText(
                globalConfigPath,
                $"[projects.'{projectRoot}']\ntrust_level = \"untrusted\"");
            var untrusted = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.Equal(2, untrusted.CustomSubagents.Count);
            var globalReviewer = Assert.Single(untrusted.CustomSubagents, definition => definition.Name == "reviewer");
            Assert.Equal("Global review agent", globalReviewer.Description);
            Assert.Equal("global-model", globalReviewer.Model);
            Assert.True(globalReviewer.HasIgnoredSettings);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, globalReviewer.Source);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void SubmittedRequestFreezesAgentsAndPromptExposesOnlySafeDiscoveryMetadata()
    {
        var globalRoot = CreateTemporaryDirectory();
        var projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(globalRoot, "agents"));
            var agentPath = Path.Combine(globalRoot, "agents", "reviewer.toml");
            File.WriteAllText(
                agentPath,
                CreateAgentConfig(
                    "reviewer",
                    "First safe description",
                    "FIRST-PRIVATE-INSTRUCTION",
                    model: "first-model"));
            var activeDocumentPath = Path.Combine(projectRoot, "Feature.cs");
            File.WriteAllText(activeDocumentPath, "namespace Feature;");
            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);

            File.WriteAllText(
                agentPath,
                CreateAgentConfig(
                    "reviewer",
                    "Second safe description",
                    "SECOND-PRIVATE-INSTRUCTION",
                    model: "second-model"));

            var submittedPlan = CopilotAgentRequestFactory.Prepare(
                "Inspect Feature.cs with a specialist.",
                CopilotAgentMode.Auto,
                submittedContext);
            var refreshedContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            var refreshedPlan = CopilotAgentRequestFactory.Prepare(
                "Inspect Feature.cs with a specialist.",
                CopilotAgentMode.Auto,
                refreshedContext);
            var submittedRequest = CopilotAgentRequestFactory.Create(
                submittedPlan,
                CreateBuildInput());

            Assert.Equal("First safe description", Assert.Single(submittedPlan.CodexCustomSubagents).Description);
            Assert.Equal("first-model", Assert.Single(submittedRequest.CodexCustomSubagents).Model);
            Assert.Equal("Second safe description", Assert.Single(refreshedPlan.CodexCustomSubagents).Description);

            var prompt = new CopilotAgentContextBuilder().BuildPreparedUserMessageContent(
                submittedRequest,
                Array.Empty<CopilotToolResult>());
            Assert.Contains("reviewer: First safe description", prompt, StringComparison.Ordinal);
            Assert.Contains("fixed read-only capability boundary", prompt, StringComparison.Ordinal);
            Assert.DoesNotContain("FIRST-PRIVATE-INSTRUCTION", prompt, StringComparison.Ordinal);
            Assert.DoesNotContain(agentPath, prompt, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public async Task CustomAgentOverridesSpawnRuntimeWithoutWideningTheSelectedRole()
    {
        var runner = new RecordingSubagentRunner();
        var tool = new CopilotDelegateExploreTool(runner);
        var request = CreateParentRequest(
            new CopilotCodexCustomSubagentDefinition
            {
                Name = "reviewer",
                Description = "Review bounded workspace evidence.",
                DeveloperInstructions = "Prioritize authorization boundary defects.",
                Model = "agent-model",
                ReasoningEffort = CopilotCodexReasoningEffort.High,
                Source = CopilotProjectInstructionConfigSources.TrustedProject,
            });
        var arguments = new Dictionary<string, object?>
        {
            ["task"] = "Inspect the bounded workspace evidence.",
            ["agent"] = "reviewer",
            ["model"] = "spawn-model",
            ["reasoning_effort"] = "low",
        };
        Assert.True(tool.InputSchema.TryBind(arguments, out var input, out var bindError), bindError);
        var progress = new CopilotToolProgressContext();

        var result = await tool.ExecuteWithProgressAsync(
            request,
            input,
            progress,
            CancellationToken.None);

        Assert.Equal(1, runner.RunCount);
        var runRequest = Assert.IsType<CopilotSubagentRunRequest>(runner.LastRunRequest);
        Assert.Equal("reviewer", runRequest.Agent);
        var childRequest = CopilotSubagentRunner.CreateChildRequest(
            request,
            CopilotSubagentRoleCatalog.Default.GetRequired(CopilotSubagentRoleCatalog.ExploreRoleId),
            runRequest);
        Assert.Equal("agent-model", childRequest.Profile.Model);
        Assert.Equal(CopilotCodexReasoningEffort.High, childRequest.CodexReasoningEffort);
        Assert.Contains("Prioritize authorization boundary defects.", childRequest.RuntimeRoleInstructions, StringComparison.Ordinal);
        Assert.Contains("Custom-agent boundary", childRequest.RuntimeRoleInstructions, StringComparison.Ordinal);
        Assert.Empty(childRequest.WritableLocalRootPaths);
        Assert.Empty(childRequest.WritableLocalFilePaths);
        Assert.Empty(childRequest.ExternalMcpServers);
        Assert.Equal(CopilotAgentHarnessFeatures.None, childRequest.HarnessFeatures);

        var delegated = Assert.IsType<CopilotDelegatedRunUsage>(result.DelegatedRunUsage);
        Assert.Equal("reviewer", delegated.AgentName);
        Assert.Equal("agent-model", delegated.Model);
        Assert.Equal("high", delegated.ReasoningEffort);
        Assert.Equal("reviewer", progress.LatestSnapshot?.DelegatedRun?.AgentName);
        Assert.Contains("agent: reviewer", result.Content, StringComparison.Ordinal);

        var outcome = CreateOutcome(tool, request, result);
        using var formatted = JsonDocument.Parse(CopilotFrameworkToolResultFormatter.Format(outcome));
        Assert.Equal(
            "reviewer",
            formatted.RootElement.GetProperty("delegated_run").GetProperty("agent").GetString());
        var trace = CopilotAgentTraceEntry.FromResult(outcome.Execution, result);
        Assert.Equal(15, trace.SchemaVersion);
        Assert.Equal("reviewer", trace.DelegatedAgentName);
    }

    [Fact]
    public void CustomAgentModelWithoutEffortUsesTheSelectedModelDefault()
    {
        var request = CreateParentRequest(
            new CopilotCodexCustomSubagentDefinition
            {
                Name = "reviewer",
                Description = "Review evidence.",
                DeveloperInstructions = "Review the evidence.",
                Model = "different-model",
            });

        var child = CopilotSubagentRunner.CreateChildRequest(
            request,
            CopilotSubagentRoleCatalog.Default.GetRequired(CopilotSubagentRoleCatalog.ExploreRoleId),
            new CopilotSubagentRunRequest
            {
                RunId = "custom-model-default",
                Task = "Inspect bounded evidence.",
                Agent = "reviewer",
                RequestTokenBudget = 16_384,
            });

        Assert.Equal("different-model", child.Profile.Model);
        Assert.Equal(CopilotCodexReasoningEffort.Unspecified, child.CodexReasoningEffort);
    }

    [Fact]
    public async Task UnknownOrInjectedAgentNamesAreRejectedBeforeRunnerStarts()
    {
        var runner = new RecordingSubagentRunner();
        var tool = new CopilotDelegateScoutTool(runner);
        var request = CreateParentRequest(
            new CopilotCodexCustomSubagentDefinition
            {
                Name = "docs",
                Description = "Find public docs.",
                DeveloperInstructions = "Cite exact public docs.",
            });
        var unknown = await tool.ExecuteAsync(
            request,
            new CopilotAgentToolInput
            {
                Arguments = new Dictionary<string, object?>
                {
                    ["task"] = "Find public documentation.",
                    ["agent"] = "missing",
                },
            },
            CancellationToken.None);
        var invalidArguments = new Dictionary<string, object?>
        {
            ["task"] = "Find public documentation.",
            ["agent"] = "../docs",
        };

        Assert.True(tool.InputSchema.TryBind(invalidArguments, out _, out _));
        var invalid = await tool.ExecuteAsync(
            request,
            new CopilotAgentToolInput { Arguments = invalidArguments },
            CancellationToken.None);

        Assert.Equal(0, runner.RunCount);
        Assert.Equal(CopilotToolFailureKind.Validation, unknown.FailureKind);
        Assert.Contains("Available: docs", unknown.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(CopilotToolFailureKind.Validation, invalid.FailureKind);
        Assert.Contains("Argument 'agent'", invalid.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompletedRunResumeRequiresTheSameEffectiveRuntimeProfile()
    {
        var request = CreateParentRequest(
            new CopilotCodexCustomSubagentDefinition
            {
                Name = "reviewer",
                Description = "Review bounded workspace evidence.",
                DeveloperInstructions = "Review the evidence.",
                Model = "agent-model",
                ReasoningEffort = CopilotCodexReasoningEffort.High,
            });
        var coordinator = new CopilotSubagentCoordinator(request);
        var lease = await coordinator.TryAcquireAsync(
            CopilotSubagentRoleCatalog.ExploreRoleId,
            CancellationToken.None);
        Assert.NotNull(lease);
        var runId = lease!.RunId;
        using (lease)
        {
            coordinator.RecordCompleted(
                CopilotSubagentRoleCatalog.ExploreRoleId,
                runId,
                "reviewer",
                "agent-model",
                "high",
                CreateStructurallyValidCheckpoint());
            lease.Commit(0);
        }

        Assert.True(coordinator.TryResolveCompletedRun(
            CopilotSubagentRoleCatalog.ExploreRoleId,
            runId,
            "reviewer",
            "agent-model",
            "high",
            out var checkpoint,
            out var successFailureKind,
            out var successError));
        Assert.NotNull(checkpoint);
        Assert.Equal(CopilotToolFailureKind.None, successFailureKind);
        Assert.Empty(successError);

        Assert.False(coordinator.TryResolveCompletedRun(
            CopilotSubagentRoleCatalog.ExploreRoleId,
            runId,
            "docs",
            "agent-model",
            "high",
            out _,
            out var agentFailureKind,
            out var agentError));
        Assert.Equal(CopilotToolFailureKind.Validation, agentFailureKind);
        Assert.Contains("different agent/model/reasoning profile", agentError, StringComparison.Ordinal);

        Assert.False(coordinator.TryResolveCompletedRun(
            CopilotSubagentRoleCatalog.ExploreRoleId,
            runId,
            "reviewer",
            "different-model",
            "high",
            out _,
            out _,
            out _));
        Assert.False(coordinator.TryResolveCompletedRun(
            CopilotSubagentRoleCatalog.ExploreRoleId,
            runId,
            "reviewer",
            "agent-model",
            "low",
            out _,
            out _,
            out _));
    }

    [Fact]
    public async Task DelegateToolRejectsCrossAgentResumeBeforeStartingAnotherRunner()
    {
        var runner = new RecordingSubagentRunner(new CopilotSubagentResult
        {
            Answer = "Bounded result.",
            StopReason = CopilotAgentStopReason.Completed,
            HasSuccessfulEvidence = true,
            SessionCheckpoint = CreateStructurallyValidCheckpoint(),
        });
        var tool = new CopilotDelegateExploreTool(runner);
        var request = CreateParentRequest(
            new CopilotCodexCustomSubagentDefinition
            {
                Name = "reviewer",
                Description = "Review evidence.",
                DeveloperInstructions = "Review the evidence.",
                Model = "review-model",
                ReasoningEffort = CopilotCodexReasoningEffort.High,
            },
            new CopilotCodexCustomSubagentDefinition
            {
                Name = "docs",
                Description = "Document evidence.",
                DeveloperInstructions = "Document the evidence.",
                Model = "docs-model",
                ReasoningEffort = CopilotCodexReasoningEffort.Low,
            });
        var first = await tool.ExecuteAsync(
            request,
            new CopilotAgentToolInput
            {
                Arguments = new Dictionary<string, object?>
                {
                    ["task"] = "Review bounded evidence.",
                    ["agent"] = "reviewer",
                },
            },
            CancellationToken.None);
        var firstUsage = Assert.IsType<CopilotDelegatedRunUsage>(first.DelegatedRunUsage);

        var resumed = await tool.ExecuteAsync(
            request,
            new CopilotAgentToolInput
            {
                Arguments = new Dictionary<string, object?>
                {
                    ["task"] = "Continue the bounded investigation.",
                    ["agent"] = "docs",
                    ["resume_from"] = firstUsage.RunId,
                },
            },
            CancellationToken.None);

        Assert.True(first.Success);
        Assert.Equal(1, runner.RunCount);
        Assert.False(resumed.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, resumed.FailureKind);
        Assert.Contains("different agent/model/reasoning profile", resumed.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalDiagnosticsExposeSafeAgentMetadataWithoutInstructionBodiesOrPaths()
    {
        var privatePath = @"C:\private\.codex\agents\reviewer.toml";
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            CustomSubagents =
            [
                new CopilotCodexCustomSubagentDefinition
                {
                    Name = "reviewer",
                    Description = "Review bounded workspace evidence.",
                    DeveloperInstructions = "PRIVATE-INSTRUCTION-BODY",
                    Model = "review-model",
                    ReasoningEffort = CopilotCodexReasoningEffort.High,
                    Source = CopilotProjectInstructionConfigSources.TrustedProject,
                    SourceFilePath = privatePath,
                    HasIgnoredSettings = true,
                },
            ],
        };

        var memoryReport = CopilotProjectInstructionDiagnostics.Format(
            new CopilotProjectInstructionSnapshot(
                string.Empty,
                string.Empty,
                string.Empty,
                options,
                Array.Empty<CopilotProjectInstructionDocument>()),
            hasActiveAgentRun: false);
        var debugReport = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                ComposerMode = CopilotAgentMode.Code,
                CodexConfigOptions = options,
            });

        foreach (var report in new[] { memoryReport, debugReport })
        {
            Assert.Contains("Codex custom agents：1", report, StringComparison.Ordinal);
            Assert.Contains("reviewer · Review bounded workspace evidence.", report, StringComparison.Ordinal);
            Assert.Contains("来源 受信项目", report, StringComparison.Ordinal);
            Assert.Contains("model review-model · reasoning high", report, StringComparison.Ordinal);
            Assert.Contains("未支持设置已忽略", report, StringComparison.Ordinal);
            Assert.DoesNotContain("PRIVATE-INSTRUCTION-BODY", report, StringComparison.Ordinal);
            Assert.DoesNotContain(privatePath, report, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static CopilotAgentRequest CreateParentRequest(
        params CopilotCodexCustomSubagentDefinition[] definitions) => new()
        {
            ConversationId = "custom-subagent-" + Guid.NewGuid().ToString("N"),
            UserText = "Delegate a bounded investigation.",
            TaskIntentText = "Delegate a bounded investigation.",
            Profile = CreateProfile(),
            CodexCustomSubagents = definitions,
            CodexReasoningEffort = CopilotCodexReasoningEffort.Medium,
        };

    private static CopilotAgentRequestBuildInput CreateBuildInput() => new()
    {
        ConversationId = "custom-subagent-snapshot",
        Profile = CreateProfile(),
        AgentDefaults = new CopilotAgentDefaultsConfig(),
    };

    private static CopilotProfileConfig CreateProfile() => new()
    {
        VendorType = CopilotVendorType.Custom,
        ProviderType = CopilotProviderType.OpenAICompatible,
        ApiKey = "test-key",
        BaseUrl = "https://example.test/v1",
        Model = "parent-model",
        MaxTokens = 4_096,
    };

    private static string CreateAgentConfig(
        string name,
        string description,
        string developerInstructions,
        string model = "",
        string effort = "")
    {
        var lines = new List<string>
        {
            $"name = \"{name}\"",
            $"description = \"{description}\"",
            $"developer_instructions = \"{developerInstructions}\"",
        };
        if (model.Length > 0)
            lines.Add($"model = \"{model}\"");
        if (effort.Length > 0)
            lines.Add($"model_reasoning_effort = \"{effort}\"");
        return string.Join('\n', lines);
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ColorVisionCopilotCustomSubagentTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static CopilotAgentSessionCheckpoint CreateStructurallyValidCheckpoint() => new()
    {
        ProfileKey = "test-profile",
        SerializedSessionJson = "{}",
    };

    private static CopilotToolExecutionOutcome CreateOutcome(
        ICopilotTool tool,
        CopilotAgentRequest request,
        CopilotToolResult result) => new()
        {
            Invocation = new CopilotToolInvocation
            {
                CallId = "custom-subagent-call",
                Round = 1,
                Tool = tool,
                AgentRequest = request,
                ToolCall = new CopilotToolCall { ToolName = tool.Name },
            },
            Result = result,
            Execution = new CopilotToolExecutionInfo
            {
                CallId = "custom-subagent-call",
                Round = 1,
                ToolName = tool.Name,
                State = CopilotToolExecutionState.Failed,
                FailureKind = result.FailureKind,
            },
        };

    private sealed class RecordingSubagentRunner : ICopilotSubagentRunner
    {
        private readonly CopilotSubagentResult _result;

        public RecordingSubagentRunner(CopilotSubagentResult? result = null)
        {
            _result = result ?? new CopilotSubagentResult();
        }

        public int RunCount { get; private set; }

        public CopilotSubagentRunRequest? LastRunRequest { get; private set; }

        public Task<CopilotSubagentResult> RunAsync(
            CopilotAgentRequest parentRequest,
            CopilotSubagentRoleDescriptor role,
            CopilotSubagentRunRequest runRequest,
            CancellationToken cancellationToken)
        {
            RunCount++;
            LastRunRequest = runRequest;
            return Task.FromResult(_result);
        }
    }
}
