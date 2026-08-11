using ColorVision.Copilot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexShellEnvironmentPolicyTests
{
    [Fact]
    public void TrustedLayersMergeAndFreezePolicyIntoSubmittedRequest()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                [shell_environment_policy]
                inherit = "all"

                [shell_environment_policy.filters]
                "drop_*" = "include"

                [shell_environment_policy.set]
                CV_LOWER = "lower"
                CV_PADDED = "  padded  "

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            string projectConfigPath = Path.Combine(projectConfigDirectory, "config.toml");
            File.WriteAllText(
                projectConfigPath,
                """
                [shell_environment_policy]
                inherit = "core"
                ignore_default_excludes = false

                [shell_environment_policy.filters]
                "DROP_*" = "exclude"
                "PATH" = "include"

                [shell_environment_policy.set]
                CV_UPPER = "upper"
                """);

            var context = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            var plan = CopilotAgentRequestFactory.Prepare(
                "Inspect the shell environment.",
                CopilotAgentMode.Code,
                context);
            var request = CopilotAgentRequestFactory.Create(
                plan,
                new CopilotAgentRequestBuildInput
                {
                    ConversationId = "frozen-conversation",
                    Profile = CopilotProfileConfig.CreateDefault(),
                    AgentDefaults = new CopilotAgentDefaultsConfig(),
                });
            File.WriteAllText(
                projectConfigPath,
                "[shell_environment_policy]\ninherit = \"none\"");

            var configured = context.ProjectInstructionDiscoveryOptions;
            Assert.True(configured.HasShellEnvironmentPolicyOverride);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.CodexHome
                    | CopilotProjectInstructionConfigSources.TrustedProject,
                configured.ShellEnvironmentPolicySources);
            Assert.Equal(CopilotCodexShellEnvironmentInherit.Core, request.CodexShellEnvironmentPolicy.Inherit);
            Assert.False(request.CodexShellEnvironmentPolicy.IgnoreDefaultExcludes);
            Assert.Equal("lower", request.CodexShellEnvironmentPolicy.Set["CV_LOWER"]);
            Assert.Equal("  padded  ", request.CodexShellEnvironmentPolicy.Set["CV_PADDED"]);
            Assert.Equal("upper", request.CodexShellEnvironmentPolicy.Set["CV_UPPER"]);
            Assert.Contains("DROP_*", request.CodexShellEnvironmentPolicy.Exclude);
            Assert.Contains("PATH", request.CodexShellEnvironmentPolicy.IncludeOnly);
            Assert.DoesNotContain(
                request.CodexShellEnvironmentPolicy.IncludeOnly,
                pattern => string.Equals(pattern, "drop_*", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(
                CopilotCodexShellEnvironmentInherit.None,
                CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot)
                    .ConfiguredShellEnvironmentPolicy.Inherit);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void LegacyListsApplyButUntrustedProjectPolicyIsIgnored()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                [shell_environment_policy]
                inherit = "core"
                exclude = ["GLOBAL_*"]
                include_only = ["PATH"]

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                """
                [shell_environment_policy]
                inherit = "none"
                exclude = ["PROJECT_*"]
                """);

            var options = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.Equal(CopilotCodexProjectTrustLevel.Untrusted, options.ProjectTrustLevel);
            Assert.Equal(
                CopilotCodexShellEnvironmentInherit.Core,
                options.ConfiguredShellEnvironmentPolicy.Inherit);
            Assert.Contains("GLOBAL_*", options.ConfiguredShellEnvironmentPolicy.Exclude);
            Assert.DoesNotContain("PROJECT_*", options.ConfiguredShellEnvironmentPolicy.Exclude);
            Assert.Contains("PATH", options.ConfiguredShellEnvironmentPolicy.IncludeOnly);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.CodexHome,
                options.ShellEnvironmentPolicySources);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void EnvironmentConstructionMatchesCodexOrderAndScrubsLaunchContext()
    {
        var policy = new CopilotCodexShellEnvironmentPolicy
        {
            Inherit = CopilotCodexShellEnvironmentInherit.All,
            IgnoreDefaultExcludes = false,
            Exclude = ["DROP_*"],
            Set = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["RESTORED_TOKEN"] = "configured",
                ["PUBLIC_VALUE"] = "visible",
                ["OPENAI_IDENTITY_TOKEN_FILE"] = "must-not-return",
            },
            IncludeOnly = ["PATH", "RESTORED_TOKEN", "PUBLIC_VALUE", "OPENAI_*"],
        };
        var environment = policy.CreateEnvironmentVariables(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Path"] = "C:\\Tools",
                ["OPENAI_API_KEY"] = "removed-before-include",
                ["DROP_ME"] = "removed",
                ["OTHER"] = "not-included",
                ["OPENAI_FEDERATION_RULE_ID"] = "must-not-inherit",
            },
            "thread-123");

        Assert.Equal("C:\\Tools", environment["PATH"]);
        Assert.Equal("configured", environment["RESTORED_TOKEN"]);
        Assert.Equal("visible", environment["PUBLIC_VALUE"]);
        Assert.Equal("thread-123", environment["CODEX_THREAD_ID"]);
        Assert.Equal(".COM;.EXE;.BAT;.CMD", environment["PATHEXT"]);
        Assert.DoesNotContain("OPENAI_API_KEY", environment.Keys);
        Assert.DoesNotContain("DROP_ME", environment.Keys);
        Assert.DoesNotContain("OTHER", environment.Keys);
        Assert.DoesNotContain("OPENAI_IDENTITY_TOKEN_FILE", environment.Keys);
        Assert.DoesNotContain("OPENAI_FEDERATION_RULE_ID", environment.Keys);
    }

    [Fact]
    public void MalformedMixedFilterRepresentationsFailClosed()
    {
        string globalRoot = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                """
                [shell_environment_policy]
                inherit = "all"
                exclude = ["SECRET_*"]

                [shell_environment_policy.filters]
                "PATH" = "include"
                """);

            var options = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);

            Assert.True(options.HasShellEnvironmentPolicyOverride);
            Assert.NotEmpty(options.ShellEnvironmentPolicyError);
            Assert.Equal(
                CopilotCodexShellEnvironmentInherit.None,
                options.ConfiguredShellEnvironmentPolicy.Inherit);
            Assert.False(options.ConfiguredShellEnvironmentPolicy.IgnoreDefaultExcludes);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
        }
    }

    [Fact]
    public void DiagnosticsNeverRevealConfiguredEnvironmentValues()
    {
        const string secretValue = "do-not-render-this-environment-value";
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredShellEnvironmentPolicy = new CopilotCodexShellEnvironmentPolicy
            {
                Inherit = CopilotCodexShellEnvironmentInherit.Core,
                Set = new Dictionary<string, string> { ["PRIVATE_VALUE"] = secretValue },
            },
            HasShellEnvironmentPolicyOverride = true,
            ShellEnvironmentPolicySources = CopilotProjectInstructionConfigSources.CodexHome,
        };
        string projectReport = CopilotProjectInstructionDiagnostics.Format(
            new CopilotProjectInstructionSnapshot(
                string.Empty,
                string.Empty,
                string.Empty,
                options,
                Array.Empty<CopilotProjectInstructionDocument>()),
            hasActiveAgentRun: false);
        string contextReport = CopilotContextDiagnostics.Format(new CopilotContextDiagnosticSnapshot
        {
            ProfileLabel = "Profile",
            Mode = CopilotAgentMode.Code,
            CodexShellEnvironmentPolicySummary = options.ConfiguredShellEnvironmentPolicy.BuildRedactedSummary(),
            HasCodexShellEnvironmentPolicyOverride = true,
            CodexShellEnvironmentPolicySourceLabel = options.ShellEnvironmentPolicySourceLabel,
        });
        string effectiveReport = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                ComposerMode = CopilotAgentMode.Code,
                CodexConfigOptions = options,
            });

        Assert.Contains("set=1", projectReport, StringComparison.Ordinal);
        Assert.Contains("set=1", contextReport, StringComparison.Ordinal);
        Assert.Contains("set=1", effectiveReport, StringComparison.Ordinal);
        Assert.DoesNotContain(secretValue, projectReport, StringComparison.Ordinal);
        Assert.DoesNotContain(secretValue, contextReport, StringComparison.Ordinal);
        Assert.DoesNotContain(secretValue, effectiveReport, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ForegroundAndBackgroundCommandsReceiveTheRequestEnvironment()
    {
        var policy = new CopilotCodexShellEnvironmentPolicy
        {
            Inherit = CopilotCodexShellEnvironmentInherit.None,
            Set = new Dictionary<string, string> { ["CV_ENV_POLICY_TEST"] = "configured" },
        };
        var request = new CopilotAgentRequest
        {
            ConversationId = "shell-environment-test",
            TaskId = "task",
            Profile = CopilotProfileConfig.CreateDefault(),
            PreferredShell = CopilotShellKind.CommandPrompt,
            CodexShellEnvironmentPolicy = policy,
        };
        var input = new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?>
            {
                ["command"] = "if \"%CV_ENV_POLICY_TEST%\"==\"configured\" (echo configured) else (exit /b 7)",
                ["shell"] = "cmd",
                ["timeoutSeconds"] = 10,
            },
        };

        var foreground = await new CopilotShellCommandService().ExecuteAsync(
            request,
            input,
            CancellationToken.None);
        Assert.True(foreground.Success, foreground.ErrorMessage);
        Assert.Contains("configured", foreground.Content, StringComparison.Ordinal);

        var registry = new CopilotBackgroundShellCommandRegistry();
        try
        {
            var started = await registry.StartAsync(request, input, CancellationToken.None);
            Assert.True(started.Success, started.ErrorMessage);
            var observed = await registry.WaitForObservationAsync(
                request.ConversationId,
                started.Snapshot!.Id,
                "configured",
                timeoutSeconds: 10,
                onSnapshot: null,
                CancellationToken.None);
            Assert.True(observed.Success, observed.ErrorMessage);
            Assert.Contains("configured", observed.Snapshot!.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            await registry.ShutdownAsync();
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"copilot-shell-environment-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
