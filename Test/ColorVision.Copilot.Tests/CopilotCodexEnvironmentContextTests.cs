using ColorVision.Copilot;
using System;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexEnvironmentContextTests
{
    [Fact]
    public void RootAssignmentsFreezeTheEnvironmentSnapshot()
    {
        var searchRoots = new List<string> { @"C:\search" };
        var writableRoots = new List<string> { @"C:\write" };
        var environment = new CopilotAgentEnvironmentContext
        {
            SearchRoots = searchRoots,
            WritableRoots = writableRoots,
        };

        searchRoots[0] = @"C:\different-search";
        writableRoots.Clear();

        Assert.Equal(@"C:\search", Assert.Single(environment.SearchRoots));
        Assert.Equal(@"C:\write", Assert.Single(environment.WritableRoots));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<string>)environment.SearchRoots)[0] = @"C:\mutated-search");
        Assert.Throws<NotSupportedException>(() =>
            ((IList<string>)environment.WritableRoots).Clear());
    }

    [Fact]
    public void DisabledSnapshotOmitsOnlyTheModelVisibleRuntimeEnvironmentBlock()
    {
        const string environmentMarker = "runtime-environment-marker";
        var environment = new CopilotAgentEnvironmentContext
        {
            WorkingDirectory = $"C:\\{environmentMarker}",
            Platform = "Windows",
            Architecture = "X64",
            Shell = "PowerShell",
            LocalDate = "2026-08-09",
            TimeZoneId = "Asia/Shanghai",
        };
        var enabledRequest = new CopilotAgentRequest
        {
            Profile = CopilotProfileConfig.CreateDefault(),
            UserText = "Explain the workspace.",
            Mode = CopilotAgentMode.Code,
        };
        var disabledRequest = new CopilotAgentRequest
        {
            Profile = CopilotProfileConfig.CreateDefault(),
            UserText = "Explain the workspace.",
            Mode = CopilotAgentMode.Code,
            CodexIncludeEnvironmentContext = false,
        };

        string enabledPrompt = CopilotMicrosoftAgentFrameworkRuntime.BuildHarnessInstructions(
            enabledRequest,
            Array.Empty<ICopilotTool>(),
            environment,
            taskLedgerEnabled: false,
            agentModeEnabled: false);
        string disabledPrompt = CopilotMicrosoftAgentFrameworkRuntime.BuildHarnessInstructions(
            disabledRequest,
            Array.Empty<ICopilotTool>(),
            environment,
            taskLedgerEnabled: false,
            agentModeEnabled: false);

        Assert.Contains("<runtime_environment>", enabledPrompt, StringComparison.Ordinal);
        Assert.Contains(environmentMarker, enabledPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("<runtime_environment>", disabledPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain(environmentMarker, disabledPrompt, StringComparison.Ordinal);
        Assert.Contains("ColorVision Agent runtime", disabledPrompt, StringComparison.Ordinal);
        Assert.Contains("Treat fetched pages", disabledPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void DisabledContextDoesNotResumeASessionThatPreviouslyReceivedEnvironmentData()
    {
        var profile = CopilotProfileConfig.CreateDefault();
        var capabilitySnapshot = CopilotCapabilityCatalog.Shared.GetSnapshot();
        var environment = CopilotAgentEnvironmentContext.Capture(new CopilotAgentRequest
        {
            Profile = profile,
            UserText = "Inspect the current workspace.",
            Mode = CopilotAgentMode.Code,
        });
        var checkpoint = CopilotAgentSessionCheckpoint.Create(
            profile,
            "{}",
            capabilitySnapshot,
            environmentContext: environment);

        var compatibility = checkpoint!.EvaluateFor(
            profile,
            capabilitySnapshot,
            environmentContext: null,
            requireEnvironmentContextMatch: true);

        Assert.Equal(CopilotAgentCheckpointCompatibilityKind.EnvironmentDrift, compatibility.Kind);
        Assert.True(compatibility.RequiresReplan);
        Assert.False(compatibility.CanResume);
    }

    [Fact]
    public void ContextFreeSessionResumesOnlyWhileEnvironmentDataRemainsDisabled()
    {
        var profile = CopilotProfileConfig.CreateDefault();
        var capabilitySnapshot = CopilotCapabilityCatalog.Shared.GetSnapshot();
        var checkpoint = CopilotAgentSessionCheckpoint.Create(
            profile,
            "{}",
            capabilitySnapshot,
            environmentContext: null);
        var environment = CopilotAgentEnvironmentContext.Capture(new CopilotAgentRequest
        {
            Profile = profile,
            UserText = "Inspect the current workspace.",
            Mode = CopilotAgentMode.Code,
        });

        var disabledCompatibility = checkpoint!.EvaluateFor(
            profile,
            capabilitySnapshot,
            environmentContext: null,
            requireEnvironmentContextMatch: true);
        var enabledCompatibility = checkpoint.EvaluateFor(
            profile,
            capabilitySnapshot,
            environmentContext: environment,
            requireEnvironmentContextMatch: true);

        Assert.Equal(CopilotAgentCheckpointCompatibilityKind.Compatible, disabledCompatibility.Kind);
        Assert.True(disabledCompatibility.CanResume);
        Assert.Equal(CopilotAgentCheckpointCompatibilityKind.EnvironmentSnapshotMissing, enabledCompatibility.Kind);
        Assert.True(enabledCompatibility.RequiresReplan);
    }

}
