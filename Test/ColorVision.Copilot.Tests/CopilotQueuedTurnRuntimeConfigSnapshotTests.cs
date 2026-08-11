using ColorVision.Copilot;
using System.Collections.ObjectModel;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotQueuedTurnRuntimeConfigSnapshotTests
{
    [Fact]
    public void QueuedFollowUpKeepsSubmittedAgentDefaultsAndExternalMcpPolicy()
    {
        var defaults = new CopilotAgentDefaultsConfig
        {
            ContextWindowTokens = 96_000,
            RequestTokenBudget = 24_000,
            PreferredShell = CopilotShellKind.PowerShell,
            SkillOverrides = new ObservableCollection<CopilotAgentSkillOverrideConfig>
            {
                new()
                {
                    Name = "review",
                    State = CopilotAgentSkillOverrideState.On,
                },
            },
        };
        var server = new CopilotMcpClientServerConfig
        {
            Name = "submitted-docs",
            Endpoint = "https://example.test/mcp",
            AccessPolicy = CopilotMcpClientAccessPolicy.ReadOnly,
        };
        var runtimeConfig = new CopilotTurnRuntimeConfigSnapshot(defaults, [server]);
        var queuedFollowUp = new CopilotQueuedFollowUp(
            "run-1",
            "conversation-1",
            "Conversation",
            "Continue the submitted turn.",
            CopilotAgentMode.Code,
            CopilotProfileConfig.CreateDefault(),
            new CopilotAgentHostContextSnapshot("", "", []),
            runtimeConfigSnapshot: runtimeConfig);

        defaults.ContextWindowTokens = 32_000;
        defaults.RequestTokenBudget = 4_000;
        defaults.SkillOverrides[0].State = CopilotAgentSkillOverrideState.Off;
        server.Name = "changed";
        server.Enabled = false;

        var queuedDefaults = queuedFollowUp.RuntimeConfigSnapshot.CreateAgentDefaultsSnapshot();
        var queuedServers = queuedFollowUp.RuntimeConfigSnapshot.CreateExternalMcpServerSnapshots();

        Assert.Equal(96_000, queuedDefaults.ContextWindowTokens);
        Assert.Equal(24_000, queuedDefaults.RequestTokenBudget);
        Assert.Equal(CopilotShellKind.PowerShell, queuedDefaults.PreferredShell);
        Assert.Equal(CopilotAgentSkillOverrideState.On, Assert.Single(queuedDefaults.SkillOverrides).State);
        var queuedServer = Assert.Single(queuedServers);
        Assert.Equal("submitted-docs", queuedServer.Name);
        Assert.True(queuedServer.Enabled);
        Assert.Equal(CopilotMcpClientAccessPolicy.ReadOnly, queuedServer.AccessPolicy);

        queuedDefaults.RequestTokenBudget = 8_000;
        queuedServer.Enabled = false;
        Assert.Equal(
            24_000,
            queuedFollowUp.RuntimeConfigSnapshot.CreateAgentDefaultsSnapshot().RequestTokenBudget);
        Assert.True(Assert.Single(
            queuedFollowUp.RuntimeConfigSnapshot.CreateExternalMcpServerSnapshots()).Enabled);
    }
}
