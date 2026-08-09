using ColorVision.Copilot;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ColorVision.UI.Tests;

public sealed class CopilotAppliedSandboxModeTests
{
    [Fact]
    public void CapturedAgentSandboxModePersistsAndExplainsTheAppliedConstraint()
    {
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "Completed.")
        {
            RequestMode = CopilotAgentMode.Auto,
        };

        assistant.CaptureAppliedCodexSandboxMode(CopilotCodexSandboxMode.ReadOnly);
        var json = JsonConvert.SerializeObject(assistant);
        var restored = JsonConvert.DeserializeObject<CopilotChatMessage>(json);

        Assert.Equal("read-only", assistant.AppliedCodexSandboxMode);
        Assert.True(assistant.HasAppliedCodexSandboxMode);
        Assert.True(assistant.HasAgentRunMetrics);
        Assert.Contains("Codex 沙箱约束：read-only", assistant.AgentRunMetricsToolTip, StringComparison.Ordinal);
        Assert.Contains("写工具不暴露", assistant.AgentRunMetricsToolTip, StringComparison.Ordinal);
        Assert.NotNull(JObject.Parse(json)[nameof(CopilotChatMessage.AppliedCodexSandboxMode)]);
        Assert.NotNull(restored);
        restored.EnsureValid();
        Assert.Equal("read-only", restored.AppliedCodexSandboxMode);
    }

    [Fact]
    public void UnconfiguredAgentSandboxModeStillRecordsTheNativeBoundary()
    {
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "Completed.")
        {
            RequestMode = CopilotAgentMode.Plan,
        };

        assistant.CaptureAppliedCodexSandboxMode(CopilotCodexSandboxMode.Unspecified);

        Assert.Equal("unspecified", assistant.AppliedCodexSandboxMode);
        Assert.Contains("保留 ColorVision 原生访问与审批边界", assistant.AgentRunMetricsToolTip, StringComparison.Ordinal);
        Assert.Contains(
            nameof(CopilotChatMessage.AppliedCodexSandboxMode),
            JsonConvert.SerializeObject(assistant),
            StringComparison.Ordinal);
    }

    [Fact]
    public void SandboxModeSnapshotNormalizesKnownTokensAndRejectsUnknownValues()
    {
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "Completed.")
        {
            RequestMode = CopilotAgentMode.Review,
            AppliedCodexSandboxMode = " WORKSPACE-WRITE ",
        };

        Assert.Equal("workspace-write", assistant.AppliedCodexSandboxMode);

        assistant.AppliedCodexSandboxMode = "host-defined";

        Assert.Empty(assistant.AppliedCodexSandboxMode);
        Assert.False(assistant.HasAppliedCodexSandboxMode);
        Assert.DoesNotContain(
            nameof(CopilotChatMessage.AppliedCodexSandboxMode),
            JsonConvert.SerializeObject(assistant),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CopilotChatRole.User, CopilotAgentMode.Auto)]
    [InlineData(CopilotChatRole.Assistant, CopilotAgentMode.Chat)]
    public void ValidationRemovesAgentSandboxStateFromUnsupportedMessageRoles(
        CopilotChatRole role,
        CopilotAgentMode mode)
    {
        var message = new CopilotChatMessage(role, "Message")
        {
            RequestMode = mode,
            AppliedCodexSandboxMode = "danger-full-access",
        };

        Assert.True(message.EnsureValid());
        Assert.Empty(message.AppliedCodexSandboxMode);
        Assert.False(message.HasAppliedCodexSandboxMode);
        Assert.DoesNotContain(
            nameof(CopilotChatMessage.AppliedCodexSandboxMode),
            JsonConvert.SerializeObject(message),
            StringComparison.Ordinal);
    }
}
