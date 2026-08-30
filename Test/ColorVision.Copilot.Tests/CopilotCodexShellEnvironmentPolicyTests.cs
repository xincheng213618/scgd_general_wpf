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
    public void PolicyAssignmentsFreezeTheSubmittedEnvironmentRules()
    {
        var exclude = new List<string> { "SECRET_*" };
        var set = new Dictionary<string, string> { ["CV_FIXED"] = "original" };
        var includeOnly = new List<string> { "PATH" };
        var policy = new CopilotCodexShellEnvironmentPolicy
        {
            Exclude = exclude,
            Set = set,
            IncludeOnly = includeOnly,
        };

        exclude[0] = "PUBLIC_*";
        set["CV_FIXED"] = "mutated";
        set["CV_ADDED"] = "late";
        includeOnly.Clear();

        Assert.Equal("SECRET_*", Assert.Single(policy.Exclude));
        Assert.Equal("original", Assert.Single(policy.Set).Value);
        Assert.Equal("PATH", Assert.Single(policy.IncludeOnly));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<string>)policy.Exclude)[0] = "MUTATED_*");
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<string, string>)policy.Set)["CV_ADDED"] = "late");
        Assert.Throws<NotSupportedException>(() =>
            ((IList<string>)policy.IncludeOnly).Clear());
    }

    [Fact]
    public void DefaultPolicyScrubsAmbientCredentialShapedNames()
    {
        var environment = CopilotCodexShellEnvironmentPolicy.Default.CreateEnvironmentVariables(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PATH"] = "C:\\Tools",
                ["SERVICE_API_KEY"] = "secret",
                ["DATABASE_PASSWORD"] = "secret",
                ["CLIENT_SECRET"] = "secret",
                ["ACCESS_TOKEN"] = "secret",
                ["PUBLIC_VALUE"] = "visible",
            },
            conversationId: null);

        Assert.False(CopilotCodexShellEnvironmentPolicy.Default.IgnoreDefaultExcludes);
        Assert.Equal("C:\\Tools", environment["PATH"]);
        Assert.Equal("visible", environment["PUBLIC_VALUE"]);
        Assert.DoesNotContain("SERVICE_API_KEY", environment.Keys);
        Assert.DoesNotContain("DATABASE_PASSWORD", environment.Keys);
        Assert.DoesNotContain("CLIENT_SECRET", environment.Keys);
        Assert.DoesNotContain("ACCESS_TOKEN", environment.Keys);

        var explicitlyUnfiltered = new CopilotCodexShellEnvironmentPolicy
        {
            IgnoreDefaultExcludes = true,
        }.CreateEnvironmentVariables(
            new Dictionary<string, string> { ["DATABASE_PASSWORD"] = "configured" },
            conversationId: null);
        Assert.Equal("configured", explicitlyUnfiltered["DATABASE_PASSWORD"]);
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
}
