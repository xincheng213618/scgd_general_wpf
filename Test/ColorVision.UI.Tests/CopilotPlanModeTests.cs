using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotPlanModeTests
{
    [Fact]
    public void PlanEnumValuesAreAppendedWithoutChangingExistingContracts()
    {
        Assert.Equal(6, (int)CopilotAgentMode.Diagnose);
        Assert.Equal(7, (int)CopilotAgentMode.Plan);
        Assert.Equal(5, (int)CopilotPromptMode.Diagnose);
        Assert.Equal(6, (int)CopilotPromptMode.Plan);
        Assert.Equal(6, (int)CopilotModuleAgentMode.Diagnose);
        Assert.Equal(7, (int)CopilotModuleAgentMode.Plan);
    }

    [Theory]
    [InlineData("/plan", "")]
    [InlineData("/plan fix the auth bug", "fix the auth bug")]
    public void PlanCommandAcceptsAnOptionalTask(string input, string expectedTask)
    {
        var invocation = CopilotLocalCommandCatalog.Parse(input);

        Assert.NotNull(invocation);
        Assert.Equal(CopilotLocalCommandKind.Plan, invocation.Command.Kind);
        Assert.True(invocation.Command.AcceptsArguments);
        Assert.Equal(expectedTask, invocation.Arguments);
    }

    [Fact]
    public void PlanModeSuppressesEveryWriteExecutionIntent()
    {
        var request = new CopilotAgentRequest
        {
            Mode = CopilotAgentMode.Plan,
            UserText = "Implement a new flow, create files, update the database, run tests, and execute the migration command.",
            WritableLocalRootPaths = [@"C:\workspace"],
            SearchRootPaths = [@"C:\workspace"],
        };

        Assert.True(CopilotToolIntentPolicy.IsReadOnlyMode(request.Mode));
        Assert.True(CopilotToolIntentPolicy.ExplicitlyDisallowsWriteAccess(request));
        Assert.True(CopilotToolIntentPolicy.NeedsTaskLedger(request));
        Assert.False(CopilotToolIntentPolicy.NeedsWorkspaceEdit(request));
        Assert.False(CopilotToolIntentPolicy.NeedsWorkspaceCreate(request));
        Assert.False(CopilotToolIntentPolicy.NeedsWorkspaceRollback(request));
        Assert.False(CopilotToolIntentPolicy.NeedsWorkspaceValidation(request));
        Assert.False(CopilotToolIntentPolicy.NeedsFlowMutation(request));
        Assert.False(CopilotToolIntentPolicy.NeedsDatabaseWrite(request));
        Assert.False(CopilotToolIntentPolicy.NeedsShellExecution(request));
        Assert.False(CopilotToolIntentPolicy.NeedsBatchImageProcessing(request));
    }

    [Fact]
    public void PlanModeKeepsReadToolsAndRejectsWriteToolsAtTheRegistryBoundary()
    {
        var request = new CopilotAgentRequest { Mode = CopilotAgentMode.Plan };

        Assert.True(CopilotToolRegistry.IsAllowedForMode(new CopilotTemplatePatchTool(), request));
        Assert.False(CopilotToolRegistry.IsAllowedForMode(new CopilotApplyTemplatePatchTool(), request));
    }

    [Fact]
    public void PlanModeStartsAndCompletesAsAnExplicitPlanningRun()
    {
        Assert.Equal("plan", CopilotMicrosoftAgentFrameworkRuntime.ResolveInitialHarnessMode(CopilotAgentMode.Plan));
        Assert.Equal("execute", CopilotMicrosoftAgentFrameworkRuntime.ResolveInitialHarnessMode(CopilotAgentMode.Auto));

        var ledger = new CopilotAgentTaskLedgerSnapshot
        {
            Mode = "plan",
            Items =
            [
                new CopilotAgentTaskItem { Id = 1, Title = "Implement the change" },
                new CopilotAgentTaskItem { Id = 2, Title = "Run focused tests" },
            ],
        };
        var budget = new CopilotAgentBudgetSnapshot();

        Assert.Equal(
            CopilotAgentStopReason.Completed,
            CopilotMicrosoftAgentFrameworkRuntime.DetermineStopReason(
                ledger,
                budget,
                Array.Empty<CopilotAgentStepRecord>(),
                hasModelFinalAnswer: true,
                requestMode: CopilotAgentMode.Plan));
        Assert.Equal(
            CopilotAgentStopReason.AwaitingUser,
            CopilotMicrosoftAgentFrameworkRuntime.DetermineStopReason(
                ledger,
                budget,
                Array.Empty<CopilotAgentStepRecord>(),
                hasModelFinalAnswer: true));
    }

    [Fact]
    public void PlanModeInstructionRequiresAnImplementationReadyReadOnlyPlan()
    {
        var instruction = CopilotAgentContextBuilder.BuildModeInstruction(CopilotAgentMode.Plan);

        Assert.Contains("plan-only", instruction, StringComparison.Ordinal);
        Assert.Contains("implementation-ready plan", instruction, StringComparison.Ordinal);
        Assert.Contains("Never modify files or application state", instruction, StringComparison.Ordinal);
        Assert.Contains("Never", instruction, StringComparison.Ordinal);
        Assert.Contains("testing occurred", instruction, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanHarnessRemainsInPlanAndTreatsTodosAsProposedWork()
    {
        var request = new CopilotAgentRequest
        {
            Mode = CopilotAgentMode.Plan,
            UserText = "Plan the requested implementation.",
        };

        var instructions = CopilotMicrosoftAgentFrameworkRuntime.BuildHarnessInstructions(
            request,
            [new CopilotTemplatePatchTool()],
            CopilotAgentEnvironmentContext.Capture(request),
            taskLedgerEnabled: true,
            agentModeEnabled: true);

        Assert.Contains("Remain in plan mode", instructions, StringComparison.Ordinal);
        Assert.Contains("planned steps, not completed work", instructions, StringComparison.Ordinal);
        Assert.Contains("Do not switch to execute mode", instructions, StringComparison.Ordinal);
        Assert.DoesNotContain("Use execute mode for authorized work", instructions, StringComparison.Ordinal);
    }
}
