using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotCodexConfiguredCommandHookTests
{
    [Fact]
    public void GlobalHooksJsonPrefersWindowsCommandAndMatchesCodexShellAlias()
    {
        var codexHome = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(codexHome, "hooks.json"),
                """
                {
                  "hooks": {
                    "PreToolUse": [
                      {
                        "matcher": "^Bash$",
                        "hooks": [
                          {
                            "type": "command",
                            "command": "fallback-command",
                            "commandWindows": "windows-command",
                            "timeout": 7,
                            "statusMessage": "Checking shell policy"
                          }
                        ]
                      }
                    ]
                  }
                }
                """);

            var options = CopilotProjectInstructionDiscoveryConfig.Load(codexHome);

            var hook = Assert.Single(options.ConfiguredCommandHooks);
            Assert.Equal("windows-command", hook.Command);
            Assert.Equal(7, hook.TimeoutSeconds);
            Assert.Equal("Checking shell policy", hook.StatusMessage);
            Assert.Equal(CopilotCodexConfiguredHookEvent.PreToolUse, hook.Event);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, hook.Source);
            Assert.True(hook.Matches("RunShellCommand"));
            Assert.False(hook.Matches("ReadLocalFile"));
            Assert.Equal(
                "Bash",
                CopilotCodexConfiguredHookToolNames.GetCanonicalName("RunShellCommand"));
            Assert.Equal([Path.Combine(codexHome, "hooks.json")], options.AppliedHookFilePaths);
            Assert.Empty(options.ConfiguredHookIssues);
        }
        finally
        {
            Directory.Delete(codexHome, recursive: true);
        }
    }

    [Fact]
    public void GlobalInlineTomlHookLoadsWindowsCommandAndPreservesQuotedComment()
    {
        var codexHome = CreateTemporaryDirectory();
        try
        {
            var configPath = Path.Combine(codexHome, "config.toml");
            File.WriteAllText(
                configPath,
                """
                [[hooks.PreToolUse]]
                matcher = '^Bash$'

                [[hooks.PreToolUse.hooks]]
                type = "command"
                command = "fallback-command"
                command_windows = 'windows # command'
                timeout = 7
                async = true
                statusMessage = "Checking inline shell policy"
                """);

            var options = CopilotProjectInstructionDiscoveryConfig.Load(codexHome);

            var hook = Assert.Single(options.ConfiguredCommandHooks);
            Assert.Equal("windows # command", hook.Command);
            Assert.Equal(7, hook.TimeoutSeconds);
            Assert.Equal("Checking inline shell policy", hook.StatusMessage);
            Assert.Equal(CopilotToolExecutionHookMode.Async, hook.ExecutionMode);
            Assert.Equal(CopilotCodexConfiguredHookEvent.PreToolUse, hook.Event);
            Assert.True(hook.Matches("RunShellCommand"));
            Assert.Equal([configPath], options.AppliedHookFilePaths);
            Assert.Empty(options.ConfiguredHookIssues);
        }
        finally
        {
            Directory.Delete(codexHome, recursive: true);
        }
    }

    [Fact]
    public void ProjectInlineTomlHookLoadsOnlyFromTrustedProject()
    {
        var codexHome = CreateTemporaryDirectory();
        var projectRoot = CreateTemporaryDirectory();
        try
        {
            var projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            var projectConfigPath = Path.Combine(projectConfigDirectory, "config.toml");
            File.WriteAllText(
                projectConfigPath,
                """
                [[hooks.PostToolUse]]
                matcher = "^ReadLocalFile$"

                [[hooks.PostToolUse.hooks]]
                type = "command"
                commandWindows = "project-inline-audit"
                """);
            var globalConfigPath = Path.Combine(codexHome, "config.toml");
            File.WriteAllText(
                globalConfigPath,
                $"[projects.'{projectRoot}']\ntrust_level = \"untrusted\"");

            var untrusted = CopilotProjectInstructionDiscoveryConfig.Load(codexHome, projectRoot);

            Assert.Empty(untrusted.ConfiguredCommandHooks);
            Assert.Empty(untrusted.AppliedHookFilePaths);

            File.WriteAllText(
                globalConfigPath,
                $"[projects.'{projectRoot}']\ntrust_level = \"trusted\"");
            var trusted = CopilotProjectInstructionDiscoveryConfig.Load(codexHome, projectRoot);

            var hook = Assert.Single(trusted.ConfiguredCommandHooks);
            Assert.Equal("project-inline-audit", hook.Command);
            Assert.Equal(CopilotProjectInstructionConfigSources.TrustedProject, hook.Source);
            Assert.Empty(trusted.AppliedProjectConfigFilePaths);
            Assert.Equal([projectConfigPath], trusted.AppliedHookFilePaths);
        }
        finally
        {
            Directory.Delete(codexHome, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void HooksJsonAndInlineTomlHooksBothLoadAndSurfaceLayerDiagnostic()
    {
        var codexHome = CreateTemporaryDirectory();
        try
        {
            var hooksPath = Path.Combine(codexHome, "hooks.json");
            var configPath = Path.Combine(codexHome, "config.toml");
            File.WriteAllText(
                hooksPath,
                CreateSingleHookJson("PreToolUse", "^ReadLocalFile$", "json-hook"));
            File.WriteAllText(
                configPath,
                """
                [[hooks.PreToolUse]]
                matcher = "^ReadLocalFile$"

                [[hooks.PreToolUse.hooks]]
                type = "command"
                commandWindows = "inline-hook"
                """);

            var options = CopilotProjectInstructionDiscoveryConfig.Load(codexHome);

            Assert.Equal(["json-hook", "inline-hook"], options.ConfiguredCommandHooks.Select(hook => hook.Command));
            Assert.Equal([hooksPath, configPath], options.AppliedHookFilePaths);
            Assert.Contains(
                options.ConfiguredHookIssues,
                issue => issue.SourceFilePath == configPath
                    && issue.Message.Contains("both hooks.json and config.toml", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(codexHome, recursive: true);
        }
    }

    [Fact]
    public void OrphanInlineTomlHandlerFailsClosedAndSurfacesDiagnostic()
    {
        var codexHome = CreateTemporaryDirectory();
        try
        {
            var configPath = Path.Combine(codexHome, "config.toml");
            File.WriteAllText(
                configPath,
                """
                [[hooks.PermissionRequest.hooks]]
                type = "command"
                commandWindows = "must-not-run"
                """);

            var options = CopilotProjectInstructionDiscoveryConfig.Load(codexHome);

            Assert.Empty(options.ConfiguredCommandHooks);
            Assert.Equal([configPath], options.AppliedHookFilePaths);
            Assert.Contains(
                options.ConfiguredHookIssues,
                issue => issue.Message.Contains("without a preceding matcher group", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(codexHome, recursive: true);
        }
    }

    [Fact]
    public void ProjectHooksJsonLoadsOnlyFromTrustedProjectWithoutConfigToml()
    {
        var codexHome = CreateTemporaryDirectory();
        var projectRoot = CreateTemporaryDirectory();
        try
        {
            var projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "hooks.json"),
                CreateSingleHookJson("PostToolUse", "^ReadLocalFile$", "project-audit"));
            File.WriteAllText(
                Path.Combine(codexHome, "config.toml"),
                $"[projects.'{projectRoot}']\ntrust_level = \"untrusted\"");

            var untrusted = CopilotProjectInstructionDiscoveryConfig.Load(codexHome, projectRoot);

            Assert.Empty(untrusted.ConfiguredCommandHooks);
            Assert.Empty(untrusted.AppliedHookFilePaths);

            File.WriteAllText(
                Path.Combine(codexHome, "config.toml"),
                $"[projects.'{projectRoot}']\ntrust_level = \"trusted\"");
            var trusted = CopilotProjectInstructionDiscoveryConfig.Load(codexHome, projectRoot);

            var hook = Assert.Single(trusted.ConfiguredCommandHooks);
            Assert.Equal("project-audit", hook.Command);
            Assert.Equal(CopilotProjectInstructionConfigSources.TrustedProject, hook.Source);
            Assert.Empty(trusted.AppliedProjectConfigFilePaths);
            Assert.Equal(
                [Path.Combine(projectConfigDirectory, "hooks.json")],
                trusted.AppliedHookFilePaths);
        }
        finally
        {
            Directory.Delete(codexHome, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public async Task PermissionAllowOutputCannotBypassNativeApproval()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var runner = new RecordingCommandHookRunner(new CopilotCodexCommandHookProcessResult(
                0,
                false,
                """
                {"hookSpecificOutput":{"hookEventName":"PermissionRequest","decision":{"behavior":"allow"}}}
                """,
                string.Empty));
            var request = CreateRequest(
                workspace,
                CreateDefinition(CopilotCodexConfiguredHookEvent.PermissionRequest, "^ProtectedTool$"));
            var invocation = CreateInvocation(new ProtectedTool(), request, "permission-hook-call");

            var outcome = await CreateExecutor(runner).EvaluatePermissionRequestAsync(
                invocation,
                CancellationToken.None);

            Assert.True(outcome.Decision.ShouldPrompt);
            var run = Assert.Single(outcome.HookRuns);
            Assert.Equal(CopilotToolExecutionHookPhase.PermissionRequest, run.Phase);
            Assert.Equal(CopilotToolExecutionHookState.Completed, run.State);
            var call = Assert.Single(runner.Calls);
            using var input = JsonDocument.Parse(call.StandardInput);
            Assert.Equal("PermissionRequest", input.RootElement.GetProperty("hook_event_name").GetString());
            Assert.Equal("ProtectedTool", input.RootElement.GetProperty("tool_name").GetString());
            Assert.Equal("exact value", input.RootElement.GetProperty("tool_input").GetProperty("query").GetString());
            Assert.False(input.RootElement.TryGetProperty("tool_use_id", out _));
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task PermissionDenyOutputBlocksBeforeNativeApproval()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var runner = new RecordingCommandHookRunner(new CopilotCodexCommandHookProcessResult(
                0,
                false,
                """
                {"hookSpecificOutput":{"hookEventName":"PermissionRequest","decision":{"behavior":"deny","message":"operator policy denied"}}}
                """,
                string.Empty));
            var request = CreateRequest(
                workspace,
                CreateDefinition(CopilotCodexConfiguredHookEvent.PermissionRequest, "^ProtectedTool$"));

            var outcome = await CreateExecutor(runner).EvaluatePermissionRequestAsync(
                CreateInvocation(new ProtectedTool(), request, "permission-deny-call"),
                CancellationToken.None);

            Assert.False(outcome.Decision.ShouldPrompt);
            Assert.Equal("configured_hook_denied", outcome.Decision.FailureCode);
            Assert.Contains("operator policy denied", outcome.Decision.Reason, StringComparison.Ordinal);
            var run = Assert.Single(outcome.HookRuns);
            Assert.Equal(CopilotToolExecutionHookState.Denied, run.State);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task PreToolExitTwoFailsClosedBeforeToolExecution()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var runner = new RecordingCommandHookRunner(new CopilotCodexCommandHookProcessResult(
                2,
                false,
                string.Empty,
                "blocked by configured policy"));
            var tool = new RecordingReadTool();
            var request = CreateRequest(
                workspace,
                CreateDefinition(CopilotCodexConfiguredHookEvent.PreToolUse, "^RecordingReadTool$"));

            var outcome = await CreateExecutor(runner).ExecuteAsync(
                CreateInvocation(tool, request, "pre-hook-call"),
                _ => { },
                CancellationToken.None);

            Assert.False(outcome.Result.Success);
            Assert.Equal("configured_hook_denied", outcome.Result.FailureCode);
            Assert.Contains("blocked by configured policy", outcome.Result.ErrorMessage, StringComparison.Ordinal);
            Assert.Equal(0, tool.ExecutionCount);
            Assert.Contains(outcome.HookRuns, run =>
                run.SourceId.StartsWith("codex-config:", StringComparison.Ordinal)
                && run.Phase == CopilotToolExecutionHookPhase.BeforeExecute
                && run.State == CopilotToolExecutionHookState.Denied);
            using var input = JsonDocument.Parse(Assert.Single(runner.Calls).StandardInput);
            Assert.Equal("pre-hook-call", input.RootElement.GetProperty("tool_use_id").GetString());
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task PreToolInputRewriteFailsClosedAgainstApprovalSnapshot()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var runner = new RecordingCommandHookRunner(new CopilotCodexCommandHookProcessResult(
                0,
                false,
                """
                {"hookSpecificOutput":{"hookEventName":"PreToolUse","updatedInput":{"query":"changed"}}}
                """,
                string.Empty));
            var tool = new RecordingReadTool();
            var request = CreateRequest(
                workspace,
                CreateDefinition(CopilotCodexConfiguredHookEvent.PreToolUse, "^RecordingReadTool$"));

            var outcome = await CreateExecutor(runner).ExecuteAsync(
                CreateInvocation(tool, request, "rewrite-hook-call"),
                _ => { },
                CancellationToken.None);

            Assert.False(outcome.Result.Success);
            Assert.Equal("configured_hook_input_rewrite_unsupported", outcome.Result.FailureCode);
            Assert.Equal(0, tool.ExecutionCount);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task PreToolAskDecisionFailsClosedWithoutBoundNativeApproval()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var runner = new RecordingCommandHookRunner(new CopilotCodexCommandHookProcessResult(
                0,
                false,
                """
                {"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"ask","permissionDecisionReason":"operator must approve"}}
                """,
                string.Empty));
            var tool = new RecordingReadTool();
            var request = CreateRequest(
                workspace,
                CreateDefinition(CopilotCodexConfiguredHookEvent.PreToolUse, "^RecordingReadTool$"));

            var outcome = await CreateExecutor(runner).ExecuteAsync(
                CreateInvocation(tool, request, "ask-hook-call"),
                _ => { },
                CancellationToken.None);

            Assert.False(outcome.Result.Success);
            Assert.Equal("configured_hook_approval_required", outcome.Result.FailureCode);
            Assert.Contains("operator must approve", outcome.Result.ErrorMessage, StringComparison.Ordinal);
            Assert.Equal(0, tool.ExecutionCount);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task PreAndPostHooksRunOnlyInTheirDeclaredPhasesWhenPluginsAreDisabled()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var runner = new RecordingCommandHookRunner(
                new CopilotCodexCommandHookProcessResult(0, false, string.Empty, string.Empty),
                new CopilotCodexCommandHookProcessResult(0, false, string.Empty, string.Empty));
            var tool = new RecordingReadTool();
            var request = CreateRequest(
                workspace,
                codexPluginsEnabled: false,
                CreateDefinition(CopilotCodexConfiguredHookEvent.PreToolUse, "^RecordingReadTool$", 0),
                CreateDefinition(CopilotCodexConfiguredHookEvent.PostToolUse, "^RecordingReadTool$", 1));

            var outcome = await CreateExecutor(runner).ExecuteAsync(
                CreateInvocation(tool, request, "lifecycle-hook-call"),
                _ => { },
                CancellationToken.None);

            Assert.True(outcome.Result.Success);
            Assert.Equal(1, tool.ExecutionCount);
            Assert.Equal(
                [CopilotCodexConfiguredHookEvent.PreToolUse, CopilotCodexConfiguredHookEvent.PostToolUse],
                runner.Calls.Select(call => call.Definition.Event));
            Assert.Equal(
                [CopilotToolExecutionHookPhase.BeforeExecute, CopilotToolExecutionHookPhase.AfterExecute],
                outcome.HookRuns
                    .Where(run => run.SourceId.StartsWith("codex-config:", StringComparison.Ordinal))
                    .Select(run => run.Phase));
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task ShellHookReceivesCanonicalBashToolName()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var runner = new RecordingCommandHookRunner(
                new CopilotCodexCommandHookProcessResult(0, false, string.Empty, string.Empty));
            var tool = new RecordingReadTool("RunShellCommand");
            var request = CreateRequest(
                workspace,
                CreateDefinition(CopilotCodexConfiguredHookEvent.PreToolUse, "^Bash$"));

            var outcome = await CreateExecutor(runner).ExecuteAsync(
                CreateInvocation(tool, request, "bash-hook-call"),
                _ => { },
                CancellationToken.None);

            Assert.True(outcome.Result.Success);
            using var input = JsonDocument.Parse(Assert.Single(runner.Calls).StandardInput);
            Assert.Equal("Bash", input.RootElement.GetProperty("tool_name").GetString());
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task PostToolBlockIsRecordedAsFeedbackWithoutUndoingCompletedTool()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var runner = new RecordingCommandHookRunner(new CopilotCodexCommandHookProcessResult(
                0,
                false,
                """
                {"decision":"block","reason":"review generated output"}
                """,
                string.Empty));
            var tool = new RecordingReadTool();
            var events = new List<CopilotAgentEvent>();
            var request = CreateRequest(
                workspace,
                CreateDefinition(CopilotCodexConfiguredHookEvent.PostToolUse, "^RecordingReadTool$"));

            var outcome = await CreateExecutor(runner).ExecuteAsync(
                CreateInvocation(tool, request, "post-feedback-call"),
                events.Add,
                CancellationToken.None);

            Assert.True(outcome.Result.Success);
            Assert.Equal("Recording read tool completed.", outcome.Result.Summary);
            Assert.Equal(1, tool.ExecutionCount);
            Assert.Equal("Recording read tool completed.", outcome.StepRecord.Observation.Summary);
            Assert.Equal("PostToolUse hook feedback.", outcome.StepRecord.EffectiveModelObservation.Summary);
            Assert.Equal("review generated output", outcome.StepRecord.EffectiveModelObservation.Content);
            using var formatted = JsonDocument.Parse(CopilotFrameworkToolResultFormatter.Format(outcome));
            Assert.Equal(
                "PostToolUse hook feedback.",
                formatted.RootElement.GetProperty("summary").GetString());
            Assert.Equal(
                "review generated output",
                formatted.RootElement.GetProperty("content").GetString());
            var modelContext = new CopilotAgentContextBuilder().BuildObservationSummary(
                [outcome.StepRecord],
                maxSteps: 4,
                maxContentChars: 2_000,
                includeContent: true);
            Assert.Contains("review generated output", modelContext, StringComparison.Ordinal);
            Assert.DoesNotContain("Recording read tool completed.", modelContext, StringComparison.Ordinal);
            var terminal = Assert.Single(events, item => item.Type == CopilotAgentEventType.ToolResult);
            Assert.Equal("Recording read tool completed.", terminal.ToolResult?.Summary);
            Assert.Contains(outcome.HookRuns, run =>
                run.SourceId.StartsWith("codex-config:", StringComparison.Ordinal)
                && run.Phase == CopilotToolExecutionHookPhase.AfterExecute
                && run.State == CopilotToolExecutionHookState.Completed
                && run.FailureCode.Length == 0);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task PostToolHookDoesNotRunWhenPreToolHookDeniedExecution()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var runner = new RecordingCommandHookRunner(new CopilotCodexCommandHookProcessResult(
                0,
                false,
                """
                {"decision":"block","reason":"repository policy denied execution"}
                """,
                string.Empty));
            var tool = new RecordingReadTool();
            var request = CreateRequest(
                workspace,
                CreateDefinition(CopilotCodexConfiguredHookEvent.PreToolUse, "^RecordingReadTool$", 0),
                CreateDefinition(CopilotCodexConfiguredHookEvent.PostToolUse, "^RecordingReadTool$", 1));

            var outcome = await CreateExecutor(runner).ExecuteAsync(
                CreateInvocation(tool, request, "pre-denied-post-hook-call"),
                _ => { },
                CancellationToken.None);

            Assert.False(outcome.Result.Success);
            Assert.Equal("configured_hook_denied", outcome.Result.FailureCode);
            Assert.Equal(0, tool.ExecutionCount);
            Assert.Single(runner.Calls);
            Assert.DoesNotContain(outcome.HookRuns, run =>
                run.SourceId.StartsWith("codex-config:", StringComparison.Ordinal)
                && run.Phase == CopilotToolExecutionHookPhase.AfterExecute);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task PostToolExitTwoReturnsRedactedFeedbackToTheModel()
    {
        const string secret = "hook-secret-value";
        var workspace = CreateTemporaryDirectory();
        try
        {
            var runner = new RecordingCommandHookRunner(new CopilotCodexCommandHookProcessResult(
                2,
                false,
                string.Empty,
                "review this output; api_key=" + secret));
            var tool = new RecordingReadTool();
            var request = CreateRequest(
                workspace,
                CreateDefinition(CopilotCodexConfiguredHookEvent.PostToolUse, "^RecordingReadTool$"));

            var outcome = await CreateExecutor(runner).ExecuteAsync(
                CreateInvocation(tool, request, "post-exit-two-feedback-call"),
                _ => { },
                CancellationToken.None);

            Assert.True(outcome.Result.Success);
            Assert.Equal("Recording read tool completed.", outcome.Result.Summary);
            Assert.Contains("api_key=<redacted>", outcome.StepRecord.EffectiveModelObservation.Content, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, outcome.StepRecord.EffectiveModelObservation.Content, StringComparison.Ordinal);
            Assert.DoesNotContain(
                secret,
                CopilotFrameworkToolResultFormatter.Format(outcome),
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task FeaturesHooksFalseSuppressesConfiguredCommandHooks()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var runner = new RecordingCommandHookRunner();
            var tool = new RecordingReadTool();
            var request = new CopilotAgentRequest
            {
                ConversationId = "configured-hook-session",
                TaskId = "configured-hook-turn",
                WorkspacePath = workspace,
                Mode = CopilotAgentMode.Code,
                UserText = "Run the configured hook test.",
                TaskIntentText = "Run the configured hook test.",
                CodexHooksEnabled = false,
                CodexPluginsEnabled = true,
                CodexCommandHooks =
                [
                    CreateDefinition(CopilotCodexConfiguredHookEvent.PreToolUse, "^RecordingReadTool$"),
                ],
            };

            var outcome = await CreateExecutor(runner).ExecuteAsync(
                CreateInvocation(tool, request, "disabled-hook-call"),
                _ => { },
                CancellationToken.None);

            Assert.True(outcome.Result.Success);
            Assert.Equal(1, tool.ExecutionCount);
            Assert.Empty(runner.Calls);
            Assert.DoesNotContain(outcome.HookRuns, run =>
                run.SourceId.StartsWith("codex-config:", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public void HookSurfaceIncludesCommandHooksIndependentlyOfPluginsAndTracksChanges()
    {
        var executor = new CopilotToolExecutor(Array.Empty<ICopilotToolExecutionHook>());
        var original = CreateDefinition(CopilotCodexConfiguredHookEvent.PreToolUse, "^ReadLocalFile$");
        var changed = original with
        {
            Command = "changed-command",
            ConfigurationFingerprint = new string('b', 64),
        };

        var enabled = executor.GetHookSurfaceSnapshot(true, false, [original]);
        var changedSurface = executor.GetHookSurfaceSnapshot(true, false, [changed]);
        var disabled = executor.GetHookSurfaceSnapshot(false, true, [original]);

        Assert.True(enabled.IsStructurallyValid());
        Assert.Contains(enabled.Entries, entry => entry.SourceId == original.SourceId);
        Assert.DoesNotContain(disabled.Entries, entry => entry.SourceId == original.SourceId);
        Assert.NotEqual(enabled.Fingerprint, changedSurface.Fingerprint);
    }

    [Fact]
    public async Task ShellProcessRunnerWritesConfiguredStandardInputAndClosesTheStream()
    {
        var workspace = CreateTemporaryDirectory();
        try
        {
            var executable = CopilotShellCommandService.FindTrustedShellExecutable(
                CopilotShellKind.CommandPrompt);
            Assert.False(string.IsNullOrWhiteSpace(executable));
            var result = await new CopilotShellProcessRunner().RunAsync(
                new CopilotShellProcessCommand(
                    CopilotShellKind.CommandPrompt,
                    executable!,
                    CopilotShellCommandService.BuildArguments(CopilotShellKind.CommandPrompt, "more"),
                    workspace,
                    TimeSpan.FromSeconds(5))
                {
                    StandardInput = "hook-payload" + Environment.NewLine,
                },
                CancellationToken.None);

            Assert.False(result.TimedOut);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("hook-payload", result.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    private static CopilotToolExecutor CreateExecutor(ICopilotCodexCommandHookRunner runner) =>
        new(
            Array.Empty<ICopilotToolExecutionHook>(),
            utcNow: null,
            hookPhaseTimeout: TimeSpan.FromSeconds(2),
            progressInterval: TimeSpan.FromSeconds(1),
            codexCommandHookRunner: runner);

    private static CopilotAgentRequest CreateRequest(
        string workspace,
        params CopilotCodexCommandHookDefinition[] definitions) =>
        CreateRequest(workspace, codexPluginsEnabled: true, definitions);

    private static CopilotAgentRequest CreateRequest(
        string workspace,
        bool codexPluginsEnabled,
        params CopilotCodexCommandHookDefinition[] definitions) =>
        new()
        {
            ConversationId = "configured-hook-session",
            TaskId = "configured-hook-turn",
            WorkspacePath = workspace,
            Mode = CopilotAgentMode.Code,
            UserText = "Run the configured hook test.",
            TaskIntentText = "Run the configured hook test.",
            CodexHooksEnabled = true,
            CodexPluginsEnabled = codexPluginsEnabled,
            CodexCommandHooks = definitions,
        };

    private static CopilotCodexCommandHookDefinition CreateDefinition(
        CopilotCodexConfiguredHookEvent hookEvent,
        string matcher,
        int order = 0)
    {
        var fingerprint = new string((char)('a' + order), 64);
        return new CopilotCodexCommandHookDefinition(
            "codex-config:" + fingerprint[..32],
            Path.GetFullPath(Path.Combine(Path.GetTempPath(), "configured-hook-tests", "hooks.json")),
            CopilotProjectInstructionConfigSources.CodexHome,
            hookEvent,
            matcher,
            "test-command",
            5,
            string.Empty,
            CopilotToolExecutionHookMode.Sync,
            order,
            fingerprint);
    }

    private static CopilotToolInvocation CreateInvocation(
        ICopilotTool tool,
        CopilotAgentRequest request,
        string callId) =>
        new()
        {
            CallId = callId,
            RuntimeName = "configured-command-hook-test",
            Tool = tool,
            AgentRequest = request,
            ToolInput = new CopilotAgentToolInput
            {
                Query = "exact value",
                Arguments = new Dictionary<string, object?>
                {
                    ["query"] = "exact value",
                },
            },
        };

    private static string CreateSingleHookJson(string eventName, string matcher, string command) =>
        JsonSerializer.Serialize(new
        {
            hooks = new Dictionary<string, object?>
            {
                [eventName] = new[]
                {
                    new
                    {
                        matcher,
                        hooks = new[]
                        {
                            new { type = "command", commandWindows = command },
                        },
                    },
                },
            },
        });

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"copilot-configured-hooks-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class RecordingCommandHookRunner(params CopilotCodexCommandHookProcessResult[] results)
        : ICopilotCodexCommandHookRunner
    {
        private readonly Queue<CopilotCodexCommandHookProcessResult> _results = new(results);

        public List<CommandHookCall> Calls { get; } = [];

        public Task<CopilotCodexCommandHookProcessResult> RunAsync(
            CopilotCodexCommandHookDefinition definition,
            CopilotAgentRequest request,
            string standardInput,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add(new CommandHookCall(definition, standardInput));
            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed record CommandHookCall(
        CopilotCodexCommandHookDefinition Definition,
        string StandardInput);

    private sealed class RecordingReadTool(string name = "RecordingReadTool") : ICopilotTool
    {
        public string Name => name;

        public string Description => "Records a read-only execution.";

        public CopilotToolCapabilityDescriptor Capability { get; } =
            CopilotToolCapabilityDescriptor.ReadOnly();

        public int ExecutionCount { get; private set; }

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExecutionCount++;
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = "Recording read tool completed.",
            });
        }
    }

    private sealed class ProtectedTool : ICopilotFrameworkApprovedTool
    {
        public string Name => "ProtectedTool";

        public string Description => "Requires native approval.";

        public CopilotToolCapabilityDescriptor Capability { get; } =
            CopilotToolCapabilityDescriptor.ProtectedWrite(CopilotToolIdempotency.NonIdempotent);

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Native approval is required.");

        public Task<CopilotToolResult> ExecuteApprovedAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = "Protected tool completed.",
            });
    }
}
